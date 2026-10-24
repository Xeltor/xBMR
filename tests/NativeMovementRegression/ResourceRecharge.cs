using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void ResourceRecharge()
    {
        ImmutableArray<PlanningRecharge> clocks = [new(0, 2, 3, 1)];
        var initial = PlanningResourceState.Initial(1, clocks);
        Check(initial.At(0.9f, clocks).Charges == 1 && initial.At(1, clocks).Charges == 2,
            "a charge returns at the original observed deadline");
        var used = initial.Spend(1, 0, clocks);
        Check(used.At(1, clocks).Charges == 1 && used.At(4, clocks).Charges == 2,
            "spending a partial stack does not restart its running cooldown");
        Check(initial.At(1, clocks).Spend(1, 2, clocks).Next[0] == 5,
            "spending from a capped stack starts the next cooldown at execution");
        Check(used.Ready(2, 0, clocks, out var ready) && ready == 4,
            "the readiness calculation includes every missing charge");
        Check(!used.Ready(0x11, 0, clocks, out _), "time alone never generates a missing gauge resource");
        Check(!used.Ready(3, 0, clocks, out _), "a cooldown cannot exceed its actual capacity");
        Check(!used.Dominates(used with { Next = [0.5f] }, 0, clocks),
            "equal charges with a later recharge cannot dominate an earlier schedule");
        Check((used with { Next = [0.5f] }).Dominates(used, 0, clocks),
            "equal charges with an earlier recharge can reproduce the later schedule");

        // Exhaustively compare short spending sequences with a separate queue of
        // charge-return events. Half-second steps are exact binary floats.
        for (var capacity = 1; capacity <= 3; ++capacity)
            for (var count = 0; count <= capacity; ++count)
                for (var sequence = 0; sequence < 256; ++sequence)
                {
                    ImmutableArray<PlanningRecharge> schedule = [new(0, capacity, 1.5f, count == capacity ? float.MaxValue : 0.5f)];
                    var state = PlanningResourceState.Initial((ulong)count, schedule);
                    var expected = count;
                    var returns = new Queue<float>();
                    for (var missing = 0; missing < capacity - count; ++missing) returns.Enqueue(0.5f + missing * 1.5f);
                    for (var step = 0; step < 8; ++step)
                    {
                        var now = step * 0.5f;
                        while (returns.TryPeek(out var due) && due <= now) { returns.Dequeue(); ++expected; }
                        state = state.At(now, schedule);
                        Check(state.Charges == (ulong)expected, "charge timeline agrees with independent return-event reference");
                        if ((sequence & (1 << step)) == 0 || expected == 0) continue;
                        --expected;
                        returns.Enqueue(returns.Count == 0 ? now + 1.5f : returns.Last() + 1.5f);
                        state = state.Spend(1, now, schedule);
                    }
                }

        var map = Grid(5, 1);
        var baseline = Context(map, 0);
        var action = new PlanningAction(10, 20, 0, 2, 0, 0, 0, new ulong[5], [0, 0, 0, 0, 1]);
        var edge = new PlanningMobility(0, 4, MovementStageKind.TargetDash, 11, 20, null, 0, 0.1f, 0, 0);
        var input = new MovementPlanningContext(baseline.Identity, map, 0, 0, Enumerable.Repeat(true, 5).ToArray(), new float[5],
            [action], [edge], 0, recharges: [new(0, 1, 30, 0.5f)]);
        var plan = JointMovementSearch.Search(input, Exhaustive);
        Check(plan.DeliveredActions == 1 && plan.Stages.Any(s => s.Kind == MovementStageKind.TargetDash)
            && plan.Stages.First().Kind == MovementStageKind.Wait,
            "waiting for an observed returning charge preserves an action that walking cannot reach in time");
        Check(MovementPlanValidation.Validate(plan, input, 0, true, input.Identity.CapturedTimestamp).Allowed,
            "exact route validation uses the same recharge timeline as search");
        var depleted = new MovementPlanningContext(baseline.Identity, map, 0, 0, Enumerable.Repeat(true, 5).ToArray(), new float[5],
            [action], [edge], 0, recharges: [new(0, 1, 30, 5)]);
        Check(!MovementPlanValidation.Validate(plan, depleted, 0, true, input.Identity.CapturedTimestamp).Allowed,
            "a changed cooldown invalidates a previously promised action opportunity");
        var first = edge with { DestinationCell = 2, AdditionalResourceCost = 0x10 };
        var second = edge with { SourceCell = 2, AdditionalResourceCost = 0x10 };
        var shared = new MovementPlanningContext(baseline.Identity, map, 0, 0, Enumerable.Repeat(true, 5).ToArray(), new float[5],
            [action with { Latest = 1.5f }], [first, second], 0x12,
            recharges: [new(0, 2, 30, float.MaxValue), new(1, 1, 1, float.MaxValue)]);
        var twice = JointMovementSearch.Search(shared, Exhaustive);
        Check(twice.DeliveredActions == 1 && twice.Stages.Count(s => s.Kind == MovementStageKind.TargetDash) == 2
            && twice.Stages.Any(s => s.Kind == MovementStageKind.Wait) && twice.DeliveryTimes[0] >= 1.1f,
            "multiple charges still respect the shared secondary repeat cooldown");
        Check(MovementPlanValidation.Validate(twice, shared, 0, true, shared.Identity.CapturedTimestamp).Allowed,
            "secondary shared cooldown is validated across both actions");
        var unknown = new MovementPlanningContext(baseline.Identity, map, 0, 0, Enumerable.Repeat(true, 5).ToArray(), new float[5],
            [action with { Latest = 1.5f }], [first, second], 0x12,
            recharges: [new(0, 2, 30, float.MaxValue)], cooldownEvidenceLimited: true);
        var uncertain = JointMovementSearch.Search(unknown, Exhaustive);
        Check(uncertain.DeliveredActions == 0 && uncertain.Bound == MovementSearchBound.CooldownEvidence,
            "unknown repeat cooldown is not fabricated or reported as a completed optimization");
        var seedMap = Grid(5, 1);
        seedMap.PixelPriority[4] = 5;
        var seedInput = new MovementPlanningContext(baseline.Identity, seedMap, 0, 0, Enumerable.Repeat(true, 5).ToArray(), new float[5],
            [], [], 0, recharges: [new(0, 1, 30, 0.5f)]);
        var seed = NativeMovementIncumbent.CaptureRoute(seedInput, [seedInput.Origin, Point(seedMap, 4)]);
        Check(seed != null && JointMovementSearch.Search(seedInput, Exhaustive with { Expansions = 0 }, nativeIncumbent: seed).UsefulDestination == Point(seedMap, 4),
            "charge timelines do not remove the complete native safety incumbent");
        var current = new MovementPlanningContext(baseline.Identity with { WorldTime = baseline.Identity.WorldTime.AddSeconds(0.1) },
            map, 0, 0, Enumerable.Repeat(true, 5).ToArray(), new float[5], [action with { Latest = 1.9f }], [edge], 0,
            recharges: [new(0, 1, 30, 0.4f)]);
        Check(current.SameProblem(input), "ordinary cooldown countdowns preserve an existing worker generation");
    }
}
