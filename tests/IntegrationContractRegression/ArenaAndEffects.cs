using BossMod;
using BossMod.Integration;
using BossMod.Pathfinding;

static partial class Checks
{
    private static void ActionEffects()
    {
        var time = new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);
        var actors = new ActorState();
        // This event serialization fixture needs only identity; the Actor constructor requires game sheets.
        var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        actor.InstanceID = 0xA123;
        var effect = new ActorCastEvent(new(ActionType.Spell, 3571), 0xB123, 0, 1, new(1, 2, 3), 42, 7, default);
        using var observer = new IntegrationActionEffects(actors, () => (123, time));
        actors.CastEvent.Fire(actor, effect);
        actors.CastEvent.Fire(actor, effect);
        var publication = observer.Capture();
        Check(publication.Complete && publication.Effects.Length == 2, "duplicate native observations remain explicit");
        Check(publication.Effects[1].ObservationSequence > publication.Effects[0].ObservationSequence, "native observation order is monotonic");
        var first = publication.Effects[0];
        Check(first.ActorId == "A123" && first.MainTargetId == "B123" && first.ActionId == 3571, "effect actor/action/target correlation");
        Check(first.WorldFrame == 123 && first.WorldTimeTicks == time.Ticks.ToString(), "effect world-time relationship retained");
        Check(first.GlobalSequence == 42 && first.SourceSequence == 7 && first.GroundPosition!.SequenceEqual(new float[] { 1, 2, 3 }), "effect execution correlation and ground position");
        for (var i = 0; i < 70; ++i)
            actors.CastEvent.Fire(actor, effect);
        var limited = observer.Capture();
        Check(!limited.Complete && limited.Effects.Length == 64 && limited.Dropped == 8, "oversized native update is bounded with explicit loss");
        observer.BeginUpdate();
        Check(observer.Capture().Effects.Length == 0 && observer.Capture().Complete, "new update does not retain a combat history");
        Check(publication.Effects.Length == 2 && limited.Effects.Length == 64, "captured lists remain stable across update clear");
        actors.CastEvent.Fire(actor, effect);
        Check(observer.Capture().Effects[0].ObservationSequence > limited.Effects[^1].ObservationSequence, "sequence is not reset by frame clear");
        observer.Dispose();
        actors.CastEvent.Fire(actor, effect);
        Check(observer.Capture().Effects.Length == 0, "dispose unsubscribes world action observations");
    }

    private static void Near(float? actual, float expected, string reason, float tolerance = 0.001f)
        => Check(actual is { } v && Math.Abs(v - expected) < tolerance, reason);

    private static void ArenaGeometry()
    {
        var center = new WPos(100, 200);
        var circle = new ArenaBoundsCircle(10);
        var atCenter = IntegrationArena.Query(circle, center, null, center, 0.5f);
        Check(atCenter.Available && atCenter.Contains == true && atCenter.InsetCandidate!.SequenceEqual(new float[] { 100, 200 }), "circle interior and radius-aware candidate");
        Near(atCenter.SignedClearance, 10, "analytic circle clearance");
        var outside = IntegrationArena.Query(circle, center, null, new(111, 200), 0.5f);
        Near(outside.SignedClearance, -1, "negative clearance outside circle");
        Check(outside.Contains == false && outside.ClosestBoundary!.SequenceEqual(new float[] { 110, 200 }), "closest native circular boundary");
        var inset = outside.InsetCandidate!;
        Check(circle.Contains(new(inset[0] - 100, inset[1] - 200)), "inset candidate is inside logical geometry");
        Check(outside.Reachability == "not-evaluated", "geometric ingress never claims a safe route");
        Check(!IntegrationArena.Query(circle, center, 0, center, 0).Available, "projection layer on single-floor shape rejected");
        Check(!IntegrationArena.Query(circle, center, null, new(float.NaN, 0), 0).Available, "nonfinite point rejected");
        Check(!IntegrationArena.Query(circle, center, null, center, 6).Available, "unbounded query radius rejected");
        Check(!IntegrationArena.Query(circle, center, null, new(float.MaxValue, 0), 0).Available, "overflow-scale coordinate rejected");
        var rect = new ArenaBoundsRect(3, 7, 0.6f.Radians());
        var shortEdge = center + rect.Orientation.OrthoL() * 4;
        Near(IntegrationArena.Query(rect, center, null, shortEdge, 0).SignedClearance, -1, "rotated rectangle uses its actual orientation");
        Near(IntegrationArena.Query(rect, center, null, center, 0).SignedClearance, 3, "rectangle nearest edge clearance");
        var desc = IntegrationArena.Describe(rect, center, null);
        Check(desc.Available && desc.Kind == "rectangle" && desc.HalfWidth == 3 && desc.HalfHeight == 7 && desc.Rotation == 0.6f, "rectangle dimensions are not replaced by radius");

        var donut = new ArenaBoundsCustom([new Circle(default, 10)], [new Circle(default, 3)]);
        var inHole = IntegrationArena.Query(donut, center, null, center, 0.5f);
        Check(inHole.Available && inHole.Contains == false, "custom arena hole is outside playable geometry");
        Near(inHole.SignedClearance, -3, "hole clearance uses nearest real contour", 0.05f);
        var contours = IntegrationArena.Describe(donut, center, null).Contours!;
        Check(contours.Length == 1 && contours[0].Holes.Length == 1, "custom contours publish real holes");
        Check(IntegrationArena.Query(donut, center, null, new(106, 200), 0).Contains == true, "custom annulus interior");

        var left = new ArenaBoundsRect(5, 5).Shape;
        var right = new ArenaBoundsRect(2, 2).Shape;
        var layered = new ArenaBoundsCustom([new Circle(default, 10)], WorldProjectionLayers:
            [new(left, 0), new(right, 10)]);
        Check(!IntegrationArena.Query(layered, center, null, center, 0).Available, "unknown layer cannot silently use the union");
        Check(!IntegrationArena.Query(layered, center, 2, center, 0).Available, "invalid layer rejected");
        var p = new WPos(104, 200);
        Check(IntegrationArena.Query(layered, center, 0, p, 0).Contains == true, "first physical floor geometry");
        Check(IntegrationArena.Query(layered, center, 1, p, 0).Contains == false, "other floor cannot inherit first-floor containment");
        Check(IntegrationArena.Describe(layered, center, 0).MultiLayer, "multi-layer limitation is published");
        Check(!IntegrationArena.Query(new UnknownBounds(), center, null, center, 0).Available, "unsupported native geometry fails closed");
        var invalid = IntegrationArena.Describe(circle, new(float.NaN, 0), null);
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
