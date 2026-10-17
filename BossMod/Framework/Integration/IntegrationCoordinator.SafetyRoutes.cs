using BossMod.Pathfinding;
using System.Globalization;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record SafetyRouteRequest(IntegrationSession.Context Context, string OwnerToken, string SnapshotId, string Kind,
        float[] Destination, float[]? Origin = null, float? Speed = null, float? Radius = null, float DepartureDelaySeconds = 0,
        float DestinationTolerance = 1, string? MustArriveByTicks = null);
    internal sealed record SafetyRoutePoll(IntegrationSession.Context Context, string OwnerToken, string SnapshotId, string RouteId);
    private sealed record SafetyRouteJob(string Id, string Owner, string Parent, string Kind, WPos Origin, WPos Destination,
        DateTime RequestedTime, IntegrationRoutes.Model Model, float Tolerance, DateTime? Deadline, NavigationRaster Raster,
        Task<IntegrationSearch.Result> Work);
    private readonly Dictionary<string, SafetyRouteJob> _safetyRoutes = [];
    private Task<IntegrationSearch.Result>? _safetyRouteWorker;
    private long _safetyRouteFrame;
    private int _safetyRouteAttempts;
    private static NavigationRaster? StrictRouteRaster(NavigationRaster source)
    {
        var map = source.CopyForSearch(0, IntegrationSearch.MaxCells);
        if (map == null) return null;
        var blocks = new byte[map.Width * map.Height];
        for (var y = 0; y < map.Height; ++y)
            for (var x = 0; x < map.Width; ++x) blocks[map.GridToIndex(x, y)] = source.BlockAt(x, y) == 0 ? (byte)0 : (byte)1;
        return new(map, null, blocks);
    }
    private void PruneSafetyRoutes()
    {
        foreach (var route in _safetyRoutes.Values.ToArray())
            if (!_safetyJobs.ContainsKey(route.Parent)) { _safetyRoutes.Remove(route.Id); _ = route.Work.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted); }
    }
    internal string StartSafetyRoute(string json)
    {
        var request = IntegrationSession.Parse<SafetyRouteRequest>(json);
        if (request == null || !Session.Matches(request.Context) || request.Kind is not ("preparation" or "recovery")
            || request.Destination is not { Length: 2 } || request.Destination.Any(v => !float.IsFinite(v) || Math.Abs(v) > 1_000_000)
            || !float.IsFinite(request.DepartureDelaySeconds) || request.DepartureDelaySeconds is < 0 or > 10
            || !float.IsFinite(request.DestinationTolerance) || request.DestinationTolerance is < 0.25f or > 3)
            return Unavailable("invalid-native-safety-route-request");
        lock (_safetyGate)
        {
            var parent = SafetySource(request.Context, request.OwnerToken, request.SnapshotId);
            PruneSafetyRoutes();
            if (parent?.Work.IsCompletedSuccessfully != true || parent.Work.Result is not { } maps || World.Party.Player() is not { } actor
                || hints.Teleporters.Count != 0 || hints.ImminentSpecialMode.mode != AIHints.SpecialMode.Normal || _encounterMovement != null)
                return Unavailable("native-safety-route-source-unavailable");
            if (_safetyRouteFrame != Session.FrameId) { _safetyRouteFrame = Session.FrameId; _safetyRouteAttempts = 0; }
            if (++_safetyRouteAttempts > 4 || _safetyRouteWorker is { IsCompleted: false } || _safetyWorker is { IsCompleted: false }
                || _safetyRoutes.Count >= 8 || _safetyRoutes.Values.Count(r => r.Parent == parent.Id) >= 4)
                return Unavailable("native-safety-route-work-limit");
            var origin = actor.Position;
            if (request.Kind == "recovery")
            {
                if (request.Origin is not { Length: 2 } || request.Origin.Any(v => !float.IsFinite(v) || Math.Abs(v) > 1_000_000)) return Unavailable("native-recovery-origin-missing");
                origin = new(request.Origin[0], request.Origin[1]);
            }
            else if (request.Origin != null) return Unavailable("native-preparation-origin-is-actual-player");
            var target = new WPos(request.Destination[0], request.Destination[1]);
            var speed = request.Speed ?? World.Client.MoveSpeed; var radius = request.Radius ?? actor.HitboxRadius;
            if ((origin - actor.Position).LengthSq() > 256 * 256 || (target - actor.Position).LengthSq() > 256 * 256
                || !float.IsFinite(World.Client.MoveSpeed) || World.Client.MoveSpeed is <= 0 or > 20 || !float.IsFinite(actor.HitboxRadius)
                || !float.IsFinite(speed) || speed <= 0 || speed > World.Client.MoveSpeed || !float.IsFinite(radius) || radius < actor.HitboxRadius || radius is < 0 or > 5)
                return Unavailable("native-safety-route-model-invalid");
            DateTime? deadline = null;
            if (request.MustArriveByTicks != null)
            {
                if (!long.TryParse(request.MustArriveByTicks, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                    || ticks < World.CurrentTime.Ticks || ticks > DateTime.MaxValue.Ticks) return Unavailable("native-safety-route-deadline-invalid");
                deadline = new(ticks, World.CurrentTime.Kind);
            }
            if (request.Kind == "preparation" && parent.NativeDeadline is { } nativeDeadline && (deadline == null || nativeDeadline < deadline)) deadline = nativeDeadline;
            var source = request.Kind == "preparation" ? maps.TimedPreparation : maps.Landing;
            var strict = StrictRouteRaster(source);
            if (strict == null) return Unavailable("native-safety-route-raster-unavailable");
            var now = World.CurrentTime; var model = new IntegrationRoutes.Model(radius, speed, request.DepartureDelaySeconds, 0.25f);
            var sourceAge = (float)(now - parent.WorldTime).TotalSeconds;
            var work = Task.Run(() => IntegrationSearch.Run(strict, origin, target, request.DepartureDelaySeconds + sourceAge, speed));
            var job = new SafetyRouteJob(Guid.NewGuid().ToString("N"), request.OwnerToken, parent.Id, request.Kind, origin, target, now,
                model, request.DestinationTolerance, deadline, strict, work);
            _safetyRouteWorker = work; _safetyRoutes.Add(job.Id, job);
            return IntegrationSession.Serialize(new { accepted = true, context = request.Context, snapshotId = parent.Id, routeId = job.Id,
                status = "pending", sourceContext = parent.SourceContext, sourceWorldTimeTicks = T(parent.WorldTime), requestedWorldTimeTicks = T(now) });
        }
    }
    internal string PollSafetyRoute(string json)
    {
        var request = IntegrationSession.Parse<SafetyRoutePoll>(json);
        lock (_safetyGate)
        {
            var parent = request == null ? null : SafetySource(request.Context, request.OwnerToken, request.SnapshotId);
            PruneSafetyRoutes();
            if (parent == null || !_safetyRoutes.TryGetValue(request!.RouteId, out var job) || job.Parent != parent.Id || job.Owner != request.OwnerToken)
                return Unavailable("native-safety-route-retired");
            if (!job.Work.IsCompleted) return IntegrationSession.Serialize(new { available = true, context = request.Context, request.SnapshotId, request.RouteId, status = "pending" });
            if (!job.Work.IsCompletedSuccessfully) { _ = job.Work.Exception; return Unavailable("native-safety-route-worker-failed"); }
            if (World.Party.Player() is not { } actor || job.Model.Radius < actor.HitboxRadius || job.Model.Speed > World.Client.MoveSpeed
                || job.Kind == "preparation" && (actor.Position - job.Origin).LengthSq() > 0.01f)
                return Unavailable("native-safety-route-origin-or-model-changed");
            var result = job.Work.Result;
            var validation = new IntegrationRoutes.Result("unknown", "native-witness-incomplete");
            if (result.WitnessComplete && result.Route.Length is >= 1 and <= IntegrationRoutes.MaxPoints)
                validation = IntegrationRasterRoutes.Evaluate(job.Raster, result.Route, job.Model,
                    (float)(World.CurrentTime - parent.WorldTime).TotalSeconds, new());
            var satisfied = result.Endpoint is { } end && (end - job.Destination).LengthSq() <= job.Tolerance * job.Tolerance;
            var arrival = result.TimeToGoal is { } seconds && float.IsFinite(seconds)
                ? World.CurrentTime.AddSeconds(job.Model.DepartureDelaySeconds + Math.Max(seconds,
                    validation.TravelSeconds ?? result.Route.Zip(result.Route.Skip(1), (a, b) => (b - a).Length()).Sum() / job.Model.Speed)) : (DateTime?)null;
            if (!satisfied && validation.Status == "clear") validation = new("blocked", "native-destination-not-reached");
            if (job.Deadline is { } deadline && (arrival == null || arrival.Value.AddSeconds(NavigationDecision.ActivationTimeCushion + 0.25) > deadline))
                validation = new("blocked", "native-route-arrival-deadline");
            return IntegrationSession.Serialize(new { available = true, context = request.Context, snapshotId = parent.Id, request.RouteId,
                status = validation.Status, validation, sourceContext = parent.SourceContext, sourceWorldTimeTicks = T(parent.WorldTime),
                requestedWorldTimeTicks = T(job.RequestedTime), validatedWorldTimeTicks = T(World.CurrentTime), job.Kind,
                origin = P(job.Origin), destination = P(job.Destination), endpoint = P(result.Endpoint), witness = result.Route.Select(p => P(p)).ToArray(),
                result.WitnessComplete, destinationSatisfied = satisfied, nativeTimeToGoal = result.TimeToGoal, result.NativeScore,
                arrivalWorldTimeTicks = arrival is { } at ? T(at) : null, mustArriveByTicks = job.Deadline is { } limit ? T(limit) : null,
                model = job.Model, hypotheticalOrigin = job.Kind == "recovery", strictUnion = true,
                deadlineMarginSeconds = NavigationDecision.ActivationTimeCushion + 0.25f,
                scope = "complete native ThetaStar witness plus swept footprint and original unrelaxed native warning exit limits; original snapshot constraints rechecked; recovery origin is a caller hypothesis, not displacement approval",
                doesNotAuthorizeMovementOrActions = true });
        }
    }
}
