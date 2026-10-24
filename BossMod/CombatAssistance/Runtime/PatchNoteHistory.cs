using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace BossMod.CombatAssistance.Runtime;

internal sealed record PatchNoteEntry(string Id, string Version, string[] Changes, bool Backfilled = false);

internal static class PatchNoteHistory
{
    public static PatchNoteEntry[] Load()
    {
        var assembly = typeof(PatchNoteHistory).Assembly;
        using var stream = assembly.GetManifestResourceStream("BossMod.CombatAssistance.PatchNotes.json")
            ?? throw new InvalidOperationException("Patch notes are missing from this build.");
        var entries = JsonSerializer.Deserialize<PatchNoteEntry[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Patch notes are empty.");
        return entries;
    }

    public static PatchNoteEntry[] Pending(IReadOnlyList<PatchNoteEntry> entries, string[]? acknowledged, string? baselineVersion = null)
    {
        // No history exists before this feature: introduce it without a backlog.
        if (acknowledged == null)
            return entries.TakeLast(1).ToArray();
        var seen = new HashSet<string>(acknowledged, StringComparer.Ordinal);
        var hasBaseline = Version.TryParse(baselineVersion, out var baseline);
        return entries.Where(entry => !seen.Contains(entry.Id) &&
            (!entry.Backfilled || (hasBaseline && Version.TryParse(entry.Version, out var version) && version > baseline))).ToArray();
    }

    public static string[] Acknowledge(IReadOnlyList<PatchNoteEntry> entries, string[]? acknowledged, IReadOnlyList<PatchNoteEntry> shown)
    {
        return (acknowledged ?? entries.Select(entry => entry.Id).ToArray())
            .Concat(shown.Select(entry => entry.Id)).Distinct(StringComparer.Ordinal).ToArray();
    }
}
