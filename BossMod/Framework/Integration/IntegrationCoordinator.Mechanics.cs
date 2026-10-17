using BossMod.Components;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal string GetMechanics(string request) => WithContext(request, context =>
        bossmod.ActiveModule is { } module && World.Party.Player() is { } actor && module.StateMachine.ActivePhase != null
            ? IntegrationSession.Serialize(IntegrationMechanics.Capture(module, actor, context))
            : Unavailable("no-active-module-or-player"));

    internal sealed record ProjectionRequest(IntegrationSession.Context Context, string ComponentId, float[] Position, float Facing);

    // This initial family observes native projections at the actual actor pose only. It cannot
    // manufacture a counterfactual actor or drop independent native constraints to declare safety.
    internal string EvaluateMechanicMovement(string json)
    {
        var request = IntegrationSession.Parse<ProjectionRequest>(json);
        if (request == null || !Session.Matches(request.Context) || World.Party.Player() is not { } actor
            || bossmod.ActiveModule is not { } module || module.StateMachine.ActivePhase == null
            || request.Position is not { Length: 2 } || request.Position.Any(v => !float.IsFinite(v)) || !float.IsFinite(request.Facing))
            return Unavailable("invalid-projection-context");
        if (new WPos(request.Position[0], request.Position[1]) != actor.Position || request.Facing != actor.Rotation.Rad)
            return Unavailable("counterfactual-projection-unsupported");
        var slot = World.Party.FindSlot(actor.InstanceID);
        if (slot < 0) return Unavailable("actor-not-in-native-party");
        try
        {
            var component = module.Components.Take(IntegrationMechanics.MaxComponents).FirstOrDefault(c => IntegrationMechanics.ComponentId(c) == request.ComponentId);
            var movements = component == null ? null : IntegrationMechanics.ObserveCurrentProjection(component, actor, slot);
            if (movements == null)
                return Unavailable("unsupported-or-bounded-out-component");
            return IntegrationSession.Serialize(new
            {
                available = true, context = request.Context, request.ComponentId, actorId = actor.InstanceID.ToString("X"), movements,
                status = "unknown", reason = "native-component-projection-only", independentHazardsEvaluated = false,
                recoveryReachabilityEvaluated = false, counterfactualSupported = false,
                meaning = "native ordered projection and DestinationUnsafe observations; never a complete safety approval"
            });
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Native projection failed: {error.GetType().Name}: {error.Message}");
            return Unavailable("native-projection-unavailable");
        }
    }
}
