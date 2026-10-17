using BossMod.Pathfinding;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record SafetySegmentsRequest(IntegrationSession.Context Context, string OwnerToken, string SnapshotId, string Kind,
        float[][][] Routes, IntegrationRoutes.Model Model);
    internal string EvaluateSafetySegments(string json)
    {
        var request = IntegrationSession.Parse<SafetySegmentsRequest>(json);
        if (request == null || !Session.Matches(request.Context) || request.Kind is not ("preparation" or "landing")
            || request.Routes is not { Length: >= 1 and <= IntegrationRoutes.MaxRoutes } || request.Model == null
            || request.Routes.Any(r => r == null || r.Length is < 1 or > IntegrationRoutes.MaxPoints
                || r.Any(p => p == null || p.Length != 2 || p.Any(v => !float.IsFinite(v) || Math.Abs(v) > 1_000_000))))
            return Unavailable("invalid-explicit-safety-segments");
        lock (_safetyGate)
        {
            var parent = SafetySource(request.Context, request.OwnerToken, request.SnapshotId);
            if (parent?.Work.IsCompletedSuccessfully != true || parent.Work.Result is not { } maps || World.Party.Player() is not { } actor
                || !float.IsFinite(actor.HitboxRadius) || request.Model.Radius < actor.HitboxRadius || !float.IsFinite(World.Client.MoveSpeed)
                || request.Kind == "preparation" && request.Model.Speed > World.Client.MoveSpeed)
                return Unavailable("explicit-safety-segments-source-or-model-invalid");
            var raster = StrictRouteRaster(request.Kind == "preparation" ? maps.TimedPreparation : maps.Landing);
            if (raster == null) return Unavailable("explicit-safety-segments-raster-unavailable");
            var budget = new IntegrationRoutes.Budget();
            var routes = request.Routes.Select(r => r.Select(p => new WPos(p[0], p[1])).ToArray()).ToArray();
            if (routes.Any(r => r.Any(p => (p - actor.Position).LengthSq() > 256 * 256))) return Unavailable("explicit-safety-segments-range-limit");
            var results = routes.Select(r => IntegrationRasterRoutes.Evaluate(raster, r, request.Model, (float)(World.CurrentTime - parent.WorldTime).TotalSeconds, budget)).ToArray();
            if (SafetySource(request.Context, request.OwnerToken, request.SnapshotId) == null) return Unavailable("explicit-safety-segments-source-changed");
            return IntegrationSession.Serialize(new { available = true, context = request.Context, snapshotId = parent.Id,
                sourceContext = parent.SourceContext, sourceWorldTimeTicks = T(parent.WorldTime), sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs,
                request.Kind, model = request.Model, routes = request.Routes, results, complete = results.All(r => r.Status != "unknown"),
                exactSubmittedSegments = true, detourSearchPerformed = false, forcedPhysicsVerified = false, actionPermissionGranted = false,
                meaning = "swept footprint along exact supplied linear segments in strict native sampled cells; model/timing remain caller hypotheses; no native knockback transit semantics, immunity, action or input authority" });
        }
    }
}
