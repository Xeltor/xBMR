using BossMod.ReplayAnalysis.Evidence;
using System;
using System.Collections.Generic;
using System.IO;

namespace BossMod.CombatAssistance.Sharing;

internal sealed record FightUploadFailure(string Code, bool Blocked, TimeSpan RetryDelay);

internal static class FightUploadFailurePolicy
{
    private static readonly HashSet<string> SourceFailures = new(StringComparer.Ordinal)
    {
        "source_size_limit", "binary_v31_required", "unsupported_replay_version", "invalid_frequency",
        "invalid_game_version", "unsupported_writer_contract", "inconsistent_writer_contract",
        "invalid_beast_count", "unsupported_tag", "truncated_entry", "truncated_tag",
        "truncated_brotli", "invalid_brotli", "trailing_brotli_data", "nonmonotonic_timestamp",
        "allocation_limit", "target_limit", "actor_limit", "decoded_byte_limit", "decode_budget",
        "identity_name_limit", "identity_slot_limit", "identity_batch_limit", "no_supported_events",
        "ambiguous_beast_count_layout", "unsupported_beast_count_layout", "inconsistent_beast_count_layout",
        "batch_limit", "compressed_batch_limit"
    };

    public static bool IsSourceFailureCode(string code) => SourceFailures.Contains(code);

    public static FightUploadFailure Classify(Exception error, int failures)
    {
        for (int depth = 0; depth < 16 && error.InnerException != null; ++depth)
        {
            error = error.InnerException;
        }
        if (error is InvalidDataException && IsSourceFailureCode(error.Message))
        {
            return new(error.Message, true, TimeSpan.Zero);
        }
        if (error is EndOfStreamException)
        {
            return new("truncated_entry", true, TimeSpan.Zero);
        }
        if (error is IOException or UnauthorizedAccessException)
        {
            return new("recording_unavailable", false, TimeSpan.FromMinutes(5));
        }
        // Unknown exception text is never retained. A transient upload failure
        // gets another bounded attempt instead of a six-hour per-file backoff.
        return new("upload_or_reader_failed", false,
            TimeSpan.FromSeconds(Math.Min(300, 15 * Math.Pow(2, Math.Clamp(failures - 1, 0, 5)))));
    }

    public static string WaitingStatus(int pending, int blocked, DateTime nextRetry, DateTime now)
    {
        if (pending <= blocked && blocked > 0)
        {
            return blocked == 1 ? "1 recording could not be read." : $"{blocked} recordings could not be read.";
        }
        string retry = nextRetry > now && nextRetry != DateTime.MaxValue
            ? $"Retrying in {Math.Max(1, (int)Math.Ceiling((nextRetry - now).TotalMinutes))}m"
            : "Retrying soon";
        return blocked > 0 ? $"{retry} · {blocked} unreadable" : retry + ".";
    }
}
