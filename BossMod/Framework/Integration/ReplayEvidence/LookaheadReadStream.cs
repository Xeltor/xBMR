// Adapted from the companion XCAI numeric evidence decoder. Native parser ownership stays in xBMR.
using System;
using System.IO;
namespace BossMod.Integration.ReplayEvidence;

// A fixed-size forward-only peek. Bytes are returned exactly once to the outer
// BoundedStream; peeking never seeks, skips bytes or changes entry boundaries.
internal sealed class LookaheadReadStream(Stream source) : Stream
{
    private readonly byte[] prefix = new byte[BeastStateLayout.LookaheadLimit];
    private int buffered;
    internal bool EndOfSource
    {
        get; private set;
    }

    internal ReadOnlySpan<byte> Peek()
    {
        while (this.buffered < this.prefix.Length && !this.EndOfSource)
        {
            var read = source.Read(this.prefix.AsSpan(this.buffered));
            this.buffered += read;
            this.EndOfSource = read == 0;
        }
        return this.prefix.AsSpan(0, this.buffered);
    }

    public override int Read(Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }
        if (this.buffered == 0)
        {
            var read = source.Read(buffer);
            this.EndOfSource = read == 0;
            return read;
        }
        var count = Math.Min(buffer.Length, this.buffered);
        this.prefix.AsSpan(0, count).CopyTo(buffer);
        this.buffered -= count;
        this.prefix.AsSpan(count, this.buffered).CopyTo(this.prefix);
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));
    public override bool CanRead => source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException(); set => throw new NotSupportedException();
    }
    public override void Flush()
    {
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    // The decoder owns and disposes the underlying decompression/source streams.
}
