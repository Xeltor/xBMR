using BossMod;
using BossMod.AI;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void CombatFollowRangeTests()
    {
        var settings = new AIConfig { MaxDistanceToTarget = 3 };
        Check(settings.UseCombatPositioningRange && settings.MaxDistanceToTarget == 3,
            "Automatic combat range is enabled without overwriting a saved fixed follow distance.");
        Check(CombatFollowRange.Resolve(Role.Melee, 3) == 2.6f
            && CombatFollowRange.Resolve(Role.Tank, 3) == 2.6f
            && CombatFollowRange.Resolve(Role.Ranged, 3) == 25
            && CombatFollowRange.Resolve(Role.Healer, 3) == 25
            && CombatFollowRange.Resolve(Role.None, 3) == 3,
            "Combat following keeps melee reach for melee roles and ranged reach for ranged/healing roles.");
        var map = Grid(25, 1);
        var target = Point(map, 24);
        var origin = 10;
        void Follow(float range)
        {
            var goal = AIHints.GoalSingleTarget(target, range);
            for (var cell = 0; cell < map.PixelPriority.Length; ++cell)
                map.PixelPriority[cell] = goal(Point(map, cell));
        }
        Follow(settings.MaxDistanceToTarget);
        var fixedPlan = JointMovementSearch.Search(Context(map, origin), Exhaustive);
        Check((fixedPlan.UsefulDestination - target).Length() <= 3,
            "The fixed-distance follow mode still deliberately approaches melee distance.");
        Follow(CombatFollowRange.Resolve(Role.Ranged, settings.MaxDistanceToTarget));
        var ranged = JointMovementSearch.Search(Context(map, origin), Exhaustive);
        Check(ranged.UsefulDestination == Point(map, origin) && ranged.Stages.All(s => s.From == s.To),
            "A ranged job already in attack reach is not pulled into melee by the generic follow goal.");
        var nearby = JointMovementSearch.Search(Context(map, 23), Exhaustive);
        Check(nearby.UsefulDestination == Point(map, 23),
            "Automatic combat range does not create a maximum-range retreat when a nearby position is equivalent.");
        var aoe = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, target, 8, 0,
            [new(10, target, 0, 1, true)]);
        var shortAction = new PlanningAction(16499, 10, 0, 12, 0, .6f, 0, [], [], Opportunity: aoe);
        var shortPlan = JointMovementSearch.Search(Context(map, origin, [shortAction]), Exhaustive);
        Check(shortPlan.DeliveredActions == 1 && (shortPlan.UsefulDestination - target).Length() <= 8,
            "A real short-range attack still requires approaching its legal range despite the wider generic follow goal.");
        map.PixelMaxG[16] = -1;
        var blocked = JointMovementSearch.Search(Context(map, origin, [shortAction]), Exhaustive);
        Check(blocked.DeliveredActions == 0 && blocked.UsefulDestination.X < Point(map, 16).X,
            "Automatic ranged following cannot cross a physical obstruction to deliver a short-range attack.");
    }
}
