using BossMod.ReplayAnalysis.Evidence;
using System;
using System.Collections.Generic;

namespace BossMod.CombatAssistance.Sharing;

/// <summary>Conservative whole-recording gate using the loaded BMR module registry.
/// Actor presence does not prove module activation: any non-WIP primary actor vetoes
/// the file, even if a WIP primary actor is also present. Recordings without a known
/// module remain eligible for research. No live modules are created.</summary>
internal sealed class RecordingModuleEligibility(IReadOnlyDictionary<uint, bool> modules)
{
    public const int PolicyVersion = 2;
    private bool foundOther;

    public void Observe(Dictionary<string, object?> batch)
    {
        foreach (Dictionary<string, object?> row in (Dictionary<string, object?>[])batch["events"]!)
        {
            if (row["kind"] is not "create"
                || row["params"] is not List<long> parameters || parameters.Count == 0
                || parameters[0] is not (0x205 or 0x700)) // BMR Enemy or EventObj
                continue;
            if (row["oid"] is long oid && oid is >= 0 and <= uint.MaxValue
                && modules.TryGetValue((uint)oid, out bool incomplete))
            {
                this.foundOther |= !incomplete;
            }
        }
    }

    public void RequireEligible()
    {
        if (this.foundOther)
            throw new RecordingModuleSkippedException();
    }
}

internal sealed class RecordingModuleSkippedException : Exception
{
}
