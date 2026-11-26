using System.Linq;

namespace Anomaly.Client.Shared.Audio;

/// <summary>
/// Mends an Ogg Vorbis file's comments in memory before Godot reads it. Godot warns once a file about a comment with no <c>=</c>, and
/// tg's sounds made by Sony's encoder carry the bare <c>Sony Ogg Vorbis 1.0 Final</c>; loading every sound at the start printed a
/// screen of them. A bad comment's first space becomes <c>=</c> (its last byte when it has none), so its length stays and only the
/// checksums of the pages it lies in are worked out again.
/// </summary>
public static class VorbisComments
{
    private const int PageHeader = 27;

    private static readonly uint[] Table = MakeTable();

    /// <summary>Mends <paramref name="ogg"/> in place; true when a comment was changed. A file it cannot follow is left as it is.</summary>
    public static bool Repair(byte[] ogg)
    {
        // the bodies of the pages from the second one (the comment header starts it; the first holds the identification header alone)
        var pages = new List<(int Start, int Body, int Length)>();
        int at = 0, total = 0;
        while (at + PageHeader <= ogg.Length && pages.Count < 8 && total < 1 << 16)
        {
            if (ogg[at] != 'O' || ogg[at + 1] != 'g' || ogg[at + 2] != 'g' || ogg[at + 3] != 'S')
                return false;
            int segments = ogg[at + 26], body = at + PageHeader + segments, length = 0;
            if (body > ogg.Length)
                return false;
            for (int i = 0; i < segments; i++)
                length += ogg[at + PageHeader + i];
            if (body + length > ogg.Length)
                return false;
            if (at > 0)
            {
                pages.Add((at, body, length));
                total += length;
            }
            at = body + length;
        }
        var packet = new Mapped(ogg, pages);
        // "\x03vorbis", the vendor string, the comments' count, then each comment's length and bytes
        if (packet.Length < 15 || packet[0] != 3 || packet[1] != 'v' || packet[6] != 's')
            return false;
        long p = 7 + 4 + packet.UInt(7);
        if (p + 4 > packet.Length)
            return false;
        long count = packet.UInt(p);
        p += 4;
        var mends = new List<long>(); // written only once the whole header is read: a file cut short is left as it was
        for (long c = 0; c < count; c++)
        {
            if (p + 4 > packet.Length)
                return false;
            long length = packet.UInt(p);
            p += 4;
            if (length == 0 || p + length > packet.Length)
                return false;
            long space = -1;
            bool hasEquals = false;
            for (long i = p; i < p + length; i++)
            {
                if (packet[i] == '=')
                    hasEquals = true;
                else if (packet[i] == ' ' && space < 0)
                    space = i;
            }
            if (!hasEquals)
                mends.Add(space >= 0 ? space : p + length - 1);
            p += length;
        }
        var dirty = new HashSet<int>();
        foreach (long i in mends)
            dirty.Add(packet.Set(i, (byte)'='));
        foreach (int page in dirty)
            Stamp(ogg, page);
        return dirty.Count > 0;
    }

    /// <summary>The Ogg checksum (poly 0x04C11DB7, init 0, no reflection, no final xor).</summary>
    public static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0;
        foreach (byte b in bytes)
            crc = (crc << 8) ^ Table[(crc >> 24) ^ b];
        return crc;
    }

    // a page's checksum is over the whole page with the checksum's own four bytes as zeros
    private static void Stamp(byte[] ogg, int start)
    {
        int segments = ogg[start + 26], end = start + PageHeader + segments;
        for (int i = 0; i < segments; i++)
            end += ogg[start + PageHeader + i];
        ogg[start + 22] = ogg[start + 23] = ogg[start + 24] = ogg[start + 25] = 0;
        uint crc = Crc(ogg.AsSpan(start, end - start));
        ogg[start + 22] = (byte)crc;
        ogg[start + 23] = (byte)(crc >> 8);
        ogg[start + 24] = (byte)(crc >> 16);
        ogg[start + 25] = (byte)(crc >> 24);
    }

    private static uint[] MakeTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint r = i << 24;
            for (int k = 0; k < 8; k++)
                r = (r & 0x80000000) != 0 ? (r << 1) ^ 0x04C11DB7 : r << 1;
            table[i] = r;
        }
        return table;
    }

    // the packet's bytes as one run over the pages' bodies
    private readonly struct Mapped(byte[] ogg, List<(int Start, int Body, int Length)> pages)
    {
        public long Length => pages.Sum(page => (long)page.Length);

        public byte this[long i] => ogg[Offset(i, out _)];

        public long UInt(long i) => this[i] | (long)this[i + 1] << 8 | (long)this[i + 2] << 16 | (long)this[i + 3] << 24;

        /// <summary>Writes a byte; returns the start of its page.</summary>
        public int Set(long i, byte value)
        {
            int at = Offset(i, out int page);
            ogg[at] = value;
            return page;
        }

        private int Offset(long i, out int page)
        {
            foreach (var p in pages)
            {
                if (i < p.Length)
                {
                    page = p.Start;
                    return p.Body + (int)i;
                }
                i -= p.Length;
            }
            throw new ArgumentOutOfRangeException(nameof(i));
        }
    }
}
