using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void UtilityPlanning()
    {
        var map = Grid(3, 3);
        var origin = Context(map, 7);
        var target = Point(map, 4);
        var opportunity = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, target, 3, 0,
            [new(10, target, 0, 1, true)], positional: Positional.Rear);
        MovementPlanningContext Input(float earliest = 0, ulong charges = 1, float duration = 10, float active = 0, bool available = true)
            => new(origin.Identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9],
                [new(1, 10, earliest, 10, 0, 0, 0, [], [], Opportunity: opportunity)], [], charges,
                utilities: available ? [new(7546, PlanningUtilityEffect.IgnorePositional, duration, 0.6f, 0, 0)] : [],
                positionalBypassUntil: active);
        var input = Input();
        var plan = JointMovementSearch.Search(input, Exhaustive);
        Check(plan.Stages is [{ Kind: MovementStageKind.UtilityAction }, { Kind: MovementStageKind.Cast }]
            && plan.RemainingResources == 0 && plan.UsefulDestination == input.Origin,
            "True North can preserve the immediate positional action without committing a slower positional walk first.");
        Check(MovementPlanValidation.Validate(plan, input, 0, true, input.Identity.CapturedTimestamp).Allowed,
            "The same complete route validator verifies the utility resource, lock, effect and subsequent action.");
        var firstOpportunity = JointMovementSearch.Search(input, Exhaustive, includeMobilityActions: false, stopAtCurrentOpportunity: true);
        Check(firstOpportunity.DeliveredActions == 1 && firstOpportunity.RemainingResources == 0
            && firstOpportunity.Stages.Any(s => s.Kind == MovementStageKind.UtilityAction),
            "The first-opportunity pass compares True North with walking before expensive directional mobility enumeration.");
        var continued = JointMovementSearch.Search(input, Exhaustive with { Expansions = 0 }, nativeIncumbent: firstOpportunity);
        Check(continued.DeliveredActions == 1 && continued.RemainingResources == 0
            && continued.EndPositionalBypassUntil == firstOpportunity.EndPositionalBypassUntil,
            "Continuing a useful incumbent preserves its spent charge and original positional-effect expiry.");
        var rechargeInput = new MovementPlanningContext(input.Identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9],
            [new(1, 10, 0, 10, 0, 0, 0, [], [], Opportunity: opportunity)], [], 1,
            utilities: input.Utilities, recharges: [new(0, 1, 45, float.MaxValue)]);
        var rechargeSeed = JointMovementSearch.Search(rechargeInput, Exhaustive, includeMobilityActions: false, stopAtCurrentOpportunity: true);
        var rechargeContinuation = JointMovementSearch.Search(rechargeInput, Exhaustive with { Expansions = 0 }, nativeIncumbent: rechargeSeed);
        Check(rechargeContinuation.RemainingResources == 0 && rechargeContinuation.DeliveredActions == 1
            && rechargeContinuation.EndInventory!.Next.SequenceEqual(rechargeSeed.EndInventory!.Next),
            "Shared recharge clocks survive a utility-bearing seed without restoring a consumed charge.");
        Check(!MovementPlanValidation.Validate(plan, Input(charges: 0), 0, true, input.Identity.CapturedTimestamp).Allowed,
            "A charge lost before execution invalidates the utility sequence.");
        var walking = JointMovementSearch.Search(Input(earliest: 3), Exhaustive);
        Check(walking.DeliveredActions == 1 && walking.RemainingResources == 1
            && walking.Stages.All(s => s.Kind != MovementStageKind.UtilityAction),
            "Walking before the evidenced GCD window preserves an otherwise unnecessary charge.");
        var expired = JointMovementSearch.Search(Input(earliest: 3, charges: 0, active: 1, available: false), Exhaustive);
        Check(expired.DeliveredActions == 1 && expired.UsefulDestination != input.Origin,
            "An already active buff cannot satisfy a positional after its observed expiry.");
        var shortEffect = JointMovementSearch.Search(Input(duration: 0.2f), Exhaustive);
        Check(shortEffect.DeliveredActions == 1 && shortEffect.RemainingResources == 1,
            "An effect expiring during its own lock does not justify spending the resource.");
        var current = Input(charges: 0, active: 8, available: false);
        Check(MovementPlanValidation.Validate(plan, current, 1, true, input.Identity.CapturedTimestamp).Allowed,
            "After the utility was observed, remaining execution uses the actual current status rather than requiring another charge.");
        var rules = new PlanningExecutionRules(float.MaxValue, 0, []);
        var prohibited = new MovementPlanningContext(input.Identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9],
            [new(1, 10, 0, 10, 0, 0, 0, [], [], Opportunity: opportunity)], [], 1,
            executionRules: rules, utilities: input.Utilities);
        Check(JointMovementSearch.Search(prohibited, Exhaustive).Stages.All(s => s.Kind != MovementStageKind.UtilityAction),
            "A utility action cannot bypass an authored action prohibition.");
        var required = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, target, 3, 0,
            [new(10, target, 0, 1, true), new(11, new(100, 100), 0, 1, true)], positional: Positional.Rear);
        Check(!required.Evaluate(input.Origin, true).Allowed, "Positional immunity never suppresses an independent recipient requirement.");
    }
}
