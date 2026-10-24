using BossMod;
using BossMod.Pathfinding;

public static partial class Checks
{
    private static void Near(float? actual, float expected, string reason, float tolerance = 0.001f)
        => Check(actual is { } v && Math.Abs(v - expected) < tolerance, reason);

    private static void ArenaGeometry()
    {
        var center = new WPos(100, 200);
        var circle = new ArenaBoundsCircle(10);
        var atCenter = NativeArenaGeometry.Query(circle, center, null, center, 0.5f);
        Check(atCenter.Available && atCenter.Contains == true && atCenter.InsetCandidate!.SequenceEqual(new float[] { 100, 200 }), "circle interior and radius-aware candidate");
        Near(atCenter.SignedClearance, 10, "analytic circle clearance");
        var outside = NativeArenaGeometry.Query(circle, center, null, new(111, 200), 0.5f);
        Near(outside.SignedClearance, -1, "negative clearance outside circle");
        Check(outside.Contains == false && outside.ClosestBoundary!.SequenceEqual(new float[] { 110, 200 }), "closest native circular boundary");
        var inset = outside.InsetCandidate!;
        Check(circle.Contains(new(inset[0] - 100, inset[1] - 200)), "inset candidate is inside logical geometry");
        Check(outside.Reachability == "not-evaluated", "geometric ingress never claims a safe route");
        Check(!NativeArenaGeometry.Query(circle, center, 0, center, 0).Available, "projection layer on single-floor shape rejected");
        Check(!NativeArenaGeometry.Query(circle, center, null, new(float.NaN, 0), 0).Available, "nonfinite point rejected");
        Check(NativeArenaGeometry.Query(circle, center, null, center, 6).Available, "Finite native footprints are not restricted by the old IPC radius cap.");
        Check(!NativeArenaGeometry.Query(circle, center, null, new(float.MaxValue, 0), 0).Available, "overflow-scale coordinate rejected");
        var rect = new ArenaBoundsRect(3, 7, 0.6f.Radians());
        var shortEdge = center + rect.Orientation.OrthoL() * 4;
        Near(NativeArenaGeometry.Query(rect, center, null, shortEdge, 0).SignedClearance, -1, "rotated rectangle uses its actual orientation");
        Near(NativeArenaGeometry.Query(rect, center, null, center, 0).SignedClearance, 3, "rectangle nearest edge clearance");
        var desc = NativeArenaGeometry.Describe(rect, center, null);
        Check(desc.Available && desc.Kind == "rectangle" && desc.HalfWidth == 3 && desc.HalfHeight == 7 && desc.Rotation == 0.6f, "rectangle dimensions are not replaced by radius");

        var donut = new ArenaBoundsCustom([new Circle(default, 10)], [new Circle(default, 3)]);
        var inHole = NativeArenaGeometry.Query(donut, center, null, center, 0.5f);
        Check(inHole.Available && inHole.Contains == false, "custom arena hole is outside playable geometry");
        Near(inHole.SignedClearance, -3, "hole clearance uses nearest real contour", 0.05f);
        var contours = NativeArenaGeometry.Describe(donut, center, null).Contours!;
        Check(contours.Length == 1 && contours[0].Holes.Length == 1, "custom contours publish real holes");
        Check(NativeArenaGeometry.Query(donut, center, null, new(106, 200), 0).Contains == true, "custom annulus interior");

        var left = new ArenaBoundsRect(5, 5).Shape;
        var right = new ArenaBoundsRect(2, 2).Shape;
        var layered = new ArenaBoundsCustom([new Circle(default, 10)], WorldProjectionLayers:
            [new(left, 0), new(right, 10)]);
        Check(!NativeArenaGeometry.Query(layered, center, null, center, 0).Available, "unknown layer cannot silently use the union");
        Check(!NativeArenaGeometry.Query(layered, center, 2, center, 0).Available, "invalid layer rejected");
        var p = new WPos(104, 200);
        Check(NativeArenaGeometry.Query(layered, center, 0, p, 0).Contains == true, "first physical floor geometry");
        Check(NativeArenaGeometry.Query(layered, center, 1, p, 0).Contains == false, "other floor cannot inherit first-floor containment");
        Check(NativeArenaGeometry.Describe(layered, center, 0).MultiLayer, "multi-layer limitation is published");
        Check(!NativeArenaGeometry.Query(new UnknownBounds(), center, null, center, 0).Available, "unsupported native geometry fails closed");
        var invalid = NativeArenaGeometry.Describe(circle, new(float.NaN, 0), null);
        Check(!invalid.Available && invalid.Center == null, "invalid geometry publication contains no nonfinite JSON number");
    }

    private sealed class UnknownBounds() : ArenaBounds(1, 0.5f)
    {
        public override void PathfindMap(Map map, WPos center) => throw new NotSupportedException();
        public override bool Contains(in WDir offset) => true;
        public override float IntersectRay(in WDir originOffset, in WDir dir) => 0;
        public override WDir ClampToBounds(in WDir offset) => default;
    }
}
