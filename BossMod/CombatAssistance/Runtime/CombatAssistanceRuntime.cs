using BossMod.AI;
using BossMod.Autorotation;
using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Targeting;
using Dalamud.Plugin;

namespace BossMod.CombatAssistance.Runtime;

internal sealed record MovementCombatIntent(ImmutableArray<PlanningAction> Actions, float UnlockSeconds, bool Enabled,
    AvailableMobility? Mobility = null, float PositionalBypassUntil = 0, PlanningTravel? Travel = null, float? LateDepartureMargin = null, ForcedMovementChoices? ForcedMovement = null, PlanningFieldPlacement? FieldPlacement = null, string? NativeRecoveryReason = null, bool StationaryOnly = false, PlanningRange? RangeRecovery = null,
    PlanningHealingCoverage? HealingCoverage = null, PlanningMovementValue? MovementValue = null,
    HealingDeliveryRequest? HealingDelivery = null);

// Framework-thread composition of native actors, authority and external action evidence.
// It does not activate AI, own a preset, publish movement or retain combat history.
internal sealed partial class CombatAssistanceRuntime : IDisposable
{
    private readonly AssistanceSettingsSession _settings;
    private readonly RotationModuleManager _rotation;
    private readonly ActionManagerEx _actions;
    private readonly MovementOverride _movement;
    private readonly RotationSolverRebornModule _rsr;
    private readonly RsrActionEvidenceSource _evidence;
    private RsrActionEvidence? _frameEvidence;
    private RsrActionEvidence? _frameImmediateEvidence;
    private RsrRedMageIntent? _frameComboIntent;
    private DateTime _evidenceFrame;
    private ulong _evidencePlayer;
    private readonly RsrPlannedUtilitySelection _utilitySelection;
    private readonly RsrLeyLinesPlacement _leyLinesPlacement;
    private readonly RsrKnockbackSelection _knockbackSelection;
    private readonly RsrNativeMobilitySelection _mobilitySelection;
    private readonly RsrRotationalDelivery _rotationalDelivery;
    private readonly NativeMeleeRecoveryWindow _meleeRecoveryWindow = new();
    private readonly NativeHealingDelivery _healingDelivery = new();
    private RsrActionEvidence? _deliveryEvidence;
    private (MovementPlan Plan, MovementPlanningContext Context, int Stage)? _movementFrame;
    private DateTime _lastGcdDelivery;
    private DateTime _knockbackFrame;
    private ulong _knockbackActor;
    private BossModule? _knockbackModule;
    private NativeKnockbackAuthority? _knockbackAuthority;
    private NativeDisplacementCapture? _displacementCapture;
    private NativeKnockbackPhases? _knockbackPhases;
    internal NativeDisplacementStatus? DisplacementStatus
    {
        get
        {
            if (_rotation.WorldState.Party.Player() is not { } actor) return null;
            _ = CaptureKnockbackAuthority(_rotation.WorldState, actor);
            var input = _displacementCapture!.Snapshot;
            return new(input.KnockbackOwners, input.Knockbacks.Length, input.Knockbacks.Count(s => s.Interrupts(input.At)),
                input.Complete, input.AuthoredAbsenceVerified, input.MarchPublished, input.Pending,
                _knockbackPhases?.Phases.Length ?? 0, _knockbackPhases?.Detail ?? "phase capture not requested by a native movement owner",
                input.PreparationConstraints);
        }
    }
    private readonly LinkedRotationOwner _linkedRotation;
    private readonly RsrOperatingMode _modes;
    private readonly ForcedMarchProtection _forcedMarch;
    private readonly Control.NativeCastControl _castControl;
    private readonly Knockback.NativeKnockbackObserver _knockbackObserver;
    internal Knockback.KnockbackCompletionEvidence? KnockbackCompletion => _knockbackObserver.Completion;
    internal Knockback.NativeKnockbackObservationStatus KnockbackObservationStatus => _knockbackObserver.Status;
    private readonly NativeSpeedObservation _speed = new();
    private readonly PartyFacingPreference _partyFacing = new();
    private readonly NativeFormationPreference _formation = new();
    private readonly NativeSocialSpacing _socialSpacing = new();
    private readonly NativePartyContact _partyContact = new();
    private readonly NativeUtilityZones _zones;
    private readonly NativeLeyLinesTiming _leyLinesTiming = new();
    private readonly NativePictomancerTiming _pictomancerTiming = new();
    private readonly IDisposable _leyLinesEvents;
    private DateTime _nextLinkedRotation;
    private readonly Camera.CombatCameraController _camera;
    private readonly Camera.LegacyMovementOption _cameraMovement = Camera.LegacyMovementOption.Native();
    private readonly UI.DecisionOverlayController _overlay;
    private readonly NativeCorrections _corrections;
    private readonly QuestTargetingCorrection _questTargeting;
    private readonly Support.SupportRecipientController _support;
    private readonly RsrSelectionScopeOwner _selectionScopes;
    private readonly Targeting.NativeOffensiveTargeting _targeting;
    private readonly Support.RaiseAcceptanceController _raises;
    private readonly Support.RsrMeleeFallbackController _meleeFallback;
    private readonly Support.NativeTankStance _tankStance;
    private readonly Support.NativeTankRecovery _tankRecovery;
    private readonly AIConfig _ai = Service.Config.Get<AIConfig>();
    internal bool RsrAvailable => _rsr.IsInstalled;
    internal bool AllowNativeCombatSprint => !_settings.MayAssist
        || _settings.Preferences.UseSprint && !_mobilitySelection.Owned.Contains(3);
    internal string ActionStatus { get; private set; } = "Native rotation goals";

    internal CombatAssistanceRuntime(IDalamudPluginInterface plugin, AssistanceSettingsSession settings,
        RotationModuleManager rotation, ActionManagerEx actions, MovementOverride movement, RotationSolverRebornModule rsr)
    {
        _settings = settings;
        _rotation = rotation;
        _actions = actions;
        _movement = movement;
        _rsr = rsr;
        _knockbackObserver = new(rotation.WorldState,
            () => _movement.IsMoveRequested() || _movement.NativeInput.ManualFacing
                || _rotation.MovementPlanning.Execution?.Kind is MovementExecutionKind.Walk or MovementExecutionKind.Action or MovementExecutionKind.Face,
            id => Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Knockback>().TryGetRow(id, out var row)
                ? new(row.Distance, row.Speed, (byte)row.Direction) : null,
            (error, message) => Service.Logger.Verbose(error, message));
        _zones = new(rotation.WorldState);
        _leyLinesEvents = rotation.WorldState.Actors.CastEvent.Subscribe((actor, cast) =>
        {
            _leyLinesTiming.ObserveAction(actor.InstanceID, cast.Action, rotation.WorldState.CurrentTime);
            _preparation.Observe(actor.InstanceID, cast.Action.ID, cast.MainTargetID, cast.SourceSequence);
            _healingDelivery.Observe(actor.InstanceID, cast.Action.ID);
            if (actor.InstanceID == rotation.WorldState.Party.Player()?.InstanceID && ActionDefinitions.Instance[cast.Action]?.IsGCD == true)
                _lastGcdDelivery = rotation.WorldState.CurrentTime;
        });
        _evidence = new(plugin, Service.DataManager, Service.Logger);
        _actions.PreserveManualFacing = ManualFacingProtected;
        _camera = new(() => _settings.Preferences, Service.ObjectTable, Service.Logger, Service.Hook, CameraAllowed, _cameraMovement);
        _overlay = new(() => _settings.Preferences);
        _corrections = new(rotation.Bossmods);
        _questTargeting = new(rotation);
        _raises = new(() => _settings.Preferences, rotation.WorldState,
            () => _settings.MayAssist && !_ai.ForbidActions, Service.ObjectTable, Service.Condition, Service.GameGui, Service.Logger);
        var supportContext = new Support.NativeSupportContext(rotation.WorldState, rotation.Hints, Service.ObjectTable, Service.Logger);
        var rsrRuntime = new RsrRuntimeAccess(plugin);
        _selectionScopes = new(rsrRuntime, () => _settings.MayAssist, Service.Logger);
        _utilitySelection = new(rsrRuntime, () => SupportAllowed() && _settings.Preferences.ManageTrueNorth
            && _settings.Preferences.ManagePositionals, DeferRotationalTrueNorth);
        _mobilitySelection = new(rsrRuntime, () => MobilityAllowed() && OrdinaryMovementForecastAvailable(), StationaryRotationalMobility);
        _rotationalDelivery = new(rsrRuntime, actions, _selectionScopes, () => MobilityAllowed() && OrdinaryMovementForecastAvailable()
            && (AIManager.Instance?.Beh != null || Autorotation.MiscAI.NormalMovement.Instance != null)
            && _settings.Preferences.UseGapCloser && _rotation.WorldState.Party.Player() is { } current
            && _settings.Preferences.IsGapCloserJobEnabled((uint)current.Class), DeferMeleeFallback, ApprovePreparation);
        _knockbackSelection = new(rotation, rsrRuntime, () => MobilityAllowed() && _settings.Preferences.ManageKnockback,
            TankArmsLengthUseful, _selectionScopes);
        var modes = _modes = new RsrOperatingMode(plugin, rsrRuntime);
        _linkedRotation = new(modes.Read, modes.Set);
        _forcedMarch = new(() => { var mode = modes.Read(); return (mode, modes.ProviderIdentity); }, modes.Set);
        _castControl = new(rotation.WorldState, rotation.Hints, actions, rsrRuntime, CastControlAllowed);
        _meleeFallback = new(() => _settings.Preferences, supportContext, rsrRuntime, SupportAllowed);
        _tankRecovery = new(rotation.WorldState, rotation.Hints, actions, () => _settings.Preferences, rsrRuntime, MobilityAllowed);
        _leyLinesPlacement = new(rotation, actions, rsrRuntime,
            () => MobilityAllowed() && _settings.Preferences.ManageMovement && _settings.Preferences.ManageLeylines
                && _settings.Preferences.PlanLeyLinesPlacement && !CompulsoryMovementProtected && OrdinaryMovementForecastAvailable()
                && (AIManager.Instance?.Beh != null || Autorotation.MiscAI.NormalMovement.Instance != null)
                && rotation.WorldState.Party.Player() is { Class: Class.BLM, InCombat: true }, _selectionScopes);
        _targeting = new(rotation, rsrRuntime, () => _settings.Preferences, TargetingAllowed,
            () => !_ai.ManualTarget,
            () => _tankRecovery.LooseEnemyTarget, _selectionScopes);
        _tankStance = new(rotation.WorldState, rotation.Hints, actions, rsrRuntime,
            () => MobilityAllowed() && _settings.Preferences.TankDropStanceWhenCoTankHasStance, () => _tankRecovery.Pending);
        _support = new(() => _settings.Preferences, supportContext, rsrRuntime, SupportAllowed, _selectionScopes)
        {
            IncomingDamage = supportContext.IncomingDamage,
            MitigationDeadline = supportContext.MitigationDeadline
        };
    }

    private bool ManualFacingProtected()
        => _settings.MayAssist && _settings.Preferences.ManageMovement
            && (_forcedMarch.Protecting || _settings.Preferences.DisableAutoFaceTargetDuringManualMovement
            && (_movement.IsMoveRequested() || _movement.NativeInput.ManualFacing));

    internal bool StationaryTargetPlanningRequested => _settings.MayAssist && !_settings.Preferences.ManageMovement
        && _settings.Preferences.PickBetterAoeTarget && TargetingAllowed();

    internal bool CompulsoryMovementProtected => _forcedMarch.Protecting
        || _settings.MayAssist && (_settings.Preferences.ManageMovement || _settings.Preferences.PickBetterAoeTarget)
            && _rotation.WorldState.Party.Player() is { IsDead: false, MountId: 0 } player
            && NativeForcedMovementCapture.NeedsExclusivePlanning(_rotation.WorldState, _rotation.Bossmods.ActiveModule, player);
    internal bool IngressCastLocked => _actions.MoveMightInterruptCast;
    internal bool HasEncounterModule => _rotation.Bossmods.ActiveModule != null;

    internal bool HasMovementAuthority(Actor player, AIHints hints)
        => Service.ClientState.IsLoggedIn && Service.ObjectTable.LocalPlayer is { } local && local.IsValid()
            && local.Address != 0 && local.GameObjectId == player.InstanceID
            && !player.IsDead && !player.IsDestroyed
            && CombatAssistanceAuthority.CanSteer(_ai.ForbidMovement, hints.AutomaticMovementPaused,
                _movement.IsMoveRequested(), _movement.IsForceUnblocked(), _movement.NativeInput.ManualFacing)
            && (!_ai.ForbidAIMovementMounted || player.MountId == 0)
            && !_rotation.WorldState.Client.Flying
            && !_rotation.WorldState.Party.Members[PartyState.PlayerSlot].InCutscene;

    private bool HasCombatAuthority(Actor? player)
    {
        var local = Service.ObjectTable.LocalPlayer;
        return CombatAssistanceAuthority.CanAct(player,
            Service.ClientState.IsLoggedIn && player != null && local != null && local.IsValid()
                && local.Address != 0 && local.GameObjectId == player.InstanceID,
            _rotation.WorldState.Client.Flying,
            _rotation.WorldState.Party.Members[PartyState.PlayerSlot].InCutscene,
            Service.ClientState.IsPvP,
            Service.Condition.Any(Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas,
                Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51,
                Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene,
                Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78));
    }

    internal MovementCombatIntent Capture(WorldState world, AIHints hints, Actor player, NativeKnockbackPlanning? displacement = null)
    {
        var capturePhase = System.Diagnostics.Stopwatch.GetTimestamp();
        void CaptureCheckpoint(string phase)
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(capturePhase, now).TotalMilliseconds;
            if (elapsed >= 50) Service.Logger.Warning($"[CombatCaptureHitch] phase={phase} elapsed={elapsed:f1}ms job={player.Class}");
            capturePhase = now;
        }
        _evidence.ClearHealingDemand();
        // Cast completion is native evidence, including a cast started by another plugin
        // after the last world update. Strict search falls back to native recovery if
        // the cast cannot finish before a mandatory safety departure.
        var unlock = _actions.MoveMightInterruptCast ? Math.Max(0, _actions.CastTimeRemaining - 0.5f) : 0;
        if (player.CastInfo is { EventHappened: false } cast)
            unlock = Math.Max(unlock, Math.Max(0, (float)cast.RemainingTime - 0.5f));
        var config = _settings.Preferences;
        var travel = _speed.Capture(player, world.CurrentZone, world.CurrentTime, world.Client.MoveSpeed);
        var stationaryOnly = StationaryTargetPlanningRequested;
        var enabled = _settings.MayAssist && config.ManageMovement || stationaryOnly;
        if (enabled && displacement?.RecoveryReason is { } unavailable)
        {
            ActionStatus = unavailable;
            return new([], unlock, false, Travel: travel, NativeRecoveryReason: unavailable);
        }
        if (enabled && displacement?.Forced is { } physical)
        {
            ActionStatus = "Native knockback transaction; original event window and observed completion";
            return new([], 0, true, Travel: travel, ForcedMovement: physical);
        }
        if (enabled && displacement?.Ready != true && CaptureKnockbackAuthority(world, player) is { } knockback)
        {
            ActionStatus = knockback.Reason;
            return new([], unlock, false, Travel: travel, NativeRecoveryReason: knockback.Reason);
        }
        if (enabled && CompulsoryMovementProtected)
        {
            var forced = NativeForcedMovementCapture.Capture(world, _rotation.Bossmods.ActiveModule,
                player, config.FixBmrForcedMarchHeading && !_ai.ForbidActions && _forcedMarch.Paused);
            if (stationaryOnly || forced == null || forced.Routes.IsEmpty)
            {
                ActionStatus = "Native compulsory march projection unavailable; retaining native movement";
                return new([], unlock, false, Travel: travel, NativeRecoveryReason: ActionStatus);
            }
            ActionStatus = "Native compulsory march and onward route";
            return new([], unlock, true, Travel: travel, ForcedMovement: forced);
        }
        if (!enabled || !NativeCombatEngagement.Active(world, hints, player) || _ai.ForbidActions || !_rsr.IsAutorotationActive)
        {
            _healingDelivery.Reset();
            _deliveryEvidence = null;
            ActionStatus = "Native rotation goals";
            return new([], unlock, enabled, Travel: travel);
        }
        CaptureCheckpoint("current state and authority");
        var evidence = CaptureActionEvidence(world, player);
        CaptureCheckpoint("selected action");
        // No selected attack is expected while out of range. Still capture
        // independently enabled mobility and the native combat-range objective.
        PlanningAction? action = null;
        ActionStatus = evidence == null ? _evidence.Status : "Native rotation goals";
        if (evidence != null)
        {
            var eligible = evidence.Friendly ? world.Party.WithoutSlot().Select(ActorValue).ToArray()
                : hints.PriorityTargetsSpan.ToArray().Select(e => ActorValue(e.Actor)).ToArray();
            var forbidden = evidence.Friendly ? [] : hints.ForbiddenTargetsSpan.ToArray().Select(e => ActorValue(e.Actor)).ToArray();
            var positional = config.ManagePositionals && !evidence.Friendly
                ? evidence.Positional : Positional.Any;
            // Existing desired boss orientation remains in its original native goal.
            // Do not invent an angular tolerance or reinterpret that opaque goal as an
            // authored safety constraint. Typed facing requirements carry their own tolerance.
            var latest = player.Class == Class.NIN && evidence.ActionId == 2267 && player.FindStatus((uint)NIN.SID.Mudra) is { } mudra
                ? Math.Min(MovementPlanningContext.MaximumHorizonSeconds, Math.Max(0, (float)(mudra.ExpireAt - world.CurrentTime).TotalSeconds))
                : MovementPlanningContext.MaximumHorizonSeconds;
            action = RsrExecutionCapture.Create(evidence, ActorValue(player), eligible, forbidden,
                latest, positional, null, out var reason, horizonLimited: true,
                improveCoverage: !stationaryOnly && (evidence.Friendly ? config.ManageHealerCoverageZone : config.ManageAoePackPositioning),
                improveTargetChoice: config.PickBetterAoeTarget);
            ActionStatus = $"Selected action {evidence.ActionId}: {reason}; shape {action?.Opportunity?.Shape}, range {evidence.Range}, effect {evidence.EffectRange}, positional {positional}";
            if (action != null)
            {
                action = action with { Timing = CaptureCastTiming(world, player, evidence) };
                action = _targeting.AttachChoices(action, evidence);
            }
        }
        CaptureCheckpoint("action geometry and target choices");
        if (stationaryOnly)
        {
            // Target permission does not grant movement, utility, positional or
            // healing optimization. Native recovery still owns any required move.
            return action is { UsesHardTarget: true, Opportunity: { } opportunity } selectedTargetAction
                && Targeting.NativeOffensiveTargeting.SupportsTargetChoice(opportunity)
                ? new([selectedTargetAction], unlock, true, Travel: travel, StationaryOnly: true)
                : new([], unlock, false, Travel: travel, StationaryOnly: true);
        }
        PlanningAction? immediate = null;
        if (action != MeleeRecoveryPreference.ForPlanning(player.Class, action))
        {
            // Ranged fallback cannot reserve a stationary delivery ahead of a
            // safe melee recovery. RSR retains its normal action selection while
            // the movement owner restores melee access.
            action = null;
            ActionStatus += "; prefer verified melee recovery over ranged fallback";
        }
        if (_frameImmediateEvidence is { } partyAbility)
        {
            immediate = RsrExecutionCapture.Create(partyAbility, ActorValue(player),
                partyAbility.Friendly ? world.Party.WithoutSlot().Select(ActorValue).ToArray()
                    : hints.PriorityTargetsSpan.ToArray().Select(e => ActorValue(e.Actor)).ToArray(),
                partyAbility.Friendly ? [] : hints.ForbiddenTargetsSpan.ToArray().Select(e => ActorValue(e.Actor)).ToArray(), MovementPlanningContext.MaximumHorizonSeconds,
                Positional.Any, null, out _, horizonLimited: true, improveCoverage: config.ManageHealerCoverageZone);
            if (immediate != null)
            {
                immediate = immediate with { Timing = CaptureCastTiming(world, player, partyAbility) };
                ActionStatus += $"; immediate action {partyAbility.ActionId}, recipients {partyAbility.AffectedRecipients.Length}";
            }
        }
        var fieldMayWeave = _evidence.FieldActionMayWeave(player.Class, evidence, _frameImmediateEvidence);
        CaptureCheckpoint("immediate action");
        var healing = config.ManageHealerCoverageZone ? _evidence.CaptureHealingCoverage(world, player) : null;
        CaptureCheckpoint("configured healing need");
        if (player.Role == Role.Healer && config.ManageHealerCoverageZone) ActionStatus += "; " + _evidence.HealingStatus;
        PlanningRange? packRange = null;
        if (player.Class == Class.SGE && config.ManageAoePackPositioning && healing == null && evidence?.Friendly != true
            && _frameImmediateEvidence?.Friendly != true)
        {
            packRange = _targeting.CaptureSagePackRange(Math.Max(0, _actions.GCD()), out var packReason);
            ActionStatus += "; " + packReason;
            if (action != NativeSagePackApproach.ForPlanning(player.Class, action, packRange, player.Position))
            {
                action = null;
                ActionStatus += "; ranged filler yields to pack approach; current native cast lock retained";
            }
        }
        CaptureCheckpoint("pack range");
        var prepared = CapturePreparation(world, hints, player, evidence, _evidence.EnabledActions(player.Class, originalIds: true));
        CaptureCheckpoint("preparation and saved action choices");
        if (_preparation.Active && !prepared.IsEmpty) { immediate = null; action = null; }
        var original = NativeActionSequence.Compose(immediate, action);
        // An already selected setup supplies the same effect; never forecast a duplicate.
        if (original.Any(a => prepared.Any(p => p.Id == a.Id))) prepared = [];
        var sequence = original.AddRange(prepared.Select(a => a with
        { RequiredEarlierActions = (byte)((a.RequiredEarlierActions << original.Length) | NativeActionSequence.CompleteMask(original.Length)) }));
        var mobility = NativeMobilityCapture.Capture(world, hints, player, _actions, config, _evidence.EnabledActions(player.Class),
                _utilitySelection.Available, _mobilitySelection.Owned.Union(_rotationalDelivery.Owned), travel, _leyLinesTiming.PlanningField(world.CurrentTime),
                fieldMayWeave, displacement?.Ready == true ? _knockbackSelection.AvailableAction : 0, sequence);
        if (!mobility.PreparationAvailable)
        {
            sequence = original;
            mobility = NativeMobilityCapture.Capture(world, hints, player, _actions, config, _evidence.EnabledActions(player.Class),
                _utilitySelection.Available, _mobilitySelection.Owned.Union(_rotationalDelivery.Owned), travel,
                _leyLinesTiming.PlanningField(world.CurrentTime), fieldMayWeave,
                displacement?.Ready == true ? _knockbackSelection.AvailableAction : 0, sequence);
        }
        if (world.CurrentZone is 1252 or 1346)
            ActionStatus += $"; Phantom mobility owned [{string.Join(',', _mobilitySelection.Owned.Where(NativePhantomMobility.Contains).Order())}], available [{string.Join(',', mobility.Actions.Select(a => a.ActionId).Concat(mobility.Utilities.Select(u => u.ActionId)).Where(NativePhantomMobility.Contains).Order())}]";
        CaptureCheckpoint("mobility and saved action choices");
        sequence = NativeRotationalPreparation.Apply(sequence, mobility.PreparationGrants, mobility.PreparationCosts)
            .Select(a => NativeRotationalMobility.WithReadyAlternatives(a, mobility)!).ToImmutableArray();
        return new(sequence, unlock, enabled, mobility,
            player.FindStatus(1250) is { } trueNorth ? Math.Max(0, (float)(trueNorth.ExpireAt - world.CurrentTime).TotalSeconds) : 0, travel,
            CaptureDepartureMargin(player, evidence?.Recast ?? 0), FieldPlacement: action != null && fieldMayWeave ? _leyLinesPlacement.Capture() : null,
            RangeRecovery: packRange ?? CaptureCombatRange(hints, player, Math.Max(0, _actions.GCD())), HealingCoverage: healing,
            MovementValue: PlanningMovementValue.Capture(player.Class, _actions.GCD(), sequence),
            HealingDelivery: CaptureHealingDelivery(world, player, evidence));
    }

    private HealingDeliveryRequest? CaptureHealingDelivery(WorldState world, Actor player, RsrActionEvidence? selected)
    {
        if (player.Role != Role.Healer || !_settings.Preferences.ManageHealerCoverageZone)
        {
            _healingDelivery.Reset(); _deliveryEvidence = null;
            return null;
        }
        bool Healing(RsrActionEvidence? value) => value is { Friendly: true } && NativeHealingActions.Supported(value.ActionId);
        var evidence = Healing(_frameImmediateEvidence) ? _frameImmediateEvidence : Healing(selected) ? selected
            : player.Class == Class.SGE ? _evidence.CaptureSageShield(_actions, selected) : null;
        var worldCast = player.CastInfo is { EventHappened: false } cast ? cast : null;
        var casting = worldCast?.Action.ID ?? (_actions.CastTimeRemaining > 0 ? _actions.CastAction.ID : 0);
        var remaining = Math.Max(worldCast == null ? 0 : (float)worldCast.RemainingTime, _actions.CastTimeRemaining);
        if (evidence == null && _deliveryEvidence?.ActionId == casting && remaining > 0)
            evidence = _deliveryEvidence with { ReadyIn = 0, CastTime = remaining };
        _deliveryEvidence = evidence;
        var action = evidence == null ? null : RsrExecutionCapture.Create(evidence, ActorValue(player),
            world.Party.WithoutSlot().Select(ActorValue).ToArray(), [], MovementPlanningContext.MaximumHorizonSeconds,
            Positional.Any, null, out _, horizonLimited: true, improveCoverage: false);
        // The native ready/cast/animation timings and RSR's observed queue window
        // bound the effect handoff. This is never a renewable arbitrary timeout.
        var completion = evidence == null ? 0 : evidence.ReadyIn + evidence.CastTime + evidence.AnimationLock + evidence.QueueAhead;
        return _healingDelivery.Update(world.CurrentTime, player.InstanceID, world.CurrentZone, player.Class,
            player.Position, action, completion, casting, remaining);
    }

    internal float FollowTargetRange(Actor player, Actor target, float fixedRange)
    {
        // This is the identified generic follow goal, not an encounter or rotation
        // goal. A melee-only follow radius must not compete with the selected
        // ranged action. Exact action geometry is captured by the joint planner.
        if (!_settings.MayAssist || !_settings.Preferences.ManageMovement || _ai.ForbidActions
            || !_rsr.IsAutorotationActive || !NativeCombatEngagement.Active(_rotation.WorldState, _rotation.Hints, player))
            return fixedRange;
        foreach (var enemy in _rotation.Hints.PriorityTargetsSpan)
            if (enemy.Actor == target)
                return CombatFollowRange.Resolve(player.Role, fixedRange);
        return fixedRange;
    }

    internal static PlanningRange? CaptureCombatRange(AIHints hints, Actor player, float readyIn = 0)
    {
        var targetId = hints.ForcedTarget?.InstanceID ?? player.TargetID;
        foreach (var enemy in hints.PriorityTargetsSpan)
        {
            var target = enemy.Actor;
            if (target.InstanceID != targetId || target.IsDead || target.IsDestroyed || !target.IsTargetable) continue;
            var range = player.Role is Role.Melee or Role.Tank
                ? AssistancePreferences.InternalMeleeUptimeRange : AssistancePreferences.InternalRangedUptimeRange;
            return new(targetId, target.Position, range + player.HitboxRadius + target.HitboxRadius, RecoverExecution: true, RecoveryReadyIn: readyIn);
        }
        return null;
    }

    private float? CaptureDepartureMargin(Actor player, float gcd)
    {
        if (NativeMechanicTiming.PrefersEarlyClearance(player.Class)) return null;
        if (_movement.IsMoving() || _rotation.MovementPlanning.Execution?.Kind == MovementExecutionKind.Walk) return null;
        var settings = _settings.Preferences;
        // Keep the native activation cushion. The additional margin accounts for
        // two observed control frames; it is not an IPC freshness or query limit.
        var reaction = Math.Max(0.05f, 2 * _rotation.WorldState.Frame.DurationRaw);
        if (player.Class == Class.BLM && (settings.BlackMageLateMovement
            || settings.ManageLeylines && settings.GreedyLeyLines && player.FindStatus(738) != null))
            return Math.Max(0.35f, reaction);
        return settings.CombatStyle switch
        {
            CombatStyle.GreedGCD => Math.Max(gcd, reaction),
            CombatStyle.GreedLastMoment => reaction,
            _ => null
        };
    }

    private PlanningCastTiming CaptureCastTiming(WorldState world, Actor player, RsrActionEvidence evidence)
    {
        var settings = _settings.Preferences;
        var enabled = _settings.MayAssist && settings.ManageMovement;
        var timing = _leyLinesTiming.Capture(world, player, evidence, PlanningCastTiming.Capture(world, player, evidence),
            enabled && settings.ManageLeylines, settings.ReturnToLeylines);
        return _pictomancerTiming.Capture(world, player, _zones.StarryMuse(player), evidence, timing,
            enabled && settings.ManagePictomancerStarryMuse, settings.UsePictomancerStarryMuseSmudge);
    }

    internal MovementExecutionObservation Observe(Actor player)
    {
        var last = _rotation.LastCast;
        return new(player.Rotation, last.Data?.Action.ID ?? 0, last.Data?.MainTargetID ?? 0, last.Time, player.TargetID,
            _actions.EffectiveAnimationLock > 0);
    }

    internal void BeginNativeUpdate()
    {
        var preferences = _settings.Preferences;
        var enabled = _settings.MayAssist && preferences.ManageMovement;
        _corrections.UpdateHallways(enabled && preferences.FixBmrDawntrailHallwayModules);
        _rotation.Hints.ProjectionSourcesRequested = enabled
            && (preferences.ManageKnockback || preferences.PlanKnockbackLandings);
        _rotation.Hints.NativeStackCarrierSeparationRequested = enabled && preferences.FixBmrStackSeparation;
        _rotation.Hints.NativeFateFrameAlignmentRequested = enabled && preferences.FixBmrFateArenaFrame;
        _rotation.Hints.NativeTargetIntentCollectionRequested = TargetingAllowed() && _targeting.Enabled;
    }

    internal void BeforeRotationUpdate()
    {
        var updatePhase = System.Diagnostics.Stopwatch.GetTimestamp();
        void UpdateCheckpoint(string phase)
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(updatePhase, now).TotalMilliseconds;
            if (elapsed >= 50) Service.Logger.Warning($"[AssistanceUpdateHitch] phase={phase} elapsed={elapsed:f1}ms");
            updatePhase = now;
        }
        _knockbackObserver.Update(_rotation.Bossmods.ActiveModule, _settings.MayAssist && Service.ClientState.IsLoggedIn
            && !Service.ClientState.IsPvP && !Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.DutyRecorderPlayback]);
        var marchPlayer = _rotation.WorldState.Party.Player();
        var movementActive = AIManager.Instance?.Beh != null || _rotation.Preset != null && _rotation.Preset != RotationModuleManager.ForceDisable;
        _forcedMarch.Update(movementActive && _settings.MayAssist && _settings.Preferences.ManageMovement
            && (_settings.Preferences.FixBmrForcedMarchFacing || _settings.Preferences.FixBmrForcedMarchHeading) && marchPlayer is { IsDead: false, MountId: 0 }
            && HasMovementAuthority(marchPlayer, _rotation.Hints) && !Service.ClientState.IsPvP,
            _rotation.WorldState, _rotation.Bossmods.ActiveModule, marchPlayer);
        UpdateCheckpoint("knockback and march");
        if (!_forcedMarch.OwnsRotation && !CompulsoryMovementProtected && DateTime.UtcNow >= _nextLinkedRotation)
        {
            _nextLinkedRotation = DateTime.UtcNow.AddMilliseconds(250);
            var current = _rotation.WorldState.Party.Player();
            var nativeActive = AIManager.Instance?.Beh != null || _rotation.Preset != null && _rotation.Preset != RotationModuleManager.ForceDisable;
            _linkedRotation.Update(_settings.MayAssist, _settings.Preferences.LinkRotationSolverToggle,
                nativeActive && !_ai.ForbidActions, !HasCombatAuthority(current));
        }
        UpdateCheckpoint("linked rotation");
        _raises.Update();
        UpdateCheckpoint("raise");
        _support.Update();
        UpdateCheckpoint("support recipients");
        _meleeFallback.Update();
        UpdateCheckpoint("melee fallback");
        _utilitySelection.Update();
        _knockbackSelection.Update();
        _mobilitySelection.Update(marchPlayer?.Class ?? Class.None);
        UpdateCheckpoint("utility and mobility selection");
        _castControl.Update(_rotation.MovementPlanning.Execution?.Kind == MovementExecutionKind.Walk);
        if (_tankRecovery.Enabled && MobilityAllowed() && marchPlayer is { Role: Role.Tank })
        {
            var tankAction = _evidence.Capture(_actions, marchPlayer.Class);
            _tankRecovery.Update(tankAction, _evidence.NoSelectedAction, _evidence.EnabledActions(marchPlayer.Class));
        }
        else _tankRecovery.Reset();
        _tankStance.Update();
        UpdateCheckpoint("cast control and tank support");
        var player = _rotation.WorldState.Party.Player();
        _questTargeting.Update(_settings.MayAssist && _settings.Preferences.FixQuestionableQuestTargeting
            && !_ai.ManualTarget && player != null && HasCombatAuthority(player) && !CompulsoryMovementProtected
            && !Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.DutyRecorderPlayback],
            player, _actions.AnimationLockDelayEstimate, _movement.IsMoving());
        _leyLinesPlacement.Update();
        UpdateCheckpoint("quest targeting and field placement");
        _targeting.Update();
        _selectionScopes.Refresh();
        UpdateCheckpoint("targeting and selection scopes");
        if (SupportAllowed() && player is { InCombat: true } && _settings.Preferences.ManagePositionals
            && CaptureActionEvidence(_rotation.WorldState, player) is { } selected)
            // Native following and the joint opportunity must use the same GCD.
            _rotation.Hints.RSRDesiredPositional = selected.Friendly ? Positional.Any
                : MeleeRecoveryPreference.DesiredPositional(selected.Positional, selected.ReadyIn + selected.CastTime,
                    player.FindStatus(1250) is { } north ? (float)(north.ExpireAt - _rotation.WorldState.CurrentTime).TotalSeconds : 0);
        UpdateCheckpoint("selected action evidence");
    }

    private RsrActionEvidence? CaptureActionEvidence(WorldState world, Actor player)
    {
        if (_evidenceFrame != world.CurrentTime || _evidencePlayer != player.InstanceID)
        {
            _evidenceFrame = world.CurrentTime;
            _evidencePlayer = player.InstanceID;
            _frameEvidence = _evidence.Capture(_actions, player.Class);
            _frameImmediateEvidence = _evidence.CaptureImmediatePlanningAction(_actions, player.Class);
            _frameComboIntent = player.Class == Class.RDM ? _evidence.CaptureRedMageIntent(_frameEvidence,
                world.Client.ComboState.Action, world.Client.ComboState.Remaining, world.CurrentZone) : null;
        }
        return _frameEvidence;
    }

    private bool DeferRotationalTrueNorth()
    {
        var host = _rotation.MovementPlanning;
        var plan = host.CurrentPlan;
        if (plan is not { HasVerifiedRoute: true, Status: MovementSearchStatus.Complete or MovementSearchStatus.Bounded }
            || host.Execution is null or { Kind: MovementExecutionKind.Invalidated }
            || System.Diagnostics.Stopwatch.GetElapsedTime(plan.Input.CapturedTimestamp).TotalSeconds is < 0 or >= 0.25
            || _rotation.WorldState.Party.Player() is not { } player)
            return false;
        var current = _evidence.Capture(_actions, player.Class);
        return current != null && plan.ActionEvidence.Any(a => a.Id == current.ActionId && a.TargetId == current.TargetId
            && current.AffectedRecipients.All(id => a.RequiredRecipientIds.Contains(id)));
    }

    private bool SupportAllowed()
        => _settings.MayAssist && !_ai.ForbidActions && _rsr.IsAutorotationActive
            && (AIManager.Instance?.Beh != null || _rotation.Preset != null && _rotation.Preset != RotationModuleManager.ForceDisable)
            && _rotation.WorldState.Party.Player() is { IsDead: false, MountId: 0 } player
            && HasCombatAuthority(player) && !CompulsoryMovementProtected;

    internal bool ResolveTargetIntent() => _targeting.ResolveTargetIntent();
    internal bool ApplyPlannedTarget(MovementExecution execution, MovementPlan plan, MovementPlanningContext context,
        int stage, out string reason) => _targeting.PublishPlannedTarget(execution, plan, context, stage, out reason);
    internal void ConsiderPlacement(MovementPlan? plan, MovementPlanningContext context)
    {
        _leyLinesPlacement.Consider(plan, context);
        if (plan != null) _knockbackSelection.Consider(plan, context);
    }
    internal bool MayCommitPlacement(MovementPlan plan) => _leyLinesPlacement.MayCommit(plan) && _knockbackSelection.MayCommit(plan);
    internal void ObservePlacementExecution(MovementPlan plan, MovementPlanningContext context, int stage, MovementExecution execution)
    {
        ObserveMovementExecution(plan, context, stage);
        _leyLinesPlacement.ObserveExecution(plan, context, stage, execution);
        _knockbackSelection.ObserveExecution(plan, context, stage, execution);
    }
    internal void ObserveMovementExecution(MovementPlan plan, MovementPlanningContext context, int stage)
        => _movementFrame = (plan, context, stage);
    internal string KnockbackSelectionStatus => $"{_knockbackSelection.Status}; tank slow: {_tankArmsLengthStatus}";
    internal string PlacementStatus => _leyLinesPlacement.Status;
    internal Targeting.OffensiveTargetDecision? TargetDecision => _targeting.Decision;
    internal string TargetStatus => $"{_targeting.Status}; callback: {_selectionScopes.Status}; authority: {TargetingBlockReason() ?? "allowed"}";

    private bool TargetingAllowed() => TargetingBlockReason() == null;

    private string? TargetingBlockReason()
    {
        if (!_settings.MayAssist) return "assistance inactive";
        if (_ai.ForbidActions) return "native actions forbidden";
        if (!_rsr.IsAutorotationActive) return "RSR inactive";
        if (AIManager.Instance?.Beh == null && (_rotation.Preset == null || _rotation.Preset == RotationModuleManager.ForceDisable))
            return "native AI/preset inactive";
        if (_rotation.WorldState.Party.Player() is not { IsDead: false, MountId: 0 } player) return "player unavailable, dead or mounted";
        if (!HasCombatAuthority(player)) return "native player/zone authority";
        if (Service.ClientState.IsPvP) return "PvP";
        if (CompulsoryMovementProtected) return "compulsory movement";
        var world = _rotation.WorldState;
        return NativeCombatEngagement.Active(world, _rotation.Hints, player)
            || FateTargetPolicy.HasAcquisition(_rotation.Hints, world.Client.ActiveFate.ID,
                Utils.IsPlayerSyncedToFate(world), world.Client.ActiveFate.Progress)
                ? null : "no native player/party engagement or permitted current FATE acquisition";
    }

    private bool CastControlAllowed()
        => SupportAllowed() && _settings.Preferences.ManageMovement && _settings.Preferences.ManageCastControl
            && !CompulsoryMovementProtected && !Service.ClientState.IsPvP
            && _rotation.WorldState.Party.Player() is { } player
            && NativeCombatEngagement.Active(_rotation.WorldState, _rotation.Hints, player) && player.FindStatus(418) == null;

    private NativeKnockbackAuthority? CaptureKnockbackAuthority(WorldState world, Actor player)
    {
        var module = _rotation.Bossmods.ActiveModule;
        if (_knockbackFrame != world.CurrentTime || _knockbackActor != player.InstanceID || !ReferenceEquals(module, _knockbackModule))
        {
            _knockbackFrame = world.CurrentTime; _knockbackActor = player.InstanceID; _knockbackModule = module;
            _displacementCapture = NativeDisplacementCapture.Capture(world, module, player, _rotation.Hints);
            _knockbackPhases = null;
            _knockbackAuthority = NativeKnockbackAuthorityCapture.Capture(_displacementCapture.Snapshot);
            _knockbackObserver.Track(_displacementCapture.Snapshot);
        }
        return _knockbackAuthority;
    }
    internal NativeKnockbackPhases? CaptureKnockbackPhases(WorldState world, AIHints hints, Actor player,
        Pathfinding.NavigationDecision.CapturedInput input, IEnumerable<BossMod.Pathfinding.NativePhysicalObstacle>? observedBlocks)
    {
        _ = CaptureKnockbackAuthority(world, player);
        if (!_settings.MayAssist || !_settings.Preferences.ManageMovement || !hints.ProjectionSourcesRequested
            || _displacementCapture == null) return null;
        return _knockbackPhases ??= NativeKnockbackPhaseCapture.Freeze(_displacementCapture, hints,
            _rotation.Bossmods.ActiveModule, player, input, observedBlocks);
    }

    private bool OrdinaryMovementForecastAvailable()
        => _rotation.WorldState.Party.Player() is { } player && CaptureKnockbackAuthority(_rotation.WorldState, player) == null && !_knockbackTransaction.Active && !CompulsoryMovementProtected;

    private bool MobilityAllowed()
        => SupportAllowed() && _settings.Preferences.ManageMovement && !CompulsoryMovementProtected && !Service.ClientState.IsPvP
            && _rotation.WorldState.Party.Player() is { } player
            && HasMovementAuthority(player, _rotation.Hints)
            && NativeCombatEngagement.Active(_rotation.WorldState, _rotation.Hints, player) && player.FindStatus(418) == null;

    private bool StationaryRotationalMobility(uint id, object action)
    {
        var definition = NativeMobilityCapture.Definitions.FirstOrDefault(d => d.Id == id);
        var player = _rotation.WorldState.Party.Player();
        var target = _rotation.WorldState.Actors.Find(_evidence.TargetOf(action));
        return definition != null && player != null && target is { IsDead: false, IsDestroyed: false }
            && NativeMobilityCapture.NoDisplacement(definition, player.Position, player.HitboxRadius, target.Position, target.HitboxRadius);
    }

    internal Control.CastControlDecision? CastControlDecision => _castControl.Decision;
    internal Support.TankStanceDecision? TankStanceDecision => _tankStance.Decision;
    internal string TankStanceStatus => _tankStance.Status;
    internal Support.TankRecoveryDecision? TankRecoveryDecision => _tankRecovery.Decision;
    internal string TankRecoveryStatus => _tankRecovery.Status;
    internal string RotationMode => _modes.Read()?.ToString() ?? "Unavailable";

    internal Support.SupportRecipientSnapshot? SupportSnapshot => _support.Snapshot;
    internal string SupportStatus => _support.Status + "; Ley Lines: " + _leyLinesPlacement.Status
        + "; rotational movement: " + _rotationalDelivery.Status;
    internal string HealingStatus => _evidence.HealingStatus;
    internal string? SupportAvailability => _support.AvailabilityWarning;

    private bool CameraAllowed()
        => _settings.MayAssist && _settings.Preferences.ManageMovement
            && (AIManager.Instance?.Beh != null || Autorotation.MiscAI.NormalMovement.Instance != null)
            && _rotation.WorldState.Party.Player() is { InCombat: true } player
            && HasMovementAuthority(player, _rotation.Hints) && !Service.GameGui.GameUiHidden;

    internal void UpdatePresentation()
    {
        ApplyKnockbackCastWindow();
        var allowed = CameraAllowed();
        _cameraMovement.Update(allowed && _settings.Preferences.CombatCamera,
            _movement.IsMoveRequested() || _movement.IsForceUnblocked() || _movement.NativeInput.ManualFacing, DateTime.UtcNow);
        _camera.Update(allowed ? Camera.CameraScene.Capture(_rotation.WorldState, _rotation.Hints,
            _rotation.Bossmods.ActiveModule?.StateMachine.ActiveState != null) : null);
        if (_settings.MayAssist && !Service.GameGui.GameUiHidden
            && _rotation.WorldState.Party.Player() is { IsDead: false, MountId: 0 } player
            && HasMovementAuthority(player, _rotation.Hints))
            _overlay.Draw(_rotation.MovementPlanning, player);
    }

    internal float[] CapturePresentation(Pathfinding.Map map, AIHints hints, WorldState world, Actor player, float[] native)
        => _settings.MayAssist && _settings.Preferences.ManageMovement
            ? NativePresentationCapture.Capture(map, hints, world, player, _settings.Preferences, native,
                _rotation.Bossmods.ActiveModule?.StateMachine.ActiveState != null, _formation.Capture(world, hints, player,
                    _settings.Preferences.ManageNaturalDestinations && HasMovementAuthority(player, hints)),
                _socialSpacing.Capture(map, hints, world, player, _settings.Preferences, HasMovementAuthority(player, hints))) : native;

    internal PlanningMechanicClearance? CaptureMechanicClearance(Pathfinding.Map map, AIHints hints, WorldState world, Actor player, bool dodging)
    {
        if (!_settings.MayAssist || !_settings.Preferences.ManageMovement || !_settings.Preferences.ManageForbiddenZoneDistance) return null;
        var clearance = PlanningMechanicClearance.Capture(map, hints, _settings.Preferences.PreferredForbiddenZoneDistance);
        return clearance == null ? null : clearance with
        {
            PartyRegion = dodging && _settings.Preferences.ManageHealerCoverageZone
                ? _evidence.CaptureHealingEscapePosture(world, player) : null
        };
    }

    internal ImmutableArray<PlanningEnemyFootprint> CaptureEnemyFootprints(AIHints hints, Actor player)
        => _settings.MayAssist && _settings.Preferences.ManageMovement && _settings.Preferences.AvoidStandingInsideEnemies
            ? NativePresentationCapture.CaptureEnemyFootprints(hints, player) : [];

    internal PlanningArenaInterior? CaptureArenaInterior(Pathfinding.Map map, AIHints hints)
        => NativePresentationCapture.CaptureArenaInterior(map, hints,
            _settings.MayAssist && _settings.Preferences.ManageMovement && _settings.Preferences.AvoidArenaEdge
                && _rotation.Bossmods.ActiveModule?.StateMachine.ActiveState != null);

    internal ImmutableArray<PlanningZone> CaptureZones(Actor player, AIHints hints)
        => _zones.Capture(player, hints, _settings.Preferences,
            _settings.MayAssist && _settings.Preferences.ManageMovement && HasMovementAuthority(player, hints),
            _leyLinesTiming.ObserveField(_rotation.WorldState, player,
                _settings.MayAssist && _settings.Preferences.ManageMovement && _settings.Preferences.ManageLeylines));

    internal PlanningRange? CapturePartyContact(WorldState world, AIHints hints, Actor player)
    {
        var settings = _settings.Preferences;
        var dungeon = world.CurrentCFCID != 0
            && Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(world.CurrentCFCID)?.ContentType.RowId == 2;
        var (tank, engaged) = SelectDungeonTank(world, hints, player);
        var permission = new PartyContactAuthority(_settings.MayAssist && settings.ManageMovement,
            HasMovementAuthority(player, hints), _ai.FollowSlot == PartyState.PlayerSlot
                && (AIManager.Instance?.MasterSlot ?? PartyState.PlayerSlot) == PartyState.PlayerSlot,
            player.InCombat, settings.MaintainPullContact);
        var radius = player.Role switch
        {
            Role.Melee => 6,
            Role.Healer => 10,
            _ => player.Class is Class.BLM or Class.THM or Class.RDM or Class.SMN or Class.ACN or Class.PCT or Class.BLU ? 12 : 14
        };
        return _partyContact.Observe(new(world.CurrentTime, tank?.InstanceID ?? 0, tank?.Position ?? default,
            engaged, dungeon,
            _rotation.Bossmods.ActiveModule is not SimpleBossModule && _rotation.Bossmods.ActiveModule?.StateMachine.ActiveState != null,
            permission.Allowed, player.Role == Role.Tank, tank?.InCombat == true, world.CurrentZone, player.InstanceID), player.Position, radius);
    }

    private static (Actor? Tank, int Engaged) SelectDungeonTank(WorldState world, AIHints hints, Actor player)
    {
        var targets = hints.PriorityTargetsSpan.ToArray();
        var owners = world.Party.WithoutSlot(excludeAlliance: true)
            .Where(a => a.Role == Role.Tank && !a.IsDead && !a.IsDestroyed && a.InstanceID != player.InstanceID)
            .Select(a => (Actor: a, Count: targets.Count(e => e.Actor.InCombat && e.Actor.TargetID == a.InstanceID)))
            .OrderByDescending(a => a.Count).ToArray();
        return owners.Length == 1 || owners.Length > 1 && owners[0].Count > owners[1].Count
            ? (owners[0].Actor, owners[0].Count) : (null, 0);
    }

    internal PlanningComboRecovery? CaptureComboRecovery(WorldState world, AIHints hints, Actor player)
    {
        if (!_settings.MayAssist || !_settings.Preferences.ManageMovement || !_settings.Preferences.UseRedMageMeleeComboMovement
            || player.Class != Class.RDM || !player.InCombat || player.MountId != 0 || _ai.ForbidActions || !HasMovementAuthority(player, hints)
            || !_rsr.IsAutorotationActive || _frameComboIntent == null)
            return null;
        return NativeComboPreparation.Capture(_frameComboIntent, ActorValue(player),
            hints.PriorityTargetsSpan.ToArray().Select(e => ActorValue(e.Actor)).ToArray(),
            hints.ForbiddenTargetsSpan.ToArray().Select(e => ActorValue(e.Actor)).ToArray(),
            Math.Min(EnablingEffectUntil(_frameComboIntent.Swordplay, (uint)RDM.SID.MagickedSwordplay),
                EnablingEffectUntil(_frameComboIntent.Embolden, (uint)RDM.SID.Embolden)));

        float EnablingEffectUntil(bool observed, uint status) => !observed ? float.MaxValue
            : player.FindStatus(status) is { } effect ? Math.Max(0, (float)(effect.ExpireAt - world.CurrentTime).TotalSeconds) : 0;
    }

    internal Angle? CaptureDowntimeFacing(WorldState world, AIHints hints, Actor player)
    {
        var party = world.Party.WithoutSlot(excludeAlliance: true);
        return _partyFacing.Observe(world.CurrentTime, DowntimeFacingAllowed(player, hints),
            party.Where(a => a.InstanceID != player.InstanceID && a.Type == ActorType.Player && !a.IsDead && !a.IsDestroyed
                && (a.Position - player.Position).LengthSq() <= 35 * 35).Select(a => a.Rotation),
            world.Party.Members.Take(PartyState.MaxPartySize).Count(m => m.IsValid()) >= 8 ? 4 : 2, player.Rotation);
    }

    internal void ReleasePresentation()
    {
        _healingDelivery.Reset(); _deliveryEvidence = null;
        _movementFrame = null; _meleeRecoveryWindow.Reset(); _lastGcdDelivery = default; _preparation.Reset();
        _rotationalDelivery.ResetPlan();
        _leyLinesPlacement.RetirePlanning();
        _knockbackSelection.ResetPlan();
        _leyLinesTiming.Reset();
        _pictomancerTiming.Reset();
        _partyContact.Reset();
        _partyFacing.Reset();
        _formation.Reset();
        _socialSpacing.Reset();
    }

    private bool DowntimeFacingAllowed(Actor player, AIHints hints)
    {
        var module = _rotation.Bossmods.ActiveModule;
        var timeline = module?.StateMachine;
        var downtime = PartyFacingPreference.EligibleDowntime(module != null, !hints.PriorityTargetsSpan.IsEmpty,
            timeline?.ActiveState != null ? timeline.NextTransitionWithFlag(StateMachine.StateHint.DowntimeStart) : DateTime.MaxValue,
            timeline?.ActiveState != null ? timeline.NextTransitionWithFlag(StateMachine.StateHint.DowntimeEnd) : DateTime.MaxValue);
        return _settings.MayAssist && _settings.Preferences.ManageSocialTurning && !_ai.ForbidActions && downtime
            && player.CastInfo == null && player.PendingKnockbacks.Count == 0 && !CompulsoryMovementProtected
            && !hints.ForbiddenDirections.Any(d => d.activation <= _rotation.WorldState.FutureTime(0.5))
            && PlanningExecutionRules.Capture(_rotation.WorldState, hints).PermitsMovement(0, 0.5f);
    }

    internal Camera.CombatCameraStatus CameraStatus => _camera.Status;

    internal void ApplyFacing(MovementExecution execution, Actor player, AIHints hints)
    {
        if (execution.Kind == MovementExecutionKind.Face && execution.Facing is { } facing && HasMovementAuthority(player, hints)
            && (!CompulsoryMovementProtected || execution.ForcedMovementFacing && _forcedMarch.Paused
                && _settings.Preferences.FixBmrForcedMarchHeading && _modes.Read() == RsrMode.Off) && !_ai.ForbidActions && !hints.ForbiddenDirections.Any(d => d.activation <= _rotation.WorldState.FutureTime(0.5)
                && facing.AlmostEqual(d.center, d.halfWidth.Rad)))
        {
            if (execution.PresentationFacing)
            {
                if (!DowntimeFacingAllowed(player, hints)) return;
                // Cosmetic turns are gradual and never extend across a stalled frame.
                var step = 180f.Degrees().Rad * Math.Clamp(_rotation.WorldState.Frame.Duration, 0, 0.05f);
                facing = player.Rotation + Math.Clamp((facing - player.Rotation).Normalized().Rad, -step, step).Radians();
            }
            _actions.FaceDirection(facing);
        }
    }

    internal unsafe bool MaySubmitPlannedAction(MovementExecution execution, MovementPlan plan, MovementPlanningContext context,
        int stage, out string? rejection)
    {
        rejection = null;
        if (_evidenceFrame == _rotation.WorldState.CurrentTime && _evidence.ImmediateSelection is { } selected
            && ActionDefinitions.Instance[new(ActionType.Spell, selected.Id)] is { } definition
            && RsrRotationalDelivery.YieldToCurrent(execution.ActionId, selected.Id, selected.Gcd,
                _actions.GetActionStatus(new(ActionType.Spell, selected.Id), selected.Target) == 0,
                definition.InstantAnimLock + _actions.AnimationLockDelayEstimate, execution.DepartureSeconds))
            return false; // Original RSR delivery gets this still-executable present opportunity; native departure remains binding.
        return _rotationalDelivery.MaySubmit(execution, plan, context, stage, out rejection);
    }

    private unsafe bool DeferMeleeFallback(uint id)
    {
        if (!SupportAllowed() || !_settings.Preferences.ManageMovement || CompulsoryMovementProtected
            || _rotation.WorldState.Party.Player() is not { Role: Role.Melee } player
            || !MeleeRecoveryPreference.IsFallback(player.Class, id) || _movementFrame is not { } frame
            || !ReferenceEquals(_rotation.MovementPlanning.CurrentPlan, frame.Plan)
            || _rotation.MovementPlanning.Execution is null or { Kind: MovementExecutionKind.Invalidated }
            || Service.ObjectTable.LocalPlayer is not { } actual || !actual.IsValid() || actual.Address == 0
            || actual.GameObjectId != player.InstanceID || _rotation.Hints.ForcedTarget?.InstanceID is { } forced && forced != frame.Context.TargetId
            || player.TargetID != frame.Context.TargetId)
            return false;
        var last = _rotation.LastCast;
        if (last.Time > _lastGcdDelivery && last.Data is { } data && ActionDefinitions.Instance[data.Action]?.IsGCD == true)
            _lastGcdDelivery = last.Time;
        var point = new WPos(actual.Position.X, actual.Position.Z);
        var arrival = NativeMeleeRecoveryWindow.VerifiedArrival(frame.Plan, frame.Context, frame.Stage, player.TargetID,
            System.Diagnostics.Stopwatch.GetTimestamp(), point);
        if (arrival == null) return false;
        var action = new ActionID(ActionType.Spell, id);
        var definition = ActionDefinitions.Instance[action];
        if (definition?.IsGCD != true) return false;
        var recast = _actions.GetAdjustedRecastTime(action) * .001f;
        var cast = _actions.GetAdjustedCastTime(action) * .001f;
        return _meleeRecoveryWindow.Defer(player.InstanceID, player.TargetID, id, _lastGcdDelivery,
            _rotation.WorldState.CurrentTime, Math.Max(0, _actions.GCD()),
            Math.Max(recast, cast + (cast > 0 ? definition.CastAnimLock : definition.InstantAnimLock)), arrival.Value, _rotation.MovementPlanning.InputRevision);
    }

    internal bool ApplyPlannedAction(MovementExecution execution, MovementPlan plan, MovementPlanningContext context,
        int stage, Actor player, AIHints hints, out string reason)
    {
        reason = "native movement or action authority changed";
        if (!_settings.MayAssist || !_settings.Preferences.ManageMovement || _ai.ForbidActions
            || CompulsoryMovementProtected || !_rsr.IsAutorotationActive || !HasMovementAuthority(player, hints) || execution.Waypoint is not { } landing)
            return false;
        var checkedRoute = MovementPlanValidation.Validate(plan, context, stage, true, continuingCommittedRoute: true);
        if (!checkedRoute.Allowed)
        {
            reason = checkedRoute.Reason;
            return false;
        }
        // Ground targeting uses an observed floor height, never the player's Y
        // pasted onto an unverified point on a different projection layer.
        var ground = new Vector3(landing.X, player.PosRot.Y, landing.Z);
        if (execution.TargetId != 0)
        {
            if (_rotation.WorldState.Actors.Find(execution.TargetId) is not { IsDestroyed: false, IsDead: false } anchor)
            {
                reason = "mobility anchor disappeared before submission";
                return false;
            }
            ground = anchor.PosRot.XYZ();
        }
        else if (plan.Stages[stage].Kind == MovementStageKind.Return)
        {
            var definition = NativeMobilityCapture.Definitions.FirstOrDefault(d => d.Id == execution.ActionId);
            var saved = _rotation.WorldState.Actors.Where(a => a.OwnerID == player.InstanceID && a.OID == definition?.ReturnObject
                && !a.IsDestroyed && a.Position.AlmostEqual(landing, 0.01f)).ToArray();
            if (saved.Length != 1)
            {
                reason = "owned return position changed before submission";
                return false;
            }
            ground = saved[0].PosRot.XYZ();
        }
        if (plan.Stages[stage].Kind == MovementStageKind.GroundMovement
            && !Pathfinding.NativeGroundObservation.TryLanding(player, landing, out ground))
        {
            reason = "exact ground destination has no verified native floor support";
            return false;
        }
        return _actions.TryExecutePlannedAction(execution, player, context.Origin, ground,
            plan.Stages[stage].Kind != MovementStageKind.UtilityAction && plan.Stages[stage].From != plan.Stages[stage].To, out reason);
    }

    private static ExecutionActor ActorValue(Actor actor)
        => new(actor.InstanceID, actor.Position, actor.PosRot.Y, actor.HitboxRadius, actor.Rotation,
            actor.IsDead || actor.IsDestroyed, actor.Omnidirectional);

    public void Dispose()
    {
        _knockbackTransaction.Release();
        _forcedMarch.Release();
        _zones.Dispose();
        _leyLinesEvents.Dispose();
        _leyLinesTiming.Reset();
        _pictomancerTiming.Reset();
        _castControl.Dispose();
        _knockbackObserver.Dispose();
        _tankStance.Dispose();
        _tankRecovery.Reset();
        _linkedRotation.Release();
        _actions.PreserveManualFacing = null;
        _leyLinesPlacement.Dispose();
        _knockbackSelection.Dispose();
        _support.Dispose();
        _targeting.Dispose();
        _rotation.Hints.NativeTargetIntentCollectionRequested = false;
        _rotation.Hints.ProjectionSourcesRequested = false;
        _displacementCapture = null;
        _knockbackPhases = null;
        _selectionScopes.Dispose();
        _rotationalDelivery.Dispose();
        _meleeFallback.Dispose();
        _utilitySelection.Dispose();
        _mobilitySelection.Dispose();
        _corrections.Dispose();
        _questTargeting.Dispose();
        _rotation.Hints.NativeStackCarrierSeparationRequested = false;
        _rotation.Hints.NativeFateFrameAlignmentRequested = false;
        _cameraMovement.Restore();
        _camera.Dispose();
        _evidence.Dispose();
    }
}
