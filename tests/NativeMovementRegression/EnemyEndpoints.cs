using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void EnemyEndpoints()
    {
        var map = Grid(11, 3);
        var baseline = Context(map, 11, speed: 6);
        var center = Point(map, 16);
        MovementPlanningContext Input(int origin = 11, PlanningAction[]? actions = null, bool[]? allowed = null,
            bool avoidance = true, float radius = 2)
            => new(baseline.Identity with { Origin = Point(map, origin) }, map, 0, 0,
                allowed ?? Enumerable.Repeat(true, 33).ToArray(), new float[33], actions ?? [], [],
                range: new(10, center, 3, RecoverExecution: true),
                enemyFootprints: avoidance ? [new(10, center, radius)] : [],
                movementValue: PlanningMovementValue.Capture(Class.SGE, 0, []));
        var input = Input();
        var approach = JointMovementSearch.Search(input, Exhaustive, stopAtCurrentOpportunity: true);
        Check(input.PreferredEnemyEndpoint(approach.UsefulDestination) && input.Range!.Loss(approach.UsefulDestination) == 0
            && approach.UsefulDestination.X < center.X,
            "Combat approach prefers usable outside space before committing an under-boss endpoint.");
        var attack = Action(map, 4, 0, new ulong[33],
            Enumerable.Range(0, 33).Select(i => i % 11 is >= 4 and <= 8 ? 1f : 0).ToArray());
        input = Input(actions: [attack]);
        var outside = JointMovementSearch.Search(input, Exhaustive, stopAtCurrentOpportunity: true);
        Check(input.PreferredEnemyEndpoint(outside.UsefulDestination) && outside.DeliveredActions == 1
            && outside.Stages.Any(s => s.Kind == MovementStageKind.Walk && s.To.InCircle(center, 2))
            && MovementPlanValidation.Validate(outside, input, 0, true, input.Identity.CapturedTimestamp).Allowed,
            $"A usable outside endpoint wins over stopping inside; crossing remains legal. Actual: {outside.UsefulDestination}, actions={outside.DeliveredActions}, {outside.Evidence}");
        var reference = JointMovementSearch.Search(input, Exhaustive, proveCompleteOptimum: false,
            pruneExecutionInferiorStates: false, pruneResourceInferiorMobility: false, partitionFrontier: false);
        Check(reference.UsefulDestination == outside.UsefulDestination
            && reference.DeliveryTimes.SequenceEqual(outside.DeliveryTimes),
            "Endpoint preference survives first-pass stopping and execution/resource pruning.");
        var through = Input(actions: [attack], allowed: Enumerable.Range(0, 33).Select(i => i % 11 == 8).ToArray());
        var crossed = JointMovementSearch.Search(through, Exhaustive);
        Check(crossed.UsefulDestination.X > center.X && crossed.DeliveredActions == 1
            && MovementPlanValidation.Validate(crossed, through, 0, true, through.Identity.CapturedTimestamp).Allowed,
            "A necessary far-side arrival can step through the boss without treating its footprint as a wall.");
        var inner = Enumerable.Range(0, 33).Select(i => i % 11 is >= 4 and <= 6).ToArray();
        input = Input(actions: [attack], allowed: inner);
        var pocket = JointMovementSearch.Search(input, Exhaustive);
        Check(pocket.HasVerifiedRoute && !input.PreferredEnemyEndpoint(pocket.UsefulDestination)
            && pocket.DeliveredActions == 1,
            "An independently assigned inside pocket remains available when no outside endpoint can satisfy the assignment.");
        input = Input(origin: 16);
        var covered = JointMovementSearch.Search(input, Exhaustive);
        Check(covered.UsefulDestination == input.Origin && covered.Stages.IsEmpty,
            "A boss moving over the current pose does not itself invent a reason to walk out.");
        var healthy = Input(origin: 13);
        var held = JointMovementSearch.Search(healthy, Exhaustive);
        Check(held.UsefulDestination == healthy.Origin && held.Stages.IsEmpty,
            "A healthy healer already in offensive range holds position without a preparatory party-centering objective.");
        var huge = Input(actions: [attack], radius: 50);
        var fallback = JointMovementSearch.Search(huge, Exhaustive);
        Check(fallback.HasVerifiedRoute && fallback.DeliveredActions == 1,
            "A boss covering the whole playable map cannot turn its footprint preference into a forbidden arena.");
        var disabled = Input(actions: [attack], avoidance: false);
        var original = JointMovementSearch.Search(disabled, Exhaustive);
        Check(original.DurationSeconds < outside.DurationSeconds,
            "The saved enemy-avoidance opt-out retains the original execution-first endpoint selection.");
    }
}
