using BossMod.Components;
using System.Globalization;

namespace BossMod.Integration;

// Read-only evaluation view. No live shape list, actor, immunity or authored constraint is edited.
internal static class IntegrationLandingConstraints
{
    internal readonly record struct Envelope(string Kind, WPos Center, WPos Origin, float PushDistance, float Limit);
    internal sealed record Inspection(IntegrationCounterfactualProjection.Result Geometry, string CurrentPublishedConstraintsStatus,
        string Reason, string EvaluatedAtTicks, int SeparatedProjectionConstraints, int IndependentConstraints,
        int LaterWarnings, bool ArenaFootprintClear, bool OwnLandingEnvelopesClear, string? BlockerSourceActorId);
    private sealed record Region(ShapeDistance Shape, DateTime Activation, ulong Source);

    // Retain the exact union graph at producer time. A changed child, cycle, oversized tree
    // or opaque composite cannot silently inherit another node's producer attribution.
    internal static ShapeDistance[]? Graph(ShapeDistance shape)
    {
        var nodes = new List<ShapeDistance>();
        var path = new HashSet<ShapeDistance>(ReferenceEqualityComparer.Instance);
        bool Visit(ShapeDistance node, int depth)
        {
            if (node == null || depth > 8 || nodes.Count >= 128 || !path.Add(node)) return false;
            nodes.Add(node);
            if (node is SDUnion union)
                foreach (var child in union.Operands)
                    if (!Visit(child, depth + 1)) return false;
            path.Remove(node);
            return true;
        }
        return Visit(shape, 0) ? [.. nodes] : null;
    }

    internal static Inspection[]? Evaluate(AIHints hints, BossModule module, GenericKnockback component, Actor actor, int slot,
        IntegrationCounterfactualProjection.Pose[] poses, IntegrationRoutes.Budget budget)
    {
        if (!hints.ProjectionSourcesValid || !hints.ProjectionSourcesMatch(module, actor) || actor.IsDead || actor.IsDestroyed || actor.InstanceID == 0
            || slot < 0 || slot >= PartyState.MaxAllies || poses.Length is < 1 or > 4
            || module.Components.Count > IntegrationMechanics.MaxComponents
            || !component.SupportsCounterfactualProjection || component.StopAtWall || component.StopAfterWall || component.MaxCasts <= 0
            || actor.HitboxRadius is < 0 or > 5 || !float.IsFinite(actor.HitboxRadius)
            || module.Bounds is not ArenaBoundsCircle && !(module.Bounds is ArenaBoundsSquare square && square.Rotation.Rad == 0)
            || hints.PathfindMapArenaProjectionLayer != null || hints.ForbiddenZones.Count + hints.TemporaryObstacles.Count > 128
            || hints.Teleporters.Count != 0 || actor.PendingKnockbacks.Count != 0)
            return null;
        var sources = component.ActiveKnockbacks(slot, actor);
        if (sources.Length != 1) return null;
        var source = sources[0];
        var remaining = (source.Activation - module.WorldState.CurrentTime).TotalSeconds;
        if (source.Kind != GenericKnockback.Kind.AwayFromOrigin || source.Shape != null || source.SafeWalls.Length != 0
            || source.MinDistance != 0 || source.ArenaProjectionLayer != null || remaining is <= 0 or > 120
            || !float.IsFinite(source.Distance) || source.Distance <= 0 || !budget.Step()) return null;
        var envelopes = new List<Envelope>();
        var independent = new List<Region>();
        var separated = 0;
        var later = 0;
        var leaves = 0;
        bool Finite(WPos p) => float.IsFinite(p.X) && float.IsFinite(p.Z) && Math.Abs(p.X) <= 1_000_000 && Math.Abs(p.Z) <= 1_000_000;
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
                if (++leaves > 128) return false;
                if (own)
                {
                    Envelope? envelope = node switch
                    {
                        SDKnockbackInCircleAwayFromOrigin circle => circle.ProjectionEnvelope,
                        SDKnockbackInAABBSquareAwayFromOrigin box => box.ProjectionEnvelope,
                        _ => null
                    };
                    // A sibling could itself be a preparation rule. Never reinterpret it as
                    // an independent damage region or discard it to approve a landing.
                    if (envelope is not { } e || activation != source.Activation || id != 0 && id != source.ActorID
                        || e.Origin != source.Origin || e.PushDistance != source.Distance || !Finite(e.Center)
                        || !float.IsFinite(e.Limit) || e.Limit is <= 0 or > 10000) return false;
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
        var radius = actor.HitboxRadius + IntegrationRoutes.SpatialMargin;
        foreach (var pose in poses)
        {
            if (pose == null || !budget.Step()) return null;
            var geometry = IntegrationCounterfactualProjection.Project(component, actor, slot, pose, budget);
            if (geometry?.Movements is not [{ SourceIndex: 0 } step]) return null;
            var landing = new WPos(step.To[0], step.To[1]);
            var arena = IntegrationArena.Query(module.Bounds, module.Center, null, landing, actor.HitboxRadius);
            if (!arena.Available || arena.SignedClearance is not { } clearance || !float.IsFinite(clearance)) return null;
            var arenaClear = arena.Contains == true && clearance >= radius;
            var envelopesClear = envelopes.All(e => e.Kind == "circle" ? e.Limit - (landing - e.Center).Length() >= radius
                : Math.Min(e.Limit - Math.Abs(landing.X - e.Center.X), e.Limit - Math.Abs(landing.Z - e.Center.Z)) >= radius);
            var status = "clear";
            var reason = "current-published-regions-at-native-knockback-estimate";
            string? blocker = null;
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
                { status = "blocked"; reason = "independent-region-footprint-clearance"; blocker = region.Source == 0 ? null : region.Source.ToString("X"); }
            }
            if (status == "clear" && step.NativeDestinationContext != "native-base-arena-rule")
            { status = "unknown"; reason = "native-override-receives-actual-actor-context"; }
            if (!budget.Step()) return null;
            result.Add(new(geometry, status, reason, source.Activation.Ticks.ToString(CultureInfo.InvariantCulture),
                separated, independent.Count, later, arenaClear, envelopesClear, blocker));
        }
        return [.. result];
    }
}
