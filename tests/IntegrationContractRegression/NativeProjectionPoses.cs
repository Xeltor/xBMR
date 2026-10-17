using BossMod;
using BossMod.Components;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private static void NativeProjectionPoses()
    {
        var (world, actor, module) = ProjectionFixture();
        typeof(BossModule).GetField(nameof(BossModule.StateMachine))!.SetValue(module,
            new StateMachine([new(new(), "fixture")]) { ActivePhaseIndex = 0 });
        var knockback = new ActualActorUnsafe(module);
        knockback.Casters.Add(new(default, 5, world.FutureTime(3)));
        module.Components.Add(knockback);
        var native = typeof(AIHints).Assembly;
        Type Type(string name) => native.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        void Set(object obj, string field, object? value) => obj.GetType().GetField(field, BindingFlags.Public | InternalInstance)!.SetValue(obj, value);
        object? Call(object obj, string name, params object?[] args) => obj.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(obj, args);
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => 1), 1000L);
        Call(session, "BeginFrame", "fixture", true);
        object Context() => session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!;
        var manager = (BossModuleManager)RuntimeHelpers.GetUninitializedObject(typeof(BossModuleManager));
        Set(manager, nameof(BossModuleManager.WorldState), world); Set(manager, "_activeModule", module);
        var coordinator = RuntimeHelpers.GetUninitializedObject(Type("Integration.IntegrationCoordinator"));
        Set(coordinator, "Session", session); Set(coordinator, "<bossmod>P", manager);
        using var publication = JsonDocument.Parse((string)Call(coordinator, "GetMechanics", IntegrationSession.Serialize(Context()))!);
        var component = publication.RootElement.GetProperty("components")[0];
        var id = component.GetProperty("componentId").GetString()!;
        Check(component.GetProperty("counterfactualGeometrySupported").GetBoolean(), "native mechanic publication advertises per-component counterfactual geometry support");
        string Request(string? identity = null, object? context = null, int count = 1, float x = 3) => IntegrationSession.Serialize(new
        {
            context = context ?? Context(), componentId = identity ?? id,
            poses = Enumerable.Range(0, count).Select(_ => new { position = new[] { x, 4f }, facing = 0f }).ToArray()
        });
        JsonDocument Query(string request) => JsonDocument.Parse((string)Call(coordinator, "ProjectMechanicPoses", request)!);
        _ = Call(coordinator, "ProjectMechanicPoses", Request()); // Warm native JIT; no mutation or authorization.
        using (var result = Query(Request()))
        {
            var root = result.RootElement;
            Check(root.GetProperty("available").GetBoolean() && root.GetProperty("poses")[0].GetProperty("movements")[0].GetProperty("to")[0].GetSingle() == 6,
                "actual native endpoint projects arbitrary position through native displacement math");
            Check(root.GetProperty("status").GetString() == "unknown" && !root.GetProperty("independentHazardsEvaluated").GetBoolean()
                && !root.GetProperty("recoveryReachabilityEvaluated").GetBoolean(), "native endpoint never relabels geometry as independent hazard or recovery safety");
        }
        var contextJson = JsonSerializer.SerializeToElement(Context(), IntegrationSession.Json);
        var consumer = new IntegrationExample.BossModV1ProjectionGeometry(r => (string)Call(coordinator, "ProjectMechanicPoses", r)!);
        var consumed = consumer.TryProject(contextJson, id, [new(3, 4)], [0f]);
        Check(consumed is [{ Movements: [{ To: { X: 6, Y: 8 } }] }], "compiled BCL consumer round-trips the actual native endpoint without plugin types");
        using (var incompatible = Query(Request().Replace("\"schemaVersion\":1", "\"schemaVersion\":2")))
            Check(!incompatible.RootElement.GetProperty("available").GetBoolean(), "native projection endpoint rejects an incompatible schema");
        using (var invalid = Query(Request().Replace("\"position\":[3,4]", "\"position\":null")))
            Check(!invalid.RootElement.GetProperty("available").GetBoolean(), "native projection endpoint rejects a missing candidate position");
        var calls = 0;
        var absent = new IntegrationExample.BossModV1ProjectionGeometry(_ => { ++calls; return "{\"available\":false}"; });
        Check(absent.TryProject(contextJson, id, [new(3, 4)], [0]) == null, "compiled consumer preserves unavailable geometry as absent evidence");
        Check(absent.TryProject(contextJson, id, [new(3, 4)], []) == null && calls == 1,
            "compiled consumer bounds malformed candidate batches before IPC");
        var malformed = new IntegrationExample.BossModV1ProjectionGeometry(_ => "{broken");
        Check(malformed.TryProject(contextJson, id, [new(3, 4)], [0]) == null, "malformed provider response cannot become an empty successful projection");
        using (var invalid = Query(Request(count: 5))) Check(!invalid.RootElement.GetProperty("available").GetBoolean(), "native endpoint rejects an oversized candidate batch");
        using (var invalid = Query(Request(x: 1000))) Check(!invalid.RootElement.GetProperty("available").GetBoolean(), "native endpoint rejects candidates outside its bounded actor neighborhood");
        using (var missing = Query(Request("missing"))) Check(!missing.RootElement.GetProperty("available").GetBoolean(), "unknown component identity cannot bind to a similar source");
        var previous = Context(); Call(session, "BeginFrame", "fixture", true);
        using (var stale = Query(Request(context: previous))) Check(!stale.RootElement.GetProperty("available").GetBoolean(), "old-frame projection request cannot inherit current sources");
        Check(Task.Run(() => { using var wrongThread = Query(Request()); return !wrongThread.RootElement.GetProperty("available").GetBoolean(); }).GetAwaiter().GetResult(),
            "native counterfactual query requires the exact callback thread");
        actor.IsDead = true;
        using (var dead = Query(Request())) Check(!dead.RootElement.GetProperty("available").GetBoolean(), "missing live actor context cannot produce counterfactual geometry");
        actor.IsDead = false;
        module.Components.Clear(); module.Components.Add(new ActualActorUnsafe(module));
        using (var replaced = Query(Request())) Check(!replaced.RootElement.GetProperty("available").GetBoolean(), "component reset/replacement invalidates its old opaque identity");
        using (var unsupported = JsonDocument.Parse((string)Call(coordinator, "EvaluateMechanicMovement", IntegrationSession.Serialize(new
            { context = Context(), componentId = id, position = new[] { 3f, 4f }, facing = 0f }))!))
            Check(!unsupported.RootElement.GetProperty("available").GetBoolean(), "legacy current-pose endpoint retains its prior counterfactual rejection semantics");
        using (var capabilities = JsonDocument.Parse((string)Call(coordinator, "Capabilities")!))
            Check(capabilities.RootElement.GetProperty("families").GetProperty("counterfactual-projection-geometry").GetInt32() == 1,
                "native provider advertises geometry independently of complete mechanic projection safety");
    }
}
