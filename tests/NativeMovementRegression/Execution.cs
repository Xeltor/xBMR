using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;

public static partial class Checks
{
    private static void Execution()
    {
        var map = Grid(3, 1);
        map.PixelPriority[2] = 1;
        var context = Context(map, 0);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        var executor = new MovementPlanExecutor();
        var movement = executor.Advance(plan, context, default, true, context.Identity.CapturedTimestamp);
        Check(movement.Kind == MovementExecutionKind.Walk && movement.Waypoint == Point(map, 1), "one executor starts with the first verified waypoint");
        Check(executor.CommittedDestination == Point(map, 2), "useful destination is retained separately from the immediate waypoint");
        var changedTarget = new MovementPlanningContext(context.Identity, map, 0, 0, [true, true, true], [0, 0, 0], [], [], targetId: 123);
        Check(!MovementPlanValidation.Validate(plan, changedTarget, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "a changed target invalidates movement even when no external rotation action was captured");
        var atEnd = new MovementPlanningContext(context.Identity with { Origin = Point(map, 2) }, map, 0, 0,
            [true, true, true], [0, 0, 0], [], []);
        // Advancing to a later waypoint without passing the first is not accepted as
        // completion; the exact remaining route must still be processed.
        var atFirst = new MovementPlanningContext(context.Identity with { Origin = Point(map, 1) }, map, 0, 0,
            [true, true, true], [0, 0, 0], [], []);
        Check(executor.Advance(plan, atFirst, default, true, context.Identity.CapturedTimestamp).Waypoint == Point(map, 2),
            "walking advances through the complete route");
        Check(executor.Advance(plan, atEnd, default, true, context.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Arrived
            && executor.Advance(plan, atEnd, default, true, context.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Arrived,
            "equivalent arrived frames do not request another correction move");
        Check(executor.Advance(plan, atEnd, default, false, context.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated
            && executor.CommittedDestination == null, "manual handoff cancels execution immediately");
        Check(executor.Advance(plan, context, default, true, context.Identity.CapturedTimestamp + Stopwatch.Frequency).Kind == MovementExecutionKind.Invalidated,
            "stale worker completion cannot start walking or apply an action");
        var whileWalking = new MovementPlanningContext(context.Identity with { Origin = Point(map, 1) + new WDir(0.2f, 0) }, map, 0, 0,
            [true, true, true], [0, 0, 0], [], []);
        Check(executor.Advance(plan with { }, whileWalking, default, true, context.Identity.CapturedTimestamp).Waypoint == Point(map, 2),
            "a completed worker joins the occupied route segment without walking back to a passed waypoint");
        var waitBeforeWalk = plan with { Stages = [new(MovementStageKind.Wait, context.Origin, context.Origin, 0, 1), .. plan.Stages] };
        Check(executor.Advance(waitBeforeWalk, whileWalking, default, true, context.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "resuming a walking prefix cannot skip an unexecuted wait or action");

        var mobility = new PlanningMobility(0, 2, MovementStageKind.DirectionalMovement, 10, 0, 180f.Degrees(), 0.05f, 0.1f, 0, 0);
        var jump = Context(map, 0, mobility: [mobility], resources: 1);
        var jumpPlan = JointMovementSearch.Search(jump, Exhaustive);
        // Resource preservation selects walking unless it loses an evidenced window.
        var deadline = Action(map, 0.5f, 1, [0, 0, 1], [0, 0, 1]);
        jump = Context(map, 0, actions: [deadline], mobility: [mobility], resources: 1);
        jumpPlan = JointMovementSearch.Search(jump, Exhaustive);
        var beforeFacing = executor.Advance(jumpPlan, jump, new(default), true, jump.Identity.CapturedTimestamp);
        Check(beforeFacing.Kind == MovementExecutionKind.Face && beforeFacing.Facing == 180f.Degrees(),
            "directional displacement requests its deliberate facing before action submission");
        var now = jump.Identity.CapturedTimestamp + (long)(Stopwatch.Frequency * 0.06);
        Check(executor.Advance(jumpPlan, jump, new(180f.Degrees()), true, now).Kind == MovementExecutionKind.Action,
            "mobility is submitted only after setup and exact facing are satisfied");
        var changed = Context(map, 0, actions: [deadline], mobility: [mobility with { AnchorId = 99 }], resources: 1);
        Check(executor.Advance(jumpPlan, changed, new(180f.Degrees()), true, now).Kind == MovementExecutionKind.Invalidated,
            "changed executable anchor invalidates a prepared mobility action");

        var one = Grid(1, 1);
        var cast = Context(one, 0, actions: [Action(one, 2, 1, [1], [1], cast: 0.05f)]);
        var castPlan = JointMovementSearch.Search(cast, Exhaustive);
        Check(executor.Advance(castPlan, cast, default, true, cast.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Wait,
            "a planned rotation action waits for delivery rather than issuing a duplicate GCD");
        var completed = new MovementExecutionObservation(default, 1, 10, cast.Identity.WorldTime.AddSeconds(0.05));
        Check(executor.Advance(castPlan, cast, completed, true, cast.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Wait,
            "an action timestamp beyond the current observation cannot prove delivery");
        var afterCast = new MovementPlanningContext(cast.Identity with { WorldTime = completed.ActionTime }, one, 0, 0,
            [true], [0], [], []);
        Check(executor.Advance(castPlan, afterCast, completed, true, cast.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Arrived,
            "the selected rotation action advances only after its target-specific action effect is observed");

        var rangeMap = Grid(3, 1);
        var rangeInput = Context(rangeMap, 0);
        var ranged = new MovementPlanningContext(rangeInput.Identity, rangeMap, 0, 0.1f,
            [true, true, true], [0, 0, 0], [], [], range: new(10, Point(rangeMap, 2), 0.1f));
        var rangePlan = JointMovementSearch.Search(ranged, Exhaustive);
        Check(rangePlan.UsefulDestination == Point(rangeMap, 2) && rangePlan.Stages[0].Kind == MovementStageKind.Wait,
            "owner range and departure delay are evaluated before selecting the first destination");
        rangeMap.PixelMaxG[2] = 1;
        ranged = new(rangeInput.Identity, rangeMap, 0, 0, [true, true, true], [0, 0, 0], [], [],
            range: new(10, Point(rangeMap, 2), 0.1f));
        Check(JointMovementSearch.Search(ranged, Exhaustive).UsefulDestination != Point(rangeMap, 2),
            "range instructions cannot turn a temporary warning position into a final safe destination");
        rangeMap.PixelMaxG[0] = 0.05f;
        ranged = new(rangeInput.Identity, rangeMap, 0, 0.1f, [true, true, true], [0, 0, 0], [], []);
        Check(!JointMovementSearch.Search(ranged, Exhaustive).HasVerifiedRoute,
            "a cosmetic departure delay yields to native recovery when its wait misses a safety deadline");
    }
}
