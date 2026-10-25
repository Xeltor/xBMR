using BossMod.CombatAssistance.Config;
using CSActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
using CSActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace BossMod.CombatAssistance.Planning;

internal sealed record AvailableMobility(ImmutableArray<MobilityMechanics> Actions, ulong Resources, ImmutableArray<PlanningUtility> Utilities = default, ImmutableArray<PlanningRecharge> Recharges = default, bool CooldownEvidenceLimited = false,
    PlanningPersonalField? PersonalField = null, ImmutableArray<PreparationGrant> PreparationGrants = default, ImmutableArray<PreparationCost> PreparationCosts = default, bool PreparationAvailable = true)
{
    internal static readonly AvailableMobility None = new([], 0, [], []);
}

// Mechanics are deliberately separate from the user's "gap closer" label. A legal
// action can be used for escape, recovery or coverage; the joint search decides why.
internal static class NativeMobilityCapture
{
    internal sealed record Definition(uint Id, MobilityMechanic Mechanic, float Distance = 0, uint ReturnObject = 0, bool Phantom = false, bool Rotational = false);
    internal static readonly ImmutableArray<Definition> Definitions =
    [
        new(16461, MobilityMechanic.Target), new(7386, MobilityMechanic.Target),
        new(36926, MobilityMechanic.Target), new(36934, MobilityMechanic.Target),
        new(25762, MobilityMechanic.Target), new(36951, MobilityMechanic.Target),
        new(94, MobilityMechanic.Directional, -15), new(2262, MobilityMechanic.Ground),
        new(7492, MobilityMechanic.Target), new(7493, MobilityMechanic.TargetBackstep, -10),
        new(112, MobilityMechanic.TargetBackstep, -10), new(7506, MobilityMechanic.Target),
        new(7515, MobilityMechanic.TargetBackstep, -15), new(24295, MobilityMechanic.Target),
        new(34646, MobilityMechanic.Target), new(16010, MobilityMechanic.Directional, 10),
        new(34684, MobilityMechanic.Directional, 15), new(37008, MobilityMechanic.Directional, 15),
        new(155, MobilityMechanic.Target), new(7419, MobilityMechanic.Return, ReturnObject: 0x179),
        new(24401, MobilityMechanic.Directional, 15), new(24402, MobilityMechanic.Directional, -15),
        new(24403, MobilityMechanic.Return, ReturnObject: 0x4c3), new(44893, MobilityMechanic.Target),
        new(41595, MobilityMechanic.FixedTowardTarget, 15, Phantom: true),
        new(41600, MobilityMechanic.Directional, 15, Phantom: true),
        new(49078, MobilityMechanic.FixedTowardGround, 10, Phantom: true),
        new(46604, MobilityMechanic.Target, Phantom: true)
    ];

    internal static IEnumerable<Definition> AllDefinitions => Definitions.Concat(NativeRotationalMobility.Definitions);

    internal static unsafe AvailableMobility Capture(WorldState world, AIHints hints, Actor player, ActionManagerEx manager,
        AssistancePreferences settings, ImmutableHashSet<uint> enabledActions, bool utilitySelectionOwned = false,
        ImmutableHashSet<uint>? ownedMobility = null, PlanningTravel? travel = null, PlanningPersonalField? personalField = null,
        bool fieldActionMayWeave = false, uint immunityAction = 0, ImmutableArray<PlanningAction> setupActions = default)
    {
        if (!Service.Framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Mobility capture requires the framework thread.");
        var native = CSActionManager.Instance();
        if (native == null || player.PendingKnockbacks.Count != 0
            || player.Statuses.Any(s => s.ID is 1186 or 1317 && s.ExpireAt > world.CurrentTime))
            return AvailableMobility.None;
        var mudra = player.FindStatus((uint)NIN.SID.Mudra) is { } activeMudra && activeMudra.ExpireAt > world.CurrentTime;
        var actions = ImmutableArray.CreateBuilder<MobilityMechanics>();
        var utilities = ImmutableArray.CreateBuilder<PlanningUtility>();
        var grants = ImmutableArray.CreateBuilder<PreparationGrant>();
        var costs = ImmutableArray.CreateBuilder<PreparationCost>();
        var preparationAvailable = true;
        var resources = new Dictionary<int, (int Slot, uint Charges)>();
        var hostileAnchors = hints.PriorityTargetsSpan.ToArray().Select(t => t.Actor.InstanceID).ToHashSet();
        var recharges = new Dictionary<int, PlanningRecharge>();
        var cooldownEvidenceLimited = false;
        // Gyoten and Yaten consume the same gauge as well as their distinct cooldowns.
        // One unit represents ten Kenki; remainder cannot buy another movement action.
        if (player.Class == Class.SAM)
            resources.Add(-1, (0, (uint)(world.Client.GetGauge<FFXIVClientStructs.FFXIV.Client.Game.Gauge.SamuraiGauge>().Kenki / 10)));
        for (var index = 0; index < (setupActions.IsDefault ? 0 : setupActions.Length); ++index)
        {
            var setup = setupActions[index];
            if (setup.Preparation == PlanningPreparationMode.None || setup.Id is not (2259 or 7389 or 3555)) continue;
            var definition = ActionDefinitions.Instance[new(ActionType.Spell, setup.Id)];
            if (definition == null || !ObserveResource(definition, native->GetCurrentCharges(setup.Id),
                out var resource, out _, out var extra, sharedGcd: definition.IsGCD))
            { preparationAvailable = false; break; }
            costs.Add(new(index, (1ul << (resource.Slot * 4)) | extra));
        }
        foreach (var entry in AllDefinitions)
        {
            if (hints.ForbidDashes && (!entry.Rotational || entry.Mechanic != MobilityMechanic.Target) || !EnabledBySettings(entry, settings, player.Class)
                || mudra && !NativeRotationalPreparation.MayFinishMudra(player.Class, entry.Id, setupActions,
                    player.InstanceID, hints.ForcedTarget?.InstanceID ?? player.TargetID)
                || !enabledActions.Contains(entry.Id) || ownedMobility?.Contains(entry.Id) != true
                || entry.Phantom && !NativePhantomMobility.Available(entry.Id, world.CurrentZone, player, world.CurrentTime))
                continue;
            var id = new ActionID(ActionType.Spell, entry.Id);
            var definition = ActionDefinitions.Instance[id];
            var nativeStatus = native->GetActionStatus(CSActionType.Action, entry.Id, checkRecastActive: false, checkCastingActive: false);
            var setupIndex = NativeRotationalPreparation.PreparationIndex(player.Class, entry.Id, setupActions,
                player.InstanceID, hints.ForcedTarget?.InstanceID ?? player.TargetID, nativeStatus == 0, mudra);
            bool HasProc(uint status) => player.FindStatus(status) is { } observed && observed.ExpireAt > world.CurrentTime;
            var missingProc = setupIndex >= 0 && (entry.Id switch
            {
                25777 => !HasProc(2690),
                25753 => !HasProc(2624),
                25835 => !HasProc(2724),
                16480 => world.Client.GetGauge<FFXIVClientStructs.FFXIV.Client.Game.Gauge.DragoonGauge>().LotdTimer == 0,
                _ => false
            });
            var castProc = CSActionManager.CastTimeProc.None;
            var castTime = manager.GetAdjustedCastTime(id, true, &castProc);
            if (definition == null || definition.IsGCD && !entry.Rotational || !definition.IsUnlocked(world, player)
                || native->GetAdjustedActionId(entry.Id) != entry.Id
                || castTime != 0
                || nativeStatus != 0 && (setupIndex < 0 || !missingProc))
                continue;
            float Remaining(uint status) => player.FindStatus(status) is { } observed
                ? Math.Max(0, (float)(observed.ExpireAt - world.CurrentTime).TotalSeconds) : 0;
            var availableUntil = setupIndex >= 0 ? float.MaxValue : entry.Id switch
            {
                25777 => Remaining(2690),
                25753 => Remaining(2624),
                25835 => Remaining(2724),
                16480 => world.Client.GetGauge<FFXIVClientStructs.FFXIV.Client.Game.Gauge.DragoonGauge>().LotdTimer * 0.001f,
                _ => float.MaxValue
            };
            var instantCost = 0ul;
            if (manager.GetAdjustedCastTime(id, false) > 0)
            {
                // BLU's casted movement requires the actual instant-cast effect,
                // not just today's observed zero cast time. One Swiftcast cannot
                // support two different movements or an earlier selected spell.
                if (player.Class != Class.BLU || PlanningCastTiming.ProcStatus((byte)castProc) != 167
                    || Remaining(167) <= 0 || !setupActions.IsDefaultOrEmpty && setupActions.Any(a => a.Id != entry.Id
                        && a.CastSeconds == 0 && a.Timing is { WithoutProc: > 0 })) continue;
                if (!resources.TryGetValue(-167, out var instant))
                {
                    if (resources.Count >= 16) continue;
                    resources.Add(-167, instant = (resources.Count, 1));
                }
                instantCost = 1ul << (instant.Slot * 4);
                availableUntil = Math.Min(availableUntil, Remaining(167));
            }
            if (entry.Phantom)
                availableUntil = Math.Min(availableUntil, NativePhantomMobility.Remaining(entry.Id, player, world.CurrentTime));
            if (availableUntil <= 0) continue;
            var charges = native->GetCurrentCharges(entry.Id);
            if (charges > 15 || definition.MainCooldownGroup < 0)
                continue;
            ulong additionalCost = instantCost;
            if (entry.Id is 7492 or 7493)
            {
                if (!resources.TryGetValue(-1, out var kenki) || kenki.Charges == 0)
                    continue;
                additionalCost = 1ul << (kenki.Slot * 4);
            }
            var anchors = ImmutableArray<MobilityAnchor>.Empty;
            if (entry.Mechanic is MobilityMechanic.Target or MobilityMechanic.FixedTowardTarget or MobilityMechanic.TargetBackstep)
            {
                IEnumerable<Actor> eligible = [];
                if ((definition.AllowedTargets & ActionTargets.Hostile) != 0)
                    eligible = eligible.Concat(hints.PriorityTargetsSpan.ToArray().Select(t => t.Actor));
                if ((definition.AllowedTargets & (ActionTargets.Party | ActionTargets.Alliance | ActionTargets.Friendly)) != 0)
                    eligible = eligible.Concat(world.Party.WithoutSlot(excludeAlliance: (definition.AllowedTargets & ActionTargets.Alliance) == 0));
                anchors = eligible.DistinctBy(a => a.InstanceID)
                    .Where(a => a.InstanceID != player.InstanceID && !a.IsDead && !a.IsDestroyed && a.IsTargetable)
                    .Where(a => !hints.ForbidDashes || NoDisplacement(entry, player.Position, player.HitboxRadius, a.Position, a.HitboxRadius))
                    .Where(a => LegalNativeAnchor(entry.Id, a.InstanceID))
                    .Select(a => CaptureAnchor(entry.Id, definition, player, a, hints) is { } anchor
                        ? anchor with { MinimumTravel = AnchorMinimumTravel(settings, entry.Rotational,
                            hostileAnchors.Contains(a.InstanceID)) } : null)
                    .OfType<MobilityAnchor>().ToImmutableArray();
                if (anchors.IsEmpty)
                    continue;
            }
            WPos? returnPosition = null;
            if (entry.Mechanic == MobilityMechanic.Return)
            {
                if (entry.Id == 7419)
                {
                    if (personalField == null) continue;
                    returnPosition = personalField.Center;
                }
                else
                {
                    var returns = world.Actors.Where(a => a.OID == entry.ReturnObject && a.OwnerID == player.InstanceID && !a.IsDestroyed).ToArray();
                    if (returns.Length != 1) continue;
                    returnPosition = returns[0].Position;
                }
            }
            (int Slot, uint Charges) resource;
            float readyIn;
            ulong extraCost;
            if (definition.IsGCD)
            {
                // A currently legal proc/GCD opportunity is consumable once.
                // Its GCD clock is readiness, not a forecast of another charge.
                if (resources.Count >= 16) continue;
                resource = (resources.Count, setupIndex >= 0 ? 0u : 1u);
                resources.Add(-checked((int)entry.Id) - 100, resource);
                readyIn = Math.Max(0, manager.GCD()); extraCost = 0;
            }
            else if (!ObserveResource(definition, charges, out resource, out readyIn, out extraCost)) continue;
            if (setupIndex >= 0)
            {
                ulong granted;
                if (definition.IsGCD) granted = 1ul << (resource.Slot * 4);
                else
                {
                    // A stance/proc grant cannot reset the dive's own observed
                    // cooldown. Keep that clock and use a separate conditional unit.
                    if (resources.Count >= 16) continue;
                    var procSlot = resources.Count;
                    resources.Add(-checked((int)entry.Id) - 100, (procSlot, 0));
                    granted = 1ul << (procSlot * 4);
                    additionalCost |= granted;
                }
                grants.Add(new(setupIndex, granted));
            }
            // Native action definitions provide the observed animation-lock estimate.
            // Full traversal is conservative for teleports until a native transition
            // proves which intervening geometry may be skipped.
            actions.Add(new(entry.Id, entry.Mechanic, definition.Range, entry.Distance, player.HitboxRadius, 0,
                Math.Max(0.1f, definition.InstantAnimLock + manager.AnimationLockDelayEstimate), resource.Slot, anchors,
                returnPosition, readyIn: Math.Max(readyIn, manager.EffectiveAnimationLock),
                additionalResourceCost: additionalCost | extraCost, allowStationary: entry.Rotational,
                isGCD: definition.IsGCD, recastSeconds: definition.IsGCD ? manager.GetAdjustedRecastTime(id) * 0.001f : 0,
                requiredEarlierActions: setupIndex < 0 ? (byte)0 : (byte)(1 << setupIndex), availableUntil: availableUntil));
        }
        var trueNorth = ActionDefinitions.Instance[ActionID.MakeSpell(ClassShared.AID.TrueNorth)];
        // Speed forecasts are supported for the observed ordinary running
        // baseline (including the ninja trait). Slow/heavy/unknown baselines
        // require new evidence; never assume a boost will override a debuff.
        if (!mudra && settings.UseSprint && travel != null)
            foreach (var id in new uint[] { 3, 41646 })
            {
                var definition = ActionDefinitions.Instance[new(ActionType.Spell, id)];
                if (SprintForecast(id, travel) is not { } forecast
                    || !SprintEnabled(id, settings, player.Class) || player.Statuses.Any(s => s.ID is 13 or 14 && s.ExpireAt > world.CurrentTime)
                    || id == 41646 && !NativePhantomMobility.Available(id, world.CurrentZone, player, world.CurrentTime)
                    || resources.Count >= 16 || ownedMobility?.Contains(id) != true || !enabledActions.Contains(id)
                    || definition == null || !definition.IsUnlocked(world, player) || native->GetAdjustedActionId(id) != id
                    || native->GetCurrentCharges(id) > 15
                    || native->GetActionStatus(CSActionType.Action, id, checkRecastActive: false, checkCastingActive: false) != 0)
                    continue;
                if (!ObserveResource(definition, native->GetCurrentCharges(id), out var resource, out var readyIn, out var extraCost)) continue;
                var duration = id == 41646 ? Math.Min(forecast.Duration, NativePhantomMobility.Remaining(id, player, world.CurrentTime)) : forecast.Duration;
                utilities.Add(new(id, PlanningUtilityEffect.MovementSpeed, duration,
                    Math.Max(0.1f, definition.InstantAnimLock + manager.AnimationLockDelayEstimate), Math.Max(readyIn, manager.EffectiveAnimationLock),
                    resource.Slot, forecast.Speed, extraCost));
            }
        if (!mudra && utilitySelectionOwned && settings.ManageTrueNorth && settings.ManagePositionals && enabledActions.Contains((uint)ClassShared.AID.TrueNorth)
            && trueNorth != null && trueNorth.IsUnlocked(world, player) && resources.Count < 16
            && !player.Statuses.Any(s => s.ID is 496 or 1186 or 1317 && s.ExpireAt > world.CurrentTime)
            && native->GetActionStatus(CSActionType.Action, (uint)ClassShared.AID.TrueNorth, checkRecastActive: false, checkCastingActive: false) == 0
            && native->GetCurrentCharges((uint)ClassShared.AID.TrueNorth) <= 15
            && ObserveResource(trueNorth, native->GetCurrentCharges((uint)ClassShared.AID.TrueNorth), out var northResource, out var northReady, out var northExtra))
        {
            utilities.Add(new((uint)ClassShared.AID.TrueNorth, PlanningUtilityEffect.IgnorePositional, 10,
                Math.Max(0.1f, trueNorth.InstantAnimLock + manager.AnimationLockDelayEstimate),
                Math.Max(northReady, manager.EffectiveAnimationLock), northResource.Slot, AdditionalResourceCost: northExtra));
        }
        const uint retraceId = 36988;
        var retrace = ActionDefinitions.Instance[new(ActionType.Spell, retraceId)];
        if (!mudra && fieldActionMayWeave && RetraceEnabled(settings, player.Class) && personalField != null && ownedMobility?.Contains(retraceId) == true
            && enabledActions.Contains(retraceId) && retrace != null && retrace.IsUnlocked(world, player)
            && resources.Count < 16 && native->GetAdjustedActionId(retraceId) == retraceId
            && native->GetActionStatus(CSActionType.Action, retraceId, checkRecastActive: false, checkCastingActive: false) == 0
            && native->GetCurrentCharges(retraceId) <= 15
            && ObserveResource(retrace, native->GetCurrentCharges(retraceId), out var retraceResource, out var retraceReady, out var retraceExtra))
            // Duration is the action family's maximum field lifetime, never a
            // renewed effect. Relocation uses Field.EndsIn as its absolute bound.
            utilities.Add(new(retraceId, PlanningUtilityEffect.RelocatePersonalField, 20,
                Math.Max(0.1f, retrace.InstantAnimLock + manager.AnimationLockDelayEstimate),
                Math.Max(retraceReady, manager.EffectiveAnimationLock), retraceResource.Slot,
                AdditionalResourceCost: retraceExtra, Field: personalField));
        if (!mudra && settings.ManageKnockback && immunityAction is 7548 or 7559 && enabledActions.Contains(immunityAction)
            && ActionDefinitions.Instance[new(ActionType.Spell, immunityAction)] is { IsGCD: false } immunity
            && immunity.IsUnlocked(world, player) && resources.Count < 16
            && native->GetAdjustedActionId(immunityAction) == immunityAction
            && native->GetActionStatus(CSActionType.Action, immunityAction, checkRecastActive: false, checkCastingActive: false) == 0
            && native->GetCurrentCharges(immunityAction) <= 15
            && ObserveResource(immunity, native->GetCurrentCharges(immunityAction), out var immunityResource, out var immunityReady, out var immunityExtra))
        {
            // This is a proposal using observed native lock/response estimates.
            // Execution cannot rely on it until the real native buff is observed.
            var immunityLock = Math.Max(0.1f, immunity.InstantAnimLock + manager.AnimationLockDelayEstimate);
            utilities.Add(new(immunityAction, PlanningUtilityEffect.KnockbackImmunity, 6, immunityLock,
                Math.Max(immunityReady, manager.EffectiveAnimationLock), immunityResource.Slot,
                AdditionalResourceCost: immunityExtra, ApplicationSeconds: immunityLock));
        }
        ulong packed = 0;
        foreach (var resource in resources.Values)
            packed |= (ulong)resource.Charges << (resource.Slot * 4);
        return new(actions.ToImmutable(), packed, utilities.ToImmutable(), recharges.Values.OrderBy(r => r.Resource).ToImmutableArray(), cooldownEvidenceLimited, personalField, grants.ToImmutable(), costs.ToImmutable(), preparationAvailable);

        bool ObserveResource(ActionDefinition definition, uint charges, out (int Slot, uint Charges) resource, out float ready, out ulong extraCost, bool sharedGcd = false)
        {
            resource = default;
            ready = 0;
            extraCost = 0;
            var group = definition.ActualMainCooldownGroup(world.Client.DutyActions);
            if (group < 0 || group >= world.Client.Cooldowns.Length) return false;
            var cooldown = native->GetRecastGroupDetail(group);
            var capacity = definition.MaxChargesAtLevel(player.Level);
            var interval = cooldown != null && cooldown->Total > 0 ? cooldown->Total / definition.MaxChargesAtCap() : definition.Cooldown;
            if (capacity is < 1 or > 15 || charges > capacity || !float.IsFinite(interval) || interval <= 0) return false;
            // Elapsed contains the charge units already recovered at this level.
            // Use the same live timer as GetCurrentCharges, not a previous world frame.
            var next = charges == capacity ? float.MaxValue : cooldown == null ? float.NaN
                : Math.Max(0, interval * (charges + 1) - cooldown->Elapsed);
            if (!float.IsFinite(next)) return false;
            ready = Math.Max(charges == 0 ? next : 0, definition.ExtraReadyIn(world.Client.Cooldowns));
            if (ready > MovementPlanningContext.MaximumHorizonSeconds) return false;
            if (!resources.TryGetValue(group, out resource))
            {
                if (resources.Count == 16) return false;
                resource = (resources.Count, charges);
                resources.Add(group, resource);
                recharges.Add(group, new(resource.Slot, capacity, interval, next));
            }
            else
            {
                if (!recharges.TryGetValue(group, out var prior)) return false;
                // Conflicting aliases cannot establish a shared recharge schedule.
                if (resource.Charges != charges || prior.Capacity != capacity || prior.Interval != interval
                    || Math.Abs(prior.NextIn - next) > 0.02f) return false;
            }
            var extraGroup = definition.ExtraCooldownGroup;
            if (!sharedGcd && extraGroup >= 0 && extraGroup != group)
            {
                var extra = native->GetRecastGroupDetail(extraGroup);
                if (extra == null || !float.IsFinite(extra->Total) || !float.IsFinite(extra->Elapsed)) return false;
                var extraReady = Math.Max(0, extra->Total - extra->Elapsed);
                var extraCharges = extraReady > 0 ? 0u : 1u;
                ready = Math.Max(ready, extraReady);
                if (ready > MovementPlanningContext.MaximumHorizonSeconds) return false;
                if (!resources.TryGetValue(extraGroup, out var extraResource))
                {
                    if (resources.Count == 16) return false;
                    resources.Add(extraGroup, extraResource = (resources.Count, extraCharges));
                    if (extra->Total > 0)
                        recharges.Add(extraGroup, new(extraResource.Slot, 1, extra->Total,
                            extraCharges == 1 ? float.MaxValue : extraReady));
                    else
                        // The first use is legal, but no observed repeat timer exists
                        // yet. Do not promise an invented second use in this plan.
                        cooldownEvidenceLimited = true;
                }
                else if (extraResource.Charges != extraCharges
                    || recharges.TryGetValue(extraGroup, out var clock) && (clock.Capacity != 1 || clock.Interval != extra->Total))
                    return false;
                extraCost = 1ul << (extraResource.Slot * 4);
            }
            return true;
        }
    }

    internal static bool EnabledBySettings(Definition entry, AssistancePreferences settings, Class job)
        => settings.UseGapCloser && (entry.Phantom ? settings.UsePhantomGapClosers && settings.IsPhantomGapCloserJobEnabled((uint)job)
            : settings.IsGapCloserJobEnabled((uint)job))
            && (entry.Id != 7419 || settings.ManageLeylines && settings.UseBetweenTheLines);

    internal static (float Speed, float Duration)? SprintForecast(uint id, PlanningTravel travel)
    {
        // Preserve the observed running baseline. Loading during a speed buff
        // cannot establish the post-buff speed, so it cannot promise a new boost.
        if (id is not (3 or 41646) || !travel.Valid || travel.Baseline is < 5.99f or > 6.61f) return null;
        return (Math.Min(id == 41646 ? 12 : 7.8f, travel.Baseline * (id == 41646 ? 2 : 1.3f)), 10);
    }

    internal static bool SprintEnabled(uint id, AssistancePreferences settings, Class job)
        => settings.UseSprint && (id == 3 || id == 41646 && settings.UsePhantomGapClosers && settings.IsPhantomGapCloserJobEnabled((uint)job));

    internal static bool RetraceEnabled(AssistancePreferences settings, Class job)
        => job == Class.BLM && settings.ManageLeylines && settings.UseRetrace;

    internal static bool NoDisplacement(Definition definition, WPos origin, float playerRadius, WPos target, float targetRadius)
        => definition.Mechanic == MobilityMechanic.Target && playerRadius >= 0 && targetRadius >= 0
            && (target - origin).Length() <= Math.Max(0, playerRadius + targetRadius - 0.05f);

    private static unsafe bool LegalNativeAnchor(uint actionId, ulong id)
    {
        var wrapper = Service.ObjectTable.SearchById(id);
        return wrapper != null && wrapper.IsValid() && wrapper.Address != 0 && wrapper.GameObjectId == id
            && CSActionManager.CanUseActionOnTarget(actionId, (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)wrapper.Address);
    }

    // XCAI's uptime, ground and directional checks passed a zero minimum. Its
    // configured distance belonged to optional friendly escape anchors, not a
    // blanket rejection before combat execution could be compared with walking.
    internal static float AnchorMinimumTravel(AssistancePreferences settings, bool rotational, bool hostile)
        => rotational || hostile ? 0 : settings.MinimumGapCloserDistance;

    private static MobilityAnchor? CaptureAnchor(uint id, ActionDefinition definition, Actor player, Actor actor, AIHints hints)
    {
        if ((definition.AllowedTargets & ActionTargets.Hostile) == 0)
            return new(actor.InstanceID, actor.Position, actor.HitboxRadius);
        if (!Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(id, out var row))
            return null;
        if (row.EffectRange == 0)
            return new(actor.InstanceID, actor.Position, actor.HitboxRadius);
        var shape = row.CastType switch
        {
            2 => ExecutionShape.TargetCircle,
            3 => ExecutionShape.Cone,
            4 when row.XAxisModifier > 0 => ExecutionShape.Rectangle,
            _ => (ExecutionShape?)null
        };
        if (shape == null)
            return null; // Unknown damaging geometry cannot grant permission to hit a protected target.
        var height = shape == ExecutionShape.TargetCircle ? actor.PosRot.Y : player.PosRot.Y;
        var effect = new ExecutionOpportunity(shape.Value, actor.InstanceID, actor.Position,
            definition.Range + player.HitboxRadius + actor.HitboxRadius, row.EffectRange,
            [new(actor.InstanceID, actor.Position, actor.HitboxRadius, 1, true, actor.PosRot.Y - height)],
            row.XAxisModifier * 0.5f, 60.Degrees(), forbiddenRecipients: hints.ForbiddenTargetsSpan.ToArray()
                .Select(t => new ExecutionRecipient(t.Actor.InstanceID, t.Actor.Position, t.Actor.HitboxRadius, 0,
                    HeightOffset: t.Actor.PosRot.Y - height)));
        return new(actor.InstanceID, actor.Position, actor.HitboxRadius, effect);
    }
}
