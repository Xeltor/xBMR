using BossMod.Pathfinding;

namespace BossMod.Integration;

// Exact swept-footprint checks against native per-cell unrelaxed warning limits.
internal static class IntegrationRasterRoutes
{
    internal static IntegrationRoutes.Result Evaluate(NavigationRaster raster, WPos[] route, IntegrationRoutes.Model model,
        float sourceAgeSeconds, IntegrationRoutes.Budget budget)
    {
        var physical = IntegrationRoutes.Evaluate(new(raster, default, [], [], default), route, model, budget);
        if (physical.Status != "clear") return physical;
        if (!float.IsFinite(sourceAgeSeconds) || sourceAgeSeconds < 0) return new("unknown", "invalid-native-source-age");
        var elapsed = sourceAgeSeconds + model.DepartureDelaySeconds;
        var radius = (model.Radius + IntegrationRoutes.SpatialMargin) / raster.Resolution;
        for (var segment = 0; segment < Math.Max(1, route.Length - 1); ++segment)
        {
            var a = route[segment]; var b = route[Math.Min(segment + 1, route.Length - 1)];
            var ga = raster.WorldToGrid(a); var gb = raster.WorldToGrid(b);
            var duration = (b - a).Length() / model.Speed;
            var left = (int)MathF.Floor(Math.Min(ga.X, gb.X) - radius); var right = (int)MathF.Floor(Math.Max(ga.X, gb.X) + radius);
            var top = (int)MathF.Floor(Math.Min(ga.Y, gb.Y) - radius); var bottom = (int)MathF.Floor(Math.Max(ga.Y, gb.Y) + radius);
            if (left < 0 || top < 0 || right >= raster.Width || bottom >= raster.Height) return new("unknown", "outside-timed-native-raster");
            for (var y = top; y <= bottom; ++y) for (var x = left; x <= right; ++x)
            {
                if (!budget.Step()) return new("unknown", "native-raster-timing-budget");
                var limit = raster.LimitAt(x, y);
                if (!float.IsFinite(limit)) return new("unknown", "invalid-native-raster-time");
                if (limit == float.MaxValue || !Crosses(ga, gb, x - radius, y - radius, x + 1 + radius, y + 1 + radius, out var entry, out var exit)) continue;
                if (elapsed + duration * exit + model.AdditionalTimeMarginSeconds >= limit)
                    return new("blocked", "native-raster-warning-exit-deadline", segment,
                        [(a + (b - a) * entry).X, (a + (b - a) * entry).Z], SegmentFraction: entry);
            }
            elapsed += duration;
        }
        var end = raster.WorldToGrid(route[^1]);
        for (var y = (int)MathF.Floor(end.Y - radius); y <= MathF.Floor(end.Y + radius); ++y)
            for (var x = (int)MathF.Floor(end.X - radius); x <= MathF.Floor(end.X + radius); ++x)
            {
                if (!budget.Step()) return new("unknown", "native-raster-timing-budget");
                if (raster.LimitAt(x, y) != float.MaxValue
                    && Crosses(end, end, x - radius, y - radius, x + 1 + radius, y + 1 + radius, out _, out _))
                    return new("blocked", "native-raster-requires-onward-escape");
            }
        return physical with { Reason = "current-native-timed-raster-complete-route" };
    }
    private static bool Crosses(Vector2 a, Vector2 b, float left, float top, float right, float bottom, out float entry, out float exit)
    {
        var from = 0f; var to = 1f;
        bool Axis(float start, float delta, float min, float max)
        {
            if (Math.Abs(delta) <= 1e-8f) return start >= min && start <= max;
            var first = (min - start) / delta; var last = (max - start) / delta;
            if (first > last) (first, last) = (last, first);
            from = Math.Max(from, first); to = Math.Min(to, last); return from <= to;
        }
        var result = Axis(a.X, b.X - a.X, left, right) && Axis(a.Y, b.Y - a.Y, top, bottom);
        entry = from; exit = to; return result;
    }
}
