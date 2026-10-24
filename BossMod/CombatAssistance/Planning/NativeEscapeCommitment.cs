namespace BossMod.CombatAssistance.Planning;

// Once the chosen attack window ends and walking to shelter begins, finish that
// escape. A new rotation selection is not a new mechanic. The retained witness
// is re-proved on each fresh native map; it never retains an old deadline.
internal sealed class NativeEscapeCommitment(MovementPlanningContext source, ImmutableArray<WPos> waypoints)
{
    private int _next;

    internal static NativeEscapeCommitment? Begin(MovementPlanningContext context, MovementPlan plan, int stage)
    {
        var remaining = plan.Stages.Skip(stage).ToArray();
        return context.Native != null && !context.CanRemain(context.Origin)
            && context.ForcedMovement == null && context.KnockbackPhases?.Phases.IsEmpty != false
            && remaining.Length != 0 && remaining.All(s => s.Kind == MovementStageKind.Walk)
            ? new(context, remaining.Select(s => s.To).ToImmutableArray()) : null;
    }

    internal MovementPlan? Continue(MovementPlanningContext current)
    {
        if (current.Native == null || source.Native == null || !current.Native.SameSafetyMap(source.Native)
            || source.Identity.ActorId != current.Identity.ActorId || source.Identity.ZoneId != current.Identity.ZoneId
            || source.Identity.ContentId != current.Identity.ContentId || source.Identity.ProjectionLayer != current.Identity.ProjectionLayer
            || source.TargetId != current.TargetId || current.ForcedMovement != null
            || current.KnockbackPhases?.Phases.IsEmpty == false)
            return null;
        while (_next < waypoints.Length && current.Origin.AlmostEqual(waypoints[_next], MovementPlanValidation.ArrivalTolerance)) ++_next;
        var witness = new[] { current.Origin }.Concat(waypoints.Skip(_next)).ToArray();
        var plan = NativeMovementIncumbent.CaptureRoute(current, witness);
        return plan != null && MovementPlanValidation.Validate(plan, current, 0, true, current.Identity.CapturedTimestamp).Allowed
            ? plan with { Evidence = "committed native escape; same mechanic, freshly revalidated complete route" } : null;
    }
}
