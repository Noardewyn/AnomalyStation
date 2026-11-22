using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>An in-memory appearance table for the compositor's tests (the replica's three tables).</summary>
internal sealed class TestTables : IAppearanceSource
{
    private readonly List<AppearanceEntry> _entries = new();
    private readonly List<string> _strings = new();
    private readonly List<string> _resources = new();

    public TableRef Str(string text)
    {
        if (text == null)
            return TableRef.None;
        int i = _strings.IndexOf(text);
        if (i < 0)
        {
            _strings.Add(text);
            i = _strings.Count - 1;
        }
        return TableRef.Public((uint)i + 1);
    }

    public TableRef Res(string text)
    {
        if (text == null)
            return TableRef.None;
        int i = _resources.IndexOf(text);
        if (i < 0)
        {
            _resources.Add(text);
            i = _resources.Count - 1;
        }
        return TableRef.Public((uint)i + 1);
    }

    /// <summary>Adds an entry; <paramref name="configure"/> may set its other fields.</summary>
    public TableRef Add(string icon, string state, Action<AppearanceEntry> configure = null)
    {
        var entry = new AppearanceEntry { Icon = Res(icon), IconState = Str(state) };
        configure?.Invoke(entry);
        _entries.Add(entry);
        return TableRef.Public((uint)_entries.Count);
    }

    public AppearanceEntry Entry(TableRef reference) => _entries[(int)reference.Index - 1];

    public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry)
    {
        entry = reference.IsNone || reference.Index > _entries.Count ? null : _entries[(int)reference.Index - 1];
        return entry != null;
    }

    public string ResolveString(TableRef reference) => reference.IsNone || reference.Index > _strings.Count ? null : _strings[(int)reference.Index - 1];

    public string ResolveResource(TableRef reference) => reference.IsNone || reference.Index > _resources.Count ? null : _resources[(int)reference.Index - 1];
}

/// <summary>A hand-made icon: named states of given pictures.</summary>
internal sealed class FakeIcon : IIcon
{
    private readonly Dictionary<string, IIconState> _states = new();

    /// <summary>A state with one picture for every direction and frame given in <paramref name="frames"/> (direction-major).</summary>
    public FakeIcon State(string name, int dirs, int frames, params Raster[] rasters)
    {
        var all = new Raster[dirs * frames];
        for (int i = 0; i < all.Length; i++)
            all[i] = rasters[i % rasters.Length];
        _states[name] = new GeneratedState(dirs, frames, all[0].Width, all[0].Height, all, frames > 1 ? Enumerable.Repeat(1.0, frames).ToArray() : null, 0, false);
        return this;
    }

    /// <summary>A still state of one solid colour.</summary>
    public FakeIcon Solid(string name, uint rgba, int size = 32) => State(name, 1, 1, Raster.Solid(size, size, rgba));

    public bool TryGetState(string name, out IIconState state) => _states.TryGetValue(name ?? string.Empty, out state);
}

internal static class RasterAssert
{
    public static void Pixel(Raster raster, int x, int y, uint expected)
        => Assert.True(expected == raster.Get(x, y), $"pixel ({x},{y}) is {raster.Get(x, y):X8}, expected {expected:X8}");

    public static int CountOpaque(Raster raster)
    {
        int n = 0;
        for (int i = 3; i < raster.Pixels.Length; i += 4)
        {
            if (raster.Pixels[i] != 0)
                n++;
        }
        return n;
    }
}

/// <summary>A tiny PNG encoder with every colour type, filter and Adam7, so that the decoder is tested against what it must read.</summary>
internal static class PngEncoder
{
    /// <summary>Encodes <paramref name="pixels"/> (RGBA, rows from the top) as a PNG of the given colour type and depth.</summary>
    public static byte[] Encode(Raster picture, int colorType, int depth, int filter, bool interlaced, uint? transparentKey = null)
    {
        int width = picture.Width, height = picture.Height;
        int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };
        int bits = channels * depth;
        var palette = new List<uint>();
        var raw = new MemoryStream();
        if (interlaced)
        {
            int[] x0 = { 0, 4, 0, 2, 0, 1, 0 }, y0 = { 0, 0, 4, 0, 2, 0, 1 }, dx = { 8, 8, 4, 4, 2, 2, 1 }, dy = { 8, 8, 8, 4, 4, 2, 2 };
            for (int pass = 0; pass < 7; pass++)
            {
                int pw = (width - x0[pass] + dx[pass] - 1) / dx[pass], ph = (height - y0[pass] + dy[pass] - 1) / dy[pass];
                if (pw <= 0 || ph <= 0)
                    continue;
                WritePass(raw, picture, x0[pass], y0[pass], dx[pass], dy[pass], pw, ph, colorType, depth, bits, filter, palette);
            }
        }
        else
        {
            WritePass(raw, picture, 0, 0, 1, 1, width, height, colorType, depth, bits, filter, palette);
        }

        using var output = new MemoryStream();
        output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var header = new byte[13];
        Int(header, 0, width);
        Int(header, 4, height);
        header[8] = (byte)depth;
        header[9] = (byte)colorType;
        header[12] = (byte)(interlaced ? 1 : 0);
        Chunk(output, "IHDR", header);
        if (colorType == 3)
        {
            var plte = new byte[palette.Count * 3];
            var trns = new byte[palette.Count];
            for (int i = 0; i < palette.Count; i++)
            {
                plte[i * 3] = (byte)(palette[i] >> 24);
                plte[i * 3 + 1] = (byte)(palette[i] >> 16);
                plte[i * 3 + 2] = (byte)(palette[i] >> 8);
                trns[i] = (byte)palette[i];
            }
            Chunk(output, "PLTE", plte);
            Chunk(output, "tRNS", trns);
        }
        using var idat = new MemoryStream();
        using (var deflate = new ZLibStream(idat, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(raw.ToArray());
        Chunk(output, "IDAT", idat.ToArray());
        Chunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void WritePass(MemoryStream raw, Raster picture, int x0, int y0, int dx, int dy, int pw, int ph, int colorType, int depth, int bits, int filter, List<uint> palette)
    {
        int stride = (pw * bits + 7) / 8;
        int bpp = Math.Max(1, bits / 8);
        byte[] previous = new byte[stride];
        for (int row = 0; row < ph; row++)
        {
            byte[] line = new byte[stride];
            for (int col = 0; col < pw; col++)
            {
                uint p = picture.Get(x0 + col * dx, y0 + row * dy);
                int r = (int)(p >> 24) & 255, g = (int)(p >> 16) & 255, b = (int)(p >> 8) & 255, a = (int)p & 255;
                switch (colorType)
                {
                    case 6 when depth == 8:
                        line[col * 4] = (byte)r; line[col * 4 + 1] = (byte)g; line[col * 4 + 2] = (byte)b; line[col * 4 + 3] = (byte)a;
                        break;
                    case 6:
                        line[col * 8] = (byte)r; line[col * 8 + 1] = (byte)r; line[col * 8 + 2] = (byte)g; line[col * 8 + 3] = (byte)g;
                        line[col * 8 + 4] = (byte)b; line[col * 8 + 5] = (byte)b; line[col * 8 + 6] = (byte)a; line[col * 8 + 7] = (byte)a;
                        break;
                    case 2:
                        line[col * 3] = (byte)r; line[col * 3 + 1] = (byte)g; line[col * 3 + 2] = (byte)b;
                        break;
                    case 4:
                        line[col * 2] = (byte)g; line[col * 2 + 1] = (byte)a;
                        break;
                    case 0:
                        PutBits(line, col, depth, depth == 8 ? g : g >> (8 - depth));
                        break;
                    case 3:
                    {
                        int index = palette.IndexOf(p);
                        if (index < 0)
                        {
                            palette.Add(p);
                            index = palette.Count - 1;
                        }
                        PutBits(line, col, depth, index);
                        break;
                    }
                }
            }
            raw.WriteByte((byte)filter);
            raw.Write(Filter(filter, line, previous, bpp));
            previous = line;
        }
    }

    private static void PutBits(byte[] line, int col, int depth, int value)
    {
        if (depth == 8)
        {
            line[col] = (byte)value;
            return;
        }
        int perByte = 8 / depth;
        int shift = (perByte - 1 - col % perByte) * depth;
        line[col / perByte] |= (byte)(value << shift);
    }

    private static byte[] Filter(int filter, byte[] line, byte[] previous, int bpp)
    {
        var result = new byte[line.Length];
        for (int i = 0; i < line.Length; i++)
        {
            int a = i >= bpp ? line[i - bpp] : 0, b = previous[i], c = i >= bpp ? previous[i - bpp] : 0;
            int predictor = filter switch
            {
                0 => 0,
                1 => a,
                2 => b,
                3 => (a + b) >> 1,
                _ => Paeth(a, b, c),
            };
            result[i] = (byte)(line[i] - predictor);
        }
        return result;
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void Chunk(Stream output, string type, byte[] body)
    {
        var length = new byte[4];
        Int(length, 0, body.Length);
        output.Write(length);
        byte[] name = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(name);
        output.Write(body);
        // the CRC of the type and the body (PNG 5.3), so that other decoders read the file too (the M6 contact sheet)
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in name.Concat(body))
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        var check = new byte[4];
        Int(check, 0, (int)~crc);
        output.Write(check);
    }

    private static void Int(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}

/// <summary>
/// The icons the tool converted from the real tgstation clone: <c>godot/assets/tg</c> when it exists, else a conversion into a temporary
/// folder made by the built tool (once per run); the tests that need it are skipped when neither is possible (no clone, no built tool).
/// </summary>
internal static class RealIcons
{
    private static readonly Lazy<string> LazyRoot = new(Locate);

    /// <summary>The converted icons' folder, or null.</summary>
    public static string Root => LazyRoot.Value;

    public static IconLibrary NewLibrary() => new(new DirectoryIconFiles(Root));

    private static string Locate()
    {
        string directory = AppContext.BaseDirectory;
        string repository = null;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "AnomalyStation.sln")))
            {
                repository = directory;
                break;
            }
            directory = Path.GetDirectoryName(directory);
        }
        if (repository == null)
            return null;
        string assets = Path.Combine(repository, "godot", "assets", "tg");
        if (File.Exists(Path.Combine(assets, "icons", "mob", "human", "bodyparts_greyscale.json")))
            return assets;

        string tg = Environment.GetEnvironmentVariable("ANOMALY_TGSTATION");
        for (string up = repository; tg == null && !string.IsNullOrEmpty(up); up = Path.GetDirectoryName(up))
        {
            string candidate = Path.Combine(up, "tgstation");
            if (File.Exists(Path.Combine(candidate, "tgstation.dme")))
                tg = candidate;
        }
        if (tg == null)
            return null;
        string tool = new[] { "Debug", "Release" }
            .Select(c => Path.Combine(repository, "tools", "Anomaly.Tools.Icons", "bin", c, "net10.0", "Anomaly.Tools.Icons.dll"))
            .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (tool == null)
            return null;
        string output = Path.Combine(Path.GetTempPath(), "anomaly-real-icons");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(tool);
        start.ArgumentList.Add("--tg");
        start.ArgumentList.Add(tg);
        start.ArgumentList.Add("--out");
        start.ArgumentList.Add(output);
        using Process process = Process.Start(start);
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);
        return process.ExitCode == 0 ? output : null;
    }
}

/// <summary>A fact that needs the converted real icons; skipped when they cannot be found or made.</summary>
public sealed class RealIconsFactAttribute : FactAttribute
{
    public RealIconsFactAttribute()
    {
        if (RealIcons.Root == null)
            Skip = "the converted tg icons are not available (run tools/Anomaly.Tools.Icons into godot/assets/tg, or put tgstation at ../tgstation)";
    }
}

/// <summary>Icon files held in memory: a dictionary of relative paths.</summary>
internal sealed class MemoryIconFiles : IIconFiles
{
    public Dictionary<string, byte[]> Files { get; } = new();

    public MemoryIconFiles Text(string path, string text)
    {
        Files[path] = System.Text.Encoding.UTF8.GetBytes(text);
        return this;
    }

    public bool TryRead(string relativePath, out byte[] bytes) => Files.TryGetValue(relativePath, out bytes);
}
