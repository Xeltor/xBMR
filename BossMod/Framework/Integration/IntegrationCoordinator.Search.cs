using BossMod.Pathfinding;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    private sealed record SearchJob(string Id, string Owner, string Scope, NavigationPublication Source, WPos Origin, WPos Target,
        IntegrationRoutes.Model Model, string Mode, float Tolerance, Task<IntegrationSearch.Result> Work);
    private readonly Dictionary<string, SearchJob> _searches = [];
    private readonly object _searchGate = new();
    private Task<IntegrationSearch.Result>? _searchWorker;

    private void DisposeSearches()
    {
        lock (_searchGate)
        {
            foreach (var job in _searches.Values)
                _ = job.Work.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            _searches.Clear();
        }
    }

    internal sealed record SearchRequest(IntegrationSession.Context Context, string OwnerToken, long Generation, string Mode,
        float[]? Destination = null, float? Radius = null, float? Speed = null, float DepartureDelaySeconds = 0,
        float AdditionalTimeMarginSeconds = 0.25f, float DestinationTolerance = 1);
    internal sealed record SearchPoll(IntegrationSession.Context Context, string OwnerToken, string SearchId);

    private void PruneSearches()
    {
        var player = World.Party.Player();
        foreach (var job in _searches.Values.ToArray())
        {
            if (player == null || !Session.IsOwnerActive(job.Owner) || job.Scope != ControlScope()
                || !job.Source.Matches(Session.InstanceGeneration, job.Source.Source.Generation, player.InstanceID, World.CurrentZone, World.CurrentCFCID))
            {
                _searches.Remove(job.Id);
                // Worker inputs contain no live state. Observe any discarded failure, without waiting.
                _ = job.Work.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }

    private bool SearchModeAllowed() => ControlsAllowed() && _encounterMovement == null && hints.Teleporters.Count == 0
        && hints.ImminentSpecialMode.mode == AIHints.SpecialMode.Normal && !World.Client.Flying;

    internal string StartSearch(string json)
    {
        lock (_searchGate) return StartSearchCore(json);
    }

    private string StartSearchCore(string json)
    {
        var request = IntegrationSession.Parse<SearchRequest>(json);
        if (request == null || !Session.Matches(request.Context))
            return Unavailable("invalid-search-context");
        var publication = QueryNavigation(request.Context, request.Generation);
        var player = World.Party.Player();
        if (request == null || publication?.Raster == null || publication.MapSource == null || publication.HasTeleporters || player == null
            || !Session.IsOwnerActive(request.OwnerToken) || !SearchModeAllowed())
            return Unavailable("invalid-search-source-owner-or-mode");
        if (request.Mode is not ("destination" or "nearest-safe-raster"))
            return Unavailable("unsupported-search-mode");
        var target = player.Position;
        if (request.Mode == "destination")
        {
            if (request.Destination is not { Length: 2 } || request.Destination.Any(v => !float.IsFinite(v) || Math.Abs(v) > 1_000_000))
                return Unavailable("invalid-search-destination");
            target = new(request.Destination[0], request.Destination[1]);
            if ((target - player.Position).LengthSq() > 256 * 256)
                return Unavailable("search-distance-limit");
        }
        else if (request.Destination != null)
            return Unavailable("nearest-search-has-no-explicit-destination");
        var speed = request.Speed ?? World.Client.MoveSpeed;
        var radius = request.Radius ?? player.HitboxRadius;
        var elapsed = (float)(World.CurrentTime - publication.Source.WorldTime).TotalSeconds;
        if (!float.IsFinite(World.Client.MoveSpeed) || World.Client.MoveSpeed <= 0 || !float.IsFinite(player.HitboxRadius)
            || (long)publication.Raster.Width * publication.Raster.Height > IntegrationSearch.MaxCells || !float.IsFinite(elapsed) || elapsed < 0 || elapsed >= 0.25f
            || !float.IsFinite(speed) || speed is <= 0 or > 20 || speed > World.Client.MoveSpeed + 0.001f
            || !float.IsFinite(radius) || radius < player.HitboxRadius || radius is < 0 or > 5
            || !float.IsFinite(request.DepartureDelaySeconds) || request.DepartureDelaySeconds is < 0 or > 10
            || !float.IsFinite(request.AdditionalTimeMarginSeconds) || request.AdditionalTimeMarginSeconds is < 0.1f or > 2
            || !float.IsFinite(request.DestinationTolerance) || request.DestinationTolerance is < 0.25f or > 3)
            return Unavailable("unsupported-or-bounded-out-search-model");
        PruneSearches();
        if (_searchWorker is { IsCompleted: false } || _searches.Count >= 4 || _searches.Values.Any(j => j.Owner == request.OwnerToken))
            return Unavailable("search-busy-or-owner-limit");
        var origin = player.Position;
        var raster = publication.Raster;
        var work = Task.Run(() => IntegrationSearch.Run(raster, origin, target, elapsed + request.DepartureDelaySeconds, speed));
        var id = Guid.NewGuid().ToString("N");
        _searchWorker = work;
        _searches.Add(id, new(id, request.OwnerToken, ControlScope(), publication, origin, target,
            new(radius, speed, request.DepartureDelaySeconds, request.AdditionalTimeMarginSeconds), request.Mode, request.DestinationTolerance, work));
        return IntegrationSession.Serialize(new { accepted = true, reason = "search-started", searchId = id, status = "pending",
            context = request.Context, sourceNavigationGeneration = publication.Source.Generation });
    }

    internal string PollSearch(string json)
    {
        lock (_searchGate) return PollSearchCore(json);
    }

    private string PollSearchCore(string json)
    {
        var request = IntegrationSession.Parse<SearchPoll>(json);
        if (request == null || !Session.Matches(request.Context))
            return Unavailable("invalid-search-poll-context");
        PruneSearches();
        if (request.SearchId == null || !_searches.TryGetValue(request.SearchId, out var job) || job.Owner != request.OwnerToken
            || !SearchModeAllowed() || World.Party.Player() is not { } player)
            return Unavailable("expired-or-unavailable-search");
        if (!job.Work.IsCompleted)
            return IntegrationSession.Serialize(new { available = true, context = request.Context, request.SearchId, status = "pending" });
        if (!job.Work.IsCompletedSuccessfully)
        {
            _ = job.Work.Exception;
            return Unavailable("native-search-failed");
        }
        if ((player.Position - job.Origin).LengthSq() > 0.01f || !ReferenceEquals(job.Source.MapSource!.Bounds, hints.PathfindMapBounds)
            || job.Source.MapSource.Center != hints.PathfindMapCenter || job.Source.Source.ProjectionLayer != hints.PathfindMapArenaProjectionLayer
            || job.Model.Radius < player.HitboxRadius || job.Model.Speed > World.Client.MoveSpeed + 0.001f)
            return Unavailable("changed-search-origin-geometry-or-model");
        if (amex.MoveMightInterruptCast && job.Model.DepartureDelaySeconds + 0.001f < Math.Max(0, amex.CastTimeRemaining - 0.5f)
            && !hints.ForceCancelCastMechanic && !ai.Controller.AllowInterruptingCastByMovement)
            return Unavailable("cast-departure-not-authorized");
        var result = job.Work.Result; // completion was checked; never blocks the frame
        IntegrationRoutes.Result validation = new("unknown", "incomplete-or-unavailable-native-witness");
        if (result.WitnessComplete && result.Route.Length is > 0 and <= IntegrationRoutes.MaxPoints)
        {
            var region = hints.PathfindMapObstacles;
            if (region.Bitmap == null || (long)region.Rect.Width * region.Rect.Height <= NavigationRaster.MaxCells)
            {
                var source = new IntegrationRoutes.Source(job.Source.Raster!, hints.CapturePathfindMapSource().Obstacles,
                    hints.TemporaryObstacles.ToArray(), hints.ForbiddenZones.Select(w => (w.shapeDistance, w.activation, w.Source)).ToArray(),
                    World.CurrentTime, hints.PathfindMapBounds.AllowObstacleMap);
                validation = IntegrationRoutes.Evaluate(source, result.Route, job.Model, new());
            }
        }
        var destinationSatisfied = result.Endpoint is { } endpoint && (endpoint - job.Target).LengthSq() <= job.Tolerance * job.Tolerance;
        return IntegrationSession.Serialize(new
        {
            available = true, context = request.Context, request.SearchId, status = validation.Status, validation,
            job.Mode, sourceNavigationGeneration = job.Source.Source.Generation, sourceHintGeneration = job.Source.Source.HintGeneration,
            validatedHintGeneration = request.Context.FrameId, sourceTimeTicks = T(job.Source.Source.WorldTime), result.CompletedUtc,
            origin = P(job.Origin), requestedDestination = job.Mode == "destination" ? P(job.Target) : null,
            endpoint = P(result.Endpoint), witness = result.Route.Select(p => P(p)).ToArray(), result.WitnessComplete,
            nativeTimeToGoal = result.TimeToGoal, result.NativeScore, destinationSatisfied,
            nearestMeaning = "best native raster candidate; no continuous-space nearest-point or global optimality proof",
            model = job.Model, doesNotAuthorizeMovementOrActions = true
        });
    }
}
