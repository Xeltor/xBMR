using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void DeparturePreference()
    {
        var map = Grid(3, 1);
        map.PixelMaxG[0] = 8;
        var action = Action(map, 2, 1, [1, 0, 0], [1, 0, 0], cast: 1);
        var input = Context(map, 0, [action]);
        var normal = JointMovementSearch.Search(input, Exhaustive);
        var context = new MovementPlanningContext(input.Identity, map, 0, 0, [true, true, true], [0, 0, 0], [action], [], lateDepartureMargin: 0.5f);
        var delayed = JointMovementSearch.Search(context, Exhaustive);
        Check(delayed.HasVerifiedRoute && delayed.UsefulDestination == normal.UsefulDestination && delayed.DeliveryTimes.SequenceEqual(normal.DeliveryTimes),
            "late departure preserves the first destination and the exact current action delivery");
        Check(delayed.Stages.Any(s => s.Kind == MovementStageKind.Wait && s.StartSeconds >= 1 && s.EndSeconds > 2)
            && delayed.PlanSlackSeconds < normal.PlanSlackSeconds,
            "late departure is an explicit verified wait before walking");
        Check(Math.Abs(delayed.DepartureDeadlineSeconds - normal.DepartureDeadlineSeconds) < 0.001f,
            "planned waiting does not renew the original last-safe departure deadline");
        var executor = new MovementPlanExecutor();
        var execution = executor.Advance(delayed, context, default, true, context.Identity.CapturedTimestamp);
        Check(execution.Kind == MovementExecutionKind.Wait && execution.ActionId == 1
            && execution.DepartureSeconds > action.CastSeconds && execution.DepartureSeconds > delayed.PlanSlackSeconds,
            "native follow receives the full cast window rather than only extra sequence slack");
        var recovery = Action(map, 3, 1, [0, 0, 1], [0, 0, 1]);
        var moveFirst = new MovementPlanningContext(input.Identity, map, 0, 0, [true, true, true], [0, 0, 0], [recovery], [], lateDepartureMargin: 0.5f);
        var movePlan = JointMovementSearch.Search(moveFirst, Exhaustive);
        Check(movePlan.Stages[0].Kind == MovementStageKind.Walk && movePlan.DeliveryTimes[0] <= 2.1f,
            "late preference cannot postpone an action that requires range recovery");
        var expiringSpeed = new MovementPlanningContext(input.Identity, map, 0, 0, [true, true, true], [0, 0, 0], [action], [],
            lateDepartureMargin: 0.5f, travel: new(0, 1, 3));
        var speedPlan = JointMovementSearch.Search(expiringSpeed, Exhaustive);
        Check(speedPlan.HasVerifiedRoute && speedPlan.DurationSeconds <= 3,
            "late departure is rejected when the proved escape speed would expire during the hold");
    }
}
