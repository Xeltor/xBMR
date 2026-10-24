// Adapted from the companion XCAI numeric evidence projection; parsing and file ownership are native.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BossMod.ReplayAnalysis.Evidence;

/// <summary>Reads closed local BMR v31 recordings without a ReplayBuilder, WorldState or game service.
/// Emits numeric evidence plus bounded names/content IDs only when xBMR anonymization is explicitly off.
/// Markers, RSV, chat and packets remain excluded.
/// Actor numbers and time origin are scoped to one source, not a persistent player identity.</summary>
internal sealed partial class ReplayEvidenceDecoder
{
    internal const int MaximumBatchEvents = 16384;
    internal const int IntermediateBatchEvents = 2048;
    internal const int LegacyBatchEvents = 1024;
    private int projectionSchema = 1;
    private readonly Dictionary<ulong, int> actors = new();
    private readonly Dictionary<string, int> omitted = new();
    private readonly Dictionary<(int, long), long> statuses = new();
    private readonly Dictionary<int, (long Id, long Type)> casts = new();
    private int Actor(ulong id)
    {
        if (id is 0 or 0xE0000000)
        {
            return 0;
        }
        if (this.actors.TryGetValue(id, out var actor))
        {
            return actor;
        }
        if (this.actors.Count >= 100000)
        {
            throw new InvalidDataException("actor_limit");
        }
        return this.actors[id] = this.actors.Count + 1;
    }
    public async Task DecodeAsync(string path, Func<Dictionary<string, object?>, CancellationToken, Task> emit,
        CancellationToken cancel, int batchEvents = MaximumBatchEvents, bool? anonymize = null, int projectionSchema = 1)
    {
        var assembly = typeof(ReplayParserLog).Assembly;
        if (batchEvents is not (LegacyBatchEvents or IntermediateBatchEvents or MaximumBatchEvents))
        {
            throw new ArgumentOutOfRangeException(nameof(batchEvents));
        }
        if (projectionSchema is not (1 or 3))
        {
            throw new ArgumentOutOfRangeException(nameof(projectionSchema));
        }
        this.projectionSchema = projectionSchema;
        this.actors.Clear();
        this.omitted.Clear();
        this.statuses.Clear();
        this.casts.Clear();
        // Exclusive sharing rejects a recorder still holding its write handle. Never upload a live file.
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.SequentialScan);
        if (source.Length is < 36 or > 128L * 1024 * 1024)
        {
            throw new InvalidDataException("source_size_limit");
        }
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(source, cancel)).ToLowerInvariant();
        source.Position = 0;
        using var binary = new BinaryReader(source, Encoding.UTF8, true);
        var magic = binary.ReadUInt32();
        using Stream decoded = magic == 0x42434C42 ? new VerifiedBrotliStream(source)
            : magic == 0x474F4C42 ? new LeaveOpenStream(source) : throw new InvalidDataException("binary_v31_required");
        using var lookahead = new LookaheadReadStream(decoded);
        using var strict = new BoundedStream(lookahead);
        using var header = new GuardedBinaryReader(strict);
        if (header.ReadUInt32() != 0x20524556 || header.ReadInt32() != 31)
        {
            throw new InvalidDataException("unsupported_replay_version");
        }
        var qpf = header.ReadUInt64();
        if (qpf == 0)
        {
            throw new InvalidDataException("invalid_frequency");
        }
        var gameVersion = header.ReadString();
        if (gameVersion.Length > 64 || gameVersion.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
        {
            throw new InvalidDataException("invalid_game_version");
        }
        var start = new DateTime(header.ReadInt64());
        using var guarded = new GuardedBinaryReader(strict);
        using var parser = ReplayParserLog.CreateEvidenceInput(guarded, start, qpf);
        var registeredTags = parser.RegisteredTags;
        int? selectedBeastWidth = null;
        var timestamp = start;
        var origin = start.Ticks;
        this.currentOrigin = origin;
        var rows = new List<Dictionary<string, object?>>(batchEvents);
        var identities = new List<Dictionary<string, object?>>();
        var part = 0;
        var batchBytes = 0;
        var entries = 0;
        long zone = 0, duty = 0;
        var identityLocked = false;
        var clock = Stopwatch.StartNew();
        Dictionary<string, object?> Batch(bool complete)
        {
            var result = new Dictionary<string, object?>
            {
                ["schema_version"] = projectionSchema,
                ["source_digest"] = digest,
                ["part"] = part,
                ["bmr_version"] = assembly.GetName().Version!.ToString(),
                ["parser_mvid"] = typeof(ReplayParserLog).Module.ModuleVersionId.ToString(),
                ["parserRevision"] = typeof(ReplayParserLog).Module.ModuleVersionId.ToString(),
                ["replay_version"] = 31,
                ["game_version"] = gameVersion,
                ["zone"] = zone,
                ["duty"] = duty,
                ["recorded_day"] = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["events"] = rows.ToArray(),
                ["complete"] = complete,
                ["decoded_entries"] = complete ? entries : 0,
                ["omitted_kinds"] = complete ? new Dictionary<string, int>(this.omitted) : new Dictionary<string, int>()
            };
            if (anonymize.HasValue)
            {
                result["shared_identities"] = identities.ToArray();
            }
            return result;
        }
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            if (entries >= 10000000 || clock.Elapsed > TimeSpan.FromMinutes(10))
            {
                throw new InvalidDataException("decode_budget");
            }
            strict.BeginEntry();
            var tag = parser.NextEntry();
            var tagText = tag.ToString()!;
            if (strict.EndReached)
            {
                if (strict.EntryBytes != 0)
                {
                    throw new InvalidDataException("truncated_tag");
                }
                break;
            }
            ++entries;
            WorldState.Operation? op;
            if (tagText == "CBST")
            {
                int width = BeastStateLayout.Detect(lookahead.Peek(), lookahead.EndOfSource, registeredTags, selectedBeastWidth);
                selectedBeastWidth = width;
                var count = width == 1 ? header.ReadByte() : header.ReadInt32();
                if (count is < 0 or > 3)
                {
                    throw new InvalidDataException("invalid_beast_count");
                }
                for (var i = 0; i < count; ++i)
                {
                    header.ReadByte();
                }
                this.omitted["OpBeastmasterBeastsChanged"] = this.omitted.GetValueOrDefault("OpBeastmasterBeastsChanged") + 1;
                continue;
            }

            guarded.NextIntegerLimit = tagText switch
            {
                "CST!" => 64,
                "SLOG" => 256,
                "PATS" => 65536,
                _ => null
            };
            op = parser.Decode(tag);
            if (op == null || strict.EndReached)
            {
                throw new InvalidDataException("truncated_entry");
            }
            var kind = op.GetType().Name;
            if (op is WorldState.OpFrameStart frame)
            {
                var current = frame.Frame.Timestamp;
                if (current < timestamp)
                {
                    throw new InvalidDataException("nonmonotonic_timestamp");
                }
                timestamp = current;
            }
            if (op is WorldState.OpZoneChange zoneChange && !identityLocked)
            {
                zone = zoneChange.Zone;
                duty = zoneChange.CFCID;
            }
            long ticks = timestamp.Ticks - origin;
            Dictionary<string, object?>? identity = anonymize == false ? this.SharedIdentity(op, entries, ticks) : null;
            var row = this.Project(op, entries, ticks);
            if (row == null)
            {
                this.omitted[kind] = this.omitted.GetValueOrDefault(kind) + 1;
                if (identity == null)
                {
                    continue;
                }
            }
            // Leave a nonempty final batch to carry EOF/coverage. One source may span zones;
            // zone operations remain in the event stream even after initial catalog identity locks.
            var rowBytes = (row == null ? 0 : JsonSerializer.SerializeToUtf8Bytes(row).Length)
                + (identity == null ? 0 : JsonSerializer.SerializeToUtf8Bytes(identity).Length);
            var byteBudget = batchEvents == MaximumBatchEvents ? 4 * 1024 * 1024 : 1024 * 1024;
            if (row != null && rows.Count > 0 && (rows.Count >= batchEvents || identities.Count >= batchEvents || batchBytes + rowBytes > byteBudget))
            {
                identityLocked = true;
                // The safety budget measures decoder CPU/wall work, excluding caller network waits.
                clock.Stop();
                await emit(Batch(false), cancel).WaitAsync(cancel);
                clock.Start();
                ++part;
                rows.Clear();
                identities.Clear();
                batchBytes = 0;
                await Task.Delay(10, cancel); // Cooperative yield; caller additionally pauses for combat.
            }
            if (row != null)
            {
                rows.Add(row);
            }
            if (identity != null)
            {
                if (identities.Count >= 16384 || batchBytes + rowBytes > 5 * 1024 * 1024)
                {
                    throw new InvalidDataException("identity_batch_limit");
                }
                identities.Add(identity);
            }
            batchBytes += rowBytes;
        }
        if (rows.Count == 0)
        {
            throw new InvalidDataException("no_supported_events");
        }
        await emit(Batch(true), cancel).WaitAsync(cancel);
    }

    private long currentOrigin;

    private sealed class GuardedBinaryReader(Stream source) : BinaryReader(source, Encoding.UTF8, true)
    {
        public int? NextIntegerLimit
        {
            get; set;
        }

        public override int ReadInt32()
        {
            int value = base.ReadInt32();
            int? limit = this.NextIntegerLimit;
            this.NextIntegerLimit = null;
            if (limit.HasValue && (value < 0 || value > limit.Value))
            {
                throw new InvalidDataException("allocation_limit");
            }
            return value;
        }

        public override byte[] ReadBytes(int count)
        {
            if (count is < 0 or > 1024 * 1024)
            {
                throw new InvalidDataException("allocation_limit");
            }
            return base.ReadBytes(count);
        }

        public override string ReadString()
        {
            int count = this.Read7BitEncodedInt();
            byte[] bytes = this.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException();
            }
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private class LeaveOpenStream(Stream stream) : Stream
    {
        protected readonly Stream Source = stream;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException(); set => throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer) => this.Source.Read(buffer);
        public override void Flush()
        {
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    // BrotliStream may accept an unfinished compressed stream. Verify decoder Done and
    // reject missing trailers/trailing bytes before declaring a complete recording.
    private sealed class VerifiedBrotliStream(Stream source) : LeaveOpenStream(source)
    {
        private BrotliDecoder decoder;
        private readonly byte[] input = new byte[65536];
        private int start;
        private int end;
        private bool done;
        public override int Read(Span<byte> output)
        {
            if (output.IsEmpty || this.done)
            {
                return 0;
            }
            while (true)
            {
                var status = this.decoder.Decompress(this.input.AsSpan(this.start, this.end - this.start), output, out var consumed, out var written);
                this.start += consumed;
                if (status == System.Buffers.OperationStatus.InvalidData)
                {
                    throw new InvalidDataException("invalid_brotli");
                }
                if (status == System.Buffers.OperationStatus.Done)
                {
                    if (this.start != this.end || this.Source.ReadByte() != -1)
                    {
                        throw new InvalidDataException("trailing_brotli_data");
                    }
                    this.done = true;
                    return written;
                }
                if (written != 0)
                {
                    return written;
                }
                if (status == System.Buffers.OperationStatus.NeedMoreData)
                {
                    var remaining = this.end - this.start;
                    this.input.AsSpan(this.start, remaining).CopyTo(this.input);
                    this.start = 0;
                    var read = this.Source.Read(this.input, remaining, this.input.Length - remaining);
                    if (read == 0)
                    {
                        throw new InvalidDataException("truncated_brotli");
                    }
                    this.end = remaining + read;
                }
            }
        }
        protected override void Dispose(bool disposing)
        {
            this.decoder.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class BoundedStream(Stream stream) : LeaveOpenStream(stream)
    {
        private long total;
        public int EntryBytes
        {
            get; private set;
        }
        public bool EndReached
        {
            get; private set;
        }
        public void BeginEntry()
        {
            this.EntryBytes = 0;
            this.EndReached = false;
        }
        public override int Read(Span<byte> buffer)
        {
            if (this.EntryBytes + buffer.Length > 1024 * 1024 || this.total + buffer.Length > 768L * 1024 * 1024)
            {
                throw new InvalidDataException("decoded_byte_limit");
            }
            var read = base.Read(buffer);
            this.total += read;
            this.EntryBytes += read;
            if (read == 0 && buffer.Length != 0)
            {
                this.EndReached = true;
            }
            return read;
        }
    }
}
