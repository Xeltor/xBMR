using System.IO;

namespace BossMod;

public sealed partial class ReplayParserLog
{
    // Closed v31 parse-only path: no ReplayBuilder, world execution, actor creation or game service.
    internal sealed class EvidenceInput : IDisposable
    {
        private readonly ReplayParserLog _parser;
        internal IReadOnlySet<uint> RegisteredTags { get; }
        internal EvidenceInput(BinaryReader reader, DateTime start, ulong frequency)
        {
            if (frequency == 0) throw new InvalidDataException("invalid_frequency");
            _parser = new(new BinaryInput(reader), null!) { _version = 31, _tsStart = start, _invQPF = 1.0 / frequency };
            RegisteredTags = _parser._dispatch.Keys.Select(k => k.Value).ToHashSet();
        }
        internal FourCC NextEntry() => _parser._input.NextEntry();
        internal WorldState.Operation Decode(FourCC tag)
            => _parser._dispatch.TryGetValue(tag, out var decode) && decode() is { } operation
                ? operation : throw new InvalidDataException("invalid_native_recording_tag");
        public void Dispose() => _parser.Dispose();
    }
    internal static EvidenceInput CreateEvidenceInput(BinaryReader reader, DateTime start, ulong frequency)
        => new(reader, start, frequency);
}
