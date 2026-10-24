namespace BossMod.CombatAssistance.Planning;

// Shared field-benefit check for search and exact route validation. Compare the
// selected spell and its own recovery; never invent the next rotational attack.
internal static class NativeFieldExecution
{
    internal static bool LosesCurrent(MovementPlanningContext context, FrozenPlanningAction action, WPos position,
        float begin, float end, ExecutionCoverage coverage, bool enteredByAbility, PlanningPersonalField? field, bool fieldBenefitAllowed = true)
    {
        if (action.Timing is not { ObservedFieldCast: not null } timing || !timing.Covers(position, begin, field)
            || action.RequiredEarlierActions != 0 || !context.CanRemain(context.Origin)
            || !context.DestinationAllowed(context.Cell(context.Origin))
            || context.RequiredDestination is { } required && required != context.Origin) return false;
        var baselineStart = Math.Max(context.UnlockSeconds, action.Earliest);
        var baselineDelivered = baselineStart + action.CastAt(context.Origin, baselineStart);
        var baselineEnd = baselineDelivered + action.LockSeconds;
        if (end - action.LockSeconds <= baselineDelivered
            && Math.Max(end, begin + timing.RecastAt(position, begin, enteredByAbility, field, fieldBenefitAllowed))
                <= Math.Max(baselineEnd, baselineStart + timing.RecastAt(context.Origin, baselineStart))) return false;
        var baseline = context.Evaluate(action, context.Origin, baselineDelivered < context.PositionalBypassUntil);
        return baseline.Allowed && baseline.Value >= coverage.Value && baselineDelivered <= action.Latest
            && context.ExecutionRules.PermitsAction(baseline.Facing, baselineStart, baselineEnd)
            && context.CanChooseStationaryAction(context.Origin)
            && context.Traverse(context.Origin, context.Origin, 0, baselineEnd, out _);
    }
}
