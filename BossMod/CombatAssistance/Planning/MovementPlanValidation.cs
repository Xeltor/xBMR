using System.Diagnostics;

namespace BossMod.CombatAssistance.Planning;

// Final validation uses a NEW native snapshot, with deadlines relative to that snapshot.
// The host still owns activation, manual handoff and actual action submission.
internal static class MovementPlanValidation
{
    internal const float ArrivalTolerance = 0.1f;
    internal sealed record Result(bool Allowed, string Reason, float DepartureSeconds = 0);

    internal static Result Validate(MovementPlan plan, MovementPlanningContext current, int firstStage,
        bool hasAuthority, long? timestamp = null, WPos? actualPosition = null)
    {
        if (!hasAuthority)
            return new(false, "manual or external movement authority");
        if (!plan.HasVerifiedRoute || plan.Input.ActorId != current.Identity.ActorId || plan.Input.ZoneId != current.Identity.ZoneId
            || plan.Input.ContentId != current.Identity.ContentId || plan.Input.ProjectionLayer != current.Identity.ProjectionLayer
            || plan.TargetId != current.TargetId && !plan.Stages.Any(s => s.Kind == MovementStageKind.Target
                && s.AnchorId == current.TargetId && current.ActionFor(s) != null))
            return new(false, "invalid plan or changed player, territory or projection");
        if (NativeMovementHost.RetiredContact(plan.PartyContact, current.PartyContact))
            return new(false, "party contact objective retired or changed owner");
        var elapsed = ((timestamp ?? Stopwatch.GetTimestamp()) - plan.Input.CapturedTimestamp) / (double)Stopwatch.Frequency;
        var worldElapsed = (current.Identity.WorldTime - plan.Input.WorldTime).TotalSeconds;
        if (elapsed is < 0 or >= 0.25 || worldElapsed is < 0 or >= 0.25)
            return new(false, "stale planning input");
        if (firstStage < 0 || firstStage > plan.Stages.Length)
            return new(false, "invalid execution stage");
        if (current.ForcedMovement != null && !plan.Stages.Skip(firstStage).Any(s => s.Kind == MovementStageKind.ForcedMovement))
            return new(false, "a compulsory native movement replaced the walking plan");
        if (current.StationaryOnly && plan.Stages.Skip(firstStage).Any(stage => stage.From != stage.To
            || stage.Kind is not (MovementStageKind.Wait or MovementStageKind.Cast or MovementStageKind.Target)))
            return new(false, "stationary target permission cannot execute movement or utility stages");
        var remainingPushes = plan.Stages.Skip(firstStage).Where(stage => stage.Kind == MovementStageKind.Knockback).ToArray();
        var livePushes = current.KnockbackPhases?.Phases ?? [];
        if (remainingPushes.Length != 0 && current.KnockbackPhases?.MatchesInventory(current.Identity, current.ForcedMovement) != true)
            return new(false, "knockback preparation lacks a fresh complete matching authored/learned inventory");
        if (remainingPushes.Length != livePushes.Length
            || remainingPushes.Where((stage, index) => stage.Knockback?.SameBinding(livePushes[index]) != true).Any())
            return new(false, "native knockback source, timing or complete phase sequence changed");
        if (current.KnockbackPhases is { } sourceInventory && remainingPushes.Length == 0
            && current.ForcedMovement?.Routes.Any(route => route.Knockback != null) != true
            && NativeKnockbackAuthorityCapture.Capture(sourceInventory.Inventory) != null)
            return new(false, "an unresolved native displacement replaced the ordinary route");
        var phaseIndex = 0;
        var previousWait = 0f;
        float? firstDeparture = null;
        var position = actualPosition ?? current.Origin;
        var time = current.UnlockSeconds;
        var inputAge = (float)Math.Max(elapsed, worldElapsed);
        var actionReadyAt = 0f;
        var immunityAppliedBy = float.MaxValue;
        var immunityUntil = 0f;
        var immunityPossibleFrom = float.MaxValue;
        var immunityPossibleUntil = 0f;
        uint immunityActionId = 0;
        var comboReady = Math.Max(0, plan.ComboReadyAt - inputAge);
        var fieldEntry = false;
        var fieldBenefitAllowed = true;
        PlanningPersonalField? relocatedField = null;
        var restrictFieldEntry = current.Actions.Any(a => a.Timing?.FieldEntryByAbility == true);
        foreach (var stage in plan.Stages.Take(firstStage))
        {
            if (stage.Utility is { Effect: PlanningUtilityEffect.KnockbackImmunity })
                return new(false, "immunity submission is not native buff confirmation; replan from observed native status");
            if (float.IsFinite(stage.ActionLockUntil))
                actionReadyAt = Math.Max(actionReadyAt, Math.Max(0, stage.ActionLockUntil - inputAge));
            fieldBenefitAllowed = PlanningCastTiming.BenefitAfter(fieldBenefitAllowed, stage, current.Actions);
            if (stage.Kind == MovementStageKind.UtilityAction && stage.ActionId == 36988 && stage.Field is { } moved)
            {
                if (current.PersonalField is not { } observed || !observed.SameIdentity(moved)
                    || !observed.Center.AlmostEqual(stage.To, ArrivalTolerance))
                    return new(false, "observed personal field relocation not yet corroborated");
                relocatedField = observed with { EndsIn = Math.Min(observed.EndsIn, Math.Max(0, moved.EndsIn - inputAge)) };
            }
            if (restrictFieldEntry)
                fieldEntry = PlanningCastTiming.EntryAfter(fieldEntry,
                    stage with { EndSeconds = Math.Max(0, stage.EndSeconds - inputAge) }, current.Actions, relocatedField);
        }
        var slack = float.MaxValue;
        if (!current.Traverse(position, position, 0, time, out var lockSlack))
            return new(false, "current lock no longer clears native deadline");
        slack = Math.Min(slack, lockSlack);
        var resources = PlanningResourceState.Initial(current.Resources, current.Recharges);
        var positionalBypassUntil = current.PositionalBypassUntil;
        var travel = current.Travel;
        float? departure = null;
        for (var index = firstStage; index < plan.Stages.Length; ++index)
        {
            var stage = plan.Stages[index];
            if (stage.From != stage.To && stage.Kind != MovementStageKind.Knockback) departure ??= time;
            float duration;
            var landingLock = 0f;
            var executableFacing = stage.Facing;
            var stationary = stage.Kind is MovementStageKind.Wait or MovementStageKind.Cast or MovementStageKind.UtilityAction or MovementStageKind.Target or MovementStageKind.FieldPlacement;
            if (stationary && !position.AlmostEqual(stage.To, ArrivalTolerance))
                return new(false, "stationary action position changed");
            if (stage.ActionId != 0 && stage.Kind != MovementStageKind.Target && !current.CanChooseStationaryAction(position))
                return new(false, "authored teleporter may interrupt the selected action");
            // Arrival tolerance is not a teleport to the intended point. Evaluate an
            // action/wait and the onward route from the actual stopped position.
            var landing = stationary ? position : stage.To;
            var teleported = false;
            if (stage.ActionId != 0 && stage.Kind != MovementStageKind.Target && actionReadyAt > time)
            {
                if (!current.Traverse(position, position, time, actionReadyAt - time, out var animationSlack))
                    return new(false, "waiting for the preceding animation lock no longer safe");
                slack = Math.Min(slack, animationSlack);
                time = actionReadyAt;
            }
            switch (stage.Kind)
            {
                case MovementStageKind.Knockback:
                    var push = livePushes[phaseIndex++];
                    var nextFrame = current.WithDisplacementMap(push.After);
                    if (stage.PreventedByActionId != 0)
                    {
                        if (stage.PreventedByActionId != immunityActionId || stage.From != stage.To
                            || !position.AlmostEqual(stage.From, ArrivalTolerance)
                            || !push.TryPrevent(current, nextFrame, position, time, immunityAppliedBy, immunityUntil, out var immunitySlack, immunityActionId))
                            return new(false, "planned immunity no longer covers the exact native source window or position");
                        duration = 0;
                        landing = position;
                        slack = Math.Min(slack, immunitySlack);
                        current = nextFrame;
                        teleported = true; // Native position is unchanged; only this preparation constraint was removed.
                        break;
                    }
                    if (current.KnockbackPhases?.PlanLandings == false || push.MayBePrevented(current, immunityPossibleFrom, immunityPossibleUntil)
                        || !position.AlmostEqual(stage.From, ArrivalTolerance)
                        || !push.TryTransition(current, nextFrame, position, time, out var projected,
                            out var onset, out var completed, out var pushSlack)
                        || !projected.AlmostEqual(stage.To, ArrivalTolerance))
                        return new(false, "executable native knockback launch, corridor or landing changed");
                    firstDeparture ??= departure == null ? onset : departure + Math.Min(slack, previousWait + pushSlack);
                    landing = projected;
                    duration = completed - time;
                    slack = Math.Min(slack, previousWait + pushSlack);
                    current = nextFrame;
                    teleported = true; // Native swept corridor/window were validated above.
                    break;
                case MovementStageKind.ForcedMovement:
                    var forced = stage.Forced == null ? null : current.ForcedMovement?.Routes.FirstOrDefault(route =>
                        route.Source == stage.Forced.Source && (route.Active || route.Heading == stage.Forced.Heading)
                        && route.Landing.AlmostEqual(stage.To, ArrivalTolerance)
                        && Math.Abs(route.End - Math.Max(0, stage.EndSeconds - (float)worldElapsed)) < 0.01f);
                    if (forced == null || index != firstStage || !forced.Evaluate(current, out var forcedSlack))
                        return new(false, "compulsory native projection, facing or complete route changed");
                    duration = forced.End;
                    time = 0;
                    slack = Math.Min(slack, forcedSlack);
                    teleported = true; // Each native leg was checked above; never substitute a direct chord.
                    break;
                case MovementStageKind.Teleport:
                    var transit = stage.Transit == null ? null : current.Teleports.FirstOrDefault(t => t.SameGeometry(stage.Transit)
                        && t.NotBefore <= Math.Max(0, stage.Transit.NotBefore - (float)worldElapsed) + 0.01f);
                    if (transit == null || stage.To != transit.Exit
                        || !transit.Evaluate(current, position, travel, time, out var arrival, out var transitSlack))
                        return new(false, "authored teleporter, approach, entrance wait or landing changed");
                    duration = arrival - time;
                    slack = Math.Min(slack, transitSlack);
                    teleported = true;
                    break;
                case MovementStageKind.Walk:
                    duration = travel.Seconds((stage.To - position).Length(), time);
                    break;
                case MovementStageKind.Target:
                    if (current.TargetConfirmed(stage, current.TargetId)) duration = 0;
                    else if (current.TargetSetup(stage with { From = position }) != null)
                        duration = Math.Max(0, stage.EndSeconds - inputAge - time);
                    else return new(false, "target setup action, eligibility or captured timing changed");
                    if (stage.From != stage.To) return new(false, "target setup cannot imply movement");
                    break;
                case MovementStageKind.Wait:
                    // A plan's old wait/departure deadline is never renewed by validation.
                    duration = Math.Max(0, stage.EndSeconds - (float)Math.Max(elapsed, worldElapsed) - time);
                    break;
                case MovementStageKind.UtilityAction:
                    var utility = current.Utilities.FirstOrDefault(u => u.ActionId == stage.ActionId);
                    if (utility == null || !resources.Ready(utility.ResourceCost, Math.Max(time, utility.ReadyIn), current.Recharges, out var ready)
                        || (utility.Effect == PlanningUtilityEffect.KnockbackImmunity
                            ? stage.Utility == null || !utility.Same(stage.Utility, inputAge) : stage.Utility != null)
                        || current.RejectedMobility.Any(r => r.ActionId == utility.ActionId && r.From.AlmostEqual(position, ArrivalTolerance)))
                        return new(false, "utility action, charges or execution permission changed");
                    if (!current.Traverse(position, position, time, ready - time, out var utilitySlack))
                        return new(false, "utility action preparation no longer safe");
                    slack = Math.Min(slack, utilitySlack);
                    time = ready;
                    resources = resources.Spend(utility.ResourceCost, time, current.Recharges);
                    if (utility.Effect == PlanningUtilityEffect.IgnorePositional)
                        positionalBypassUntil = Math.Max(positionalBypassUntil, time + utility.Duration);
                    else if (utility.Effect == PlanningUtilityEffect.MovementSpeed)
                        travel = new(travel.Baseline, utility.Speed, time + utility.Duration);
                    else if (utility.Effect == PlanningUtilityEffect.KnockbackImmunity)
                    {
                        if (!NativeKnockbackPhases.SafePhysicalFallback(current, phaseIndex, position, time + utility.LockSeconds))
                            return new(false, "unconfirmed immunity no longer has its verified native physical fallback");
                        immunityAppliedBy = time + utility.ApplicationSeconds;
                        immunityUntil = time + utility.Duration;
                        immunityActionId = utility.ActionId;
                        immunityPossibleFrom = time;
                        immunityPossibleUntil = time + utility.ApplicationSeconds + utility.Duration;
                    }
                    else if (utility.Effect == PlanningUtilityEffect.RelocatePersonalField)
                    {
                        if (stage.Field is not { } original || utility.Field is not { } available
                            || current.PersonalField is not { } observed || !available.SameIdentity(original) || !observed.SameIdentity(original)
                            || relocatedField == null && observed.Center != original.Center)
                            return new(false, "owned personal field identity or original location changed");
                        relocatedField = original with { Center = position, EndsIn = Math.Min(observed.EndsIn, Math.Max(0, original.EndsIn - inputAge)) };
                        if (!relocatedField.LiveAt(time + utility.LockSeconds))
                            return new(false, "personal field expires before relocation finishes");
                    }
                    duration = utility.LockSeconds;
                    break;
                case MovementStageKind.FieldPlacement:
                    if (current.FieldPlacement is not { } placement || plan.FieldPlacement is not { } plannedPlacement
                        || placement.Opportunity != plannedPlacement.Opportunity || stage.ActionId != PlanningFieldPlacement.ActionId
                        || stage.AnchorId != 0 || time > placement.Latest)
                        return new(false, "original Ley Lines placement opportunity changed or expired");
                    time = Math.Max(time, placement.Earliest);
                    if (time > placement.Latest) return new(false, "Ley Lines placement no longer fits its original weave");
                    duration = placement.LockSeconds;
                    break;
                case MovementStageKind.Cast:
                    var action = current.ActionFor(stage);
                    var actionStart = action == null ? time : Math.Max(time, action.Earliest);
                    var castSeconds = action?.CastAt(position, actionStart, fieldEntry, relocatedField, fieldBenefitAllowed) ?? 0;
                    var ignoresPositional = action != null && actionStart + castSeconds < positionalBypassUntil;
                    var coverage = action == null ? default : current.Evaluate(action, position, ignoresPositional);
                    if (action == null || !coverage.Allowed)
                        return new(false, "action, target or required recipients changed");
                    executableFacing = coverage.Facing;
                    var evidence = plan.ActionEvidence.FirstOrDefault(a => a.Id == action.Id && a.TargetId == action.TargetId
                        && (stage.SourceActionIndex < 0 || a.SourceActionIndex == stage.SourceActionIndex));
                    if (evidence == null || coverage.Value < evidence.Value || evidence.RequiredRecipientIds.Any(id => !current.Covers(action, position, id, ignoresPositional)))
                        return new(false, "original action value or individual recipient would be lost");
                    // Native cast observations have millisecond resolution. Losing
                    // an instant/field-speed condition cannot retain the old route's
                    // faster current-action promise merely because its broad horizon fits.
                    if (float.IsFinite(evidence.CastSeconds) && castSeconds > evidence.CastSeconds + 0.001f)
                        return new(false, "observed cast timing no longer supports the selected route");
                    if (actionStart <= 0.001f && float.IsFinite(evidence.CastSeconds) && action.CastSeconds > evidence.CastSeconds + 0.001f)
                        return new(false, "native cast timing has not yet corroborated the projected field benefit");
                    var recast = action.Timing?.RecastAt(position, actionStart, fieldEntry, relocatedField, fieldBenefitAllowed) ?? 0;
                    if (float.IsFinite(evidence.RecastSeconds) && recast > evidence.RecastSeconds + 0.001f
                        || actionStart <= 0.001f && float.IsFinite(evidence.RecastSeconds)
                            && action.Timing?.NativeRecast > evidence.RecastSeconds + 0.001f)
                        return new(false, "native recast timing no longer supports the selected field recovery");
                    if (NativeFieldExecution.LosesCurrent(current, action, position, actionStart,
                        actionStart + castSeconds + action.LockSeconds, coverage, fieldEntry, relocatedField, fieldBenefitAllowed))
                        return new(false, "field setup would now delay available current execution or recovery");
                    if (!current.Traverse(position, position, time, actionStart - time, out var actionWaitSlack))
                        return new(false, "waiting for the action no longer safe");
                    slack = Math.Min(slack, actionWaitSlack);
                    time = actionStart;
                    if (time + castSeconds > action.Latest)
                        return new(false, "current action opportunity would be lost");
                    var overlapsPush = float.IsFinite(stage.ActionLockUntil);
                    if (overlapsPush && (phaseIndex >= livePushes.Length
                        || time + castSeconds >= (livePushes[phaseIndex].Timing!.EarliestUtc - current.Identity.WorldTime).TotalSeconds
                        || Math.Abs(stage.ActionLockUntil - stage.EndSeconds - action.LockSeconds) > 0.001f))
                        return new(false, "cast delivery or animation lock no longer fits before the native push");
                    actionReadyAt = time + castSeconds + action.LockSeconds;
                    if (actionReadyAt + inputAge > MovementPlanningContext.MaximumHorizonSeconds)
                        return new(false, "action lock exceeds evidenced forecast horizon");
                    duration = castSeconds + (overlapsPush ? 0 : action.LockSeconds);
                    break;
                default:
                    var mobility = current.ResolveMobility(position, stage, relocatedField);
                    if (mobility == null || !resources.Ready(mobility.ResourceCost, Math.Max(time, mobility.ReadyIn), current.Recharges, out var mobilityReady))
                        return new(false, "executable mobility anchor, facing, landing or charges changed");
                    var setupEnd = mobilityReady + mobility.SetupSeconds;
                    resources = resources.Spend(mobility.ResourceCost, setupEnd, current.Recharges);
                    if (!current.Traverse(position, position, time, setupEnd - time, out var setupSlack))
                        return new(false, "mobility setup no longer safe");
                    slack = Math.Min(slack, setupSlack);
                    time = setupEnd;
                    duration = mobility.TravelSeconds;
                    landingLock = mobility.LockSeconds;
                    if (stage.ActionId == 7419 && (relocatedField ?? current.PersonalField) is { } returnField
                        && !returnField.LiveAt(time + duration + landingLock))
                        return new(false, "owned return field expires before the landing lock clears");
                    if (!current.Traverse(stage.To, stage.To, time + duration, mobility.LockSeconds, out var landingSlack))
                        return new(false, "mobility landing lock no longer safe");
                    slack = Math.Min(slack, landingSlack);
                    break;
            }
            var segmentSlack = float.MaxValue;
            if (stage.Kind == MovementStageKind.Cast && current.ComboRecovery is { } preparation
                && preparation.AfterActionId == stage.ActionId && preparation.TargetId == stage.AnchorId)
                comboReady = Math.Max(comboReady, Math.Max(time + preparation.RecastSeconds, time + duration));
            if (!teleported && !(stationary ? current.Traverse(position, landing, time, duration, out segmentSlack)
                : current.TraverseOrdinary(position, landing, time, duration, out segmentSlack)))
                return new(false, "complete executable route no longer clears native deadlines");
            if (!stationary && stage.Kind is not (MovementStageKind.ForcedMovement or MovementStageKind.Knockback) && !current.ExecutionRules.PermitsMovement(time, time + duration + landingLock)
                || stage.ActionId != 0 && stage.Kind != MovementStageKind.Target && !current.ExecutionRules.PermitsAction(executableFacing, time, time + duration + landingLock))
                return new(false, "authored movement, action or facing prohibition changed");
            slack = Math.Min(slack, segmentSlack);
            time += duration + landingLock;
            previousWait = stage.Kind == MovementStageKind.Wait ? duration : 0;
            fieldBenefitAllowed = PlanningCastTiming.BenefitAfter(fieldBenefitAllowed, stage, current.Actions);
            if (restrictFieldEntry)
                fieldEntry = PlanningCastTiming.EntryAfter(fieldEntry, stage with { To = landing, EndSeconds = time }, current.Actions, relocatedField);
            if (time + Math.Max(elapsed, worldElapsed) > MovementPlanningContext.MaximumHorizonSeconds)
                return new(false, "route exceeds evidenced forecast horizon");
            position = landing;
        }
        var destination = current.Cell(position);
        if (destination < 0 || !position.AlmostEqual(plan.UsefulDestination, ArrivalTolerance) || !current.DestinationAllowed(destination) || !current.CanRemain(position)
            || !current.DestinationSatisfied(plan.UsefulDestination, time))
            return new(false, "final position needs a different assignment or onward escape");
        if (current.NativePriority(destination) < plan.NativePriority)
            return new(false, "committed destination no longer preserves native goal priority");
        if (plan.ComboRecovery is { } combo && plan.ComboExecutionValue > 0
            && (current.ComboRecovery is not { } freshCombo || !freshCombo.Preserves(combo)
                || freshCombo.Value(position, time, comboReady, current.ExecutionRules) < plan.ComboExecutionValue
                || Math.Max(time, comboReady) + inputAge >= combo.EligibilityEndsIn
                || combo.DeadlineTime(time + inputAge, comboReady + inputAge) >= combo.EndsIn))
            return new(false, "observed combo or its original recovery deadline no longer supports this route");
        if (plan.UsefulZones.Any(zone => !current.Zones.Any(z => z.ActionId == zone.ActionId && z.OwnerId == zone.OwnerId
            && z.Benefit == zone.Benefit && z.EffectiveCenter(relocatedField) == zone.Center && z.Contains(plan.UsefulDestination, time, relocatedField))))
            return new(false, "friendly effect no longer covers the executable arrival");
        var departureDeadline = firstDeparture ?? (slack == float.MaxValue ? float.MaxValue : slack + (departure ?? 0));
        return new(true, "fresh complete executable route", Math.Min(departureDeadline, plan.RemainingDepartureSeconds(timestamp ?? Stopwatch.GetTimestamp())));
    }
}
