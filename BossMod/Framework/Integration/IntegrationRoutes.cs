using BossMod.Pathfinding;
using System.Diagnostics;

namespace BossMod.Integration;

// Validates an explicit complete walk route; it never changes native movement or runs pathfinding.
internal static class IntegrationRoutes
{
    internal const int MaxRoutes = 8;
    internal const int MaxPoints = 64;
    internal const float SpatialMargin = 0.05f;
    internal sealed record Model(float Radius, float Speed, float DepartureDelaySeconds, float AdditionalTimeMarginSeconds);
    internal sealed record Source(NavigationRaster Raster, Bitmap.Region CurrentPhysicalObstacles,
        ShapeDistance[] CurrentTemporaryObstacles, (ShapeDistance Shape, DateTime Activation, ulong Actor)[] CurrentWarnings, DateTime WorldTime,
        bool RequirePhysicalCoverage = false);
    internal sealed record Result(string Status, string Reason, int? Segment = null, float[]? FirstBlocker = null,
        string? SourceActorId = null, int? WarningIndex = null, float? TravelSeconds = null, float? ArrivalSeconds = null,
        float? SegmentFraction = null);

    internal sealed class Budget(Func<long>? clock = null, long frequency = 0, int evaluations = 8192)
    {
        private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
        private readonly long _frequency = frequency == 0 ? Stopwatch.Frequency : frequency;
        private readonly long _start = (clock ?? Stopwatch.GetTimestamp)();
        private int _remaining = evaluations;
        internal bool Step() => --_remaining >= 0 && (_clock() - _start) / (double)_frequency <= 0.004;
    }

    private static Result Unknown(string reason) => new("unknown", reason);
    private static float[] V(WPos p) => [p.X, p.Z];
    private static bool Finite(WPos p) => float.IsFinite(p.X) && float.IsFinite(p.Z) && Math.Abs(p.X) <= 1_000_000 && Math.Abs(p.Z) <= 1_000_000;

    internal static Result Evaluate(Source source, WPos[] route, Model model, Budget budget)
    {
        if (route.Length is < 1 or > MaxPoints || route.Any(p => !Finite(p)) || !float.IsFinite(model.Radius) || model.Radius is < 0 or > 5
            || !float.IsFinite(model.Speed) || model.Speed is <= 0 or > 20 || !float.IsFinite(model.DepartureDelaySeconds)
            || model.DepartureDelaySeconds is < 0 or > 10 || !float.IsFinite(model.AdditionalTimeMarginSeconds) || model.AdditionalTimeMarginSeconds is < 0.1f or > 2)
            return Unknown("invalid-walk-model-or-route");
        if (source.CurrentWarnings.Length + source.CurrentTemporaryObstacles.Length > 128)
            return Unknown("constraint-limit");
        foreach (var shape in source.CurrentTemporaryObstacles.Concat(source.CurrentWarnings.Select(w => w.Shape)))
            if (!float.IsFinite(shape.ConservativeLipschitz) || shape.ConservativeLipschitz < 0)
                return Unknown("unsupported-distance-bound");
        var totalLength = 0f;
        for (var i = 1; i < route.Length; ++i)
            totalLength += (route[i] - route[i - 1]).Length();
        if (!float.IsFinite(totalLength) || totalLength > 256)
            return Unknown("route-length-limit");
        var travel = totalLength / model.Speed;
        var arrival = model.DepartureDelaySeconds + travel;
        if (!float.IsFinite(travel) || !float.IsFinite(arrival) || arrival > 120)
            return Unknown("travel-time-limit");
        var elapsed = model.DepartureDelaySeconds;
        var radius = model.Radius + SpatialMargin;
        var segments = Math.Max(1, route.Length - 1);
        for (var segment = 0; segment < segments; ++segment)
        {
            var a = route[segment];
            var b = route[Math.Min(segment + 1, route.Length - 1)];
            var duration = (b - a).Length() / model.Speed;
            var physical = CheckPhysical(source, a, b, radius, segment, budget);
            if (physical?.Status == "unknown")
                return physical;
            var first = physical;
            Result? Earlier(Result? candidate)
            {
                if (candidate == null)
                    return first;
                return first == null || candidate.SegmentFraction < first.SegmentFraction ? candidate : first;
            }
            foreach (var shape in source.CurrentTemporaryObstacles)
            {
                var obstacle = CheckShape(shape, a, b, radius, elapsed, duration, float.NegativeInfinity, segment, null, null, budget);
                if (obstacle?.Status == "unknown")
                    return obstacle;
                first = Earlier(obstacle);
            }
            for (var index = 0; index < source.CurrentWarnings.Length; ++index)
            {
                var warning = source.CurrentWarnings[index];
                var deadline = (float)(warning.Activation - source.WorldTime).TotalSeconds
                    - Pathfinding.NavigationDecision.ActivationTimeCushion - model.AdditionalTimeMarginSeconds;
                var hazard = CheckShape(warning.Shape, a, b, radius, elapsed, duration, deadline, segment,
                    warning.Actor == 0 ? null : warning.Actor.ToString("X"), index, budget);
                if (hazard?.Status == "unknown")
                    return hazard;
                first = Earlier(hazard);
            }
            if (first != null)
                return first;
            elapsed += duration;
        }
        // Arrival in a warning is not proof of an escape. Require the supplied continuation to end
        // outside every current warning; no expiry time is invented from an activation time.
        for (var index = 0; index < source.CurrentWarnings.Length; ++index)
        {
            var warning = source.CurrentWarnings[index];
            if (!budget.Step())
                return Unknown("query-budget");
            var distance = warning.Shape.Distance(route[^1]);
            if (!float.IsFinite(distance))
                return Unknown("invalid-distance");
            if (distance <= warning.Shape.ConservativeLipschitz * radius)
                return new("blocked", "requires-onward-escape", segments - 1, V(route[^1]), warning.Actor == 0 ? null : warning.Actor.ToString("X"), index);
        }
        return new("clear", "current-hints-complete-walk-route", TravelSeconds: travel, ArrivalSeconds: arrival);
    }

    private static Result? CheckShape(ShapeDistance shape, WPos a, WPos b, float radius, float elapsed, float duration,
        float deadline, int segment, string? actor, int? index, Budget budget)
    {
        var lipschitz = shape.ConservativeLipschitz;
        var delta = b - a;
        var length = delta.Length();
        var pending = new Stack<(float From, float To)>();
        pending.Push((0, 1));
        while (pending.Count != 0)
        {
            if (!budget.Step())
                return Unknown("query-budget");
            var (from, to) = pending.Pop();
            var mid = (from + to) * 0.5f;
            var point = a + delta * mid;
            var distance = shape.Distance(point);
            if (!float.IsFinite(distance))
                return Unknown("invalid-distance");
            var halfLength = length * (to - from) * 0.5f;
            if (distance > lipschitz * (halfLength + radius))
                continue; // proves the entire swept footprint outside this shape on this interval
            if (elapsed + to * duration < deadline)
                continue; // even the latest possible exit precedes the conservative activation deadline
            if (halfLength <= 0.025f)
                return new("blocked", index == null ? "temporary-obstacle-clearance" : "warning-deadline-or-clearance",
                    segment, V(a + delta * from), actor, index, SegmentFraction: from);
            // Process earlier intervals first so the witness is the first rejected interval for this shape.
            pending.Push((mid, to));
            pending.Push((from, mid));
        }
        return null;
    }

    private static Result? CheckPhysical(Source source, WPos a, WPos b, float radius, int segment, Budget budget)
    {
        var map = source.Raster;
        var ga = map.WorldToGrid(a);
        var gb = map.WorldToGrid(b);
        var r = radius / map.Resolution;
        if (!float.IsFinite(ga.X) || !float.IsFinite(ga.Y) || !float.IsFinite(gb.X) || !float.IsFinite(gb.Y) || !float.IsFinite(r))
            return Unknown("invalid-raster-transform");
        var left = MathF.Floor(Math.Min(ga.X, gb.X) - r);
        var right = MathF.Floor(Math.Max(ga.X, gb.X) + r);
        var top = MathF.Floor(Math.Min(ga.Y, gb.Y) - r);
        var bottom = MathF.Floor(Math.Max(ga.Y, gb.Y) + r);
        if (left < 0 || top < 0 || right >= map.Width || bottom >= map.Height)
            return Unknown("outside-navigation-window");
        var first = float.MaxValue;
        string? reason = null;
        for (var y = (int)top; y <= (int)bottom; ++y)
        {
            for (var x = (int)left; x <= (int)right; ++x)
            {
                if (!budget.Step())
                    return Unknown("query-budget");
                if (map.BlockAt(x, y) == byte.MaxValue)
                    return Unknown("invalid-arena-raster-cell");
                var arenaBlocked = map.BlockAt(x, y) == 1;
                var physicalBlocked = PhysicalAt(source.CurrentPhysicalObstacles, x, y);
                if (source.RequirePhysicalCoverage && physicalBlocked == null)
                    return Unknown("physical-map-coverage");
                if ((!arenaBlocked && physicalBlocked != true) || !IntersectsBox(ga, gb, x - r, y - r, x + 1 + r, y + 1 + r, out var entry))
                    continue;
                if (entry < first)
                {
                    first = entry;
                    reason = arenaBlocked ? "arena-raster-boundary" : "physical-obstacle";
                }
            }
        }
        return reason == null ? null : new("blocked", reason, segment, V(a + (b - a) * first), SegmentFraction: first);
    }

    private static bool? PhysicalAt(Bitmap.Region region, int x, int y)
    {
        if (region.Bitmap == null)
            return null;
        var bx = (long)x + region.Rect.Left;
        var by = (long)y + region.Rect.Top;
        if (x >= (long)region.Rect.Right - region.Rect.Left || y >= (long)region.Rect.Bottom - region.Rect.Top
            || bx < 0 || by < 0 || bx >= region.Bitmap.Width || by >= region.Bitmap.Height)
            return null;
        return region.Bitmap[(int)bx, (int)by];
    }

    private static bool IntersectsBox(Vector2 a, Vector2 b, float left, float top, float right, float bottom, out float entry)
    {
        var from = 0f;
        var to = 1f;
        bool Axis(float start, float delta, float min, float max)
        {
            if (Math.Abs(delta) <= 1e-8f)
                return start >= min && start <= max;
            var first = (min - start) / delta;
            var last = (max - start) / delta;
            if (first > last)
                (first, last) = (last, first);
            from = Math.Max(from, first);
            to = Math.Min(to, last);
            return from <= to;
        }
        var result = Axis(a.X, b.X - a.X, left, right) && Axis(a.Y, b.Y - a.Y, top, bottom);
        entry = from;
        return result;
    }
}
