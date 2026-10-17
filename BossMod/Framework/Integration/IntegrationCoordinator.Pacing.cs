using BossMod.Pathfinding;
using System.Diagnostics;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record PaceRequest(IntegrationSession.Context Context, string OwnerToken, long Generation, float Scale, int LeaseMs);
    private sealed record PendingPace(PaceRequest Request, IntegrationMovementDecision Decision, IntegrationPacing.Ticket Ticket, string Scope);
    private PendingPace? _pendingPace;
    private int _paceAttempts;
    private string? _paceResult;

    private bool PacingOwnerLive(Func<bool> ownerAlive, string scope)
    {
        var observation = System.Threading.Volatile.Read(ref _controlObservation);
        return ownerAlive() && !System.Threading.Volatile.Read(ref amex.MoveMightInterruptCast)
            && observation is { Allowed: true } && observation.Scope == scope
            && (Stopwatch.GetTimestamp() - observation.Timestamp) / (double)Stopwatch.Frequency < 0.25;
    }

    private void BeginPacingFrame()
    {
        movement.IntegrationPacing.Clear();
        _pendingPace = null; _paceAttempts = 0; _paceResult = null;
    }

    private string? ValidatePace(PaceRequest request, IntegrationMovementDecision decision)
    {
        var player = World.Party.Player();
        if (!Session.CanContribute || !Session.IsOwnerActive(request.OwnerToken) || !ControlsAllowed() || player is not { InCombat: false }
            || ai.Beh != null || player.CastInfo is { EventHappened: false } || amex.MoveMightInterruptCast
            || player.PendingKnockbacks.Count != 0 || World.Client.Flying || movement.FollowPathActive()
            || hints.ImminentSpecialMode.mode != AIHints.SpecialMode.Normal || hints.ForbiddenDirections.Count != 0
            || hints.PredictedDamage.Count != 0 || hints.ForceCancelCastMechanic || hints.ForceCancelCastOther || hints.HoldCasts
            || ai.Controller.ForceCancelCastMechanicAI || ai.Controller.ForceCancelCastOtherAI
            || hints.InteractWithTarget != null || hints.WantJump || _encounterMovement != null || !hints.GoalZonesEnabled)
            return "native-control-cast-combat-or-mechanic-pressure";
        for (var slot = 0; slot < PartyState.MaxAllies; ++slot)
            if (World.Party[slot]?.InCombat == true) return "party-in-combat";
        if (hints.PotentialTargets.Count > AIHints.NumEnemies || hints.PotentialTargets.Any(t => t.Priority >= 0 && t.Actor.InCombat))
            return "native-target-engagement";
        var now = Stopwatch.GetTimestamp();
        var frameAge = (now - World.LiveFrameObservedTimestamp) / (double)Stopwatch.Frequency;
        if (!float.IsFinite(hints.MaxCastTime) || hints.MaxCastTime < request.LeaseMs / 1000d + frameAge)
            return "native-cast-pressure-deadline";
        if (World.LiveFrameObservedTimestamp <= 0 || frameAge is < 0 or >= 0.25 || decision.InstanceGeneration != Session.InstanceGeneration
            || decision.FrameId != Session.FrameId || decision.ActorId != player.InstanceID || decision.WorldTime != World.CurrentTime
            || decision.Owner != "bmr-normal-movement" || !ReferenceEquals(decision, hints.IntegrationMovementDecision)
            || hints.EffectiveForcedMovement != decision.Direction || decision.Direction.Y != 0 || (player.Position - decision.Origin).LengthSq() > 0.01f)
            return "changed-or-stale-native-controller-decision";
        var p = QueryNavigation(request.Context, request.Generation);
        if (p == null || !ReferenceEquals(p, decision.Navigation) || !ReferenceEquals(p, Autorotation.MiscAI.NormalMovement.Instance?.PublishedNavigation)
            || p.Raster == null || p.MapSource == null || !p.WitnessComplete || !p.NativeMaxGoalReached || p.NativeRelaxedWarnings
            || p.HasTeleporters || hints.Teleporters.Count != 0 || p.Destination != decision.Destination
            || !ReferenceEquals(p.MapSource.Bounds, hints.PathfindMapBounds) || p.MapSource.Center != hints.PathfindMapCenter
            || p.MapSource.ProjectionLayer != hints.PathfindMapArenaProjectionLayer)
            return "unavailable-or-adjusted-native-route";
        if (!p.TryRemainingLeeway(World.CurrentTime, player.InstanceID, World.CurrentZone, World.CurrentCFCID, out var leeway)
            || leeway < request.LeaseMs / 1000f + frameAge + 0.1 || !float.IsFinite(World.Client.MoveSpeed) || World.Client.MoveSpeed <= 0)
            return "insufficient-native-deadline-reserve";
        var route = p.CopyWitness();
        if (route.Length is < 2 or > IntegrationRoutes.MaxPoints || (route[0] - player.Position).LengthSq() > 0.01f)
            return "incomplete-or-moved-route-origin";
        route[0] = player.Position;
        var region = hints.PathfindMapObstacles;
        if (region.Bitmap != null && (long)region.Rect.Width * region.Rect.Height > NavigationRaster.MaxCells) return "physical-map-copy-limit";
        var source = new IntegrationRoutes.Source(p.Raster, hints.CapturePathfindMapSource().Obstacles,
            hints.TemporaryObstacles.ToArray(), hints.ForbiddenZones.Select(z => (z.shapeDistance, z.activation, z.Source)).ToArray(),
            World.CurrentTime, hints.PathfindMapBounds.AllowObstacleMap);
        // Assume zero progress for the ENTIRE lease, then normal native speed. This does not
        // assume a calibrated linear relationship between analog magnitude and game speed.
        var model = new IntegrationRoutes.Model(player.HitboxRadius, Math.Min(World.Client.MoveSpeed, p.Source.Speed),
            request.LeaseMs / 1000f + (float)frameAge, 0.25f);
        var result = IntegrationRoutes.Evaluate(source, route, model, new());
        return result.Status == "clear" ? null : "pace-route-" + result.Status + ":" + result.Reason;
    }

    internal string RequestPace(string json)
    {
        var request = IntegrationSession.Parse<PaceRequest>(json);
        if (request == null || !Session.Matches(request.Context) || !Session.CanContribute || request.Context.Stage != "movement-proposed"
            || string.IsNullOrEmpty(request.OwnerToken) || !Session.IsOwnerActive(request.OwnerToken)
            || !float.IsFinite(request.Scale) || request.Scale is < IntegrationPacing.MinScale or > 1
            || request.LeaseMs is < 1 or > IntegrationPacing.MaxLeaseMs || ++_paceAttempts > 4)
            return Unavailable("invalid-or-bounded-out-pace-request");
        if (_pendingPace != null || hints.IntegrationMovementDecision is not { } decision) return Unavailable("no-native-decision-or-pace-already-submitted");
        var epoch = movement.IntegrationPacing.CaptureEpoch(request.OwnerToken);
        if (epoch == null) return Unavailable("pace-owner-work-limit");
        try
        {
            var rejection = ValidatePace(request, decision);
            if (rejection != null) return Unavailable(rejection);
            var scope = ControlScope();
            var alive = Session.CaptureOwnerLiveness(request.OwnerToken);
            if (alive == null) return Unavailable("expired-pace-owner");
            var expires = Math.Min(decision.CapturedTimestamp + (long)(request.LeaseMs / 1000d * Stopwatch.Frequency),
                decision.Navigation.Source.CapturedTimestamp + Stopwatch.Frequency / 4);
            var ticket = new IntegrationPacing.Ticket(Guid.NewGuid().ToString("N"), request.OwnerToken, Session.InstanceGeneration,
                Session.FrameId, request.Generation, decision.Direction, request.Scale, expires, () => PacingOwnerLive(alive, scope));
            if (!movement.IntegrationPacing.Queue(ticket, epoch)) return Unavailable("pace-expired-cleared-or-already-owned");
            _pendingPace = new(request, decision, ticket, scope); _paceResult = "pending-native-application";
            return IntegrationSession.Serialize(new { accepted = true, context = request.Context, proofId = ticket.Id, status = _paceResult,
                scale = request.Scale, maxLeaseMs = IntegrationPacing.MaxLeaseMs, sourceNavigationGeneration = request.Generation,
                worstCasePauseSeconds = request.LeaseMs / 1000f, timingModel = "zero-progress-for-entire-lease-then-native-model-speed",
                analogSpeedRatioVerified = false });
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
        try { rejection = pending.Scope != ControlScope() ? "changed-scope" : ValidatePace(pending.Request, pending.Decision); }
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
