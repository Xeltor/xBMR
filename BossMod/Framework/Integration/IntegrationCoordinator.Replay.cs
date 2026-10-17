using System.Threading;
using System.Threading.Tasks;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record EvidenceRequest(int SchemaVersion, string InstanceGeneration, string Path,
        int BatchEvents = 16384, int ProjectionSchema = 1, bool? Anonymize = null);
    private sealed class EvidenceReceiverFailure : Exception;
    private static readonly HashSet<string> EvidenceSourceFailures = new(StringComparer.Ordinal)
    {
        "source_size_limit", "binary_v31_required", "unsupported_replay_version", "invalid_frequency", "invalid_game_version",
        "invalid_beast_count", "unsupported_tag", "truncated_entry", "truncated_tag", "truncated_brotli", "invalid_brotli",
        "trailing_brotli_data", "nonmonotonic_timestamp", "allocation_limit", "target_limit", "actor_limit", "decoded_byte_limit",
        "decode_budget", "identity_name_limit", "identity_slot_limit", "identity_batch_limit", "no_supported_events",
        "ambiguous_beast_count_layout", "unsupported_beast_count_layout", "inconsistent_beast_count_layout"
    };
    private static string? EvidenceFailureCode(Exception error) => error switch
    {
        System.IO.EndOfStreamException => "truncated_entry",
        System.IO.InvalidDataException { Message: "invalid_native_recording_tag" } => "unsupported_tag",
        System.IO.InvalidDataException data when EvidenceSourceFailures.Contains(data.Message) => data.Message,
        _ => null
    };
    private readonly CancellationTokenSource _decodeShutdown = new();
    private int _activeDecodes;

    internal Task<string> DecodeEvidence(string json, Func<string, Task> receive, CancellationToken cancellation)
    {
        var request = IntegrationSession.Parse<EvidenceRequest>(json);
        if (request == null || receive == null || request.SchemaVersion != 1 || request.InstanceGeneration != Session.InstanceGeneration
            || !Session.ProviderAlive || string.IsNullOrWhiteSpace(request.Path) || request.Path.Length > 4096
            || request.BatchEvents is not (1024 or 2048 or 16384) || request.ProjectionSchema is not (1 or 3))
            return Task.FromResult(IntegrationSession.Serialize(new { accepted = false, completed = false, reason = "invalid-native-decode-request", instanceGeneration = Session.InstanceGeneration }));
        if (Interlocked.Increment(ref _activeDecodes) > 2)
        {
            Interlocked.Decrement(ref _activeDecodes);
            return Task.FromResult(IntegrationSession.Serialize(new { accepted = false, completed = false, reason = "native-decode-work-limit", instanceGeneration = Session.InstanceGeneration }));
        }
        // No file IO, hashing, decompression, parsing or consumer work executes on the caller's frame.
        return Task.Run(async () =>
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _decodeShutdown.Token);
            cancel.CancelAfter(TimeSpan.FromMinutes(10));
            var token = cancel.Token;
            var batches = 0;
            var revision = typeof(ReplayParserLog).Module.ModuleVersionId.ToString();
            try
            {
                var decoder = new ReplayEvidence.ReplayEvidenceDecoder();
                await decoder.DecodeAsync(request.Path, async (batch, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Session.ProviderAlive) throw new OperationCanceledException(ct);
                    var payload = System.Text.Json.JsonSerializer.Serialize(batch);
                    if (payload.Length > 6 * 1024 * 1024) throw new InvalidOperationException("native-evidence-batch-limit");
                    try { await receive(payload).WaitAsync(ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch { throw new EvidenceReceiverFailure(); }
                    ct.ThrowIfCancellationRequested();
                    ++batches;
                }, token, request.BatchEvents, request.Anonymize, request.ProjectionSchema).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (!Session.ProviderAlive) throw new OperationCanceledException(token);
                return IntegrationSession.Serialize(new { accepted = true, completed = true, instanceGeneration = Session.InstanceGeneration,
                    parserRevision = revision, batches, reason = "native-evidence-complete" });
            }
            catch (Exception error)
            {
                // Error text/path/recording content never crosses the contract.
                return IntegrationSession.Serialize(new { accepted = true, completed = false, instanceGeneration = Session.InstanceGeneration,
                    parserRevision = revision, batches, failureCode = EvidenceFailureCode(error), reason = error is OperationCanceledException && token.IsCancellationRequested ? "native-evidence-cancelled" : "native-evidence-failed" });
            }
            finally { Interlocked.Decrement(ref _activeDecodes); }
        });
    }
}
