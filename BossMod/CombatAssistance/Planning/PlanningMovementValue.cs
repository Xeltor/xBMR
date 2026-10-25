namespace BossMod.CombatAssistance.Planning;

// A job's need for stationary time is a route cost, not a prediction of future
// damage. Only the observed GCD clock and delivered actions grant free movement.
internal sealed record PlanningMovementValue(Class Job, bool CastBound, float ReadyIn)
{
    internal static readonly PlanningMovementValue None = new(Class.None, false, 0);

    // Native recast clocks are sampled in milliseconds. Sub-millisecond float
    // summation differences must not purchase a cooldown or an extra step.
    internal static int Compare(float first, float second)
        => Math.Abs(first - second) <= .001f ? 0 : first.CompareTo(second);

    internal static PlanningMovementValue Capture(Class job, float readyIn, ImmutableArray<PlanningAction> actions)
        => new(job, job is Class.THM or Class.BLM or Class.PCT or Class.RDM
            or Class.CNJ or Class.WHM or Class.SCH or Class.AST or Class.SGE or Class.ACN or Class.BLU
            || actions.Any(a => a.CastSeconds > 0), Math.Max(0, readyIn));

    internal bool Same(PlanningMovementValue previous, float age)
        => Job == previous.Job && CastBound == previous.CastBound
            && Math.Abs(ReadyIn - Math.Max(0, previous.ReadyIn - age)) <= 0.01f;

    internal (float Loss, float CoveredUntil) Accumulate(MovementPlanningContext context,
        ImmutableArray<MovementStage> stages, float loss, float coveredUntil, float gcdReadyAt)
    {
        if (!CastBound) return (0, 0);
        var freeUntil = Math.Max(ReadyIn, gcdReadyAt);
        foreach (var stage in stages)
        {
            if (stage.Kind != MovementStageKind.Target && context.ActionFor(stage) is { IsGCD: true }
                && float.IsFinite(stage.PlannedRecastSeconds))
                freeUntil = Math.Max(freeUntil, stage.StartSeconds + stage.PlannedRecastSeconds);
            if (stage.Kind is not (MovementStageKind.Walk or MovementStageKind.TargetDash
                or MovementStageKind.GroundMovement or MovementStageKind.DirectionalMovement
                or MovementStageKind.Return or MovementStageKind.UtilityAction)) continue;
            var end = float.IsFinite(stage.ActionLockUntil) ? Math.Max(stage.EndSeconds, stage.ActionLockUntil) : stage.EndSeconds;
            // Sprint can overlap walking. Charge the union, including any lock
            // left at arrival, rather than adding its animation lock twice.
            loss += Math.Max(0, end - Math.Max(stage.StartSeconds, Math.Max(freeUntil, coveredUntil)));
            coveredUntil = Math.Max(coveredUntil, end);
        }
        return (loss, coveredUntil);
    }
}
