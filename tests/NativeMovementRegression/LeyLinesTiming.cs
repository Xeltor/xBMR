using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void LeyLinesTiming()
    {
        var now = DateTime.UtcNow;
        var conditions = new LeyLinesTimingConditions(1, 2, 100, default, 3, []);
        var field = new ObservedLeyLines(1, 10, default, 0, now.AddSeconds(20));
        var outside = new WPos(6, 0);
        var baseline = new PlanningCastTiming(3, 4, float.MaxValue);
        var inside = baseline with { WithoutProc = 2, HasteCenter = default(WPos), HasteRadius = 3, HasteUntil = 20 };
        PlanningCastTiming Observe(NativeLeyLinesTiming observer, bool covered, DateTime? time = null,
            ObservedLeyLines? ground = null, LeyLinesTimingConditions? state = null, uint action = 100, float? cast = null,
            bool walking = true, float? recast = null)
            => observer.Observe(state ?? conditions, ground ?? field, time ?? now, covered ? default : outside,
                covered, action, cast ?? (covered ? 2 : 3), covered ? inside : baseline, walking, recast ?? (covered ? 2 : 3));
        foreach (var insideFirst in new[] { false, true })
        {
            var observer = new NativeLeyLinesTiming();
            var first = Observe(observer, insideFirst);
            Check(first.ObservedFieldCast == null, "One cast observation cannot invent a Ley Lines multiplier.");
            var paired = Observe(observer, !insideFirst);
            Check(paired is { Valid: true, ObservedFieldCast: 2, ObservedOutsideCast: 3, HasteUntil: 20 }
                && paired.Seconds(3, default, 1) == 2 && paired.Seconds(2, outside, 1) == 3,
                "Matching inside/outside samples work in either order and preserve the actual outside cast.");
            Check(paired.Seconds(0, outside, 1) == 0 && paired.Seconds(2, default, 20) == 3
                && paired.RecastAt(default, 1) == 2 && paired.RecastAt(outside, 1) == 3,
                "Observed return timing preserves instants and never keeps field speed after expiry or outside its footprint.");
            var unknownAction = Observe(observer, false, action: 101);
            Check(unknownAction.ObservedFieldCast == null, "An unrelated spell cannot borrow another action's observed speed.");
            var jitter = Observe(observer, false, now.AddSeconds(1), field with { ExpiresAt = now.AddSeconds(21) });
            Check(jitter.ObservedFieldCast == 2 && jitter.HasteUntil == 19,
                "Native expiry jitter neither discards a valid calibration nor renews its original deadline.");
            Check(observer.PlanningField(now.AddSeconds(1)) is { EndsIn: 19 },
                "Mobility and relocation capture the same stabilized original field deadline as the cast profile.");
            var expired = Observe(observer, false, now.AddSeconds(20));
            Check(expired.ObservedFieldCast == null, "An expired field cannot lend a faster cast to its successor.");
            Observe(observer, false, now.AddSeconds(20), field with { ExpiresAt = now.AddSeconds(21) });
            Check(observer.CurrentField == null && observer.PlanningField(now.AddSeconds(20)) == null,
                "A stale native extension cannot renew an expired effect merely because the same object remains present.");
        }
        foreach (var changed in new[] { conditions with { Player = 2 }, conditions with { Zone = 3 },
            conditions with { Level = 99 }, conditions with { Element = -3 }, conditions with { Stats = new(100, 200, 0) },
            conditions with { Modifiers = [new(123, 0, 1, now.AddSeconds(10))] } })
        {
            var observer = new NativeLeyLinesTiming();
            Observe(observer, false);
            Observe(observer, true);
            Check(Observe(observer, false, state: changed).ObservedFieldCast == null,
                "Changed player, territory, level, element or another timing modifier retires counterfactual field speed.");
        }
        var expiryObserver = new NativeLeyLinesTiming();
        var modified = conditions with { Modifiers = [new(123, 2, 1, now.AddSeconds(5))] };
        Observe(expiryObserver, false, state: modified);
        Observe(expiryObserver, true, state: modified);
        var clipped = Observe(expiryObserver, false, now.AddSeconds(1), state: modified with
        { Modifiers = [new(123, 2, 1, now.AddSeconds(6))] });
        Check(clipped.ObservedFieldCast == 2 && clipped.HasteUntil == 4,
            "Other buff deadline jitter cannot renew the original matched-condition proof.");
        Check(Observe(expiryObserver, false, now.AddSeconds(5), state: modified).ObservedFieldCast == null,
            "A paired observation loses authority when another observed modifier expires.");
        var replacement = new NativeLeyLinesTiming();
        Observe(replacement, false);
        Observe(replacement, true);
        Check(Observe(replacement, false, ground: field with { Object = 11 }).ObservedFieldCast == null,
            "A newly observed field needs its own inside calibration.");
        Observe(replacement, true);
        var slowerCast = Observe(replacement, true, cast: 3);
        Check(slowerCast.ObservedFieldCast == 3 && slowerCast.Seconds(3, default, 1) == 3 && slowerCast.ObservedFieldRecast == 2,
            "A slower inside observation revokes the cast gain while keeping a separately evidenced recast gain identifiable.");
        Check(Observe(replacement, true, cast: 3, recast: 3).ObservedFieldCast == null,
            "Losing both cast and recast gains removes the old improvement.");
        var reversal = new NativeLeyLinesTiming();
        Observe(reversal, false);
        Observe(reversal, true);
        Check(Observe(reversal, false, now.AddSeconds(-1)).ObservedFieldCast == null,
            "A reset native clock cannot reuse samples from the future.");
        var lifecycle = new NativeLeyLinesTiming();
        Observe(lifecycle, false);
        Observe(lifecycle, true);
        Observe(lifecycle, false, now.AddSeconds(1), field with { ExpiresAt = now.AddSeconds(21) }, conditions with { Element = -3 });
        Check(lifecycle.PlanningField(now.AddSeconds(1)) is { EndsIn: 19 },
            "A changed elemental stance clears calibration without renewing the same personal field's original lifetime.");
        lifecycle.ObserveAction(2, new(ActionType.Spell, 3573), now.AddSeconds(1));
        lifecycle.ObserveAction(1, new(ActionType.Spell, 36988), now.AddSeconds(1));
        Check(lifecycle.PlanningField(now.AddSeconds(1)) is { EndsIn: 19 },
            "Another player's placement and an owned Retrace cannot renew the tracked lifetime.");
        lifecycle.ObserveAction(1, new(ActionType.Spell, 3573), now.AddSeconds(2));
        Observe(lifecycle, false, now.AddSeconds(2), field with { ExpiresAt = now.AddSeconds(22) });
        Check(lifecycle.PlanningField(now.AddSeconds(2)) is { EndsIn: 20 },
            "A real owned Ley Lines placement can establish a new observed effect lifetime, including a reused object identity.");
        var missing = new NativeLeyLinesTiming();
        Observe(missing, false);
        Observe(missing, true);
        missing.Observe(conditions, null, now.AddSeconds(1), outside, false, 100, 3, baseline, true, 3);
        Check(missing.CurrentField == null, "A missing or ambiguous object cannot supply spatial return proof.");
        Observe(missing, false, now.AddSeconds(2), field with { ExpiresAt = now.AddSeconds(21) });
        Check(missing.PlanningField(now.AddSeconds(2)) is { EndsIn: 18 },
            "Reappearing native geometry cannot restart the same effect's original deadline.");

        foreach (var actionOwner in new[] { 1ul, 2ul })
        {
            var relocated = new NativeLeyLinesTiming();
            Observe(relocated, false);
            Observe(relocated, true);
            relocated.ObserveAction(actionOwner, new(ActionType.Spell, 36988), now.AddSeconds(1));
            var profile = Observe(relocated, false, now.AddSeconds(2), field with
            { Object = 11, Center = outside, ExpiresAt = now.AddSeconds(21) });
            Check(relocated.PlanningField(now.AddSeconds(2)) is { Object: 11, EffectIdentity: var effect, EndsIn: var ends }
                && effect == (actionOwner == 1 ? 10ul : 11ul) && ends == (actionOwner == 1 ? 18 : 19),
                "Only an observed owned Retrace can carry the original effect identity/deadline onto a replacement object.");
            Check((profile.ObservedFieldCast != null) == (actionOwner == 1),
                "A corroborated relocation retains same-effect calibration; another owner's action cannot lend it to a new object.");
        }
        var expiredRetrace = new NativeLeyLinesTiming();
        Observe(expiredRetrace, false);
        expiredRetrace.ObserveAction(1, new(ActionType.Spell, 36988), now.AddSeconds(20));
        Observe(expiredRetrace, false, now.AddSeconds(21), field with { Object = 11, ExpiresAt = now.AddSeconds(30) });
        Check(expiredRetrace.PlanningField(now.AddSeconds(21))?.EffectIdentity == 11,
            "A relocation event after expiry cannot establish continuity for a later ground object.");
        var interruptedRelocation = new NativeLeyLinesTiming();
        Observe(interruptedRelocation, false);
        Observe(interruptedRelocation, true);
        interruptedRelocation.ObserveAction(1, new(ActionType.Spell, 36988), now.AddSeconds(1));
        interruptedRelocation.Observe(conditions, null, now.AddSeconds(1), outside, false, 100, 3, baseline, true, 3);
        Observe(interruptedRelocation, false, now.AddSeconds(2), field with { Object = 11, ExpiresAt = now.AddSeconds(21) });
        Check(interruptedRelocation.PlanningField(now.AddSeconds(2)) is { Object: 11, EffectIdentity: 10, EndsIn: 18 }
            && interruptedRelocation.CurrentField?.ExpiresAt == field.ExpiresAt,
            "A temporary object gap withholds geometry without renewing a corroborated relocation's original effect.");
        interruptedRelocation.ObserveAction(1, new(ActionType.Spell, 3573), now.AddSeconds(3));
        Observe(interruptedRelocation, false, now.AddSeconds(3), field with { Object = 12, ExpiresAt = now.AddSeconds(23) });
        Check(interruptedRelocation.PlanningField(now.AddSeconds(3)) is { Object: 12, EffectIdentity: 12, EndsIn: 20 },
            "A real placement resets pending relocation continuity rather than lending old identity to the new effect.");

        var map = Grid(7, 1);
        var center = Point(map, 6);
        var timing = baseline with { HasteCenter = center, HasteRadius = 0.4f, HasteUntil = 10,
            ObservedFieldCast = 1, ObservedOutsideCast = 3, NativeRecast = 3,
            ObservedFieldRecast = 2, ObservedOutsideRecast = 3, FieldEntryByAbility = true };
        var action = Action(map, 12, 0, new ulong[7], Enumerable.Repeat(1f, 7).ToArray(), cast: 3, earliest: 1) with { Timing = timing };
        PlanningMobility jump = new(0, 6, MovementStageKind.Return, 7419, 10, null, 0, 0.2f, 0, 0);
        var walking = JointMovementSearch.Search(Context(map, 0, [action]), Exhaustive);
        Check(walking.DeliveryTimes[0] == 4 && walking.Stages.All(s => s.Kind != MovementStageKind.Walk),
            "Abilities-only return cannot create an elective walk for an unobserved future haste gain.");
        var context = Context(map, 0, [action], [jump], 1);
        var returned = JointMovementSearch.Search(context, Exhaustive);
        Check(returned.DeliveryTimes[0] == 2 && returned.Stages.Any(s => s.Kind == MovementStageKind.Return)
            && MovementPlanValidation.Validate(returned, context, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "A legal native return and its lock are compared with walking in the same current-action plan.");
        var lostTiming = Context(map, 0, [action with { Timing = baseline }], [jump], 1);
        Check(!MovementPlanValidation.Validate(returned, lostTiming, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "Losing paired cast evidence invalidates the faster promise even when the broad action horizon still fits.");
        var declined = JointMovementSearch.Search(Context(map, 0, [action], [], 1), Exhaustive);
        Check(declined.DeliveryTimes[0] == 4 && declined.RemainingResources == 1,
            "An unavailable return removes that candidate while preserving the legal current spell.");
        var lateAction = action with { Earliest = 6, Timing = timing with { FieldEntryByAbility = false } };
        var conserving = JointMovementSearch.Search(Context(map, 0, [lateAction], [jump], 1), Exhaustive);
        Check(conserving.DeliveryTimes[0] == 7 && conserving.RemainingResources == 1
            && conserving.Stages.Any(s => s.Kind == MovementStageKind.Walk),
            "Walking to the same useful cast before readiness preserves a charge when mobility delivers no earlier action.");
        var instant = action with { CastSeconds = 0, Timing = timing with { ProcUntil = 10 } };
        var proc = JointMovementSearch.Search(Context(map, 0, [instant], [jump], 1), Exhaustive);
        Check(proc.DeliveryTimes[0] == 1 && proc.RecoveryTimes[0] == 3 && proc.RemainingResources == 0,
            "An instant spell can gain evidenced GCD recovery from a return completed before its ready time.");
        var unchangedRecast = instant with { Timing = instant.Timing! with { ObservedFieldRecast = 3 } };
        var noGain = JointMovementSearch.Search(Context(map, 0, [unchangedRecast], [jump], 1), Exhaustive);
        Check(noGain.DeliveryTimes[0] == 1 && noGain.RemainingResources == 1,
            "An instant spell with no evidenced recast gain preserves the otherwise unnecessary charge.");
        var instantObserver = new NativeLeyLinesTiming();
        var instantOutside = new PlanningCastTiming(0, 0, float.MaxValue, NativeRecast: 3);
        var instantInside = instantOutside with { HasteCenter = default(WPos), HasteRadius = 3, HasteUntil = 20, NativeRecast = 2 };
        instantObserver.Observe(conditions, field, now, outside, false, 101, 0, instantOutside, true, 3);
        instantObserver.Observe(conditions, field, now, default, true, 101, 0, instantInside, true, 2);
        var instantPair = instantObserver.Observe(conditions, field, now, outside, false, 101, 0, instantOutside, true, 3);
        Check(instantPair is { ObservedFieldCast: 0, ObservedFieldRecast: 2 } && instantPair.RecastAt(default, 1) == 2,
            "Naturally instant spells can calibrate their own recast improvement without an unrelated hardcast sample.");
        var naturalInstant = Action(map, 12, 0, new ulong[7], Enumerable.Repeat(1f, 7).ToArray(), 0, 1)
            with { Timing = instantPair with { HasteCenter = center, HasteRadius = 0.4f } };
        var instantContext = Context(map, 0, [naturalInstant], [jump], 1);
        var naturalPlan = JointMovementSearch.Search(instantContext, Exhaustive);
        var noResourcePrune = JointMovementSearch.Search(instantContext, Exhaustive, pruneResourceInferiorMobility: false);
        Check(naturalPlan.DeliveryTimes[0] == 1 && naturalPlan.RecoveryTimes[0] == 3 && naturalPlan.RemainingResources == 0
            && naturalPlan.RecoveryTimes.SequenceEqual(noResourcePrune.RecoveryTimes),
            "Resource pruning cannot mistake an instant delivered GCD for the complete execution/recovery ceiling.");
        var rawInside = instantInside with { UnbuffedRecastUpperBound = 3 };
        Check(rawInside.RecastAt(default, 1) == 2 && rawInside.RecastAt(outside, 1) == 3 && rawInside.RecastAt(default, 20) == 3,
            "Losing unpaired field haste does not retain a fast GCD recast beyond the observed pose or expiry.");
        var unknownOutside = rawInside with { OutsideTimingKnown = false };
        Check(float.IsPositiveInfinity(unknownOutside.Seconds(2, outside, 1))
            && float.IsPositiveInfinity(unknownOutside.RecastAt(outside, 1)),
            "A native inside-only observation cannot prove outside timing from data-sheet values under other live modifiers.");
        var unknownTimingAction = Action(map, 12, 0, new ulong[7], Enumerable.Repeat(1f, 7).ToArray(), 0)
            with { Timing = unknownOutside with { HasteCenter = Point(map, 0) } };
        var unknownSearch = JointMovementSearch.Search(Context(map, 0, [unknownTimingAction]), Exhaustive);
        Check(unknownSearch.DeliveredActions == 1 && unknownSearch.Bound == MovementSearchBound.ActionTimingEvidence,
            "A supported current spell stays executable while diagnostics disclose incomplete timing alternatives.");
        var slowTiming = timing with { HasteCenter = Point(map, 2), ObservedFieldCast = 2.8f, NativeRecast = 3,
            ObservedFieldRecast = 2.9f, FieldEntryByAbility = false };
        var elective = action with { Earliest = 0, Timing = slowTiming };
        var noLostCycle = JointMovementSearch.Search(Context(map, 0, [elective]), Exhaustive);
        Check(noLostCycle.DeliveryTimes[0] == 3 && noLostCycle.Stages.All(s => s.Kind != MovementStageKind.Walk),
            "A weak haste gain cannot pay for delaying a safely available current cast and GCD cycle.");
        var entry = returned.Stages.Single(s => s.Kind == MovementStageKind.Return);
        Check(PlanningCastTiming.EntryAfter(false, entry, context.Actions)
            && !PlanningCastTiming.EntryAfter(true, entry with { To = context.Origin }, context.Actions)
            && !PlanningCastTiming.EntryAfter(true, entry with { EndSeconds = 10 }, context.Actions),
            "An ability-entry label is retained only while its exact landing remains in the live field.");
        var unsafeMap = Grid(7, 1);
        unsafeMap.PixelMaxG[6] = -1;
        var safeFallback = JointMovementSearch.Search(Context(unsafeMap, 0, [action], [jump], 1), Exhaustive);
        Check(safeFallback.DeliveryTimes[0] == 4 && safeFallback.RemainingResources == 1,
            "A cast-speed benefit cannot purchase a hazardous return landing.");
        LeyLinesOwnership(now);
    }

    private static void LeyLinesOwnership(DateTime now)
    {
        var world = (WorldState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        var actors = new ActorState();
        typeof(WorldState).GetField(nameof(WorldState.Actors))!.SetValue(world, actors);
        world.Frame.Timestamp = now;
        Actor MakeActor(ulong id, uint oid = 0)
        {
            var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            actor.InstanceID = id;
            actor.OID = oid;
            actor.Class = Class.BLM;
            actor.Statuses = new ActorStatus[Actor.NumStatuses];
            return actor;
        }
        var player = MakeActor(1);
        player.Statuses[0] = new(737, 0, now.AddSeconds(20), 1);
        var ground = MakeActor(10, 0x179);
        ground.OwnerID = 1;
        actors.Actors.Add(1, player);
        actors.Actors.Add(10, ground);
        Check(NativeLeyLines.Capture(world, player) is { Object: 10, Owner: 1, ExpiresAt: var end } && end == now.AddSeconds(20),
            "Owned native object and status provide one shared exact Ley Lines anchor and original expiry.");
        ground.OwnerID = 2;
        Check(NativeLeyLines.Capture(world, player) == null, "Another player's circle cannot become a return anchor.");
        ground.OwnerID = 1;
        var duplicate = MakeActor(11, 0x179);
        duplicate.OwnerID = 1;
        actors.Actors.Add(11, duplicate);
        Check(NativeLeyLines.Capture(world, player) == null, "Ambiguous owned objects cannot prove a personal field footprint.");
        actors.Actors.Remove(11);
        ground.PosRot = new(float.NaN, 0, 0, 0);
        Check(NativeLeyLines.Capture(world, player) == null, "Invalid native coordinates cannot prove a return landing.");
        ground.PosRot = default;
        player.Statuses[0] = player.Statuses[0] with { ExpireAt = now };
        Check(NativeLeyLines.Capture(world, player) == null, "A ground object alone cannot renew an expired personal field.");
    }
}
