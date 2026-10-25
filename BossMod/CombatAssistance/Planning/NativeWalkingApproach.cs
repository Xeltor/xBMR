using BossMod.Pathfinding;
using System.Threading;

namespace BossMod.CombatAssistance.Planning;

// Propose an ordinary walking corridor with native ThetaStar, then use the
// existing joint action/route validator on that corridor before full mobility
// optimization. This is a bounded alternative, never a new safety implementation.
internal static class NativeWalkingApproach
{
    internal static MovementPlan? Search(MovementPlanningContext context, CancellationToken cancel = default,
        Action<MovementPlan>? publish = null)
    {
        if (!context.Optimize || context.StationaryOnly || context.ForcedMovement != null || context.KnockbackPhases?.Phases.IsEmpty == false
            || !context.Teleports.IsEmpty || context.FieldPlacement != null
            || context.MobilityMechanics.Any(m => context.Actions.Any(a => a.Id == m.ActionId))) return null;
        var goal = context.Range?.NeedsRecovery(context.Origin) == true ? context.Range
            : context.PartyContact?.Loss(context.Origin) > MovementPlanValidation.ArrivalTolerance ? context.PartyContact : null;
        if (goal == null) return null;
        var map = context.CopyMap();
        var maximum = Enumerable.Range(0, context.Cells).Where(i => context.DestinationAllowed(i) && context.HasPermanentCell(i))
            .Select(context.NativePriority).DefaultIfEmpty(float.MinValue).Max();
        map.MaxPriority = float.MinValue;
        for (var cell = 0; cell < context.Cells; ++cell)
        {
            map.PixelPriority[cell] = context.DestinationAllowed(cell) && context.HasPermanentCell(cell)
                && context.NativePriority(cell) == maximum ? 1 / (1 + goal.Loss(context.Point(cell))) : float.MinValue;
            map.MaxPriority = Math.Max(map.MaxPriority, map.PixelPriority[cell]);
        }
        var native = new ThetaStar();
        native.Start(map, context.Origin, 1 / context.Identity.Speed);
        var last = native.ExecuteCancellable(cancel);
        var witness = NavigationDecision.CopyWitness(native, last, context.Origin, out var complete);
        if (!complete || witness.Length < 2 || goal.Loss(witness[^1]) >= goal.Loss(context.Origin)) return null;
        var corridor = new HashSet<int>();
        for (var i = 1; i < witness.Length; ++i)
        {
            var start = map.ClampToGrid(map.WorldToGrid(witness[i - 1]));
            var end = map.ClampToGrid(map.WorldToGrid(witness[i]));
            foreach (var cell in map.EnumeratePixelsInLine(start.x, start.y, end.x, end.y).Prepend(start))
                for (var dy = -1; dy <= 1; ++dy)
                    for (var dx = -1; dx <= 1; ++dx)
                        if (map.InBounds(cell.x + dx, cell.y + dy)) corridor.Add(map.GridToIndex(cell.x + dx, cell.y + dy));
        }
        MovementPlan? accepted = null;
        void Accept(MovementPlan plan)
        {
            if (!plan.HasVerifiedRoute || !context.PreservesPresent(plan.DeliveredActions) || !context.CompletePreparation(plan.DeliveredActions)
                || !context.PreferredEnemyEndpoint(plan.UsefulDestination)
                || !context.PreferredArenaEndpoint(plan.UsefulDestination)
                || goal.Loss(plan.UsefulDestination) >= goal.Loss(context.Origin)
                || !MovementPlanValidation.Validate(plan, context, 0, true).Allowed) return;
            accepted = plan with { Status = MovementSearchStatus.Bounded, Bound = MovementSearchBound.SearchInProgress,
                Evidence = "verified native walking corridor; full action/mobility optimization pending" };
            publish?.Invoke(accepted);
        }
        var result = JointMovementSearch.Search(context, cancellation: cancel, includeMobilityActions: false,
            stopAtCurrentOpportunity: true, walkingCorridor: corridor, publishBest: Accept);
        Accept(result);
        return accepted;
    }
}
