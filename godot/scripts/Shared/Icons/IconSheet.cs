using System.IO;
using System.Text.Json;

namespace Anomaly.Client.Shared.Icons;

/// <summary>BYOND's direction numbers (<c>NORTH</c> 1, <c>SOUTH</c> 2, <c>EAST</c> 4, <c>WEST</c> 8 and their sums) and how an icon with 1, 4 or 8 directions shows each.</summary>
public static class ByondDir
{
    public const byte North = 1;
    public const byte South = 2;
    public const byte East = 4;
    public const byte West = 8;
    public const byte NorthEast = 5;
    public const byte SouthEast = 6;
    public const byte NorthWest = 9;
    public const byte SouthWest = 10;

    /// <summary>The eight BYOND directions in the order the sectors of the viewer's angle use: south (the mob faces the viewer), clockwise from there.</summary>
    public static readonly byte[] FrontClockwise = { South, SouthEast, East, NorthEast, North, NorthWest, West, SouthWest };

    private static readonly byte[] FileOrder = { South, North, East, West, SouthEast, SouthWest, NorthEast, NorthWest };

    /// <summary>The BYOND dir of the frames at <paramref name="index"/> of a state (the inverse of <see cref="Index"/> for 8 directions).</summary>
    public static byte FromIndex(int index) => FileOrder[Math.Clamp(index, 0, 7)];

    /// <summary>
    /// The index of <paramref name="dir"/>'s frames in a state with <paramref name="dirs"/> directions: the file order south, north,
    /// east, west (then southeast, southwest, northeast, northwest for eight). One direction shows its frames for every dir; a
    /// diagonal on a four-direction icon shows the horizontal side (a mob walking diagonally in tg faces east or west); a dir that
    /// is no direction (0, up, down) shows south.
    /// </summary>
    public static int Index(byte dir, int dirs)
    {
        if (dirs <= 1)
            return 0;
        int index = dir switch
        {
            South => 0,
            North => 1,
            East => 2,
            West => 3,
            SouthEast => dirs >= 8 ? 4 : 2,
            SouthWest => dirs >= 8 ? 5 : 3,
            NorthEast => dirs >= 8 ? 6 : 2,
            NorthWest => dirs >= 8 ? 7 : 3,
            _ => 0,
        };
        return index;
    }
}

/// <summary>One icon state of a sheet: its frame rectangles (direction-major) and animation.</summary>
public sealed class IconState
{
    /// <summary>The state's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Directions: 1, 4 or 8.</summary>
    public int Dirs { get; init; } = 1;

    /// <summary>Animation frames per direction.</summary>
    public int Frames { get; init; } = 1;

    /// <summary>Frame delays in deciseconds, null for a still state.</summary>
    public double[] Delays { get; init; }

    /// <summary>Loop count (0: forever).</summary>
    public int Loop { get; init; }

    /// <summary>Whether the animation plays back and forth.</summary>
    public bool Rewind { get; init; }

    /// <summary>A movement state.</summary>
    public bool Movement { get; init; }

    /// <summary>The frames' rectangles in the sheet (x, y, width, height), <see cref="Dirs"/> times <see cref="Frames"/> of them, direction-major.</summary>
    public (int X, int Y, int W, int H)[] Rects { get; init; } = Array.Empty<(int, int, int, int)>();

    /// <summary>True when the state animates.</summary>
    public bool IsAnimated => Frames > 1;

    /// <summary>The animation frame shown <paramref name="seconds"/> after the animation started (BYOND: delays in deciseconds, <c>loop</c> times, <c>rewind</c> back and forth).</summary>
    public int FrameAt(double seconds)
    {
        if (Frames <= 1 || Delays == null || Delays.Length != Frames)
            return 0;
        double total = 0;
        for (int i = 0; i < Frames; i++)
            total += Delays[i];
        if (total <= 0)
            return 0;
        double t = Math.Max(0, seconds) * 10.0;
        // A rewinding animation plays frames 0..n-1 and then n-2..1 (the end frames are not repeated).
        int steps = Rewind && Frames > 2 ? Frames * 2 - 2 : Frames;
        double cycle = total;
        if (Rewind && Frames > 2)
        {
            cycle = total;
            for (int i = Frames - 2; i >= 1; i--)
                cycle += Delays[i];
        }
        if (Loop > 0 && t >= cycle * Loop)
            return Rewind ? 0 : Frames - 1;
        t %= cycle;
        for (int step = 0; step < steps; step++)
        {
            int frame = step < Frames ? step : steps - step;
            t -= Delays[frame];
            if (t < 0)
                return frame;
        }
        return Frames - 1;
    }
}

/// <summary>The frames of a state as pictures, whatever their source (a sheet, a GAGS composition): what the compositor draws from.</summary>
public interface IIconState
{
    /// <summary>Directions: 1, 4 or 8.</summary>
    int Dirs { get; }

    /// <summary>Animation frames per direction.</summary>
    int Frames { get; }

    /// <summary>The frame size in pixels.</summary>
    int Width { get; }

    int Height { get; }

    /// <summary>True when the state animates.</summary>
    bool IsAnimated { get; }

    /// <summary>The frame delays in deciseconds (null for a still state), the loop count (0: forever) and whether the animation plays back and forth.</summary>
    double[] Delays { get; }

    int Loop { get; }

    bool Rewind { get; }

    /// <summary>The animation frame shown <paramref name="seconds"/> into the animation.</summary>
    int FrameAt(double seconds);

    /// <summary>The picture of direction <paramref name="dirIndex"/> (<see cref="ByondDir.Index"/>) and animation frame <paramref name="frame"/>. Do not modify it.</summary>
    Raster Get(int dirIndex, int frame);
}

/// <summary>A source of named icon states (a .dmi, a GAGS composition).</summary>
public interface IIcon
{
    /// <summary>The first state called <paramref name="name"/> (null and "" are the default state).</summary>
    bool TryGetState(string name, out IIconState state);
}

/// <summary>BYOND's display rule for an icon state (M3 I1d).</summary>
public static class IconStates
{
    /// <summary>
    /// The state an atom of this icon and <c>icon_state</c> shows: the named state, or, when the icon has none of that name,
    /// its default ("") state (BYOND's display; OpenDream <c>ParsedDMIDescription.GetStateOrDefault</c>). tg relies on it: a
    /// canister keeps its map-preview state (<c>"/obj/machinery/portable_atmospherics/canister"</c>, post_init_icon_state "")
    /// after init gives it the GAGS icon, which has only "" and <c>window-base</c>, so it shows "".
    /// </summary>
    public static bool TryGetShown(this IIcon icon, string name, out IIconState state)
    {
        if (icon.TryGetState(name ?? string.Empty, out state))
            return true;
        return !string.IsNullOrEmpty(name) && icon.TryGetState(string.Empty, out state);
    }
}

/// <summary>
/// A converted .dmi: the metadata the icon tool wrote (<c>&lt;path&gt;.json</c>, tools/Anomaly.Tools.Icons/DmiJson.cs) and the decoded
/// sheet (<c>&lt;path&gt;.png</c>). Frames are cut out when first asked for and kept.
/// </summary>
public sealed class IconSheet : IIcon
{
    private readonly Dictionary<string, IconState> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<IconState, CutState> _cut = new();
    private readonly Raster _sheet;

    /// <summary>The states in file order.</summary>
    public IReadOnlyList<IconState> States { get; }

    /// <summary>Frame width in pixels.</summary>
    public int Width { get; }

    /// <summary>Frame height in pixels.</summary>
    public int Height { get; }

    public IconSheet(IReadOnlyList<IconState> states, int width, int height, Raster sheet)
    {
        States = states;
        Width = width;
        Height = height;
        _sheet = sheet;
        foreach (IconState state in states)
            _byName.TryAdd(state.Name, state);
    }

    /// <summary>Reads the tool's JSON and pairs it with the decoded sheet; throws <see cref="InvalidDataException"/> on a malformed file.</summary>
    public static IconSheet Parse(string json, Raster sheet)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            int width = root.GetProperty("width").GetInt32(), height = root.GetProperty("height").GetInt32();
            var states = new List<IconState>();
            foreach (JsonElement s in root.GetProperty("states").EnumerateArray())
            {
                int dirs = s.GetProperty("dirs").GetInt32(), frames = s.GetProperty("frames").GetInt32();
                double[] delays = null;
                if (s.TryGetProperty("delays", out JsonElement d) && d.ValueKind == JsonValueKind.Array)
                {
                    delays = new double[d.GetArrayLength()];
                    int i = 0;
                    foreach (JsonElement e in d.EnumerateArray())
                        delays[i++] = e.GetDouble();
                }
                JsonElement rectArray = s.GetProperty("rects");
                var rects = new (int, int, int, int)[rectArray.GetArrayLength()];
                int r = 0;
                foreach (JsonElement rect in rectArray.EnumerateArray())
                    rects[r++] = (rect[0].GetInt32(), rect[1].GetInt32(), rect[2].GetInt32(), rect[3].GetInt32());
                if (rects.Length != dirs * frames)
                    throw new InvalidDataException($"state '{s.GetProperty("name").GetString()}' lists {rects.Length} frames, not {dirs} x {frames}");
                states.Add(new IconState
                {
                    Name = s.GetProperty("name").GetString() ?? string.Empty,
                    Dirs = dirs,
                    Frames = frames,
                    Delays = delays,
                    Loop = s.TryGetProperty("loop", out JsonElement loop) ? loop.GetInt32() : 0,
                    Rewind = s.TryGetProperty("rewind", out JsonElement rewind) && rewind.ValueKind == JsonValueKind.True,
                    Movement = s.TryGetProperty("movement", out JsonElement movement) && movement.ValueKind == JsonValueKind.True,
                    Rects = rects,
                });
            }
            return new IconSheet(states, width, height, sheet);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("a malformed icon description: " + e.Message, e);
        }
    }

    /// <summary>The first state of that name (BYOND's lookup); movement states only when no plain state has the name.</summary>
    public bool TryGetState(string name, out IIconState state)
    {
        if (_byName.TryGetValue(name ?? string.Empty, out IconState found))
        {
            if (!_cut.TryGetValue(found, out CutState cut))
                _cut[found] = cut = new CutState(found, _sheet);
            state = cut;
            return true;
        }
        state = null;
        return false;
    }

    /// <summary>The metadata of a state (null when there is none).</summary>
    public IconState Find(string name) => _byName.TryGetValue(name ?? string.Empty, out IconState found) ? found : null;

    private sealed class CutState : IIconState
    {
        private readonly IconState _state;
        private readonly Raster _sheet;
        private readonly Raster[] _rasters;

        public CutState(IconState state, Raster sheet)
        {
            _state = state;
            _sheet = sheet;
            _rasters = new Raster[state.Rects.Length];
        }

        public int Dirs => _state.Dirs;

        public int Frames => _state.Frames;

        public int Width => _state.Rects.Length > 0 ? _state.Rects[0].W : 0;

        public int Height => _state.Rects.Length > 0 ? _state.Rects[0].H : 0;

        public bool IsAnimated => _state.IsAnimated;

        public double[] Delays => _state.Delays;

        public int Loop => _state.Loop;

        public bool Rewind => _state.Rewind;

        public int FrameAt(double seconds) => _state.FrameAt(seconds);

        public Raster Get(int dirIndex, int frame)
        {
            dirIndex = Math.Clamp(dirIndex, 0, _state.Dirs - 1);
            frame = Math.Clamp(frame, 0, _state.Frames - 1);
            int i = dirIndex * _state.Frames + frame;
            Raster raster = _rasters[i];
            if (raster == null)
            {
                (int x, int y, int w, int h) = _state.Rects[i];
                _rasters[i] = raster = _sheet.Crop(x, y, w, h);
            }
            return raster;
        }
    }
}
