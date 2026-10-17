using BossMod.AI;
using BossMod.Autorotation;
using BossMod.Pathfinding;
using System.Globalization;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator(BossModuleManager bossmod, ZoneModuleManager zonemod, AIHints hints,
    RotationModuleManager rotation, ActionManagerEx amex, MovementOverride movement, AIManager ai) : IDisposable
{
    internal readonly IntegrationSession Session = new();
    private readonly IntegrationActionEffects _effects = new(bossmod.WorldState);
    private WorldState World => bossmod.WorldState;
    private Vector3? _encounterMovement;
    private DateTimeOffset _observedUtc;
    private IntegrationHintEvidence? _nativeHintEvidence;
    private sealed record TargetIntent(AIHints.Enemy Entry, WPos? Position, Angle? Rotation, string Source);
    private TargetIntent[] _authoredTargetIntent = [];
    internal void BeginWorldUpdate()
    {
        Session.BeginWorldUpdate();
        RetireDistanceSources();
        hints.StackCarrierSeparationPolicy = _stackCorrection;
        movement.IntegrationPacing?.Clear();
        hints.ProjectionSourcesRequested = Session.HasActiveOwners;
        hints.ProjectionSourcesContext = null;
        _nativeHintEvidence = null;
        _effects.BeginUpdate();
    }

    internal void HintsReady()
    {
        BeginPacingFrame();
        var player = World.Party.Player();
        _encounterMovement = hints.ForcedMovement;
        var targetIntentSource = bossmod.ActiveModule?.StateMachine.ActivePhase != null ? "encounter-hints"
            : zonemod.ActiveModule != null ? "zone-hints" : "automatic-hints";
        _authoredTargetIntent = hints.PotentialTargets.Take(AIHints.NumEnemies)
            .Select(t => new TargetIntent(t, t.DesiredPosition, t.DesiredRotation, targetIntentSource)).ToArray();
        _observedUtc = DateTimeOffset.UtcNow;
        Session.BeginFrame(ControlScope(),
            player is { IsDead: false } && !movement.IsMoveRequested() && !hints.AutomaticMovementPaused);
        RetireDistanceSources();
        _nativeHintEvidence = new(Session.CurrentContext, hints.GoalZones.Count);
        hints.ProjectionSourcesContext = hints.ProjectionSourcesValid ? Session.CurrentContext : null;
        hints.IntegrationGoals = new(Session, hints.GoalZones.ToArray());
        if (_safetyJobs != null) lock (_safetyGate) PruneSafetySnapshots();
        Session.Publish("hints-ready", Snapshot);
    }

    internal void MovementProposed()
    {
        _ = hints.IntegrationConstraints?.Capture();
        Session.Publish("movement-proposed", Snapshot);
        FinalizePace();
    }
    internal void MovementApplied()
    {
        ObserveApplicationBoundary();
        ArmPace();
        Session.Publish("movement-applied", Snapshot);
    }

    internal string Capabilities() => IntegrationSession.Serialize(new
    {
        schemaVersion = IntegrationSession.SchemaVersion,
        instanceGeneration = Session.InstanceGeneration,
        families = new Dictionary<string, int>
        {
            ["automatic-input-pacing"] = 1, ["cast-departure-hold"] = 1, ["native-arrival-input-hold"] = 1, ["native-follow-arrival-input-hold"] = 1, ["native-physical-input-brake"] = 1, ["native-follow-physical-input-brake"] = 1, ["native-physical-step-observation"] = 1, ["controller-input-provenance"] = 1, ["frame-stages"] = 1, ["goal-scoring"] = 1, ["native-goal-no-loss"] = 1, ["native-goal-matrix"] = 1, ["movement-observation"] = 1,
            ["cast-control-observation"] = 1, ["native-constraint-distances"] = 1, ["native-aoe-forecast-distances"] = 1, ["target-policy"] = 1, ["predicted-damage"] = 1, ["action-effects"] = 1, ["arena-geometry"] = 1, ["owned-fate-frame"] = 1, ["owned-duty-ingress"] = 1,
            ["native-route-search"] = 1, ["nearest-safe-raster-search"] = 1,
            ["navigation-publication"] = 1, ["navigation-raster"] = 1, ["walk-route-validation"] = 1,
            ["mechanic-observation"] = 1, ["native-stack-participation"] = 1, ["current-native-projection"] = 1,
            ["counterfactual-projection-geometry"] = 1,
            ["native-knockback-landing-constraints"] = 1,
            ["native-goal-presence"] = 1, ["dash-policy-observation"] = 1, ["target-aggro-observation"] = 1,
            ["module-catalog"] = 1, ["static-module-registry"] = 1, ["native-recording-evidence"] = 1, ["native-forced-march-safety"] = 1, ["native-forced-march-heading"] = 1, ["native-stack-carrier-separation"] = 1, ["native-actor-pose-knockback"] = 1, ["native-strict-safety-snapshots"] = 1, ["native-safety-route-witnesses"] = 1, ["owned-projected-constraints"] = 1, ["snapshot-explicit-segment-validation"] = 1, ["module-enablement-leases"] = 1, ["transient-strategy-leases"] = 1, ["configuration-leases"] = 1, ["directional-angle-scopes"] = 1, ["follow-control"] = 1
        },
        limits = new { owners = IntegrationSession.MaxOwners, goalsPerOwner = IntegrationSession.MaxGoalsPerOwner,
            leaseMs = IntegrationSession.MaxLeaseMs, frameAgeMs = IntegrationSession.MaxFrameAgeMs, queryPoints = IntegrationSession.MaxQueryPoints,
            rasterCells = NavigationRaster.MaxCells, rasterWindowCells = NavigationRaster.MaxWindowCells,
            searchJobs = 4, runningSearches = 1, searchCells = IntegrationSearch.MaxCells,
            automaticPaceLeaseMs = IntegrationPacing.MaxLeaseMs, automaticPaceMinScale = IntegrationPacing.MinScale,
            noLossLeaves = IntegrationSession.MaxNoLossLeaves, noLossBaselines = IntegrationSession.MaxNoLossBaselines,
            nativeGoals = IntegrationNativeGoals.MaxGoals, nativeGoalPoints = IntegrationNativeGoals.MaxPoints,
            counterfactualPoses = IntegrationCounterfactualProjection.MaxPoses, nativeKnockbackPoses = 32, nativeKnockbackPoseCalls = 8,
            nativeKnockbackSourceRecords = 128, nativeKnockbackWalls = 512, nativeKnockbackResponseCharacters = 262144,
            projectionConstraintZones = 128, projectionConstraintTreeNodes = 128, projectionConstraintTreeDepth = 8,
            routes = IntegrationRoutes.MaxRoutes, routePoints = IntegrationRoutes.MaxPoints, routeEvaluations = 8192, routeBudgetMs = 4 },
        unsupported = new[] { "continuous-nearest-safe-search", "arena-route-ingress", "mechanic-projection", "validated-movement-preferences", "owned-constraint-native-projection-approval" }
    });

    internal string GetSnapshot(string request) => WithContext(request, Snapshot);
    internal string GetLatestSnapshot() => Session.LatestPublication is { } json ? json : Unavailable("no-fresh-publication");
    private string WithContext(string request, Func<IntegrationSession.Context, string> read)
    {
        var context = IntegrationSession.Parse<IntegrationSession.Context>(request);
        if (!Session.Matches(context))
            return Unavailable("stale-or-invalid-context");
        try { return read(context!); }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Observation failed: {error.GetType().Name}: {error.Message}");
            return Unavailable("native-observation-unavailable");
        }
    }
    private string Unavailable(string reason) => IntegrationSession.Serialize(new { available = false, reason, context = Session.CurrentContext });
    private static float? Finite(float value) => float.IsFinite(value) && value != float.MaxValue ? value : null;
    private static float[]? V(Vector3? value) => value is { } v && float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) ? [v.X, v.Y, v.Z] : null;
    private static float[]? P(WPos? value) => value is { } v && float.IsFinite(v.X) && float.IsFinite(v.Z) ? [v.X, v.Z] : null;
    private static string T(DateTime value) => value.Ticks.ToString(CultureInfo.InvariantCulture);

    private object? InputObservation()
    {
        var sample = movement.IntegrationInput.Latest;
        return sample == null ? null : new
        {
            generation = sample.Generation, sample.ObservedUtc, sample.Kind,
            ageSeconds = (System.Diagnostics.Stopwatch.GetTimestamp() - sample.ObservedTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency,
            userDirection = new[] { sample.UserDirection.X, sample.UserDirection.Y },
            actualDirection = new[] { sample.ActualDirection.X, sample.ActualDirection.Y },
            outputDirection = new[] { sample.OutputDirection.X, sample.OutputDirection.Y },
            sample.FollowPath, sample.Misdirection, sample.AutomaticPaceScale, sample.PacingProofId, sample.Automatic,
            matchingAssignment = AssignmentObservation(sample.MatchingAssignment),
            meaning = "last completed native walk input observation; native assignment identity captured before this read must remain unchanged and direction-matched"
        };
    }

    private string Snapshot(IntegrationSession.Context context)
    {
        var player = World.Party.Player();
        var module = bossmod.ActiveModule;
        var (navigation, navigationOwner) = CurrentNavigation();
        if (navigation?.Source.IntegrationInstance != Session.InstanceGeneration)
            navigation = null;
        return IntegrationSession.Serialize(new
        {
            available = player != null && Session.Matches(context),
            context,
            observationUtc = DateTimeOffset.UtcNow,
            hintsObservedUtc = _observedUtc,
            worldTime = new { ticks = T(World.CurrentTime), kind = World.CurrentTime.Kind.ToString() },
            hintGeneration = context.FrameId,
            navigationGeneration = navigation?.Source.Generation,
            navigationHintGeneration = navigation?.Source.HintGeneration,
            navigationBuildTime = navigation?.BuildCompletedUtc,
            navigationPublicationTime = navigation?.PublishedUtc,
            navigationOwner,
            navigationMeaning = "retained-native-search-result; not proof of the final applied route",
            playerId = player?.InstanceID.ToString("X"),
            playerPosition = P(player?.Position),
            encounterForcedDirection = V(_encounterMovement),
            finalProposedDirection = context.Stage == "hints-ready" ? null : V(hints.EffectiveForcedMovement),
            appliedOverrideDirection = context.Stage == "movement-applied" ? V(movement.DesiredDirection) : null,
            applicationMeaning = "override-assigned; physical movement is a separate last-input-read observation",
            lastInputRead = InputObservation(),
            overrideAssignment = context.Stage == "movement-applied" ? AssignmentObservation(movement.IntegrationInput.CurrentAssignment) : null,
            controllerDecision = ControllerObservation(),
            moveRequested = movement.IsMoveRequested(),
            isMoving = movement.IsMoving(),
            movementBlocked = movement.MovementBlocked,
            automaticMovementPaused = hints.AutomaticMovementPaused,
            controllerTarget = P(ai.Controller.NaviTargetPos),
            nativeGoalNoLoss = hints.IntegrationGoals?.NoLossObservation,
            stackCarrierSeparation = new { currentBuildEnabled = hints.StackCarrierSeparationActive, nextBuildEnabled = _stackCorrection?.Enabled == true },
            nativeControllerDecision = hints.IntegrationMovementDecision is { } decision ? new
            {
                decision.Owner, decision.FrameId, decision.InstanceGeneration, decision.ControllerDecisionId,
                followMasterId = decision.FollowMasterId?.ToString("X"), followMasterPosition = V(decision.FollowMasterPosition), sourceNavigationGeneration = decision.Navigation.Source.Generation,
                origin = P(decision.Origin), destination = P(decision.Destination), direction = V(decision.Direction),
                matchesCurrentHints = hints.EffectiveForcedMovement == decision.Direction
            } : null,
            automaticPacing = _pendingPace is { Ticket.DepartureHold: false } pending ? new { proofId = pending.Ticket.Id, scale = pending.Ticket.Scale,
                status = System.Threading.Volatile.Read(ref pending.Ticket.State) == 2 ? "revoked" : _paceResult,
                reason = _paceResult,
                frameId = pending.Ticket.Frame, sourceNavigationGeneration = pending.Ticket.NavigationGeneration } : null,
            controllerLeewaySeconds = Finite(ai.Controller.NaviLeewaySeconds),
            automaticDepartureHold = _pendingPace is { Ticket.DepartureHold: true } hold ? new
            {
                proofId = hold.Ticket.Id, sourceNavigationGeneration = hold.Ticket.NavigationGeneration,
                requestedLeaseMs = hold.Request.LeaseMs, kind = hold.Kind.Length == 0 ? "departure-hold" : hold.Kind,
                status = System.Threading.Volatile.Read(ref hold.Ticket.State) == 2 ? "revoked" : _paceResult,
                state = System.Threading.Volatile.Read(ref hold.Ticket.State),
                meaning = "bounded zero automatic input on the exact native assignment; no cast or gameplay approval"
            } : null,
            forceMovementInSeconds = Finite(ai.ForceMovementIn),
            routeTravelSeconds = navigation == null ? null : Finite(navigation.TimeToGoal),
            canInterruptCast = ai.Controller.AllowInterruptingCastByMovement,
            castRemainingSeconds = Finite(amex.CastTimeRemaining),
            moveMightInterruptCast = amex.MoveMightInterruptCast,
            maxCastTimeSeconds = Finite(hints.MaxCastTime),
            holdCasts = hints.HoldCasts,
            cancelCastMechanic = hints.ForceCancelCastMechanic,
            cancelCastOther = hints.ForceCancelCastOther,
            cancelCastMechanicAI = ai.Controller.ForceCancelCastMechanicAI,
            cancelCastOtherAI = ai.Controller.ForceCancelCastOtherAI,
            preset = rotation.Preset?.Name,
            followActive = ai.Beh != null,
            followMasterId = ai.Beh != null ? World.Party[ai.MasterSlot]?.InstanceID.ToString("X") : null,
            followPathOwner = ai.Beh != null ? "bmr-follow" : "unknown",
            interactionTargetId = hints.InteractWithTarget?.InstanceID.ToString("X"),
            specialMode = hints.ImminentSpecialMode.mode.ToString(),
            specialModeActivationTicks = T(hints.ImminentSpecialMode.activation),
            goalZonesEnabled = hints.GoalZonesEnabled,
            nativeHintGoalCount = _nativeHintEvidence?.CountFor(context),
            hasNativeHintGoals = _nativeHintEvidence?.HasGoalsFor(context),
            nativeHintGoalScope = IntegrationHintEvidence.GoalScope,
            nativeHintGoalObservedStage = "hints-ready",
            forbidDashes = hints.ForbidDashes,
            forbiddenZones = hints.ForbiddenZones.Count,
            forbiddenDirections = hints.ForbiddenDirections.Count,
            temporaryObstacles = hints.TemporaryObstacles.Count,
            zoneId = World.CurrentZone,
            contentId = World.CurrentCFCID,
            selectedModule = module?.GetType().FullName,
            activePhaseOwnsPositioning = module?.StateMachine.ActiveState != null,
            zoneModule = zonemod.ActiveModule?.GetType().FullName
        });
    }

    private string TargetIntentSource(AIHints.Enemy target, bool rotation)
    {
        var authored = _authoredTargetIntent.FirstOrDefault(t => ReferenceEquals(t.Entry, target));
        if (rotation ? target.DesiredRotation == null : target.DesiredPosition == null)
            return "none";
        if (authored != null && (rotation ? target.DesiredRotation == authored.Rotation : target.DesiredPosition == authored.Position))
            return authored.Source;
        return "post-hints-producer-or-unknown";
    }

    internal string GetTargets(string request) => WithContext(request, context => IntegrationSession.Serialize(new
    {
        available = true, context,
        defaultDesiredRotation = "unset-unless-natively-requested",
        automaticSelfFacingCopied = false,
        forcedTargetId = hints.ForcedTarget?.InstanceID.ToString("X"),
        highestPriority = hints.HighestPotentialTargetPriority,
        complete = hints.PotentialTargets.Count <= AIHints.NumEnemies,
        targets = hints.PotentialTargets.Take(AIHints.NumEnemies).Select(t => new
        {
            actorId = t.Actor.InstanceID.ToString("X"), position = P(t.Actor.Position), rotation = Finite(t.Actor.Rotation.Rad),
            hitboxRadius = Finite(t.Actor.HitboxRadius), t.Priority, t.ShouldBeTargeted, t.Actor.PendingDead, t.Actor.IsTargetable,
            t.ForbidDOTs, t.ShouldBeTanked, t.PreferProvoking, t.CanMove, desiredPosition = P(t.DesiredPosition), desiredRotation = t.DesiredRotation is { } a ? Finite(a.Rad) : null,
            desiredRotationProvenance = TargetIntentSource(t, rotation: true), desiredPositionProvenance = TargetIntentSource(t, rotation: false),
            targetRequirement = t.ExplicitTargetRequirement, requirementMeaning = "null=producer-has-not-specified; true=required; false=explicitly-not-required",
            targetId = t.Actor.TargetID.ToString("X"), t.Actor.FateID, t.ShouldBeInterrupted, t.ShouldBeStunned,
            aggroPlayer = IntegrationHintEvidence.AggroPlayer(t.Actor),
            cast = IntegrationCastControl.ObserveCast(t.Actor.CastInfo, World.CurrentTime)
        }).ToArray()
    }));

    internal string GetPredictedDamage(string request) => WithContext(request, context =>
        World.Party.Player() == null ? Unavailable("missing-player") : IntegrationSession.Serialize(
            IntegrationDamage.Capture(World.Party, hints.PredictedDamage, World.CurrentTime, context)));

    internal string EvaluateNativeGoals(string request)
    {
        var query = IntegrationSession.Parse<IntegrationSession.Query>(request);
        if (query == null || !Session.Matches(query.Context) || query.Points == null || query.Points.Length > IntegrationSession.MaxQueryPoints
            || query.Points.Any(p => p == null || p.Length != 2 || !float.IsFinite(p[0]) || !float.IsFinite(p[1])))
            return Unavailable("invalid-query");
        return IntegrationSession.Serialize(new
        {
            available = true, context = query.Context, enabled = hints.GoalZonesEnabled,
            scope = query.Context.Stage == "hints-ready" ? "encounter-and-zone-before-rotation" : "all-current-native-goals",
            scores = query.Points.Select(p => Finite(hints.GoalZones.Sum(g => g(new(p[0], p[1]))))).ToArray()
        });
    }

    internal string GetActionEffects(string request) => WithContext(request, context => IntegrationSession.Serialize(new
    {
        available = true, context, publication = _effects.Capture(),
        delivery = "current-world-update; observation order; duplicates retained; no history"
    }));

    private (NavigationPublication? Publication, string Owner) CurrentNavigation()
        => ai.Beh != null ? (ai.Beh.PublishedNavigation, "bmr-follow-async")
            : (Autorotation.MiscAI.NormalMovement.Instance?.PublishedNavigation, "bmr-normal-movement");

    private NavigationPublication? QueryNavigation(IntegrationSession.Context? context, long generation)
    {
        if (!Session.Matches(context))
            return null;
        var player = World.Party.Player();
        if (player is not { IsDead: false })
            return null;
        var (publication, _) = CurrentNavigation();
        return publication?.Matches(Session.InstanceGeneration, generation, player.InstanceID, World.CurrentZone, World.CurrentCFCID) == true ? publication : null;
    }

    internal sealed record NavigationQuery(IntegrationSession.Context Context, long Generation);
    internal sealed record RasterQuery(IntegrationSession.Context Context, long Generation, int Left, int Top, int Width, int Height);

    internal string GetNavigationSnapshot(string request)
    {
        var query = IntegrationSession.Parse<NavigationQuery>(request);
        var p = query == null ? null : QueryNavigation(query.Context, query.Generation);
        if (p == null)
            return Unavailable("stale-or-unavailable-navigation");
        return IntegrationSession.Serialize(new
        {
            available = true, context = query!.Context, p.Source.Generation,
            sourceHintGeneration = p.Source.HintGeneration,
            sameAsCurrentHints = p.Source.HintGeneration == query.Context.FrameId,
            sourceWorldTimeTicks = T(p.Source.WorldTime), sourceWorldTimeKind = p.Source.WorldTime.Kind.ToString(),
            sourceActorId = p.Source.ActorId.ToString("X"), p.Source.ZoneId, p.Source.ContentId,
            p.Source.CapturedUtc, p.BuildCompletedUtc, p.PublishedUtc, p.AgeSeconds,
            origin = P(p.Source.Origin), nativeFirstWaypoint = P(p.Destination), nativeNextWaypoint = P(p.NextWaypoint),
            nativeEndpoint = p.WitnessComplete ? P(p.Endpoint) : null, nativeTimeToGoal = Finite(p.TimeToGoal), nativeLeewaySeconds = Finite(p.LeewaySeconds),
            speed = p.Source.Speed, wasCasting = p.Source.Casting, avoidanceBuffer = p.Source.AvoidanceBuffer, p.Source.ProjectionLayer,
            movementModel = "native point navigation on conservative raster; no query-supplied player radius",
            p.NativeScore, p.NativeMaxGoalReached, p.NativeRelaxedWarnings, p.HasTeleporters,
            p.WitnessComplete, witness = p.CopyWitness().Select(w => P(w)).ToArray(),
            rasterAvailable = p.Raster != null,
            currentRouteSafetyVerified = false,
            meaning = "source-generation search result; final movement may apply native cast/range/follow corrections"
        });
    }

    internal string GetNavigationMap(string request)
    {
        var query = IntegrationSession.Parse<RasterQuery>(request);
        var p = query == null ? null : QueryNavigation(query.Context, query.Generation);
        var raster = p?.Raster;
        var window = query == null || raster == null ? null : raster.CopyWindow(query.Left, query.Top, query.Width, query.Height);
        if (window == null)
            return Unavailable("stale-unavailable-or-invalid-raster-window");
        return IntegrationSession.Serialize(new
        {
            available = true, context = query!.Context, p!.Source.Generation, sourceHintGeneration = p.Source.HintGeneration,
            timeOriginTicks = T(p.Source.WorldTime), timeOriginKind = p.Source.WorldTime.Kind.ToString(),
            raster!.Resolution, raster.Width, raster.Height, center = P(raster.Center), rotation = raster.Rotation.Rad,
            p.Source.ProjectionLayer, p.NativeRelaxedWarnings, p.HasTeleporters,
            blockCodes = new { clear = 0, arenaBoundary = 1, physicalObstacle = 2, temporaryObstacle = 3 },
            noWarningTime = "null on an unblocked cell means no finite published warning limit",
            warningMarginSeconds = NavigationDecision.ActivationTimeCushion,
            goalPriorityMeaning = "native score or integration lexicographic rank; never a safety verdict",
            safetyFreshness = "source-hints-only; current route safety requires separate validation",
            window
        });
    }

    internal sealed record RouteQuery(IntegrationSession.Context Context, long Generation, float[][][] Routes,
        float? Radius = null, float? Speed = null, float DepartureDelaySeconds = 0, float AdditionalTimeMarginSeconds = 0.25f);

    internal string EvaluateRoutes(string request)
    {
        var query = IntegrationSession.Parse<RouteQuery>(request);
        if (query == null || !Session.Matches(query.Context))
            return Unavailable("invalid-route-context");
        var p = QueryNavigation(query.Context, query.Generation);
        var player = World.Party.Player();
        if (p?.Raster == null || p.MapSource == null || player is not { IsDead: false } || query?.Routes == null)
            return Unavailable("stale-or-unavailable-route-source");
        if (!ReferenceEquals(p.MapSource.Bounds, hints.PathfindMapBounds) || p.MapSource.Center != hints.PathfindMapCenter
            || p.MapSource.ProjectionLayer != hints.PathfindMapArenaProjectionLayer)
            return Unavailable("changed-map-geometry-or-layer");
        if (p.HasTeleporters || hints.Teleporters.Count != 0 || hints.ImminentSpecialMode.mode != AIHints.SpecialMode.Normal
            || _encounterMovement != null || hints.AutomaticMovementPaused || movement.IsMoveRequested())
            return Unavailable("unsupported-movement-mode-or-manual-input");
        if (query.Routes.Length is < 1 or > IntegrationRoutes.MaxRoutes || query.Routes.Any(r => r == null || r.Length is < 1 or > IntegrationRoutes.MaxPoints
            || r.Any(v => v == null || v.Length != 2)))
            return Unavailable("invalid-route-batch");
        var speed = query.Speed ?? World.Client.MoveSpeed;
        var radius = query.Radius ?? player.HitboxRadius;
        if (!float.IsFinite(player.HitboxRadius) || !float.IsFinite(World.Client.MoveSpeed) || World.Client.MoveSpeed <= 0 || radius < player.HitboxRadius
            || speed > World.Client.MoveSpeed + 0.001f)
            return Unavailable("unsupported-footprint-or-speed-assumption");
        if (amex.MoveMightInterruptCast && query.DepartureDelaySeconds + 0.001f < Math.Max(0, amex.CastTimeRemaining - 0.5f)
            && !hints.ForceCancelCastMechanic && !ai.Controller.AllowInterruptingCastByMovement)
            return Unavailable("cast-departure-not-authorized");
        var region = hints.PathfindMapObstacles;
        if (region.Bitmap != null && (long)region.Rect.Width * region.Rect.Height > NavigationRaster.MaxCells)
            return Unavailable("physical-map-copy-limit");
        var budget = new IntegrationRoutes.Budget();
        var currentMap = hints.CapturePathfindMapSource();
        var source = new IntegrationRoutes.Source(p.Raster, currentMap.Obstacles, hints.CaptureIntegrationObstacles(),
            hints.CaptureIntegrationWarnings().Select(w => (w.shapeDistance, w.activation, w.Source)).ToArray(), World.CurrentTime,
            hints.PathfindMapBounds.AllowObstacleMap);
        var model = new IntegrationRoutes.Model(radius, speed, query.DepartureDelaySeconds, query.AdditionalTimeMarginSeconds);
        var results = query.Routes.Select(route => IntegrationRoutes.Evaluate(source, route.Select(v => new WPos(v[0], v[1])).ToArray(), model, budget)).ToArray();
        return IntegrationSession.Serialize(new
        {
            available = true, context = query.Context, p.Source.Generation, sourceMapHintGeneration = p.Source.HintGeneration,
            validatedHintGeneration = query.Context.FrameId, worldTimeTicks = T(World.CurrentTime),
            model, spatialMargin = IntegrationRoutes.SpatialMargin, nativeActivationMarginSeconds = NavigationDecision.ActivationTimeCushion,
            scope = "complete proposed walk routes against current native arena/map/temporary/warning constraints",
            doesNotAuthorizeMovementOrActions = true,
            unsupported = new[] { "dash", "teleport", "unknown-distance-bound", "future-warning-expiry" },
            results
        });
    }

    internal string GetArenaSnapshot(string request) => WithContext(request, context =>
    {
        var module = bossmod.ActiveModule;
        var player = World.Party.Player();
        return IntegrationSession.Serialize(new
        {
            available = true, context,
            selectedModule = module?.GetType().FullName,
            encounterOwnsPositioning = module?.StateMachine.ActiveState != null,
            encounter = module == null ? null : IntegrationArena.Describe(module.Bounds, module.Center,
                player == null ? null : module.ResolveArenaProjectionLayer(player)),
            pathfindingRegion = IntegrationArena.Describe(hints.PathfindMapBounds, hints.PathfindMapCenter, hints.PathfindMapArenaProjectionLayer),
            pathfindingRegionProvenance = "current-AIHints; not a substitute for encounter arena",
            obstacleMap = new { present = hints.PathfindMapObstacles.Bitmap != null,
                provenance = "native-hints-producer-not-attributed", generation = (long?)null,
                assetIdentity = (string?)null, defaultFrameAlignment = (object?)null },
            geometryOnly = true
        });
    });

    internal sealed record ArenaQuery(IntegrationSession.Context Context, string Region, float[][] Points, float Radius = 0, int? ProjectionLayer = null);

    internal string QueryArena(string request)
    {
        var query = IntegrationSession.Parse<ArenaQuery>(request);
        if (query == null || !Session.Matches(query.Context) || query.Points == null || query.Points.Length > IntegrationSession.MaxQueryPoints
            || query.Points.Any(p => p == null || p.Length != 2) || query.Region is not ("encounter" or "pathfinding"))
            return Unavailable("invalid-arena-query");
        ArenaBounds? bounds;
        WPos center;
        int? layer;
        if (query.Region == "encounter")
        {
            var module = bossmod.ActiveModule;
            var player = World.Party.Player();
            if (module?.StateMachine.ActiveState == null || player == null)
                return Unavailable("no-active-encounter-arena");
            bounds = module.Bounds;
            center = module.Center;
            layer = module.ResolveArenaProjectionLayer(player);
        }
        else
        {
            bounds = hints.PathfindMapBounds;
            center = hints.PathfindMapCenter;
            layer = hints.PathfindMapArenaProjectionLayer;
        }
        if (query.ProjectionLayer != layer)
            return Unavailable("projection-layer-mismatch");
        return IntegrationSession.Serialize(new
        {
            available = true, context = query.Context, query.Region, query.ProjectionLayer, geometryOnly = true,
            points = query.Points.Select(p => IntegrationArena.Query(bounds, center, layer, new(p[0], p[1]), query.Radius)).ToArray()
        });
    }

    public void Dispose()
    {
        _decodeShutdown.Cancel();
        DisposeSafetySnapshots();
        RetireDistanceSources(all: true);
        DisposeSearches();
        DisposeControls();
        Session.Dispose();
        _effects.Dispose();
    }
}
