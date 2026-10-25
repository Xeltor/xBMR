using BossMod.Pathfinding;
using System.Threading;

namespace BossMod.CombatAssistance.Planning;

// One job owns both the native recovery search and its action-aware optimization. Recovery
// remains available when strict elective routing has no solution (including already-active AOEs).
internal static class NativeMovementSearch
{
    internal static MovementPlan Recover(MovementPlanningContext context, NavigationDecision.Context inspection,
        CancellationToken cancel = default)
    {
        var native = context.Native ?? throw new InvalidOperationException("Native recovery requires a frozen navigation snapshot.");
        var recovery = native.Recover(inspection, cancel);
        return new(context.Identity, MovementSearchStatus.NativeRecovery, [],
            recovery.Publication?.Endpoint ?? context.Origin, recovery.Destination, recovery.LeewaySeconds, recovery.TimeToGoal,
            context.Resources, 0, [], float.MinValue, 0, 0,
            native.HasTeleporters ? "authored native teleporter recovery" : !context.Optimize ? "native movement; combat objectives not requested"
                : "native recovery; strict complete route unavailable")
        { NativeRecovery = recovery, TargetId = context.TargetId };
    }

    internal static MovementPlan Run(MovementPlanningContext context, CancellationToken cancel, Action<MovementPlan> publish)
        => Run(context, cancel, publish, new());

    // One service runs one worker at a time. Its recovery scratch stays worker
    // owned; publications contain copied route witnesses, never these buffers.
    internal static MovementPlan Run(MovementPlanningContext context, CancellationToken cancel, Action<MovementPlan> publish,
        NavigationDecision.Context recoveryContext)
    {
        if (context.ForcedMovement != null || context.Native is not { } native)
            return JointMovementSearch.Search(context, cancellation: cancel, publishBest: publish);
        cancel.ThrowIfCancellationRequested();
        var recoveryPlan = Recover(context, recoveryContext, cancel);
        var recovery = recoveryPlan.NativeRecovery!.Value;
        cancel.ThrowIfCancellationRequested();
        if (!context.Optimize || context.StationaryOnly && recovery.Destination is { } required
            && !context.Origin.AlmostEqual(required, MovementPlanValidation.ArrivalTolerance))
            return recoveryPlan;
        // The owner may need mandatory native recovery before elective optimization
        // finishes. Publish it promptly, with recovery status rather than a safety proof.
        publish(recoveryPlan with { OptimizationPending = true });
        var incumbent = recovery.Publication is { } publication ? NativeMovementIncumbent.Capture(context, publication) : null;
        if (!context.Actions.IsEmpty || context.Range?.NeedsRecovery(context.Origin) == true
            || context.HealingCoverage?.Satisfied(context.Origin) == false)
        {
            // Search walking plus finite legal anchors/returns before enumerating
            // every ground point and facing. A walking-only first publication can
            // otherwise commit lost uptime before the useful dash is considered.
            // The full pass still considers every supported mobility mechanic.
            var walking = JointMovementSearch.Search(context, cancellation: cancel, nativeIncumbent: incumbent,
                stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false,
                publishBest: plan =>
                {
                    // A complete mobility route must not remain private until
                    // this pass exhausts its walking/position alternatives.
                    // Initial native walking seeds still wait for comparison.
                    if (plan.Stages.Any(s => s.Kind is MovementStageKind.TargetDash or MovementStageKind.DirectionalMovement
                        or MovementStageKind.GroundMovement or MovementStageKind.Return
                        || s.Kind == MovementStageKind.UtilityAction && context.Utilities.Any(u => u.ActionId == s.ActionId
                            && u.Effect == PlanningUtilityEffect.MovementSpeed)))
                        publish(plan with { NativeRecovery = recovery });
                });
            if (walking.Status == MovementSearchStatus.Invalidated) return walking;
            if (walking.HasVerifiedRoute && context.PreservesPresent(walking.DeliveredActions))
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
