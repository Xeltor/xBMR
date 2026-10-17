using BossMod;
using BossMod.Integration;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static partial class Checks
{
    private static void NativeReplayEvidence()
    {
        var assembly = typeof(AIHints).Assembly;
        var type = assembly.GetType("BossMod.Integration.IntegrationCoordinator", true)!;
        var coordinator = RuntimeHelpers.GetUninitializedObject(type);
        var sessionType = assembly.GetType("BossMod.Integration.IntegrationSession", true)!;
        using var session = (IDisposable)Activator.CreateInstance(sessionType, BindingFlags.Public | InternalInstance, null,
            new object?[] { null, 0L }, null)!;
        type.GetField("Session", InternalInstance)!.SetValue(coordinator, session);
        using var shutdown = new CancellationTokenSource();
        type.GetField("_decodeShutdown", InternalInstance)!.SetValue(coordinator, shutdown);
        var instance = (string)sessionType.GetField("InstanceGeneration", InternalInstance)!.GetValue(session)!;
        var method = type.GetMethod("DecodeEvidence", InternalInstance)!;
        var directory = Path.Combine(Path.GetTempPath(), "xbmr-native-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "closed.log");
            var start = new DateTime(2026, 1, 1);
            void Recording(string file, int rows, bool compressed = false)
            {
                using var payload = new MemoryStream();
                using (var writer = new BinaryWriter(payload, Encoding.UTF8, true))
                {
                    writer.Write(0x20524556u); writer.Write(31); writer.Write(10_000_000UL); writer.Write("7.5"); writer.Write(start.Ticks);
                }
                using (var output = new ReplayRecorder.BinaryOutput(payload))
                {
                    new WorldState.OpZoneChange(1, 2).Write(output);
                    new ActorState.OpRename(42, "PrivateName", 1).Write(output);
                    new WorldState.OpUserMarker("PrivateMarker").Write(output);
                    for (var i = 0; i < rows; ++i) new ActorState.OpMove(42, new(i, 0, 1, 0.2f)).Write(output);
                    output.Flush();
                    var bytes = payload.ToArray();
                    using var source = File.Create(file);
                    using var header = new BinaryWriter(source, Encoding.UTF8, true);
                    header.Write(compressed ? 0x42434C42u : 0x474F4C42u); header.Flush();
                    if (compressed) { using var brotli = new BrotliStream(source, CompressionLevel.Fastest, true); brotli.Write(bytes); }
                    else source.Write(bytes);
                }
            }
            string Request(string file, int schema = 3, bool? anonymize = null, string? generation = null)
                => IntegrationSession.Serialize(new { schemaVersion = 1, instanceGeneration = generation ?? instance, path = file,
                    batchEvents = 1024, projectionSchema = schema, anonymize });
            Task<string> Decode(string request, Func<string, Task> receive, CancellationToken token = default)
                => (Task<string>)method.Invoke(coordinator, [request, receive, token])!;
            Recording(path, 1100);
            var batches = new List<string>();
            using var first = new ManualResetEventSlim();
            var unblock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var decoding = Decode(Request(path), async json =>
            {
                batches.Add(json);
                if (batches.Count == 1) { first.Set(); await unblock.Task; }
            });
            Check(first.Wait(TimeSpan.FromSeconds(5)) && !decoding.IsCompleted && batches.Count == 1,
                "native decoder executes off-thread and awaits each batch callback for backpressure");
            using var cancellation = new CancellationTokenSource();
            var secondPath = Path.Combine(directory, "second.log"); Recording(secondPath, 1);
            using var secondStarted = new ManualResetEventSlim();
            var pending = Decode(Request(secondPath), _ => { secondStarted.Set(); return new TaskCompletionSource().Task; }, cancellation.Token);
            Check(secondStarted.Wait(TimeSpan.FromSeconds(5)), "second native decode can progress independently");
            using (var full = JsonDocument.Parse(Decode(Request(path), _ => Task.CompletedTask).GetAwaiter().GetResult()))
                Check(!full.RootElement.GetProperty("accepted").GetBoolean(), "third native decoder is rejected before file IO");
            cancellation.Cancel();
            using (var cancelled = JsonDocument.Parse(pending.GetAwaiter().GetResult()))
                Check(!cancelled.RootElement.GetProperty("completed").GetBoolean(), "caller cancellation interrupts a stalled batch callback without finalizing");
            unblock.SetResult();
            using (var receipt = JsonDocument.Parse(decoding.GetAwaiter().GetResult()))
            {
                Check(receipt.RootElement.GetProperty("completed").GetBoolean() && receipt.RootElement.GetProperty("batches").GetInt32() == 2,
                    "completion receipt is emitted only after final batch acknowledgement");
                Check(receipt.RootElement.GetProperty("parserRevision").GetString() == typeof(ReplayParserLog).Module.ModuleVersionId.ToString(),
                    "decoder receipt identifies the exact native parser revision");
            }
            using (var batch = JsonDocument.Parse(batches[^1]))
            {
                Check(batch.RootElement.GetProperty("complete").GetBoolean() && batch.RootElement.GetProperty("schema_version").GetInt32() == 3
                    && batch.RootElement.GetProperty("source_digest").GetString() == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                    "existing numeric evidence schema and full closed-source digest are preserved");
                Check(!string.Join("", batches).Contains("PrivateName") && !string.Join("", batches).Contains("PrivateMarker"),
                    "default numeric projection excludes identity names and user marker content");
            }
            foreach (var anonymous in new[] { true, false })
            {
                var output = new List<string>();
                using var result = JsonDocument.Parse(Decode(Request(secondPath, anonymize: anonymous), json => { output.Add(json); return Task.CompletedTask; }).GetAwaiter().GetResult());
                Check(result.RootElement.GetProperty("completed").GetBoolean() && string.Join("", output).Contains("PrivateName") == !anonymous,
                    "explicit identity sharing preserves the caller anonymization mode");
            }
            var compressed = Path.Combine(directory, "compressed.log"); Recording(compressed, 2, true);
            using (var result = JsonDocument.Parse(Decode(Request(compressed, schema: 1), _ => Task.CompletedTask).GetAwaiter().GetResult()))
                Check(result.RootElement.GetProperty("completed").GetBoolean(), "native guarded decoder accepts complete Brotli v31 recordings and legacy schema 1");
            File.WriteAllBytes(compressed, File.ReadAllBytes(compressed)[..^3]);
            using (var truncated = JsonDocument.Parse(Decode(Request(compressed), _ => Task.CompletedTask).GetAwaiter().GetResult()))
                Check(!truncated.RootElement.GetProperty("completed").GetBoolean() && truncated.RootElement.GetProperty("failureCode").GetString() == "truncated_brotli", "truncated compressed recordings cannot finalize evidence");
            using (var throwing = JsonDocument.Parse(Decode(Request(secondPath), _ => throw new System.IO.InvalidDataException("source_size_limit")).GetAwaiter().GetResult()))
                Check(!throwing.RootElement.GetProperty("completed").GetBoolean() && throwing.RootElement.GetProperty("failureCode").ValueKind == JsonValueKind.Null, "a throwing consumer batch never yields a completion receipt");
            using (var stale = JsonDocument.Parse(Decode(Request(secondPath, generation: "retired"), _ => Task.CompletedTask).GetAwaiter().GetResult()))
                Check(!stale.RootElement.GetProperty("accepted").GetBoolean(), "stale parser provider generation cannot start a recording session");
            shutdown.Cancel();
            using (var unloaded = JsonDocument.Parse(Decode(Request(secondPath), _ => Task.CompletedTask).GetAwaiter().GetResult()))
                Check(!unloaded.RootElement.GetProperty("completed").GetBoolean(), "provider shutdown cancels queued native recording work");
        }
        finally { Directory.Delete(directory, true); }
    }
}
