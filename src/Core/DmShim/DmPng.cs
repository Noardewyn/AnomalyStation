using System.IO;
using System.IO.Compression;

namespace Anomaly.Core.DmShim;

// Silicons SI4d (D326): the PNG files a world writes and reads itself (rust-g's rustg_dmi_create_png, BYOND's icon(file) of such a PNG
// and its Width, Height and GetPixel): tg's persistent paintings. The server reads no tg .dmi (D104); these are its own files.

/// <summary>A minimal PNG codec: 8-bit RGBA, no interlacing (what rust-g writes); decoding also takes RGB, grey and palette images.</summary>
public static class DmPng
{
    private static ReadOnlySpan<byte> Signature => new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

    /// <summary>Encodes <paramref name="rgba"/> (rows from the top, each pixel 0xRRGGBBAA) as an 8-bit RGBA PNG.</summary>
    public static byte[] Encode(int width, int height, uint[] rgba)
    {
        if (width < 1 || height < 1 || rgba.Length != width * height)
            throw new ArgumentException("a PNG of " + width + " x " + height + " needs that many pixels");
        var raw = new byte[height * (width * 4 + 1)];
        int at = 0;
        for (int y = 0; y < height; y++)
        {
            raw[at++] = 0; // filter: none
            for (int x = 0; x < width; x++)
            {
                uint p = rgba[y * width + x];
                raw[at++] = (byte)(p >> 24);
                raw[at++] = (byte)(p >> 16);
                raw[at++] = (byte)(p >> 8);
                raw[at++] = (byte)p;
            }
        }
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw);
        using var png = new MemoryStream();
        png.Write(Signature);
        var header = new byte[13];
        WriteBig(header, 0, (uint)width);
        WriteBig(header, 4, (uint)height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    /// <summary>The width and height of a PNG from its header, without decoding it.</summary>
    public static (int Width, int Height) SizeOf(byte[] png)
    {
        if (png == null || png.Length < 24 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("not a PNG");
        return ((int)ReadBig(png, 16), (int)ReadBig(png, 20));
    }

    /// <summary>Decodes a PNG into its pixels (rows from the top, each 0xRRGGBBAA).</summary>
    public static (int Width, int Height, uint[] Rgba) Decode(byte[] png)
    {
        (int width, int height) = SizeOf(png);
        byte depth = png[24], type = png[25];
        if (depth != 8 || png[28] != 0 || type is not (0 or 2 or 3 or 4 or 6))
            throw new InvalidDataException("a PNG this decoder does not read (depth " + depth + ", type " + type + ", interlaced " + png[28] + ")");
        int channels = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };
        byte[] palette = null, alphas = null;
        using var data = new MemoryStream();
        for (int at = 8; at + 8 <= png.Length;)
        {
            int length = (int)ReadBig(png, at);
            string name = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            if (at + 12 + length > png.Length)
                throw new InvalidDataException("a PNG chunk runs past the file");
            if (name == "IDAT")
                data.Write(png, at + 8, length);
            else if (name == "PLTE")
                palette = png.AsSpan(at + 8, length).ToArray();
            else if (name == "tRNS")
                alphas = png.AsSpan(at + 8, length).ToArray();
            else if (name == "IEND")
                break;
            at += 12 + length;
        }
        int stride = width * channels;
        var raw = new byte[height * (stride + 1)];
        data.Position = 0;
        using (var z = new ZLibStream(data, CompressionMode.Decompress))
        {
            int read = 0;
            while (read < raw.Length)
            {
                int n = z.Read(raw, read, raw.Length - read);
                if (n == 0)
                    throw new InvalidDataException("a PNG's image data is short");
                read += n;
            }
        }
        var pixels = new byte[height * stride];
        for (int y = 0; y < height; y++)
        {
            int filter = raw[y * (stride + 1)];
            for (int i = 0; i < stride; i++)
            {
                int x = raw[y * (stride + 1) + 1 + i];
                int a = i >= channels ? pixels[y * stride + i - channels] : 0;
                int b = y > 0 ? pixels[(y - 1) * stride + i] : 0;
                int c = i >= channels && y > 0 ? pixels[(y - 1) * stride + i - channels] : 0;
                pixels[y * stride + i] = (byte)(filter switch
                {
                    0 => x,
                    1 => x + a,
                    2 => x + b,
                    3 => x + (a + b) / 2,
                    4 => x + Paeth(a, b, c),
                    _ => throw new InvalidDataException("a PNG row filter " + filter),
                });
            }
        }
        var rgba = new uint[width * height];
        for (int i = 0; i < rgba.Length; i++)
        {
            int p = i * channels;
            (uint r, uint g, uint bl, uint al) = type switch
            {
                0 => (pixels[p], pixels[p], pixels[p], 255u),
                2 => (pixels[p], pixels[p + 1], pixels[p + 2], 255u),
                3 => (palette[pixels[p] * 3], palette[pixels[p] * 3 + 1], palette[pixels[p] * 3 + 2], alphas != null && pixels[p] < alphas.Length ? alphas[pixels[p]] : 255u),
                4 => (pixels[p], pixels[p], pixels[p], pixels[p + 1]),
                _ => (pixels[p], pixels[p + 1], pixels[p + 2], pixels[p + 3]),
            };
            rgba[i] = r << 24 | g << 16 | bl << 8 | al;
        }
        return (width, height, rgba);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream png, string name, byte[] data)
    {
        var head = new byte[8];
        WriteBig(head, 0, (uint)data.Length);
        System.Text.Encoding.ASCII.GetBytes(name, 0, 4, head, 4);
        png.Write(head);
        png.Write(data);
        var crcInput = new byte[4 + data.Length];
        Array.Copy(head, 4, crcInput, 0, 4);
        Array.Copy(data, 0, crcInput, 4, data.Length);
        var crc = new byte[4];
        WriteBig(crc, 0, Crc32(crcInput));
        png.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }

    private static void WriteBig(byte[] buffer, int at, uint value)
    {
        buffer[at] = (byte)(value >> 24);
        buffer[at + 1] = (byte)(value >> 16);
        buffer[at + 2] = (byte)(value >> 8);
        buffer[at + 3] = (byte)value;
    }

    private static uint ReadBig(byte[] buffer, int at) => (uint)(buffer[at] << 24 | buffer[at + 1] << 16 | buffer[at + 2] << 8 | buffer[at + 3]);
}
