using BossMod;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private static void NativeStaticRegistry()
    {
        var assembly = typeof(AIHints).Assembly;
        Type Type(string name) => assembly.GetType("BossMod.Integration." + name, true)!;
        object? Call(object obj, string name, params object?[] args) => obj.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(obj, args);
        using var session = (IDisposable)Activator.CreateInstance(Type("IntegrationSession"), BindingFlags.Public | InternalInstance,
            null, new object?[] { (Func<long>)(() => 1L), 1000L }, null)!;
        var coordinator = RuntimeHelpers.GetUninitializedObject(Type("IntegrationCoordinator"));
        coordinator.GetType().GetField("Session", InternalInstance)!.SetValue(coordinator, session);
        var instance = (string)session.GetType().GetField("InstanceGeneration", InternalInstance)!.GetValue(session)!;
        string Request(int offset = 0, int count = 128, int schemaVersion = 1, string? generation = null)
            => IntegrationSession.Serialize(new { schemaVersion, instanceGeneration = generation ?? instance, offset, count });
        JsonDocument Query(string? request = null) => JsonDocument.Parse((string)Call(coordinator, "GetStaticRegistry", request ?? Request())!);
        var rows = new Dictionary<uint, (string Maturity, bool Wip)>();
        var offset = 0; string? revision = null; var pages = 0;
        do
        {
            using var page = Query(Request(offset)); var root = page.RootElement;
            Check(root.GetProperty("available").GetBoolean() && root.GetProperty("pageComplete").GetBoolean()
                && root.GetProperty("snapshotComplete").GetBoolean(), "static registry page is native and complete without a live frame/player");
            var parser = root.GetProperty("parserRevision").GetString();
            Check(parser == typeof(ReplayParserLog).Module.ModuleVersionId.ToString("D") && (revision == null || revision == parser),
                "opaque parser/registry revision remains the actual provider identity across all pages");
            revision = parser;
            foreach (var entry in root.GetProperty("entries").EnumerateArray())
                rows.Add(entry.GetProperty("oid").GetUInt32(), (entry.GetProperty("maturity").GetString()!, entry.GetProperty("isWip").GetBoolean()));
            var next = root.GetProperty("nextOffset");
            offset = next.ValueKind == JsonValueKind.Null ? -1 : next.GetInt32(); ++pages;
            Check(pages <= 128, "static registry pagination is bounded");
        } while (offset >= 0);
        Check(rows.Count == BossModuleRegistry.RegisteredModules.Count, "whole registry retains every native primary actor, including non-WIP and dummy modules");
        Check(BossModuleRegistry.RegisteredModules.All(p => rows[p.Key].Maturity == Enum.GetName(p.Value.Maturity)
            && rows[p.Key].Wip == (p.Value.Maturity == BossModuleInfo.Maturity.WIP)), "native named maturity and exact WIP equality preserve upload eligibility semantics");
        Check(Task.Run(() => { using var page = Query(); return page.RootElement.GetProperty("available").GetBoolean(); }).GetAwaiter().GetResult(),
            "immutable registry is readable on a background thread without a native frame callback");
        foreach (var invalid in new[] { Request(count: 0), Request(count: 129), Request(offset: -1), Request(offset: rows.Count + 1),
            Request(schemaVersion: 2), Request(generation: "old") })
        {
            using var rejected = Query(invalid);
            Check(!rejected.RootElement.GetProperty("available").GetBoolean(), "registry rejects invalid schema/instance/page bounds");
        }
        session.Dispose(); using var disposed = Query();
        Check(!disposed.RootElement.GetProperty("available").GetBoolean(), "unloading the provider invalidates pending background registry access");
    }
}
