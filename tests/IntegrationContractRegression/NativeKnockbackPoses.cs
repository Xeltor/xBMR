using BossMod;
using BossMod.Components;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private static void NativeKnockbackPoses()
    {
        var (world, actor, module) = ProjectionFixture(); actor.HitboxRadius = 0.5f;
        var assembly = typeof(AIHints).Assembly;
        Type Type(string name) => assembly.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        object Blank(string name) => RuntimeHelpers.GetUninitializedObject(Type(name));
        void Set(object o, string name, object? value) => o.GetType().GetField(name, BindingFlags.Public | InternalInstance)!.SetValue(o, value);
        object? Call(object o, string name, params object?[] args) => o.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(o, args);
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => 1L), 1000L);
        var instance = (string)session.GetType().GetField("InstanceGeneration", InternalInstance)!.GetValue(session)!;
        var owner = (string)Property(Call(session, "Register", New("Integration.IntegrationSession+Registration", 1, instance, "native-knockback", 2000), (Action<string>)(_ => { }))!, "OwnerToken");
        var manager = Blank("BossModuleManager"); Set(manager, nameof(BossModuleManager.WorldState), world); Set(manager, "_activeModule", module);
        var coordinator = Blank("Integration.IntegrationCoordinator"); Set(coordinator, "Session", session); Set(coordinator, "<bossmod>P", manager);
        void Frame() { Call(session, "BeginFrame", "native-knockback", true); Call(session, "Publish", "hints-ready", (Func<object, string>)(c => IntegrationSession.Serialize(c))); }
        object Context() => session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!;
        string Id(BossComponent component) => (string)Type("Integration.IntegrationMechanics").GetMethod("ComponentId", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [component])!;
        string Request(BossComponent component, float x, float z, float facing = 0, int count = 1) => IntegrationSession.Serialize(new
        { context = Context(), ownerToken = owner, componentId = Id(component), poses = Enumerable.Range(0, count).Select(_ => new { position = new[] { x, z }, facing }).ToArray() });
        JsonDocument Query(string request) => JsonDocument.Parse((string)Call(coordinator, "ProjectNativeKnockbackPoses", request)!);
        var custom = new PoseDependentSources(module); custom.Casters.Add(new(default, 5, world.FutureTime(3))); module.Components.Add(custom);
        for (var i = 0; i < 3; ++i) { Frame(); using var warm = Query(Request(custom, 3, 4)); }
        Frame(); using (var result = Query(Request(custom, 3, 4)))
        {
            var root = result.RootElement; var observation = root.GetProperty("observations")[0];
            Check(root.GetProperty("complete").GetBoolean() && observation.GetProperty("movements")[0].GetProperty("to")[0].GetSingle() == 6,
                "native actor-pose contract evaluates custom pose-dependent sources using exact existing displacement math");
            Check(root.GetProperty("status").GetString() == "unknown" && !root.GetProperty("independentHazardsEvaluated").GetBoolean()
                && !root.GetProperty("recoveryReachabilityEvaluated").GetBoolean(), "custom geometry never becomes complete landing/recovery or immunity permission");
            Check(observation.GetProperty("sources")[0].GetProperty("activationTicks").GetString() == world.FutureTime(3).Ticks.ToString()
                && observation.GetProperty("movements")[0].GetProperty("sourceIndex").GetInt32() == 0,
                "native source timing and movement attribution share one exact captured source publication");
        }
        using (var catalog = JsonDocument.Parse(IntegrationSession.Serialize(IntegrationMechanics.Capture(module, actor,
            new(1, instance, 1, "hints-ready"), new(clock: () => 0, frequency: 1000)))))
            Check(catalog.RootElement.GetProperty("components")[0].GetProperty("nativeKind").GetString() == "knockback",
                "custom knockback component identity remains discoverable even when actual-position publications are empty");
        Frame(); using (var absent = Query(Request(custom, -3, 4)))
            Check(absent.RootElement.GetProperty("observations")[0].GetProperty("movements").GetArrayLength() == 0
                && absent.RootElement.GetProperty("observations")[0].GetProperty("status").GetString() == "no-published-displacement",
                "custom source applicability is evaluated at hypothetical start without inferring immunity from an empty result");
        module.Components.Clear();
        var chain = new PublishedKnockback(module) { Sources = [new(new(-1, 0), 4, world.FutureTime(2)),
            new(default, 2, world.FutureTime(3), direction: 90f.Degrees(), kind: GenericKnockback.Kind.DirForward)] };
        module.Components.Add(chain); var before = actor.PosRot;
        Frame(); using (var sequence = Query(Request(chain, 0, 0)))
        {
            var movements = sequence.RootElement.GetProperty("observations")[0].GetProperty("movements");
            Check(movements.GetArrayLength() == 2 && movements[0].GetProperty("to")[0].GetSingle() == 4
                && movements[1].GetProperty("from")[0].GetSingle() == 4 && movements[1].GetProperty("to")[0].GetSingle() == 6,
                "mixed custom native sequences retain ordered from/to chain and exact source indices");
        }
        Check(actor.PosRot == before, "native custom/sequence queries never edit actual actor pose");
        module.Components.Clear(); var unsafeComponent = new ActualActorUnsafe(module); unsafeComponent.Casters.Add(new(default, 5, world.FutureTime(2))); module.Components.Add(unsafeComponent);
        Frame(); using (var unsafeResult = Query(Request(unsafeComponent, 3, 4)))
            Check(unsafeResult.RootElement.GetProperty("observations")[0].GetProperty("movements")[0].GetProperty("nativeDestinationUnsafe").GetBoolean()
                && unsafeResult.RootElement.GetProperty("observations")[0].GetProperty("nativeStationaryDestinationUnsafe").GetBoolean()
                && unsafeComponent.SeenActor == new WPos(3, 4) && actor.PosRot == before,
                "custom native DestinationUnsafe receives the queried actor start pose while actual actor remains unchanged");
        unsafeComponent.ImmuneUntil(world.FutureTime(4)); Frame();
        using (var immune = Query(Request(unsafeComponent, 3, 4)))
            Check(immune.RootElement.GetProperty("observations")[0].GetProperty("sources")[0].GetProperty("immuneAtActivation").GetBoolean()
                && immune.RootElement.GetProperty("observations")[0].GetProperty("movements").GetArrayLength() == 0,
                "native immunity facts and native skipped displacement stay separate from action permission");
        Frame(); using (var bounded = Query(Request(unsafeComponent, 0, 0, count: 33))) Check(!bounded.RootElement.GetProperty("available").GetBoolean(), "native pose batches are bounded before geometry work");
        Frame(); Check(Task.Run(() => { using var wrong = Query(Request(unsafeComponent, 0, 0)); return !wrong.RootElement.GetProperty("available").GetBoolean(); }).GetAwaiter().GetResult(), "native custom projection stays on the exact native callback thread");
    }
}
