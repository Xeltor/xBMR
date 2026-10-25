using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

// Both existing intent owners share one worker, immutable native capture and staged
// execution. Their activation, following, explicit destination and preset gates remain native.
internal sealed class NativeMovementHost : IDisposable
{
    private readonly MovementPlanningService _service;
    private readonly MovementPlanExecutor _executor = new();
    private readonly NativeRoutePhysicalObservation _physical = new();
    private readonly NativeTeleportProgress _teleportProgress = new();
    private readonly NavigationDecision.Context _stationaryInspection = new();
    private DateTime _lastOwnerUpdate;
    private MovementPlan? _committed;
    private MovementPlan? _lastCandidate;
    private NativeEscapeCommitment? _escape;
    private MovementPlanningContext? _problem;
    private string? _owner;
    private long _revision;
    private DateTime? _episodeStarted;
    private ImmutableArray<RejectedMobility> _rejectedMobility = [];
    internal MovementPlan? CurrentPlan { get; private set; }
    internal string? LastFailure { get; private set; }
    internal string? LastInputChange { get; private set; }
    internal Runtime.CombatAssistanceRuntime? Assistance { get; private set; }
    internal MovementExecution? Execution { get; private set; }
    internal bool PlansOwnerTiming { get; private set; }
    internal int StageIndex => _executor.StageIndex;
    internal long InputRevision => _revision;
    internal ImmutableArray<RejectedMobility> RejectedMobility => _rejectedMobility;
    internal MovementPlanningCost LastCost { get; private set; }
    internal MovementSearchCost? LastWorkerCost => _service.LastWorkerCost;
    private long _inputCost, _combatCost, _contextCost, _serviceCost;
    private int _costCells;
    private bool _idleSkipped;

    internal NativeMovementHost(MovementPlanningService? service = null) => _service = service ?? new();

    internal void Configure(Runtime.CombatAssistanceRuntime assistance) => Assistance = assistance;

    internal static MovementPlan? CommitCandidate(MovementPlan? candidate, MovementPlanningContext context)
    {
        if (!context.Optimize || candidate is not { HasVerifiedRoute: true } || !context.CompletePreparation(candidate.DeliveredActions)) return null;
        if (candidate.Status is MovementSearchStatus.Complete or MovementSearchStatus.Bounded) return candidate;
        // The worker publishes complete verified routes while alternatives are
        // still being searched. Do not confuse unfinished optimization with an
        // unfinished route, or bypass it with a second native-only destination.
        if (candidate.Status != MovementSearchStatus.Searching
            || !context.PreservesPresent(candidate.DeliveredActions) || !context.CompletePreparation(candidate.DeliveredActions)) return null;
        return candidate with
        {
            Status = MovementSearchStatus.Bounded,
            Bound = MovementSearchBound.SearchInProgress,
            Evidence = candidate.Evidence + "; complete verified incumbent while optimization continues"
        };
    }

    internal static bool RequestsCombatOptimization(Runtime.MovementCombatIntent? combat, MovementIntent intent)
        => intent.ExplicitDestination == null && combat is { Enabled: true, NativeRecoveryReason: null,
            UnlockSeconds: <= MovementPlanningContext.MaximumHorizonSeconds };

    internal NavigationDecision Update(string owner, WorldState world, AIHints hints, Actor player, float speed,
        float cushion, NavigationDecision.Context inspection, MovementIntent? intent = null)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        _inputCost = _combatCost = _contextCost = _serviceCost = 0;
        _costCells = 0;
        _idleSkipped = false;
        try
        {
            LastFailure = null;
            _lastOwnerUpdate = world.CurrentTime;
            var result = CaptureAndUpdate(owner, world, hints, player, speed, cushion, inspection, intent ?? new());
            LastFailure ??= _service.LastFailure;
            return result;
        }
        catch (Exception error)
        {
            // A native goal/capture failure must not escape the frame or retain an unsafe old route.
            Release(owner);
            var reason = error.GetType().Name + ": " + error.Message;
            if (reason != LastFailure)
                Service.LogVerbose($"[MovementPlanning] Capture failed: {reason}");
            LastFailure = reason;
            return NativeRecoveryAfterCaptureFailure(world, hints, player, speed, cushion, inspection, reason);
        }
        finally
        {
            var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
            var milliseconds = 1000d / System.Diagnostics.Stopwatch.Frequency;
            LastCost = new(elapsed * milliseconds, _inputCost * milliseconds, _combatCost * milliseconds,
                _contextCost * milliseconds, _serviceCost * milliseconds,
                Math.Max(0, elapsed - _inputCost - _combatCost - _contextCost - _serviceCost) * milliseconds,
                GC.GetAllocatedBytesForCurrentThread() - allocated, _costCells, _idleSkipped);
        }
    }

    internal static bool CanSkipIdleNavigation(AIHints hints, Actor player, bool encounter, bool engaged, MovementIntent intent)
        => !encounter && !engaged && !player.InCombat && player.TargetID == 0
            && intent.ExplicitDestination == null && intent.Range == null
            && ReferenceEquals(hints.PathfindMapBounds, AIHints.DefaultBounds)
            && hints.PathfindMapArenaProjectionLayer == null
            && hints.PathfindMapBounds.Contains(player.Position - hints.PathfindMapCenter)
            && hints.GoalZones.Count == 0 && hints.ForbiddenZones.Count == 0 && hints.TemporaryObstacles.Count == 0
            && hints.Teleporters.Count == 0 && hints.ForbiddenDirections.Count == 0
            && hints.ImminentSpecialMode.mode == AIHints.SpecialMode.Normal && hints.ForcedMovement == null
            && hints.InteractWithTarget == null && hints.PredictedDamage.Count == 0;

    // A preset need not contain NormalMovement to authorize stationary target
    // assistance. Reuse this host only when neither native movement owner ran.
    // The returned recovery directive is not applied by this target-only caller.
    internal void UpdateStationaryTarget(WorldState world, AIHints hints, Actor? player)
    {
        const string owner = "stationary-target";
        if (Assistance?.StationaryTargetPlanningRequested != true || player == null
            || AI.AIManager.Instance?.Beh != null)
        {
            Release(owner);
            return;
        }
        if (_lastOwnerUpdate == world.CurrentTime) return;
        _ = Update(owner, world, hints, player, world.Client.MoveSpeed, 0, _stationaryInspection);
    }

    internal NavigationDecision UpdateIngress(string owner, WorldState world, AIHints hints, Actor player, BossModule module,
        float speed, MovementIntent intent)
    {
        _lastOwnerUpdate = world.CurrentTime;
        _service.Release(_owner ?? owner);
        if (_owner != owner) _episodeStarted = null;
        _episodeStarted ??= world.CurrentTime;
        _owner = owner;
        _problem = null;
        _committed = null;
        _rejectedMobility = [];
        _executor.Release();
        CurrentPlan = null;
        Execution = null;
        PlansOwnerTiming = true;
        LastFailure = null;
        try
        {
            if (Assistance?.HasMovementAuthority(player, hints) == false || hints.AutomaticMovementPaused
                || player.IsDead || Assistance?.IngressCastLocked == true
                || world.CurrentTime < _episodeStarted.Value.AddSeconds(intent.MoveDelay))
                return new() { LeewaySeconds = float.MaxValue };
            var input = NavigationDecision.CaptureInput(world.CurrentTime, hints, player, speed, 0, world.CurrentZone, world.CurrentCFCID);
            var destination = NativeIngressRecovery.Find(world, hints, player, module, speed, out var reason);
            var decision = new NavigationDecision
            {
                Destination = destination,
                LeewaySeconds = 0,
                TimeToGoal = destination is { } point ? (point - player.Position).Length() / speed : 0
            };
            var endpoint = destination ?? player.Position;
            decision.Publication = new NavigationPublication(input.Identity, null, [player.Position, endpoint], destination != null,
                false, ThetaStar.Score.Safe, decision, false).Publish();
            CurrentPlan = new(input.Identity, MovementSearchStatus.NativeRecovery,
                destination is { } entry ? [new(MovementStageKind.Walk, player.Position, entry, 0, decision.TimeToGoal)] : [],
                endpoint, destination, 0, decision.TimeToGoal, 0, 0, [], float.MinValue, 0, 0, reason)
            { NativeRecovery = decision, TargetId = hints.ForcedTarget?.InstanceID ?? player.TargetID };
            Execution = _executor.AdvanceRecovery(CurrentPlan, input.Identity,
                Assistance?.HasMovementAuthority(player, hints) != false, destination != null);
            decision.Destination = Execution.Kind == MovementExecutionKind.Walk ? Execution.Waypoint : null;
            return decision;
        }
        catch (Exception error)
        {
            LastFailure = "Native entry unavailable: " + error.GetBaseException().Message;
            Service.LogVerbose(LastFailure);
            return new() { LeewaySeconds = float.MaxValue };
        }
    }

    private NavigationDecision NativeRecoveryAfterCaptureFailure(WorldState world, AIHints hints, Actor player,
        float speed, float cushion, NavigationDecision.Context inspection, string reason)
    {
        // A policy or frozen evaluator failure must not disable BMR's safety
        // recovery. This exceptional path uses the original framework-thread
        // implementation; ordinary optimization never blocks the framework.
        try
        {
            if (hints.AutomaticMovementPaused || player.IsDead || Assistance?.CompulsoryMovementProtected == true
                || Assistance?.HasMovementAuthority(player, hints) == false)
                return new() { LeewaySeconds = float.MaxValue };
            var input = NavigationDecision.CaptureInput(world.CurrentTime, hints, player, speed, cushion, world.CurrentZone, world.CurrentCFCID);
            var recovery = NavigationDecision.BuildCaptured(inspection, input);
            if (recovery.Publication is { } publication) recovery.Publication = publication.Publish();
            CurrentPlan = new(input.Identity, MovementSearchStatus.NativeRecovery, [],
                recovery.Publication?.Endpoint ?? player.Position, recovery.Destination, recovery.LeewaySeconds, recovery.TimeToGoal,
                0, 0, [], float.MinValue, 0, 0, "native recovery after capture failure: " + reason)
            { NativeRecovery = recovery, TargetId = hints.ForcedTarget?.InstanceID ?? player.TargetID };
            return ExecuteRecovery(CurrentPlan, input.Identity, player, hints);
        }
        catch (Exception failure)
        {
            LastFailure = reason + "; native recovery unavailable: " + failure.GetBaseException().Message;
            Service.LogVerbose("[MovementPlanning] " + LastFailure);
            return new() { LeewaySeconds = float.MaxValue };
        }
    }

    private NavigationDecision CaptureAndUpdate(string owner, WorldState world, AIHints hints, Actor player, float speed,
        float cushion, NavigationDecision.Context inspection, MovementIntent intent)
    {
        if (Assistance != null && !Service.Framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Movement capture and execution require the framework thread.");
        if (hints.AutomaticMovementPaused || player.IsDead || Assistance?.HasMovementAuthority(player, hints) == false)
        {
            Release(owner);
            return new() { LeewaySeconds = float.MaxValue };
        }
        if (Assistance != null && CanSkipIdleNavigation(hints, player, Assistance.HasEncounterModule,
            Runtime.NativeCombatEngagement.Active(world, hints, player), intent))
        {
            Release(_owner ?? owner);
            _idleSkipped = true;
            LastInputChange = "idle; no navigation or combat intent";
            Execution = new(MovementExecutionKind.Arrived, "idle; no navigation or combat intent");
            return new() { LeewaySeconds = float.MaxValue };
        }
        var phase = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_owner != owner)
        {
            _episodeStarted = null;
            _escape = null;
        }
        _episodeStarted ??= world.CurrentTime;
        var input = NavigationDecision.CaptureInput(world.CurrentTime, hints, player, speed, cushion, world.CurrentZone, world.CurrentCFCID);
        var explicitInstruction = intent.ExplicitDestination != null;
        if (intent.ExplicitDestination is { } required)
            input = input with { Goals = new([.. input.Goals.Native, AIHints.GoalProximity(required, 1000, 1000)]) };
        if (Assistance != null) _physical.Refresh(player, world.CurrentZone, input.Identity.ProjectionLayer);
        var native = NativeNavigationSnapshot.Capture(input, Assistance == null ? null : _physical.BlockedPoints);
        if (intent.ExplicitDestination != null) Assistance?.ReleaseKnockbackTransaction();
        var displacement = intent.ExplicitDestination == null
            ? Assistance?.ResolveKnockbackPlanning(owner, world, hints, player, input, native, _physical.BlockedPoints) : null;
        native = displacement?.Navigation ?? native;
        _inputCost = System.Diagnostics.Stopwatch.GetTimestamp() - phase;
        phase = System.Diagnostics.Stopwatch.GetTimestamp();
        var combat = Assistance?.Capture(world, hints, player, displacement);
        _combatCost = System.Diagnostics.Stopwatch.GetTimestamp() - phase;
        phase = System.Diagnostics.Stopwatch.GetTimestamp();
        var optimizeCombat = RequestsCombatOptimization(combat, intent);
        var optimize = optimizeCombat || explicitInstruction;
        var map = native.CopyStrictMap();
        var origin = map.WorldToGrid(player.Position);
        var dodging = map.InBounds(origin.x, origin.y) && map.PixelMaxG[map.GridToIndex(origin)] < float.MaxValue;
        var configuredDelay = intent.SeparateDodgeDelay && dodging ? intent.DodgeDelay : intent.MoveDelay;
        var remainingDelay = Math.Max(0, configuredDelay - (float)(world.CurrentTime - _episodeStarted.Value).TotalSeconds);
        var unlock = Math.Max(intent.InterruptCast ? 0 : combat?.UnlockSeconds ?? 0, remainingDelay);
        optimize &= unlock <= MovementPlanningContext.MaximumHorizonSeconds;
        optimizeCombat &= optimize;
        PlansOwnerTiming = optimize;
        if (combat?.NativeRecoveryReason is { } nativeReason)
        {
            LastInputChange = nativeReason;
            _committed = null;
            _executor.Release();
        }
        var cells = checked(map.Width * map.Height);
        _costCells = cells;
        var presentation = native.CopyPresentation();
        if (optimizeCombat)
            presentation = Assistance!.CapturePresentation(map, hints, world, player, presentation);
        var contact = optimizeCombat ? Assistance?.CapturePartyContact(world, hints, player) : null;
        var continuity = displacement?.Continuity ?? ContactContinuity(_problem?.PartyContact, contact, player.Position, _executor.CommittedDestination);
        if (RetiredContact(_problem?.PartyContact, contact))
        {
            _committed = null;
            _executor.Release();
        }
        var context = new MovementPlanningContext(input.Identity, map, 0, optimize ? unlock : 0,
            Enumerable.Repeat(true, cells).ToArray(), presentation, optimizeCombat ? combat!.Actions : [], [],
            committedDestination: continuity, native: native,
            targetId: hints.ForcedTarget?.InstanceID ?? player.TargetID, optimize: optimize, range: intent.Range ?? (optimizeCombat ? combat?.RangeRecovery : null),
            resources: optimizeCombat ? combat?.Mobility?.Resources ?? 0 : 0,
            mobilityMechanics: optimizeCombat ? combat?.Mobility?.Actions ?? [] : [],
            executionRules: PlanningExecutionRules.Capture(world, hints),
            utilities: optimizeCombat ? combat?.Mobility?.Utilities ?? [] : [],
            positionalBypassUntil: optimizeCombat ? combat?.PositionalBypassUntil ?? 0 : 0,
            travel: optimize ? combat?.Travel : null,
            downtimeFacing: optimizeCombat && combat?.StationaryOnly != true ? Assistance?.CaptureDowntimeFacing(world, hints, player) : null,
            requiredDestination: intent.ExplicitDestination, arrivalDeadline: intent.ArrivalDeadline,
            lateDepartureMargin: optimizeCombat ? combat?.LateDepartureMargin : null,
            zones: optimizeCombat ? Assistance?.CaptureZones(player, hints) : null,
            recharges: optimizeCombat ? combat?.Mobility?.Recharges ?? [] : [],
            cooldownEvidenceLimited: optimizeCombat && combat?.Mobility?.CooldownEvidenceLimited == true,
            forcedMovement: combat?.ForcedMovement,
            knockbackPhases: displacement?.Phases ?? Assistance?.CaptureKnockbackPhases(world, hints, player, input, _physical.BlockedPoints),
            teleports: _teleportProgress.Observe(input.Identity, native.Teleports),
            comboRecovery: optimizeCombat ? Assistance?.CaptureComboRecovery(world, hints, player) : null,
            partyContact: contact, healingCoverage: optimizeCombat ? combat?.HealingCoverage : null,
            personalField: optimizeCombat ? combat?.Mobility?.PersonalField : null,
            fieldPlacement: optimizeCombat && combat?.StationaryOnly != true ? combat?.FieldPlacement : null,
            stationaryOnly: optimizeCombat && combat?.StationaryOnly == true,
            knockbackCompletion: Assistance?.KnockbackCompletion, takeOwnership: true);
        if (_owner != owner || _problem == null || !context.SameProblem(_problem, _committed != null, ignoreRejections: true))
            _rejectedMobility = [];
        context = context.Excluding(_rejectedMobility);
        if (_owner != owner || _problem == null || !context.SameProblem(_problem, _committed != null))
        {
            if (_owner != owner)
            {
                _committed = null;
                _executor.Release();
            }
            _owner = owner;
            LastInputChange = _problem == null ? "new owner" : context.ProblemChange(_problem, _committed != null);
            _problem = context;
            ++_revision;
            // Reconsider changed inputs without first stopping an ongoing valid
            // route. Advance below checks the entire remainder against this NEW
            // snapshot before any directive is applied, retaining the 250ms ceiling.
        }
        _contextCost = System.Diagnostics.Stopwatch.GetTimestamp() - phase;
        phase = System.Diagnostics.Stopwatch.GetTimestamp();
        _service.Submit(owner, _revision, context);
        CurrentPlan = _service.Poll(owner, _revision, world.CurrentTime);
        _serviceCost = System.Diagnostics.Stopwatch.GetTimestamp() - phase;
        Assistance?.ConsiderPlacement(CurrentPlan, context);
        // Diagnostic/special-mode readers own a frame-local map; no worker writes this buffer.
        inspection.Map = native.CopyRecoveryMap(inspection.Map);
        Execution = null;
        if (optimize)
        {
            var escaping = explicitInstruction ? null : _escape?.Continue(context);
            if (escaping == null) _escape = null;
            else _committed = escaping;
            var candidate = CurrentPlan;
            if (candidate != null && Assistance?.MayCommitPlacement(candidate) == false)
            {
                // Keep preparation in the one worker. Native recovery and any
                // still-valid committed route retain execution until RSR approval.
                candidate = candidate.NativeRecovery is { } recovery
                    ? candidate with { Status = MovementSearchStatus.NativeRecovery, NativeRecovery = recovery }
                    : null;
                CurrentPlan = candidate;
            }
            if (escaping == null && !_executor.AwaitingActionEffect && !ReferenceEquals(candidate, _lastCandidate))
            {
                _lastCandidate = candidate;
                if (CommitCandidate(candidate, context) is { } verified
                    && !(_committed != null && verified.Input == _committed.Input && verified.Stages == _committed.Stages))
                    _committed = verified;
            }
            if (_committed != null)
            {
                _committed = _executor.RebindInitialMobility(_committed, context);
                var execution = _executor.Advance(_committed, context, Assistance?.Observe(player) ?? new(player.Rotation),
                    Assistance?.HasMovementAuthority(player, hints) != false);
                if (execution.Kind == MovementExecutionKind.Walk && Assistance != null
                    && !_physical.Check(player, _committed.Stages.Skip(_executor.StageIndex)))
                {
                    LastFailure = _physical.LastReason;
                    Execution = new(MovementExecutionKind.Invalidated, LastFailure ?? "observed physical obstruction");
                    _committed = null;
                    _executor.Release();
                    // The next snapshot feeds this observation into BOTH native
                    // recovery and optimization. Never apply the rejected move.
                    return new() { LeewaySeconds = float.MaxValue };
                }
                if (execution.Kind == MovementExecutionKind.Target)
                {
                    var targetRejection = "Native target assistance is unavailable";
                    if (Assistance == null || !Assistance.ApplyPlannedTarget(execution, _committed, context, _executor.StageIndex, out targetRejection))
                    {
                        execution = new(MovementExecutionKind.Invalidated, targetRejection);
                        _executor.Release();
                    }
                }
                if (execution.Kind == MovementExecutionKind.Wait && execution.ActionId != 0 && Assistance != null
                    && Integrations.RsrRotationalDelivery.IsPreparation(_committed, context, _executor.StageIndex)
                    && !Assistance.MaySubmitPlannedAction(execution, _committed, context, _executor.StageIndex, out var preparationRejection)
                    && preparationRejection != null)
                {
                    var rejected = _committed.Stages[_executor.StageIndex];
                    _rejectedMobility = _rejectedMobility.Add(new(rejected.ActionId, rejected.AnchorId, context.Origin, rejected.To,
                        rejected.Facing, preparationRejection));
                    execution = new(MovementExecutionKind.Invalidated, preparationRejection);
                    _executor.Release();
                }
                if (execution.Kind == MovementExecutionKind.Action && Assistance != null
                    && !Assistance.MaySubmitPlannedAction(execution, _committed, context, _executor.StageIndex, out var selectionRejection))
                {
                    if (selectionRejection == null)
                        execution = new(MovementExecutionKind.Wait, "waiting for original RSR movement-GCD selection",
                            DepartureSeconds: execution.DepartureSeconds);
                    else
                    {
                        var rejected = _committed.Stages[_executor.StageIndex];
                        _rejectedMobility = _rejectedMobility.Add(new(rejected.ActionId, rejected.AnchorId, context.Origin, rejected.To,
                            rejected.Facing, selectionRejection));
                        execution = new(MovementExecutionKind.Invalidated, selectionRejection);
                        _executor.Release();
                    }
                }
                if (execution.Kind == MovementExecutionKind.Action
                    && Assistance != null && !Assistance.ApplyPlannedAction(execution, _committed, context, _executor.StageIndex, player, hints, out var rejection))
                {
                    var stage = _committed.Stages[_executor.StageIndex];
                    _rejectedMobility = _rejectedMobility.Add(new(stage.ActionId, stage.AnchorId, context.Origin, stage.To, stage.Facing, rejection));
                    execution = new(MovementExecutionKind.Invalidated, rejection);
                    _executor.Release();
                }
                else if (execution.Kind == MovementExecutionKind.Action && Assistance != null)
                    _executor.ConfirmActionSubmission(_committed);
                if (execution.Kind != MovementExecutionKind.Invalidated)
                {
                    if (execution.Kind == MovementExecutionKind.Walk)
                        _escape ??= NativeEscapeCommitment.Begin(context, _committed, _executor.StageIndex);
                    CurrentPlan = _committed;
                    Execution = execution;
                    if (execution.Kind == MovementExecutionKind.Arrived)
                        _episodeStarted = null;
                    Assistance?.ObserveKnockbackPlan(_committed);
                    Assistance?.ObservePlacementExecution(_committed, context, _executor.StageIndex, execution);
                    Assistance?.ApplyFacing(execution, player, hints);
                    var selected = new NavigationDecision
                    {
                        Destination = execution.Kind == MovementExecutionKind.Walk ? execution.Waypoint : null,
                        LeewaySeconds = execution.DepartureSeconds,
                        TimeToGoal = Math.Max(0, _committed.DurationSeconds - (float)(world.CurrentTime - _committed.Input.WorldTime).TotalSeconds)
                    };
                    var witness = new[] { context.Origin }.Concat(_committed.Stages.Skip(_executor.StageIndex)
                        .SelectMany(s => s.Forced != null ? s.Forced.Legs.Select(l => l.To) : s.Transit == null ? new[] { s.To } : new[] { s.Transit.Entrance, s.To })).ToArray();
                    if (!execution.SubmissionPending)
                        selected.Publication = new NavigationPublication(context.Identity, null, witness, true, false,
                            ThetaStar.Score.Safe, selected, _committed.Stages.Any(s => s.Kind == MovementStageKind.Teleport)).Publish();
                    return selected;
                }
                LastFailure = execution.Reason;
                _escape = null;
                Execution = execution;
                _committed = null;
            }
        }
        // No fully revalidated optimized directive is available. A previous
        // worker's positive leeway cannot authorize waiting under new hazards.
        return RecoverWithoutWorker(context, inspection, player, hints);
    }

    private NavigationDecision RecoverWithoutWorker(MovementPlanningContext context, NavigationDecision.Context inspection,
        Actor player, AIHints hints)
    {
        // A pending, cancelled or expired optimizer result cannot suspend native
        // safety. Use the same freshly rasterized map and original ThetaStar now;
        // this is native recovery work, never a framework wait for optimization.
        if (context.ForcedMovement != null || Assistance?.CompulsoryMovementProtected == true)
            return new() { LeewaySeconds = float.MaxValue };
        CurrentPlan = NativeMovementSearch.Recover(context, inspection) with
        {
            Evidence = "fresh native recovery while optimization has no executable publication"
        };
        if (context.Optimize && CurrentPlan.NativeRecovery is { LeewaySeconds: > 0 } pending
            && !CanRecoverCombatRange(context, CurrentPlan))
        {
            // A fresh native calculation proves how long optimization may still
            // continue. Recompute against every changed mechanic; never borrow
            // the old worker's deadline. Mandatory departure takes precedence.
            Execution = new(MovementExecutionKind.Wait, "fresh native departure permits further optimization", DepartureSeconds: pending.LeewaySeconds);
            return new() { LeewaySeconds = pending.LeewaySeconds };
        }
        var decision = ExecuteRecovery(CurrentPlan, context.Identity, player, hints, context.Native);
        if (Execution?.Kind == MovementExecutionKind.Walk && CurrentPlan?.NativeRecovery?.Publication is { } escapePublication
            && NativeMovementIncumbent.Capture(context, escapePublication) is { } escapePlan)
            _escape ??= NativeEscapeCommitment.Begin(context, escapePlan, 0);
        if (CurrentPlan != null && Execution is { Kind: not MovementExecutionKind.Invalidated })
            Assistance?.ObserveMovementExecution(CurrentPlan, context, 0);
        return decision;
    }

    internal static bool CanRecoverCombatRange(MovementPlanningContext context, MovementPlan recovery)
        => context.Actions.IsEmpty && context.UnlockSeconds == 0 && context.Range is { } range && range.NeedsRecovery(context.Origin)
            && range.Loss(recovery.UsefulDestination) < range.Loss(context.Origin)
            // Elective fallback must not start a center/crowd-directed route
            // while the worker is still choosing its useful arrival. Urgent
            // native departure bypasses this optional-wait check above.
            && context.Cell(recovery.UsefulDestination) is var destination && destination >= 0
            && context.Cell(context.Origin) is var origin && origin >= 0
            && context.Presentation(destination) >= context.Presentation(origin)
            && recovery.NativeRecovery?.Publication is { } publication
            && NativeMovementIncumbent.Capture(context, publication) is { HasVerifiedRoute: true };

    private NavigationDecision ExecuteRecovery(MovementPlan plan, NavigationIdentity identity, Actor player, AIHints hints,
        NativeNavigationSnapshot? native = null)
    {
        if (plan.NativeRecovery is not { } decision || decision.Publication is not { } publication)
            return new() { LeewaySeconds = float.MaxValue };
        if (_owner == "stationary-target")
        {
            _committed = null;
            _executor.Release();
            Execution = new(MovementExecutionKind.Wait, "Target-only assistance yields to required native movement");
            return new() { LeewaySeconds = float.MaxValue };
        }
        if (Assistance != null && decision.Destination is { } destination
            && !_physical.Check(player, [new(MovementStageKind.Walk, player.Position, destination, 0, decision.TimeToGoal)]))
        {
            LastFailure = _physical.LastReason;
            Execution = new(MovementExecutionKind.Invalidated, LastFailure ?? "observed physical obstruction");
            return new() { LeewaySeconds = float.MaxValue };
        }
        _committed = null;
        Execution = _executor.AdvanceRecovery(plan, identity, Assistance?.HasMovementAuthority(player, hints) != false,
            native?.CanExecuteRecovery(publication, player.Position) != false);
        if (Execution.Kind == MovementExecutionKind.Invalidated && native != null
            && Assistance?.HasMovementAuthority(player, hints) != false)
        {
            // Never wait for optional optimization when the actual native egress
            // changed. This is the existing native safety workload, on fresh input.
            decision = native.Recover(new());
            plan = plan with
            {
                Input = identity,
                NativeRecovery = decision,
                UsefulDestination = decision.Publication?.Endpoint ?? player.Position,
                ImmediateWaypoint = decision.Destination,
                PlanSlackSeconds = decision.LeewaySeconds,
                DurationSeconds = decision.TimeToGoal,
                Status = MovementSearchStatus.NativeRecovery,
                OptimizationPending = false,
                Evidence = "fresh native recovery after route revalidation"
            };
            CurrentPlan = plan;
            Execution = _executor.AdvanceRecovery(plan, identity, true,
                decision.Publication is { } refreshed && native.CanExecuteRecovery(refreshed, player.Position));
        }
        decision.Destination = Execution.Kind == MovementExecutionKind.Walk ? Execution.Waypoint : null;
        decision.LeewaySeconds = Execution.DepartureSeconds;
        decision.Publication = decision.Publication?.Publish();
        return decision;
    }

    internal static bool RetiredContact(PlanningRange? previous, PlanningRange? current)
        => previous != null && (current == null || previous.TargetId != current.TargetId);

    internal static WPos? ContactContinuity(PlanningRange? previous, PlanningRange? current, WPos origin, WPos? committed)
        => RetiredContact(previous, current) ? origin : committed;

    internal void Release(string owner)
    {
        _service.Release(owner);
        if (_owner != owner)
            return;
        Assistance?.ReleaseKnockbackTransaction();
        Assistance?.ReleasePresentation();
        _owner = null;
        _episodeStarted = null;
        _rejectedMobility = [];
        _teleportProgress.Reset();
        PlansOwnerTiming = false;
        _problem = null;
        CurrentPlan = null;
        _lastCandidate = null;
        _escape = null;
        _committed = null;
        Execution = null;
        _executor.Release();
    }

    public void Dispose()
    {
        _service.Dispose();
        _teleportProgress.Reset();
        Assistance?.Dispose();
        _executor.Release();
        _committed = null;
        Execution = null;
        _problem = null;
        _escape = null;
        CurrentPlan = null;
        _owner = null;
        _episodeStarted = null;
        _rejectedMobility = [];
        PlansOwnerTiming = false;
    }
}
