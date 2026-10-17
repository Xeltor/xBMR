// Adapted from the companion XCAI numeric evidence decoder. Native parser ownership stays in xBMR.
using System;
using System.IO;
using System.Collections.Generic;
using System.Buffers.Binary;

namespace BossMod.Integration.ReplayEvidence;

// v31 has both byte-count and legacy int-count CBST writers. The recording
// header does not identify the writer build. Require an unambiguous following
// registered tag, then pin the layout for the rest of this source.
internal static class BeastStateLayout
{
    internal const int LookaheadLimit = 11;

    internal static int Detect(ReadOnlySpan<byte> prefix, bool eof, IReadOnlySet<uint> tags, int? selectedWidth)
    {
        var byteLayout = Valid(prefix, eof, tags, 1, selectedWidth != null);
        var intLayout = Valid(prefix, eof, tags, 4, selectedWidth != null);
        if (byteLayout && intLayout)
        {
            throw new InvalidDataException("ambiguous_beast_count_layout");
        }
        if (!byteLayout && !intLayout)
        {
            throw new InvalidDataException("unsupported_beast_count_layout");
        }
        var width = byteLayout ? 1 : 4;
        if (selectedWidth != null && width != selectedWidth)
        {
            throw new InvalidDataException("inconsistent_beast_count_layout");
        }
        return width;
    }

    private static bool Valid(ReadOnlySpan<byte> prefix, bool eof, IReadOnlySet<uint> tags, int width, bool allowEnd)
    {
        if (prefix.Length < width)
        {
            return false;
        }
        var count = width == 1 ? prefix[0] : BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (count is < 0 or > 3)
        {
            return false;
        }
        var end = width + count;
        return prefix.Length >= end + 4
            ? tags.Contains(BinaryPrimitives.ReadUInt32LittleEndian(prefix[end..]))
            : allowEnd && eof && prefix.Length == end;
    }

    internal static byte[] NativeValues(ReadOnlySpan<byte> values)
    {
        if (values.Length > 3)
        {
            throw new InvalidDataException("invalid_beast_count");
        }
        // Native ParseClientBeastmasterBeasts always returns three slots, even
        // when the recorded list is shorter; Op.Exec copies exactly three.
        var result = new byte[3];
        values.CopyTo(result);
        return result;
    }
}
