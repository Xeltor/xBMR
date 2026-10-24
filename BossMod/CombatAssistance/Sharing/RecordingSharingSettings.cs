using System.IO;
using System.Threading;

namespace BossMod.CombatAssistance.Sharing;

// Captured on the framework thread. Workers never read a mutable configuration node.
internal sealed record RecordingSharingSettings(bool Anonymize, string? ReplayFolder)
{
    internal static RecordingSharingSettings Capture(ReplayManagementConfig config)
        => new(config.Anonymize, !string.IsNullOrWhiteSpace(config.ReplayFolder) && Path.IsPathRooted(config.ReplayFolder)
            ? config.ReplayFolder : null);
}

internal sealed record RecordingModuleCatalog(string ParserRevision, ImmutableDictionary<uint, bool> Modules)
{
    internal static RecordingModuleCatalog Capture() => new(typeof(ReplayParserLog).Module.ModuleVersionId.ToString(),
        BossModuleRegistry.RegisteredModules.ToImmutableDictionary(p => p.Key,
            p => p.Value.Maturity == BossModuleInfo.Maturity.WIP));
}

internal delegate Task ReplayEvidenceDecode(string path, Func<Dictionary<string, object?>, CancellationToken, Task> emit,
    CancellationToken cancel, int batchEvents, bool? anonymize, int projectionSchema);
