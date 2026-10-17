using BossMod;
using BossMod.Pathfinding;
using BossMod.Integration;
using System.Reflection;

static partial class Checks
{
    private static void NativeSearch()
    {
        var assembly = typeof(NavigationDecision).Assembly;
        var rasterType = assembly.GetType("BossMod.Pathfinding.NavigationRaster", true)!;
        var search = assembly.GetType("BossMod.Integration.IntegrationSearch", true)!;
        var run = search.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)!;
        var map = new Map();
        map.InitGrid(1, default, 16, 16);
        var blocks = new byte[256];
        var physical = new Bitmap(16, 16, default, default, 1024);
        // A wall with a real opening above it: direct line is blocked, a native detour exists.
        for (var y = 4; y <= 11; ++y) { blocks[y * 16 + 8] = 2; map.PixelMaxG[y * 16 + 8] = -1; physical[8, y] = true; }
        var nativeRaster = Activator.CreateInstance(rasterType, BindingFlags.Instance | BindingFlags.NonPublic, null,
            [map, null, blocks], null)!;
        object Search(WPos from, WPos target, float elapsed = 0, float speed = 6)
            => run.Invoke(null, [nativeRaster, from, target, elapsed, speed])!;
        var result = Search(new(-4, 0), new(4, 0));
        var route = (WPos[])Property(result, "Route");
        Check((bool)Property(result, "WitnessComplete") && route.Length > 2 && route[0] == new WPos(-4, 0),
            "native search returns a complete witness from the requested origin around an obstructed direct line");
        Check(((WPos?)Property(result, "Endpoint")) is { } endpoint && (endpoint - new WPos(4, 0)).LengthSq() < 4,
            "native detour reaches a raster candidate near the requested destination");
        Check((string)Property(result, "Status") == "candidate", "worker search never calls an unvalidated witness safe");
        var sourceRaster = new NavigationRaster(map, null, blocks);
        var validation = IntegrationRoutes.Evaluate(new(sourceRaster, new(physical, new(0, 0, 16, 16)), [], [], DateTime.UtcNow), route,
            new(0, 6, 0, 0.25f), new(clock: () => 0, frequency: 1000));
        Check(validation.Status == "clear", "complete native detour can be independently validated through the current swept-route checker");
        var copied = sourceRaster.CopyForSearch(0, 256)!;
        copied.PixelMaxG[0] = -1;
        Check(sourceRaster.CopyForSearch(0, 256)!.PixelMaxG[0] == float.MaxValue, "search map mutation cannot affect the immutable source raster");
        Check(sourceRaster.CopyForSearch(0, 255) == null && sourceRaster.CopyForSearch(-1, 256) == null,
            "search bounds and invalid time origins reject without partial geometry");
        Check((string)Property(Search(default, default, speed: float.NaN), "Status") == "unknown", "invalid worker speed returns unknown");
        Check((string)Property(Search(new(999, 999), default), "Status") == "unknown", "unmapped origin cannot become a safe search result");
    }
}
