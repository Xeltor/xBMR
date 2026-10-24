using BossMod.Pathfinding;

namespace BossMod.Pathfinding;

// Exact swept-footprint checks against native per-cell unrelaxed warning limits.
internal static class NativeRasterRouteValidation
{
    internal static NativeRouteValidation.Result Evaluate(NavigationRaster raster, WPos[] route, NativeRouteValidation.Model model,
        float sourceAgeSeconds, NativeRouteValidation.Budget budget)
    {
        var physical = NativeRouteValidation.Evaluate(new(raster, default, [], [], default), route, model, budget);
        if (physical.Status != "clear") return physical;
        if (!float.IsFinite(sourceAgeSeconds) || sourceAgeSeconds < 0) return new("unknown", "invalid-native-source-age");
        var elapsed = sourceAgeSeconds + model.DepartureDelaySeconds;
        var radius = (model.Radius + NativeRouteValidation.SpatialMargin) / raster.Resolution;
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
        => TimedTraversal.Clip(a, b, left, top, right, bottom, out entry, out exit);
}
