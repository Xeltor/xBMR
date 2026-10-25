using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void FrontierMath()
    {
        var random = new Random(2913);
        for (var scene = 0; scene < 96; ++scene)
        {
            var map = Grid(3, 3);
            for (var cell = 1; cell < 9; ++cell)
            {
                map.PixelMaxG[cell] = random.Next(6) switch { 0 => -1, 1 => 1.5f, _ => float.MaxValue };
                map.PixelPriority[cell] = random.Next(3);
            }
            var first = Action(map, 4, 0, new ulong[9], Enumerable.Range(0, 9).Select(_ => (float)random.Next(3)).ToArray(), earliest: .5f);
            var next = first with { Id = 2, RequiredEarlierActions = 1, Earliest = 1.5f, ResourceCost = scene % 3 == 0 ? 1ul : 0 };
            var seed = Context(map, 0);
            var input = new MovementPlanningContext(seed.Identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(),
                new float[9], [first, next], [new(0, 8, MovementStageKind.GroundMovement, 7, 0, null, 0, .2f, .1f, 0)], 1,
                recharges: scene % 2 == 0 ? [new(0, 1, 2, float.MaxValue)] : []);
            var reference = JointMovementSearch.Search(input, Exhaustive, partitionFrontier: false);
            var optimized = JointMovementSearch.Search(input, Exhaustive);
            Check(reference.Status == optimized.Status && reference.Bound == optimized.Bound
                && reference.ExpandedStates == optimized.ExpandedStates && reference.UsefulDestination == optimized.UsefulDestination
                && reference.Stages.SequenceEqual(optimized.Stages) && reference.ExecutionValues.SequenceEqual(optimized.ExecutionValues)
                && reference.DeliveryTimes.SequenceEqual(optimized.DeliveryTimes) && reference.RemainingResources == optimized.RemainingResources
                && reference.RecoveryTimes.SequenceEqual(optimized.RecoveryTimes),
                "Partitioned frontiers preserve identical admissions, expansions, exact route, actions and resources with warnings and charge recovery.");
        }
        foreach (var size in new[] { 8, 16 })
        {
            var map = Grid(size, size); var count = size * size;
            map.PixelPriority[count - 1] = 1;
            var seed = Context(map, 0, speed: 6);
            var first = Action(map, 6, 0, new ulong[count], Enumerable.Repeat(1f, count).ToArray(), earliest: 1);
            var input = new MovementPlanningContext(seed.Identity, map, 0, 0, Enumerable.Repeat(true, count).ToArray(), new float[count],
                [first, first with { Id = 2, Earliest = 2, RequiredEarlierActions = 1 }], [], 1,
                utilities: [new(3, PlanningUtilityEffect.MovementSpeed, 10, .6f, .4f, 0, 7.8f)], recharges: [new(0, 1, 4, float.MaxValue)]);
            var referenceTimes = new List<double>(); var optimizedTimes = new List<double>();
            var referenceBytes = new List<long>(); var optimizedBytes = new List<long>();
            MovementPlan? reference = null; MovementPlan? optimized = null;
            for (var repeat = 0; repeat < 6; ++repeat)
            {
                MovementPlan Measure(bool partition)
                {
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    // Fixed admissions keep the larger stress comparison deterministic
                    // instead of letting wall-clock freshness choose different work.
                    var plan = JointMovementSearch.Search(input, new(CreatedStates: 12000, RespectFreshness: false), partitionFrontier: partition);
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    if (repeat > 0)
                    {
                        (partition ? optimizedTimes : referenceTimes).Add(plan.SearchMilliseconds);
                        (partition ? optimizedBytes : referenceBytes).Add(allocated);
                    }
                    return plan;
                }
                if (repeat % 2 == 0) { reference = Measure(false); optimized = Measure(true); }
                else { optimized = Measure(true); reference = Measure(false); }
                Check(reference.Stages.SequenceEqual(optimized.Stages) && reference.ExpandedStates == optimized.ExpandedStates
                    && reference.Status == optimized.Status, "Frontier benchmark retains the same exact path and search work.");
            }
            referenceTimes.Sort(); optimizedTimes.Sort();
            referenceBytes.Sort(); optimizedBytes.Sort();
            Console.WriteLine($"Frontier math {count} cells: unpartitioned={referenceTimes[2]:F3}ms/{referenceBytes[2]}B partitioned={optimizedTimes[2]:F3}ms/{optimizedBytes[2]}B, expansions={optimized!.ExpandedStates}, {optimized.Status}/{optimized.Bound}; synthetic only.");
        }
    }
}
