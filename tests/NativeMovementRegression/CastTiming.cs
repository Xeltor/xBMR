using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void CastTiming()
    {
        var timing = new PlanningCastTiming(2.5f, 3, 1);
        Check(timing.Seconds(0, default, 0.9f) == 0 && timing.Seconds(0, default, 1) == 2.5f,
            "instant-cast proc is usable only before its observed expiry");
        var region = timing with { ProcUntil = 4, HasteCenter = default(WPos), HasteRadius = 3, HasteUntil = 3 };
        Check(region.Seconds(0, new(10, 0), 2) == 0 && region.Seconds(2, new(10, 0), 2) == 3,
            "leaving a haste field loses its fast hardcast but preserves an unexpired instant proc");
        Check(region.Seconds(2, default, 2.9f) == 2 && region.Seconds(2, default, 3) == 3,
            "ground haste is checked at the actual cast start and cannot survive expiry");
        var map = Grid(3, 1);
        var action = Action(map, 5, 1, [0, 0, 1], [0, 0, 1]) with { Latest = 4, Timing = timing };
        var context = Context(map, 0, [action]);
        var result = JointMovementSearch.Search(context, Exhaustive);
        Check(result.DeliveredActions == 0,
            "walking past proc expiry cannot claim a current attack whose real hardcast misses the window");
        var beforeExpiry = action with { Timing = timing with { ProcUntil = 3 } };
        var fastContext = Context(map, 0, [beforeExpiry]);
        var fastPlan = JointMovementSearch.Search(fastContext, Exhaustive);
        Check(fastPlan.DeliveredActions == 1 && fastPlan.DeliveryTimes[0] == 2,
            "the same route preserves an instant action when the observed proc genuinely lasts long enough");
        Check(!MovementPlanValidation.Validate(fastPlan, context, 0, true, fastContext.Identity.CapturedTimestamp).Allowed,
            "pre-execution revalidation detects lost proc timing independently of search");
        Check((timing with { ProcUntil = 0.5f }).Same(timing, 0.5f) && !(timing with { ProcUntil = 2 }).Same(timing, 0.5f),
            "proc countdown comparison cannot silently refresh an old promise");
    }
}
