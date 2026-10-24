using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void RotationalPreparation()
    {
        var map = Grid(9, 1);
        var identity = Context(map, 0).Identity;
        var origin = identity.Origin;
        var target = Point(map, 8);
        var self = new ExecutionOpportunity(ExecutionShape.Self, identity.ActorId, origin, 0, 0,
            [new(identity.ActorId, origin, 0, 1, true)]);
        Check(self.Evaluate(target).Allowed && self.Evaluate(target).Value == 1,
            "A self-only buff moves with its caster instead of pinning the later route to the captured origin.");
        var setup = new PlanningAction(7389, identity.ActorId, 0, 8, 0, .6f, 0, [], [], Opportunity: self);
        Check(NativeRotationalPreparation.MovementFor(Class.WAR, setup.Id) == 25753
            && NativeRotationalPreparation.MovementFor(Class.NIN, 2267) == 25777
            && NativeRotationalPreparation.MovementFor(Class.SMN, 25838) == 25835
            && NativeRotationalPreparation.MovementFor(Class.DRG, 3555) == 16480
            && NativeRotationalPreparation.MovementFor(Class.DRG, 75) == 0,
            "Only authored setup effects create a conditional rotational movement opportunity.");
        Check(NativeRotationalPreparation.SharedClockAction(2259) == 18805
            && NativeRotationalPreparation.SharedClockAction(2261) == 18806
            && NativeRotationalPreparation.SharedClockAction(2263) == 18807
            && NativeRotationalPreparation.SharedClockAction(2267) == 2267,
            "First mudras use the observed follow-up spell clock, independently of their 20-second charge cooldown.");
        Check(NativeRotationalPreparation.SourceIndex(Class.WAR, 25753, [setup], identity.ActorId, 10) == 0
            && NativeRotationalPreparation.SourceIndex(Class.WAR, 25753, [setup with { TargetId = 10 }], identity.ActorId, 10) < 0,
            "Preparation binds the actually selected action and its correct self/hostile target.");
        var raiton = setup with { Id = 2267, TargetId = 10 };
        Check(NativeRotationalPreparation.MayFinishMudra(Class.NIN, 25777, [raiton], identity.ActorId, 10)
            && !NativeRotationalPreparation.MayFinishMudra(Class.NIN, 2262, [raiton], identity.ActorId, 10)
            && !NativeRotationalPreparation.MayFinishMudra(Class.NIN, 25777, [raiton], identity.ActorId, 11)
            && !NativeRotationalPreparation.MayFinishMudra(Class.NIN, 25777, [], identity.ActorId, 10),
            "Active mudras permit only the selected Raiton's conditional Raiju continuation, never an intervening dash or invented setup.");
        Check(NativeRotationalPreparation.PreparationIndex(Class.NIN, 25777, [raiton], identity.ActorId, 10, true, true) == 0
            && NativeRotationalPreparation.PreparationIndex(Class.NIN, 25777, [raiton], identity.ActorId, 10, true, false) == -1,
            "An existing Raiju stack cannot authorize dashing before a selected Raiton completes the active mudra.");
        var actions = NativeRotationalPreparation.Apply([setup], [new(0, 1)]);
        MovementPlanningContext Input(ImmutableArray<PlanningAction> observed, ulong resources = 0, byte dependencies = 1,
            float ready = 0, float until = float.MaxValue)
            => new(identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9], observed, [], resources,
                targetId: 10, mobilityMechanics: [new(25753, MobilityMechanic.Target, 20, 0, 0, 0, .6f, 0,
                    [new(10, target, 0)], readyIn: ready, allowStationary: true, isGCD: true, recastSeconds: 2.5f,
                    requiredEarlierActions: dependencies, availableUntil: until)], range: new(10, target, 0, true));
        var input = Input(actions);
        var plan = JointMovementSearch.Search(input, Exhaustive);
        var reference = JointMovementSearch.Search(input, Exhaustive,
            pruneResourceInferiorMobility: false, pruneExecutionInferiorStates: false);
        Check(plan.DeliveredActions == 1 && plan.Stages.Any(s => s.Kind == MovementStageKind.TargetDash && s.ActionId == 25753)
            && plan.Stages.First(s => s.ActionId != 0).ActionId == 7389 && plan.UsefulDestination == target
            && plan.RemainingResources == 0 && plan.DeliveryTimes.SequenceEqual(reference.DeliveryTimes)
            && plan.UsefulDestination == reference.UsefulDestination,
            "The first route includes Inner Release then Rend; conditional resources cannot be spent before the setup delivers.");
        Check(MovementPlanValidation.Validate(plan, input, 0, true, identity.CapturedTimestamp).Allowed,
            "Exact route validation replays the delivered setup grant, action lock and movement GCD.");
        var noGrant = JointMovementSearch.Search(Input([setup]), Exhaustive);
        Check(noGrant.Stages.All(s => s.ActionId != 25753),
            "Elapsed time or the setup's name alone cannot manufacture a missing movement proc.");
        var unavailableSetup = JointMovementSearch.Search(Input([actions[0] with { Earliest = 12, Latest = 12 }]), Exhaustive);
        Check(unavailableSetup.Stages.All(s => s.ActionId != 25753),
            "A setup that cannot finish its lock in the current horizon does not authorize an early dash.");
        // Recheck newly selected attack geometry even when the earlier plan used
        // the same mobility as an as-yet unselected subsequent execution.
        var ordinary = JointMovementSearch.Search(Input([], 1, 0), Exhaustive);
        var lostRecipient = new ExecutionOpportunity(ExecutionShape.CasterCircle, 10, target, 0, 1,
            [new(10, target, 0, 1, true), new(11, origin, 0, 1, true)]);
        var changed = Input([new(25753, 10, 0, 8, 0, .6f, 0, [], [], Opportunity: lostRecipient,
            IsGCD: true, Timing: new(0, 0, 0, NativeRecast: 2.5f, UnbuffedRecastUpperBound: 2.5f))], 1, 0);
        Check(!MovementPlanValidation.Validate(ordinary, changed, 0, true, identity.CapturedTimestamp).Allowed,
            "A future Rend landing cannot discard a recipient after the moving attack becomes the current selection.");
        var expired = JointMovementSearch.Search(Input([], 1, 0, ready: 1, until: .5f), Exhaustive);
        Check(expired.Stages.All(s => s.ActionId != 25753),
            "A proc that expires before the observed action window cannot support a movement route.");
        Check(!MovementPlanValidation.Validate(ordinary, Input([], 1, 0, ready: 1, until: .5f), 0, true,
                identity.CapturedTimestamp).Allowed,
            "Fresh proc expiry invalidates an older committed movement immediately.");

        var ten = setup with { Id = 2259, IsGCD = true, LockSeconds = .35f,
            Timing = new(0, 0, 0, NativeRecast: .5f, UnbuffedRecastUpperBound: .5f) };
        var chi = ten with { Id = 18806, RequiredEarlierActions = 1 };
        var lightning = setup with { Id = 2267, TargetId = 10, RequiredEarlierActions = 3, IsGCD = true, GrantedResources = 1,
            Opportunity = new(ExecutionShape.SingleTarget, 10, target, 20, 0, [new(10, target, 0, 1, true)]),
            Timing = new(0, 0, 0, NativeRecast: 1.5f, UnbuffedRecastUpperBound: 1.5f) };
        var mudraInput = new MovementPlanningContext(identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9],
            [ten, chi, lightning], [], 0, targetId: 10, mobilityMechanics: [new(25777, MobilityMechanic.Target, 20, 0, 0, 0, .6f,
                0, [new(10, target, 0)], allowStationary: true, isGCD: true, recastSeconds: 2.112f, requiredEarlierActions: 4)],
            range: new(10, target, 0, true));
        var mudraPlan = JointMovementSearch.Search(mudraInput, Exhaustive);
        Check(mudraPlan.DeliveredActions == 7 && mudraPlan.DeliveryTimes.SequenceEqual([0f, .5f, 1f])
            && mudraPlan.Stages.First(s => s.ActionId == 25777).StartSeconds == 2.5f
            && Math.Abs(mudraPlan.EndGcdReadyAt - 4.612f) < .001f
            && MovementPlanValidation.Validate(mudraPlan, mudraInput, 0, true, identity.CapturedTimestamp).Allowed,
            "Evidenced mudras and Raiton share their actual spell clocks before Raiju; the charge cooldown cannot delay the sequence by 20 seconds.");
        var optionalMudras = new[] { ten with { ResourceCost = 16 }, chi, lightning }
            .Select(a => a with { Preparation = PlanningPreparationMode.Optional }).ToArray();
        MovementPlanningContext Optional(WPos destination, PlanningAction[]? proposed = null, ulong inventory = 16)
            => new(identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9], proposed ?? optionalMudras, [], inventory,
                targetId: 10, mobilityMechanics: [new(25777, MobilityMechanic.Target, 20, 0, 0, 0, .6f, 0,
                    [new(10, destination, 0)], allowStationary: true, isGCD: true, recastSeconds: 2.112f, requiredEarlierActions: 4)],
                range: new(10, destination, 0, true));
        var optional = Optional(target);
        var prepared = JointMovementSearch.Search(optional, Exhaustive);
        var optionalReference = JointMovementSearch.Search(optional, Exhaustive,
            pruneResourceInferiorMobility: false, pruneExecutionInferiorStates: false);
        Check(prepared.DeliveredActions == 7 && prepared.Stages.First(s => s.ActionId != 0).ActionId == 2259
            && prepared.Stages.Any(s => s.ActionId == 25777) && prepared.RemainingResources == 0
            && prepared.DurationSeconds == optionalReference.DurationSeconds
            && prepared.UsefulDestination == optionalReference.UsefulDestination,
            "A full productive optional mudra route can restore melee earlier than walking and spends the real first-mudra charge exactly once.");
        Check(MovementPlanValidation.Validate(prepared, optional, 0, true, identity.CapturedTimestamp).Allowed,
            "The full optional setup uses the same exact native route, spell clocks, dependencies and conditional resource proof.");
        var close = JointMovementSearch.Search(Optional(Point(map, 1)), Exhaustive);
        Check(close.DeliveredActions == 0 && close.Stages.All(s => s.ActionId == 0)
            && close.UsefulDestination == Point(map, 1),
            "Optional setup damage cannot buy slower melee recovery when a short useful walk is already available.");
        var impossible = optionalMudras.Select(a => a.Id == 2267 ? a with { Latest = .9f } : a).ToArray();
        var noPartial = JointMovementSearch.Search(Optional(target, impossible), Exhaustive);
        Check(noPartial.DeliveredActions == 0 && noPartial.Stages.All(s => s.ActionId == 0),
            "A proposal that cannot complete Raiton cannot publish a partial Ten or Chi before correcting its destination later.");
        var noCharge = JointMovementSearch.Search(Optional(target, inventory: 0), Exhaustive);
        Check(noCharge.DeliveredActions == 0 && noCharge.Stages.All(s => s.ActionId == 0),
            "Forecasting a later Raiju grant cannot invent the first mudra's own missing charge.");
        var committed = Optional(target, optionalMudras.Select(a => a with { Preparation = PlanningPreparationMode.Committed }).ToArray());
        Check(committed.PresentActionsMask == 7 && !committed.CompletePreparation(0) && !committed.CompletePreparation(1)
            && committed.CompletePreparation(7) && committed.PreparationLocksActions(0) && !committed.PreparationLocksActions(7)
            && !optional.PreparationLocksActions(0) && optional.PreparationLocksActions(1),
            "An acknowledged mudra sequence retains its remaining required actions and excludes unrelated actions until Raiton delivers.");
        Check(!MovementPlanValidation.Validate(prepared with { Stages = prepared.Stages.Where(s => s.ActionId != 2267).ToImmutableArray() },
                optional, 0, true, identity.CapturedTimestamp).Allowed,
            "Removing a required setup step invalidates its complete route rather than relying on a later correction.");
        var progress = new NativePreparationProgress();
        var now = identity.WorldTime;
        Check(progress.Approve(identity.ActorId, 10, [2259, 18806, 2267], 2259, now)
            && progress.Remaining.SequenceEqual([2259u, 18806u, 2267u]),
            "Original selection approval cannot advance a mudra before its native acknowledgement.");
        progress.Observe(identity.ActorId, 2259, identity.ActorId, 41);
        progress.Observe(identity.ActorId, 2259, identity.ActorId, 41);
        Check(progress.Remaining.SequenceEqual([18806u, 2267u])
            && progress.Approve(identity.ActorId, 10, [18806, 2267], 18806, now.AddSeconds(.5)) && progress.Started == now,
            "Native acknowledgement removes only the completed prefix; repeated frames and effects never restart Ten or refresh its ownership deadline.");
        progress.Observe(identity.ActorId, 18806, identity.ActorId, 42);
        progress.Observe(identity.ActorId, 2267, 10, 43);
        Check(!progress.Active, "Observed Raiton delivery returns subsequent Raiju selection to its actual native proc and the common executor.");
        progress.Approve(identity.ActorId, 10, [2259, 18806, 2267], 2259, now);
        progress.Observe(identity.ActorId, 2247, 10, 44);
        Check(!progress.Active, "An unexpected native action retires the preparation instead of assuming an unobserved mudra effect.");

    }
}
