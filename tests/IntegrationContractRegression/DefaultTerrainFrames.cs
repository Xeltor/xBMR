using BossMod;
using BossMod.Integration;
using BossMod.Pathfinding;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private class FrameFate(WorldState world, Actor primary) : OpenWorldFate(world, primary);
    private sealed class CustomUpdateFate(WorldState world, Actor primary) : FrameFate(world, primary)
    {
        protected override void UpdateModule() => base.UpdateModule();
    }
    private sealed class CustomHintsFate(WorldState world, Actor primary) : FrameFate(world, primary)
    {
        internal readonly ArenaBounds AuthoredBounds = new ArenaBoundsRect(12, 18, 0.3f.Radians());
        protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
        {
            base.CalculateModuleAIHints(slot, actor, assignment, hints);
            hints.PathfindMapBounds = AuthoredBounds;
            hints.PathfindMapCenter = new(70, 80);
        }
    }

    private static void DefaultTerrainFrames()
    {
        var native = typeof(AIHints).Assembly;
        Type Type(string name) => native.GetType("BossMod." + name, true)!;
        object Blank(string name) => RuntimeHelpers.GetUninitializedObject(Type(name));
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        void Set(object obj, string field, object? value)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
                if (type.GetField(field, BindingFlags.Public | InternalInstance | BindingFlags.DeclaredOnly) is { } found)
                { found.SetValue(obj, value); return; }
            throw new MissingFieldException(field);
        }
        object? Call(object obj, string method, params object?[] args) => obj.GetType().GetMethod(method, BindingFlags.Public | InternalInstance)!.Invoke(obj, args);
        var (world, player, _) = ProjectionFixture();
        var bitmap = new Bitmap(256, 256, default, default);
        var origin = new WPos(0.2f, -0.3f);
        var entry = new ObstacleMapDatabase.Entry(new(-1000), new(1000), origin, 60, 60, "fixture");
        bitmap[112, 104] = true;
        var obstacles = (ObstacleMapManager)Blank("Pathfinding.ObstacleMapManager");
        Set(obstacles, "_tempMapLock", Activator.CreateInstance(typeof(System.Threading.Lock)));
        Set(obstacles, "_entries", new List<(ObstacleMapDatabase.Entry, Bitmap)> { (entry, bitmap) });
        FrameFate Module(Type type)
        {
            var module = (FrameFate)RuntimeHelpers.GetUninitializedObject(type);
            Set(module, nameof(BossModule.WorldState), world);
            Set(module, nameof(BossModule.Arena), new MiniArena(default, new ArenaBoundsCircle(30, AllowObstacleMap: true)));
            Set(module, nameof(BossModule.Components), new List<BossComponent>());
            Set(module, nameof(BossModule.Obstacles), obstacles);
            Set(module, nameof(BossModule.StateMachine), new StateMachine([]));
            Set(module, "_actorArenaProjectionLayers", new Dictionary<ulong, int>());
            return module;
        }
        var fate = Module(typeof(FrameFate));
        var hints = new AIHints();
        var hazard = new SDCircle(new(52, 54), 2);
        hints.ForbiddenZones.Add((hazard, world.FutureTime(4), 123));
        hints.GoalZones.Add(_ => 3);
        var arenaBounds = fate.Bounds;
        void Update(FrameFate module, WPos position)
        {
            player.PosRot = new(position.X, 7, position.Z, 0);
            Call(module, "UpdateModule");
            module.CalculateAIHints(0, player, default, hints);
        }
        object? Alignment() => typeof(AIHints).GetField("DefaultTerrainFrameAlignment", InternalInstance)!.GetValue(hints);
        var config = Service.Config._nodes[Type("AI.AIConfig")];
        var disabled = (bool)config.GetType().GetField("DisableObstacleMaps")!.GetValue(config)!;
        Set(config, "DisableObstacleMaps", false);
        try
        {
            Map Raster()
            {
                var source = Call(hints, "CapturePathfindMapSource")!;
                var map = new Map(); Call(source, "Init", map); return map;
            }
            WPos BlockedWorld(Map map)
            {
                var index = Enumerable.Range(0, map.Width * map.Height).Single(i => map.PixelMaxG[i] < 0);
                return map.GridToWorld(index % map.Width, index / map.Width, 0.5f, 0.5f);
            }
            var expected = origin + bitmap.PixelSize * new WDir(112.5f, 104.5f);
            Update(fate, new(50, 50));
            var center = hints.PathfindMapCenter; var rect = hints.PathfindMapObstacles.Rect;
            Check(Alignment() != null && center != new WPos(50, 50), "native inherited FATE frame aligns a fractional terrain origin to one lattice");
            Check(BlockedWorld(Raster()).AlmostEqual(expected, 0.001f), "native raster places the blocked bitmap cell at its actual world coordinate");
            Check(ReferenceEquals(fate.Bounds, arenaBounds) && fate.Center == new WPos(50, 50), "terrain alignment does not alter authored/display arena center or bounds");
            Check(ReferenceEquals(hints.ForbiddenZones[0].shapeDistance, hazard) && hints.ForbiddenZones[0].Source == 123 && hints.GoalZones.Single()(default) == 3,
                "native frame correction leaves authored hazards, timing, sources and goals intact");
            foreach (var position in new WPos[] { new(50.2f, 50.1f), new(51, 51), new(53, 51), new(54, 50) })
            {
                Update(fate, position);
                Check(hints.PathfindMapCenter == center && hints.PathfindMapObstacles.Rect.Equals(rect),
                    "sub-threshold player movement retains both search origin and its terrain window");
                Check(BlockedWorld(Raster()).AlmostEqual(expected, 0.001f), "terrain cells cannot drift beneath the held native search frame");
            }
            Update(fate, new(56, 50));
            Check(hints.PathfindMapCenter != center && BlockedWorld(Raster()).AlmostEqual(expected, 0.001f),
                "crossing native five-yalm hysteresis advances center/window together without shifting terrain");
            var copied = Call(hints, "CapturePathfindMapSource")!;
            bitmap[112, 104] = false;
            var immutable = new Map(); Call(copied, "Init", immutable);
            Check(BlockedWorld(immutable).AlmostEqual(expected, 0.001f), "aligned source capture owns a terrain copy independent of later bitmap edits");
            bitmap[112, 104] = true;
            Set(config, "DisableObstacleMaps", true);
            var optedOut = Raster();
            Check(optedOut.PixelMaxG.Take(optedOut.Width * optedOut.Height).All(v => v == float.MaxValue),
                "native obstacle-map opt-out remains authoritative after frame alignment");
            Set(config, "DisableObstacleMaps", false);
            var legacy = Module(typeof(CustomUpdateFate));
            Update(legacy, new(50, 50));
            hints.Clear(); Update(legacy, new(53, 51));
            Check(Alignment() == null && hints.PathfindMapCenter == new WPos(50, 50), "custom native frame overrides retain their existing policy");
            Check(!BlockedWorld(Raster()).AlmostEqual(expected, 0.1f), "uncorrected native baseline reproduces the moving terrain window under a held center");

            var custom = (CustomHintsFate)Module(typeof(CustomHintsFate));
            Set(custom, nameof(CustomHintsFate.AuthoredBounds), new ArenaBoundsRect(12, 18, 0.3f.Radians()));
            hints.Clear(); Update(custom, new(50, 50));
            Check(ReferenceEquals(hints.PathfindMapBounds, custom.AuthoredBounds) && hints.PathfindMapCenter == new WPos(70, 80) && Alignment() == null,
                "custom hint overrides retain authored rotated geometry and frame policy");

            object? Align(ObstacleMapDatabase.Entry? e = null, Bitmap? b = null) => Type("Pathfinding.DefaultTerrainFrame")
                .GetMethod("Align", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [hints, e ?? entry, b ?? bitmap]);
            hints.Clear(); hints.PathfindMapCenter = new(-1.2f, -1.7f); hints.PathfindMapObstacles = new(bitmap, new(0, 0, 120, 120));
            var negative = Align()!;
            var negativeCenter = (WPos)Property(negative, "Center");
            Check(Math.Abs((negativeCenter.X - origin.X) / 0.5f - MathF.Round((negativeCenter.X - origin.X) / 0.5f)) < 0.001f,
                "negative lattice coordinates remain integer-aligned without truncation drift");
            hints.PathfindMapArenaProjectionLayer = 3;
            Check(Align() == null, "multi-layer frames remain outside inherited default terrain alignment");
            hints.PathfindMapArenaProjectionLayer = null; hints.PathfindMapBounds = new ArenaBoundsSquare(30);
            Check(Align() == null, "a distinct authored bounds object is never treated as the inherited default window");
            hints.PathfindMapBounds = AIHints.DefaultBounds;
            Check(Align(b: new Bitmap(256, 256, default, default)) == null, "map transition cannot align against a different terrain bitmap");
            var coarse = new Bitmap(256, 256, default, default, 1024);
            hints.PathfindMapObstacles = new(coarse, new(0, 0, 120, 120));
            Check(Align(b: coarse) == null, "terrain/navigation resolution mismatch remains unsupported");
            hints.PathfindMapObstacles = default;
            Check(Align() == null, "absent terrain never produces aligned or complete physical coverage evidence");
            hints.PathfindMapObstacles = new(bitmap, new(0, 0, 120, 120)); hints.PathfindMapCenter = new(float.NaN, 0);
            Check(Align() == null, "invalid frame coordinates cannot produce a terrain alignment");

            hints.Clear(); Update(fate, new(50, 50));
            var manager = (BossModuleManager)Blank("BossModuleManager"); Set(manager, nameof(BossModuleManager.WorldState), world); Set(manager, "_activeModule", fate);
            var coordinator = Blank("Integration.IntegrationCoordinator"); Set(coordinator, "<bossmod>P", manager); Set(coordinator, "<hints>P", hints);
            using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => 1), 1000L); Set(coordinator, "Session", session);
            Call(session, "BeginFrame", "fixture", true);
            var context = session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!;
            JsonDocument Snapshot() => JsonDocument.Parse((string)Call(coordinator, "GetArenaSnapshot", IntegrationSession.Serialize(context))!);
            using (var observation = Snapshot())
            {
                var map = observation.RootElement.GetProperty("obstacleMap");
                Check(map.GetProperty("defaultFrameAlignment").GetProperty("status").GetString() == "aligned"
                    && map.GetProperty("defaultFrameAlignment").GetProperty("obstacleOrigin")[0].GetSingle() == origin.X,
                    "native arena endpoint publishes current alignment provenance and world-space terrain origin");
                Check(map.GetProperty("generation").ValueKind == JsonValueKind.Null && observation.RootElement.GetProperty("geometryOnly").GetBoolean(),
                    "alignment provenance cannot invent a complete obstacle generation or route safety proof");
            }
            hints.PathfindMapCenter += new WDir(1, 0);
            using (var changed = Snapshot()) Check(changed.RootElement.GetProperty("obstacleMap").GetProperty("defaultFrameAlignment").ValueKind == JsonValueKind.Null,
                "later native frame changes invalidate alignment attribution instead of retaining stale provenance");
            hints.Clear();
            Check(Alignment() == null, "new hint builds clear prior terrain alignment provenance");
            using var capabilities = JsonDocument.Parse((string)Call(coordinator, "Capabilities")!);
            Check(capabilities.RootElement.GetProperty("families").GetProperty("native-default-terrain-frame").GetInt32() == 1,
                "native terrain-frame correction is independently discoverable without consumer writes");
        }
        finally { Set(config, "DisableObstacleMaps", disabled); }
    }
}
