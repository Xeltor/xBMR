using BossMod.Pathfinding;

namespace BossMod.Integration;

// Runs on a worker against copied, immutable published data. Never mutates native navigation.
internal static class IntegrationSearch
{
    internal const int MaxCells = 16384;
    internal sealed record Result(string Status, string Reason, WPos[] Route, WPos? Endpoint,
        float? TimeToGoal, string? NativeScore, bool WitnessComplete, DateTimeOffset CompletedUtc);
    internal static Result Run(NavigationRaster raster, WPos origin, WPos target, float elapsedSeconds, float speed)
    {
        Result Unknown(string reason) => new("unknown", reason, [], null, null, null, false, DateTimeOffset.UtcNow);
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Z) || !float.IsFinite(target.X) || !float.IsFinite(target.Z)
            || !float.IsFinite(speed) || speed is <= 0 or > 20)
            return Unknown("invalid-search-model");
        var map = raster.CopyForSearch(elapsedSeconds, MaxCells);
        if (map == null)
            return Unknown("unsupported-or-bounded-out-raster");
        var grid = map.WorldToGrid(origin);
        if (!map.InBounds(grid.x, grid.y))
            return Unknown("origin-outside-search-map");
        NavigationDecision.RasterizeGoalZones(map, [p => 16f / (1 + (p - target).Length())]);
        var search = new ThetaStar();
        search.Start(map, origin, 1f / speed);
        var best = search.Execute();
        ref var node = ref search.NodeByIndex(best);
        var route = NavigationDecision.CopyWitness(search, best, origin, out var complete);
        return new("candidate", "requires-current-complete-route-validation", route, route.Length == 0 ? null : route[^1],
            float.IsFinite(node.GScore) ? node.GScore : null, node.Score.ToString(), complete, DateTimeOffset.UtcNow);
    }
}
