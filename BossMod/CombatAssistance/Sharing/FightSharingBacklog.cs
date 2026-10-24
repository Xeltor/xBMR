using BossMod.ReplayAnalysis.Evidence;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BossMod.CombatAssistance.Sharing;

internal sealed record FightSharingBacklog(int Shared, int Total, bool ScanComplete, int SkippedUnsynced = 0, int SkippedModules = 0)
{
    // ReplayManagementWindow.GetPrefix writes U, LS, MI, NE in that order;
    // ReplayRecorder appends the timestamp. Only U without LS means unsynced.
    // Anchor to that suffix so duty/character text elsewhere is not a flag.
    private static readonly Regex UnsyncedSuffix = new(
        @"_U(?:_MI)?(?:_NE)?_[0-9]{4}(?:_[0-9]{2}){5}\.log\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public string Summary => (this.ScanComplete
        ? $"Recordings: {this.Shared}/{this.Total} shared · {this.Total - this.Shared} remaining"
        : $"Recordings: {this.Shared}+ shared · {this.Total - this.Shared}+ remaining")
        + (this.SkippedUnsynced > 0 ? $" · {this.SkippedUnsynced}{(this.ScanComplete ? "" : "+")} unsynced skipped" : "")
        + (this.SkippedModules > 0 ? $" · {this.SkippedModules}{(this.ScanComplete ? "" : "+")} module status skipped" : "");

    public static bool IsUnsynced(string name) => UnsyncedSuffix.IsMatch(name);

    public static string Fingerprint(FileInfo file) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"public-v1:{file.Name}:{file.Length}:{file.LastWriteTimeUtc.Ticks}")));

    public static bool IsEligible(FileInfo file, DateTime now) =>
        (file.Attributes & FileAttributes.ReparsePoint) == 0
        && file.Length is >= 36 and <= 128L * 1024 * 1024
        && file.LastWriteTimeUtc < now.AddMinutes(-1)
        && !IsUnsynced(file.Name);

    // Metadata only, on the worker. No recording is opened or decoded for counts.
    public static FightSharingBacklog Scan(string root, Func<string, bool> completed,
        DateTime now, CancellationToken token, int fileLimit = 32768, Func<string, bool>? skippedModules = null)
    {
        int shared = 0, total = 0, examined = 0, skippedUnsynced = 0, skipped = 0;
        foreach (FileInfo file in new DirectoryInfo(root).EnumerateFiles("*.log"))
        {
            token.ThrowIfCancellationRequested();
            if (++examined > fileLimit)
            {
                return new(shared, total, false, skippedUnsynced, skipped);
            }
            if (IsUnsynced(file.Name))
            {
                ++skippedUnsynced;
                continue;
            }
            if (IsEligible(file, now))
            {
                string fingerprint = Fingerprint(file);
                if (skippedModules?.Invoke(fingerprint) == true)
                {
                    ++skipped;
                    continue;
                }
                ++total;
                if (completed(fingerprint))
                {
                    ++shared;
                }
            }
        }
        return new(shared, total, true, skippedUnsynced, skipped);
    }
}
