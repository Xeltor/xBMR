namespace BossMod.Pathfinding;

// A frozen observed surface segment expanded by the mover's physical radius.
// No actor, collider pointer, rendered bounds or live evaluator reaches workers.
internal readonly record struct NativePhysicalObstacle(WPos A, WPos B, float Radius)
{
    internal static NativePhysicalObstacle FromHit(NativeMovementCollision.Hit hit, float radius)
    {
        var point = new WPos(hit.Point);
        Span<Vector3> vertices = [hit.A, hit.B, hit.C];
        Span<WPos> intersections = stackalloc WPos[6];
        var count = 0;
        for (var i = 0; i < 3; ++i)
        {
            var a = vertices[i]; var b = vertices[(i + 1) % 3];
            if (!Finite(a) || !Finite(b)) return new(point, point, 0);
            var da = a.Y - hit.Point.Y; var db = b.Y - hit.Point.Y;
            if (da == 0) intersections[count++] = new(a);
            if (da < 0 && db > 0 || da > 0 && db < 0)
                intersections[count++] = new(Vector3.Lerp(a, b, da / (da - db)));
        }
        // Sphere sweeps report the mover centre at impact, not the surface
        // contact. Expand an observed triangle once; an unlocated primitive
        // impact is already a forbidden centre position and gets no expansion.
        // Some native primitive colliders leave triangle vertices unset. Never
        // manufacture a surface from those zeros or from an unrelated plane.
        if (count < 2 || Vector3.Cross(hit.B - hit.A, hit.C - hit.A).LengthSquared() < 1e-8f)
            return new(point, point, 0);
        var start = point; var end = point; var longest = 0f;
        for (var i = 0; i < count; ++i)
            for (var j = i + 1; j < count; ++j)
                if ((intersections[i] - intersections[j]).LengthSq() is var length && length > longest)
                { longest = length; start = intersections[i]; end = intersections[j]; }
        return DistanceSquared(point, start, end) <= (radius + 0.05f) * (radius + 0.05f) ? new(start, end, radius) : new(point, point, 0);
    }

    internal void Apply(Map map, float[]? strict, byte[]? blocks, WPos? occupied = null)
    {
        // Raster overlap is conservative. It must not invent an emergency at
        // the occupied exact point solely because a nearby wall shares its cell.
        // Preserve that cell's existing authored/physical limits; execution still
        // sweeps the actual route before allowing any exit from this cell.
        var occupiedCell = occupied is { } p && DistanceSquared(p, A, B) > Radius * Radius
            ? map.WorldToGrid(p) : (-1, -1);
        var a = map.WorldToGridFrac(A); var b = map.WorldToGridFrac(B);
        var radius = Radius / map.Resolution;
        var minX = Math.Max(0, (int)MathF.Ceiling(Math.Min(a.X, b.X) - radius) - 1);
        var maxX = Math.Min(map.Width - 1, (int)MathF.Floor(Math.Max(a.X, b.X) + radius));
        var minY = Math.Max(0, (int)MathF.Ceiling(Math.Min(a.Y, b.Y) - radius) - 1);
        var maxY = Math.Min(map.Height - 1, (int)MathF.Floor(Math.Max(a.Y, b.Y) + radius));
        for (var y = minY; y <= maxY; ++y)
            for (var x = minX; x <= maxX; ++x)
            {
                if ((x, y) == occupiedCell || !TouchesCell(a, b, radius, x, y)) continue;
                var index = map.GridToIndex(x, y);
                map.PixelMaxG[index] = -1000;
                map.PixelPriority[index] = float.MinValue;
                if (strict != null) strict[index] = -1000;
                if (blocks != null) blocks[index] = 2;
            }
    }

    internal static bool TouchesCell(Vector2 a, Vector2 b, float radius, int x, int y)
    {
        var lo = new Vector2(x, y); var hi = lo + Vector2.One;
        var delta = b - a; var enter = 0f; var exit = 1f;
        if (Clip(a.X, delta.X, lo.X, hi.X, ref enter, ref exit)
            && Clip(a.Y, delta.Y, lo.Y, hi.Y, ref enter, ref exit)) return true;
        var distance = Math.Min(Vector2.DistanceSquared(a, Vector2.Clamp(a, lo, hi)),
            Vector2.DistanceSquared(b, Vector2.Clamp(b, lo, hi)));
        foreach (var corner in (ReadOnlySpan<Vector2>)[lo, hi, new(lo.X, hi.Y), new(hi.X, lo.Y)])
        {
            var t = delta.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(corner - a, delta) / delta.LengthSquared(), 0, 1) : 0;
            distance = Math.Min(distance, Vector2.DistanceSquared(corner, a + t * delta));
        }
        return distance <= radius * radius;
    }
    private static bool Clip(float start, float delta, float lo, float hi, ref float enter, ref float exit)
    {
        if (delta == 0) return start >= lo && start <= hi;
        var a = (lo - start) / delta; var b = (hi - start) / delta;
        enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
        return enter <= exit;
    }
    private static float DistanceSquared(WPos p, WPos a, WPos b)
    {
        var d = b - a; var t = d.LengthSq() > 0 ? Math.Clamp((p - a).Dot(d) / d.LengthSq(), 0, 1) : 0;
        return (p - (a + t * d)).LengthSq();
    }
    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}
