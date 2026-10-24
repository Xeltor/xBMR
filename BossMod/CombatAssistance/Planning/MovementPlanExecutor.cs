using System.Diagnostics;

namespace BossMod.CombatAssistance.Planning;

internal enum MovementExecutionKind { Invalidated, Walk, Wait, Face, Action, Arrived, Target }
internal sealed record MovementExecution(MovementExecutionKind Kind, string Reason, WPos? Waypoint = null,
    Angle? Facing = null, uint ActionId = 0, ulong TargetId = 0, float DepartureSeconds = 0, bool PresentationFacing = false, bool ForcedMovementFacing = false);
internal readonly record struct MovementExecutionObservation(Angle Facing, uint ActionId = 0, ulong TargetId = 0,
    DateTime ActionTime = default, ulong CurrentTarget = 0);

// One framework-thread execution state machine, independent of activation/preset selection.
// Every directive is checked against a fresh captured context. The caller applies it in
// the same frame, after its native movement and manual-control gates have been checked.
internal sealed class MovementPlanExecutor
{
    private MovementPlan? _plan;
    private int _stage;
    private (uint Action, ulong Target, WPos Landing, Angle? Facing)? _preparation;
    private long _preparationStarted;
    private (uint Action, ulong Target, DateTime Time)? _consumedAction;
    internal WPos? CommittedDestination { get; private set; }
    internal int StageIndex => _stage;

    internal MovementExecution AdvanceRecovery(MovementPlan plan, Pathfinding.NavigationIdentity current, bool hasAuthority,
        bool executable, long? timestamp = null)
    {
        Release();
        if (!hasAuthority) return Invalidate("manual or external movement authority");
        if (plan.NativeRecovery?.Publication is not { } source || !executable || source.Source.ProjectionLayer != current.ProjectionLayer)
            return Invalidate("native recovery route changed");
        var publication = source.Publish();
        if (!publication.TryRemainingLeeway(current.WorldTime, current.ActorId, current.ZoneId, current.ContentId, out var remaining, timestamp))
            return Invalidate("stale native recovery input");
        CommittedDestination = publication.Endpoint;
        return publication.Destination is { } destination && !current.Origin.AlmostEqual(destination, MovementPlanValidation.ArrivalTolerance)
            ? new(MovementExecutionKind.Walk, "native recovery: " + publication.NativeScore, destination, DepartureSeconds: remaining)
            : new(MovementExecutionKind.Arrived, "native recovery destination reached", DepartureSeconds: remaining);
    }

    internal MovementExecution Advance(MovementPlan plan, MovementPlanningContext current,
        MovementExecutionObservation observed, bool hasAuthority, long? timestamp = null)
    {
        var now = timestamp ?? Stopwatch.GetTimestamp();
        if (!hasAuthority)
            return Invalidate("manual or external movement authority");
        if (!ReferenceEquals(plan, _plan))
        {
            _plan = plan;
            _stage = 0;
            // A worker can finish while the previous verified route is being followed.
            // Resume at the actually occupied walking segment, without skipping a wait
            // or action or walking backwards to an already passed waypoint.
            for (var i = 0; i < plan.Stages.Length && plan.Stages[i].Kind == MovementStageKind.Walk; ++i)
            {
                var segment = plan.Stages[i];
                var delta = segment.To - segment.From;
                var lengthSquared = delta.LengthSq();
                if (lengthSquared <= 1e-8f)
                    continue;
                var progress = (current.Origin - segment.From).Dot(delta) / lengthSquared;
                if (progress is >= 0 and <= 1 && current.Origin.AlmostEqual(segment.From + progress * delta, MovementPlanValidation.ArrivalTolerance))
                    _stage = i;
            }
        }
        var age = Math.Max((now - plan.Input.CapturedTimestamp) / (double)Stopwatch.Frequency,
            (current.Identity.WorldTime - plan.Input.WorldTime).TotalSeconds);
        var actionObservation = (observed.ActionId, observed.TargetId, observed.ActionTime);
        while (_stage < plan.Stages.Length)
        {
            var stage = plan.Stages[_stage];
            if (stage.Kind == MovementStageKind.Target && current.TargetConfirmed(stage, observed.CurrentTarget)
                || stage.Kind == MovementStageKind.Walk && current.Origin.AlmostEqual(stage.To, MovementPlanValidation.ArrivalTolerance)
                || stage.Kind == MovementStageKind.Teleport && current.Origin.AlmostEqual(stage.To, MovementPlanValidation.ArrivalTolerance)
                || stage.Kind == MovementStageKind.ForcedMovement && current.ForcedMovement == null
                    && current.Origin.AlmostEqual(stage.To, MovementPlanValidation.ArrivalTolerance) && age >= stage.EndSeconds
                || stage.Kind == MovementStageKind.Knockback && stage.PreventedByActionId == 0 && stage.Knockback?.Completed(current, stage) == true
                || stage.Kind == MovementStageKind.Wait && age >= stage.EndSeconds
                || stage.Kind != MovementStageKind.Target && _consumedAction != actionObservation && stage.ActionId != 0 && observed.ActionId == stage.ActionId
                    && (observed.TargetId == stage.AnchorId || stage.AnchorId == 0 && stage.Kind is not MovementStageKind.Cast
                        && (observed.TargetId == current.Identity.ActorId || observed.TargetId == 0xE0000000))
                    && observed.ActionTime >= plan.Input.WorldTime.AddSeconds(stage.StartSeconds)
                    && observed.ActionTime <= current.Identity.WorldTime
                    && (stage.Kind == MovementStageKind.Cast || current.Origin.AlmostEqual(stage.To, MovementPlanValidation.ArrivalTolerance)))
            {
                if (stage.ActionId != 0 && stage.Kind != MovementStageKind.Target)
                    _consumedAction = actionObservation;
                ++_stage;
                _preparation = null;
            }
            else
                break;
        }
        var validation = MovementPlanValidation.Validate(plan, current, _stage, true, now);
        if (!validation.Allowed)
            return Invalidate(validation.Reason);
        CommittedDestination = plan.UsefulDestination;
        if (_stage == plan.Stages.Length)
        {
            if (plan.DowntimeFacing is { } preferred && current.DowntimeFacing == preferred && current.Actions.IsEmpty
                && current.UnlockSeconds == 0 && !observed.Facing.AlmostEqual(preferred, PartyFacingPreference.Tolerance.Rad)
                && current.ExecutionRules.PermitsAction(preferred, 0, 0.25f))
                return new(MovementExecutionKind.Face, "stable party facing during downtime", Facing: preferred,
                    DepartureSeconds: validation.DepartureSeconds, PresentationFacing: true);
            return new(MovementExecutionKind.Arrived, "useful destination reached and planned actions observed", DepartureSeconds: validation.DepartureSeconds);
        }
        var next = plan.Stages[_stage];
        if (next.Kind == MovementStageKind.ForcedMovement && next.Forced is { } authored)
        {
            var fresh = current.ForcedMovement?.Routes.FirstOrDefault(route => route.Source == authored.Source
                && (route.Active || route.Heading == authored.Heading)
                && route.Landing.AlmostEqual(authored.Landing, MovementPlanValidation.ArrivalTolerance));
            if (fresh is { CanTurn: true } && !observed.Facing.AlmostEqual(fresh.Heading, 0.01f))
                return new(MovementExecutionKind.Face, "prepare verified native forced-march route", Facing: fresh.Heading,
                    DepartureSeconds: Math.Min(validation.DepartureSeconds, fresh.StartsIn), ForcedMovementFacing: true);
            return new(MovementExecutionKind.Wait, "native forced movement controls the route", DepartureSeconds: validation.DepartureSeconds);
        }
        if (next.Kind == MovementStageKind.Knockback)
            return new(MovementExecutionKind.Wait, next.PreventedByActionId != 0 ? "awaiting native immunity confirmation"
                : "awaiting native effect and observed knockback landing", DepartureSeconds: validation.DepartureSeconds);
        if (current.UnlockSeconds > 0)
            return new(MovementExecutionKind.Wait, "current cast or action lock", DepartureSeconds: validation.DepartureSeconds);
        if (next.Kind == MovementStageKind.Target && age >= next.EndSeconds)
            return Invalidate("planned target was not confirmed in its captured setup window");
        if (next.Kind == MovementStageKind.Target)
            return observed.CurrentTarget == next.AnchorId
                ? new(MovementExecutionKind.Wait, "waiting for the native rotation to confirm the planned target", TargetId: next.AnchorId,
                    ActionId: next.ActionId, DepartureSeconds: validation.DepartureSeconds)
                : new(MovementExecutionKind.Target, "prepare the action target before movement commitment", TargetId: next.AnchorId,
                    ActionId: next.ActionId, DepartureSeconds: validation.DepartureSeconds);
        if (next.Kind == MovementStageKind.Walk)
        {
            _preparation = null;
            return new(MovementExecutionKind.Walk, "fresh complete native route", next.To, DepartureSeconds: validation.DepartureSeconds);
        }
        if (next.Kind == MovementStageKind.Wait)
            return new(MovementExecutionKind.Wait, "planned execution window", DepartureSeconds: validation.DepartureSeconds);
        if (next.Kind == MovementStageKind.Teleport && next.Transit is { } transit)
            return current.Origin.AlmostEqual(transit.Entrance, MovementPlanValidation.ArrivalTolerance)
                ? new(MovementExecutionKind.Wait, "waiting for the authored teleporter landing", DepartureSeconds: validation.DepartureSeconds)
                : new(MovementExecutionKind.Walk, "approaching the authored teleporter entrance", transit.Entrance,
                    DepartureSeconds: validation.DepartureSeconds);
        if (plan.Stages.Take(_stage).Any(stage => float.IsFinite(stage.ActionLockUntil) && age < stage.ActionLockUntil))
            return new(MovementExecutionKind.Wait, "preceding cast animation lock", DepartureSeconds: validation.DepartureSeconds);
        if (next.Kind == MovementStageKind.UtilityAction && current.Utilities.FirstOrDefault(u => u.ActionId == next.ActionId) is { ReadyIn: > 0 })
            return new(MovementExecutionKind.Wait, "current utility action lock", DepartureSeconds: validation.DepartureSeconds);
        if (next.Kind is not (MovementStageKind.Cast or MovementStageKind.FieldPlacement) && current.ResolveMobility(current.Origin, next) is { ReadyIn: > 0 })
            return new(MovementExecutionKind.Wait, "current mobility action lock", DepartureSeconds: validation.DepartureSeconds);
        // A stopped player is within native arrival tolerance, not necessarily at
        // the mathematical waypoint. Recompute an attack's facing from the actual
        // position; fixed directional mobility still requires its exact landing.
        var executableFacing = next.Kind == MovementStageKind.Cast
            && current.ActionFor(next) is { } action
                ? current.Evaluate(action, current.Origin, Math.Max(current.UnlockSeconds, action.Earliest)
                    + action.CastAt(current.Origin, Math.Max(current.UnlockSeconds, action.Earliest),
                        action.Timing?.FieldEntryByAbility == true && plan.Stages.Take(_stage).Aggregate(false,
                            (entry, stage) => PlanningCastTiming.EntryAfter(entry,
                                stage with { EndSeconds = Math.Max(0, stage.EndSeconds - (float)age) }, current.Actions, current.PersonalField)),
                        current.PersonalField, plan.Stages.Take(_stage).Aggregate(true,
                            (allowed, stage) => PlanningCastTiming.BenefitAfter(allowed, stage, current.Actions))) < current.PositionalBypassUntil).Facing : next.Facing;
        var key = (next.ActionId, next.AnchorId, next.To, executableFacing);
        if (_preparation != key)
        {
            _preparation = key;
            _preparationStarted = now;
        }
        if (executableFacing is { } facing && !observed.Facing.AlmostEqual(facing, 0.01f))
            return new(MovementExecutionKind.Face, "planned action requires its executable facing", Facing: facing,
                DepartureSeconds: validation.DepartureSeconds);
        if (Stopwatch.GetElapsedTime(_preparationStarted, now).TotalSeconds < next.PreparationSeconds)
            return new(MovementExecutionKind.Wait, "planned action setup", DepartureSeconds: validation.DepartureSeconds);
        // GCD selection and delivery stay with the selected rotation. We hold at the
        // useful execution point until the native action-effect observation confirms it.
        if (next.Kind is MovementStageKind.Cast or MovementStageKind.FieldPlacement
            || next.Utility?.Effect == PlanningUtilityEffect.KnockbackImmunity)
            return new(MovementExecutionKind.Wait, "waiting for the selected rotation action", Facing: executableFacing,
                ActionId: next.ActionId, TargetId: next.AnchorId, DepartureSeconds: validation.DepartureSeconds);
        return new(MovementExecutionKind.Action, "exact mobility path, anchor, facing and landing revalidated", next.To,
            next.Facing, next.ActionId, next.AnchorId, validation.DepartureSeconds);
    }

    private MovementExecution Invalidate(string reason)
    {
        Release();
        return new(MovementExecutionKind.Invalidated, reason);
    }

    internal void Release()
    {
        _plan = null;
        _stage = 0;
        _preparation = null;
        _consumedAction = null;
        CommittedDestination = null;
    }
}
