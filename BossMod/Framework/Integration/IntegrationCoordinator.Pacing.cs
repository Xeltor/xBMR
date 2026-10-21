using BossMod.Pathfinding;
using System.Diagnostics;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record PaceRequest(IntegrationSession.Context Context, string OwnerToken, long Generation, float Scale, int LeaseMs);
    internal sealed record DepartureHoldRequest(IntegrationSession.Context Context, string OwnerToken, long Generation, int LeaseMs);
    private sealed record PendingPace(PaceRequest Request, IntegrationMovementDecision Decision, IntegrationPacing.Ticket Ticket, string Scope, string Kind);
    private PendingPace? _pendingPace;
    private int _paceAttempts;
    private string? _paceResult;

    private bool PacingOwnerLive(Func<bool> ownerAlive, string scope, bool departureHold)
    {
        var observation = System.Threading.Volatile.Read(ref _controlObservation);
        return ownerAlive() && (departureHold || !System.Threading.Volatile.Read(ref amex.MoveMightInterruptCast))
            && observation is { Allowed: true } && observation.Scope == scope
            && (Stopwatch.GetTimestamp() - observation.Timestamp) / (double)Stopwatch.Frequency < 0.25;
    }

    private void BeginPacingFrame()
    {
        movement.IntegrationPacing.Clear();
        _pendingPace = null; _paceAttempts = 0; _paceResult = null; _lastPhysicalProbe = null;
    }

    private string? ValidatePace(PaceRequest request, IntegrationMovementDecision decision)
        => ValidateMovementPause(request, decision, false);

    private string? ValidateMovementPause(PaceRequest request, IntegrationMovementDecision decision, bool departureHold, string kind = "")
    {
        var stationary = kind is "physical-brake" or "follow-physical-brake";
        var follow = kind is "follow-arrival" or "follow-physical-brake";
        var player = World.Party.Player();
        if (!Session.CanContribute || !Session.IsOwnerActive(request.OwnerToken) || !ControlsAllowed() || player is not { IsDestroyed: false }
            || (follow ? ai.Beh == null : ai.Beh != null) || (!departureHold && (player.InCombat || player.CastInfo is { EventHappened: false } || amex.MoveMightInterruptCast))
            || player.PendingKnockbacks.Count != 0 || World.Client.Flying || movement.FollowPathActive()
            || hints.ImminentSpecialMode.mode != AIHints.SpecialMode.Normal || hints.ForbiddenDirections.Count != 0
            || (!departureHold && hints.PredictedDamage.Count != 0) || hints.ForceCancelCastMechanic || hints.ForceCancelCastOther || hints.HoldCasts
            || ai.Controller.ForceCancelCastMechanicAI || ai.Controller.ForceCancelCastOtherAI
            || hints.InteractWithTarget != null || hints.WantJump || _encounterMovement != null || !hints.GoalZonesEnabled)
            return "native-control-cast-combat-or-mechanic-pressure";
        if (!departureHold)
            for (var slot = 0; slot < PartyState.MaxAllies; ++slot)
                if (World.Party[slot]?.InCombat == true) return "party-in-combat";
        if (hints.PotentialTargets.Count > AIHints.NumEnemies || (!departureHold && hints.PotentialTargets.Any(t => t.Priority >= 0 && t.Actor.InCombat)))
            return "native-target-engagement";
        var now = Stopwatch.GetTimestamp();
        var frameAge = (now - World.LiveFrameObservedTimestamp) / (double)Stopwatch.Frequency;
        if (!float.IsFinite(hints.MaxCastTime) || hints.MaxCastTime < request.LeaseMs / 1000d + frameAge)
            return "native-cast-pressure-deadline";
        if (World.LiveFrameObservedTimestamp <= 0 || frameAge is < 0 or >= 0.25 || decision.InstanceGeneration != Session.InstanceGeneration
            || decision.FrameId != Session.FrameId || decision.ActorId != player.InstanceID || decision.WorldTime != World.CurrentTime
            || decision.Owner != (follow ? "bmr-follow" : "bmr-normal-movement") || !ReferenceEquals(decision, hints.IntegrationMovementDecision)
            || hints.EffectiveForcedMovement != decision.Direction || decision.Direction.Y != 0 || (player.Position - decision.Origin).LengthSq() > 0.01f)
            return "changed-or-stale-native-controller-decision";
        var p = QueryNavigation(request.Context, request.Generation);
        if (p == null || !ReferenceEquals(p, decision.Navigation) || !ReferenceEquals(p, follow ? ai.Beh?.PublishedNavigation : Autorotation.MiscAI.NormalMovement.Instance?.PublishedNavigation)
            || p.Raster == null || p.MapSource == null || !p.WitnessComplete || !stationary && !p.NativeMaxGoalReached || p.NativeRelaxedWarnings
            || p.HasTeleporters || hints.Teleporters.Count != 0 || p.Destination is not { } publishedDestination
            || (follow ? (publishedDestination - decision.Destination).LengthSq() > 0.01f : publishedDestination != decision.Destination)
            || !ReferenceEquals(p.MapSource.Bounds, hints.PathfindMapBounds) || p.MapSource.Center != hints.PathfindMapCenter
            || p.MapSource.ProjectionLayer != hints.PathfindMapArenaProjectionLayer)
            return "unavailable-or-adjusted-native-route";
        if (!p.TryRemainingLeeway(World.CurrentTime, player.InstanceID, World.CurrentZone, World.CurrentCFCID, out var leeway)
            || leeway < request.LeaseMs / 1000f + frameAge + 0.1 || !float.IsFinite(World.Client.MoveSpeed) || World.Client.MoveSpeed <= 0)
            return "insufficient-native-deadline-reserve";
        if (follow && (ai.ForceMovementIn != float.MaxValue || ai.Controller.NaviLeewaySeconds != float.MaxValue
            || ai.Controller.AllowInterruptingCastByMovement || ai.Controller.NaviTargetPos != decision.Destination
            || decision.ControllerDecisionId == null || hints.IntegrationControllerDecision is not { } controller
            || controller.Id != decision.ControllerDecisionId || controller.Owner != "bmr-follow"
            || !controller.Matches(Session.InstanceGeneration, Session.FrameId, player.InstanceID, decision.Direction)
            || decision.FollowMasterId == null || World.Party[ai.MasterSlot] is not { } master || master.InstanceID != decision.FollowMasterId
            || master.PosRot.XYZ() != decision.FollowMasterPosition)) return "changed-or-urgent-native-follow-source";
        var route = p.CopyWitness();
        if (route.Length is < 2 or > IntegrationRoutes.MaxPoints || (route[0] - player.Position).LengthSq() > 0.01f)
            return "incomplete-or-moved-route-origin";
        route[0] = player.Position;
        if (kind is "arrival-hold" or "follow-arrival" or "physical-brake" or "follow-physical-brake")
        {
            if (player.CastInfo is { EventHappened: false } || amex.MoveMightInterruptCast || hints.PredictedDamage.Count != 0)
                return "native-hold-cast-or-damage-pressure";
            if (kind is "arrival-hold" or "follow-arrival")
            {
                if (p.NextWaypoint is { } onward && (onward - decision.Destination).LengthSq() > 0.25f || (decision.Destination - player.Position).LengthSq() > 1.5f * 1.5f)
                    return "native-arrival-requires-onward-or-distant-movement";
                if (!EveryArrivalLeafNonImproving(player.Position, decision.Destination, route)) return "native-arrival-improving-goal-leaf";
            }
            else if (request.LeaseMs > 50) return "native-physical-brake-lease-limit";
        }
        var region = hints.PathfindMapObstacles;
        if (region.Bitmap != null && (long)region.Rect.Width * region.Rect.Height > NavigationRaster.MaxCells) return "physical-map-copy-limit";
        var source = new IntegrationRoutes.Source(p.Raster, hints.CapturePathfindMapSource().Obstacles,
            hints.CaptureIntegrationObstacles(), hints.CaptureIntegrationWarnings().Select(z => (z.shapeDistance, z.activation, z.Source)).ToArray(),
            World.CurrentTime, hints.PathfindMapBounds.AllowObstacleMap);
        // Assume zero progress for the ENTIRE lease, then normal native speed. This does not
        // assume a calibrated linear relationship between analog magnitude and game speed.
        var model = new IntegrationRoutes.Model(player.HitboxRadius, Math.Min(World.Client.MoveSpeed, p.Source.Speed),
            request.LeaseMs / 1000f + (float)frameAge, 0.25f);
        if (stationary)
        {
            var direction = new WDir(decision.Direction.X, decision.Direction.Z).Normalized();
            var step = player.Position + direction * World.Client.MoveSpeed * 0.05f;
            var block = IntegrationRoutes.CheckPhysical(source, player.Position, step, player.HitboxRadius + IntegrationRoutes.SpatialMargin, 0, new());
            if (block?.Status != "blocked")
            {
                _lastPhysicalProbe = ObservePhysicalStep(player, decision.Direction);
                if (_lastPhysicalProbe.Status != "blocked" || !_lastPhysicalProbe.CurrentSupportVerified)
                    return "native-physical-brake-no-proven-blocked-step:" + _lastPhysicalProbe.Reason;
                // Actual current five-point native floor support supplies independent 3D
                // coverage. Known 2D blocks remain binding; missing 2D cells are not
                // promoted to evidence of either a drop or a wall.
                source = source with { RequirePhysicalCoverage = false };
            }
            route = [player.Position]; // validates remaining stationary for the whole bounded lease
        }
        var result = IntegrationRoutes.Evaluate(source, route, model, new());
        return result.Status == "clear" ? null : "pace-route-" + result.Status + ":" + result.Reason;
    }

    internal string RequestPace(string json)
    {
        return RequestMovementPause(IntegrationSession.Parse<PaceRequest>(json), false);
    }

    internal string RequestDepartureHold(string json)
    {
        var request = IntegrationSession.Parse<DepartureHoldRequest>(json);
        return RequestMovementPause(request == null ? null : new(request.Context, request.OwnerToken, request.Generation, 0, request.LeaseMs), true);
    }

    internal sealed record FollowArrivalHold(IntegrationSession.Context Context, string OwnerToken, long Generation, string ControllerDecisionId, int LeaseMs);
    internal string RequestFollowArrivalHold(string json) => RequestFollowInputHold(json, "follow-arrival");
    internal string RequestFollowPhysicalBrake(string json) => RequestFollowInputHold(json, "follow-physical-brake");
    private string RequestFollowInputHold(string json, string kind)
    {
        var request = IntegrationSession.Parse<FollowArrivalHold>(json);
        if (request == null || !Session.Matches(request.Context) || hints.IntegrationMovementDecision?.ControllerDecisionId != request.ControllerDecisionId)
            return Unavailable("invalid-native-follow-arrival-source");
        return RequestMovementPause(new(request.Context, request.OwnerToken, request.Generation, 0, request.LeaseMs), true, kind);
    }
    internal string RequestArrivalHold(string json) => RequestFinalInputHold(json, "arrival-hold");
    internal string RequestPhysicalBrake(string json) => RequestFinalInputHold(json, "physical-brake");
    private string RequestFinalInputHold(string json, string kind)
    {
        var request = IntegrationSession.Parse<DepartureHoldRequest>(json);
        return RequestMovementPause(request == null ? null : new(request.Context, request.OwnerToken, request.Generation, 0, request.LeaseMs), true, kind);
    }
    private bool EveryArrivalLeafNonImproving(WPos origin, WPos destination, WPos[] route)
    {
        var external = Session.CaptureIndividualGoalLeaves();
        if (hints.GoalZones.Count + external.Length > 128) return false;
        var functions = hints.GoalZones.Concat(external.Select(f => (Func<WPos, float>)(p => f(new(p.X, p.Z))))).ToArray();
        var budget = new IntegrationRoutes.Budget();
        try
        {
            foreach (var score in functions)
            {
                if (!budget.Step()) return false;
                var baseline = score(origin);
                if (!float.IsFinite(baseline)) return false;
                foreach (var candidate in route.Append(destination))
                {
                    if (!budget.Step()) return false;
                    var value = score(candidate);
                    if (!float.IsFinite(value) || value > baseline || !budget.Step()) return false;
                }
            }
            return true;
        }
        catch { return false; }
    }

    private string RequestMovementPause(PaceRequest? request, bool departureHold, string kind = "")
    {
        if (request == null || !Session.Matches(request.Context) || !Session.CanContribute || request.Context.Stage != "movement-proposed"
            || string.IsNullOrEmpty(request.OwnerToken) || !Session.IsOwnerActive(request.OwnerToken)
            || !float.IsFinite(request.Scale) || (departureHold ? request.Scale != 0 : request.Scale is < IntegrationPacing.MinScale or > 1)
            || request.LeaseMs is < 1 or > IntegrationPacing.MaxLeaseMs || ++_paceAttempts > 4)
            return Unavailable("invalid-or-bounded-out-pace-request");
        if (_pendingPace != null || hints.IntegrationMovementDecision is not { } decision) return Unavailable("no-native-decision-or-pace-already-submitted");
        var epoch = movement.IntegrationPacing.CaptureEpoch(request.OwnerToken);
        if (epoch == null) return Unavailable("pace-owner-work-limit");
        try
        {
            var rejection = ValidateMovementPause(request, decision, departureHold, kind);
            if (rejection != null) return Unavailable(rejection);
            var scope = ControlScope();
            var alive = Session.CaptureOwnerLiveness(request.OwnerToken);
            if (alive == null) return Unavailable("expired-pace-owner");
            var goalsValid = Session.CaptureGoalValidity();
            if (!goalsValid()) return Unavailable("incomplete-integration-goal-scoring");
            var expires = Math.Min(decision.CapturedTimestamp + (long)(request.LeaseMs / 1000d * Stopwatch.Frequency),
                decision.Navigation.Source.CapturedTimestamp + Stopwatch.Frequency / 4);
            var ticket = new IntegrationPacing.Ticket(Guid.NewGuid().ToString("N"), request.OwnerToken, Session.InstanceGeneration,
                Session.FrameId, request.Generation, decision.Direction, request.Scale, expires, () => goalsValid() && PacingOwnerLive(alive, scope, departureHold), departureHold);
            if (!movement.IntegrationPacing.Queue(ticket, epoch)) return Unavailable("pace-expired-cleared-or-already-owned");
            _pendingPace = new(request, decision, ticket, scope, kind); _paceResult = "pending-native-application";
            return IntegrationSession.Serialize(new { accepted = true, context = request.Context, proofId = ticket.Id, status = _paceResult,
                kind = kind.Length != 0 ? kind : departureHold ? "departure-hold" : "automatic-pace", scale = request.Scale, maxLeaseMs = IntegrationPacing.MaxLeaseMs, sourceNavigationGeneration = request.Generation,
                worstCasePauseSeconds = request.LeaseMs / 1000f, timingModel = "zero-progress-for-entire-lease-then-native-model-speed",
                physicalProbe = kind is "physical-brake" or "follow-physical-brake" ? _lastPhysicalProbe : null, analogSpeedRatioVerified = false, castPermissionGranted = false });
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Pace validation unavailable: {error.GetType().Name}: {error.Message}");
            return Unavailable("pace-validation-unavailable");
        }
    }

    private void FinalizePace()
    {
        if (_pendingPace is not { } pending) return;
        string? rejection;
        try { rejection = pending.Scope != ControlScope() ? "changed-scope" : ValidateMovementPause(pending.Request, pending.Decision, pending.Ticket.DepartureHold, pending.Kind); }
        catch { rejection = "pace-revalidation-unavailable"; }
        if (rejection != null) { movement.IntegrationPacing.Cancel(pending.Ticket); _paceResult = rejection; }
    }

    private void ArmPace()
    {
        if (_pendingPace is { } pending && System.Threading.Volatile.Read(ref pending.Ticket.State) != 2)
            _paceResult = movement.IntegrationPacing.Arm(pending.Ticket, movement.IntegrationInput.CurrentAssignment!)
                ? "armed-for-native-input" : "not-armed-or-revoked";
    }

    internal string ClearPace(string json)
    {
        var request = IntegrationSession.Parse<IntegrationSession.Lease>(json);
        if (request == null || request.SchemaVersion != 1 || request.InstanceGeneration != Session.InstanceGeneration || string.IsNullOrEmpty(request.OwnerToken))
            return Unavailable("invalid-pace-owner");
        var accepted = movement.IntegrationPacing.ClearOwner(request.OwnerToken);
        return IntegrationSession.Serialize(new { accepted, reason = accepted ? "pace-cleared-or-absent" : "pace-owned-by-another-owner" });
    }
}
