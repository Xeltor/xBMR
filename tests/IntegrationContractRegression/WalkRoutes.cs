using BossMod;
using BossMod.Integration;
using BossMod.Pathfinding;

static partial class Checks
{
    private static void CurrentWalkRoutes()
    {
        var now = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        var map = new Map(); map.InitGrid(1, default, 40, 40);
        var blocks = new byte[1600];
        NavigationRaster Raster() => new(map, null, blocks);
        var route = new WPos[] { new(-8, 0), new(8, 0) };
        var model = new IntegrationRoutes.Model(0.5f, 6, 0, 0.25f);
        IntegrationRoutes.Source Source((ShapeDistance, DateTime, ulong)[]? warnings = null, ShapeDistance[]? obstacles = null,
            Bitmap.Region physical = default, bool requireCoverage = false)
            => new(Raster(), physical, obstacles ?? [], warnings ?? [], now, requireCoverage);
        IntegrationRoutes.Result Evaluate(IntegrationRoutes.Source source, WPos[]? points = null, IntegrationRoutes.Model? movement = null,
            IntegrationRoutes.Budget? budget = null)
            => IntegrationRoutes.Evaluate(source, points ?? route, movement ?? model, budget ?? new(() => 0, 1000));
        var clear = Evaluate(Source());
        Check(clear.Status == "clear", "empty native arena accepts a complete walk route");
        Near(clear.TravelSeconds, 16f / 6, "route travel includes every segment");
        var warnings = new (ShapeDistance, DateTime, ulong)[]
        {
            (new SDCircle(new(-2, 0), 1), now.AddSeconds(10), 0xA)
        };
        Check(Evaluate(Source(warnings)).Status == "clear", "future warning can be crossed before its activation with margins");
        var early = new[] { (warnings[0].Item1, now.AddSeconds(1), 0xAuL) };
        Check(Evaluate(Source(early)).Status == "blocked", "late traversal of the same warning is rejected");
        var overlapping = new (ShapeDistance, DateTime, ulong)[]
        {
            (new SDCircle(default, 1), now.AddSeconds(3.5), 1),
            (new SDCircle(new(5, 0), 1), now.AddSeconds(5), 2)
        };
        Check(Evaluate(Source(overlapping)).Status == "clear", "successive warnings are traversable with proven exits");
        overlapping[1] = (overlapping[1].Item1, now.AddSeconds(2), 2);
        var second = Evaluate(Source(overlapping));
        Check(second.Status == "blocked" && second.SourceActorId == "2", "later segment/overlap deadline cannot be skipped");
        var warningAtGoal = new (ShapeDistance, DateTime, ulong)[] { (new SDCircle(default, 2), now.AddSeconds(10), 3) };
        var temporaryGoal = new WPos[] { new(-8, 0), default };
        Check(Evaluate(Source(warningAtGoal), temporaryGoal).Reason == "requires-onward-escape",
            "temporary arrival in a warning needs a complete onward escape");
        Check(Evaluate(Source(warningAtGoal), [new(-8, 0), default, new(8, 0)]).Status == "clear",
            "explicit onward escape before activation validates the complete route");
        Check(Evaluate(Source(warningAtGoal), [default, new(8, 0)], model with { DepartureDelaySeconds = 2 }).Status == "clear",
            "a bounded departure delay is included before a timed exit");
        Check(Evaluate(Source(warningAtGoal), [default, new(8, 0)], model with { DepartureDelaySeconds = 10 }).Status == "blocked",
            "cast/action delay cannot be omitted to fabricate a warning crossing");
        var currentDanger = new (ShapeDistance, DateTime, ulong)[] { (new SDCircle(default, 0.01f), now, 4) };
        Check(Evaluate(Source(currentDanger), [new(-1, 0), new(1, 0)], model with { Radius = 0 }).Status == "blocked",
            "swept intervals catch a narrow active danger between endpoints");
        Check(Evaluate(Source(obstacles: [new SDCircle(default, 1)])).Status == "blocked", "current temporary obstruction is checked as permanent for this query");
        Array.Fill(map.PixelMaxG, 0);
        Check(Evaluate(Source()).Status == "clear", "resolved source-time warnings do not become permanent walls when current hints clear them");
        Array.Fill(map.PixelMaxG, float.MaxValue);
        blocks[20 * 40 + 20] = 1;
        Check(Evaluate(Source()).Reason == "arena-raster-boundary", "native arena raster blocks a direct walk");
        Check(Evaluate(Source(), [new(-8, 0), new(-2, -4), new(2, -4), new(8, 0)]).Status == "clear",
            "a complete supplied detour is validated instead of replacing it with a straight line");
        blocks[20 * 40 + 20] = 2;
        Check(Evaluate(Source()).Status == "clear", "an obsolete source-map physical obstacle is not treated as a permanent current wall");
        var physical = new Bitmap(40, 40, default, default);
        physical[20, 20] = true;
        Check(Evaluate(Source(physical: physical.FullRegion)).Reason == "physical-obstacle", "current physical obstacle data overrides a stale clear map");
        blocks[20 * 40 + 20] = 3;
        Check(Evaluate(Source()).Status == "clear", "obsolete source-map temporary blocks are replaced by current authored constraints");
        blocks[20 * 40 + 20] = 0;
        blocks[20 * 40 + 25] = 1;
        var first = Evaluate(Source([(new SDCircle(new(-5, 0), 1), now, 5)]));
        Check(first.SourceActorId == "5" && first.FirstBlocker![0] < 0,
            "first blocker compares physical and timed constraints by route order");
        blocks[20 * 40 + 25] = 0;
        blocks[20 * 40 + 20] = byte.MaxValue;
        Check(Evaluate(Source()).Reason == "invalid-arena-raster-cell", "invalid native cells are unavailable, never clear");
        blocks[20 * 40 + 20] = 0;
        Check(Evaluate(Source(requireCoverage: true)).Status == "unknown", "missing required physical-map coverage cannot become clear");
        var binary = new SDComplexPolygonInvertedContains(new ArenaBoundsSquare(5).Shape, default);
        Check(Evaluate(Source(obstacles: [binary])).Reason == "unsupported-distance-bound", "binary/projected geometry is explicit unknown");
        Check(Evaluate(Source(), budget: new(() => 0, 1000, 1)).Reason == "query-budget", "exhausted work budget cannot become clear");
        long clock = 0;
        var timed = new IntegrationRoutes.Budget(() => clock, 1000); clock = 5;
        Check(Evaluate(Source(), budget: timed).Reason == "query-budget", "elapsed query budget cannot become clear");
        Check(Evaluate(Source(), [new(float.NaN, 0)]).Status == "unknown", "nonfinite route rejected");
        Check(Evaluate(Source(), movement: model with { Speed = 0 }).Status == "unknown", "invalid speed cannot create an instantaneous crossing");
        Check(Evaluate(Source(), movement: model with { Speed = 1e-38f }).Reason == "travel-time-limit", "overflowing arrival estimate cannot become clear");
        Check(Evaluate(Source(), [new(-100, 0), new(100, 0)]).Reason == "outside-navigation-window", "outside copied raster remains unknown");

        var shapes = new ShapeDistance[]
        {
            new SDCircle(default, 3), new SDInvertedCircle(default, 3), new SDDonut(default, 2, 4), new SDInvertedDonut(default, 2, 4),
            new SDHalfPlane(default, new(1, 2)), new SDRect(default, new WDir(0, 2), 3, 2, 1),
            new SDInvertedRect(default, new WDir(0, 2), 3, 2, 1), new SDCapsule(default, new WDir(0, 2), 4, 1),
            new SDInvertedCapsule(default, new WDir(0, 2), 4, 1), new SDCone(default, 5, default, 1.8f.Radians()),
            new SDInvertedCone(default, 5, default, 1.8f.Radians())
        };
        var random = new Random(117);
        foreach (var shape in shapes)
        {
            for (var i = 0; i < 100; ++i)
            {
                var a = new WPos(random.NextSingle() * 20 - 10, random.NextSingle() * 20 - 10);
                var b = new WPos(random.NextSingle() * 20 - 10, random.NextSingle() * 20 - 10);
                Check(float.IsFinite(shape.ConservativeLipschitz) && Math.Abs(shape.Distance(a) - shape.Distance(b))
                    <= shape.ConservativeLipschitz * (b - a).Length() + 0.0001f, "native shape distance bound holds including non-unit directions");
            }
        }
        Check(!float.IsFinite(new SDCircle(default, float.NaN).ConservativeLipschitz), "invalid native shape parameters remain unsupported");
    }
}
