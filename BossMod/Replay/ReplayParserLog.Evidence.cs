using System.IO;

namespace BossMod;

public sealed partial class ReplayParserLog
{
    // Closed binary parse-only path: no ReplayBuilder, world execution, actor creation or game service.
    internal sealed class EvidenceInput : IDisposable
    {
        private readonly ReplayParserLog _parser;
        internal IReadOnlySet<uint> RegisteredTags { get; }
        internal EvidenceInput(BinaryReader reader, int version, DateTime start, ulong frequency)
        {
            if (frequency == 0) throw new InvalidDataException("invalid_frequency");
            var input = new BinaryInput(reader, compactTargetLimit: 64)
            {
                CompactV32Mode = version == 32,
                CurFrameTicks = start.Ticks,
            };
            _parser = new(input, null!) { _version = version, _tsStart = start, _invQPF = 1.0 / frequency };
            RegisteredTags = _parser._dispatch.Keys.Select(k => k.Value).ToHashSet();
        }
        internal FourCC NextEntry() => _parser._input.NextEntry();
        internal WorldState.Operation Decode(FourCC tag)
            => tag != new FourCC("VER "u8) && _parser._dispatch.TryGetValue(tag, out var decode) && decode() is { } operation
                ? operation : throw new InvalidDataException("invalid_native_recording_tag");
        public void Dispose() => _parser.Dispose();
    }
    internal static EvidenceInput CreateEvidenceInput(BinaryReader reader, int version, DateTime start, ulong frequency)
        => new(reader, version, start, frequency);
}
