using BossMod.Components;
using System.Reflection;

namespace BossMod.Integration;

internal static class IntegrationDisplacementObservation
{
    internal sealed record Result(string State, bool Complete, bool NoPublishedNativeDisplacementVerified,
        bool ForeignProjectionPresent, bool PendingKnockbackPresent, bool ForcedMovementPresent,
        bool ForcedMarchPresent, object[] Components, string Meaning);
    internal static Result Capture(BossModule? module, Actor actor, AIHints hints, IntegrationRoutes.Budget budget)
    {
        var complete = (module?.Components.Count ?? 0) <= 64 && actor.PendingKnockbacks != null;
        var native = false; var march = false; var foreign = false; var poseDependent = false;
        var components = new List<object>();
        var totalSources = 0; var totalWalls = 0;
        var slot = module?.Raid.FindSlot(actor.InstanceID) ?? -1;
        if (module != null)
            foreach (var component in module.Components.Take(64))
            {
                if (!budget.Step()) { complete = false; break; }
                if (component is GenericKnockback knockback)
                {
                    if (slot < 0) { complete = false; continue; }
                    var sources = knockback.ActiveKnockbacks(slot, actor);
                    if (sources.Length > 32) { complete = false; continue; }
                    if (sources.ToArray().Any(s => s.SafeWalls == null || s.SafeWalls.Length > 32)) { complete = false; continue; }
                    totalSources += sources.Length; totalWalls += sources.ToArray().Sum(s => s.SafeWalls.Length);
                    if (totalSources > 128 || totalWalls > 512) { complete = false; continue; }
                    if (sources.ToArray().Any(s => s.Shape != null && IntegrationMechanics.Describe(s.Shape).Status != "known")) complete = false;
                    native |= sources.Length != 0;
                    // An empty actual-pose custom publication cannot prove absence at candidate starts.
                    poseDependent |= sources.Length == 0 && !knockback.SupportsCounterfactualProjection;
                    components.Add(new { componentId = IntegrationMechanics.ComponentId(component), publishedSourceCount = sources.Length,
                        poseIndependentPublication = knockback.SupportsCounterfactualProjection, knockback.StopAtWall, knockback.StopAfterWall, knockback.MaxCasts,
                        sources = sources.ToArray().Select(s => new { origin = new[] { s.Origin.X, s.Origin.Z }, s.Distance, direction = s.Direction.Rad,
                            kind = s.Kind.ToString(), s.MinDistance, activationTicks = s.Activation.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            s.IgnoreImmunes, immuneAtActivation = knockback.IsImmune(slot, s.Activation), sourceActorId = s.ActorID == 0 ? null : s.ActorID.ToString("X"), s.ArenaProjectionLayer, s.RestrictToArenaProjectionLayer,
                            shape = s.Shape == null ? null : IntegrationMechanics.Describe(s.Shape),
                            walls = s.SafeWalls.Select(w => new[] { w.Vertex1.X, w.Vertex1.Z, w.Vertex2.X, w.Vertex2.Z }).ToArray() }).ToArray() });
                }
                if (component is GenericForcedMarch forced) march |= forced.HasForcedMovements(actor);
            }
        var visited = new Dictionary<ShapeDistance, int>(ReferenceEqualityComparer.Instance);
        void Inspect(ShapeDistance shape, bool attributed, int depth)
        {
            if (!budget.Step() || depth > 8 || visited.Count >= 128) { complete = false; return; }
            if (shape is IKnockbackPreparationConstraint) { if (!attributed) foreign = true; return; }
            var flag = attributed ? 1 : 2;
            if (visited.TryGetValue(shape, out var prior) && (prior & flag) != 0) return;
            visited[shape] = prior | flag;
            for (var type = shape.GetType(); type != null && type != typeof(ShapeDistance); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.GetValue(shape) is ShapeDistance child) Inspect(child, attributed, depth + 1);
                    else if (field.GetValue(shape) is ShapeDistance[] children)
                    {
                        if (children.Length > 128) { complete = false; continue; }
                        foreach (var item in children) if (item != null) Inspect(item, attributed, depth + 1); else complete = false;
                    }
                }
        }
        foreach (var zone in hints.ForbiddenZones)
        {
            var producer = hints.ProjectionProducer(zone.shapeDistance, zone.activation, zone.Source);
            Inspect(zone.shapeDistance, producer is GenericKnockback, 0);
        }
        foreach (var obstacle in hints.TemporaryObstacles) Inspect(obstacle, false, 0);
        var pending = actor.PendingKnockbacks?.Count > 0;
        var forcedMovement = hints.ForcedMovement != null;
        var none = complete && !poseDependent && !native && !march && !foreign && !pending && !forcedMovement;
        return new(foreign ? "foreign-projection" : native || march || pending || forcedMovement ? "native-displacement"
            : none ? "none-published" : "unknown", complete && !poseDependent, none, foreign, pending, forcedMovement, march, [.. components],
            "current bounded native publications, pending state, custom pose-dependence and exact native projection markers; never infer absence from empty mechanic entries or grant immunity/action permission");
    }
}
