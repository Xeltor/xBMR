namespace BossMod.Pathfinding;

// Shared swept-cell test. Deadlines are last-safe EXIT times, never hazard expiry times.
// The caller owns the map and must pass unrelaxed native limits for elective movement.
internal static class TimedTraversal
{
    internal static bool Clip(Vector2 a, Vector2 b, float left, float top, float right, float bottom, out float entry, out float exit)
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
        var intersects = Axis(a.X, b.X - a.X, left, right) && Axis(a.Y, b.Y - a.Y, top, bottom);
        entry = from;
        exit = to;
        return intersects;
    }

    internal static bool Check(Map map, WPos start, WPos end, float elapsed, float duration, float radius, out float departureSlack,
        bool physicalOnly = false)
    {
        departureSlack = float.MaxValue;
        if (!float.IsFinite(elapsed) || elapsed < 0 || !float.IsFinite(duration) || duration < 0
            || !float.IsFinite(radius) || radius < 0 || !float.IsFinite(map.Resolution) || map.Resolution <= 0)
            return false;
        var a = map.WorldToGridFrac(start);
        var b = map.WorldToGridFrac(end);
        if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(b.X) || !float.IsFinite(b.Y))
            return false;
        var r = radius / map.Resolution;
        var left = (int)MathF.Floor(Math.Min(a.X, b.X) - r);
        var right = (int)MathF.Floor(Math.Max(a.X, b.X) + r);
        var top = (int)MathF.Floor(Math.Min(a.Y, b.Y) - r);
        var bottom = (int)MathF.Floor(Math.Max(a.Y, b.Y) + r);
        if (left < 0 || top < 0 || right >= map.Width || bottom >= map.Height)
            return false;
        for (var y = top; y <= bottom; ++y)
            for (var x = left; x <= right; ++x)
            {
                if (!Clip(a, b, x - r, y - r, x + 1 + r, y + 1 + r, out _, out var exit))
                    continue;
                var limit = map.PixelMaxG[map.GridToIndex(x, y)];
                if (!float.IsFinite(limit) || limit < 0)
                    return false;
                if (physicalOnly || limit == float.MaxValue)
                    continue;
                var slack = limit - elapsed - duration * exit;
                if (slack <= 0)
                    return false;
                departureSlack = Math.Min(departureSlack, slack);
            }
        return true;
    }

    internal static bool CanRemain(Map map, WPos point, float radius)
        => Check(map, point, point, 0, 0, radius, out var slack) && slack == float.MaxValue;
}
