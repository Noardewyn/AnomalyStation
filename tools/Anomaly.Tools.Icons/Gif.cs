using System.IO;
using System.IO.Compression;
using System.Text;

namespace Anomaly.Tools.Icons;

// M9 slice E: tgui's GIF pictures (tg's /datum/asset/simple/headers, icons/ui/programs/*.gif: NtOS's battery, signal and program
// headers). A GIF becomes the same pair as a .dmi: <path without .gif>.png, its frames left to right, and <path>.json, one unnamed
// state with the frames and their delays (DmiJson). The decoder reads what tg's files use: GIF87a/89a, global and local colour
// tables, the graphic control extension's delay, transparency and disposal, interlaced or not.

/// <summary>One decoded GIF: its frames, composited as a viewer shows them, and their delays in deciseconds.</summary>
public sealed record GifImage(int Width, int Height, List<uint[]> Frames, List<double> Delays);

public static class GifReader
{
    /// <summary>Decodes <paramref name="bytes"/>; throws <see cref="InvalidDataException"/> on a malformed file.</summary>
    public static GifImage Read(byte[] bytes)
    {
        int p = 0;
        string signature = Encoding.ASCII.GetString(bytes, 0, 6);
        if (signature != "GIF87a" && signature != "GIF89a")
            throw new InvalidDataException("not a GIF");
        p = 6;
        int width = bytes[p] | (bytes[p + 1] << 8);
        int height = bytes[p + 2] | (bytes[p + 3] << 8);
        int packed = bytes[p + 4];
        int background = bytes[p + 5];
        p += 7;
        uint[] globalTable = null;
        if ((packed & 0x80) != 0)
            globalTable = ReadTable(bytes, ref p, 1 << ((packed & 7) + 1));

        var frames = new List<uint[]>();
        var delays = new List<double>();
        var canvas = new uint[width * height];
        int delay = 0, transparent = -1, disposal = 0;
        while (p < bytes.Length)
        {
            byte block = bytes[p++];
            if (block == 0x3B) // trailer
                break;
            if (block == 0x21) // extension
            {
                byte label = bytes[p++];
                if (label == 0xF9 && bytes[p] >= 4) // graphic control
                {
                    int flags = bytes[p + 1];
                    delay = bytes[p + 2] | (bytes[p + 3] << 8);
                    transparent = (flags & 1) != 0 ? bytes[p + 4] : -1;
                    disposal = (flags >> 2) & 7;
                }
                SkipBlocks(bytes, ref p);
                continue;
            }
            if (block != 0x2C)
                throw new InvalidDataException("unknown block 0x" + block.ToString("x2"));
            int left = bytes[p] | (bytes[p + 1] << 8), top = bytes[p + 2] | (bytes[p + 3] << 8);
            int w = bytes[p + 4] | (bytes[p + 5] << 8), h = bytes[p + 6] | (bytes[p + 7] << 8);
            int localPacked = bytes[p + 8];
            p += 9;
            uint[] table = (localPacked & 0x80) != 0 ? ReadTable(bytes, ref p, 1 << ((localPacked & 7) + 1)) : globalTable;
            if (table == null)
                throw new InvalidDataException("a frame without a colour table");
            bool interlaced = (localPacked & 0x40) != 0;
            int minCode = bytes[p++];
            byte[] data = ReadBlocks(bytes, ref p);
            byte[] indices = Lzw(data, minCode, w * h);

            uint[] before = disposal == 3 ? (uint[])canvas.Clone() : null;
            for (int i = 0; i < w * h; i++)
            {
                int row = i / w, col = i % w;
                int y = interlaced ? InterlacedRow(row, h) : row;
                int index = indices[i];
                if (index == transparent || index >= table.Length)
                    continue;
                int x = left + col, yy = top + y;
                if (x < width && yy < height)
                    canvas[yy * width + x] = table[index];
            }
            frames.Add((uint[])canvas.Clone());
            delays.Add(delay / 10.0); // centiseconds to BYOND deciseconds
            if (disposal == 2) // restore to background: clear the frame's rectangle
            {
                for (int y = top; y < top + h && y < height; y++)
                    for (int x = left; x < left + w && x < width; x++)
                        canvas[y * width + x] = 0;
            }
            else if (disposal == 3 && before != null) // restore to previous
                canvas = before;
            delay = 0;
            transparent = -1;
            disposal = 0;
        }
        if (frames.Count == 0)
            throw new InvalidDataException("a GIF without frames");
        _ = background;
        return new GifImage(width, height, frames, delays);
    }

    // The row of the i-th stored row of an interlaced frame (passes 8 from 0, 8 from 4, 4 from 2, 2 from 1).
    private static int InterlacedRow(int stored, int height)
    {
        int[] starts = { 0, 4, 2, 1 }, steps = { 8, 8, 4, 2 };
        for (int pass = 0; pass < 4; pass++)
        {
            int rows = starts[pass] >= height ? 0 : (height - starts[pass] + steps[pass] - 1) / steps[pass];
            if (stored < rows)
                return starts[pass] + stored * steps[pass];
            stored -= rows;
        }
        return 0;
    }

    // RGBA packed as 0xAABBGGRR (little-endian bytes R, G, B, A).
    private static uint[] ReadTable(byte[] bytes, ref int p, int count)
    {
        var table = new uint[count];
        for (int i = 0; i < count; i++, p += 3)
            table[i] = bytes[p] | ((uint)bytes[p + 1] << 8) | ((uint)bytes[p + 2] << 16) | 0xFF000000u;
        return table;
    }

    private static void SkipBlocks(byte[] bytes, ref int p)
    {
        while (p < bytes.Length && bytes[p] != 0)
            p += bytes[p] + 1;
        p++;
    }

    private static byte[] ReadBlocks(byte[] bytes, ref int p)
    {
        var data = new MemoryStream();
        while (p < bytes.Length && bytes[p] != 0)
        {
            data.Write(bytes, p + 1, bytes[p]);
            p += bytes[p] + 1;
        }
        p++;
        return data.ToArray();
    }

    // GIF's variable-width LZW.
    private static byte[] Lzw(byte[] data, int minCode, int pixels)
    {
        var output = new byte[pixels];
        int clear = 1 << minCode, end = clear + 1;
        int codeSize = minCode + 1, next = end + 1;
        var prefix = new int[4096];
        var suffix = new byte[4096];
        var stack = new byte[4097];
        for (int i = 0; i < clear; i++)
            suffix[i] = (byte)i;
        int written = 0, bits = 0, buffer = 0, pos = 0, old = -1;
        byte first = 0;
        while (written < pixels)
        {
            while (bits < codeSize && pos < data.Length)
            {
                buffer |= data[pos++] << bits;
                bits += 8;
            }
            if (bits < codeSize)
                break;
            int code = buffer & ((1 << codeSize) - 1);
            buffer >>= codeSize;
            bits -= codeSize;
            if (code == clear)
            {
                codeSize = minCode + 1;
                next = end + 1;
                old = -1;
                continue;
            }
            if (code == end)
                break;
            int top = 0, current = code;
            if (old == -1)
            {
                output[written++] = suffix[code];
                old = code;
                first = suffix[code];
                continue;
            }
            if (code >= next) // the KwKwK case
            {
                stack[top++] = first;
                current = old;
            }
            while (current >= clear)
            {
                stack[top++] = suffix[current];
                current = prefix[current];
            }
            first = suffix[current];
            stack[top++] = first;
            while (top > 0 && written < pixels)
                output[written++] = stack[--top];
            if (next < 4096)
            {
                prefix[next] = old;
                suffix[next] = first;
                next++;
                if (next == 1 << codeSize && codeSize < 12)
                    codeSize++;
            }
            old = code;
        }
        return output;
    }

    /// <summary>The sheet of a picture file: a GIF's frames (<see cref="ToSheet(GifImage)"/>), or a PNG under a .gif name, as it is.</summary>
    public static (byte[] Png, DmiFile Meta) ToSheet(byte[] bytes)
    {
        if (bytes.Length > 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            int w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            int h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            var still = new DmiFile { Width = w, Height = h, SheetWidth = w, SheetHeight = h };
            still.States.Add(new DmiState());
            return (bytes, still);
        }
        return ToSheet(Read(bytes));
    }

    /// <summary>The frames left to right as a PNG (RGBA, 8 bits) and the matching metadata: one unnamed state.</summary>
    public static (byte[] Png, DmiFile Meta) ToSheet(GifImage gif)
    {
        int count = gif.Frames.Count;
        int sheetWidth = gif.Width * count;
        var rgba = new byte[sheetWidth * gif.Height * 4];
        for (int f = 0; f < count; f++)
        {
            uint[] frame = gif.Frames[f];
            for (int y = 0; y < gif.Height; y++)
                for (int x = 0; x < gif.Width; x++)
                {
                    uint c = frame[y * gif.Width + x];
                    int o = (y * sheetWidth + f * gif.Width + x) * 4;
                    rgba[o] = (byte)c;
                    rgba[o + 1] = (byte)(c >> 8);
                    rgba[o + 2] = (byte)(c >> 16);
                    rgba[o + 3] = (byte)(c >> 24);
                }
        }
        var meta = new DmiFile { Width = gif.Width, Height = gif.Height, SheetWidth = sheetWidth, SheetHeight = gif.Height };
        meta.States.Add(new DmiState { Frames = count, Delays = count > 1 ? gif.Delays.ToArray() : null });
        return (PngWriter.Write(sheetWidth, gif.Height, rgba), meta);
    }
}

/// <summary>A minimal PNG writer: RGBA, 8 bits per channel, no filter, zlib-compressed.</summary>
public static class PngWriter
{
    public static byte[] Write(int width, int height, byte[] rgba)
    {
        var raw = new MemoryStream();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgba, y * width * 4, width * 4);
        }
        var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            raw.WriteTo(z);

        var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var header = new byte[13];
        WriteBig(header, 0, width);
        WriteBig(header, 4, height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteBig(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBig(len, 0, data.Length);
        s.Write(len);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        uint crc = Crc(typeBytes, 0xFFFFFFFFu);
        crc = Crc(data, crc) ^ 0xFFFFFFFFu;
        var c = new byte[4];
        WriteBig(c, 0, (int)crc);
        s.Write(c);
    }

    private static uint[] _table;

    private static uint Crc(byte[] data, uint crc)
    {
        if (_table == null)
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            _table = t;
        }
        foreach (byte b in data)
            crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
