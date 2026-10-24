using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void PictomancerTiming()
    {
        var now = DateTime.UtcNow;
        var state = new StarryMuseTimingConditions(1, 2, 100, default, []);
        var field = new ObservedStarryMuse(1, 10, default, 0, now.AddSeconds(30));
        var outside = new WPos(10, 0);
        var raw = new PlanningCastTiming(3, 3, float.MaxValue, NativeRecast: 3);
        var hasted = raw with { WithoutProc = 2, HasteCenter = default(WPos), OutsideTimingKnown = false,
            HasteUntil = 30, NativeRecast = 2 };
        PlanningCastTiming Sample(NativePictomancerTiming observer, bool covered, DateTime? time = null,
            ObservedStarryMuse? ground = null, StarryMuseTimingConditions? conditions = null,
            uint action = 100, WPos? position = null, float? cast = null, float? recast = null, bool smudge = true)
            => observer.Observe(conditions ?? state, ground ?? field, time ?? now,
                position ?? (covered ? field.Center : outside), covered, now.AddSeconds(30), action,
                cast ?? (covered ? 2 : 3), recast ?? (covered ? 2 : 3), covered ? hasted : raw, smudge);
        foreach (var insideFirst in new[] { false, true })
        {
            var observer = new NativePictomancerTiming();
            Check(Sample(observer, insideFirst).ObservedFieldCast == null,
                "A single Pictomancer timing sample cannot invent a field multiplier.");
            var paired = Sample(observer, !insideFirst);
            Check(paired is { Valid: true, HasteRadius: 0, ObservedFieldCast: 2, ObservedOutsideCast: 3 }
                && paired.Seconds(3, default, 1) == 2 && paired.Seconds(2, outside, 1) == 3
                && paired.RecastAt(default, 1) == 2 && paired.RecastAt(outside, 1) == 3,
                "Matching selected-action observations support the covered center without inventing a footprint.");
            Check(Sample(observer, false, action: 101).ObservedFieldCast == null,
                "Another Pictomancer spell cannot borrow a selected action's cast or recast sample.");
            Check(Sample(observer, false, now.AddSeconds(1), field with { ExpiresAt = now.AddSeconds(40) }).HasteUntil == 29,
                "Landscape observations cannot refresh the original owned effect deadline.");
            Check(Sample(observer, false, now.AddSeconds(30)).ObservedFieldCast == null,
                "An expired landscape cannot lend faster timing to another action.");
        }
        foreach (var changed in new[] { state with { Player = 2 }, state with { Zone = 3 }, state with { Level = 99 },
            state with { Stats = new(1, 2, 3) }, state with { Modifiers = [new(3688, 4, 1, now.AddSeconds(30))] } })
        {
            var observer = new NativePictomancerTiming();
            Sample(observer, false); Sample(observer, true);
            Check(Sample(observer, false, conditions: changed).ObservedFieldCast == null,
                "Identity, job timing stats and Hyperphantasia stack changes retire unmatched field forecasts.");
        }
        foreach (var changed in new[] { field with { Sequence = 11 }, field with { Center = new(1, 1) },
            field with { Owner = 2 }, field with { Height = 1 } })
        {
            var observer = new NativePictomancerTiming();
            Sample(observer, false); Sample(observer, true);
            Check(Sample(observer, false, ground: changed).ObservedFieldCast == null,
                "A different, foreign or moved landscape cannot inherit prior spatial/timing calibration.");
        }
        var noCenter = new NativePictomancerTiming();
        Sample(noCenter, false); Sample(noCenter, true, position: new(1, 0));
        Check(Sample(noCenter, false).ObservedFieldCast == null,
            "A covered peripheral pose cannot prove an unobserved center or the complete Inspiration area.");
        var modifiers = state with { Modifiers = [new(123, 1, 1, now.AddSeconds(5))] };
        var deadline = new NativePictomancerTiming();
        Sample(deadline, false, conditions: modifiers); Sample(deadline, true, conditions: modifiers);
        Check(Sample(deadline, false, now.AddSeconds(1), conditions: modifiers with
            { Modifiers = [new(123, 1, 1, now.AddSeconds(6))] }).HasteUntil == 4,
            "Matched-condition forecasts retain another modifier's original expiry.");
        var lost = new NativePictomancerTiming();
        Sample(lost, false); Sample(lost, true);
        lost.Observe(state, null, now, outside, false, now.AddSeconds(30), 100, 3, 3, raw, true);
        Check(Sample(lost, false).ObservedFieldCast == null,
            "Losing owned landscape evidence immediately removes its faster return forecast.");
        var instant = new NativePictomancerTiming();
        Sample(instant, false, cast: 0); Sample(instant, true, cast: 0);
        Check(Sample(instant, false, cast: 0) is { ObservedFieldCast: 0, ObservedFieldRecast: 2 },
            "Naturally instant Pictomancer actions retain an independently observed GCD-recovery benefit.");
        var fresh = new NativePictomancerTiming();
        Sample(fresh, false); Sample(fresh, true);
        var stopped = Sample(fresh, true, position: new(0.05f, 0));
        Check(stopped.HasteCenter == new WPos(0.05f, 0) && stopped.Seconds(2, new(0.05f, 0), 1) == 2,
            "Fresh native Inspiration validates an actual stopped pose without pretending arrival tolerance is a teleport.");
        PictomancerReturnPlanning();
        PictomancerNativeObservations(now);
        PictomancerReturnOracle();
    }

    private static void PictomancerReturnPlanning()
    {
        var map = Grid(7, 1);
        var center = Point(map, 6);
        var timing = new PlanningCastTiming(3, 3, float.MaxValue, HasteUntil: 10, HasteCenter: center,
            ObservedFieldCast: 1, ObservedOutsideCast: 3, NativeRecast: 3,
            ObservedFieldRecast: 2, ObservedOutsideRecast: 3);
        var action = Action(map, 12, 0, new ulong[7], Enumerable.Repeat(1f, 7).ToArray(), 3, 1) with { Timing = timing };
        var smudge = new PlanningMobility(0, 6, MovementStageKind.DirectionalMovement, 34684, 0, 0.Degrees(), 0, 0.2f, 0, 0);
        var context = Context(map, 0, [action], [smudge], 1);
        var returned = JointMovementSearch.Search(context, Exhaustive);
        Check(returned.DeliveryTimes[0] == 2 && returned.RemainingResources == 0
            && returned.Stages.Any(s => s.ActionId == 34684)
            && MovementPlanValidation.Validate(returned, context, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "Legal Smudge, its exact landing and evidenced field cast compete in the same native plan.");
        var declined = action with { Timing = timing with { FieldBenefitExcludedAfterAction = 34684 } };
        var disabled = Context(map, 0, [declined], [smudge], 1);
        var held = JointMovementSearch.Search(disabled, Exhaustive);
        Check(held.DeliveryTimes[0] == 4 && held.RemainingResources == 1
            && !MovementPlanValidation.Validate(returned, disabled, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "The saved Starry Muse Smudge opt-out cannot be ignored by search or fresh route validation.");
        Check(!PlanningCastTiming.BenefitAfter(true, returned.Stages.First(s => s.ActionId == 34684), disabled.Actions)
            && !PlanningCastTiming.BenefitAfter(false, new(MovementStageKind.Walk, center, center, 1, 1), disabled.Actions),
            "Walking or waiting after a declined field mobility action cannot launder its forecast benefit.");
        var waiting = declined with { Earliest = 6 };
        var walking = JointMovementSearch.Search(Context(map, 0, [waiting], [smudge], 1), Exhaustive);
        Check(walking.DeliveryTimes[0] == 7 && walking.RemainingResources == 1,
            "Declining Smudge still permits a useful walking return before current-action readiness.");
        map.PixelMaxG[0] = 0.1f;
        var recovery = JointMovementSearch.Search(Context(map, 0, [declined], [smudge], 1), Exhaustive);
        Check(recovery.DeliveredActions == 1 && recovery.RemainingResources == 0 && recovery.DeliveryTimes[0] == 4,
            "A field-specific opt-out preserves general legal Smudge safety recovery without crediting the declined field gain.");
        map.PixelMaxG[0] = float.MaxValue;
        map.PixelMaxG[6] = -1;
        var unsafeField = JointMovementSearch.Search(Context(map, 0, [action], [smudge], 1), Exhaustive);
        Check(unsafeField.RemainingResources == 1 && unsafeField.DeliveryTimes[0] == 4,
            "Inspiration cannot outweigh a native blocked landing.");
        map.PixelMaxG[6] = float.MaxValue;
        var seeded = JointMovementSearch.Search(context, Exhaustive, nativeIncumbent: returned);
        Check(seeded.DeliveryTimes.SequenceEqual(returned.DeliveryTimes) && seeded.RecoveryTimes.SequenceEqual(returned.RecoveryTimes),
            "Continuing a verified route preserves field action/resource timing across the incumbent boundary.");
    }

    private static void PictomancerReturnOracle()
    {
        // Exhaust the meaningful alternatives on a two-cell map independently:
        // stay, walk to the observed point, or take one legal charge. No future
        // action or inferred effect expiry is credited by this reference solver.
        foreach (var casting in new[] { false, true })
        foreach (var ready in new[] { 0f, 0.5f, 1f, 2f })
        foreach (var walkingSpeed in new[] { 0.5f, 1f, 2f })
        foreach (var smudgeAllowed in new[] { false, true })
        foreach (var fieldSafe in new[] { false, true })
        {
            var map = Grid(2, 1);
            if (!fieldSafe) map.PixelMaxG[1] = -1;
            var center = Point(map, 1);
            var slow = casting ? 3f : 0f;
            var fast = casting ? 1f : 0f;
            var timing = new PlanningCastTiming(slow, slow, float.MaxValue, HasteUntil: 10,
                HasteCenter: center, ObservedFieldCast: fast, ObservedOutsideCast: slow, NativeRecast: 3,
                ObservedFieldRecast: 2, ObservedOutsideRecast: 3,
                FieldBenefitExcludedAfterAction: smudgeAllowed ? 0u : 34684u);
            var action = Action(map, 12, 0, [0, 0], [1, 1], slow, ready) with { Timing = timing };
            var smudge = new PlanningMobility(0, 1, MovementStageKind.DirectionalMovement, 34684, 0, 90.Degrees(), 0, 0.2f, 0, 0);
            var input = Context(map, 0, [action], [smudge], 1, speed: walkingSpeed);
            var possibilities = new List<(float Delivery, float Recovery, ulong Resources)>
                { (ready + slow, ready + Math.Max(slow, 3), 1) };
            if (fieldSafe)
            {
                var walkStart = Math.Max(ready, 1 / walkingSpeed);
                var walkDelivery = walkStart + fast;
                var walkRecovery = walkStart + Math.Max(fast, 2);
                // A safe current opportunity is binding; a return cannot buy
                // faster recovery by delivering that current spell later.
                if (walkDelivery <= ready + slow && walkRecovery <= ready + Math.Max(slow, 3))
                    possibilities.Add((walkDelivery, walkRecovery, 1));
                var dashStart = Math.Max(ready, 0.2f);
                var dashDelivery = dashStart + (smudgeAllowed ? fast : slow);
                var dashRecovery = dashStart + Math.Max(smudgeAllowed ? fast : slow, smudgeAllowed ? 2 : 3);
                if (dashDelivery <= ready + slow && dashRecovery <= ready + Math.Max(slow, 3))
                    possibilities.Add((dashDelivery, dashRecovery, 0));
            }
            var expected = possibilities.OrderBy(p => p.Delivery).ThenBy(p => p.Recovery).ThenByDescending(p => p.Resources).First();
            var actual = JointMovementSearch.Search(input, Exhaustive);
            var unpruned = JointMovementSearch.Search(input, Exhaustive, pruneResourceInferiorMobility: false, pruneExecutionInferiorStates: false);
            Check(actual.DeliveredActions == 1 && Math.Abs(actual.DeliveryTimes[0] - expected.Delivery) < 0.001f
                && Math.Abs(actual.RecoveryTimes[0] - expected.Recovery) < 0.001f && actual.RemainingResources == expected.Resources
                && actual.DeliveryTimes.SequenceEqual(unpruned.DeliveryTimes) && actual.RecoveryTimes.SequenceEqual(unpruned.RecoveryTimes),
                "Pictomancer field timing, instant recovery, opt-out and charge preservation match the finite reference alternatives.");
        }
    }

    private static void PictomancerNativeObservations(DateTime now)
    {
        var world = (WorldState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        var actors = new ActorState();
        typeof(WorldState).GetField(nameof(WorldState.Actors))!.SetValue(world, actors);
        typeof(WorldState).GetField(nameof(WorldState.Party))!.SetValue(world, new PartyState(actors));
        world.Frame.Timestamp = now;
        var player = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        player.InstanceID = 1; player.Type = ActorType.Player; player.Class = Class.PCT; player.Level = 100;
        player.Statuses = new ActorStatus[Actor.NumStatuses];
        player.Statuses[0] = new(3689, 0, now.AddSeconds(10), 1);
        world.Party.Members[0] = new(1, 1, false); actors.Actors.Add(1, player); actors.Added.Fire(player);
        var instant = new RsrActionEvidence(34662, 2, default, [], false, false, 2, 25, 5, 0,
            0, 1.8f, 0.1f, 0, 0.6f, WithoutProcCastTime: 0, BaseRecast: 2.5f);
        var current = PlanningCastTiming.Capture(world, player, instant);
        Check(current.HasteCenter == player.Position && current.RecastAt(player.Position, 1) == 1.8f
            && float.IsPositiveInfinity(current.RecastAt(new(10, 0), 1)),
            "Native instant-spell capture cannot retain Inspiration's recast outside the actually covered pose.");
        using var zones = new NativeUtilityZones(world);
        actors.CastEvent.Fire(player, new(new(ActionType.Spell, 34675), 1, 0.6f, 1, default, 10, 1, default));
        Check(zones.StarryMuse(player) is { Sequence: 10, Owner: 1, Center: var center } && center == player.Position,
            "An owned Starry Muse event publishes original placement identity without serialized transport.");
        world.Frame.Timestamp = now.AddSeconds(1);
        Check(zones.StarryMuse(player)?.ExpiresAt == now.AddSeconds(30),
            "Reading a landscape again does not renew its observed thirty-second lifetime.");
        player.Class = Class.BLM;
        Check(zones.StarryMuse(player) == null, "Changing jobs withholds another job's current field.");
    }
}
