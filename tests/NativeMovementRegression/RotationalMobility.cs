using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Integrations;
using System.Reflection;

public static partial class Checks
{
    private static void RotationalMobility()
    {
        Check(new uint[] { 25777, 25753, 25835, 16480, 96, 18325, 11389, 11401 }.All(NativeRotationalMobility.Contains)
            && !NativeRotationalMobility.Contains(25778) && !NativeRotationalMobility.Contains(92)
            && !NativeRotationalMobility.Contains(16478),
            "Rotational movement includes actual displacement actions and excludes Fleeting Raiju, Jump and High Jump.");
        var map = Grid(9, 1);
        var identity = Context(map, 0).Identity;
        var destination = Point(map, 8);
        var opportunity = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, destination, 20, 0,
            [new(10, destination, 0, 1, true)]);
        var attack = new PlanningAction(25777, 10, 0.5f, 8, 0, 0.6f, 0, [], [], Opportunity: opportunity,
            IsGCD: true, Timing: new(0, 0, 0, NativeRecast: 2.5f, UnbuffedRecastUpperBound: 2.5f));
        var dash = new MobilityMechanics(25777, MobilityMechanic.Target, 20, 0, 0, 0, 0.6f, 0,
            [new(10, destination, 0)], readyIn: 0.5f, allowStationary: true, isGCD: true, recastSeconds: 2.5f);
        MovementPlanningContext Input(PlanningAction[] actions)
            => new(identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9], actions, [], 1,
                targetId: 10, mobilityMechanics: [dash], range: new(10, destination, 0, true));
        var input = Input([attack]);
        var plan = JointMovementSearch.Search(input, Exhaustive);
        Check(plan.DeliveredActions == 1 && plan.UsefulDestination == destination && plan.RemainingResources == 0
            && plan.Stages.Count(s => s.ActionId == 25777) == 1 && !plan.Stages.Any(s => s.Kind == MovementStageKind.Cast)
            && Math.Abs(plan.DeliveryTimes[0] - 1.1f) < .001f && Math.Abs(plan.EndGcdReadyAt - 3) < .001f,
            "Forked Raiju delivers the selected current action through one real dash instead of dashing and casting it twice.");
        Check(plan.ActionEvidence is [{ Id: 25777, SourceActionIndex: 0 }]
            && MovementPlanValidation.Validate(plan, input, 0, true, identity.CapturedTimestamp).Allowed,
            "The moving action retains its exact source index, recipients, timing and final route proof.");
        var dashIndex = Array.FindIndex(plan.Stages.ToArray(), s => s.ActionId == 25777);
        var offeredInput = Input([]);
        var offeredPlan = JointMovementSearch.Search(offeredInput, Exhaustive);
        var offeredIndex = Array.FindIndex(offeredPlan.Stages.ToArray(), s => s.ActionId == 25777);
        var selector = (RsrRotationalDelivery)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(RsrRotationalDelivery));
        var request = new MovementExecution(MovementExecutionKind.Action, "test", destination, ActionId: 25777, TargetId: 10,
            DepartureSeconds: float.MaxValue);
        Check(!RsrRotationalDelivery.RequiresGcdSelection(input, dashIndex, plan)
            && RsrRotationalDelivery.RequiresGcdSelection(offeredInput, offeredIndex, offeredPlan)
            && !selector.MaySubmit(request, offeredPlan, offeredInput, offeredIndex, out var rejection) && rejection == null,
            "A planned movement GCD awaits the original RSR selection rather than entering the manual command queue.");
        var selectionFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(RsrRotationalDelivery).GetField("_attempted", selectionFlags)!.SetValue(selector,
            (offeredPlan, offeredIndex, System.Diagnostics.Stopwatch.GetTimestamp()));
        Check(!selector.MaySubmit(request, offeredPlan, offeredInput, offeredIndex, out rejection) && rejection != null,
            "An original CanUse rejection removes only this movement candidate instead of holding a ranged fallback indefinitely.");
        typeof(RsrRotationalDelivery).GetField("_approved", selectionFlags)!.SetValue(selector,
            (offeredPlan, offeredIndex, System.Diagnostics.Stopwatch.GetTimestamp()));
        Check(selector.MaySubmit(request, offeredPlan, offeredInput, offeredIndex, out rejection) && rejection == null
            && !selector.MaySubmit(request, offeredPlan with { Evidence = "new plan" }, offeredInput, offeredIndex, out _),
            "A selection approval belongs to one exact plan/stage, not another route using the same action ID.");
        selector.ResetPlan();
        typeof(RsrRotationalDelivery).GetField("_approved", selectionFlags)!.SetValue(selector,
            (offeredPlan, offeredIndex, System.Diagnostics.Stopwatch.GetTimestamp() - System.Diagnostics.Stopwatch.Frequency));
        Check(!selector.MaySubmit(request, offeredPlan, offeredInput, offeredIndex, out rejection) && rejection == null,
            "A stale original selection cannot authorize a later movement GCD.");
        Check(!selector.MaySubmit(request with { DepartureSeconds = 0 }, offeredPlan, offeredInput, offeredIndex, out rejection)
            && rejection != null, "A mandatory native departure uses recovery immediately instead of waiting for a selector callback.");
        var next = Action(map, 8, 0, new ulong[9], Enumerable.Repeat(1f, 9).ToArray()) with
        { Id = 2, IsGCD = true, RequiredEarlierActions = 1, Timing = new(0, 0, 0, NativeRecast: 2.5f, UnbuffedRecastUpperBound: 2.5f) };
        var two = Input([attack, next]);
        var sequence = JointMovementSearch.Search(two, Exhaustive);
        Check(sequence.DeliveredActions == 3 && Math.Abs(sequence.DeliveryTimes[1] - 3) < .001f
            && MovementPlanValidation.Validate(sequence, two, 0, true, identity.CapturedTimestamp).Allowed,
            "A movement GCD consumes the shared GCD window even though its animation ends sooner.");
        var first = JointMovementSearch.Search(two, Exhaustive, stopAtCurrentOpportunity: true);
        var continued = JointMovementSearch.Search(two, Exhaustive with { Expansions = 0 }, nativeIncumbent: first);
        Check(continued.DeliveredActions == first.DeliveredActions && continued.EndGcdReadyAt == first.EndGcdReadyAt,
            "A continued incumbent keeps the consumed GCD clock rather than reopening the movement GCD's window.");

        var fleeting = attack with { Id = 25778, Opportunity = opportunity.WithCastReach(3) };
        var withAlternative = NativeRotationalMobility.WithReadyAlternatives(fleeting, new([dash], 1))!;
        Check(withAlternative.MovingAlternatives is [{ ActionId: 25777 }] && withAlternative.ResourceCost == dash.ResourceCost,
            "An actually available Forked Raiju shares Fleeting's single evidenced proc and current target opportunity.");
        var equivalentInput = Input([withAlternative]);
        var equivalent = JointMovementSearch.Search(equivalentInput, Exhaustive);
        var referenceEquivalent = JointMovementSearch.Search(equivalentInput, Exhaustive,
            pruneResourceInferiorMobility: false, pruneExecutionInferiorStates: false);
        Check(equivalent.DeliveredActions == 1 && equivalent.ActionEvidence is [{ Id: 25777, SourceActionIndex: 0 }]
            && equivalent.Stages.Count(s => s.ActionId == 25777) == 1 && equivalent.Stages.All(s => s.ActionId != 25778)
            && equivalent.RemainingResources == 0 && equivalent.DeliveryTimes[0] < 2
            && equivalent.DeliveryTimes.SequenceEqual(referenceEquivalent.DeliveryTimes),
            "Selected Fleeting Raiju can be delivered by one faster legal Forked Raiju, with its real identity and shared proc recorded.");
        Check(MovementPlanValidation.Validate(equivalent, equivalentInput, 0, true, identity.CapturedTimestamp).Allowed,
            "Equivalent moving execution revalidates its exact native anchor, resources, timing and recipients.");
        var withoutAlternative = NativeRotationalMobility.WithReadyAlternatives(fleeting, AvailableMobility.None)!;
        Check(ReferenceEquals(withoutAlternative, fleeting),
            "Missing/disabled native Forked eligibility does not invent a moving alternative or suppress Fleeting.");
        var unrelated = attack with { Id = 75 };
        Check(ReferenceEquals(NativeRotationalMobility.WithReadyAlternatives(unrelated, new([dash], 1)), unrelated),
            "A rotational dash cannot purchase credit for an unrelated current attack.");
        var nearIdentity = identity with { Origin = Point(map, 7) };
        var near = new MovementPlanningContext(nearIdentity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9],
            [withAlternative], [], 1, targetId: 10, mobilityMechanics: [dash]);
        var noUnneededDash = JointMovementSearch.Search(near, Exhaustive);
        Check(noUnneededDash.ActionEvidence is [{ Id: 25778 }] && noUnneededDash.Stages.All(s => s.ActionId != 25777)
            && noUnneededDash.RemainingResources == 0,
            "At melee reach the ordinary Fleeting hit wins and consumes its real proc without an unnecessary dash.");

        MovementPlanningContext Flight(float seconds, WPos position, bool consumed)
        {
            var freshIdentity = identity with { WorldTime = identity.WorldTime.AddSeconds(seconds), Origin = position,
                CapturedTimestamp = identity.CapturedTimestamp + (long)(System.Diagnostics.Stopwatch.Frequency * seconds) };
            return new(freshIdentity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9],
                consumed ? [] : [attack with { Earliest = 0 }], [], consumed ? 0UL : 1UL,
                targetId: 10, mobilityMechanics: consumed ? [] : [new MobilityMechanics(25777, MobilityMechanic.Target,
                    20, 0, 0, 0, .6f, 0, [new(10, destination, 0)], allowStationary: true, isGCD: true, recastSeconds: 2.5f)]);
        }
        MovementPlanExecutor Submitted()
        {
            var executor = new MovementPlanExecutor();
            Check(executor.Advance(plan, input, default, true, identity.CapturedTimestamp).Kind == MovementExecutionKind.Wait,
                "The moving GCD waits for its native observed execution window.");
            var ready = Flight(.5f, identity.Origin, false);
            var facing = plan.Stages.First(s => s.ActionId == 25777).Facing ?? default;
            var directive = executor.Advance(plan, ready, new(facing), true, ready.Identity.CapturedTimestamp);
            Check(directive.Kind == MovementExecutionKind.Action,
                "The exact ready moving action is submitted through the one executor: " + directive.Kind + "/" + directive.Reason);
            executor.ConfirmActionSubmission(plan);
            return executor;
        }
        var submitted = Submitted();
        var flying = Flight(.6f, Point(map, 4), true);
        Check(submitted.Advance(plan, flying, new(default, ActionLocked: true), true, flying.Identity.CapturedTimestamp)
                is { Kind: MovementExecutionKind.Wait, SubmissionPending: true }
            && submitted.AwaitingActionEffect,
            "A consumed movement charge/proc cannot replace an already submitted flight with a second action or walk.");
        var effect = new MovementExecutionObservation(default, 25777, 10, identity.WorldTime.AddSeconds(.55), ActionLocked: true);
        Check(submitted.Advance(plan, flying, effect, true, flying.Identity.CapturedTimestamp)
            is { Kind: MovementExecutionKind.Wait, SubmissionPending: true },
            "An action effect without the actual matching landing cannot complete the movement stage.");
        var landed = Flight(.7f, destination, true);
        Check(submitted.Advance(plan, landed, effect, true, landed.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Arrived
            && !submitted.AwaitingActionEffect,
            "The corroborated movement effect and landing complete even after the required proc/resource is consumed.");
        submitted = Submitted();
        Check(submitted.Advance(plan, flying, default, false, flying.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated
            && !submitted.AwaitingActionEffect,
            "Manual handoff releases a submitted flight immediately.");
        submitted = Submitted();
        Check(submitted.Advance(plan, flying, default, true, flying.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "A submitted movement with no corroborated effect/landing after its native lock ends is not reported as completed.");

        var insideMap = Grid(1, 1);
        var insideIdentity = Context(insideMap, 0).Identity;
        var insideOpportunity = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, insideIdentity.Origin, 20, 0,
            [new(10, insideIdentity.Origin, 2, 1, true)]);
        var insideDash = new MobilityMechanics(96, MobilityMechanic.Target, 20, 0, .5f, 0, .8f, 0,
            [new(10, insideIdentity.Origin, 2)], allowStationary: true);
        var inside = new MovementPlanningContext(insideIdentity, insideMap, 0, 0, [true], [0],
            [new(96, 10, 0, 4, 0, .8f, 0, [], [], Opportunity: insideOpportunity)], [], 1, targetId: 10, mobilityMechanics: [insideDash]);
        var normalDamage = JointMovementSearch.Search(inside, Exhaustive);
        Check(normalDamage.DeliveredActions == 1 && normalDamage.Stages is [{ Kind: MovementStageKind.TargetDash, ActionId: 96 }]
            && normalDamage.Stages[0].From == normalDamage.Stages[0].To
            && MovementPlanValidation.Validate(normalDamage, inside, 0, true, insideIdentity.CapturedTimestamp).Allowed,
            "Normal rotational dive damage remains executable at contact without inventing an unnecessary walk.");
        map.PixelMaxG[8] = .2f;
        var unsafeLanding = JointMovementSearch.Search(Input([attack]), Exhaustive);
        Check(unsafeLanding.DeliveredActions == 0 && !unsafeLanding.Stages.Any(s => s.ActionId == 25777),
            "Current damage cannot purchase an unsafe rotational landing.");

        Check(!RsrRotationalDelivery.Permits(ActionType.Spell, 25777, false, true)
            && RsrRotationalDelivery.Permits(ActionType.Spell, 25777, true, true)
            && RsrRotationalDelivery.Permits(ActionType.Spell, 25777, false, false)
            && RsrRotationalDelivery.Permits(ActionType.Spell, 75, false, true),
            "Only automatic owned rotational delivery is held; forced uses, opt-outs and ordinary attacks retain their original handling.");
        var manager = (ActionManagerEx)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ActionManagerEx));
        var field = typeof(ActionManagerEx).GetField("_automaticDeliveryGate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Func<ActionID, bool> firstGate = _ => false, secondGate = _ => true;
        using (manager.EnterAutomaticDeliveryGate(firstGate))
        {
            using (manager.EnterAutomaticDeliveryGate(secondGate))
                Check(ReferenceEquals(field.GetValue(manager), secondGate), "Nested original-callback delivery scope owns only its installed predicate.");
            Check(ReferenceEquals(field.GetValue(manager), firstGate), "Unwinding a nested scope restores the prior owner.");
        }
        Check(field.GetValue(manager) == null, "The callback cannot leave an action veto installed after the original RSR update.");
        Check(PlanningCastTiming.ProcStatus(3) == 167 && PlanningCastTiming.ProcStatus(255) == 0,
            "Instant mobility uses the native Swiftcast status mapping; an unknown cast proc supplies no invented lifetime.");
        var instantMap = Grid(9, 1);
        PlanningMobility[] instantMoves =
        [new(0, 4, MovementStageKind.TargetDash, 11389, 10, null, 0, .6f, 0, 1, AdditionalResourceCost: 1,
            IsGCD: true, RecastSeconds: 2.5f, AvailableUntil: 5),
         new(4, 8, MovementStageKind.GroundMovement, 11401, 0, null, 0, .6f, 0, 2, AdditionalResourceCost: 1,
            IsGCD: true, RecastSeconds: 2.5f, AvailableUntil: 5)];
        MovementPlanningContext Instant(PlanningMobility[] movement) => new(identity, instantMap, 0, 0,
            Enumerable.Repeat(true, 9).ToArray(), new float[9], [], movement, 0x111, targetId: 20,
            range: new(20, Point(map, 8), 0, true));
        var singleInstant = JointMovementSearch.Search(Instant(instantMoves), Exhaustive);
        var unshared = JointMovementSearch.Search(Instant(instantMoves.Select(m => m with { AdditionalResourceCost = 0 }).ToArray()), Exhaustive);
        Check(singleInstant.Stages.Count(s => s.ActionId is 11389 or 11401) <= 1
            && unshared.Stages.Count(s => s.ActionId is 11389 or 11401) == 2,
            "One observed Swiftcast is shared by Flying Frenzy and Loom instead of being cloned into two independent instant movements.");
        var expiring = instantMoves.Select(m => m with { ReadyIn = 1, AvailableUntil = .5f }).ToArray();
        var expiredInstant = JointMovementSearch.Search(Instant(expiring), Exhaustive);
        Check(expiredInstant.Stages.All(s => s.ActionId is not (11389 or 11401)),
            "BLU instant-cast expiry excludes the movement without suppressing the remaining walking route.");

        Check(RsrRotationalDelivery.YieldToCurrent(36951, 7399, false, true, .6f, 2)
            && !RsrRotationalDelivery.YieldToCurrent(36951, 7399, false, true, .6f, .2f)
            && !RsrRotationalDelivery.YieldToCurrent(36951, 7399, false, false, .6f, 2)
            && !RsrRotationalDelivery.YieldToCurrent(36951, 90, true, true, .6f, 2),
            "A still-executable present RSR ability retains its window before elective movement; an urgent departure, unavailable ability or ranged filler cannot block recovery.");
        var futureDive = new MovementPlanningContext(identity, instantMap, 0, 0, Enumerable.Repeat(true, 9).ToArray(),
            new float[9], [], [], 1, targetId: 10, mobilityMechanics: [new(16480, MobilityMechanic.Target, 20, 0, 0, 0, .6f,
                0, [new(10, Point(instantMap, 8), 0)], allowStationary: true)], range: new(10, Point(instantMap, 8), 0, true));
        var futureDivePlan = JointMovementSearch.Search(futureDive, Exhaustive);
        var futureDiveIndex = Array.FindIndex(futureDivePlan.Stages.ToArray(), s => s.ActionId == 16480);
        Check(futureDiveIndex >= 0 && RsrRotationalDelivery.RequiresRotationSelection(futureDive, futureDiveIndex, futureDivePlan)
            && !RsrRotationalDelivery.RequiresGcdSelection(futureDive, futureDiveIndex, futureDivePlan),
            "An as-yet unselected Stardiver uses original RSR ability selection and its existing offensive/healing priority, as GCD movement uses the original GCD slot.");

    }
}
