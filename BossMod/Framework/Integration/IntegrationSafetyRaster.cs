using BossMod.Pathfinding;

namespace BossMod.Integration;

// Exact native rasterization over copied constraints. Least-danger fallback is never exposed as clear.
internal static class IntegrationSafetyRaster
{
    internal sealed record Region(ShapeDistance Shape, DateTime Activation, ulong Source);
    internal sealed record Pair(NavigationRaster Preparation, NavigationRaster Landing, bool PreparationRelaxed, bool LandingRelaxed, NavigationRaster TimedPreparation);
    internal static Pair? Build(NavigationMapSource mapSource, Region[] preparation, Region[] landing,
        ShapeDistance[] obstacles, DateTime referenceTime, Func<bool> live)
    {
        NavigationRaster? Raster(Region[] warnings, out bool relaxed)
        {
            relaxed = false;
            if (!live()) return null;
            var map = new Map(); var blocks = mapSource.InitForWorker(map);
            if (blocks == null || (long)map.Width * map.Height > IntegrationSearch.MaxCells || !live()) return null;
            NavigationDecision.RasterizeVoidzones(map, obstacles);
            for (var i = 0; i < blocks.Length; ++i) if (blocks[i] == 0 && map.PixelMaxG[i] < 0) blocks[i] = 3;
            var limits = warnings.Length == 0 ? null : NavigationDecision.RasterizeForbiddenZones(map,
                warnings.Select(z => (z.Shape, z.Activation, z.Source)).ToArray(), referenceTime, true, out relaxed);
            if (limits != null) limits.CopyTo(map.PixelMaxG, 0);
            // A strict cell retains native corner/center behavior but cannot inherit a NaN as clearance.
            // This also verifies row-culling and post-raster fallback output against the copied exact functions.
            var shapes = warnings.Select(w => w.Shape).Concat(obstacles).ToArray();
            Span<WPos> points = stackalloc WPos[5];
            for (var y = 0; y < map.Height; ++y)
                for (var x = 0; x < map.Width; ++x)
                {
                    if (!live()) return null;
                    var index = map.GridToIndex(x, y);
                    if (mapSource.Bounds.AllowObstacleMap && IntegrationRoutes.PhysicalAt(mapSource.Obstacles, x, y) == null)
                    { blocks[index] = 4; map.PixelMaxG[index] = 0; continue; }
                    if (blocks[index] != 0 || map.PixelMaxG[index] != float.MaxValue) { if (blocks[index] == 0) blocks[index] = 4; continue; }
                    points[0] = map.GridToWorld(x, y, 0, 0); points[1] = map.GridToWorld(x, y, 1, 0);
                    points[2] = map.GridToWorld(x, y, 0, 1); points[3] = map.GridToWorld(x, y, 1, 1); points[4] = map.GridToWorld(x, y, 0.5f, 0.5f);
                    foreach (var shape in shapes)
                    {
                        var clear = true;
                        for (var i = 0; i < points.Length; ++i)
                        {
                            var distance = shape.Distance(points[i]);
                            if (!float.IsFinite(distance) || i == 4 && distance <= map.Resolution * 0.5f || i < 4 && shape.Contains(points[i]))
                            { clear = false; break; }
                        }
                        if (!clear) { blocks[index] = 4; map.PixelMaxG[index] = 0; break; }
                    }
                }
            return live() ? new(map, null, blocks) : null;
        }
        NavigationRaster? TimedPreparation()
        {
            var map = new Map(); var blocks = mapSource.InitForWorker(map);
            if (blocks == null || !live()) return null;
            NavigationDecision.RasterizeVoidzones(map, obstacles);
            for (var i = 0; i < blocks.Length; ++i) if (blocks[i] == 0 && map.PixelMaxG[i] < 0) blocks[i] = 3;
            _ = NavigationDecision.RasterizeForbiddenZones(map, preparation.Select(z => (z.Shape, z.Activation, z.Source)).ToArray(),
                referenceTime, false, out _, allowRelaxation: false);
            for (var y = 0; y < map.Height; ++y) for (var x = 0; x < map.Width; ++x)
            {
                if (!live()) return null;
                var index = map.GridToIndex(x, y);
                if (mapSource.Bounds.AllowObstacleMap && IntegrationRoutes.PhysicalAt(mapSource.Obstacles, x, y) == null)
                { blocks[index] = 4; map.PixelMaxG[index] = 0; }
                if (blocks[index] != 0) continue;
                foreach (var region in preparation)
                    for (var point = 0; point < 5; ++point)
                    {
                        var p = point == 4 ? map.GridToWorld(x, y, 0.5f, 0.5f) : map.GridToWorld(x, y, point & 1, point >> 1);
                        var distance = region.Shape.Distance(p);
                        // Invalid functions/culling remain blocked even before activation.
                        if (!float.IsFinite(distance) || map.PixelMaxG[index] == float.MaxValue
                            && (point == 4 ? distance <= map.Resolution * 0.5f : region.Shape.Contains(p)))
                        { blocks[index] = 4; map.PixelMaxG[index] = 0; break; }
                    }
            }
            return live() ? new(map, null, blocks) : null;
        }
        var timed = TimedPreparation();
        if (timed == null) return null;
        var prep = Raster(preparation, out var prepRelaxed);
        if (prep == null) return null;
        var land = Raster(landing, out var landRelaxed);
        return land == null ? null : new(prep, land, prepRelaxed, landRelaxed, timed);
    }
}
