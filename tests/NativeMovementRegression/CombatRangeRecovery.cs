using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Runtime;

public static partial class Checks
{
    private static void CombatRangeRecovery()
    {
        var partyWorld = (WorldState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        typeof(WorldState).GetField(nameof(WorldState.Party))!.SetValue(partyWorld, new PartyState(new ActorState()));
        partyWorld.Party.Members[1] = new(0, 20, false); // Duty Support buddy, no player content ID.
        var local = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        var engaged = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        engaged.InstanceID = 10; engaged.IsTargetable = true; engaged.InCombat = true; engaged.TargetID = 20;
        var partyHints = new AIHints();
        partyHints.PotentialTargets.Add(new(engaged, 0, false)); partyHints.Normalize();
        Check(NativeCombatEngagement.Active(partyWorld, partyHints, local),
            "A native eligible enemy attacking a Duty Support buddy keeps combat assistance active when the local flag drops.");
        engaged.TargetID = 30;
        Check(!NativeCombatEngagement.Active(partyWorld, partyHints, local),
            "An unrelated NPC battle cannot activate assistance or authorize a pull.");
        engaged.TargetID = 20; engaged.InCombat = false;
        Check(!NativeCombatEngagement.Active(partyWorld, partyHints, local),
            "A party target relationship without observed enemy combat cannot manufacture an engagement.");
        engaged.InCombat = true; engaged.IsDead = true;
        Check(!NativeCombatEngagement.Active(partyWorld, partyHints, local),
            "A dead enemy cannot keep an old support engagement active.");
        Check(MeleeRecoveryPreference.DesiredPositional(Positional.Rear, 1, 5) == Positional.Any
            && MeleeRecoveryPreference.DesiredPositional(Positional.Flank, 1, 5) == Positional.Any,
            "True North covering the selected action removes rear/flank native goals, preventing pointless side changes between attacks.");
        Check(MeleeRecoveryPreference.DesiredPositional(Positional.Flank, 5, 1) == Positional.Flank
            && MeleeRecoveryPreference.DesiredPositional(Positional.Rear, 0, 0) == Positional.Rear,
            "A positional remains required when True North will expire before the observed attack window.");
        var map = Grid(9, 1);
        map.PixelPriority[8] = 1;
        var identity = Context(map, 0, speed: 1).Identity;
        var recovery = new PlanningRange(10, Point(map, 8), 0, RecoverExecution: true);
        var dash = new MobilityMechanics(7, MobilityMechanic.Target, 20, 0, 0, 0, .1f, 0,
            [new(10, Point(map, 8), 0)]);
        var filler = Action(map, 12, 0, new ulong[9], Enumerable.Repeat(1f, 9).ToArray());
        var talon = filler with { Id = 90, Earliest = 2, LockSeconds = .6f };
        Check(MeleeRecoveryPreference.ForPlanning(Class.DRG, talon) == null
            && MeleeRecoveryPreference.ForPlanning(Class.BRD, talon) == talon
            && MeleeRecoveryPreference.ForPlanning(Class.DRG, filler) == filler,
            "Piercing Talon cannot reserve a wait before melee recovery; ordinary ranged jobs and real melee attacks retain their opportunities.");
        MovementPlanningContext Capture(PlanningAction[] actions, PlanningRange? range = null)
            => new(identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9], actions, [], 1,
                targetId: 10, range: range ?? recovery, mobilityMechanics: [dash]);
        foreach (var actions in new[] { Array.Empty<PlanningAction>(), new[] { filler } })
        {
            var context = Capture(actions);
            var reference = JointMovementSearch.Search(context, Exhaustive,
                pruneResourceInferiorMobility: false, pruneExecutionInferiorStates: false);
            var selected = JointMovementSearch.Search(context, Exhaustive);
            Check(selected.HasVerifiedRoute && selected.Stages.Any(s => s.ActionId == 7)
                && selected.UsefulDestination == Point(map, 8) && selected.RemainingResources == 0,
                "No selected attack, or a ranged filler, cannot prevent a legal dash restoring native combat range.");
            Check(Math.Abs(reference.DurationSeconds - selected.DurationSeconds) < .001f
                && reference.DeliveredActions == selected.DeliveredActions
                && reference.DeliveryTimes.SequenceEqual(selected.DeliveryTimes),
                "Range-recovery resource pruning agrees with exhaustive timing and current-action execution.");
            var first = JointMovementSearch.Search(context, Exhaustive,
                stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false);
            Check(first.HasVerifiedRoute && first.Stages.Any(s => s.ActionId == 7)
                && first.UsefulDestination == Point(map, 8),
                "The first complete opportunity search considers legal target anchors before committing a walking-only route.");
            Check(MovementPlanValidation.Validate(selected, context, 0, true, identity.CapturedTimestamp).Allowed,
                "Gap recovery passes the exact executable native route and resource validation.");
        }
        var cosmetic = JointMovementSearch.Search(Capture([filler], recovery with { RecoverExecution = false }), Exhaustive);
        Check(cosmetic.RemainingResources == 1 && cosmetic.Stages.All(s => s.ActionId != 7),
            "An ordinary range preference cannot spend a charge merely to finish an equivalent move sooner.");
        var timelyWalk = JointMovementSearch.Search(Capture([filler], recovery with { RecoveryReadyIn = 10 }), Exhaustive);
        Check(timelyWalk.RemainingResources == 1 && timelyWalk.Stages.All(s => s.ActionId != 7)
            && timelyWalk.UsefulDestination == Point(map, 8),
            "Walking preserves a useful charge when native range is restored before the observed GCD window.");
        var preferences = new BossMod.CombatAssistance.Config.AssistancePreferences { MinimumGapCloserDistance = 8 };
        var shortMap = Grid(7, 1);
        var shortOrigin = Context(shortMap, 0, speed: 1);
        MovementPlanningContext ShortRecovery(bool hostile) => new(shortOrigin.Identity, shortMap, 0, 0,
            Enumerable.Repeat(true, 7).ToArray(), new float[7], [], [], 1,
            range: new(10, Point(shortMap, 6), 0, RecoverExecution: true, RecoveryReadyIn: 1),
            mobilityMechanics: [new(36951, MobilityMechanic.Target, 20, 0, 0, 0, .1f, 0,
                [new(10, Point(shortMap, 6), 0, MinimumTravel: NativeMobilityCapture.AnchorMinimumTravel(preferences, false, hostile))])]);
        var shortInput = ShortRecovery(true);
        var shortDash = JointMovementSearch.Search(shortInput, Exhaustive);
        Check(shortDash.HasVerifiedRoute && shortDash.Stages.Any(s => s.ActionId == 36951)
            && shortDash.DurationSeconds < 1 && MovementPlanValidation.Validate(shortDash, shortInput, 0, true, shortInput.Identity.CapturedTimestamp).Allowed,
            "The imported eight-yalm preference cannot exclude a six-yalm hostile dash that restores the current melee window; execution uses the same eligibility.");
        var friendlyRelay = ShortRecovery(false);
        var friendlyPlan = JointMovementSearch.Search(friendlyRelay, Exhaustive);
        Check(friendlyPlan.Stages.Any(s => s.ActionId == 36951)
            && MovementPlanValidation.Validate(friendlyPlan, friendlyRelay, 0, true, friendlyRelay.Identity.CapturedTimestamp).Allowed,
            "A friendly short anchor that restores the melee window retains XCAI's execution-relay exception through exact submission validation.");
        var optionalAnchor = new MobilityMechanics(155, MobilityMechanic.Target, 20, 0, 0, 0, .1f, 0,
            [new(20, Point(shortMap, 6), 0, MinimumTravel: NativeMobilityCapture.AnchorMinimumTravel(preferences, false, false))]);
        Check(!optionalAnchor.From(shortOrigin, shortOrigin.Origin).Any() && preferences.MinimumGapCloserDistance == 8,
            "The saved minimum remains intact for a short friendly anchor without evidenced combat access; it is neither rewritten nor applied to every movement action.");
        var jumpMap = Grid(28, 1);
        for (var cell = 8; cell <= 13; ++cell) jumpMap.PixelMaxG[cell] = .8f;
        jumpMap.PixelPriority[26] = 1;
        var jumpOrigin = Context(jumpMap, 0, speed: 6);
        var reverseJump = new MobilityMechanics(94, MobilityMechanic.Directional, 0, -15, .5f, 0, .6f, 0);
        var jumpInput = new MovementPlanningContext(jumpOrigin.Identity, jumpMap, 0, 0,
            Enumerable.Repeat(true, 28).ToArray(), new float[28], [], [], 1,
            range: new(10, Point(jumpMap, 26), 10, RecoverExecution: true), mobilityMechanics: [reverseJump]);
        var firstJump = JointMovementSearch.Search(jumpInput, Exhaustive, stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false);
        var allJumps = JointMovementSearch.Search(jumpInput, Exhaustive, pruneResourceInferiorMobility: false, pruneExecutionInferiorStates: false);
        Check(firstJump.HasVerifiedRoute && firstJump.Stages.Any(s => s.ActionId == 94)
            && firstJump.RangeAccess is { Loss: 0 } && allJumps.RangeAccess is { Loss: 0 },
            "The first opportunity pass includes turning away and reverse-jumping through warnings into useful boss reach when no targeted dash is available.");
        Check(MovementPlanValidation.Validate(firstJump, jumpInput, 0, true, jumpInput.Identity.CapturedTimestamp).Allowed,
            "The initial reverse-jump plan validates its deliberate facing, complete warning crossing and onward route.");
        jumpMap.PixelMaxG[10] = -1;
        var blockedJump = new MovementPlanningContext(jumpOrigin.Identity, jumpMap, 0, 0,
            Enumerable.Repeat(true, 28).ToArray(), new float[28], [], [], 1,
            range: new(10, Point(jumpMap, 26), 10, RecoverExecution: true), mobilityMechanics: [reverseJump]);
        Check(JointMovementSearch.Search(blockedJump, Exhaustive, stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false)
            .Stages.All(s => s.ActionId != 94), "Early reverse-jump consideration does not authorize crossing a physical obstruction.");
        var countdown = Capture([], recovery with { RecoveryReadyIn = 1 });
        var fresh = new MovementPlanningContext(identity with { WorldTime = identity.WorldTime.AddSeconds(.1) }, map,
            0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9], [], [], 1, targetId: 10,
            range: recovery with { RecoveryReadyIn = .9f }, mobilityMechanics: [dash]);
        Check(fresh.SameProblem(countdown),
            "The ordinary observed GCD countdown does not cancel an equivalent range-recovery search every frame.");
        map.PixelMaxG[4] = 2;
        var warning = JointMovementSearch.Search(Capture([filler]), Exhaustive);
        Check(warning.Stages.Any(s => s.ActionId == 7) && warning.UsefulDestination == Point(map, 8),
            "A timed warning can be traversed by a dash when its complete route clears the activation.");
        map.PixelMaxG[4] = -1;
        var blocked = JointMovementSearch.Search(Capture([filler]), Exhaustive);
        Check(blocked.Stages.All(s => s.ActionId != 7) && blocked.UsefulDestination != Point(map, 8),
            "Range recovery never authorizes a dash across a physical obstruction.");

        // Only identity, role and geometry are consumed here. Actor's production
        // constructor requires Lumina data unrelated to this pure capture check.
        var player = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        player.InstanceID = 1; player.Class = Class.DRG; player.HitboxRadius = .5f; player.TargetID = 10;
        var target = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        target.InstanceID = 10; target.PosRot = new(20, 0, 0, 0); target.HitboxRadius = 2; target.IsTargetable = true;
        var hints = new AIHints();
        hints.PotentialTargets.Add(new(target, 0, false));
        hints.Normalize();
        Check(CombatAssistanceRuntime.CaptureCombatRange(hints, player) is { Radius: 5.1f, RecoverExecution: true },
            "Runtime melee recovery includes both native hitboxes and the retained melee-uptime distance.");
        player.Class = Class.BLM;
        Check(CombatAssistanceRuntime.CaptureCombatRange(hints, player) is { Radius: 27.5f },
            "Caster recovery retains its role range rather than requesting melee movement.");
        target.IsTargetable = false;
        Check(CombatAssistanceRuntime.CaptureCombatRange(hints, player) == null,
            "An untargetable enemy does not create a combat-range objective.");
        var warningMap = Grid(4, 1);
        for (var i = 0; i < 3; ++i) warningMap.PixelMaxG[i] = 6;
        warningMap.PixelPriority[3] = 1;
        var warningIdentity = Context(warningMap, 0).Identity;
        var present = Action(warningMap, 8, 0, [0, 0, 0, 0], [1, 1, 1, 1]) with
        { Id = 2, IsGCD = true, LockSeconds = .6f, Timing = new(0, 0, 0, NativeRecast: 2, UnbuffedRecastUpperBound: 2) };
        MovementPlanningContext ContactWindow() => new(warningIdentity, warningMap, 0, 0, [true, true, true, true],
            [0, 0, 0, 0], [present], [], targetId: 10, range: new(10, warningIdentity.Origin, .1f, true));
        var timed = ContactWindow();
        var contactPlan = JointMovementSearch.Search(timed, Exhaustive);
        var contactReference = JointMovementSearch.Search(timed, Exhaustive,
            pruneResourceInferiorMobility: false, pruneExecutionInferiorStates: false);
        Check(contactPlan.HasVerifiedRoute && contactPlan.UsefulDestination == Point(warningMap, 3)
            && contactPlan.Stages.Any(s => s.Kind == MovementStageKind.Wait && s.From == warningIdentity.Origin && s.EndSeconds >= 2)
            && contactPlan.DeliveryTimes[0] == 0 && contactPlan.DurationSeconds == contactReference.DurationSeconds
            && MovementPlanValidation.Validate(contactPlan, timed, 0, true, warningIdentity.CapturedTimestamp).Allowed,
            "A future warning retains the current melee GCD and its evidenced recovery contact before a complete timely escape to shelter.");
        warningMap.PixelMaxG[0] = 1;
        var urgent = ContactWindow();
        var immediate = JointMovementSearch.Search(urgent, Exhaustive);
        Check(immediate.HasVerifiedRoute && immediate.Stages.First(s => s.Kind == MovementStageKind.Walk).StartSeconds < 1
            && MovementPlanValidation.Validate(immediate, urgent, 0, true, warningIdentity.CapturedTimestamp).Allowed,
            "A departure that precedes the next readiness window wins over retained melee access; no future attack or expiry is invented.");
        var rangeWindow = new PlanningRange(10, Point(warningMap, 2), .5f, true);
        var walked = new PlanningRangeAccess(0).Advance(rangeWindow, 0,
            [new(MovementStageKind.Walk, Point(warningMap, 0), Point(warningMap, 3), 0, 3)], Point(warningMap, 3), 3);
        var displaced = new PlanningRangeAccess(0).Advance(rangeWindow, 0,
            [new(MovementStageKind.TargetDash, Point(warningMap, 0), Point(warningMap, 3), 0, 3)], Point(warningMap, 3), 3);
        Check(walked.Loss == 0 && Math.Abs(walked.At - 1.5f) < .001f && displaced.Loss > 0,
            "Walking measures first actual range entry; an unsupported point along a displacement cannot certify an intermediate attack opportunity.");

        // Already departing for cover: dashing back briefly reaches the boss,
        // but the next attack is not ready until after melee must be abandoned.
        // Getting somewhat closer at that window is not a delivered melee GCD.
        var shelterMap = Grid(9, 1);
        for (var cell = 0; cell < 8; ++cell) shelterMap.PixelMaxG[cell] = 2.1f;
        var shelterIdentity = Context(shelterMap, 4, speed: 6).Identity;
        MovementPlanningContext Shelter(float ready) => new(shelterIdentity, shelterMap, 0, 0,
            Enumerable.Repeat(true, 9).ToArray(), new float[9], [], [], 1,
            targetId: 10, range: new(10, Point(shelterMap, 0), .1f, true, ready),
            mobilityMechanics: [new(7, MobilityMechanic.Target, 20, 0, 0, 0, .6f, 0, [new(10, Point(shelterMap, 0), 0)])]);
        var shelter = Shelter(1.5f);
        var shelterSeed = NativeMovementIncumbent.CaptureRoute(shelter, [shelter.Origin, Point(shelterMap, 8)]);
        var sheltered = JointMovementSearch.Search(shelter, Exhaustive, nativeIncumbent: shelterSeed);
        Check(sheltered.HasVerifiedRoute && sheltered.RemainingResources == 1
            && sheltered.Stages.All(s => s.ActionId != 7) && sheltered.UsefulDestination == Point(shelterMap, 8),
            "A cover-bound route cannot spend a dash back toward the boss for partial range that delivers no melee attack at readiness.");
        var available = Shelter(.6f);
        var productiveReturn = JointMovementSearch.Search(available, Exhaustive);
        Check(productiveReturn.HasVerifiedRoute && productiveReturn.RangeAccess is { Loss: 0 }
            && productiveReturn.Stages.Any(s => s.ActionId == 7)
            && MovementPlanValidation.Validate(productiveReturn, available, 0, true, shelterIdentity.CapturedTimestamp).Allowed,
            "A dash that really restores the evidenced melee window and then reaches cover before activation remains available.");

        var hideIdentity = shelterIdentity with { Origin = new(1.5f, .5f),
            CapturedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp() };
        var hideSource = BossMod.Pathfinding.NavigationMapSource.Capture(new ArenaBoundsSquare(6, mapResolution: 1),
            default, null, default, false);
        var hideInput = new BossMod.Pathfinding.NavigationDecision.CapturedInput(hideSource, [],
            [(new SDCircle(default, 4), hideIdentity.WorldTime.AddSeconds(2.1), 0)], new([]), true, [], hideIdentity, true);
        var hideNative = NativeNavigationSnapshot.Capture(hideInput);
        var hide = new MovementPlanningContext(hideIdentity, hideNative.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, hideNative.Cells).ToArray(), new float[hideNative.Cells], [], [], 1,
            native: hideNative, targetId: 10, range: new(10, new(-2.5f, .5f), .1f, true, 3),
            mobilityMechanics: [new(7, MobilityMechanic.Target, 20, 0, 0, 0, .6f, 0, [new(10, new(-2.5f, .5f), 0)])]);
        var hideRecovery = NativeMovementSearch.Recover(hide, new());
        var hideSeed = NativeMovementIncumbent.Capture(hide, hideRecovery.NativeRecovery!.Value.Publication!);
        var uselessEarlyDash = false;
        JointMovementSearch.Search(hide, Exhaustive, nativeIncumbent: hideSeed,
            stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false,
            publishBest: candidate => uselessEarlyDash |= candidate.Stages.Any(s => s.ActionId == 7));
        Check(hideSeed is { HasVerifiedRoute: true } && !uselessEarlyDash,
            "Early mobility publication cannot reverse a verified escape for partial range when no attack window can be recovered.");

        var workload = Grid(40, 40);
        var workloadIdentity = Context(workload, workload.GridToIndex(20, 20)).Identity with { Speed = 6 };
        for (var cell = 0; cell < 1600; ++cell)
        {
            workload.PixelPriority[cell] = 1;
            if ((Point(workload, cell) - workloadIdentity.Origin).LengthSq() < 100)
            { workload.PixelMaxG[cell] = 6; workload.PixelPriority[cell] = 2; }
        }
        var attack = new PlanningAction(2, 10, 0, 12, 0, .6f, 0, [], [],
            Opportunity: new(ExecutionShape.SingleTarget, 10, workloadIdentity.Origin, 3, 0,
                [new(10, workloadIdentity.Origin, 0, 1, true)]), IsGCD: true,
            Timing: new(0, 0, 0, NativeRecast: 2.5f, UnbuffedRecastUpperBound: 2.5f));
        var warningWorkload = new MovementPlanningContext(workloadIdentity, workload, 0, 0,
            Enumerable.Repeat(true, 1600).ToArray(), new float[1600], [attack], [], targetId: 10,
            range: new(10, workloadIdentity.Origin, 3, true));
        var recoverySeed = NativeMovementIncumbent.CaptureRoute(warningWorkload,
            [workloadIdentity.Origin, Point(workload, workload.GridToIndex(30, 20))]);
        var measured = JointMovementSearch.Search(warningWorkload, Exhaustive, nativeIncumbent: recoverySeed, stopAtCurrentOpportunity: true);
        Check(measured.HasVerifiedRoute && measured.DeliveredActions == 1 && measured.RangeAccess is { Loss: 0, At: >= 2.5f }
            && MovementPlanValidation.Validate(measured, warningWorkload, 0, true, workloadIdentity.CapturedTimestamp).Allowed,
            "The warning/next-readiness workload retains a complete verified contact and escape plan over the native map.");
        Console.WriteLine($"Synthetic 1600-cell warning/readiness contact: {measured.SearchMilliseconds:F3}ms, {measured.ExpandedStates} expansions, {measured.Status}/{measured.Bound}; excludes framework capture and live delivery.");

    }
}
