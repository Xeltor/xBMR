using System.Threading;

namespace BossMod.Integration;

internal sealed partial class IntegrationSession
{
    internal const int MaxNoLossLeaves = 16;
    internal const int MaxNoLossBaselines = 8;
    internal sealed record NoLossRequest(Context Context, string OwnerToken, string Category, float[][] Baselines);
    internal sealed record NoLossItem(float[][]? Baselines);
    internal sealed record NoLossBatch(Context Context, string OwnerToken, string Category, NoLossItem[] Items);
    internal sealed record NoLossLeaf(Func<Vector2, float> Score, Vector2[]? Baselines);

    internal Result SubmitNoLoss(NoLossRequest? request, Func<Vector2, float>? score)
        => SubmitNoLossBatch(request == null ? null : new(request.Context, request.OwnerToken, request.Category,
            [new(request.Baselines)]), score == null ? null : [score], requireGuard: true);

    internal Result SubmitNoLossBatch(NoLossBatch? request, Func<Vector2, float>[]? scores, bool requireGuard = false)
    {
        if (request == null || request.Items is not { Length: >= 1 and <= MaxNoLossLeaves } || scores == null || scores.Length != request.Items.Length
            || scores.Any(s => s == null)) return Reply(false, "invalid-no-loss-batch");
        var leaves = new NoLossLeaf[scores.Length];
        for (var i = 0; i < leaves.Length; ++i)
        {
            if (request.Items[i] == null) return Reply(false, "invalid-no-loss-item");
            var points = request.Items[i].Baselines;
            if ((requireGuard && points == null) || points is { Length: < 1 or > MaxNoLossBaselines })
                return Reply(false, "invalid-no-loss-baselines");
            if (points != null && points.Any(p => p == null || p.Length != 2 || p.Any(x => !float.IsFinite(x) || Math.Abs(x) > 1_000_000)))
                return Reply(false, "invalid-no-loss-point");
            leaves[i] = new(scores[i], points?.Select(p => new Vector2(p[0], p[1])).ToArray());
        }
        lock (_gate) return SubmitCore(new(request.Context, request.OwnerToken, request.Category), static _ => 0, leaves);
    }

    // Preserve individual execution leaves for final arrival comparisons; a sum
    // must not hide one improving assignment behind a different losing leaf.
    internal Func<Vector2, float>[] CaptureIndividualGoalLeaves()
    {
        lock (_gate)
        {
            if (!OnThread || !CanContribute) return [];
            var revision = Volatile.Read(ref _goalRevision);
            return _owners.Values.Where(Live).SelectMany(o => o.Goals).Where(g => g.Frame == FrameId)
                .SelectMany(g => g.NoLossLeaves == null ? new[] { Wrap(g, revision, g.Score, comparison: true) }
                    : g.NoLossLeaves.Select(leaf => Wrap(g, revision, leaf.Score, comparison: true))).ToArray();
        }
    }

    private Func<Vector2, float> CaptureNoLossScore(Goal goal, Func<Vector2[], Func<Vector2, bool>?>? captureGuard)
    {
        if (goal.NoLossLeaves == null) return goal.Score;
        var leaves = goal.NoLossLeaves.Select(leaf => (leaf.Score,
            Guard: leaf.Baselines == null ? null : captureGuard?.Invoke(leaf.Baselines) ?? (static _ => false))).ToArray();
        return point =>
        {
            var sum = 0f;
            long consumerTicks = 0;
            foreach (var (score, guard) in leaves)
            {
                // Native no-loss comparisons have their own bounded budget.
                // Charging that work to the consumer can revoke a healthy
                // observer even when its score delegate never runs.
                if (guard != null && !guard(point)) continue;
                var start = _clock();
                var value = score(point);
                consumerTicks += _clock() - start;
                if (!float.IsFinite(value) || value is < 0 or > 16) return float.NaN;
                if (consumerTicks > Milliseconds(2))
                {
                    Interlocked.Exchange(ref goal.BudgetExceeded, 1);
                    return 0f;
                }
                sum += value;
            }
            return Math.Min(16, sum);
        };
    }
}
