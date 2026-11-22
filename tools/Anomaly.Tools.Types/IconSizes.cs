using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Anomaly.Tools.Types;

/// <summary>
/// The sizes of tg's icon files that are not one tile (32 x 32), for the port's <c>get_size_in_tiles</c> (icons.dm:1266) and
/// <c>set_bounds</c>: the server renders no icons, so BYOND's <c>icon.Width()</c> of a file is read here from the file's DMI
/// header (the PNG's <c>Description</c> text chunk, "width = N" and "height = N"; a header without them is 32 x 32, BYOND's
/// default). Every <c>.dmi</c> under tg's <c>icons/</c>, by its resource path, sorted.
/// </summary>
internal static class IconSizes
{
    public static SortedDictionary<string, (int Width, int Height)> Read(string tg)
    {
        var sizes = new SortedDictionary<string, (int, int)>(StringComparer.Ordinal);
        string root = Path.Combine(tg, "icons");
        foreach (string file in Directory.EnumerateFiles(root, "*.dmi", SearchOption.AllDirectories))
        {
            (int w, int h) = SizeOf(File.ReadAllBytes(file));
            if (w == 32 && h == 32) continue;
            sizes[Path.GetRelativePath(tg, file).Replace('\\', '/')] = (w, h);
        }
        return sizes;
    }

    private static (int, int) SizeOf(byte[] png)
    {
        string text = Description(png);
        int width = 32, height = 32;
        if (text == null) return (width, height);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("state", StringComparison.Ordinal)) break; // the file's own lines come before the states
            if (line.StartsWith("width = ", StringComparison.Ordinal)) width = int.Parse(line.AsSpan(8));
            else if (line.StartsWith("height = ", StringComparison.Ordinal)) height = int.Parse(line.AsSpan(9));
        }
        return (width, height);
    }

    // The PNG's "Description" tEXt or zTXt chunk (BYOND writes the DMI metadata there).
    private static string Description(byte[] png)
    {
        int at = 8; // the signature
        while (at + 8 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            string type = Encoding.ASCII.GetString(png, at + 4, 4);
            int data = at + 8;
            if (type is "tEXt" or "zTXt")
            {
                int nul = Array.IndexOf(png, (byte)0, data, length);
                if (nul > 0 && Encoding.ASCII.GetString(png, data, nul - data) == "Description")
                {
                    if (type == "tEXt")
                        return Encoding.Latin1.GetString(png, nul + 1, data + length - nul - 1);
                    using var z = new ZLibStream(new MemoryStream(png, nul + 2, data + length - nul - 2), CompressionMode.Decompress);
                    using var reader = new StreamReader(z, Encoding.Latin1);
                    return reader.ReadToEnd();
                }
            }
            if (type == "IDAT" || type == "IEND") return null;
            at = data + length + 4; // the data and its CRC
        }
        return null;
    }
}
