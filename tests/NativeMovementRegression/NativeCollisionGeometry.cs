using System.Numerics;
using BossMod;
using BossMod.Pathfinding;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void NativeCollisionGeometry()
    {
        Check(NativeMovementCollision.Includes(0x2000) && NativeMovementCollision.Includes(0x17002), "walking material filter retains native walking surfaces");
        Check(!NativeMovementCollision.Includes(0x1000) && !NativeMovementCollision.Includes(0x4000)
            && !NativeMovementCollision.Includes(0x2800), "camera-only, targeting-only and excluded materials do not become walking obstacles");
        Vector3? Flat(Vector3 p, Vector3 d, float length) => d.Y < -.9f ? new(p.X, 0, p.Z) : null;
        var floorCalls = 0; var sweeps = 0;
        var route = new[] { new MovementStage(MovementStageKind.Walk, default, new(6, 0), 0, 1) };
        var block = NativeRoutePhysicalObservation.Inspect(default, .5f, route,
            (p, d, length) => { ++floorCalls; return Flat(p, d, length); },
            (p, d, length, radius) => { ++sweeps; Check(radius == .5f, "sweep retains actual actor footprint"); return null; });
        Check(block == null && floorCalls == 35 && sweeps == 6,
            "six-step route uses 41 queries versus previous 96 by sharing exact consecutive floor evidence");
        var wall = NativePhysicalProbe.Evaluate(default, Vector3.UnitX, .5f, Flat,
            (p, d, length, radius) => new(.6f, p.Y, .2f));
        Check(wall.Status == "blocked" && wall.ContactPoint == new Vector3(.6f, 1.01f, .2f),
            "swept body catches narrow off-center objects and preserves the measured contact");
        var incomplete = NativePhysicalProbe.Evaluate(default, Vector3.UnitX, .5f,
            (p, d, length) => p.X <= .5f ? new(p.X, 0, p.Z) : null, (_, _, _, _) => null, groundCoverage: false);
        Check(incomplete.Status == "unknown", "streaming gaps are not proven cliffs");
        var positive = NativePhysicalProbe.Evaluate(default, Vector3.UnitX, .5f, (_, _, _) => null,
            (p, d, length, radius) => p + d * .5f, groundCoverage: false);
        Check(positive.Status == "blocked", "positive walking collision remains evidence while ground is incomplete");
        var climbed = false;
        Check(NativeRoutePhysicalObservation.Inspect(default, .5f, route,
            (p, d, length) => new(p.X, p.X * .25f, p.Z),
            (p, d, length, radius) => { climbed |= d.Y > 0; return null; }) == null && climbed,
            "body sweeps follow observed slopes rather than intersecting rising ground horizontally");
        floorCalls = 0;
        NativeRoutePhysicalObservation.Inspect(default, .5f,
            [new(MovementStageKind.Walk, default, new(1, 0), 0, 1),
             new(MovementStageKind.Teleport, new(1, 0), new(4, 0), 1, 1),
             new(MovementStageKind.Walk, new(4, 0), new(5, 0), 1, 2)],
            (p, d, length) => { ++floorCalls; return Flat(p, d, length); }, (_, _, _, _) => null);
        Check(floorCalls == 20, "floor evidence is not reused across displacement");

        Check(NativePhysicalObstacle.TouchesCell(new(-1, -.5f), new(2, -.5f), .5f, 0, 0), "capsule edge tangency is inclusive");
        Check(!NativePhysicalObstacle.TouchesCell(new(-1, -.501f), new(2, -.501f), .5f, 0, 0), "clear gap outside capsule is retained");
        Check(NativePhysicalObstacle.TouchesCell(new(-.3f, -.4f), new(-.3f, -.4f), .5f, 0, 0), "degenerate observed point has circular corner clearance");
        Check(!NativePhysicalObstacle.TouchesCell(new(-.4f, -.4f), new(-.4f, -.4f), .5f, 0, 0), "capsule does not replace circular clearance with square bounds");
        var nearWall = Grid(4, 4);
        var occupied = nearWall.GridToWorld(1, 1, .1f, .5f);
        var wallStart = nearWall.GridToWorld(1, 0, .8f, .5f);
        var wallEnd = nearWall.GridToWorld(1, 3, .8f, .5f);
        var occupiedIndex = 1 + 4;
        nearWall.PixelMaxG[occupiedIndex] = 2; // Independent authored warning remains binding.
        new NativePhysicalObstacle(wallStart, wallEnd, .5f).Apply(nearWall, null, null, occupied);
        Check(nearWall.PixelMaxG[occupiedIndex] == 2 && nearWall.PixelMaxG[2 + 4] == -1000,
            "raster padding cannot trap a clear exact origin or erase its authored deadline");
        var overlapping = Grid(4, 4);
        new NativePhysicalObstacle(wallStart, wallEnd, .5f).Apply(overlapping, null, null,
            overlapping.GridToWorld(1, 1, .7f, .5f));
        Check(overlapping.PixelMaxG[occupiedIndex] == -1000, "actual observed footprint overlap is not exempted as rounding");
        var random = new Random(4711);
        for (var i = 0; i < 500; ++i)
        {
            var a = new Vector2(random.NextSingle() * 8 - 4, random.NextSingle() * 8 - 4);
            var b = new Vector2(random.NextSingle() * 8 - 4, random.NextSingle() * 8 - 4);
            var radius = random.NextSingle();
            for (var y = -2; y <= 2; ++y)
                for (var x = -2; x <= 2; ++x)
                    Check(NativePhysicalObstacle.TouchesCell(a, b, radius, x, y) == NativePhysicalObstacle.TouchesCell(b, a, radius, x, y), "capsule raster independent of surface direction");
        }
        var hit = new NativeMovementCollision.Hit(new(0, 1, 0), new(0, 0, -2), new(0, 2, -2), new(0, 1, 2), Vector3.UnitX, 0x2000);
        var surface = NativePhysicalObstacle.FromHit(hit, .5f);
        Check(surface.A.X == 0 && surface.B.X == 0 && Math.Abs(surface.A.Z - surface.B.Z) == 4, "observed triangle is clipped at actual contact height");
        Check(NativePhysicalObstacle.FromHit(hit with { Point = new(-.5f, 1, 0) }, .5f) == surface, "sweep centre offset from triangle is expanded from the actual surface exactly once");
        var primitive = NativePhysicalObstacle.FromHit(hit with { A = default, B = default, C = default }, .5f);
        Check(primitive.A == new WPos(hit.Point) && primitive.B == primitive.A && primitive.Radius == 0, "unset primitive vertices never manufacture a wall or expand the swept centre twice");
        foreach (var rotation in new[] { 0f, .7f })
            foreach (var resolution in new[] { .5f, 1f })
            {
                var map = new Map(); map.InitGrid(resolution, new(3, -5), 12, 12, new(rotation));
                var a = map.GridToWorld(5, 3, .5f, .5f); var b = map.GridToWorld(5, 8, .5f, .5f);
                var observed = new NativePhysicalObstacle(a, b, .25f * resolution);
                var strict = map.PixelMaxG.ToArray(); var blocks = new byte[144];
                observed.Apply(map, strict, blocks);
                Check(map.PixelMaxG[5 + 5 * 12] == -1000 && strict[5 + 5 * 12] == -1000 && blocks[5 + 5 * 12] == 2,
                    "surface marks the native, strict and classified maps consistently under rotation and scale");
                var start = 2 + 5 * 12; var goal = 9 + 5 * 12;
                map.PixelPriority[goal] = 10;
                var plan = JointMovementSearch.Search(Context(map, start, speed: 6), Exhaustive);
                Check(plan.UsefulDestination == Point(map, goal) && plan.Stages.Any(s => s.To.Z != Point(map, start).Z),
                    "observed wall shares the native search and preserves useful destination through a detour");
                Check(plan.Stages.All(s => TimedTraversal.Check(map, s.From, s.To, s.StartSeconds, s.EndSeconds - s.StartSeconds, 0, out _)),
                    "every detour leg respects the same frozen physical map");
            }
        Console.WriteLine("Physical collision: exact surface raster, movement materials, slope following and 41/96 route-query reuse checked.");
    }
}
