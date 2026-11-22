using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Text;

namespace Anomaly.Tools.Icons;

// Owner: M2 P7. Reads the metadata of a BYOND .dmi (a PNG whose zTXt chunk "Description" lists the states) without decoding
// a pixel: the tool copies the PNG as it is, and the client's atlas cuts the frames out of it by the rectangles written here.
// The .dmi format ("DMI spec"): the sheet holds the frames of every state one after the other, left to right and wrapping at
// the sheet's width; within a state the animation frames follow each other and each frame holds its directions (south, north,
// east, west, then southeast, southwest, northeast, northwest for eight directions): direction d of frame f is the state's frame
// f * dirs + d (BYOND's order, as OpenDream's DMIParser reads it; the owner's play test 2026-10-14 found a camera's pan drawn from
// the wrong directions while the tool read it direction-major).

/// <summary>One icon state of a .dmi.</summary>
public sealed class DmiState
{
    /// <summary>The state's name (empty for the default state); BYOND takes the first state of a name for a lookup.</summary>
    public string Name = string.Empty;

    /// <summary>Directions: 1, 4 or 8.</summary>
    public int Dirs = 1;

    /// <summary>Animation frames per direction.</summary>
    public int Frames = 1;

    /// <summary>The delay of each frame in deciseconds (BYOND ticks of 0.1 s), null for a still state.</summary>
    public double[] Delays;

    /// <summary>The loop count (0: forever).</summary>
    public int Loop;

    /// <summary>True when the animation plays forward and then backward.</summary>
    public bool Rewind;

    /// <summary>True for a movement state (BYOND shows it while the atom glides; <c>icon_state</c> lookups prefer the plain one of the same name).</summary>
    public bool Movement;

    /// <summary>The hotspot text (<c>x,y,frame</c>), null when none.</summary>
    public string Hotspot;

    /// <summary>The index of the state's first frame in the sheet.</summary>
    public int First;
}

/// <summary>The metadata of one .dmi.</summary>
public sealed class DmiFile
{
    /// <summary>The DMI version text (<c>4.0</c>).</summary>
    public string Version = "4.0";

    /// <summary>The frame width in pixels.</summary>
    public int Width = 32;

    /// <summary>The frame height in pixels.</summary>
    public int Height = 32;

    /// <summary>The PNG's width.</summary>
    public int SheetWidth;

    /// <summary>The PNG's height.</summary>
    public int SheetHeight;

    /// <summary>The states in file order.</summary>
    public List<DmiState> States = new();

    /// <summary>Frames per sheet row.</summary>
    public int Columns => Math.Max(1, SheetWidth / Math.Max(1, Width));

    /// <summary>The frame at <paramref name="index"/> in sheet order, as x, y of its upper left corner.</summary>
    public (int X, int Y) FramePosition(int index) => (index % Columns * Width, index / Columns * Height);

    /// <summary>The number of frames the sheet can hold.</summary>
    public int Capacity => Columns * Math.Max(1, SheetHeight / Math.Max(1, Height));
}

/// <summary>Reads the metadata of a .dmi from its bytes.</summary>
public static class DmiReader
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>Parses <paramref name="png"/>; throws <see cref="InvalidDataException"/> when it is no PNG or has no DMI description.</summary>
    public static DmiFile Read(byte[] png)
    {
        if (png == null || png.Length < 33)
            throw new InvalidDataException("too short for a PNG");
        for (int i = 0; i < Signature.Length; i++)
        {
            if (png[i] != Signature[i])
                throw new InvalidDataException("not a PNG (bad signature)");
        }

        int position = Signature.Length;
        int sheetWidth = 0, sheetHeight = 0;
        string description = null;
        bool sawHeader = false;
        while (position + 12 <= png.Length)
        {
            uint length = ReadUInt32(png, position);
            if (length > int.MaxValue || position + 12L + length > png.Length)
                throw new InvalidDataException("a PNG chunk runs past the end of the file");
            string type = Encoding.ASCII.GetString(png, position + 4, 4);
            int body = position + 8;
            switch (type)
            {
                case "IHDR":
                    if (length < 13)
                        throw new InvalidDataException("a short IHDR");
                    sheetWidth = (int)ReadUInt32(png, body);
                    sheetHeight = (int)ReadUInt32(png, body + 4);
                    sawHeader = true;
                    break;
                case "zTXt":
                    description ??= ReadCompressedText(png, body, (int)length);
                    break;
                case "tEXt":
                    description ??= ReadPlainText(png, body, (int)length);
                    break;
                case "iTXt":
                    description ??= ReadInternationalText(png, body, (int)length);
                    break;
            }
            if (type == "IDAT" || type == "IEND")
            {
                // The description sits before the image data in every .dmi; stop at the first IDAT when we have it.
                if (description != null || type == "IEND")
                    break;
            }
            position = body + (int)length + 4;
        }

        if (!sawHeader)
            throw new InvalidDataException("no IHDR chunk");
        if (description == null)
            throw new InvalidDataException("no DMI description (not a .dmi)");
        DmiFile file = ParseDescription(description);
        file.SheetWidth = sheetWidth;
        file.SheetHeight = sheetHeight;
        return file;
    }

    /// <summary>Parses the text of the description (<c># BEGIN DMI ... # END DMI</c>).</summary>
    public static DmiFile ParseDescription(string description)
    {
        var file = new DmiFile();
        DmiState state = null;
        bool begun = false;
        int next = 0;
        foreach (string raw in description.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("# BEGIN DMI", StringComparison.Ordinal))
            {
                begun = true;
                continue;
            }
            if (line.StartsWith("# END DMI", StringComparison.Ordinal))
                break;
            if (!begun)
                continue;
            int equals = line.IndexOf('=');
            if (equals < 0)
                continue;
            string key = line.Substring(0, equals).Trim();
            string value = line.Substring(equals + 1).Trim();
            if (state == null)
            {
                switch (key)
                {
                    case "version":
                        file.Version = value;
                        break;
                    case "width":
                        file.Width = ParseInt(value, key);
                        break;
                    case "height":
                        file.Height = ParseInt(value, key);
                        break;
                }
            }
            if (key == "state")
            {
                if (state != null)
                    next = Finish(file, state, next);
                state = new DmiState { Name = Unquote(value) };
                file.States.Add(state);
                continue;
            }
            if (state == null)
                continue;
            switch (key)
            {
                case "dirs":
                    state.Dirs = ParseInt(value, key);
                    break;
                case "frames":
                    state.Frames = ParseInt(value, key);
                    break;
                case "delay":
                    state.Delays = ParseDelays(value);
                    break;
                case "loop":
                    state.Loop = ParseInt(value, key);
                    break;
                case "rewind":
                    state.Rewind = ParseInt(value, key) != 0;
                    break;
                case "movement":
                    state.Movement = ParseInt(value, key) != 0;
                    break;
                case "hotspot":
                    state.Hotspot = value;
                    break;
            }
        }
        if (state != null)
            Finish(file, state, next);
        if (!begun)
            throw new InvalidDataException("the description has no '# BEGIN DMI' line");
        if (file.Width < 1 || file.Height < 1)
            throw new InvalidDataException("a frame size of zero");
        return file;
    }

    // A state's frame count and delay list are final when the next state starts; its first frame follows the previous state's last.
    private static int Finish(DmiFile file, DmiState state, int first)
    {
        if (state.Dirs != 1 && state.Dirs != 4 && state.Dirs != 8)
            throw new InvalidDataException($"state '{state.Name}' has {state.Dirs} directions (1, 4 or 8 expected)");
        if (state.Frames < 1)
            throw new InvalidDataException($"state '{state.Name}' has no frames");
        state.First = first;
        if (state.Frames > 1)
        {
            if (state.Delays == null || state.Delays.Length != state.Frames)
            {
                var fixedDelays = new double[state.Frames];
                for (int i = 0; i < fixedDelays.Length; i++)
                    fixedDelays[i] = state.Delays != null && i < state.Delays.Length ? state.Delays[i] : 1;
                state.Delays = fixedDelays;
            }
        }
        else
        {
            state.Delays = null;
        }
        return first + state.Dirs * state.Frames;
    }

    private static double[] ParseDelays(string value)
    {
        string[] parts = value.Split(',');
        var delays = new double[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out delays[i]))
                throw new InvalidDataException("a delay '" + parts[i] + "' is no number");
        }
        return delays;
    }

    private static int ParseInt(string value, string key)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
        {
            // Some files write a real number for the frame size.
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
                return (int)real;
            throw new InvalidDataException(key + " '" + value + "' is no number");
        }
        return number;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            return value.Substring(1, value.Length - 2);
        return value;
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
        => ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

    private static string ReadCompressedText(byte[] bytes, int offset, int length)
    {
        int zero = Array.IndexOf(bytes, (byte)0, offset, length);
        if (zero < 0 || zero + 2 > offset + length)
            return null;
        if (Encoding.Latin1.GetString(bytes, offset, zero - offset) != "Description")
            return null;
        // zero + 1 is the compression method (0: zlib).
        using var input = new MemoryStream(bytes, zero + 2, offset + length - (zero + 2));
        using var inflate = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflate.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string ReadPlainText(byte[] bytes, int offset, int length)
    {
        int zero = Array.IndexOf(bytes, (byte)0, offset, length);
        if (zero < 0 || Encoding.Latin1.GetString(bytes, offset, zero - offset) != "Description")
            return null;
        return Encoding.UTF8.GetString(bytes, zero + 1, offset + length - (zero + 1));
    }

    private static string ReadInternationalText(byte[] bytes, int offset, int length)
    {
        int zero = Array.IndexOf(bytes, (byte)0, offset, length);
        if (zero < 0 || zero + 3 > offset + length || Encoding.Latin1.GetString(bytes, offset, zero - offset) != "Description")
            return null;
        bool compressed = bytes[zero + 1] != 0;
        int at = zero + 3;
        // Skip the language tag and the translated keyword (both end with a zero).
        for (int skipped = 0; skipped < 2; skipped++)
        {
            int end = Array.IndexOf(bytes, (byte)0, at, offset + length - at);
            if (end < 0)
                return null;
            at = end + 1;
        }
        if (!compressed)
            return Encoding.UTF8.GetString(bytes, at, offset + length - at);
        using var input = new MemoryStream(bytes, at, offset + length - at);
        using var inflate = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflate.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
