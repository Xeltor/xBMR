namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    // Called at the actual native writes, rather than inferred later from a value
    // which another frame or an interaction may already have replaced.
    internal void OverrideAssigned(string reason)
    {
        var decision = hints.IntegrationControllerDecision;
        var fromController = reason == "native-hints" && !hints.AutomaticMovementPaused && World.Party.Player() is { } actor
            && decision?.Matches(Session.InstanceGeneration, Session.FrameId, actor.InstanceID, movement.DesiredDirection) == true;
        movement.IntegrationInput.AssignNative(Session.InstanceGeneration, Session.FrameId, movement.DesiredDirection,
            hints.AutomaticMovementPaused && reason == "native-hints" ? "native-movement-pause" : reason,
            fromController ? decision!.Id : null, fromController ? decision!.Owner : null,
            fromController ? decision!.NavigationGeneration : null);
    }

    private void ObserveApplicationBoundary()
    {
        var assignment = movement.IntegrationInput.CurrentAssignment;
        if (assignment == null || assignment.InstanceGeneration != Session.InstanceGeneration || assignment.FrameId != Session.FrameId
            || assignment.Direction != movement.DesiredDirection)
            movement.IntegrationInput.AssignNative(Session.InstanceGeneration, Session.FrameId, movement.DesiredDirection,
                "unattributed-application-boundary");
    }

    private static object? AssignmentObservation(IntegrationInputObservation.Assignment? assignment)
        => assignment is not { } a ? null : new
        {
            a.InstanceGeneration, a.FrameId, a.Generation, direction = V(a.Direction), a.Reason,
            a.ControllerDecisionId, a.ControllerOwner, a.ControllerNavigationGeneration
        };

    private object? ControllerObservation()
    {
        var decision = hints.IntegrationControllerDecision;
        if (decision == null || decision.InstanceGeneration != Session.InstanceGeneration || decision.FrameId != Session.FrameId
            || World.Party.Player()?.InstanceID != decision.ActorId) return null;
        return new
        {
            decision.Id, decision.InstanceGeneration, decision.FrameId, decision.Owner, actorId = decision.ActorId.ToString("X"),
            worldTimeTicks = T(decision.WorldTime), decision.AgeSeconds, origin = V(decision.Origin), destination = V(decision.Destination),
            direction = V(decision.Direction), decision.WroteMovement, decision.Reason,
            decision.NavigationGeneration, decision.NavigationFrameId, decision.NavigationInstance,
            navigationDestination = decision.NavigationDestination is { } first ? new[] { first.X, first.Y } : null,
            matchesCurrentHints = decision.WroteMovement && hints.ForcedMovement == decision.Direction,
            matchesEffectiveMovement = decision.Matches(Session.InstanceGeneration, Session.FrameId, decision.ActorId, hints.EffectiveForcedMovement)
                && !hints.AutomaticMovementPaused,
            routeSafetyVerified = false,
            meaning = "native controller outcome; source search can be older or absent; matching output is not a route safety approval"
        };
    }
}
