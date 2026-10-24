using BossMod.Pathfinding;
using System.Threading;

namespace BossMod.CombatAssistance.Planning;

// One job owns both the native recovery search and its action-aware optimization. Recovery
// remains available when strict elective routing has no solution (including already-active AOEs).
internal static class NativeMovementSearch
{
    internal static MovementPlan Run(MovementPlanningContext context, CancellationToken cancel, Action<MovementPlan> publish)
    {
        if (context.ForcedMovement != null || context.Native is not { } native)
            return JointMovementSearch.Search(context, cancellation: cancel, publishBest: publish);
        cancel.ThrowIfCancellationRequested();
        var recovery = native.Recover(new(), cancel);
        cancel.ThrowIfCancellationRequested();
        var recoveryPlan = new MovementPlan(context.Identity, MovementSearchStatus.NativeRecovery, [],
            recovery.Publication?.Endpoint ?? context.Origin, recovery.Destination, recovery.LeewaySeconds, recovery.TimeToGoal,
            context.Resources, 0, [], float.MinValue, 0, 0,
            native.HasTeleporters ? "authored native teleporter recovery" : !context.Optimize ? "native movement; combat objectives not requested"
                : "native recovery; strict complete route unavailable")
        { NativeRecovery = recovery, TargetId = context.TargetId };
        if (!context.Optimize || context.StationaryOnly && recovery.Destination is { } required
            && !context.Origin.AlmostEqual(required, MovementPlanValidation.ArrivalTolerance))
            return recoveryPlan;
        // The owner may need mandatory native recovery before elective optimization
        // finishes. Publish it promptly, with recovery status rather than a safety proof.
        publish(recoveryPlan with { OptimizationPending = true });
        var incumbent = recovery.Publication is { } publication ? NativeMovementIncumbent.Capture(context, publication) : null;
        if (!context.Actions.IsEmpty)
        {
            // Enumerating every legal directional landing at every visited point
            // can postpone the first useful positional route. Find a complete
            // walking/action alternative using the SAME search and safety model,
            // then continue all mobility alternatives in this one worker. Neither
            // phase renews the capture or its real departure deadline.
            var walking = JointMovementSearch.Search(context, cancellation: cancel, nativeIncumbent: incumbent,
                includeMobilityActions: false, stopAtCurrentOpportunity: true);
            if (walking.Status == MovementSearchStatus.Invalidated) return walking;
            if (walking.HasVerifiedRoute && walking.DeliveredActions == NativeActionSequence.CompleteMask(context.Actions.Length))
            {
                incumbent = walking;
                publish(walking with { NativeRecovery = recovery });
            }
        }
        var result = JointMovementSearch.Search(context, cancellation: cancel, nativeIncumbent: incumbent,
            publishBest: plan => publish(plan with { NativeRecovery = recovery }));
        if (result.Status == MovementSearchStatus.Invalidated)
            return result;
        return result.HasVerifiedRoute ? result with { NativeRecovery = recovery }
            : recoveryPlan with { Bound = result.Bound, ExpandedStates = result.ExpandedStates, SearchMilliseconds = result.SearchMilliseconds };
    }
}
