using BossMod.Components;

namespace BossMod.Pathfinding;

internal static class NativeMarchSafety
{
    internal sealed record Segment(WPos From, WPos To, float Start, float End);
    internal sealed record Result(float Heading, string Status, string Reason, ImmutableArray<WPos> Route, float? EndsIn = null, ImmutableArray<Segment> TimedLegs = default);
    internal sealed record Source(AIHints Hints, BossModule Module, GenericForcedMarch March, Actor Actor, int Slot,
        NativeRouteValidation.Source Terrain);

    internal static Result Evaluate(Source source, float heading, NativeRouteValidation.Budget budget)
    {
        Result Fail(string status, string reason) => new(heading, status, reason, []);
        if (!float.IsFinite(heading) || !budget.Step()) return Fail("unknown", "invalid-heading-or-budget");
        var actor = source.Actor; var march = source.March; var world = source.Module.WorldState;
        if (!march.HasForcedMovements(actor) || march.OverrideDirection || !float.IsFinite(march.MovementSpeed) || march.MovementSpeed <= 0)
            return Fail("unknown", "invalid-native-march-state");
        foreach (var shape in source.Hints.TemporaryObstacles)
            if (!float.IsFinite(shape.ConservativeLipschitz) || shape.ConservativeLipschitz < 0) return Fail("unknown", "native-distance-bound-missing");
        var copy = actor.CopyForProjection(actor.Position, heading.Radians());
        var projected = march.ProjectFromPose(copy, copy.Position, copy.Rotation);
        if (projected is not { Count: >= 1 }) return Fail("unknown", "no-native-projection");
        var segments = new List<Segment>(); var previous = actor.Position; var elapsed = 0f;
        var route = new List<WPos> { previous };
        foreach (var step in projected)
        {
            if (!budget.Step()) return Fail("unknown", "native-march-budget");
            var start = (float)(step.Activation - world.CurrentTime).TotalSeconds;
            var end = (float)(step.End - world.CurrentTime).TotalSeconds;
            var distance = (step.To - step.From).Length();
            if (!float.IsFinite(start) || !float.IsFinite(end) || !float.IsFinite(distance) || start < elapsed - 0.001f || end < start
                || (step.From - previous).LengthSq() > 0.0001f)
                return Fail("unknown", "nonsequential-native-march-timing");
            if (start > elapsed) segments.Add(new(previous, previous, elapsed, start));
            // Wall clipping ends travel early, then the native leg remains stationary until its authored end.
            var arrival = start + distance / march.MovementSpeed;
            if (arrival > end + 0.01f) return Fail("unknown", "native-march-speed-mismatch");
            arrival = Math.Min(arrival, end);
            segments.Add(new(step.From, step.To, start, arrival));
            if (arrival < end) segments.Add(new(step.To, step.To, arrival, end));
            if (march.DestinationUnsafe(source.Slot, copy, step.To)) return Fail("blocked", "native-destination-unsafe");
            previous = step.To; elapsed = end; route.Add(previous);
        }
        segments.Add(new(previous, previous, elapsed, elapsed + 0.25f));
        var clearance = Math.Max(0.6f, actor.HitboxRadius + 0.05f);
        if (!float.IsFinite(clearance) || actor.HitboxRadius < 0) return Fail("unknown", "invalid-native-footprint");
        foreach (var segment in segments)
        {
            var physical = NativeRouteValidation.CheckPhysical(source.Terrain, segment.From, segment.To, clearance, 0, budget);
            if (physical != null) return Fail(physical.Status, physical.Reason);
            // Native arena geometry remains binding even if a map cell is permissive.
            var pending = new Stack<(WPos A, WPos B)>(); pending.Push((segment.From, segment.To));
            while (pending.Count != 0)
            {
                if (!budget.Step()) return Fail("unknown", "native-march-budget");
                var (a, b) = pending.Pop(); var mid = a + (b - a) * 0.5f;
                var arena = NativeArenaGeometry.Query(source.Hints.PathfindMapBounds, source.Hints.PathfindMapCenter,
                    source.Hints.PathfindMapArenaProjectionLayer, mid, 0);
                if (!arena.Available || arena.SignedClearance is not { } margin) return Fail("unknown", "native-arena-distance-missing");
                var half = (b - a).Length() * 0.5f;
                if (margin > clearance + half) continue;
                if (half <= 0.025f) return Fail("blocked", "native-arena-clearance");
                pending.Push((mid, b)); pending.Push((a, mid));
            }
            foreach (var obstacle in source.Hints.TemporaryObstacles)
            {
                var result = NativeRouteValidation.CheckShape(obstacle, segment.From, segment.To, clearance, segment.Start,
                    segment.End - segment.Start, float.NegativeInfinity, 0, null, null, budget);
                if (result != null) return Fail(result.Status, result.Reason);
            }
            foreach (var zone in source.Hints.ForbiddenZones)
            {
                if (!float.IsFinite(zone.shapeDistance.ConservativeLipschitz) || zone.shapeDistance.ConservativeLipschitz < 0)
                    return Fail("unknown", "native-distance-bound-missing");
                var result = NativeRouteValidation.CheckShape(zone.shapeDistance, segment.From, segment.To, clearance, segment.Start,
                    segment.End - segment.Start, (float)(zone.activation - world.CurrentTime).TotalSeconds - Math.Max(0.2f, NavigationDecision.ActivationTimeCushion),
                    0, zone.Source.ToString("X"), 0, budget);
                if (result != null) return Fail(result.Status, result.Reason);
            }
        }
        return new(heading, "clear", "current-native-timed-march-constraints", [.. route], elapsed,
            [.. segments]);
    }
}
