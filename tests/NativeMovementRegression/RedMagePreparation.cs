using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void RedMagePreparation()
    {
        var definitions = new Dictionary<ActionID, ActionDefinition>();
        foreach (var (selected, native) in new (uint, uint)[] { (45960, 7527), (45961, 7528), (45962, 7529) })
        {
            var id = new ActionID(ActionType.Spell, native);
            var definition = definitions[id] = new(id);
            Check(ReferenceEquals(RsrNativeActionDefinition.Find(selected, definitions.GetValueOrDefault), definition),
                "A known ranged Manafication variant retains supported native family metadata without rewriting its action identity.");
            var exactId = new ActionID(ActionType.Spell, selected);
            var exact = definitions[exactId] = new(exactId);
            Check(ReferenceEquals(RsrNativeActionDefinition.Find(selected, definitions.GetValueOrDefault), exact),
                "An actual upstream variant definition always takes precedence over the narrow compatibility alias.");
        }
        Check(RsrNativeActionDefinition.Find(999999, definitions.GetValueOrDefault) == null,
            "Unknown adjusted actions never borrow arbitrary native metadata.");
        var available = new HashSet<uint> { 7527, 7528, 7529, 7530, 37002, 37003 };
        var state = new RedMageIntentState(RedMageRotationKind.Reborn, 0, 0, 0, false, false,
            true, true, false, true, true, 0);
        var choice = RedMageIntentPolicy.Choose(state, available, 3);
        Check(choice is { Kind: ComboIntentKind.Starter, Action: 7530, Fallback: 7527, MinimumTargets: 3, EndsIn: 12 },
            "Supported Reborn policy supplies conditional AoE preparation and an enabled single-target fallback.");
        Check(RedMageIntentPolicy.Choose(state with { Rotation = RedMageRotationKind.Other }, available, 3) == null
            && RedMageIntentPolicy.Choose(state with { EnoughForRotationStarter = false }, available, 3) == null
            && RedMageIntentPolicy.Choose(state with { InstantBeforeMelee = true }, available, 3) == null
            && RedMageIntentPolicy.Choose(state with { StarterResources = false }, available, 3) == null,
            "Mana alone, unknown rotations, pooling gates, instant spell priority and missing resources cannot authorize a starter.");
        Check(RedMageIntentPolicy.Choose(state with { AoePermitted = false }, available, 3) is { Action: 7527, Fallback: 0 }
            && RedMageIntentPolicy.Choose(state with { AoePermitted = false, Manafication = true }, available, 3) == null,
            "Saved AoE policy is binding and Manafication's ranged single-target swords do not create a melee requirement.");
        Check(RedMageIntentPolicy.Choose(state with { Manafication = true }, available, 3) is { Action: 7530, Fallback: 0 }
            && RedMageIntentPolicy.Choose(state, available, 1) is { MinimumTargets: 1 },
            "Manafication may support a real AoE cone, and a saved one-target AoE threshold is not replaced by an old transport policy.");
        available.Remove(7530);
        Check(RedMageIntentPolicy.Choose(state, available, 3) is { Action: 7527 }, "One disabled candidate does not suppress another enabled starter.");
        available.Remove(7527);
        Check(RedMageIntentPolicy.Choose(state, available, 3) == null, "Disabled starters do not acquire preparation authority.");
        available.UnionWith([7527, 7530]);
        foreach (var (last, next, mana, single) in new (uint, uint, bool, bool)[]
        { (7527, 7528, false, true), (7528, 7529, false, true), (7530, 37002, true, false), (37002, 37003, false, false) })
        {
            var active = state with { LastCombo = last, ComboRemaining = 5, ManaStacks = 1, Manafication = mana, InSingleTargetCombo = single };
            Check(RedMageIntentPolicy.Choose(active, available, 3) is { Kind: ComboIntentKind.Continuation } continuation
                && continuation.Action == next && continuation.EndsIn == 5 && continuation.MinimumTargets == 1,
                "ST and AoE continuations use the observed native combo, real next action and original expiry.");
            Check(RedMageIntentPolicy.Choose(active with { ComboRemaining = 0 }, available, 3) == null
                && RedMageIntentPolicy.Choose(active with { ManaStacks = 3 }, available, 3) == null
                && RedMageIntentPolicy.Choose(active with { ContinuationResources = false }, available, 3) == null,
                "Expired combos, ranged-finisher stacks and missing continuation resources cannot renew a melee recovery.");
        }
        var held = state with { Rotation = RedMageRotationKind.Beiruta, HoldRemaining = 2.7f };
        Check(RedMageIntentPolicy.Choose(held, available, 3) is { Kind: ComboIntentKind.RotationHold, EndsIn: 2.7f }
            && RedMageIntentPolicy.Choose(held with { HoldRemaining = 0 }, available, 3) == null,
            "Beiruta preparation requires its explicit current hold and retains the observed remaining time.");
        foreach (var id in new uint[] { 7525, 7526, 16530, 25858, 45960, 45961, 45962 })
            Check(RedMageIntentPolicy.IsRangedFinisher(id) && !RedMageIntentPolicy.CanPrepareAfter(id),
                "Ranged sword/finisher selections cannot be replaced by a different prepared melee track.");
        Check(RedMageIntentPolicy.CanPrepareAfter(7503) && RedMageIntentPolicy.CanPrepareAfter(16526)
            && !RedMageIntentPolicy.CanPrepareAfter(16528) && !RedMageIntentPolicy.CanPrepareAfter(7528)
            && !RedMageIntentPolicy.CanPrepareAfter(37002) && !RedMageIntentPolicy.CanPrepareAfter(999999),
            "Preparation cannot duplicate a selected combo step or assume unchanged gauge after Reprise or an unknown action.");

        var map = Grid(9, 3);
        var origin = Context(map, 9);
        ExecutionActor player = new(1, origin.Origin, 0, 0, default);
        ExecutionActor target = new(10, Point(map, 16), 0, 0.25f, default);
        ExecutionActor[] targets = [target, target with { Id = 11, Position = target.Position + new WDir(0, 0.25f) },
            target with { Id = 12, Position = target.Position + new WDir(0, -0.25f) }];
        var cone = new RsrActionEvidence(7530, target.Id, target.Position.ToVec3(), [10, 11, 12], false, false, 3, 3, 3, 0, 0, 2.5f, 0, 0, 0);
        var sword = cone with { ActionId = 7527, AffectedRecipients = [], CastType = 1, Range = 2, EffectRange = 0 };
        var intent = new RsrRedMageIntent(choice!.Value, cone, sword, 1, 2.5f);
        var preparation = NativeComboPreparation.Capture(intent, player, targets, [])!;
        Check(preparation is { Valid: true, ActionId: 7530, FallbackActionId: 7527, ForecastHorizon: true },
            "Native preparation freezes actual action geometry and distinguishes a horizon from combo expiry.");
        var count = map.Width * map.Height;
        var filler = Action(map, 12, 0, new ulong[count], Enumerable.Repeat(1f, count).ToArray());
        MovementPlanningContext Capture(PlanningComboRecovery? requirement, PlanningAction? current = null, bool mobility = false, PlanningExecutionRules? rules = null)
            => new(origin.Identity, map, 0, 0, Enumerable.Repeat(true, count).ToArray(), new float[count], [current ?? filler], [],
                mobility ? 1ul : 0, comboRecovery: requirement,
                executionRules: rules,
                mobilityMechanics: mobility ? [new(7, MobilityMechanic.Ground, 20, 0, 0, 0, 0.1f, 0)] : []);
        var walk = JointMovementSearch.Search(Capture(preparation), Exhaustive, includeMobilityActions: false, stopAtCurrentOpportunity: true);
        Check(walk.DeliveredActions == 1 && walk.DeliveryTimes[0] == 0 && preparation.Value(walk.UsefulDestination, walk.DurationSeconds) == 3,
            "The first walking pass includes full useful AoE preparation without delaying or replacing the current selected spell.");
        Check(walk.ComboReadyAt == 2.5f, "Prepared execution retains the actual next GCD slot after the selected action.");
        var continued = JointMovementSearch.Search(Capture(preparation), Exhaustive with { Expansions = 0 }, nativeIncumbent: walk);
        Check(continued.ComboReadyAt == walk.ComboReadyAt && continued.ComboExecutionValue == walk.ComboExecutionValue,
            "A verified incumbent retains future GCD timing and preparation value when the broader search is bounded.");
        Check(preparation.Value(walk.UsefulDestination, walk.DurationSeconds, walk.ComboReadyAt, new(float.MaxValue, 2, [])) == 0,
            "A current spell's delivery does not license a later prepared attack after an authored action prohibition.");
        var prohibition = Capture(preparation, rules: new(float.MaxValue, 2, []));
        Check(!MovementPlanValidation.Validate(walk, prohibition, 0, true, origin.Identity.CapturedTimestamp).Allowed
            && JointMovementSearch.Search(prohibition, Exhaustive).UsefulDestination == origin.Origin,
            "A newly authored action prohibition invalidates the old prepared route and removes its elective movement value.");
        var aim = Angle.FromDirection(target.Position - walk.UsefulDestination);
        Check(preparation.Value(walk.UsefulDestination, walk.DurationSeconds, walk.ComboReadyAt,
            new(float.MaxValue, float.MaxValue, [new(aim, 10f.Degrees(), 2)])) == 0,
            "Prepared melee actions respect their exact target-facing direction at the prospective execution time.");
        Check((walk.UsefulDestination - target.Position).Length() >= 2.9f,
            "Preparation uses supported action reach rather than an unnecessarily close old endpoint approximation.");
        Check(JointMovementSearch.Search(Capture(null), Exhaustive).UsefulDestination == origin.Origin,
            "The saved anticipation opt-out still avoids elective preparation walking.");
        var blocked = NativeComboPreparation.Capture(intent, player, targets, [target with { Id = 99 }])!;
        Check(blocked.Value(walk.UsefulDestination, walk.DurationSeconds) <= 1,
            "A forbidden recipient cannot be bought by an aggregate AoE gain; a separately legal single-target option may remain.");
        Check(NativeComboPreparation.Capture(intent, player, targets.Take(2).ToArray(), []) is { ActionId: 7527, FallbackActionId: 0 },
            "Incomplete AoE recipients reject that candidate without suppressing a valid single-target fallback.");
        var lostRecipient = preparation with { Opportunity = new(ExecutionShape.Cone, target.Id, target.Position, 3, 3,
            [new(10, target.Position, 0, 1, true), new(12, targets[2].Position, 0, 1, true), new(13, targets[1].Position, 0, 1, true)], halfAngle: 60f.Degrees()) };
        Check(!lostRecipient.Preserves(preparation)
            && !MovementPlanValidation.Validate(walk, Capture(lostRecipient), 0, true, origin.Identity.CapturedTimestamp).Allowed,
            "An equal count cannot conceal replacing an individually required prepared recipient.");
        Check(!MovementPlanValidation.Validate(walk, Capture(preparation with { ActionId = 37002 }), 0, true, origin.Identity.CapturedTimestamp).Allowed,
            "A different next combo action immediately invalidates an old preparation promise.");
        var hold = preparation with { Kind = ComboIntentKind.RotationHold, EndsIn = 2 };
        Check((hold with { EndsIn = 1.9f }).Same(hold, 0.1f) && !hold.Same(hold, 0.1f)
            && preparation.Same(preparation, 0.1f),
            "Real hold clocks age while the disclosed forecast window remains a separate kind of evidence.");
        var dueLater = filler with { Earliest = 3 };
        Check(JointMovementSearch.Search(Capture(hold, dueLater), Exhaustive).UsefulDestination == origin.Origin,
            "Preparation cannot discard or delay a currently selected action to invent a combo opportunity after its hold expires.");
        var lockedCombo = preparation with { Kind = ComboIntentKind.Continuation, EndsIn = 6, RecastSeconds = 10 };
        var locked = JointMovementSearch.Search(Capture(lockedCombo), Exhaustive);
        Check(locked.ComboReadyAt == 10 && locked.UsefulDestination == origin.Origin,
            "Arriving in range before combo expiry has no execution value when the next real GCD slot is too late.");
        var delayedCombo = preparation with { Kind = ComboIntentKind.Continuation, EndsIn = 6 };
        var delayed = JointMovementSearch.Search(Capture(delayedCombo, filler with { Earliest = 4 }), Exhaustive);
        Check(delayed.ComboReadyAt == 6.5f && delayed.UsefulDestination == origin.Origin,
            "Delaying the selected GCD also delays the prepared combo slot rather than refreshing its original expiry.");
        Check(JointMovementSearch.Search(Capture(preparation with { EligibilityEndsIn = 2 }), Exhaustive).UsefulDestination == origin.Origin,
            "An enabling proc cannot authorize a combo after its observed expiry even when range is reachable.");
        var expiringPolicy = preparation with { EligibilityEndsIn = 5 };
        Check((expiringPolicy with { EligibilityEndsIn = 4.9f }).Same(expiringPolicy, 0.1f)
            && !expiringPolicy.Same(expiringPolicy, 0.1f),
            "Enabling-effect evidence retains its original deadline across replans.");

        // Synthetic geometry deliberately makes the optional ST fallback reachable
        // by walking, while only mobility can restore the complete AoE opportunity.
        var tightCone = cone with { Range = 1, EffectRange = 2 };
        var broadSword = sword with { Range = 3 };
        var shortIntent = intent with { Choice = intent.Choice with { EndsIn = 4.5f, Kind = ComboIntentKind.RotationHold }, Action = tightCone, Fallback = broadSword };
        var shortPreparation = NativeComboPreparation.Capture(shortIntent, player, targets, [])!;
        var walkingOnly = JointMovementSearch.Search(Capture(shortPreparation), Exhaustive);
        Check(shortPreparation.Value(walkingOnly.UsefulDestination, walkingOnly.DurationSeconds) == 1,
            "A supported single-target fallback remains useful when a full AoE preparation cannot be walked in time.");
        var mobility = JointMovementSearch.Search(Capture(shortPreparation, mobility: true), Exhaustive);
        Check(shortPreparation.Value(mobility.UsefulDestination, mobility.DurationSeconds) == 3
            && mobility.Stages.Any(s => s.Kind == MovementStageKind.GroundMovement),
            "Resource pruning cannot discard mobility merely because a weaker single-target preparation was already reachable.");
        var unpruned = JointMovementSearch.Search(Capture(shortPreparation, mobility: true), Exhaustive, pruneResourceInferiorMobility: false);
        Check(unpruned.ExecutionValues.SequenceEqual(mobility.ExecutionValues)
            && shortPreparation.Value(unpruned.UsefulDestination, unpruned.DurationSeconds) == shortPreparation.Value(mobility.UsefulDestination, mobility.DurationSeconds),
            "The proved resource bound preserves the richer prepared execution in an unpruned comparison.");
    }
}
