using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Anomaly.Client.Shared.Icons;

// Owner: M2 P7. tg's GAGS (code/datums/greyscale/_greyscale_config.dm, layer.dm) composed on the client (D104; the server sends
// "gags:<config type>|<colours>" as the icon of an appearance). The JSON is what tools/Anomaly.Tools.Icons/Gags.cs wrote.

/// <summary>A colour operand of a GAGS layer: a 1-based index into the colours given to the config, or a literal colour.</summary>
public readonly record struct GagsColorRef(int Index, uint Literal)
{
    /// <summary>True for an index.</summary>
    public bool IsIndex => Index > 0;
}

/// <summary>One layer or nested group of a GAGS icon state.</summary>
public abstract class GagsNode
{
}

/// <summary>A layer: an icon state of the config's icon file, another config's state, or a colour matrix over what is below.</summary>
public sealed class GagsLayer : GagsNode
{
    /// <summary><c>icon_state</c>, <c>reference</c> or <c>color_matrix</c>.</summary>
    public string Kind = "icon_state";

    /// <summary>The icon state (of this config's icon file, or of the referenced config).</summary>
    public string IconState = string.Empty;

    /// <summary>The referenced config's type path (a <c>reference</c> layer).</summary>
    public string ReferenceType;

    /// <summary>How the finished layer joins what is below.</summary>
    public IconBlend Blend = IconBlend.Overlay;

    /// <summary>The colours this layer takes.</summary>
    public List<GagsColorRef> Colors = new();

    /// <summary>The 20-number matrix of a <c>color_matrix</c> layer (rows as in BYOND's <c>MapColors</c>), or null.</summary>
    public float[] Matrix;
}

/// <summary>Layers generated together first and joined with the blend of the group's first layer.</summary>
public sealed class GagsGroup : GagsNode
{
    public List<GagsNode> Items = new();
}

/// <summary>The parsed JSON of one GAGS config.</summary>
public sealed class GagsConfig
{
    /// <summary>The path every config type starts with.</summary>
    public const string Root = "/datum/greyscale_config";

    /// <summary>The type path.</summary>
    public string Type { get; init; }

    /// <summary>The icon file the config's <c>icon_state</c> layers read.</summary>
    public string IconFile { get; init; }

    /// <summary>The states: name to layers, in file order.</summary>
    public Dictionary<string, List<GagsNode>> States { get; } = new(StringComparer.Ordinal);

    /// <summary>The file (under <c>gags/</c>) of a config type: <c>/datum/greyscale_config/jumpsuit/worn</c> is <c>jumpsuit/worn.json</c>.</summary>
    public static string FileOf(string typePath)
    {
        string relative = typePath.StartsWith(Root + "/", StringComparison.Ordinal) ? typePath.Substring(Root.Length + 1) : typePath.TrimStart('/');
        return relative + ".json";
    }

    /// <summary>Parses the tool's JSON; throws <see cref="InvalidDataException"/> on a malformed config.</summary>
    public static GagsConfig Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            var config = new GagsConfig
            {
                Type = root.GetProperty("type").GetString(),
                IconFile = root.GetProperty("icon_file").GetString(),
            };
            foreach (JsonProperty state in root.GetProperty("states").EnumerateObject())
                config.States[state.Name] = ParseList(state.Value);
            return config;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("a malformed GAGS config: " + e.Message, e);
        }
    }

    private static List<GagsNode> ParseList(JsonElement array)
    {
        var list = new List<GagsNode>();
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                var group = new GagsGroup();
                group.Items.AddRange(ParseList(item));
                list.Add(group);
            }
            else
            {
                list.Add(ParseLayer(item));
            }
        }
        return list;
    }

    private static GagsLayer ParseLayer(JsonElement item)
    {
        var layer = new GagsLayer
        {
            Kind = item.GetProperty("type").GetString(),
            Blend = ParseBlend(item.GetProperty("blend_mode").GetString()),
        };
        if (item.TryGetProperty("icon_state", out JsonElement state))
            layer.IconState = state.GetString() ?? string.Empty;
        if (item.TryGetProperty("reference_type", out JsonElement reference))
            layer.ReferenceType = reference.GetString();
        if (item.TryGetProperty("color_ids", out JsonElement ids))
        {
            foreach (JsonElement id in ids.EnumerateArray())
            {
                if (id.ValueKind == JsonValueKind.Number)
                    layer.Colors.Add(new GagsColorRef(id.GetInt32(), 0));
                else if (GagsColors.TryParseOne(id.GetString(), out uint rgba))
                    layer.Colors.Add(new GagsColorRef(0, rgba));
                else
                    throw new InvalidDataException("a colour operand '" + id + "'");
            }
        }
        if (item.TryGetProperty("color_matrix", out JsonElement matrix))
        {
            var numbers = new List<float>();
            foreach (JsonElement row in matrix.EnumerateArray())
            {
                foreach (JsonElement n in row.EnumerateArray())
                    numbers.Add((float)n.GetDouble());
            }
            layer.Matrix = numbers.ToArray();
        }
        return layer;
    }

    private static IconBlend ParseBlend(string name) => name?.ToLowerInvariant() switch
    {
        "add" => IconBlend.Add,
        "subtract" => IconBlend.Subtract,
        "multiply" => IconBlend.Multiply,
        "or" => IconBlend.Or,
        "overlay" => IconBlend.Overlay,
        "underlay" => IconBlend.Underlay,
        _ => throw new InvalidDataException("a blend mode '" + name + "'"),
    };
}

/// <summary>GAGS colour strings: <c>#rrggbb#rrggbb</c> (tg's <c>SSgreyscale.ParseColorString</c> splits on <c>#</c>).</summary>
public static class GagsColors
{
    /// <summary>The colours of a concatenated string; an unreadable part is skipped.</summary>
    public static List<uint> Parse(string text)
    {
        var colors = new List<uint>();
        if (string.IsNullOrEmpty(text))
            return colors;
        foreach (string part in text.Split('#'))
        {
            if (part.Length == 0)
                continue;
            if (TryParseOne("#" + part, out uint rgba))
                colors.Add(rgba);
        }
        return colors;
    }

    /// <summary>One colour: <c>#rgb</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c> as <c>0xRRGGBBAA</c>.</summary>
    public static bool TryParseOne(string text, out uint rgba)
    {
        rgba = 0;
        if (string.IsNullOrEmpty(text) || text[0] != '#')
            return false;
        string hex = text.Substring(1);
        if (hex.Length == 3)
            hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
        if (hex.Length == 4)
            hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2], hex[3], hex[3] });
        if (hex.Length != 6 && hex.Length != 8)
            return false;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            return false;
        rgba = hex.Length == 6 ? (value << 8) | 0xFF : value;
        return true;
    }
}

/// <summary>A generated state: dense frames in direction-major order, with the animation of the first layer it was built from.</summary>
public sealed class GeneratedState : IIconState
{
    private readonly Raster[] _frames;
    private readonly IconState _meta;

    public GeneratedState(int dirs, int frames, int width, int height, Raster[] rasters, double[] delays, int loop, bool rewind)
    {
        Dirs = dirs;
        Frames = frames;
        Width = width;
        Height = height;
        _frames = rasters;
        Delays = delays;
        Loop = loop;
        Rewind = rewind;
        _meta = new IconState { Dirs = dirs, Frames = frames, Delays = delays, Loop = loop, Rewind = rewind };
    }

    public int Dirs { get; }

    public int Frames { get; }

    public int Width { get; }

    public int Height { get; }

    public bool IsAnimated => Frames > 1;

    public double[] Delays { get; }

    public int Loop { get; }

    public bool Rewind { get; }

    public int FrameAt(double seconds) => _meta.FrameAt(seconds);

    public Raster Get(int dirIndex, int frame)
        => _frames[Math.Clamp(dirIndex, 0, Dirs - 1) * Frames + Math.Clamp(frame, 0, Frames - 1)];

    /// <summary>Copies a source state's frames into a generated state.</summary>
    public static GeneratedState Copy(IIconState source)
    {
        var frames = new Raster[source.Dirs * source.Frames];
        for (int d = 0; d < source.Dirs; d++)
        {
            for (int f = 0; f < source.Frames; f++)
                frames[d * source.Frames + f] = source.Get(d, f).Clone();
        }
        return new GeneratedState(source.Dirs, source.Frames, source.Width, source.Height, frames, source.Delays, source.Loop, source.Rewind);
    }

    /// <summary>A copy of the state with its frames tinted by <paramref name="rgba"/> (<c>icon.Blend(colour, ICON_MULTIPLY)</c>).</summary>
    public GeneratedState Tinted(uint rgba)
    {
        var frames = new Raster[_frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = _frames[i].Clone();
            frames[i].Multiply(rgba);
        }
        return new GeneratedState(Dirs, Frames, Width, Height, frames, Delays, Loop, Rewind);
    }

    /// <summary>A copy with the colour matrix applied to every frame (<c>icon.MapColors</c>).</summary>
    public GeneratedState Mapped(ColorMatrix matrix)
    {
        var frames = new Raster[_frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = _frames[i].Clone();
            frames[i].Map(matrix);
        }
        return new GeneratedState(Dirs, Frames, Width, Height, frames, Delays, Loop, Rewind);
    }

    /// <summary>
    /// <c>icon.Blend(layer, mode)</c> on a copy: each direction of this state takes the same direction of the layer (a layer with one
    /// direction or one frame serves them all), so the result keeps this state's directions and frames.
    /// </summary>
    public GeneratedState BlendedWith(IIconState layer, IconBlend mode)
    {
        var frames = new Raster[_frames.Length];
        for (int d = 0; d < Dirs; d++)
        {
            int layerDir = ByondDir.Index(ByondDir.FromIndex(d), layer.Dirs);
            for (int f = 0; f < Frames; f++)
            {
                Raster result = _frames[d * Frames + f].Clone();
                result.Blend(layer.Get(layerDir, Math.Min(f, layer.Frames - 1)), mode);
                frames[d * Frames + f] = result;
            }
        }
        return new GeneratedState(Dirs, Frames, Width, Height, frames, Delays, Loop, Rewind);
    }
}

/// <summary>
/// One GAGS config drawn with its colours: tg's <c>GenerateBundle</c>, one state at a time on first use. Layers of an icon state
/// are generated in order and joined with their blend modes; nested groups are generated first and joined with the blend of their
/// first layer; a <c>reference</c> layer is another config generated with this layer's colours; a <c>color_matrix</c> layer maps
/// the colours of what is below. A layer whose icon, state or config is missing is skipped and reported to the library.
/// </summary>
public sealed class GagsIcon : IIcon
{
    private readonly GagsConfig _config;
    private readonly IconLibrary _library;
    private readonly List<uint> _colors;
    private readonly Dictionary<string, IIconState> _states = new(StringComparer.Ordinal);

    public GagsIcon(GagsConfig config, IconLibrary library, List<uint> colors)
    {
        _config = config;
        _library = library;
        _colors = colors;
    }

    public bool TryGetState(string name, out IIconState state)
    {
        name ??= string.Empty;
        if (_states.TryGetValue(name, out state))
            return state != null;
        state = null;
        if (_config.States.TryGetValue(name, out List<GagsNode> layers))
            state = GenerateGroup(layers, _colors, null, 0);
        _states[name] = state;
        return state != null;
    }

    internal IIconState GenerateState(string name)
        => TryGetState(name, out IIconState state) ? state : null;

    private GeneratedState GenerateGroup(List<GagsNode> nodes, List<uint> colors, GeneratedState external, int depth)
    {
        if (depth > 16)
        {
            _library.Report("GAGS config " + _config.Type + " nests too deep");
            return null;
        }
        GeneratedState result = null;
        foreach (GagsNode node in nodes)
        {
            GeneratedState layer;
            IconBlend blend;
            if (node is GagsGroup group)
            {
                layer = GenerateGroup(group.Items, colors, result ?? external, depth + 1);
                blend = FirstBlend(group);
            }
            else
            {
                var leaf = (GagsLayer)node;
                layer = GenerateLayer(leaf, colors, result ?? external, depth);
                blend = leaf.Blend;
            }
            if (layer == null)
                continue;
            result = result == null ? layer : result.BlendedWith(layer, blend);
        }
        return result;
    }

    private static IconBlend FirstBlend(GagsGroup group)
    {
        GagsNode first = group.Items.Count > 0 ? group.Items[0] : null;
        while (first is GagsGroup inner)
            first = inner.Items.Count > 0 ? inner.Items[0] : null;
        return first is GagsLayer layer ? layer.Blend : IconBlend.Overlay;
    }

    private GeneratedState GenerateLayer(GagsLayer layer, List<uint> colors, GeneratedState below, int depth)
    {
        var processed = new List<uint>();
        foreach (GagsColorRef color in layer.Colors)
        {
            if (!color.IsIndex)
                processed.Add(color.Literal);
            else if (color.Index <= colors.Count)
                processed.Add(colors[color.Index - 1]);
            else
            {
                // tg refuses a config given too few colours; a few of its own configs (the gold mace) do that through a reference. The layer stays untinted.
                _library.Report(_config.Type + " expects colour " + color.Index + " but got " + colors.Count);
                processed.Add(0xFFFFFFFF);
            }
        }

        switch (layer.Kind)
        {
            case "icon_state":
            {
                if (!_library.TryGetIcon(_config.IconFile, out IIcon icon) || !icon.TryGetState(layer.IconState, out IIconState source))
                {
                    _library.Report(_config.Type + ": icon state '" + layer.IconState + "' of " + _config.IconFile + " is missing");
                    return null;
                }
                GeneratedState generated = GeneratedState.Copy(source);
                return processed.Count > 0 ? generated.Tinted(processed[0]) : generated;
            }
            case "reference":
            {
                if (!_library.TryGetGags(layer.ReferenceType, processed, out GagsIcon reference))
                {
                    _library.Report(_config.Type + ": the referenced config " + layer.ReferenceType + " is missing");
                    return null;
                }
                IIconState state = reference.GenerateState(layer.IconState);
                if (state == null)
                {
                    _library.Report(_config.Type + ": " + layer.ReferenceType + " has no state '" + layer.IconState + "'");
                    return null;
                }
                return state as GeneratedState ?? GeneratedState.Copy(state);
            }
            case "color_matrix":
                return below?.Mapped(ColorMatrix.FromList(layer.Matrix));
            default:
                _library.Report(_config.Type + ": unknown layer type '" + layer.Kind + "'");
                return null;
        }
    }
}
