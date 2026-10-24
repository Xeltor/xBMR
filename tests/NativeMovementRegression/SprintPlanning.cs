using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Runtime;
using System.Runtime.CompilerServices;

public static partial class Checks
{
    private static void SprintPlanning()
    {
        var travel = new PlanningTravel(1, 2, 3);
        Check(travel.Seconds(8, 0) == 5 && travel.Seconds(4, 2) == 3 && travel.Seconds(4, 4) == 4,
            "travel spends only the remaining speed-buff duration before returning to baseline");
        Check(float.IsPositiveInfinity(new PlanningTravel(0, 2, 3).Seconds(7, 0)),
            "unknown post-buff movement cannot prove a later arrival");
        Check(!new PlanningTravel(1, 3, 1).Dominates(new(1, 2, 3), 0),
            "a faster short boost cannot dominate a longer useful boost");
        var map = Grid(21, 1);
        var origin = Context(map, 0);
        var action = Action(map, 10, 1, Enumerable.Range(0, 21).Select(i => i == 18 ? 1ul : 0).ToArray(),
            Enumerable.Range(0, 21).Select(i => i == 18 ? 1f : 0).ToArray());
        MovementPlanningContext Input(float duration = 10, ulong resources = 1, float earliest = 0)
            => new(origin.Identity, map, 0, 0, Enumerable.Repeat(true, 21).ToArray(), new float[21],
                [action with { Earliest = earliest }], [], resources,
                utilities: [new(3, PlanningUtilityEffect.MovementSpeed, duration, 0.6f, 0, 0, 2)]);
        var input = Input();
        var plan = JointMovementSearch.Search(input, Exhaustive);
        Check(plan.DeliveredActions == 1 && plan.Stages.Any(s => s is { Kind: MovementStageKind.UtilityAction, ActionId: 3 })
            && Math.Abs(plan.DeliveryTimes[0] - 9.6f) < 0.001f, "Sprint belongs to the first complete useful route when walking misses the action");
        Check(MovementPlanValidation.Validate(plan, input, 0, true, input.Identity.CapturedTimestamp).Allowed,
            "execution validates the same speed expiry and complete onward route");
        Check(JointMovementSearch.Search(Input(duration: 2), Exhaustive).DeliveredActions == 0,
            "a short boost cannot be credited for the rest of a long route");
        Check(!MovementPlanValidation.Validate(plan, Input(resources: 0), 0, true, input.Identity.CapturedTimestamp).Allowed,
            "a consumed sprint cooldown invalidates the unexecuted utility stage");
        var shortMap = Grid(3, 1);
        var shortOrigin = Context(shortMap, 0);
        var relaxed = new MovementPlanningContext(shortOrigin.Identity, shortMap, 0, 0, [true, true, true], new float[3],
            [Action(shortMap, 10, 1, [0, 0, 1], [0, 0, 1], earliest: 5)], [], 1,
            utilities: [new(3, PlanningUtilityEffect.MovementSpeed, 10, 0.6f, 0, 0, 2)]);
        var saved = JointMovementSearch.Search(relaxed, Exhaustive);
        Check(saved.DeliveredActions == 1 && saved.RemainingResources == 1 && saved.Stages.All(s => s.Kind != MovementStageKind.UtilityAction),
            "equivalent action delivery preserves sprint instead of making cosmetic faster movement");
        var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        actor.InstanceID = 1;
        actor.Statuses = new ActorStatus[60];
        var now = DateTime.UtcNow;
        var observation = new NativeSpeedObservation();
        Check(observation.Capture(actor, 1, now, 6).Baseline == 6, "native unboosted movement supplies the baseline");
        actor.Statuses[0] = new(50, 0, now.AddSeconds(2), 1);
        var boosted = observation.Capture(actor, 1, now, 7.8f);
        Check(boosted.Baseline == 6 && boosted.Until == 2 && boosted.At(3) == 6, "observed sprint expiry restores only the matching observed baseline");
        actor.Statuses[1] = new(14, 0, now.AddSeconds(20), 2);
        Check(observation.Capture(actor, 1, now, 5).Baseline == 0, "a changed debuff invalidates the pre-boost speed assumption");
        actor.Statuses[1] = default;
        Check(new NativeSpeedObservation().Capture(actor, 1, now, 7.8f).Baseline == 0,
            "startup during a speed buff cannot invent the player's post-buff speed");
        var settings = new BossMod.CombatAssistance.Config.AssistancePreferences { UseSprint = true, UseGapCloser = false, UsePhantomGapClosers = false };
        Check(NativeMobilityCapture.SprintEnabled(3, settings, Class.DRG)
            && !NativeMobilityCapture.SprintEnabled(41646, settings, Class.DRG),
            "ordinary sprint has its own setting; Occult Sprint retains the saved phantom opt-out");
        Check(NativeMobilityCapture.NoDisplacement(new(7506, MobilityMechanic.Target), default, 0.5f, new(0, 1), 1)
            && !NativeMobilityCapture.NoDisplacement(new(7506, MobilityMechanic.Target), default, 0.5f, new(0, 2), 1)
            && !NativeMobilityCapture.NoDisplacement(new(7515, MobilityMechanic.TargetBackstep, -15), default, 0.5f, new(0, 1), 1),
            "an overlapping targeted rotational action can retain selection; a small dash or any backstep still needs the planner");
    }
}
