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
        MovementPlan Refine(MovementPlan plan) => NativeMechanicClearance.Refine(context, plan, cancel);
        void Publish(MovementPlan plan)
        {
            // An inside search incumbent is not an outside-space fallback until
            // the current search pass has actually returned. Native safety
            // recovery remains independently available while it runs.
            if (plan.Status == MovementSearchStatus.Searching && (!context.PreferredEnemyEndpoint(plan.UsefulDestination)
                || !context.PreferredArenaEndpoint(plan.UsefulDestination))) return;
            publish(Refine(plan));
        }
        if (context.ForcedMovement != null || context.Native is not { } native)
            return Refine(JointMovementSearch.Search(context, cancellation: cancel, publishBest: Publish));
        cancel.ThrowIfCancellationRequested();
        var recoveryPlan = Recover(context, recoveryContext, cancel);
        var recovery = recoveryPlan.NativeRecovery!.Value;
        cancel.ThrowIfCancellationRequested();
        if (!context.Optimize || context.StationaryOnly && recovery.Destination is { } required
            && !context.Origin.AlmostEqual(required, MovementPlanValidation.ArrivalTolerance))
            return recoveryPlan;
        // The owner may need mandatory native recovery before elective optimization
        // finishes. Publish it promptly, with recovery status rather than a safety proof.
        Publish(recoveryPlan with { OptimizationPending = true });
        var incumbent = recovery.Publication is { } publication ? NativeMovementIncumbent.Capture(context, publication) : null;
        if (NativeWalkingApproach.Search(context, cancel, plan => Publish(plan with { NativeRecovery = recovery })) is { } approach)
            incumbent = approach;
        if (!context.Actions.IsEmpty || context.Range?.NeedsRecovery(context.Origin) == true
            || context.PartyContact?.Loss(context.Origin) > 0
            || context.HealingCoverage?.Satisfied(context.Origin) == false
            || incumbent is { } nativeRoute && !nativeRoute.UsefulDestination.AlmostEqual(context.Origin, MovementPlanValidation.ArrivalTolerance))
        {
            // Compare walking, Sprint, anchors, returns and deliberate spatial
            // approaches together before full map/facing enumeration. The full
            // pass still considers every supported mobility mechanic and landing.
            var opportunity = JointMovementSearch.Search(context, cancellation: cancel, nativeIncumbent: incumbent,
                stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false,
                publishBest: plan =>
                {
                    // Publish useful complete walking progress too: a distant or
                    // moving target must not leave us idle while alternatives
                    // are enumerated. Keep present actions and preparation intact.
                    if (context.PreservesPresent(plan.DeliveredActions) && context.CompletePreparation(plan.DeliveredActions)
                        && (context.Range is { } range && range.NeedsRecovery(context.Origin)
                            && range.Loss(plan.UsefulDestination) < range.Loss(context.Origin)
                        || context.PartyContact is { } contact && contact.Loss(plan.UsefulDestination) < contact.Loss(context.Origin)
                        || context.HealingCoverage?.Compare(plan.UsefulDestination, context.Origin) > 0
                        || plan.Stages.Any(s => s.Kind is MovementStageKind.TargetDash or MovementStageKind.DirectionalMovement
                        or MovementStageKind.GroundMovement or MovementStageKind.Return
                        || s.Kind == MovementStageKind.UtilityAction && context.Utilities.Any(u => u.ActionId == s.ActionId
                            && u.Effect == PlanningUtilityEffect.MovementSpeed))))
                        Publish(plan with { NativeRecovery = recovery });
                });
            if (opportunity.Status == MovementSearchStatus.Invalidated) return opportunity;
            if (opportunity.HasVerifiedRoute && context.PreservesPresent(opportunity.DeliveredActions))
            {
                incumbent = opportunity;
                Publish(opportunity with { NativeRecovery = recovery });
            }
        }
        var result = JointMovementSearch.Search(context, cancellation: cancel, nativeIncumbent: incumbent,
            publishBest: plan => Publish(plan with { NativeRecovery = recovery }));
        if (result.Status == MovementSearchStatus.Invalidated)
            return result;
        return result.HasVerifiedRoute ? Refine(result with { NativeRecovery = recovery })
            : recoveryPlan with { Bound = result.Bound, ExpandedStates = result.ExpandedStates, SearchMilliseconds = result.SearchMilliseconds };
    }
}
