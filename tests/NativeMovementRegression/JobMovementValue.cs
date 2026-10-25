using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void JobMovementValue()
    {
        Check(PlanningMovementValue.Capture(Class.BLM, 0, []).CastBound
            && PlanningMovementValue.Capture(Class.SGE, 0, []).CastBound
            && !PlanningMovementValue.Capture(Class.MCH, 0, []).CastBound
            && !PlanningMovementValue.Capture(Class.SMN, 0, []).CastBound,
            "Caster/healer stationary time differs from mobile ranged and instant-focused Summoner movement.");
        var map = Grid(7, 1);
        var origin = Context(map, 0);
        var destinations = Enumerable.Repeat(true, 7).ToArray();
        MovementPlanningContext Input(Class job, float free = 0, float dashLock = .6f,
            bool sprint = false, PlanningAction[]? actions = null)
            => new(origin.Identity, map, 0, 0, destinations, new float[7], actions ?? [],
                sprint ? [] : [new(0, 6, MovementStageKind.TargetDash, 155, 9, null, 0, .1f, dashLock - .1f, 0)], 1,
                requiredDestination: Point(map, 6),
                utilities: sprint ? [new(3, PlanningUtilityEffect.MovementSpeed, 10, .6f, 0, 0, 2)] : [],
                movementValue: PlanningMovementValue.Capture(job, free, (actions ?? []).ToImmutableArray()));
        foreach (var job in new[] { Class.BLM, Class.PCT, Class.RDM, Class.WHM, Class.SGE })
        {
            var input = Input(job);
            var plan = JointMovementSearch.Search(input, Exhaustive);
            Check(plan.Stages.Any(s => s.Kind == MovementStageKind.TargetDash) && plan.MovementLossSeconds == .6f,
                $"{job} spends legal mobility to avoid a six-second compulsory walking route.");
            Check(MovementPlanValidation.Validate(plan, input, 0, true, input.Identity.CapturedTimestamp).Allowed,
                $"{job}'s lower-cost mobility route still passes complete execution validation.");
        }
        var mobile = JointMovementSearch.Search(Input(Class.MCH), Exhaustive);
        Check(mobile.RemainingResources == 1 && mobile.Stages.All(s => s.Kind == MovementStageKind.Walk),
            "A mobile ranged job preserves a charge when walking loses no evidenced action.");
        var freePlan = JointMovementSearch.Search(Input(Class.RDM, free: 8), Exhaustive);
        Check(freePlan.RemainingResources == 1 && freePlan.MovementLossSeconds == 0,
            "Walking inside the observed free GCD window preserves mobility even for Red Mage.");
        var expensive = JointMovementSearch.Search(Input(Class.BLM, dashLock: 7), Exhaustive);
        Check(expensive.RemainingResources == 1 && expensive.MovementLossSeconds == 6,
            "A dash whose setup/travel/lock costs more stationary time than walking is conserved.");
        var sprintInput = Input(Class.SCH, sprint: true);
        var sprintPlan = JointMovementSearch.Search(sprintInput, Exhaustive);
        Check(sprintPlan.Stages.Any(s => s.ActionId == 3) && sprintPlan.MovementLossSeconds == 3,
            "Sprint reduces caster walking loss while its overlapping animation lock is counted once.");
        var shortMap = Grid(2, 1);
        var shortOrigin = Context(shortMap, 0, speed: 6);
        var shortInput = new MovementPlanningContext(shortOrigin.Identity, shortMap, 0, 0, [true, true], new float[2], [], [], 1,
            requiredDestination: Point(shortMap, 1), utilities: [new(3, PlanningUtilityEffect.MovementSpeed, 10, .6f, 0, 0, 7.8f)],
            movementValue: PlanningMovementValue.Capture(Class.SGE, 0, []));
        Check(JointMovementSearch.Search(shortInput, Exhaustive).RemainingResources == 1,
            "A very short caster walk beats Sprint's residual action lock.");
        var cast = Action(map, 12, 0, new ulong[7], Enumerable.Repeat(1f, 7).ToArray()) with { CastSeconds = 1 };
        Check(PlanningMovementValue.Capture(Class.SMN, 0, [cast]).CastBound,
            "An observed hardcast also values stationary time on an otherwise mobile job.");
        var current = JointMovementSearch.Search(Input(Class.BLM, actions: [cast]), Exhaustive);
        Check(current.DeliveredActions == 1 && current.Stages[0].Kind == MovementStageKind.Cast,
            "Reducing walking never purchases delayed delivery of the selected cast.");

        // A native goal requires movement while current instant delivery ties.
        // This previously allowed the resource proof to discard every useful dash.
        map.PixelPriority[6] = 1;
        var immediate = cast with { CastSeconds = 0 };
        var mechanics = new MovementPlanningContext(origin.Identity, map, 0, 0, destinations, new float[7], [immediate], [], 1,
            mobilityMechanics: [new(7, MobilityMechanic.Ground, 20, 0, 0, 0, .1f, 0)],
            movementValue: PlanningMovementValue.Capture(Class.BLM, 0, [immediate]));
        var pruned = JointMovementSearch.Search(mechanics, Exhaustive);
        var reference = JointMovementSearch.Search(mechanics, Exhaustive, pruneResourceInferiorMobility: false,
            pruneExecutionInferiorStates: false, partitionFrontier: false, proveCompleteOptimum: false);
        Check(pruned.RemainingResources == 0 && pruned.MovementLossSeconds < 1
            && pruned.MovementLossSeconds == reference.MovementLossSeconds && pruned.NativePriority == reference.NativePriority
            && pruned.DeliveryTimes.SequenceEqual(reference.DeliveryTimes),
            "Caster movement cost survives resource pruning, frontier dominance and optimum proofs.");
        var shifted = new MovementPlanningContext(origin.Identity with { WorldTime = origin.Identity.WorldTime.AddSeconds(1) }, map,
            0, 0, destinations, new float[7], [], [], movementValue: PlanningMovementValue.Capture(Class.BLM, 2, []));
        var initial = new MovementPlanningContext(origin.Identity, map, 0, 0, destinations, new float[7], [], [],
            movementValue: PlanningMovementValue.Capture(Class.BLM, 3, []));
        Check(shifted.SameProblem(initial) && !Input(Class.MCH).SameProblem(Input(Class.BLM)),
            "Observed GCD countdowns remain equivalent while job movement-profile changes invalidate planning.");
        var gcdInput = Input(Class.RDM, actions: [immediate with { IsGCD = true }]);
        var window = gcdInput.MovementValue.Accumulate(gcdInput,
            [new(MovementStageKind.Cast, gcdInput.Origin, gcdInput.Origin, 0, 0, immediate.Id, immediate.TargetId,
                SourceActionIndex: 0, PlannedRecastSeconds: 2.5f),
             new(MovementStageKind.Walk, gcdInput.Origin, Point(map, 1), 0, 1),
             new(MovementStageKind.Walk, Point(map, 1), Point(map, 4), 1, 4)], 0, 0, 0);
        Check(window.Loss == 1.5f && PlanningMovementValue.Compare(1, 1.0001f) == 0,
            "Delivered instant GCDs grant only their actual recast window, without float-noise cooldown spending.");
    }
}
