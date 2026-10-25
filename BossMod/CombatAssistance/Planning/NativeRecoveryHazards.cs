using BossMod.Pathfinding;
using System.Diagnostics;
using System.Threading;

namespace BossMod.CombatAssistance.Planning;

// Frozen independent native warning footprints. Aggregate earliest deadlines
// alone cannot distinguish crossing one failed mechanic from crossing two.
// Geometry comes from the existing native rasterizer; transit uses TimedTraversal.
internal sealed class NativeRecoveryHazards(ImmutableArray<NativeRecoveryHazards.Warning> warnings)
{
    internal bool Same(NativeRecoveryHazards other, float age)
        => warnings.Length == other.WarningCount && warnings.Select((w, i) =>
            Math.Abs(w.Deadline - Math.Max(0, other.WarningAt(i).Deadline - age)) <= .002f
            && w.Cells.AsSpan().SequenceEqual(other.WarningAt(i).Cells.AsSpan())).All(same => same);
    private int WarningCount => warnings.Length;
    private Warning WarningAt(int index) => warnings[index];

    internal sealed record Warning(float Deadline, ImmutableArray<ulong> Cells)
    {
        internal bool Contains(int cell) => (Cells[cell >> 6] & (1ul << (cell & 63))) != 0;
    }

    internal static NativeRecoveryHazards? Capture(Map map, NavigationDecision.CapturedInput input)
    {
        var start = map.WorldToGrid(input.Identity.Origin);
        if (input.Warnings.Length < 2 || !map.InBounds(start.x, start.y)) return null;
        var count = map.Width * map.Height;
        var captured = ImmutableArray.CreateBuilder<Warning>();
        var scratch = new Map();
        scratch.Init(map, map.Center);
        foreach (var zone in input.Warnings)
        {
            Array.Fill(scratch.PixelMaxG, float.MaxValue, 0, count);
            NavigationDecision.RasterizeForbiddenZones(scratch, [zone], input.Identity.WorldTime, false, out _,
                allowRelaxation: false, parallel: false, activationTimeCushion: input.WarningTimeCushion);
            var bits = new ulong[(count + 63) >> 6];
            var deadline = float.MaxValue;
            for (var cell = 0; cell < count; ++cell)
                if (scratch.PixelMaxG[cell] != float.MaxValue)
                {
                    bits[cell >> 6] |= 1ul << (cell & 63);
                    deadline = Math.Min(deadline, scratch.PixelMaxG[cell]);
                }
            if (deadline != float.MaxValue) captured.Add(new(deadline, [.. bits]));
        }
        return captured.Count > 1 ? new(captured.ToImmutable()) : null;
    }

    // Reuse native ThetaStar for alternatives. Each improvement blocks one more
    // avoidable failed warning; at most the number of captured warnings can be
    // added. The existing 250ms input ceiling bounds production work, not a new
    // arbitrary expansion quota. This is best-found emergency recovery, never
    // an exhaustive optimum or permission to ignore physical/manual constraints.
    internal NavigationDecision Improve(Map map, NavigationIdentity identity, NavigationDecision original,
        Func<Map, NavigationDecision> recover, CancellationToken cancellation, bool respectFreshness = true)
    {
        if (original.Publication is not { WitnessComplete: true, HasTeleporters: false } publication) return original;
        var count = map.Width * map.Height;
        var start = map.GridToIndex(map.WorldToGrid(identity.Origin));
        var scratch = new Map();
        scratch.Init(map, map.Center);
        var best = original;
        var failed = Assess(original);
        if (failed.Count == 0) return Annotate(best, 0);
        var candidates = failed.ToList();
        var lowerBound = warnings.Count(w => w.Deadline <= 0 && w.Contains(start));
        var blocked = new HashSet<int>();

        // Holding in one unavoidable mechanic can be preferable to a route
        // crossing two. It remains an explicit panic hold, never "arrived safe".
        if (TimedTraversal.Check(map, identity.Origin, identity.Origin, 0, 0, 0, out _, physicalOnly: true))
        {
            var hold = new NavigationDecision { LeewaySeconds = 0 };
            hold.Publication = new(identity, publication.Raster, [identity.Origin], true, publication.NativeRelaxedWarnings,
                ThetaStar.Score.UnsafeAsStart, hold, false, publication.MapSource) { RecoveryHoldForFewerExposures = true };
            var held = Assess(hold);
            if (held.Count < failed.Count) { best = hold; failed = held; }
        }

        var firstPass = true;
        while (firstPass || failed.Count > lowerBound)
        {
            firstPass = false;
            NavigationDecision? improved = null;
            List<int>? improvedFailed = null;
            var avoided = -1;
            foreach (var index in candidates)
            {
                cancellation.ThrowIfCancellationRequested();
                if (respectFreshness && Stopwatch.GetElapsedTime(identity.CapturedTimestamp).TotalSeconds >= .25) break;
                // Never wall in the occupied warning: native egress must remain
                // possible. These alternatives avoid adding another mechanic.
                if (blocked.Contains(index) || warnings[index].Contains(start)) continue;
                var alternative = new Map();
                alternative.Init(map, map.Center);
                alternative.MaxPriority = float.MinValue;
                for (var cell = 0; cell < count; ++cell)
                {
                    if (warnings[index].Contains(cell) || blocked.Any(b => warnings[b].Contains(cell)))
                        alternative.PixelMaxG[cell] = -1;
                    if (alternative.PixelMaxG[cell] >= 0)
                        alternative.MaxPriority = Math.Max(alternative.MaxPriority, alternative.PixelPriority[cell]);
                }
                var candidate = recover(alternative);
                if (candidate.Publication?.WitnessComplete != true) continue;
                var exposures = Assess(candidate);
                // A bounded native pass may have no onward route. Keep the
                // honest hold above rather than relabeling that as safe arrival.
                if (candidate.Destination == null) continue;
                if (Better(candidate, exposures.Count, best, failed.Count)
                    && (improvedFailed == null || Better(candidate, exposures.Count, improved!.Value, improvedFailed.Count)))
                { improved = candidate; improvedFailed = exposures; avoided = index; }
            }
            if (improved == null) break;
            best = improved.Value;
            failed = improvedFailed!;
            candidates = failed.ToList();
            blocked.Add(avoided);
        }
        return Annotate(best, failed.Count);

        bool Better(NavigationDecision candidate, int exposure, NavigationDecision before, int beforeExposure)
        {
            if (exposure != beforeExposure) return exposure < beforeExposure;
            int AtEnd(NavigationDecision decision)
            {
                var cell = map.WorldToGrid(decision.Publication!.Endpoint);
                return map.InBounds(cell.x, cell.y) ? warnings.Count(w => w.Contains(map.GridToIndex(cell))) : int.MaxValue;
            }
            var endpoint = AtEnd(candidate); var priorEndpoint = AtEnd(before);
            if (endpoint != priorEndpoint) return endpoint < priorEndpoint;
            return candidate.TimeToGoal < before.TimeToGoal;
        }

        List<int> Assess(NavigationDecision decision)
        {
            var result = new List<int>();
            if (decision.Publication is not { WitnessComplete: true } source) return Enumerable.Range(0, warnings.Length).ToList();
            var witness = source.CopyWitness();
            var end = map.WorldToGrid(source.Endpoint);
            if (witness.Length == 0 || !map.InBounds(end.x, end.y)) return Enumerable.Range(0, warnings.Length).ToList();
            var endpoint = map.GridToIndex(end);
            for (var w = 0; w < warnings.Length; ++w)
            {
                var warning = warnings[w];
                // Ending inside a warning still requires another escape; a
                // zero-duration stationary witness cannot claim zero exposure.
                var exposed = warning.Contains(endpoint);
                for (var cell = 0; !exposed && cell < count; ++cell)
                    scratch.PixelMaxG[cell] = warning.Contains(cell) ? warning.Deadline : float.MaxValue;
                var elapsed = 0f;
                for (var leg = 1; !exposed && leg < witness.Length; ++leg)
                {
                    var duration = (witness[leg] - witness[leg - 1]).Length() / identity.Speed;
                    exposed = !TimedTraversal.Check(scratch, witness[leg - 1], witness[leg], elapsed, duration, 0, out _);
                    elapsed += duration;
                }
                if (exposed) result.Add(w);
            }
            return result;
        }

        static NavigationDecision Annotate(NavigationDecision decision, int exposures)
        {
            decision.Publication = decision.Publication!.WithRecoveryExposure(exposures);
            return decision;
        }
    }
}
