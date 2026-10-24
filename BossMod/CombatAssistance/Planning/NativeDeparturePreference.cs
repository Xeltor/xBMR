namespace BossMod.CombatAssistance.Planning;

// Departure is chosen from the complete route's proved slack before publication.
// This never selects a new endpoint, predicts another attack, or delays a
// delivered current action. Exact validation includes speed-effect expiry.
internal static class NativeDeparturePreference
{
    internal static MovementPlan Apply(MovementPlanningContext context, MovementPlan plan)
    {
        if (context.ForcedMovement != null || plan.Stages.Any(stage => stage.Kind == MovementStageKind.Knockback) || context.LateDepartureMargin is not { } margin || !plan.HasVerifiedRoute
            || plan.PlanSlackSeconds == float.MaxValue || plan.PlanSlackSeconds <= margin
            || context.Actions.Length != 0 && (plan.DeliveredActions & 1) == 0)
            return plan;
        var first = -1;
        for (var i = 0; i < plan.Stages.Length; ++i)
            if (plan.Stages[i].From != plan.Stages[i].To) { first = i; break; }
        if (first < 0 || plan.Stages.Skip(first).Any(s => s.Kind == MovementStageKind.Cast)) return plan;
        var extra = Math.Min(plan.PlanSlackSeconds - margin,
            MovementPlanningContext.MaximumHorizonSeconds - plan.DurationSeconds);
        if (extra <= 0 || !float.IsFinite(extra)) return plan;
        var next = plan.Stages[first];
        var stages = ImmutableArray.CreateBuilder<MovementStage>();
        stages.AddRange(plan.Stages.Take(first));
        stages.Add(new(MovementStageKind.Wait, next.From, next.From, next.StartSeconds, next.StartSeconds + extra));
        stages.AddRange(plan.Stages.Skip(first).Select(s => s with { StartSeconds = s.StartSeconds + extra, EndSeconds = s.EndSeconds + extra }));
        var phases = 0;
        MovementStageKind? previous = null;
        foreach (var stage in stages)
        {
            if (stage.Kind is not (MovementStageKind.Walk or MovementStageKind.Wait) || previous != stage.Kind) ++phases;
            previous = stage.Kind;
        }
        if (phases > MovementPlanningContext.MaximumPhases) return plan;
        var delayed = plan with
        {
            Stages = stages.ToImmutable(),
            DurationSeconds = plan.DurationSeconds + extra,
            PlanSlackSeconds = plan.PlanSlackSeconds - extra,
            Evidence = plan.Evidence + "; departure wait included without delaying evidenced actions"
        };
        return MovementPlanValidation.Validate(delayed, context, 0, true, context.Identity.CapturedTimestamp).Allowed ? delayed : plan;
    }
}
