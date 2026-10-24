namespace BossMod.CombatAssistance.Planning;

// One observed available Ley Lines opportunity; RSR must approve commitment. This is a possible placement,
// not an invented field object, buff expiry or forecast haste multiplier.
internal sealed record PlanningFieldPlacement(long Opportunity, float Earliest, float Latest, float LockSeconds, WPos? OriginalPosition = null)
{
    internal const uint ActionId = 3573;
    internal const float Radius = 3;
    internal bool Valid => Opportunity > 0 && float.IsFinite(Earliest) && Earliest >= 0
        && float.IsFinite(Latest) && Latest >= Earliest && Latest <= MovementPlanningContext.MaximumHorizonSeconds
        && float.IsFinite(LockSeconds) && LockSeconds > 0
        && (OriginalPosition == null || float.IsFinite(OriginalPosition.Value.X) && float.IsFinite(OriginalPosition.Value.Z));
    internal bool Same(PlanningFieldPlacement other, float age) => Opportunity == other.Opportunity
        && LockSeconds == other.LockSeconds && OriginalPosition == other.OriginalPosition && Math.Abs(Earliest - Math.Max(0, other.Earliest - age)) < .01f
        && Math.Abs(Latest - Math.Max(0, other.Latest - age)) < .01f;
    internal float Window(MovementPlanningContext context, WPos position, float end)
        => context.Traverse(position, position, end, 0, out var slack)
            ? Math.Max(0, Math.Min(MovementPlanningContext.MaximumHorizonSeconds - end, slack)) : 0;
    internal bool MayImprove(MovementPlanningContext context)
        => Window(context, OriginalPosition ?? context.Origin, Earliest + LockSeconds)
            < Math.Max(0, MovementPlanningContext.MaximumHorizonSeconds - (Earliest + LockSeconds));
    internal float Gain(MovementPlanningContext context, WPos position, float end)
        => Math.Max(0, Window(context, position, end) - Window(context, OriginalPosition ?? context.Origin, Earliest + LockSeconds));
}
