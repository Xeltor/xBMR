using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using Dalamud.Game.ClientState.Objects.Types;
using System.Diagnostics;

namespace BossMod.CombatAssistance.Targeting;

internal sealed partial class NativeOffensiveTargeting
{
    private readonly NativeTargetSetupOwner _targetSetup = new();
    private readonly Dictionary<object, RsrOffensiveEligibility> _eligibility = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(uint Action, ulong Target), TargetFailure> _rejectedTargets = [];
    private long _lastSelectionPass;
    private float _setupEstimate;
    private ulong _restoreTarget;
    private sealed record TargetFailure(WPos Player, WPos Target, DateTime LastCast, ImmutableArray<ulong> Recipients);

    private static ulong HardTarget()
    {
        var target = Service.TargetManager.Target;
        return target != null && target.IsValid() && target.Address != 0 ? target.GameObjectId : 0;
    }

    private bool PlanningAuthority(bool requireResolvedPolicy = true) => _preferences().PickBetterAoeTarget && _allowed() && _mayRetarget()
        && _manager.Hints.NativeTargetIntentCollectionRequested
        && _manager.Hints.AuthoritativeTarget == null
        && _manager.Hints.AutomaticRetargetingAllowed != false
        && (!requireResolvedPolicy || !_manager.HasAutomaticTargetModule || _manager.Hints.AutomaticRetargetingAllowed != null)
        && _manager.WorldState.CurrentTime >= _manualUntil
        && _rotation != null && Fresh(_rotation) && SelectionMode() && !Manual();

    private bool Eligible(Metadata metadata, ulong id)
    {
        if (Service.ObjectTable.SearchById(id) is not IBattleChara wrapper
            || !wrapper.IsValid() || wrapper.Address == 0 || wrapper.GameObjectId != id) return false;
        try
        {
            if (!_eligibility.TryGetValue(metadata.Action, out var probe))
            {
                if (Get(metadata.Action, "TargetInfo") is not { } info) return false;
                _eligibility[metadata.Action] = probe = new(metadata.Action, metadata.Setting, info);
            }
            if (Service.ObjectTable.SearchById(_observedSelection.Target) is not IBattleChara original
                || !original.IsValid() || original.Address == 0) return false;
            return probe.Allows(wrapper, original);
        }
        catch (Exception error)
        {
            if (DateTime.UtcNow >= _nextLog)
            {
                _nextLog = DateTime.UtcNow.AddSeconds(10);
                Service.Logger.Verbose(error, "RSR rejected an individual native target choice");
            }
            return false;
        }
    }

    internal PlanningAction AttachChoices(PlanningAction action, RsrActionEvidence evidence)
    {
        try
        {
            if (!PlanningAuthority() || !SelectionFresh() || !_preferences().PickBetterAoeTarget
                || evidence.Friendly || evidence.GroundTargeted
                || !action.UsesHardTarget || action.Opportunity is not { } original
                || !SupportsTargetChoice(original)
                || !_currentActions.TryGetValue(action.Id, out var metadata)
                || CaptureFrame() is not { RetainSelection: false, Forced: 0 } frame
                || !SameSelection(ReadSelection(), _observedSelection)
                || evidence.ActionId != _observedSelection.Action || evidence.TargetId != _observedSelection.Target
                || original.Recipients.FirstOrDefault(r => r.Id == original.TargetId) is not { } oldPrimary)
                return action;
            // A measured original callback interval and the current frame are the
            // setup evidence. They are estimates, never a promise or a renewed lease.
            var setup = Math.Max(_setupEstimate, _manager.WorldState.Frame.Duration);
            if (!float.IsFinite(setup) || setup <= 0 || setup >= .25f) return action;
            var oldActor = frame.Actors.FirstOrDefault(a => a.Actor.Id == original.TargetId);
            if (oldActor == null) return action;
            var forbidden = original.ForbiddenRecipients.Concat(frame.Actors
                .Where(a => a.ForbiddenCollateral || metadata.Geometry.ProvidesTargetStatus && a.ForbidDots)
                .Select(a => new ExecutionRecipient(a.Actor.Id, a.Actor.Position, a.Actor.Radius, 0,
                    HeightOffset: a.Actor.Height - (original.Shape == ExecutionShape.TargetCircle ? oldActor.Actor.Height : frame.Player.Height))))
                .DistinctBy(a => a.Id).ToArray();
            original = CopyOpportunity(original, original.TargetId, original.Anchor, original.CastReach,
                original.Recipients, forbidden);
            action = action with { Opportunity = original };
            var priority = frame.Actors.Where(a => frame.Eligible(a.Actor.Id)).Select(a => a.Priority).DefaultIfEmpty(-1).Max();
            var choices = ImmutableArray.CreateBuilder<PlanningTargetChoice>();
            foreach (var candidate in frame.Actors.OrderBy(a => a.Actor.Id))
            {
                var actor = candidate.Actor;
                if (actor.Id == action.TargetId || !frame.Eligible(actor.Id) || candidate.Priority != priority
                    || candidate.ForbiddenCollateral || metadata.Geometry.ProvidesTargetStatus && candidate.ForbidDots
                    || !original.Recipients.Any(r => r.Id == actor.Id) || !Eligible(metadata, actor.Id)) continue;
                var key = (action.Id, actor.Id);
                if (_rejectedTargets.TryGetValue(key, out var failure))
                {
                    if (failure.Player == frame.Player.Position && failure.Target == actor.Position
                        && failure.LastCast == _manager.LastCast.Time
                        && !failure.Recipients.IsDefault && failure.Recipients.SequenceEqual(_observedSelection.Recipients)) continue;
                    _rejectedTargets.Remove(key);
                }
                var choice = Reanchor(original, actor.Id, actor.Position, actor.Radius,
                    original.Shape == ExecutionShape.TargetCircle ? oldActor.Actor.Height - actor.Height : 0,
                    setup, actor.Height - frame.Player.Height);
                if (choice != null && ChoiceRespectsMovementPreference(original, choice, frame.Player.Position,
                    _preferences().ManageMovement && _preferences().ManageAoePackPositioning))
                    choices.Add(choice);
                // Read-only RSR range/LOS checks describe the actual pose only.
                // Other route poses require fresh native eligibility before setup.
            }
            foreach (var key in _rejectedTargets.Keys.Where(key => key.Action != action.Id).ToArray()) _rejectedTargets.Remove(key);
            return action with { TargetChoices = choices.ToImmutable() };
        }
        catch (Exception error) { Failed(error); return action; }
    }

    // With movement coverage opted out, an improved anchor must already be
    // executable from the captured pose. A target preference cannot create a
    // walk to discover additional hits. Safety/native-goal movement still uses
    // the same planner and exact action range remains binding along that route.
    internal static bool ChoiceRespectsMovementPreference(ExecutionOpportunity original, PlanningTargetChoice choice,
        WPos origin, bool improveMovement)
    {
        if (improveMovement) return true;
        var baseline = original.Evaluate(origin);
        var alternative = choice.Opportunity.Evaluate(origin);
        return baseline.Allowed && alternative.Allowed && alternative.Value > baseline.Value;
    }

    internal static bool SupportsTargetChoice(ExecutionOpportunity opportunity)
        => opportunity.Shape is ExecutionShape.TargetCircle or ExecutionShape.Cone or ExecutionShape.Rectangle
            && opportunity.Positional == Positional.Any && opportunity.TankFacing == null;

    internal static PlanningTargetChoice? Reanchor(ExecutionOpportunity original, ulong target, WPos anchor,
        float radius, float heightShift, float setup, float vertical)
    {
        if (!SupportsTargetChoice(original)
            || original.Recipients.FirstOrDefault(r => r.Id == original.TargetId) is not { } primary) return null;
        var recipients = original.Recipients.Select(r => r with
        { HeightOffset = r.HeightOffset + heightShift, Required = r.Required || r.Id == target }).ToArray();
        var forbidden = original.ForbiddenRecipients.Select(r => r with { HeightOffset = r.HeightOffset + heightShift }).ToArray();
        var opportunity = CopyOpportunity(original, target, anchor, original.CastReach - primary.Radius + radius, recipients, forbidden);
        // Coverage of a target circle is independent of the player's later pose.
        // Preserve each newly affected actor in the native confirmation, too.
        if (original.Shape == ExecutionShape.TargetCircle)
            opportunity = CopyOpportunity(opportunity, target, anchor, opportunity.CastReach,
                recipients.Select(r => r with { Required = r.Required || opportunity.Covers(anchor, r.Id) }), forbidden);
        // Directional coverage is recomputed from each actual route pose and
        // its target-facing setup; no hit at a guessed pose becomes evidence.
        var choice = new PlanningTargetChoice(opportunity, setup, vertical);
        return choice.Preserves(original) ? choice : null;
    }

    // Called after native modules have resolved their retarget policy. Until
    // then, a pending handoff is merely local ownership, not a target write.
    private bool MaintainTargetSetup()
    {
        if (!PlanningAuthority()) return false;
        var hard = HardTarget();
        var wanted = _targetSetup.Pending?.Target ?? _restoreTarget;
        if (wanted == 0) return false;
        if (_targetSetup.Pending is { } pending)
        {
            if (Stopwatch.GetTimestamp() >= pending.Deadline
                || hard != pending.Original && hard != pending.Target) return false;
        }
        else if (hard != _expectedTarget && hard != _observedTarget)
        {
            _restoreTarget = 0;
            return false;
        }
        if (_manager.WorldState.Actors.Find(wanted) is not { IsDead: false, IsDestroyed: false, IsTargetable: true } actor
            || CaptureFrame() is not { } frame || !frame.Eligible(wanted)
            || !_manager.Hints.TrySetAssistedTarget(actor)) return false;
        _expectedTarget = wanted;
        _restoreTarget = 0;
        return true;
    }

    private static ExecutionOpportunity CopyOpportunity(ExecutionOpportunity source, ulong target, WPos anchor, float reach,
        IEnumerable<ExecutionRecipient> recipients, IEnumerable<ExecutionRecipient> forbidden)
        => new(source.Shape, target, anchor, reach, source.EffectRadius, recipients, source.HalfWidth, source.HalfAngle,
            source.Positional, source.TargetFacing, source.TankFacing, forbidden);

    internal bool PublishPlannedTarget(MovementExecution execution, MovementPlan plan, MovementPlanningContext context,
        int stageIndex, out string reason)
    {
        reason = "Native target setup lost its current action or authority";
        try
        {
            if (!PlanningAuthority() || !SelectionFresh() || stageIndex < 0 || stageIndex >= plan.Stages.Length
                || !MovementPlanValidation.Validate(plan, context, stageIndex, true).Allowed) return false;
            var stage = plan.Stages[stageIndex];
            if (stage.Kind != MovementStageKind.Target || stage.AnchorId != execution.TargetId || stage.ActionId != execution.ActionId
                || context.TargetSetup(stage with { From = context.Origin }) == null
                || context.ActionFor(stage)?.Opportunity is not { } opportunity
                || !_currentActions.TryGetValue(stage.ActionId, out var metadata)
                || CaptureFrame() is not { RetainSelection: false, Forced: 0 } frame
                || !frame.Eligible(stage.AnchorId) || !Eligible(metadata, stage.AnchorId)
                || !opportunity.Evaluate(context.Origin).Allowed) return false;
            var selection = ReadSelection();
            if (!SameSelection(selection, _observedSelection) || selection.Action != stage.ActionId
                || selection.Recipients.Any(id => !opportunity.Recipients.Any(r => r.Id == id && r.Required))) return false;
            var hard = HardTarget();
            var actor = _manager.WorldState.Actors.Find(stage.AnchorId);
            if (actor is not { IsDead: false, IsDestroyed: false, IsTargetable: true }
                || (actor.Position - context.Origin).LengthSq() + MathF.Pow(actor.PosRot.Y - frame.Player.Height, 2) >= 2500
                || hard != selection.Target && _targetSetup.Pending?.Target != hard) return false;
            var now = Stopwatch.GetTimestamp();
            var deadline = plan.Input.CapturedTimestamp + (long)(stage.EndSeconds * Stopwatch.Frequency);
            if (now >= deadline) { reason = "Native target setup deadline elapsed"; return false; }
            if (_targetSetup.Pending is { } pending)
            {
                reason = "An existing native target setup owns its original deadline";
                return pending.Target == stage.AnchorId && pending.Action == stage.ActionId;
            }
            if (hard == 0 || hard == stage.AnchorId) return false;
            if (!_manager.Hints.TrySetAssistedTarget(actor)) return false;
            _targetSetup.Begin(new(hard, stage.AnchorId, stage.ActionId,
                opportunity.Recipients.Where(r => r.Required).Select(r => r.Id).ToImmutableArray(), now, deadline));
            _expectedTarget = stage.AnchorId;
            reason = "Native planner selected this action anchor; awaiting fresh RSR confirmation";
            Decision = new(actor.InstanceID, actor.PosRot.XYZ(), stage.ActionId, reason, true, DateTime.UtcNow);
            return true;
        }
        catch (Exception error) { Failed(error); return false; }
    }

    private void UpdateTargetSetup(Actor player)
    {
        if (!_preferences().PickBetterAoeTarget)
        {
            _targetSetup.Release();
            _restoreTarget = 0;
            return;
        }
        if (_targetSetup.Pending is not { } pending) return;
        var hard = HardTarget();
        var selection = SelectionFresh() ? ReadSelection() : default;
        var authority = PlanningAuthority(requireResolvedPolicy: false);
        var progress = _targetSetup.Observe(hard, selection.Action, selection.Target, selection.Recipients,
            _selectionObserved, Stopwatch.GetTimestamp(), authority);
        if (progress == TargetSetupProgress.Foreign)
        {
            _expectedTarget = 0;
            _manualUntil = _manager.WorldState.CurrentTime.AddSeconds(3);
        }
        else if (progress == TargetSetupProgress.Confirmed) _expectedTarget = pending.Target;
        else if (progress == TargetSetupProgress.Failed)
        {
            if (_manager.WorldState.Actors.Find(pending.Target) is { } rejected)
                _rejectedTargets[(pending.Action, pending.Target)] = new(player.Position, rejected.Position,
                    _manager.LastCast.Time, _observedSelection.Recipients);
            // Restore only an unchanged owned target, never a user/encounter change.
            if (hard == pending.Target && authority
                && _manager.WorldState.Actors.Find(pending.Original) is { IsDead: false, IsDestroyed: false, IsTargetable: true } original
                && CaptureFrame() is { } frame && frame.Eligible(original.InstanceID))
                _restoreTarget = original.InstanceID;
            Status = "Planned target setup expired without fresh action/recipient confirmation";
        }
    }
}
