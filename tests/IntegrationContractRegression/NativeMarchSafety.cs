using BossMod;
using BossMod.Components;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private sealed class FacingMarch(BossModule module) : GenericForcedMarch(module)
    {
        internal float? SeenFacing;
        public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
        { SeenFacing = actor.Rotation.Rad; return pos.X > 4 || base.DestinationUnsafe(slot, actor, pos); }
    }
    private static void NativeMarchSafety()
    {
        var (world, actor, module) = ProjectionFixture();
        var assembly = typeof(AIHints).Assembly;
        Type Type(string name) => assembly.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        object Blank(string name) => RuntimeHelpers.GetUninitializedObject(Type(name));
        void Set(object o, string name, object? value) => o.GetType().GetField(name, BindingFlags.Public | InternalInstance)!.SetValue(o, value);
        object? Call(object o, string name, params object?[] args) => o.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(o, args);
        var smartConfig = Type("SmartRotationConfig");
        Service.Config._nodes.TryAdd(smartConfig, (ConfigNode)Activator.CreateInstance(smartConfig, true)!);
        long clock = 1;
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => clock), 1000L);
        var instance = (string)session.GetType().GetField("InstanceGeneration", InternalInstance)!.GetValue(session)!;
        var registered = Call(session, "Register", New("Integration.IntegrationSession+Registration", 1, instance, "march-native", 2000), (Action<string>)(_ => { }))!;
        var owner = (string)Property(registered, "OwnerToken");
        var hints = new AIHints { PathfindMapBounds = new ArenaBoundsSquare(8) };
        var manager = Blank("BossModuleManager"); Set(manager, nameof(BossModuleManager.WorldState), world); Set(manager, "_activeModule", module);
        var movement = (MovementOverride)Blank("MovementOverride");
        var coordinator = Blank("Integration.IntegrationCoordinator"); Set(coordinator, "Session", session);
        Set(coordinator, "<bossmod>P", manager); Set(coordinator, "<hints>P", hints); Set(coordinator, "<movement>P", movement);
        actor.PosRot = default;
        var march = new FacingMarch(module); march.AddForcedMovement(actor, default, 1, world.FutureTime(1)); module.Components.Add(march);
        var componentId = (string)Type("Integration.IntegrationMechanics").GetMethod("ComponentId", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [march])!;
        void Frame()
        {
            hints.IntegrationHeadingsForTest(null);
            Call(session, "BeginFrame", "march", true);
            Call(session, "Publish", "hints-ready", (Func<object, string>)(c => IntegrationSession.Serialize(c)));
        }
        object Context() => session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!;
        JsonDocument Evaluate(float[]? headings = null)
            => JsonDocument.Parse((string)Call(coordinator, "EvaluateForcedMarchHeadings", IntegrationSession.Serialize(new
            { context = Context(), ownerToken = owner, componentId, headings = headings ?? [0f] }))!);
        JsonDocument Constrain(float heading = 0)
            => JsonDocument.Parse((string)Call(coordinator, "ConstrainForcedMarchHeading", IntegrationSession.Serialize(new
            { context = Context(), ownerToken = owner, componentId, heading }))!);
        for (var i = 0; i < 3; ++i) { Frame(); using var warm = Evaluate(); }
        Frame(); using (var result = Evaluate())
        {
            var root = result.RootElement; var observation = root.GetProperty("results")[0];
            Check(root.GetProperty("complete").GetBoolean() && observation.GetProperty("status").GetString() == "clear",
                "native march query proves current timed waits, full native leg and landing cushion for a clear center");
            Check(!root.GetProperty("active").GetBoolean() && root.GetProperty("pending").GetArrayLength() == 1
                && observation.GetProperty("route").GetArrayLength() == 2 && observation.GetProperty("timedLegs").GetArrayLength() == 3,
                "native march query exposes pending/active state and complete timed route without consumer projection");
        }
        Frame(); using (var side = Evaluate([MathF.PI / 2])) Check(side.RootElement.GetProperty("results")[0].GetProperty("status").GetString() == "blocked"
            && march.SeenFacing == MathF.PI / 2 && actor.Rotation.Rad == 0,
            "native DestinationUnsafe receives hypothetical facing while actual actor pose stays unchanged");
        hints.AddForbiddenZone(new SDCircle(default, 1), world.FutureTime(0.5)); Frame();
        using (var wait = Evaluate()) Check(wait.RootElement.GetProperty("results")[0].GetProperty("status").GetString() == "blocked", "march waiting interval cannot ignore an activating native hazard");
        hints.ForbiddenZones.Clear(); hints.AddForbiddenZone(new SDCircle(new(0, 6), 1), world.FutureTime(3.1)); Frame();
        using (var landing = Evaluate()) Check(landing.RootElement.GetProperty("results")[0].GetProperty("status").GetString() == "blocked", "native activation cushion and final quarter-second landing wait remain binding");
        hints.ForbiddenZones.Clear(); hints.TemporaryObstacles.Add(new SDCircle(new(0, 3), 1)); Frame();
        using (var obstacle = Evaluate()) Check(obstacle.RootElement.GetProperty("results")[0].GetProperty("status").GetString() == "blocked", "temporary physical-shaped obstacles constrain the entire march leg");
        hints.TemporaryObstacles.Clear();
        Frame(); using (var warm = Constrain()) { }
        Frame(); using (var receipt = Constrain()) Check(receipt.RootElement.GetProperty("accepted").GetBoolean() && receipt.RootElement.GetProperty("samples").GetInt32() == 17,
            "native heading endpoint revalidates the full fixed sampled eight-degree window");
        var contributed = typeof(AIHints).GetField("IntegrationHeadings", InternalInstance)!.GetValue(hints)!;
        int Count() => ((Array)Call(contributed, "Current")!).Length;
        Check(Count() == 1 && hints.ForbiddenDirections.Count == 0, "heading contribution uses a separate owned collection without editing native authored ranges");
        var smart = new SmartRotationTweak(world, hints);
        var direction = smart.GetSafeRotation(MathF.PI.Radians(), MathF.PI.Radians(), 45f.Degrees());
        Check(direction != null && Math.Abs(direction.Value.Normalized().Rad) < 0.001f, "actual native facing selection consumes the contributed safe window");
        hints.ForbiddenDirections.Add((default, 1f.Degrees(), DateTime.MinValue));
        var authored = smart.GetSafeRotation(MathF.PI.Radians(), MathF.PI.Radians(), 45f.Degrees());
        Check(authored == null && Count() == 0 && hints.ForbiddenDirections.Count == 1,
            "later native authored facing mechanics retire the contributed preference and remain untouched");
        hints.ForbiddenDirections.Clear(); Check(Count() == 0, "retired heading preferences cannot revive after pressure disappears");
        Frame(); using (var receipt = Constrain()) { }
        contributed = typeof(AIHints).GetField("IntegrationHeadings", InternalInstance)!.GetValue(hints)!;
        movement.UserMove = new(1, 0); Check(Count() == 0, "manual movement immediately removes contributed facing pressure"); movement.UserMove = default;
        Call(session, "Clear", New("Integration.IntegrationSession+Lease", 1, instance, owner, 0)); Check(Count() == 0, "owner clear retires a heading window before the next frame");
        hints.ForbiddenDirections.Clear(); Frame(); using (var receipt = Constrain()) { }
        contributed = typeof(AIHints).GetField("IntegrationHeadings", InternalInstance)!.GetValue(hints)!;
        clock += 251; Check(Count() == 0, "source expiry cannot leave a native facing constraint behind"); clock = 1;
        Frame(); using (var receipt = Constrain()) { }
        contributed = typeof(AIHints).GetField("IntegrationHeadings", InternalInstance)!.GetValue(hints)!;
        Call(session, "BeginFrame", "march", true); Check(Count() == 0, "new frame invalidates old contributions even before hints clear");
    }
}

static class NativeMarchFixtureAccess
{
    internal static void IntegrationHeadingsForTest(this AIHints hints, object? value)
        => typeof(AIHints).GetField("IntegrationHeadings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(hints, value);
}
