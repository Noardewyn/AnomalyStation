using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// Owner: the contract (these types, signatures and the meaning in the comments) is the M3 contracts step's and frozen
// (docs/ai/specs/m3-ownership.md); M3 P2 implements it. The engine part of BYOND's and tg's map reading: it replaces exactly
// /datum/parsed_map's DM text parsing (New's grid parsing, tgm_build_cache, dmm_build_cache, readlist, parse_constant;
// reader.dm:178-296, 687-890, 990-1102) for our format (docs/ai/specs/map-format.md). Pure: no World, no tg code, no statics.

/// <summary>What a map value literal is (docs/ai/specs/map-format.md 4.4, tg's <c>parse_constant</c> order).</summary>
public enum MapValueKind : byte
{
    /// <summary>The empty text (tg returns <c>""</c>).</summary>
    Empty = 0,
    Number = 1,
    Text = 2,
    List = 3,
    Path = 4,
    Resource = 5,
    Null = 6,

    /// <summary>tg's fallback: the literal as text (the converter refuses it; the reader keeps tg's meaning).</summary>
    Fallback = 7,
}

/// <summary>
/// One var override's value as the map file holds it, parsed by tg's <c>parse_constant</c> rules into a small tree; turned
/// into a DM value only inside the world (<see cref="ToDm"/>), once per atom, so lists are never shared between atoms.
/// </summary>
public sealed class MapValue
{
    /// <summary>What it is.</summary>
    public MapValueKind Kind { get; init; }

    /// <summary>The number (<see cref="MapValueKind.Number"/>), as BYOND's <c>text2num</c> reads it.</summary>
    public float Number { get; init; }

    /// <summary>The text of a <see cref="MapValueKind.Text"/> (between the quotes, not unescaped, as tg), a <see cref="MapValueKind.Path"/>, a <see cref="MapValueKind.Resource"/> (between the quotes) or a <see cref="MapValueKind.Fallback"/>.</summary>
    public string Text { get; init; }

    /// <summary>The items of a <see cref="MapValueKind.List"/>, in order: a plain item has a null key; <c>key = value</c> an association.</summary>
    public IReadOnlyList<(MapValue Key, MapValue Value)> Items { get; init; }

    /// <summary>The literal text as the file holds it (trimmed), for errors and the converter's round trip.</summary>
    public string Literal { get; init; }

    /// <summary>Parses one value literal by docs/ai/specs/map-format.md 4.4. Throws <see cref="MapTextException"/> on a malformed list.</summary>
    public static MapValue Parse(string literal) => MapTextReader.ParseValue(literal);

    /// <summary>
    /// The DM value, inside a world (<c>World.Current</c>): a <c>float</c>, a <c>string</c>, a new <see cref="DmList"/> (its
    /// items converted in order), the <c>Type</c> of the path's class (<c>text2path</c>; an unknown path is null, as tg's
    /// <c>text2path</c>), a resource (<c>Dm.resource</c>), or null.
    /// </summary>
    public object ToDm()
    {
        switch (Kind)
        {
            case MapValueKind.Empty:
                return "";
            case MapValueKind.Number:
                return Number;
            case MapValueKind.Text:
            case MapValueKind.Fallback:
                return Text;
            case MapValueKind.Path:
                return Dm.text2path(Text);
            case MapValueKind.Resource:
                return Dm.resource(Text);
            case MapValueKind.Null:
                return null;
            case MapValueKind.List:
                // reader.dm:1056-1060 (readlist): a plain item is `. += list(left_constant)` (a list item stays one item),
                // an association `.[left_constant] = parse_constant(trim_right)`.
                var list = new DmList();
                foreach ((MapValue key, MapValue value) in Items)
                {
                    if (key == null) list.Add(Dm.list(value.ToDm()));
                    else list[key.ToDm()] = value.ToDm();
                }
                return list;
            default:
                throw new InvalidOperationException("unknown map value kind " + Kind);
        }
    }
}

/// <summary>One var override of a member: <c>dir = 4</c>.</summary>
public readonly record struct MapOverride(string Var, MapValue Value);

/// <summary>One atom of a tile: its type path and its overrides in the file's order.</summary>
public readonly record struct MapMember(string Path, IReadOnlyList<MapOverride> Overrides);

/// <summary>
/// One distinct member list (tg's model: the atoms of a tile in tg's order, movables, then the turf or turfs, then the area
/// last). Tiles with equal member lists share one model, as tg's <c>modelCache</c> does for one key.
/// </summary>
public sealed class MapModel
{
    /// <summary>The members, in tg's order (the area last).</summary>
    public IReadOnlyList<MapMember> Members { get; init; }

    /// <summary>The model's line text after <c>x,y: </c> (the key the reader interns models by).</summary>
    public string Text { get; init; }
}

/// <summary>A parsed <c>.as3dmap</c> file (docs/ai/specs/map-format.md 4): the header and every tile's model.</summary>
public sealed class ParsedMapText
{
    /// <summary>The file's name, for errors.</summary>
    public string SourceName { get; init; }

    /// <summary>The <c>source</c> header line's path and tg commit (informative).</summary>
    public string Source { get; init; }

    /// <summary><c>size</c>: the width (tiles, x).</summary>
    public int Width { get; init; }

    /// <summary><c>size</c>: the height (y).</summary>
    public int Height { get; init; }

    /// <summary><c>size</c>: the levels (z).</summary>
    public int Levels { get; init; }

    /// <summary>The <c>default</c> model (every tile without a line).</summary>
    public MapModel Default { get; init; }

    /// <summary>Every distinct model, the default first, then in order of first appearance in the file.</summary>
    public IReadOnlyList<MapModel> Models { get; init; }

    /// <summary>The model of the tile (x, y, z) (from 1; the default for a tile without a line). Throws outside <c>size</c>.</summary>
    public MapModel ModelAt(int x, int y, int z)
    {
        if (x < 1 || y < 1 || z < 1 || x > Width || y > Height || z > Levels)
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}, {z}) is outside the map's size {Width} x {Height} x {Levels}");
        return Models[dm_tiles[((z - 1) * Height + (y - 1)) * Width + (x - 1)]];
    }

    /// <summary>The tiles that have a line, in the file's order (chunks by z, y, x; then y decreasing, x increasing).</summary>
    public IEnumerable<(int X, int Y, int Z, MapModel Model)> Lines => dm_lines;

    // M3 P2 (the reader): the index into Models of every tile, x fastest, then y, then z; and the lines in file order.
    internal int[] dm_tiles;
    internal IReadOnlyList<(int X, int Y, int Z, MapModel Model)> dm_lines;
}

/// <summary>A malformed map file: the file, the line (from 1) and what is wrong.</summary>
public sealed class MapTextException : Exception
{
    public MapTextException(string sourceName, int line, string message)
        : base(sourceName + ":" + line + ": " + message)
    {
        SourceName = sourceName;
        Line = line;
    }

    /// <summary>The file's name.</summary>
    public string SourceName { get; }

    /// <summary>The line, from 1.</summary>
    public int Line { get; }
}

/// <summary>The engine map reader (docs/ai/specs/map-format.md 4, 5).</summary>
public static class MapText
{
    /// <summary>The format version this reader reads (the <c>anomaly-map</c> header line).</summary>
    public const int FormatVersion = 1;

    /// <summary>The chunk side of format version 1.</summary>
    public const int ChunkSize = 32;

    /// <summary>The extension of a converted map.</summary>
    public const string Extension = ".as3dmap";

    /// <summary>Parses a whole file. Throws <see cref="MapTextException"/> for anything section 4 does not allow (the reader validates the syntax; the types are checked by the converter and at load).</summary>
    public static ParsedMapText Parse(string text, string sourceName) => MapTextReader.Parse(text, sourceName);
}
