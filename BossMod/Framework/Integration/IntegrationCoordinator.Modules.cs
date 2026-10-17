namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    private BossModuleRegistry.Info[]? _moduleCatalog;
    internal sealed record ModuleQuery(IntegrationSession.Context Context, int Offset = 0, int Count = 64);

    internal string GetModules(string json)
    {
        var request = IntegrationSession.Parse<ModuleQuery>(json);
        if (request == null || !Session.Matches(request.Context) || request.Offset < 0 || request.Count is < 1 or > 128)
            return Unavailable("invalid-module-catalog-context-or-page");
        var catalog = _moduleCatalog ??= BossModuleRegistry.RegisteredModules.Values.OrderBy(i => i.PrimaryActorOID).ToArray();
        if (request.Offset > catalog.Length || bossmod.LoadedModules.Count + bossmod.PendingModules.Count > 1024)
            return Unavailable("module-page-or-observation-limit");
        var config = BossModuleManager.Config;
        var loaded = bossmod.LoadedModules.GroupBy(m => m.Info?.PrimaryActorOID ?? m.PrimaryActor.OID).ToDictionary(g => g.Key, g => g.ToArray());
        var pending = bossmod.PendingModules.GroupBy(m => m.Info?.PrimaryActorOID ?? m.PrimaryActor.OID).ToDictionary(g => g.Key, g => g.Count());
        var modules = catalog.Skip(request.Offset).Take(request.Count).Select(info =>
        {
            var enabled = config.IsModuleEnabled(info.PrimaryActorOID);
            var instances = loaded.GetValueOrDefault(info.PrimaryActorOID) ?? [];
            return new
            {
                identity = info.PrimaryActorOID.ToString("X") + ":" + info.ModuleType.FullName,
                primaryActorOid = info.PrimaryActorOID.ToString("X"), module = info.ModuleType.FullName,
                maturity = info.Maturity.ToString(), configuredEnabled = enabled,
                minimumMaturity = config.MinMaturity.ToString(), meetsMinimumMaturity = info.Maturity >= config.MinMaturity,
                eligibleByConfiguration = enabled && info.Maturity >= config.MinMaturity,
                expansion = info.Expansion.ToString(), category = info.Category.ToString(), info.GroupID, info.NameID,
                selected = bossmod.ActiveModule?.GetType() == info.ModuleType,
                loadedActors = instances.Take(16).Select(m => new { actorId = m.PrimaryActor.InstanceID.ToString("X"),
                    activePhase = m.StateMachine.ActivePhaseIndex, positioningActive = m.StateMachine.ActiveState != null }).ToArray(),
                loadedActorsComplete = instances.Length <= 16, pendingInstances = pending.GetValueOrDefault(info.PrimaryActorOID)
            };
        }).ToArray();
        return IntegrationSession.Serialize(new
        {
            available = true, context = request.Context, catalogVersion = 1, total = catalog.Length, request.Offset,
            nextOffset = request.Offset + modules.Length < catalog.Length ? (int?)(request.Offset + modules.Length) : null,
            modules, enablementLeasesSupported = false,
            meaning = "registered module identity and native configuration/load observations; no maturity bypass or active module mutation"
        });
    }
}
