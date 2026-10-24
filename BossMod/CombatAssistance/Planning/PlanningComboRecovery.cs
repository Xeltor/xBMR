namespace BossMod.CombatAssistance.Planning;

// Optional preparation from observed rotation intent. Current selected actions
// rank first. A starter has a disclosed forecast horizon, not an invented combo
// expiry. Continuations and explicit rotation holds keep their original clocks.
internal sealed record PlanningComboRecovery(ulong TargetId, WPos Center, float Radius, float EndsIn)
{
    internal uint ActionId { get; init; }
    internal uint FallbackActionId { get; init; }
    internal uint AfterActionId { get; init; }
    internal float RecastSeconds { get; init; }
    internal float ActionLockSeconds { get; init; }
    internal float FallbackLockSeconds { get; init; }
    internal float EligibilityEndsIn { get; init; } = float.MaxValue;
    internal ComboIntentKind Kind { get; init; }
    internal int MinimumTargets { get; init; } = 1;
    internal ExecutionOpportunity? Opportunity { get; init; }
    internal ExecutionOpportunity? Fallback { get; init; }
    internal bool ForecastHorizon => Kind == ComboIntentKind.Starter;
    internal float MaximumValue => Math.Max(Opportunity == null ? 1 : Ceiling(Opportunity), Fallback == null ? 0 : Ceiling(Fallback));
    internal bool Valid => new PlanningRange(TargetId, Center, Radius).Valid && float.IsFinite(EndsIn) && EndsIn > 0
        && Enum.IsDefined(Kind) && MinimumTargets > 0 && float.IsFinite(RecastSeconds) && RecastSeconds >= 0
        && float.IsFinite(ActionLockSeconds) && ActionLockSeconds >= 0 && float.IsFinite(FallbackLockSeconds) && FallbackLockSeconds >= 0
        && float.IsFinite(EligibilityEndsIn) && EligibilityEndsIn > 0
        && (Opportunity == null || ActionId != 0 && Opportunity.TargetId == TargetId)
        && (Fallback == null || FallbackActionId != 0 && Fallback.TargetId == TargetId);
    internal float DeadlineTime(float arrival, float ready) => Kind == ComboIntentKind.RotationHold ? arrival : Math.Max(arrival, ready);
    internal float Value(WPos position, float arrival, float ready = 0, PlanningExecutionRules? rules = null)
    {
        if (!float.IsFinite(arrival) || !float.IsFinite(ready) || arrival < 0 || ready < 0
            || DeadlineTime(arrival, ready) >= EndsIn || Math.Max(arrival, ready) >= EligibilityEndsIn
            || Math.Max(arrival, ready) > MovementPlanningContext.MaximumHorizonSeconds) return 0;
        if (Opportunity == null) return position.InCircle(Center, Radius) ? 1 : 0;
        var main = Opportunity.Evaluate(position);
        var alternative = Fallback?.Evaluate(position) ?? default;
        var executeAt = Math.Max(arrival, ready);
        return Math.Max(main.Value >= MinimumTargets && Legal(Opportunity, main, ActionLockSeconds) ? main.Value : 0,
            Fallback != null && Legal(Fallback, alternative, FallbackLockSeconds) ? alternative.Value : 0);

        bool Legal(ExecutionOpportunity opportunity, ExecutionCoverage coverage, float actionLock)
        {
            if (!coverage.Allowed || executeAt + actionLock > MovementPlanningContext.MaximumHorizonSeconds) return false;
            var delta = opportunity.Anchor - position;
            var facing = coverage.Facing ?? (delta.LengthSq() > 1e-8f ? Angle.FromDirection(delta) : (Angle?)null);
            return rules == null || (facing != null || rules.ForbiddenFacing.IsEmpty) && rules.PermitsAction(facing, executeAt, executeAt + actionLock);
        }
    }
    internal bool Useful(WPos position, float arrival, float ready = 0) => Value(position, arrival, ready) > 0;
    internal bool Preserves(PlanningComboRecovery old) => TargetId == old.TargetId && ActionId == old.ActionId
        && FallbackActionId == old.FallbackActionId && AfterActionId == old.AfterActionId && RecastSeconds == old.RecastSeconds
        && ActionLockSeconds == old.ActionLockSeconds && FallbackLockSeconds == old.FallbackLockSeconds
        && Kind == old.Kind && MinimumTargets == old.MinimumTargets
        && KeepsRecipients(Opportunity, old.Opportunity) && KeepsRecipients(Fallback, old.Fallback);
    internal bool Same(PlanningComboRecovery old, float age) => TargetId == old.TargetId && Center == old.Center && Radius == old.Radius
        && Preserves(old) && SameGeometry(Opportunity, old.Opportunity) && SameGeometry(Fallback, old.Fallback)
        && (EligibilityEndsIn == float.MaxValue && old.EligibilityEndsIn == float.MaxValue
            || Math.Abs(EligibilityEndsIn - Math.Max(0, old.EligibilityEndsIn - age)) < 0.01f)
        && Math.Abs(EndsIn - (ForecastHorizon ? old.EndsIn : Math.Max(0, old.EndsIn - age))) < 0.01f;
    private static float Ceiling(ExecutionOpportunity opportunity) => opportunity.Recipients.Aggregate(0f, (sum, recipient) => sum + recipient.Value);
    private static bool SameGeometry(ExecutionOpportunity? a, ExecutionOpportunity? b) => a == null ? b == null : b != null && a.SameGeometry(b);
    private static bool KeepsRecipients(ExecutionOpportunity? current, ExecutionOpportunity? old) => old == null ? current == null
        : current != null && old.Recipients.Where(r => r.Required).All(r => current.Recipients.Any(c => c.Id == r.Id && c.Required));
}
