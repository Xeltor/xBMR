using BossMod;
using BossMod.Components;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private sealed class DirectCarrier(BossModule module, Actor target) : BossComponent(module) { public Actor Target = target; }

    private static void ShapeSnapshots()
    {
        var vertices = new List<WDir> { new(-5, -5), new(5, -5), new(5, 5), new(-5, 5) };
        var polygon = new RelSimplifiedComplexPolygon(vertices);
        typeof(RelSimplifiedComplexPolygon).GetMethod("VerifyPolygonIndexExistance", BindingFlags.Public | InternalInstance)!.Invoke(polygon, null);
        var core = new SDPolygonWithHolesBase(default, polygon);
        ShapeDistance[] roots = [new SDCircle(new(1, 2), 3), new SDInvertedCircle(default, 3), new SDDonut(default, 1, 4),
            new SDInvertedDonut(default, 1, 4), new SDCone(default, 5, 0.3f.Radians(), 0.6f.Radians()),
            new SDRect(default, 0.5f.Radians(), 5, 1, 2), new SDPolygonWithHoles(core), new SDInvertedPolygonWithHoles(core),
            new SDComplexPolygonInvertedContains(polygon, default), new SDKnockbackInCircleAwayFromOrigin(default, default, 3, 10),
            new SDUnion([new SDCircle(default, 2), new SDRect(new(4, 0), default(Angle), 2, 1, 1)]),
            new SDIntersection([new SDCircle(default, 3), new SDDonut(new(1, 0), 1, 5)]),
            new SDInvertedUnion([new SDCircle(default, 3), new SDInvertedCircle(new(1, 0), 2)])];
        foreach (var shape in roots)
        {
            using var freezer = new IntegrationShapeSnapshot(new(clock: () => 0, frequency: 1000));
            var frozen = freezer.Freeze(shape);
            Check(frozen != null && !ReferenceEquals(frozen, shape), "native geometry graph is independently frozen: " + shape.GetType().Name + " " + freezer.FailureReason);
            for (var x = -6; x <= 6; ++x)
                for (var z = -6; z <= 6; ++z)
                {
                    var point = new WPos(x * 0.9f, z * 0.9f);
                    Check(shape.Distance(point) == frozen!.Distance(point), "frozen geometry preserves exact native scalar math: " + shape.GetType().Name);
                }
        }
        var children = new ShapeDistance[] { new SDCircle(default, 2) }; var original = new SDUnion(children);
        var copy = new IntegrationShapeSnapshot(new(clock: () => 0, frequency: 1000)).Freeze(original)!;
        children[0] = new SDCircle(new(100, 0), 1);
        Check(copy.Distance(default) < 0 && original.Distance(default) > 0, "later native child-array changes cannot mutate a frozen distance graph");
        var polygonSource = new SDPolygonWithHoles(core);
        using var polygonFreezer = new IntegrationShapeSnapshot(new(clock: () => 0, frequency: 1000));
        var polygonCopy = polygonFreezer.Freeze(polygonSource)!;
        var before = polygonCopy.Distance(new(7, 0)); vertices[0] = new(100, 100);
        ((IDisposable)typeof(RelSimplifiedComplexPolygon).GetField("_polyIndex", InternalInstance)!.GetValue(polygon)!).Dispose();
        Check(polygonCopy.Distance(new(7, 0)) == before, "native polygon lists/index data are detached from later original vertex mutation");
        var cycleChildren = new ShapeDistance[1]; var cycle = new SDUnion(cycleChildren); cycleChildren[0] = cycle;
        Check(new IntegrationShapeSnapshot(new(clock: () => 0, frequency: 1000)).Freeze(cycle) == null, "cyclic native distance graphs are rejected before scalar evaluation");
        Check(new IntegrationShapeSnapshot(new(clock: () => 0, frequency: 1000, evaluations: 0)).Freeze(new SDCircle(default, 1)) == null,
            "geometry copy cannot produce success after its work budget ends");
    }

    private static void NativeDistanceSurfaces()
    {
        var (world, actor, module) = ProjectionFixture();
        var assembly = typeof(AIHints).Assembly;
        Type Type(string name) => assembly.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        object Blank(string name) => RuntimeHelpers.GetUninitializedObject(Type(name));
        void Set(object obj, string name, object? value) => obj.GetType().GetField(name, BindingFlags.Public | InternalInstance)!.SetValue(obj, value);
        object? Call(object obj, string name, params object?[] args) => obj.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(obj, args);
        long clock = 1;
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => clock), 1000L);
        var instance = (string)session.GetType().GetField("InstanceGeneration", InternalInstance)!.GetValue(session)!;
        var registration = New("Integration.IntegrationSession+Registration", 1, instance, "distance-native", 2000);
        var owner = (string)Property(Call(session, "Register", registration, (Action<string>)(_ => { }))!, "OwnerToken");
        var hints = new AIHints(); var manager = Blank("BossModuleManager");
        Set(manager, nameof(BossModuleManager.WorldState), world); Set(manager, "_activeModule", module);
        var coordinator = Blank("Integration.IntegrationCoordinator"); Set(coordinator, "Session", session);
        Set(coordinator, "<hints>P", hints); Set(coordinator, "<bossmod>P", manager);
        void Frame()
        {
            Call(session, "BeginFrame", "fixture", true);
            Call(session, "Publish", "hints-ready", (Func<object, string>)(c => IntegrationSession.Serialize(c)));
        }
        string Request() => IntegrationSession.Serialize(new { context = session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session), ownerToken = owner });
        string? metadata = null; Func<Vector2, float>[]? surfaces = null;
        void Receive(string json, Func<Vector2, float>[] distance) { metadata = json; surfaces = distance; }
        JsonDocument Capture(Action<string, Func<Vector2, float>[]>? callback = null, string? request = null)
            => JsonDocument.Parse((string)Call(coordinator, "CaptureConstraintDistances", request ?? Request(), callback ?? Receive)!);
        var children = new ShapeDistance[] { new SDCircle(default, 2) };
        hints.AddForbiddenZone(new SDUnion(children), world.FutureTime(3), 42);
        hints.TemporaryObstacles.Add(new SDCircle(new(4, 0), 1));
        Frame(); using (var warm = Capture()) { }
        Frame(); using (var warm = Capture()) { }
        Frame(); using (var receipt = Capture()) Check(receipt.RootElement.GetProperty("accepted").GetBoolean(), "native distance capture delivers the typed BCL surface callback");
        using (var facts = JsonDocument.Parse(metadata!))
        {
            var root = facts.RootElement;
            Check(root.GetProperty("regionsComplete").GetBoolean() && root.GetProperty("regions").GetArrayLength() == 2 && surfaces!.Length == 2,
                "all frozen native forbidden and temporary regions align exactly to delegates");
            Check(root.GetProperty("regions")[0].GetProperty("activationTicks").GetString() == world.FutureTime(3).Ticks.ToString()
                && root.GetProperty("regions")[0].GetProperty("sourceActorId").GetString() == "2A",
                "frozen scalar functions keep exact same-frame native activation/source metadata");
        }
        var frozen = surfaces!; children[0] = new SDCircle(new(100, 0), 1);
        Check(frozen[0](default) < 0 && hints.ForbiddenZones[0].shapeDistance.Distance(default) > 0,
            "actual endpoint never leaves a delegate to a mutable original composite graph");
        Check(Task.Run(() => frozen[0](default)).GetAwaiter().GetResult() < 0, "frozen native surfaces run on workers without IPC or live geometry reads");
        clock += 251; Check(float.IsNaN(frozen[0](default)), "native source expiry returns NaN rather than safe zero"); clock = 1;
        Frame(); using (var fresh = Capture()) { }
        frozen = surfaces!;
        Call(session, "Clear", New("Integration.IntegrationSession+Lease", 1, instance, owner, 0));
        Check(float.IsNaN(frozen[0](default)), "owner clear invalidates every captured native distance surface");
        module.Components.Add(new DirectCarrier(module, actor)); Frame(); using (var carrier = Capture()) { }
        using (var facts = JsonDocument.Parse(metadata!)) Check(facts.RootElement.GetProperty("carrier").GetBoolean(), "native direct-target carrier facts match the former native read path");
        module.Components.Clear(); Frame();
        Func<Vector2, float>[]? failedFunctions = null;
        using (var failed = Capture((_, f) => { failedFunctions = f; throw new InvalidOperationException("receiver"); }))
            Check(!failed.RootElement.GetProperty("available").GetBoolean() && float.IsNaN(failedFunctions![0](default)),
                "a throwing consumer cannot leave usable partially delivered distance functions");
        // Exercise owned native polygon buffers while a worker overlaps owner revocation.
        hints.ForbiddenZones.Clear(); hints.TemporaryObstacles.Clear();
        var polygon = new RelSimplifiedComplexPolygon(new List<WDir> { new(-5, -5), new(5, -5), new(5, 5), new(-5, 5) });
        typeof(RelSimplifiedComplexPolygon).GetMethod("VerifyPolygonIndexExistance", BindingFlags.Public | InternalInstance)!.Invoke(polygon, null);
        hints.AddForbiddenZone(new SDPolygonWithHoles(new(default, polygon)));
        Frame(); using (var polygonReceipt = Capture()) Check(polygonReceipt.RootElement.GetProperty("accepted").GetBoolean(), "polygon endpoint transfers independently owned native query buffers");
        var polygonFunction = surfaces![0];
        using var started = new ManualResetEventSlim();
        var polygonWorker = Task.Run(() =>
        {
            var first = polygonFunction(new(7, 0)); started.Set();
            for (var i = 0; i < 100000; ++i) _ = polygonFunction(new(i % 9, 0));
            return first;
        });
        started.Wait();
        Call(session, "Clear", New("Integration.IntegrationSession+Lease", 1, instance, owner, 0));
        Check(float.IsFinite(polygonWorker.GetAwaiter().GetResult()) && float.IsNaN(polygonFunction(default)),
            "native polygon buffer owner clear overlaps worker reads safely and fences all future reads");

        Frame(); using (var retained = Capture()) Check(retained.RootElement.GetProperty("accepted").GetBoolean(), "retained-callback lifetime fixture captures independently owned buffers");
        var tracked = (System.Collections.IList)coordinator.GetType().GetField("_distanceSources", InternalInstance)!.GetValue(coordinator)!;
        var lifetime = ((System.Runtime.CompilerServices.ITuple)tracked[tracked.Count - 1]!)[0]!;
        clock += 251;
        Call(coordinator, "RetireDistanceSources", false);
        Check((int)lifetime.GetType().GetField("_disposed", InternalInstance)!.GetValue(lifetime)! == 1
            && ((System.Collections.IList)lifetime.GetType().GetField("Surfaces", InternalInstance)!.GetValue(lifetime)!).Count == 0,
            "native expiry releases owned buffers and geometry even when a consumer retains callbacks without querying again");
        Check(float.IsNaN(surfaces![0](default)), "proactively retired callbacks remain fenced after their backing geometry is released");

        JsonDocument Forecast(Action<string, Func<Vector2, float>[]>? callback = null)
            => JsonDocument.Parse((string)Call(coordinator, "CaptureForecastDistances", Request(), callback ?? Receive)!);
        hints.ForbiddenZones.Clear();
        GenericAOEs.AOEInstance[] published = [new(new AOEShapeCircle(3), default, activation: world.FutureTime(2), actorID: 42),
            new(new AOEShapeCircle(1), new(4, 0), activation: world.FutureTime(4), risky: false,
                shapeDistance: new SDRect(new(4, 0), default(Angle), 3, 1, 1))];
        var aoes = new PublishedAOEs(module, published); module.Components.Add(aoes);
        aoes.AddAIHints(0, actor, default, hints);
        Frame(); using (var warmForecast = Forecast()) { }
        Frame(); using (var forecastReceipt = Forecast()) Check(forecastReceipt.RootElement.GetProperty("accepted").GetBoolean(), "actual forecast endpoint delivers all published BCL surfaces");
        using (var facts = JsonDocument.Parse(metadata!))
        {
            var root = facts.RootElement; var entries = root.GetProperty("publications");
            Check(entries.GetArrayLength() == 2 && surfaces!.Length == 2 && entries[0].GetProperty("nativeHintRegionIndices")[0].GetInt32() == 0
                && entries[0].GetProperty("matchesCurrentNativeConstraint").GetBoolean(), "current forecast attribution requires exact native geometry plus activation equality");
            Check(!entries[1].GetProperty("risky").GetBoolean() && entries[1].GetProperty("color").GetUInt32() == 0
                && entries[1].GetProperty("distanceOverride").GetBoolean() && !entries[1].GetProperty("matchesCurrentNativeConstraint").GetBoolean(),
                "later non-risky publication retains native color, exact distance override and absence from current constraints");
            Check(root.GetProperty("components")[0].GetProperty("standardAIHints").GetBoolean()
                && !root.GetProperty("components")[0].GetProperty("standardRotatingPublication").GetBoolean()
                && !root.GetProperty("futurePulsesComplete").GetBoolean(), "native implementation identity never claims hidden future pulse coverage");
            Check(surfaces![1](new(4, 2)) == published[1].ShapeDistance!.Distance(new(4, 2)), "future distance uses the actual override rather than display circle");
        }
        hints.ForbiddenZones.Clear(); hints.AddForbiddenZone(new SDCircle(new(100, 0), 3), world.FutureTime(2));
        Frame(); using (var mismatched = Forecast()) { }
        using (var facts = JsonDocument.Parse(metadata!)) Check(!facts.RootElement.GetProperty("publications")[0].GetProperty("matchesCurrentNativeConstraint").GetBoolean(),
            "matching activation alone cannot attribute a different native hazard");
        module.Components.Clear(); hints.ForbiddenZones.Clear();
        Service.Config._nodes.TryAdd(typeof(ColorConfig), new ColorConfig());
        var rotating = new GenericRotatingAOE(module);
        rotating.Sequences.Add(new(new AOEShapeCone(8, 0.3f.Radians()), default, default, 0.5f.Radians(), world.FutureTime(2), 1, 4, 3));
        rotating.Update(); module.Components.Add(rotating); rotating.AddAIHints(0, actor, default, hints);
        Frame(); using (var rotatingReceipt = Forecast()) { }
        using (var facts = JsonDocument.Parse(metadata!))
        {
            var root = facts.RootElement;
            Check(root.GetProperty("components")[0].GetProperty("standardRotatingPublication").GetBoolean()
                && root.GetProperty("publications").GetArrayLength() == 3
                && root.GetProperty("publications").EnumerateArray().All(e => e.GetProperty("matchesCurrentNativeConstraint").GetBoolean()),
                "native rotating prefix order and all risky pulse attributions survive the contract");
        }
        var forecastFunctions = surfaces!;
        clock += 251; Check(float.IsNaN(forecastFunctions[0](default)), "forecast geometry cannot outlive its original native source deadline"); clock = 1;
        module.Components.Clear();
        var storm = (GenericAOEs)New("Dawntrail.Foray.FATE.WavedAway.StormWave", module); module.Components.Add(storm);
        var queue = (List<GenericAOEs.AOEInstance>)storm.GetType().GetField("_intercardinal", InternalInstance)!.GetValue(storm)!;
        queue.Add(new(new AOEShapeRect(4, 1), default, 0.5f.Radians(), world.FutureTime(2)));
        queue.Add(new(new AOEShapeRect(4, 1), default, (0.5f + MathF.PI / 2).Radians(), world.FutureTime(3)));
        Frame(); using (var stormReceipt = Forecast()) { }
        using (var facts = JsonDocument.Parse(metadata!)) Check(!facts.RootElement.GetProperty("components")[0].GetProperty("publicationComplete").GetBoolean(),
            "native StormWave parallel-axis queue inspection keeps hidden imminent axes explicit");
        module.Components.Clear(); hints.ForbiddenZones.Clear(); hints.AddForbiddenZone(new SDCircle(default, 1));
        Frame();
        Check(Task.Run(() => { using var wrong = Capture(); return !wrong.RootElement.GetProperty("available").GetBoolean(); }).GetAwaiter().GetResult(),
            "surface capture itself still requires the exact native callback thread");
        actor.IsDead = true; using (var dead = Capture()) Check(!dead.RootElement.GetProperty("available").GetBoolean(), "dead local actor cannot capture new optimization surfaces"); actor.IsDead = false;
        hints.TemporaryObstacles.Clear(); hints.ForbiddenZones.Clear();
        for (var i = 0; i < 129; ++i) hints.AddForbiddenZone(new SDCircle(default, 1));
        Frame(); using (var bounded = Capture()) Check(!bounded.RootElement.GetProperty("available").GetBoolean(), "native region count is bounded before any partial callback delivery");
    }
}
