using System.Numerics;
using BossMod;

public static partial class Checks
{
    private static void ModuleEnablementCache()
    {
        var config = new BossModuleConfig();
        Check(config.IsModuleEnabled(1), "native module is enabled by default");
        config.DisabledModuleOIDs = [1, 2];
        Check(!config.IsModuleEnabled(1) && !config.IsModuleEnabled(2), "replacement configuration is reflected in native enablement rather than an obsolete cache");
        config.DisabledModuleOIDs[0] = 3;
        Check(config.IsModuleEnabled(1) && !config.IsModuleEnabled(3), "in-place array edits also invalidate the cached disabled set");
        config.SetModuleEnabled(3, true);
        Check(config.IsModuleEnabled(3) && !config.IsModuleEnabled(2), "normal native setters preserve other disabled modules after cache refresh");
        config.SetModulesEnabled([2, 4], true);
        Check(config.IsModuleEnabled(2) && config.DisabledModuleOIDs.Length == 0, "bulk native changes remain compatible with configuration snapshots");
    }
}
