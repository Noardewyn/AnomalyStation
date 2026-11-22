using System.IO;
using System.IO.Compression;
using System.Text;

namespace Anomaly.Core.Tests.Tools.Icons;

/// <summary>Builds small PNGs (and .dmi files, which are PNGs with a zTXt "Description") for the icon tool's tests.</summary>
internal static class PngBuilder
{
    /// <summary>A grey-level RGBA PNG of <paramref name="width"/> x <paramref name="height"/> with the optional DMI <paramref name="description"/>.</summary>
    public static byte[] Make(int width, int height, string description)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var header = new byte[13];
        WriteInt(header, 0, width);
        WriteInt(header, 4, height);
        header[8] = 8;
        header[9] = 6;
        Chunk(output, "IHDR", header);
        if (description != null)
        {
            using var text = new MemoryStream();
            text.Write(Encoding.Latin1.GetBytes("Description"));
            text.WriteByte(0);
            text.WriteByte(0);
            using (var deflate = new ZLibStream(text, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(Encoding.UTF8.GetBytes(description));
            Chunk(output, "zTXt", text.ToArray());
        }
        var raw = new byte[height * (1 + width * 4)];
        using var idat = new MemoryStream();
        using (var deflate = new ZLibStream(idat, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(raw);
        Chunk(output, "IDAT", idat.ToArray());
        Chunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] body)
    {
        var length = new byte[4];
        WriteInt(length, 0, body.Length);
        output.Write(length);
        output.Write(Encoding.ASCII.GetBytes(type));
        output.Write(body);
        output.Write(new byte[4]); // the CRC is not checked by the tool
    }

    private static void WriteInt(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
