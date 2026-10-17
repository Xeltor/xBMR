using System.Text.Json.Serialization;

namespace BossMod;

public sealed partial class BossModuleConfig
{
    [JsonIgnore]
    internal OwnedModuleEnablement ModuleEnablementOwners = new();

    internal bool IsModuleConfiguredEnabled(uint oid) => !DisabledModuleOIDSet().Contains(oid);

    internal OwnedModuleEnablement.State? ObserveModuleEnablement(uint oid)
        => ModuleEnablementOwners.Observe(oid, IsModuleConfiguredEnabled(oid), true);

    internal void RefreshModuleEnablement()
    {
        var watched = ModuleEnablementOwners.WatchedOids();
        if (watched.Length == 0) return;
        var disabled = DisabledModuleOIDSet();
        foreach (var oid in watched) ModuleEnablementOwners.Observe(oid, !disabled.Contains(oid), false);
    }

    internal void ModuleEnablementUserIntent()
    {
        var disabled = DisabledModuleOIDSet();
        foreach (var oid in ModuleEnablementOwners.WatchedOids()) ModuleEnablementOwners.UserIntent(oid, !disabled.Contains(oid));
    }

    private bool EffectiveModuleEnabled(uint oid, HashSet<uint> disabled)
        => ModuleEnablementOwners.Observe(oid, !disabled.Contains(oid), false)?.Enabled ?? !disabled.Contains(oid);
}
