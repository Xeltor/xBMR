using BossMod;
using BossMod.ReplayAnalysis.Evidence;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static partial class Checks
{
    private static void NativeReplayEvidence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xbmr-native-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "closed.log");
            const long deferredMoveTicks = TimeSpan.TicksPerSecond / 16;
            const float deferredMoveX = 0.025f;
            void Recording(string file, int rows, bool compressed = false, int version = 31)
            {
                var startTime = new DateTime(2026, 1, 1);
                using var payload = new MemoryStream();
                using (var header = new BinaryWriter(payload, Encoding.UTF8, true))
                {
                    header.Write(0x20524556u); header.Write(version); header.Write(10_000_000UL);
                    header.Write("7.5"); header.Write(startTime.Ticks);
                }
                using var output = new ReplayRecorder.BinaryOutput(payload);
                if (version == 32)
                {
                    output.EnableCompactV32(startTime, 10_000_000);
                    output.WriteOp(new WorldState.OpFrameStart(new(startTime, 123, 0, 0.05f, 0.05f, 1), default, default, default));
                    output.WriteOp(new ClientState.OpBeastmasterBeastsChanged([1, 2, 3]));
                }
                output.WriteOp(new WorldState.OpZoneChange(1, 2));
                new ActorState.OpRename(42, "PrivateName", 1).Write(output);
                new WorldState.OpUserMarker("PrivateMarker").Write(output);
                new WorldState.OpDirectorUpdate(10, 20, 1, 2, 3, 4).Write(output);
                for (var i = 0; i < rows; ++i) output.WriteOp(new ActorState.OpMove(42, new(i, 0, 1, 0.2f)));
                if (version == 32)
                {
                    output.WriteOp(new ActorState.OpCreate(43, 0, 1, 0, "", 0, ActorType.Player, Class.BLM, 1,
                        default, 0.5f, default, true, true, 0, 0, 0));
                    const float duration = (float)deferredMoveTicks / TimeSpan.TicksPerSecond;
                    var finalFrame = new FrameState(startTime.AddTicks(deferredMoveTicks), 123 + (ulong)deferredMoveTicks, 1, duration, duration, 1);
                    output.WriteOp(new WorldState.OpFrameStart(finalFrame, default, default, default));
                    // The small non-POV move is held until the recording is finalized.
                    output.WriteOp(new ActorState.OpMove(43, new(deferredMoveX, 0, 0, 0)));
                    output.FlushPending();
                }
                output.Flush();
                using var source = File.Create(file);
                using var magic = new BinaryWriter(source, Encoding.UTF8, true);
                magic.Write(compressed ? 0x42434C42u : 0x474F4C42u); magic.Flush();
                if (compressed) { using var brotli = new BrotliStream(source, CompressionLevel.Fastest, true); brotli.Write(payload.ToArray()); }
                else source.Write(payload.ToArray());
            }
            Recording(path, 1100);
            var batches = new List<string>();
            using var first = new ManualResetEventSlim();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var decode = Task.Run(() => new ReplayEvidenceDecoder().DecodeAsync(path, async (batch, _) =>
            {
                batches.Add(JsonSerializer.Serialize(batch));
                if (batches.Count == 1) { first.Set(); await release.Task; }
            }, default, 1024, null, 3));
            Check(first.Wait(TimeSpan.FromSeconds(10)) && !decode.IsCompleted && batches.Count == 1,
                "Direct decoder awaits its consumer before reading the next batch.");
            release.SetResult(); decode.GetAwaiter().GetResult();
            using var final = JsonDocument.Parse(batches[^1]);
            Check(batches.Count == 2 && final.RootElement.GetProperty("complete").GetBoolean(), "Closed recording completes after all batches.");
            Check(final.RootElement.GetProperty("source_digest").GetString() == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), "Projection preserves the entire closed recording digest.");
            Check(!string.Join("", batches).Contains("PrivateName") && !string.Join("", batches).Contains("PrivateMarker"), "Default sharing excludes identity and user markers.");
            using var start = JsonDocument.Parse(batches[0]);
            var director = start.RootElement.GetProperty("events").EnumerateArray().Single(e => e.GetProperty("kind").GetString() == "director");
            Check(director.GetProperty("params").EnumerateArray().Select(p => p.GetInt64()).SequenceEqual(new long[] { 1, 2, 3, 4, 10, 20 }), "Typed projection preserves historical director parameter order.");
            foreach (var anonymous in new[] { true, false })
            {
                var output = new List<string>();
                new ReplayEvidenceDecoder().DecodeAsync(path, (batch, _) => { output.Add(JsonSerializer.Serialize(batch)); return Task.CompletedTask; }, default, 1024, anonymous, 3).GetAwaiter().GetResult();
                Check(string.Join("", output).Contains("PrivateName") == !anonymous, "Native anonymization explicitly controls identity sharing.");
            }
            var compressed = Path.Combine(directory, "compressed.log"); Recording(compressed, 2, true);
            new ReplayEvidenceDecoder().DecodeAsync(compressed, (_, _) => Task.CompletedTask, default, 1024, true, 1).GetAwaiter().GetResult();
            Check(true, "Native decoder accepts Brotli recordings and historical projection schema 1.");
            foreach (var brotli in new[] { false, true })
            {
                var compact = Path.Combine(directory, "compact.log");
                Recording(compact, 2, brotli, version: 32);
                foreach (var anonymous in new[] { true, false })
                {
                    JsonDocument? batch = null;
                    new ReplayEvidenceDecoder().DecodeAsync(compact, (chunk, _) =>
                    {
                        batch = JsonSerializer.SerializeToDocument(chunk);
                        return Task.CompletedTask;
                    }, default, 1024, anonymous, 3).GetAwaiter().GetResult();
                    using var result = batch ?? throw new InvalidOperationException("Compact replay did not emit a batch.");
                    var root = result.RootElement;
                    Check(root.GetProperty("complete").GetBoolean(), "Compact recording reaches verified EOF.");
                    Check(root.GetProperty("replay_version").GetInt32() == 32, "Compact recording reports the original format version.");
                    Check(root.GetProperty("events").EnumerateArray().Any(e => e.GetProperty("kind").GetString() == "director"),
                        "Compact Beastmaster and frame operations preserve alignment for subsequent events.");
                    Check(root.GetRawText().Contains("PrivateName") == !anonymous && !root.GetRawText().Contains("PrivateMarker"),
                        "Compact recording sharing preserves anonymization and excludes markers.");
                    var last = root.GetProperty("events").EnumerateArray().Last();
                    Check(last.GetProperty("kind").GetString() == "move" && last.GetProperty("ticks").GetInt64() == deferredMoveTicks
                        && Math.Abs(last.GetProperty("position").GetProperty("x").GetSingle() - deferredMoveX) < 0.0001f,
                        "The final compact frame retains the last deferred position of a non-POV player.");
                }
            }
            File.WriteAllBytes(compressed, File.ReadAllBytes(compressed)[..^3]);
            var truncated = false;
            try { new ReplayEvidenceDecoder().DecodeAsync(compressed, (_, _) => Task.CompletedTask, default).GetAwaiter().GetResult(); }
            catch (InvalidDataException) { truncated = true; }
            Check(truncated, "Truncated compressed files cannot finalize evidence.");
            using var stop = new CancellationTokenSource();
            var stalled = new ReplayEvidenceDecoder().DecodeAsync(path, (_, _) => { stop.Cancel(); return new TaskCompletionSource().Task; }, stop.Token, 1024);
            var canceled = false;
            try { stalled.GetAwaiter().GetResult(); } catch (OperationCanceledException) { canceled = true; }
            Check(canceled, "Shutdown interrupts a stalled archive consumer without waiting or completing the recording.");
        }
        finally { Directory.Delete(directory, true); }
    }
}
