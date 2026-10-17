namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record RegistryQuery(int SchemaVersion, string InstanceGeneration, int Offset = 0, int Count = 128);
    private sealed record RegistryEntry(uint Oid, string Identity, string Maturity, bool IsWip);
    private sealed record RegistrySnapshot(string Revision, RegistryEntry[] Entries);
    private static readonly Lazy<RegistrySnapshot> StaticRegistry = new(() =>
    {
        var registry = BossModuleRegistry.RegisteredModules;
        if (registry.Count > 16384) throw new InvalidOperationException("native-registry-size-limit");
        var entries = registry.OrderBy(p => p.Key).Select(p =>
        {
            var name = Enum.GetName(p.Value.Maturity) ?? throw new InvalidOperationException("unnamed-native-maturity");
            return new RegistryEntry(p.Key, $"{p.Key:X}:{p.Value.ModuleType.FullName}", name,
                p.Value.Maturity == BossModuleInfo.Maturity.WIP);
        }).ToArray();
        return new(typeof(ReplayParserLog).Module.ModuleVersionId.ToString("D"), entries);
    });

    // Static metadata only: no live actor, settings, module construction or callback context.
    internal string GetStaticRegistry(string json)
    {
        var request = IntegrationSession.Parse<RegistryQuery>(json);
        if (request == null || !Session.ProviderAlive || request.SchemaVersion != IntegrationSession.SchemaVersion
            || request.InstanceGeneration != Session.InstanceGeneration || request.Offset < 0 || request.Count is < 1 or > 128)
            return Unavailable("invalid-native-registry-query");
        try
        {
            var registry = StaticRegistry.Value;
            if (request.Offset > registry.Entries.Length) return Unavailable("native-registry-offset-range");
            var entries = registry.Entries.Skip(request.Offset).Take(request.Count).ToArray();
            var end = request.Offset + entries.Length;
            return IntegrationSession.Serialize(new
            {
                available = true, schemaVersion = IntegrationSession.SchemaVersion, instanceGeneration = Session.InstanceGeneration,
                parserRevision = registry.Revision, registryRevision = registry.Revision,
                request.Offset, total = registry.Entries.Length, entries, snapshotComplete = true, pageComplete = true,
                nextOffset = end < registry.Entries.Length ? (int?)end : null,
                meaning = "immutable loaded native registry and parser revision; named WIP equality, not configured enablement or active module evidence"
            });
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Native registry read failed: {error.GetType().Name}: {error.Message}");
            return Unavailable("native-registry-read-failed");
        }
    }
}
