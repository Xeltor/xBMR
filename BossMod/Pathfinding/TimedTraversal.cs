using System.Buffers;
using System.Runtime.CompilerServices;

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
        // Long diagonal routes touch a strip, not their entire bounding rectangle.
        // Keep the small adjacent-cell case allocation-free without sweep setup.
        if (right - left > 4 && bottom - top > 4)
            return CheckStrip(map, a, b, r, left, right, top, bottom, elapsed, duration, physicalOnly, out departureSlack);
        return CheckRectangle(map, a, b, r, left, right, top, bottom, elapsed, duration, physicalOnly, out departureSlack);
    }

    private readonly record struct Interval(float Entry, float Exit);

    // Slab intersection is separable: a cell is touched exactly when its X and
    // Y time intervals overlap [0,1]. X intervals are monotone in travel order.
    // Find each row's overlapping range by binary search, then visit it in the
    // original row/column order. No DDA rounding drift, center-line approximation
    // or change to the footprint, inclusive corner touches or deadline arithmetic.
    private static bool CheckStrip(Map map, Vector2 a, Vector2 b, float r, int left, int right, int top, int bottom,
        float elapsed, float duration, bool physicalOnly, out float slack)
    {
        slack = float.MaxValue;
        var count = right - left + 1;
        Interval[]? rented = null;
        Span<Interval> intervals = count <= 128 ? stackalloc Interval[count] : (rented = ArrayPool<Interval>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            // Wide footprints can also select this path for stationary segments.
            // A parallel axis has a full interval inside its slab and no interval outside.
            if (Math.Abs(dx) <= 1e-8f || Math.Abs(dy) <= 1e-8f)
                return CheckRectangle(map, a, b, r, left, right, top, bottom, elapsed, duration, physicalOnly, out slack);
            var forward = dx > 0;
            for (var i = 0; i < count; ++i)
            {
                var x = forward ? left + i : right - i;
                var first = (x - r - a.X) / dx;
                var last = (x + 1 + r - a.X) / dx;
                intervals[i] = forward ? new(first, last) : new(last, first);
            }
            for (var y = top; y <= bottom; ++y)
            {
                var entry = (y - r - a.Y) / dy;
                var exit = (y + 1 + r - a.Y) / dy;
                if (entry > exit) (entry, exit) = (exit, entry);
                entry = Math.Max(0, entry);
                exit = Math.Min(1, exit);
                if (entry > exit) continue;
                var lo = 0;
                var hi = count;
                while (lo < hi)
                {
                    var mid = lo + (hi - lo) / 2;
                    if (intervals[mid].Exit < entry) lo = mid + 1;
                    else hi = mid;
                }
                var firstIndex = lo;
                hi = count;
                while (lo < hi)
                {
                    var mid = lo + (hi - lo) / 2;
                    if (intervals[mid].Entry <= exit) lo = mid + 1;
                    else hi = mid;
                }
                var firstX = forward ? left + firstIndex : right - lo + 1;
                var lastX = forward ? left + lo - 1 : right - firstIndex;
                for (var x = firstX; x <= lastX; ++x)
                {
                    var limit = map.PixelMaxG[map.GridToIndex(x, y)];
                    if (!float.IsFinite(limit) || limit < 0) return false;
                    if (physicalOnly || limit == float.MaxValue) continue;
                    var last = Math.Min(exit, intervals[forward ? x - left : right - x].Exit);
                    var remaining = limit - elapsed - duration * last;
                    if (remaining <= 0) return false;
                    slack = Math.Min(slack, remaining);
                }
            }
            return true;
        }
        finally
        {
            if (rented != null) ArrayPool<Interval>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckRectangle(Map map, Vector2 a, Vector2 b, float r, int left, int right, int top, int bottom,
        float elapsed, float duration, bool physicalOnly, out float slack)
    {
        slack = float.MaxValue;
        for (var y = top; y <= bottom; ++y)
            for (var x = left; x <= right; ++x)
            {
                if (!Clip(a, b, x - r, y - r, x + 1 + r, y + 1 + r, out _, out var exit)) continue;
                var limit = map.PixelMaxG[map.GridToIndex(x, y)];
                if (!float.IsFinite(limit) || limit < 0) return false;
                if (physicalOnly || limit == float.MaxValue) continue;
                var remaining = limit - elapsed - duration * exit;
                if (remaining <= 0) return false;
                slack = Math.Min(slack, remaining);
            }
        return true;
    }

    internal static bool CanRemain(Map map, WPos point, float radius)
        => Check(map, point, point, 0, 0, radius, out var slack) && slack == float.MaxValue;
}
