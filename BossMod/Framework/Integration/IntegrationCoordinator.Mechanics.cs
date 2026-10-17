using BossMod.Components;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal string GetMechanics(string request) => WithContext(request, context =>
        bossmod.ActiveModule is { } module && World.Party.Player() is { } actor && module.StateMachine.ActivePhase != null
            ? IntegrationSession.Serialize(IntegrationMechanics.Capture(module, actor, context))
            : Unavailable("no-active-module-or-player"));

    internal sealed record ProjectionRequest(IntegrationSession.Context Context, string ComponentId, float[] Position, float Facing);
    internal sealed record ProjectionPoses(IntegrationSession.Context Context, string ComponentId, IntegrationCounterfactualProjection.Pose[] Poses);

    internal string EvaluateKnockbackLandings(string json)
    {
        var request = IntegrationSession.Parse<ProjectionPoses>(json);
        if (request == null || !Session.Matches(request.Context) || World.Party.Player() is not { IsDead: false, IsDestroyed: false } actor
            || bossmod.ActiveModule is not { } module || module.StateMachine.ActivePhase == null
            || request.Poses is not { Length: >= 1 and <= IntegrationCounterfactualProjection.MaxPoses }
            || hints.ProjectionSourcesContext is not { } captured || captured.InstanceGeneration != request.Context.InstanceGeneration
            || captured.FrameId != request.Context.FrameId || !hints.ProjectionSourcesValid)
            return Unavailable("invalid-or-uncaptured-landing-context");
        var slot = World.Party.FindSlot(actor.InstanceID);
        if (slot < 0) return Unavailable("actor-not-in-native-party");
        try
        {
            var component = module.Components.Take(IntegrationMechanics.MaxComponents)
                .FirstOrDefault(c => IntegrationMechanics.ComponentId(c) == request.ComponentId) as GenericKnockback;
            var observations = component == null ? null : IntegrationLandingConstraints.Evaluate(hints, module, component, actor, slot,
                request.Poses, new());
            if (observations == null) return Unavailable("unsupported-ambiguous-stale-or-bounded-out-landing-constraints");
            return IntegrationSession.Serialize(new
            {
                available = true, context = request.Context, request.ComponentId, actorId = actor.InstanceID.ToString("X"), observations,
                status = "unknown", independentPublishedConstraintsEvaluated = true,
                physicalObstaclesEvaluated = false, preparationReachabilityEvaluated = false, recoveryReachabilityEvaluated = false,
                sourceTimeMeaning = "native activation estimate; not verified server snapshot or last-safe movement time",
                meaning = "current published point/footprint constraints at the estimated landing; never complete mechanic/route safety or action approval"
            });
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Landing constraints unavailable: {error.GetType().Name}: {error.Message}");
            return Unavailable("native-landing-constraints-unavailable");
        }
    }

    internal string ProjectMechanicPoses(string json)
    {
        var request = IntegrationSession.Parse<ProjectionPoses>(json);
        if (request == null || !Session.Matches(request.Context) || World.Party.Player() is not { IsDead: false } actor
            || bossmod.ActiveModule is not { } module || module.StateMachine.ActivePhase == null
            || string.IsNullOrEmpty(request.ComponentId) || request.Poses is not { Length: >= 1 and <= IntegrationCounterfactualProjection.MaxPoses })
            return Unavailable("invalid-counterfactual-projection-context");
        var slot = World.Party.FindSlot(actor.InstanceID);
        if (slot < 0) return Unavailable("actor-not-in-native-party");
        try
        {
            var component = module.Components.Take(IntegrationMechanics.MaxComponents)
                .FirstOrDefault(c => IntegrationMechanics.ComponentId(c) == request.ComponentId);
            if (component == null || !IntegrationCounterfactualProjection.Supports(component))
                return Unavailable("unsupported-or-expired-projection-component");
            var budget = new IntegrationRoutes.Budget();
            var results = new List<IntegrationCounterfactualProjection.Result>();
            foreach (var pose in request.Poses)
            {
                if (pose == null) return Unavailable("invalid-projection-pose");
                var result = IntegrationCounterfactualProjection.Project(component, actor, slot, pose, budget);
                if (result == null) return Unavailable("unsupported-invalid-or-bounded-out-projection");
                results.Add(result);
            }
            return IntegrationSession.Serialize(new
            {
                available = true, context = request.Context, request.ComponentId, actorId = actor.InstanceID.ToString("X"), poses = results,
                status = "unknown", reason = "native-counterfactual-geometry-only", counterfactualSupported = true,
                sourceScope = "current-native-visible-projection; not complete encounter or future pulse coverage",
                independentHazardsEvaluated = false, recoveryReachabilityEvaluated = false,
                actorContext = "actual actor identity, height, layer, statuses and immunity; hypothetical horizontal position/facing only",
                meaning = "native geometry and actual-actor-context DestinationUnsafe observations; never a complete safety approval"
            });
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Counterfactual projection failed: {error.GetType().Name}: {error.Message}");
            return Unavailable("native-counterfactual-projection-unavailable");
        }
    }

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
