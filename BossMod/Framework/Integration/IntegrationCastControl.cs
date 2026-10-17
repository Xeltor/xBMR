using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace BossMod.Integration;

// Current native policy and producer-recorded cast links, not action permission or encounter coverage.
internal static class IntegrationCastControl
{
    internal const int MaxZones = 128;
    internal const int MaxPoints = 16;
    private sealed class Identity { internal readonly string Value = Guid.NewGuid().ToString("N"); }
    private static readonly ConditionalWeakTable<ActorCastInfo, Identity> Identities = new();
    private static string Id(ActorCastInfo cast) => Identities.GetOrCreateValue(cast).Value;
    private static string T(DateTime time) => time.Ticks.ToString(CultureInfo.InvariantCulture);
    private static float? F(float value) => float.IsFinite(value) && value != float.MaxValue ? value : null;

    internal sealed record Cast(string CastId, uint ActionId, int ActionType, string TargetId, float? RemainingSeconds,
        bool Interruptible, bool EventHappened, string? DeadlineTicks, string DeadlineKind, float? ReportedRemainingSeconds);

    internal static Cast? ObserveCast(ActorCastInfo? cast, DateTime now)
    {
        if (cast == null) return null;
        string? deadline = null;
        var remaining = cast.NPCRemainingTime;
        if (float.IsFinite(cast.TotalTime) && float.IsFinite(cast.ElapsedTime) && float.IsFinite(remaining) && remaining >= 0)
        {
            try { deadline = T(now.AddSeconds(remaining)); }
            catch (ArgumentOutOfRangeException) { }
        }
        return new(Id(cast), cast.Action.ID, (int)cast.Action.Type, cast.TargetID.ToString("X"), F(remaining),
            cast.Interruptible, cast.EventHappened, deadline, now.Kind.ToString(), F(cast.RemainingTime));
    }

    internal sealed record Target(string ActorId, int Priority, bool PendingDead, bool IsTargetable,
        bool InterruptRequested, bool StunRequested, Cast? Cast);
    internal sealed record Zone(int Index, string? SourceActorId, string? CastId, uint? ActionId, string Correlation,
        string? ActivationTicks, float?[] Distances);

    internal static object Capture(AIHints hints, DateTime now, WPos[] points, IntegrationSession.Context context,
        Func<long>? clock = null, long frequency = 0)
    {
        clock ??= Stopwatch.GetTimestamp;
        frequency = frequency == 0 ? Stopwatch.Frequency : frequency;
        var started = clock();
        var targets = hints.PotentialTargets.Take(AIHints.NumEnemies).Select(t =>
            new Target(t.Actor.InstanceID.ToString("X"), t.Priority, t.Actor.PendingDead, t.Actor.IsTargetable,
                t.ShouldBeInterrupted, t.ShouldBeStunned, ObserveCast(t.Actor.CastInfo, now))).ToArray();
        var zones = new List<Zone>();
        var complete = hints.PotentialTargets.Count <= AIHints.NumEnemies && hints.ForbiddenZones.Count <= MaxZones && !hints.CastSourcesTruncated;
        var evaluations = 0;
        foreach (var entry in hints.ForbiddenZones.Take(MaxZones))
        {
            if ((clock() - started) / (double)frequency > 0.004) { complete = false; break; }
            var source = hints.FindCastSource(entry.shapeDistance, entry.activation, entry.Source);
            var distances = new float?[points.Length];
            for (var i = 0; i < points.Length; ++i)
            {
                if ((clock() - started) / (double)frequency > 0.004) { complete = false; break; }
                ++evaluations;
                try { distances[i] = F(entry.shapeDistance.Distance(points[i])); }
                catch { /* A failed native distance remains unknown, never outside/safe. */ }
            }
            zones.Add(new(zones.Count, entry.Source == 0 ? null : entry.Source.ToString("X"), source is { } s ? Id(s.Cast) : null,
                source?.Action.ID, source != null ? "producer-recorded-cast" : entry.Source == 0 ? "unknown" : "source-actor-only",
                entry.activation == default ? null : T(entry.activation), distances));
        }
        return new
        {
            available = true, context, worldTime = new { ticks = T(now), kind = now.Kind.ToString() },
            complete, targets, zones, evaluations, sourceZoneCount = hints.ForbiddenZones.Count,
            pointQueriesComplete = complete && zones.All(z => z.Distances.All(d => d != null)),
            points = points.Select(p => new[] { p.X, p.Z }).ToArray(),
            castThreatCoverageComplete = false,
            timing = "deadline is native NPC remaining time including NPCFinishDelay; not a server snapshot or last-safe action deadline",
            meaning = "distance <= 0 is published point containment; values are not general Euclidean clearance; null is unknown; exact cast links only at recorded producers; absence is not safety or interrupt/stun permission"
        };
    }
}
