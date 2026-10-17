using BossMod.Components;
using BossMod.Pathfinding;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record MarchHeadings(IntegrationSession.Context Context, string OwnerToken, string ComponentId, float[] Headings);
    internal sealed record MarchHeadingConstraint(IntegrationSession.Context Context, string OwnerToken, string ComponentId, float Heading);
    private long _marchFrame;
    private int _marchAttempts;

    private IntegrationMarchSafety.Source? MarchSource(IntegrationSession.Context context, string owner, string componentId, IntegrationRoutes.Budget budget)
    {
        if (!Session.Matches(context) || !Session.IsOwnerActive(owner) || World.Party.Player() is not { IsDead: false, IsDestroyed: false } actor
            || bossmod.ActiveModule is not { } module || module.Components.Count > 64 || hints.ForbiddenZones.Count + hints.TemporaryObstacles.Count > 128
            || hints.ForcedMovement != null || hints.ForbidDashes || hints.ImminentSpecialMode.mode != AIHints.SpecialMode.Normal
            || hints.ForbiddenDirections.Count != 0 || !budget.Step()) return null;
        var marches = module.Components.OfType<GenericForcedMarch>().Where(m => m.HasForcedMovements(actor)).Take(2).ToArray();
        if (marches.Length != 1 || IntegrationMechanics.ComponentId(marches[0]) != componentId || marches[0].OverrideDirection) return null;
        var slot = World.Party.FindSlot(actor.InstanceID);
        if (slot < 0) return null;
        var bounds = hints.PathfindMapBounds;
        if (!float.IsFinite(bounds.MapResolution) || bounds.MapResolution <= 0 || !float.IsFinite(bounds.Radius)
            || bounds.Radius <= 0 || bounds.Radius / bounds.MapResolution > 256) return null;
        var region = hints.PathfindMapObstacles;
        if (region.Bitmap != null && (long)region.Rect.Width * region.Rect.Height > NavigationRaster.MaxCells) return null;
        var native = hints.CapturePathfindMapSource();
        var map = new Map(); var blocks = native.InitForWorker(map);
        if (blocks == null || !budget.Step()) return null;
        var terrain = new IntegrationRoutes.Source(new NavigationRaster(map, null, blocks), native.Obstacles,
            hints.CaptureIntegrationObstacles(), hints.CaptureIntegrationWarnings().Select(z => (z.shapeDistance, z.activation, z.Source)).ToArray(),
            World.CurrentTime, bounds.AllowObstacleMap);
        return new(hints, module, marches[0], actor, slot, terrain);
    }

    private bool MarchWorkAvailable()
    {
        if (_marchFrame != Session.FrameId) { _marchFrame = Session.FrameId; _marchAttempts = 0; }
        return ++_marchAttempts <= 8;
    }

    internal string EvaluateForcedMarchHeadings(string json)
    {
        var request = IntegrationSession.Parse<MarchHeadings>(json);
        if (request == null || request.Headings is not { Length: >= 1 and <= 128 } || request.Headings.Any(h => !float.IsFinite(h) || Math.Abs(h) > MathF.Tau)
            || !Session.Matches(request.Context) || !MarchWorkAvailable()) return Unavailable("invalid-native-march-query");
        try
        {
            var budget = new IntegrationRoutes.Budget();
            var source = MarchSource(request.Context, request.OwnerToken, request.ComponentId, budget);
            if (source == null) return Unavailable("native-march-source-unavailable");
            var results = request.Headings.Select(h => IntegrationMarchSafety.Evaluate(source, h, budget)).ToArray();
            if (!Session.Matches(request.Context)) return Unavailable("native-march-source-retired");
            var state = source.March.State[source.Actor.InstanceID];
            return IntegrationSession.Serialize(new
            {
                available = true, context = request.Context, request.ComponentId, actorId = source.Actor.InstanceID.ToString("X"),
                worldTimeTicks = T(World.CurrentTime), sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs,
                active = state.ForcedEnd > World.CurrentTime, currentRotation = source.Actor.Rotation.Rad,
                forcedEndTicks = T(state.ForcedEnd), movementSpeed = source.March.MovementSpeed,
                pending = state.PendingMoves.Select(m => new { direction = m.dir.Rad, m.duration, activationTicks = T(m.activation) }).ToArray(),
                results, complete = results.All(r => r.Status != "unknown"), windowEvaluated = false,
                clearance = Math.Max(0.6f, source.Actor.HitboxRadius + 0.05f), activationCushionSeconds = Math.Max(0.2f, NavigationDecision.ActivationTimeCushion), landingCushionSeconds = 0.25f,
                meaning = "candidate centers only; full native timed waits/legs/landing cushion, current independent constraints, physical map and arena; no action/immunity/input approval; constrain endpoint separately revalidates the sampled heading window"
            });
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Native march query failed: {error.GetType().Name}");
            return Unavailable("native-march-query-failed");
        }
    }

    internal string ConstrainForcedMarchHeading(string json)
    {
        var request = IntegrationSession.Parse<MarchHeadingConstraint>(json);
        if (request == null || request.Context.Stage != "hints-ready" || !Session.Matches(request.Context) || !Session.CanContribute
            || !float.IsFinite(request.Heading) || Math.Abs(request.Heading) > MathF.Tau || !MarchWorkAvailable()
            || !ControlsAllowed() || !SmartRotationTweak.Enabled) return Unavailable("invalid-native-march-heading-context");
        try
        {
            var budget = new IntegrationRoutes.Budget();
            var source = MarchSource(request.Context, request.OwnerToken, request.ComponentId, budget);
            if (source == null) return Unavailable("native-march-source-unavailable");
            var active = source.March.State[source.Actor.InstanceID].ForcedEnd > World.CurrentTime;
            if (active && Math.Abs((request.Heading.Radians() - source.Actor.Rotation).Normalized().Rad) > 0.001f)
                return Unavailable("native-march-heading-already-committed");
            for (var i = -8; i <= 8; ++i)
            {
                var result = IntegrationMarchSafety.Evaluate(source, new Angle(request.Heading + i * 0.5f * MathF.PI / 180).Normalized().Rad, budget);
                if (result.Status != "clear") return Unavailable("native-march-window-" + result.Status + ":" + result.Reason);
            }
            var live = Session.CaptureObservationLiveness(request.OwnerToken);
            if (live == null || !Session.Matches(request.Context)) return Unavailable("native-march-heading-retired");
            var frame = Session.FrameId; var worldTime = World.CurrentTime; var pose = source.Actor.PosRot;
            var zones = hints.ForbiddenZones.ToArray(); var obstacles = hints.CaptureIntegrationObstacles();
            var bounds = hints.PathfindMapBounds; var center = hints.PathfindMapCenter; var layer = hints.PathfindMapArenaProjectionLayer;
            var physical = hints.PathfindMapObstacles; var state = source.March.State[source.Actor.InstanceID];
            var pending = state.PendingMoves.ToArray(); var forcedEnd = state.ForcedEnd; var speed = source.March.MovementSpeed;
            var retired = 0;
            bool Current()
            {
                if (System.Threading.Volatile.Read(ref retired) != 0) return false;
                var valid = live() && Session.FrameId == frame && Session.CanContribute && ControlsAllowed()
                    && World.CurrentTime == worldTime && ReferenceEquals(World.Party.Player(), source.Actor) && source.Actor.PosRot == pose
                    && ReferenceEquals(bossmod.ActiveModule, source.Module) && source.Module.Components.Contains(source.March)
                    && source.March.HasForcedMovements(source.Actor) && !source.March.OverrideDirection
                    && state.ForcedEnd == forcedEnd && source.March.MovementSpeed == speed && state.PendingMoves.SequenceEqual(pending)
                    && hints.ForcedMovement == null && !hints.ForbidDashes && hints.ImminentSpecialMode.mode == AIHints.SpecialMode.Normal
                    && hints.ForbiddenDirections.Count == 0 && hints.ForbiddenZones.SequenceEqual(zones) && hints.CaptureIntegrationObstacles().SequenceEqual(obstacles)
                    && ReferenceEquals(hints.PathfindMapBounds, bounds) && hints.PathfindMapCenter == center && hints.PathfindMapArenaProjectionLayer == layer
                    && ReferenceEquals(hints.PathfindMapObstacles.Bitmap, physical.Bitmap) && hints.PathfindMapObstacles.Rect.Equals(physical.Rect);
                if (!valid) System.Threading.Interlocked.Exchange(ref retired, 1);
                return valid;
            }
            hints.IntegrationHeadings ??= new();
            if (!hints.IntegrationHeadings.Submit(request.OwnerToken, request.Heading, Current)) return Unavailable("native-march-owner-retired");
            return IntegrationSession.Serialize(new { accepted = true, context = request.Context, request.ComponentId,
                actorId = source.Actor.InstanceID.ToString("X"), heading = request.Heading, halfWindowRadians = 4 * MathF.PI / 180,
                samples = 17, expiresWithFrame = true, meaning = "additive sampled-safe heading window; native authored directions and manual control remain authoritative" });
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Native heading contribution failed: {error.GetType().Name}");
            return Unavailable("native-march-heading-failed");
        }
    }
}
