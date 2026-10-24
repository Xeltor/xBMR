using BossMod.ReplayAnalysis.Evidence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace BossMod.CombatAssistance.Sharing;

// Worker-owned metadata index. Receipt changes reuse it; periodic reconciliation
// discovers removals, renames and new recordings without repeated disk scans.
internal sealed class FightSharingBacklogIndex(int fileLimit = 32768)
{
    private sealed record Entry(string? Fingerprint, DateTime Modified, bool Unsynced);
    private Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private string? observedRoot;
    private DateTime nextRefresh;
    private bool complete;

    public void Invalidate()
    {
        this.observedRoot = null;
        this.entries.Clear();
    }

    public void Observe(FileInfo file)
    {
        if (this.observedRoot != file.DirectoryName)
            return;
        if (this.entries.ContainsKey(file.FullName) || this.entries.Count < fileLimit)
            this.entries[file.FullName] = Capture(file);
        else
            this.complete = false;
    }

    public FightSharingBacklog Read(string root, Func<string, bool> completed, DateTime now,
        CancellationToken token, Func<string, bool>? skippedModules = null)
    {
        token.ThrowIfCancellationRequested();
        if (this.observedRoot != root || now >= this.nextRefresh)
        {
            var refreshed = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var scanComplete = true;
            foreach (var file in new DirectoryInfo(root).EnumerateFiles("*.log"))
            {
                token.ThrowIfCancellationRequested();
                if (refreshed.Count >= fileLimit)
                {
                    scanComplete = false;
                    break;
                }
                refreshed.Add(file.FullName, Capture(file));
            }
            this.entries = refreshed;
            this.complete = scanComplete;
            this.observedRoot = root;
            this.nextRefresh = now.AddSeconds(30);
        }

        var shared = 0;
        var total = 0;
        var unsynced = 0;
        var skipped = 0;
        var cutoff = now.AddMinutes(-1);
        foreach (var entry in this.entries.Values)
        {
            token.ThrowIfCancellationRequested();
            if (entry.Unsynced)
            {
                unsynced++;
                continue;
            }
            if (entry.Fingerprint is not { } fingerprint || entry.Modified >= cutoff)
                continue;
            if (skippedModules?.Invoke(fingerprint) == true)
            {
                skipped++;
                continue;
            }
            total++;
            if (completed(fingerprint))
                shared++;
        }
        return new(shared, total, this.complete, unsynced, skipped);
    }

    private static Entry Capture(FileInfo file)
    {
        if (FightSharingBacklog.IsUnsynced(file.Name))
            return new(null, default, true);
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.Length is < 36 or > 128L * 1024 * 1024)
            return new(null, default, false);
        return new(FightSharingBacklog.Fingerprint(file), file.LastWriteTimeUtc, false);
    }
}
