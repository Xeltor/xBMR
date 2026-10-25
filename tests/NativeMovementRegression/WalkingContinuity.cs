using BossMod;
using BossMod.Pathfinding;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void WalkingContinuity()
    {
        var map = Grid(5, 1);
        var seed = Context(map, 0, speed: 6);
        var goal = Point(map, 4);
        var action = Action(map, 5, 1, Enumerable.Repeat(1ul, 5).ToArray(), Enumerable.Repeat(1f, 5).ToArray(), earliest: 1.5f)
            with { IsGCD = true };
        MovementPlanningContext Input(PlanningAction current, WPos? committed) => new(seed.Identity, map, 0, 0,
            Enumerable.Repeat(true, 5).ToArray(), new float[5], [current], [], committedDestination: committed,
            range: new(10, Point(map, 2), 10, true, current.Earliest), targetId: 10);
        var ordinary = JointMovementSearch.Search(Input(action, null), Exhaustive, stopAtCurrentOpportunity: true);
        Check(ordinary.UsefulDestination == seed.Origin, "an uncommitted in-range actor need not start a cosmetic walk");
        var retained = JointMovementSearch.Search(Input(action, goal), Exhaustive, stopAtCurrentOpportunity: true);
        Check(retained.UsefulDestination == goal && retained.Stages[0].Kind == MovementStageKind.Walk,
            "early walking publication retains an equivalent committed destination before waiting for the next GCD");
        Check(retained.DeliveryTimes[0] == ordinary.DeliveryTimes[0] && retained.ExecutionValues[0] == ordinary.ExecutionValues[0],
            "walking continuity preserves exact current delivery time and recipient value");
        var immediate = JointMovementSearch.Search(Input(action with { Earliest = 0 }, goal), Exhaustive, stopAtCurrentOpportunity: true);
        Check(immediate.DeliveryTimes[0] == 0 && immediate.Stages[0].Kind == MovementStageKind.Cast,
            "continuity never delays an immediately available attack to finish a walk");
        map.PixelMaxG[4] = -1;
        var unsafeGoal = JointMovementSearch.Search(Input(action, goal), Exhaustive, stopAtCurrentOpportunity: true);
        Check(unsafeGoal.UsefulDestination != goal && unsafeGoal.HasVerifiedRoute,
            "a newly blocked committed destination cannot retain movement");
        map.PixelMaxG[4] = float.MaxValue;
        var lostRecipient = JointMovementSearch.Search(Input(action with { RecipientsByCell = [1, 1, 1, 1, 0], ValueByCell = [1, 1, 1, 1, 0] }, goal), Exhaustive, stopAtCurrentOpportunity: true);
        Check(lostRecipient.DeliveryTimes[0] == 1.5f && lostRecipient.ExecutionValues[0] == 1,
            "continuity cannot lose a required action recipient");
    }
}
