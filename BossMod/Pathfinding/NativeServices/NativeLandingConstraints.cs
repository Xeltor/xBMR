using BossMod.Components;

namespace BossMod.Pathfinding;

// Read-only evaluation view. No live shape list, actor, immunity or authored constraint is edited.
internal static class NativeLandingConstraints
{
    internal readonly record struct Envelope(string Kind, WPos Center, WPos Origin, float PushDistance, float Limit);
    internal sealed record Inspection(NativeDisplacementProjection.Result Geometry, string CurrentPublishedConstraintsStatus,
        string Reason, DateTime EvaluatedAt, int SeparatedProjectionConstraints, int IndependentConstraints,
        int LaterWarnings, bool ArenaFootprintClear, bool OwnLandingEnvelopesClear, ulong? BlockerSourceActorId);
    private sealed record Region(ShapeDistance Shape, DateTime Activation, ulong Source);

    internal static Envelope? ProjectionEnvelope(ShapeDistance shape) => shape switch
    {
        SDKnockbackInCircleAwayFromOrigin circle => circle.ProjectionEnvelope,
        SDKnockbackInAABBSquareAwayFromOrigin box => box.ProjectionEnvelope,
        _ => null
    };

    internal static bool EnvelopeKnown(Envelope e) => e.Kind is "circle" or "square"
        && float.IsFinite(e.Center.X) && float.IsFinite(e.Center.Z)
        && float.IsFinite(e.Origin.X) && float.IsFinite(e.Origin.Z)
        && float.IsFinite(e.PushDistance) && e.PushDistance > 0 && float.IsFinite(e.Limit) && e.Limit > 0;

    internal static bool EnvelopeClear(Envelope e, WPos landing, float radius)
        => EnvelopeKnown(e) && float.IsFinite(landing.X) && float.IsFinite(landing.Z) && float.IsFinite(radius) && radius >= 0
            && (e.Kind == "circle" ? e.Limit - (landing - e.Center).Length() >= radius
                : Math.Min(e.Limit - Math.Abs(landing.X - e.Center.X), e.Limit - Math.Abs(landing.Z - e.Center.Z)) >= radius);

    // Retain the exact union graph at producer time. A changed child, cycle
    // or opaque composite cannot silently inherit another node's producer attribution.
    internal static ShapeDistance[]? Graph(ShapeDistance shape)
    {
        var nodes = new List<ShapeDistance>();
        var path = new HashSet<ShapeDistance>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<(ShapeDistance Node, bool Leave)>();
        pending.Push((shape, false));
        while (pending.TryPop(out var item))
        {
            if (item.Leave) { path.Remove(item.Node); continue; }
            if (item.Node == null || !path.Add(item.Node)) return null;
            nodes.Add(item.Node);
            pending.Push((item.Node, true));
            if (item.Node is SDUnion union)
                for (var i = union.Operands.Length - 1; i >= 0; --i)
                    pending.Push((union.Operands[i], false));
        }
        return [.. nodes];
    }

    internal static Inspection[]? Evaluate(AIHints hints, BossModule module, GenericKnockback component, Actor actor, int slot,
        NativeDisplacementProjection.Pose[] poses, NativeRouteValidation.Budget budget)
    {
        if (!hints.ProjectionSourcesValid || !hints.ProjectionSourcesMatch(module, actor) || actor.IsDead || actor.IsDestroyed || actor.InstanceID == 0
            || slot < 0 || slot >= PartyState.MaxAllies || poses.Length < 1
            || !component.SupportsCounterfactualProjection || component.StopAtWall || component.StopAfterWall || component.MaxCasts <= 0
            || actor.HitboxRadius < 0 || !float.IsFinite(actor.HitboxRadius)
            || module.Bounds is not ArenaBoundsCircle && !(module.Bounds is ArenaBoundsSquare square && square.Rotation.Rad == 0)
            || hints.PathfindMapArenaProjectionLayer != null
            || hints.Teleporters.Count != 0 || actor.PendingKnockbacks.Count != 0)
            return null;
        var sources = component.ActiveKnockbacks(slot, actor);
        if (sources.Length != 1) return null;
        var source = sources[0];
        var remaining = (source.Activation - module.WorldState.CurrentTime).TotalSeconds;
        if (source.Kind != GenericKnockback.Kind.AwayFromOrigin || source.Shape != null || source.SafeWalls.Length != 0
            || source.MinDistance != 0 || source.ArenaProjectionLayer != null || remaining <= 0
            || !float.IsFinite(source.Distance) || source.Distance <= 0 || !budget.Step()) return null;
        var envelopes = new List<Envelope>();
        var independent = new List<Region>();
        var separated = 0;
        var later = 0;
        bool Finite(WPos p) => float.IsFinite(p.X) && float.IsFinite(p.Z);
        if (!Finite(source.Origin)) return null;
        bool Collect(ShapeDistance shape, DateTime activation, ulong id, bool own)
        {
            if (!budget.Step()) return false;
            var graph = Graph(shape);
            if (graph == null) return false;
            foreach (var node in graph)
            {
                if (!budget.Step()) return false;
                if (node is SDUnion) continue;
                if (own)
                {
                    var envelope = ProjectionEnvelope(node);
                    // A sibling could itself be a preparation rule. Never reinterpret it as
                    // an independent damage region or discard it to approve a landing.
                    if (envelope is not { } e || activation != source.Activation || id != 0 && id != source.ActorID
                        || e.Origin != source.Origin || e.PushDistance != source.Distance || !Finite(e.Center)
                        || !float.IsFinite(e.Limit) || e.Limit <= 0) return false;
                    envelopes.Add(e);
                }
                else independent.Add(new(node, activation, id));
            }
            return true;
        }
        foreach (var zone in hints.ForbiddenZones)
        {
            var producer = hints.ProjectionProducer(zone.shapeDistance, zone.activation, zone.Source);
            if (producer == null && hints.ProjectionSourceWasRecorded(zone.shapeDistance)) return null;
            var own = ReferenceEquals(producer, component);
            if (own) ++separated;
            if (!own && (zone.activation - source.Activation).TotalSeconds > Pathfinding.NavigationDecision.ActivationTimeCushion + 0.25)
            { ++later; continue; }
            if (!Collect(zone.shapeDistance, zone.activation, zone.Source, own)) return null;
        }
        foreach (var obstacle in hints.TemporaryObstacles)
            if (!Collect(obstacle, default, 0, false)) return null;
        if (envelopes.Count == 0) return null;
        var result = new List<Inspection>();
        var radius = actor.HitboxRadius + NativeRouteValidation.SpatialMargin;
        foreach (var pose in poses)
        {
            if (pose == null || !budget.Step()) return null;
            var geometry = NativeDisplacementProjection.Project(component, actor, slot, pose, budget);
            if (geometry?.Movements is not [{ SourceIndex: 0 } step]) return null;
            var landing = step.To;
            var arena = NativeArenaGeometry.Query(module.Bounds, module.Center, null, landing, actor.HitboxRadius);
            if (!arena.Available || arena.SignedClearance is not { } clearance || !float.IsFinite(clearance)) return null;
            var arenaClear = arena.Contains == true && clearance >= radius;
            var envelopesClear = envelopes.All(e => EnvelopeClear(e, landing, radius));
            var status = "clear";
            var reason = "current-published-regions-at-native-knockback-estimate";
            ulong? blocker = null;
            if (!arenaClear || !envelopesClear || step.NativeDestinationUnsafe)
            {
                status = "blocked";
                reason = !arenaClear ? "arena-footprint-clearance" : !envelopesClear ? "own-projection-landing-envelope" : "native-destination-rule";
            }
            foreach (var region in independent)
            {
                if (!budget.Step()) return null;
                var bound = region.Shape.ConservativeLipschitz;
                if (!float.IsFinite(bound) || bound < 0)
                {
                    if (status != "blocked") { status = "unknown"; reason = "unsupported-independent-distance-bound"; }
                    continue;
                }
                var distance = region.Shape.Distance(landing);
                if (!float.IsFinite(distance))
                {
                    if (status != "blocked") { status = "unknown"; reason = "invalid-independent-distance"; }
                }
                else if (distance <= bound * radius)
                { status = "blocked"; reason = "independent-region-footprint-clearance"; blocker = region.Source == 0 ? null : region.Source; }
            }
            if (status == "clear" && step.NativeDestinationContext != "native-base-arena-rule")
            { status = "unknown"; reason = "native-override-receives-actual-actor-context"; }
            if (!budget.Step()) return null;
            result.Add(new(geometry, status, reason, source.Activation,
                separated, independent.Count, later, arenaClear, envelopesClear, blocker));
        }
        return [.. result];
    }
}
