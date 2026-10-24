using BossMod.CombatAssistance.Planning;
using System.Diagnostics;

public static partial class Checks
{
    private static void ResourcePruning()
    {
        for (var mask = 0; mask < 8; ++mask)
            for (var preferred = 0; preferred < 6; ++preferred)
            {
                var map = Grid(3, 2);
                for (var cell = 1; cell <= 3; ++cell)
                    if ((mask & (1 << (cell - 1))) != 0) map.PixelMaxG[cell] = 0;
                map.PixelPriority[preferred] = 1;
                var input = Context(map, 0);
                var action = Action(map, 12, 0, new ulong[6], Enumerable.Repeat(1f, 6).ToArray());
                var context = new MovementPlanningContext(input.Identity, map, 0, 0,
                    Enumerable.Repeat(true, 6).ToArray(), new float[6], [action], [], 1,
                    mobilityMechanics: [new(7, MobilityMechanic.Ground, 20, 0, 0, 0, 0.1f, 0)]);
                var reference = JointMovementSearch.Search(context, Exhaustive, pruneResourceInferiorMobility: false,
                    pruneExecutionInferiorStates: false);
                var pruned = JointMovementSearch.Search(context, Exhaustive);
                Check(reference.Status == MovementSearchStatus.Complete && pruned.Status == MovementSearchStatus.Complete,
                    "Both resource-pruning reference searches exhaust their supported alternatives.");
                Check(pruned.NativePriority == reference.NativePriority && pruned.RemainingResources == reference.RemainingResources
                    && pruned.DeliveredActions == reference.DeliveredActions && pruned.DeliveryTimes.SequenceEqual(reference.DeliveryTimes)
                    && pruned.ExecutionValues.SequenceEqual(reference.ExecutionValues),
                    "Proved resource pruning preserves execution, native priority and inventory across obstacle/goal alternatives.");
            }

        var line = Grid(5, 1);
        var identity = Context(line, 0).Identity;
        var available = Enumerable.Repeat(true, 5).ToArray();
        var immediate = Action(line, 12, 0, new ulong[5], [1, 1, 1, 1, 1]);
        MovementPlanningContext Capture(PlanningAction[] actions, PlanningRecharge[]? clocks = null, PlanningZone[]? zones = null)
            => new(identity, line, 0, 0, available, new float[5], actions, [], 1,
                mobilityMechanics: [new(7, MobilityMechanic.Ground, 20, 0, 0, 0, 0.1f, 0)], recharges: clocks, zones: zones);
        var conserved = JointMovementSearch.Search(Capture([immediate]), Exhaustive);
        Check(conserved.ResourcePrunedActions > 0 && conserved.RemainingResources == 1,
            "A perfect instantaneous opportunity avoids enumerating mobility that irreversibly spends a charge.");
        var urgent = immediate with { Latest = 0.2f, ValueByCell = [0, 0, 0, 0, 1] };
        var rescued = JointMovementSearch.Search(Capture([urgent]), Exhaustive);
        Check(rescued.DeliveredActions == 1 && rescued.Stages.Any(s => s.Kind == MovementStageKind.GroundMovement),
            "Mobility remains available when it can rescue the current action.");
        var next = urgent with { Id = 2, RequiredEarlierActions = 1 };
        var future = JointMovementSearch.Search(Capture([immediate, next]), Exhaustive);
        Check(future.DeliveredActions == 3 && future.Stages.Any(s => s.Kind == MovementStageKind.GroundMovement),
            "A later evidenced action prevents applying the single-opportunity pruning proof.");
        PlanningZone zone = new(8, 9, "Brief effect", Point(line, 4), 0, 0.1f, 0.5f, PlanningZoneBenefit.PersonalCasting);
        var regenerated = JointMovementSearch.Search(Capture([immediate], [new(0, 1, 0.2f, float.MaxValue)], [zone]), Exhaustive);
        Check(regenerated.UsefulZones.Length == 1 && regenerated.RemainingResources == 1,
            "A charge that returns inside the horizon can still reach an expiring useful effect.");

        var roundingMap = Grid(5, 1);
        var roundingIdentity = Context(roundingMap, 0).Identity;
        // Use map coordinates, independent of its world-space orientation.
        var near = Point(roundingMap, 1);
        var far = Point(roundingMap, 4) + (Point(roundingMap, 4) - Point(roundingMap, 1));
        var geometry = new ExecutionOpportunity(ExecutionShape.CasterCircle, 7, default, 0, 3,
            [new(11, near, 0, 16777216), new(12, near, 0, 3), new(13, near, 0, 3), new(14, far, 0, 3)]);
        var roundingInput = new MovementPlanningContext(roundingIdentity, roundingMap, 0, 0,
            Enumerable.Repeat(true, 5).ToArray(), new float[5],
            [new(1, 7, 2, 6, 0, 0, 0, [], [], Opportunity: geometry)], [], 1,
            mobilityMechanics: [new(7, MobilityMechanic.Ground, 20, 0, 0, 0, 0.1f, 0)]);
        var roundingReference = JointMovementSearch.Search(roundingInput, Exhaustive,
            pruneResourceInferiorMobility: false, pruneExecutionInferiorStates: false);
        var roundingPlan = JointMovementSearch.Search(roundingInput, Exhaustive);
        Check(roundingPlan.ExecutionValues.SequenceEqual(roundingReference.ExecutionValues)
            && roundingPlan.ExecutionValues[0] == geometry.Evaluate(Point(roundingMap, 4)).Value
            && roundingPlan.RemainingResources == 0,
            "Execution ceilings retain a useful resource spend even when ordered float coverage exceeds a wider-precision sum.");

        var broad = Grid(80, 80);
        var broadIdentity = Context(broad, 0, speed: 6).Identity;
        var broadInput = new MovementPlanningContext(broadIdentity, broad, 0, 0,
            Enumerable.Repeat(true, 6400).ToArray(), new float[6400],
            [Action(broad, 12, 0, new ulong[6400], Enumerable.Repeat(1f, 6400).ToArray())], [], 1,
            mobilityMechanics: [new(7, MobilityMechanic.Ground, 20, 0, 0, 0, 0.1f, 0)]);
        var clock = Stopwatch.StartNew();
        var optimized = JointMovementSearch.Search(broadInput, Exhaustive);
        Check(optimized.Status == MovementSearchStatus.Complete && optimized.DeliveredActions == 1
            && optimized.ResourcePrunedActions > 0 && optimized.RemainingResources == 1,
            "The mobility-rich full-map workload exhausts relevant alternatives without sacrificing its current action.");
        Console.WriteLine($"Synthetic mobility-rich 6400-cell resource proof: {clock.Elapsed.TotalMilliseconds:F3}ms, expansions={optimized.ExpandedStates}, pruned action enumerations={optimized.ResourcePrunedActions}; not live frame-time acceptance.");
    }
}
