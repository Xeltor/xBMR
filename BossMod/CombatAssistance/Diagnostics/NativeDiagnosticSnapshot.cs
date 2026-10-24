using BossMod.CombatAssistance.Planning;
using System.Text.Json;

namespace BossMod.CombatAssistance.Diagnostics;

// One current lifecycle and on-demand current-state projection. DevBridge owns sampling,
// retention, segmentation and capture files. Display limits never constrain the planner.
internal sealed class NativeDiagnosticSnapshot
{
    private readonly string _epoch = Guid.NewGuid().ToString("N");
    private string? _combatId;
    private DateTime? _started;
    private DateTime? _ended;
    private bool _active;
    private ulong _player;
    private ushort _zone;
    internal const int MaximumDisplayedStages = 64;
    internal const int MaximumSnapshotBytes = 8192;

    internal void Observe(bool inCombat, ulong playerId, ushort zone, DateTime now)
    {
        if (inCombat && (!_active || playerId != _player || zone != _zone))
        {
            _combatId = Guid.NewGuid().ToString("N");
            _started = now;
            _ended = null;
        }
        else if (!inCombat && _active)
            _ended = now;
        _active = inCombat;
        _player = playerId;
        _zone = zone;
    }

    internal string Read(string requestJson, MovementPlan? plan, bool assistanceAllowed, bool following, string settingsState,
        string? failure, DateTime now, bool frameworkThread = true, MovementExecution? execution = null,
        Camera.CombatCameraStatus? camera = null, string? actionStatus = null, Support.SupportRecipientSnapshot? support = null, string? supportStatus = null,
        Control.CastControlDecision? control = null, Support.TankStanceDecision? tankStance = null, string? tankStanceStatus = null,
        Support.TankRecoveryDecision? tankRecovery = null, string? tankRecoveryStatus = null, string? rotationMode = null, string? inputChange = null,
        Func<NativeActionTimingObservation?>? readTiming = null,
        Targeting.OffensiveTargetDecision? targeting = null, string? targetingStatus = null,
        Knockback.NativeKnockbackObservationStatus? knockbackObservation = null, NativeDisplacementStatus? displacement = null, string? knockbackSelection = null,
        string? healingStatus = null)
    {
        if (!frameworkThread)
            return Failure("framework_thread_required");
        if (requestJson.Length > 4096)
            return Failure("invalid_request");
        try
        {
            using var request = JsonDocument.Parse(requestJson);
            var root = request.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema) || schema != 1)
                return Failure("unsupported_schema_version");
            if (!root.TryGetProperty("stream", out var stream) || stream.ValueKind != JsonValueKind.String || stream.GetString() != "combat")
                return Failure("unsupported_stream");
        }
        catch (JsonException) { return Failure("invalid_request"); }

        var nativeTiming = readTiming?.Invoke();

        var displayedStages = MaximumDisplayedStages;
        var displayedRecipients = 64;
        while (true)
        {
            var json = Serialize();
            if (System.Text.Encoding.UTF8.GetByteCount(json) <= MaximumSnapshotBytes)
                return json;
            if (displayedStages == 0 && displayedRecipients == 0)
                return Failure("current_snapshot_metadata_too_large");
            displayedStages /= 2;
            displayedRecipients /= 2;
        }

        string Serialize() => JsonSerializer.Serialize(new
        {
            ok = true,
            schemaVersion = 1,
            epoch = _epoch,
            capturedAtUtc = now,
            stream = "combat",
            plugin = "BossModReborn",
            build = new
            {
                moduleVersionId = typeof(NativeDiagnosticSnapshot).Module.ModuleVersionId,
                assemblyVersion = typeof(NativeDiagnosticSnapshot).Assembly.GetName().Version?.ToString()
            },
            lifecycle = new { kind = "combat", id = _combatId, active = _active, startedAtUtc = _started, endedAtUtc = _ended },
            snapshot = new
            {
                active = _active,
                status = new
                {
                    assistanceAllowed,
                    following,
                    settingsState,
                    rotationMode,
                    inputChange,
                    failure = failure?[..Math.Min(512, failure.Length)],
                    zoneId = _zone,
                    playerId = _player
                },
                movement = Project(plan, execution, displayedStages, displayedRecipients),
                camera,
                nativeTiming,
                knockbackObservation,
                displacement,
                knockbackSelection,
                support,
                supportStatus,
                tankStance,
                tankStanceStatus,
                tankRecovery = tankRecovery == null ? null : new
                {
                    tankRecovery.Target,
                    tankRecovery.Action,
                    tankRecovery.Reason,
                    tankRecovery.Submitted,
                    tankRecovery.ObservedAt,
                    position = new { x = tankRecovery.Position.X, y = tankRecovery.Position.Y, z = tankRecovery.Position.Z }
                },
                tankRecoveryStatus,
                targeting = targeting == null ? null : new
                {
                    targeting.Target, targeting.Action, targeting.Reason, targeting.Published,
                    targeting.ObservedAt, targeting.GuardedActions, targeting.UnsupportedActions, targeting.CaptureMilliseconds,
                    position = new { x = targeting.Position.X, y = targeting.Position.Y, z = targeting.Position.Z }
                },
                targetingStatus,
                healingStatus,
                castControl = control == null ? null : new
                {
                    control.Target,
                    control.Cast,
                    control.Action,
                    choice = control.Choice.ToString(),
                    position = new { x = control.Position.X, y = control.Position.Y, z = control.Position.Z },
                    control.Reason,
                    control.ObservedAt
                },
                actionStatus = actionStatus?[..Math.Min(512, actionStatus.Length)]
            }
        });
    }

    private static object? Project(MovementPlan? plan, MovementExecution? execution, int displayedStages, int displayedRecipients) => plan == null ? null : new
    {
        status = plan.Status.ToString(),
        bound = plan.Bound.ToString(),
        inputGeneration = plan.Input.Generation,
        targetId = plan.TargetId,
        capturedAtUtc = plan.Input.CapturedUtc,
        inputWorldTime = plan.Input.WorldTime,
        friendlyEffects = plan.UsefulZones.Take(displayedStages).Select(z => new
        {
            z.ActionId,
            z.OwnerId,
            z.Name,
            center = new { x = z.Center.X, z.Height, z = z.Center.Z },
            z.Radius,
            z.EndsIn,
            z.CompleteFootprint,
            benefit = z.Benefit.ToString()
        }),
        friendlyEffectCount = plan.UsefulZones.Length,
        finalDestination = Position(plan.UsefulDestination),
        immediateWaypoint = (execution == null ? plan.ImmediateWaypoint : execution.Waypoint) is { } immediate ? Position(immediate) : null,
        execution = execution == null ? null : new
        {
            kind = execution.Kind.ToString(),
            execution.Reason,
            execution.ActionId,
            execution.TargetId,
            execution.SubmissionPending,
            facing = execution.Facing?.Rad,
            departureSeconds = FiniteDeadline(execution.DepartureSeconds)
        },
        optimizationPending = plan.OptimizationPending,
        downtimeFacing = plan.DowntimeFacing?.Rad,
        departureSecondsAtCapture = FiniteDeadline(plan.DepartureDeadlineSeconds),
        durationSeconds = FiniteDeadline(plan.DurationSeconds),
        additionalPlanDelayAtCapture = FiniteDeadline(plan.PlanSlackSeconds),
        plan.ExpandedStates,
        plan.ResourcePrunedActions,
        plan.SearchMilliseconds,
        plan.Evidence,
        plan.RemainingResources,
        plan.NativePriority,
        plan.OriginNativePriority,
        plan.OriginPresentation,
        plan.DestinationPresentation,
        priorDestination = plan.PriorDestination is { } prior ? Position(prior) : null,
        rangeAccess = plan.RangeAccess is { } access ? new
        { readyAt = FiniteDeadline(access.ReadyAt), loss = FiniteDeadline(access.Loss), at = FiniteDeadline(access.At) } : null,
        partyContact = plan.PartyContact is { } contact ? new { contact.TargetId, center = Position(contact.Center), contact.Radius } : null,
        healingCoverage = plan.HealingCoverage?.Requirements.Take(displayedRecipients).Select(r => new
        {
            r.RecipientId, r.HealthRatio, position = Position(r.Position),
            covered = r.Loss(plan.UsefulDestination) <= MovementPlanValidation.ArrivalTolerance,
            alternatives = r.Alternatives.Take(displayedStages).Select(a => new { a.ActionId, a.Radius, kind = a.Kind.ToString() })
        }),
        healingRequirementCount = plan.HealingCoverage?.Requirements.Length ?? 0,
        comboRecovery = plan.ComboRecovery is { } combo ? new
        {
            combo.TargetId,
            center = Position(combo.Center),
            combo.Radius,
            combo.EndsIn,
            combo.ActionId,
            combo.FallbackActionId,
            combo.AfterActionId,
            combo.RecastSeconds,
            plan.ComboReadyAt,
            plan.ComboExecutionValue,
            kind = combo.Kind.ToString(),
            combo.ForecastHorizon,
            combo.MinimumTargets,
            eligibilityEndsIn = FiniteDeadline(combo.EligibilityEndsIn),
            requiredRecipientIds = combo.Opportunity?.Recipients.Where(r => r.Required).Select(r => r.Id).Take(displayedRecipients),
            recipientCount = combo.Opportunity?.Recipients.Count(r => r.Required) ?? 0
        } : null,
        actionDeliverySeconds = plan.DeliveryTimes.Select(FiniteDeadline),
        actionRecoverySeconds = plan.RecoveryTimes.Select(FiniteDeadline),
        actionEvidence = plan.ActionEvidence.Select(a => new
        {
            a.Id,
            preparation = a.Preparation.ToString(),
            a.TargetId,
            requiredRecipientIds = a.RequiredRecipientIds.Take(displayedRecipients),
            recipientCount = a.RequiredRecipientIds.Length,
            recipientsTruncated = a.RequiredRecipientIds.Length > displayedRecipients,
            a.Value,
            a.SourceActionIndex,
            castSeconds = FiniteDeadline(a.CastSeconds),
            recastSeconds = FiniteDeadline(a.RecastSeconds),
            castTiming = a.Timing == null ? null : new
            {
                a.Timing.WithoutProc,
                a.Timing.UnbuffedUpperBound,
                a.Timing.ObservedFieldCast,
                a.Timing.ObservedOutsideCast,
                a.Timing.ObservedFieldRecast,
                a.Timing.ObservedOutsideRecast,
                a.Timing.OutsideTimingKnown,
                a.Timing.FieldEntryByAbility,
                a.Timing.FieldBenefitExcludedAfterAction,
                hasteUntil = FiniteDeadline(a.Timing.HasteUntil)
            }
        }),
        stageCount = plan.Stages.Length,
        stagesTruncated = plan.Stages.Length > displayedStages,
        stages = plan.Stages.Take(displayedStages).Select(stage => new
        {
            kind = stage.Kind.ToString(),
            from = Position(stage.From),
            to = Position(stage.To),
            stage.StartSeconds,
            stage.EndSeconds,
            actionLockUntil = FiniteDeadline(stage.ActionLockUntil),
            preventedByActionId = stage.PreventedByActionId == 0 ? (uint?)null : stage.PreventedByActionId,
            immunityApplicationSeconds = stage.Utility?.ApplicationSeconds,
            immunityDurationSeconds = stage.Utility?.Duration,
            stage.ActionId,
            stage.AnchorId,
            stage.SourceActionIndex,
            facing = stage.Facing?.Rad,
            stage.PreparationSeconds,
            personalField = stage.Field == null ? null : new
            {
                stage.Field.Owner,
                stage.Field.Object,
                stage.Field.EffectIdentity,
                originalCenter = Position(stage.Field.Center),
                stage.Field.Radius,
                stage.Field.EndsIn,
                relocatedCenter = Position(stage.To)
            },
            knockback = stage.Knockback == null ? null : new
            {
                stage.Knockback.Source.SourceActor,
                stage.Knockback.Source.CastActionId,
                stage.Knockback.Source.SourceDataId,
                stage.Knockback.Source.Activation,
                stage.Knockback.CastGeneration,
                earliest = stage.Knockback.Timing?.EarliestUtc,
                latest = stage.Knockback.Timing?.LatestUtc,
                landingLatest = stage.Knockback.Timing?.LandingLatestUtc
            },
            transit = stage.Transit == null ? null : new
            {
                entrance = Position(stage.Transit.Entrance),
                exit = Position(stage.Transit.Exit),
                stage.Transit.Radius,
                stage.Transit.UseTime,
                stage.Transit.NotBefore,
                stage.Transit.ElapsedUse
            },
            forced = stage.Forced == null ? null : new
            {
                stage.Forced.Source,
                stage.Forced.CanTurn,
                stage.Forced.Active,
                legCount = stage.Forced.Legs.Length,
                legs = stage.Forced.Legs.Take(8).Select(leg => new
                { from = Position(leg.From), to = Position(leg.To), leg.Start, leg.End })
            }
        })
    };

    private static object Position(WPos position) => new { x = position.X, z = position.Z };
    private static float? FiniteDeadline(float value) => float.IsFinite(value) && value != float.MaxValue ? value : null;
    private static string Failure(string error) => JsonSerializer.Serialize(new { ok = false, schemaVersion = 1, error });
}
