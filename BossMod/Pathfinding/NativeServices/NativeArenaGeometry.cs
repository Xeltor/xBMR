namespace BossMod.Pathfinding;

internal static class NativeArenaGeometry
{
    internal sealed record Part(float[][] Exterior, float[][][] Holes);
    internal sealed record Description(bool Available, string Reason, string Kind, float[]? Center, float? Radius,
        float? HalfWidth, float? HalfHeight, float? Rotation, int? ProjectionLayer, bool MultiLayer, Part[]? Contours);
    internal sealed record PointResult(bool Available, string Reason, bool? Contains, float? SignedClearance,
        float[]? ClosestBoundary, float[]? InsetCandidate, string Reachability = "not-evaluated");

    private static float[] V(WPos p) => [p.X, p.Z];
    private static bool Finite(WPos p) => float.IsFinite(p.X) && float.IsFinite(p.Z);
    private static bool Positive(float f) => float.IsFinite(f) && f > 0;
    private static float? Number(float? value) => value is { } v && float.IsFinite(v) ? v : null;

    private static string? Validate(ArenaBounds? bounds, WPos center, int? layer, out RelSimplifiedComplexPolygon? polygon)
    {
        polygon = null;
        if (bounds == null || !Finite(center))
            return "missing-or-invalid-arena";
        if (!Positive(bounds.MapResolution) || !Positive(bounds.Radius))
            return "invalid-dimensions";
        switch (bounds)
        {
            case ArenaBoundsCircle:
                return layer == null ? null : "unexpected-projection-layer";
            case ABRect rect:
                return !Positive(rect.HalfWidth) || !Positive(rect.HalfHeight) || !float.IsFinite(rect.Rotation.Rad)
                    ? "invalid-dimensions" : layer == null ? null : "unexpected-projection-layer";
            case ArenaBoundsCustom custom:
                if (custom.WorldProjectionLayers is { Length: > 0 })
                {
                    if (!custom.IsValidProjectionLayer(layer))
                        return "unknown-projection-layer";
                    polygon = custom.ProjectionLayer2DShape(layer);
                }
                else
                {
                    if (layer != null)
                        return "unexpected-projection-layer";
                    polygon = custom.Shape;
                }
                if (polygon.Parts.Count == 0 || polygon.Parts.Any(p => p.Vertices.Count < 3 || p.Vertices.Any(v => !Finite(new(v.X, v.Z)))))
                    return "invalid-polygon";
                return null;
            default:
                return "unsupported-arena-type";
        }
    }

    internal static Description Describe(ArenaBounds? bounds, WPos center, int? layer)
    {
        var reason = Validate(bounds, center, layer, out var polygon);
        var rect = bounds as ABRect;
        return new(reason == null, reason ?? "geometry-only", bounds switch
        {
            ArenaBoundsCircle => "circle", ArenaBoundsSquare => "square", ABRect => "rectangle", ArenaBoundsCustom => "polygon", _ => "unknown"
        }, Finite(center) ? V(center) : null, bounds is ArenaBoundsCircle ? Number(bounds.Radius) : null,
            Number(rect?.HalfWidth), Number(rect?.HalfHeight), Number(rect?.Rotation.Rad),
            layer, bounds is ArenaBoundsCustom { WorldProjectionLayers.Length: > 0 },
            reason == null && polygon != null ? polygon.Parts.Select(DescribePart).ToArray() : null);
    }

    private static Part DescribePart(RelPolygonWithHoles polygon)
    {
        static float[][] Copy(ReadOnlySpan<WDir> vertices)
        {
            var copy = new float[vertices.Length][];
            for (var i = 0; i < copy.Length; ++i)
                copy[i] = [vertices[i].X, vertices[i].Z];
            return copy;
        }
        var holes = new float[polygon.HoleStarts.Count][][];
        for (var i = 0; i < holes.Length; ++i)
            holes[i] = Copy(polygon.Interior(i));
        return new(Copy(polygon.Exterior), holes);
    }

    internal static PointResult Query(ArenaBounds? bounds, WPos center, int? layer, WPos point, float radius)
    {
        var reason = Validate(bounds, center, layer, out var polygon);
        if (reason != null || !Finite(point) || !float.IsFinite(radius) || radius < 0)
            return new(false, reason ?? "invalid-point-or-radius", null, null, null, null);
        var native = bounds!;
        bool Contains(WPos p) => polygon != null ? polygon.Contains(p - center) : native.Contains(p - center);
        WPos Boundary(WPos p)
        {
            var off = p - center;
            if (native is ArenaBoundsCircle circle)
                return center + (off.LengthSq() > 0 ? off.Normalized() : new WDir(1, 0)) * circle.Radius;
            if (native is ABRect rect)
            {
                var x = off.Dot(rect.Orientation.OrthoL());
                var z = off.Dot(rect.Orientation);
                var bx = Math.Clamp(x, -rect.HalfWidth, rect.HalfWidth);
                var bz = Math.Clamp(z, -rect.HalfHeight, rect.HalfHeight);
                if (native.Contains(off))
                {
                    if (rect.HalfWidth - Math.Abs(x) < rect.HalfHeight - Math.Abs(z))
                        bx = x < 0 ? -rect.HalfWidth : rect.HalfWidth;
                    else
                        bz = z < 0 ? -rect.HalfHeight : rect.HalfHeight;
                }
                return center + rect.Orientation.OrthoL() * bx + rect.Orientation * bz;
            }
            return center + polygon!.ClosestPointOnBoundary(off);
        }
        var contains = Contains(point);
        var boundary = Boundary(point);
        var distance = (point - boundary).Length();
        if (!Finite(boundary) || !float.IsFinite(distance))
            return new(false, "geometry-arithmetic-overflow", null, null, null, null);
        var clearance = contains ? distance : -distance;
        WPos? inset = clearance >= radius + 0.05f ? point : null;
        var bestDistance = float.MaxValue;
        // This is a verified geometric candidate, not a route or an overall safety verdict.
        for (var ring = 1; inset == null && ring <= 4; ++ring)
        {
            for (var i = 0; i < 16; ++i)
            {
                var angle = (i * MathF.Tau / 16).Radians();
                var candidate = boundary + angle.ToDirection() * ((radius + 0.05f) * ring);
                if (!Contains(candidate) || (candidate - Boundary(candidate)).Length() < radius + 0.049f)
                    continue;
                var d = (candidate - point).LengthSq();
                if (d < bestDistance)
                {
                    bestDistance = d;
                    inset = candidate;
                }
            }
        }
        return new(true, "geometry-only", contains, clearance, V(boundary), inset is { } p ? V(p) : null);
    }
}
