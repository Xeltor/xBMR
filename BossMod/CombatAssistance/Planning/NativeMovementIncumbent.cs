using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

// Native safety recovery is computed independently of the additional optimization budget.
// Only a complete witness re-proved on the unrelaxed map can seed elective optimization.
internal static class NativeMovementIncumbent
{
    internal static MovementPlan? Capture(MovementPlanningContext context, NavigationPublication publication)
    {
        if (publication.Source != context.Identity || !publication.WitnessComplete || publication.HasTeleporters)
            return null;
        return CaptureRoute(context, publication.CopyWitness());
    }

    internal static MovementPlan? CaptureRoute(MovementPlanningContext context, ReadOnlySpan<WPos> witness, float departure = 0)
    {
        if (witness.IsEmpty || !witness[0].AlmostEqual(context.Origin, 0.001f))
            return null;
        if (!float.IsFinite(departure) || departure < 0 || departure > MovementPlanningContext.MaximumHorizonSeconds) return null;
        var time = Math.Max(context.UnlockSeconds, departure);
        var slack = float.MaxValue;
        var stages = ImmutableArray.CreateBuilder<MovementStage>();
        if (time > 0)
        {
            if (!context.Traverse(context.Origin, context.Origin, 0, time, out slack))
                return null;
            stages.Add(new(MovementStageKind.Wait, context.Origin, context.Origin, 0, time));
        }
        for (var i = 1; i < witness.Length; ++i)
        {
            var duration = context.Travel.Seconds((witness[i] - witness[i - 1]).Length(), time);
            if (time + duration > MovementPlanningContext.MaximumHorizonSeconds
                || !context.ExecutionRules.PermitsMovement(time, time + duration)
                || !context.TraverseOrdinary(witness[i - 1], witness[i], time, duration, out var segmentSlack))
                return null;
            slack = Math.Min(slack, segmentSlack);
            stages.Add(new(MovementStageKind.Walk, witness[i - 1], witness[i], time, time + duration));
            time += duration;
        }
        var cell = context.Cell(witness[^1]);
        if (cell < 0 || !context.DestinationAllowed(cell) || !context.CanRemain(witness[^1]) || !context.DestinationSatisfied(witness[^1], time))
            return null;
        return new(context.Identity, MovementSearchStatus.Bounded, stages.ToImmutable(), witness[^1], witness.Length > 1 ? witness[1] : null,
            slack, time, PlanningResourceState.Initial(context.Resources, context.Recharges).At(time, context.Recharges).Charges, 0, ImmutableArray.CreateRange(Enumerable.Repeat(0f, context.Actions.Length)),
            context.NativePriority(cell), 0, 0, "verified native witness; optimization not yet searched")
        { TargetId = context.TargetId };
    }
}
