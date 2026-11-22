using System.Collections.Concurrent;
using System.Text;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Icons;

// Protocol version 79 (silicons SI4d, D326): the images the server's world made at run time (a painting's PNG), which it sends once
// before naming them. Each is read as a one-state icon file (its .png and a .json the client writes for it, as the icon tool's converted
// icons), so a sprite, an overlay or a tgui window draws it as any icon. Content-addressed (rsc/MD5.png), so one store serves every session.

/// <summary>The runtime images the server sent (<see cref="RuntimeResourceMessage"/>), as icon files.</summary>
public static class RuntimeImages
{
    private static readonly ConcurrentDictionary<string, byte[]> s_files = new(StringComparer.Ordinal);

    /// <summary>Takes a runtime image: its PNG and the one-state sheet description of it.</summary>
    public static void Add(string path, byte[] png)
    {
        if (!Codec.IsRuntimeResourcePath(path) || png == null || png.Length < 24)
            return;
        int width = (int)ReadBig(png, 16), height = (int)ReadBig(png, 20);
        if (width < 1 || height < 1)
            return;
        string stem = path.Substring(0, path.Length - 4);
        string json = $"{{\"version\":\"4.0\",\"width\":{width},\"height\":{height},\"sheetWidth\":{width},\"sheetHeight\":{height},\"states\":[{{\"name\":\"\",\"dirs\":1,\"frames\":1,\"delays\":null,\"loop\":0,\"rewind\":false,\"movement\":false,\"hotspot\":null,\"first\":0,\"rects\":[[0,0,{width},{height}]]}}]}}";
        s_files[stem + ".json"] = Encoding.UTF8.GetBytes(json);
        s_files[path] = png;
    }

    /// <summary>The bytes of a runtime image's file (<c>rsc/MD5.png</c> or its <c>.json</c>); false for any other path.</summary>
    public static bool TryRead(string relativePath, out byte[] bytes)
    {
        bytes = null;
        return relativePath != null && relativePath.StartsWith("rsc/", StringComparison.Ordinal) && s_files.TryGetValue(relativePath, out bytes);
    }

    private static uint ReadBig(byte[] b, int at) => (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]);
}
