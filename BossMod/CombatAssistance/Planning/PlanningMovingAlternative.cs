namespace BossMod.CombatAssistance.Planning;

// Explicit action equivalence, separate from target choice. Forked/Fleeting
// consume the same Raiju opportunity and hit the same one target. A movement
// action may not receive credit for an unrelated selected attack or heal.
internal sealed record PlanningMovingAlternative(uint ActionId, ExecutionOpportunity Opportunity,
    float LockSeconds, PlanningCastTiming Timing)
{
    internal bool Preserves(FrozenPlanningAction source) => source.Id == 25778 && ActionId == 25777
        && source.IsGCD && source.CastSeconds == 0 && source.ResourceCost != 0
        && source.Opportunity is { Shape: ExecutionShape.SingleTarget } original
        && Opportunity.Shape == ExecutionShape.SingleTarget && Opportunity.TargetId == source.TargetId
        && Opportunity.CastReach >= original.CastReach && Opportunity.WithCastReach(original.CastReach).SameGeometry(original)
        && float.IsFinite(LockSeconds) && LockSeconds >= 0 && Timing.Valid && Timing.WithoutProc == 0
        && source.Timing is { NativeRecast: > 0 } before && Timing.NativeRecast > 0
        && Timing.NativeRecast <= before.NativeRecast;

    internal bool Same(PlanningMovingAlternative other, float age, bool revalidation) => ActionId == other.ActionId
        && (revalidation || LockSeconds == other.LockSeconds) && Timing.Same(other.Timing, age)
        && (revalidation ? Opportunity.SameBinding(other.Opportunity) : Opportunity.SameGeometry(other.Opportunity));
}
