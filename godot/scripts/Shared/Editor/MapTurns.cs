using System.Globalization;
using System.Linq;
using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The box a turned selection leaves and the one it takes (docs/ai/specs/map-editor.md 5.14): the tiles turn a quarter about the middle of the box
/// that holds them. A box whose sides are an odd and an even number of tiles has no tile in its middle: it moves half a tile one way on one turn
/// and back on the next, so four turns bring every tile home.
/// </summary>
public readonly record struct TurnBox(int X0, int Y0, int Width, int Height, int ToX0, int ToY0, bool Clockwise)
{
    /// <summary>The box of the tiles, turned a quarter.</summary>
    public static TurnBox Of(IEnumerable<(int X, int Y)> cells, bool clockwise)
    {
        ArgumentNullException.ThrowIfNull(cells);
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach ((int x, int y) in cells)
            (x0, y0, x1, y1) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y));
        if (x1 < x0)
            return default;
        int width = x1 - x0 + 1, height = y1 - y0 + 1;
        // the middle in half tiles; an even side by an odd one gives the half-tile step
        int cx = 2 * x0 + width - 1, cy = 2 * y0 + height - 1;
        if (width % 2 == 0 && height % 2 != 0)
            (cx, cy) = (cx - 1, cy + 1);
        else if (width % 2 != 0 && height % 2 == 0)
            (cx, cy) = (cx + 1, cy - 1);
        return new TurnBox(x0, y0, width, height, (cx - (height - 1)) / 2, (cy - (width - 1)) / 2, clockwise);
    }

    /// <summary>Where a tile of the box goes. Clockwise, seen from above with north up: what was at the box's north side is at its east side.</summary>
    public (int X, int Y) Spot(int x, int y)
    {
        int dx = x - X0, dy = y - Y0;
        return Clockwise ? (ToX0 + dy, ToY0 + (Width - 1 - dx)) : (ToX0 + (Height - 1 - dy), ToY0 + dx);
    }
}

/// <summary>The map editor's quarter turns of a thing and of a selection of tiles (docs/ai/specs/map-editor.md 5.14).</summary>
public static class MapTurns
{
    // BYOND's dirs clockwise from north, the diagonals between
    private static readonly int[] Ring = { 1, 5, 4, 6, 2, 10, 8, 9 };
    private const string Helper = "/directional/";

    /// <summary>A BYOND dir turned a quarter (north 1, south 2, east 4, west 8, and the diagonals); what is no dir counts as south, tg's own.</summary>
    public static int Dir(int dir, bool clockwise)
    {
        int at = Array.IndexOf(Ring, dir);
        return Ring[((at < 0 ? 4 : at) + (clockwise ? 2 : 6)) % 8];
    }

    /// <summary>The way a member faces as the map says it: its <c>dir</c>, or its type's.</summary>
    public static int DirOf(MapMember member)
    {
        string literal = MapEdits.VarOf(member, "dir") ?? TypeVars.Of(member.Path).FirstOrDefault(v => v.Name == "dir")?.Default;
        return int.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dir) && dir > 0 ? dir : 2;
    }

    /// <summary>
    /// A member turned a quarter. A thing hung on a wall of its tile by its type (tg's <c>/directional/north</c> family) becomes the type of the next
    /// wall. Any other thing gets its <c>dir</c> written, turned from <paramref name="facing"/>: the way it is seen to face (the game turns a
    /// closet away from the wall behind it, and a random spawner's pick shows another way than the map says), or 0 for what the map says. A turf
    /// turns only a <c>dir</c> it has of its own, an area nothing. Pixel offsets turn with the member.
    /// </summary>
    public static MapMember Turned(MapMember member, bool clockwise, int facing = 0)
    {
        if (MapEdits.IsArea(member.Path))
            return member;
        MapMember turned = member;
        if (Pixels(member, "pixel_x") is var px && Pixels(member, "pixel_y") is var py && (px != null || py != null))
        {
            (int x, int y) = (px ?? 0, py ?? 0);
            (x, y) = clockwise ? (y, -x) : (-y, x);
            turned = MapEdits.WithVar(turned, "pixel_x", x == 0 ? null : x.ToString(CultureInfo.InvariantCulture));
            turned = MapEdits.WithVar(turned, "pixel_y", y == 0 ? null : y.ToString(CultureInfo.InvariantCulture));
        }
        bool said = MapEdits.VarOf(member, "dir") != null;
        int at = member.Path.LastIndexOf(Helper, StringComparison.Ordinal);
        int side = at < 0 ? 0 : member.Path.Substring(at + Helper.Length) switch { "north" => 1, "south" => 2, "east" => 4, "west" => 8, _ => 0 };
        if (side != 0 && MapEdits.Directional(member.Path, (byte)Dir(side, clockwise)) is { } hung)
        {
            turned = new MapMember(hung, turned.Overrides);
            return said ? MapEdits.WithVar(turned, "dir", Dir(DirOf(member), clockwise).ToString(CultureInfo.InvariantCulture)) : turned;
        }
        if (MapEdits.IsTurf(member.Path) && !said)
            return turned;
        int now = said || facing <= 0 ? DirOf(member) : facing;
        return MapEdits.WithVar(turned, "dir", Dir(now, clockwise).ToString(CultureInfo.InvariantCulture));
    }

    private static int? Pixels(MapMember member, string name)
        => MapEdits.VarOf(member, name) is { } literal && double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out double pixels) ? (int)Math.Round(pixels) : null;

    /// <summary>
    /// The tiles that change when tiles of a level turn a quarter about the middle of the box that holds them (a later entry of the list is the
    /// one that holds for a tile named twice). Tiles that differ in their turf or their area (a room with its walls) turn whole: the tiles they
    /// leave become the map's default tile and each takes its new place, its members turned. Tiles of one floor and one area (furniture on a
    /// floor) keep their floor: only what stands on them turns, and takes the place of what stands where it goes.
    /// <paramref name="facing"/> says the way a member is seen to face (x, y, the member's place in its tile), or 0. Null when the turned box
    /// would reach past the map: nothing of a selection is lost to a turn.
    /// </summary>
    public static List<(int X, int Y, int Z, MapModel Model)> Turn(MapDocument map, IReadOnlyCollection<(int X, int Y)> cells, int z, bool clockwise, Func<int, int, int, int> facing = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(cells);
        var tiles = new List<(int, int, int, MapModel)>();
        if (cells.Count == 0)
            return tiles;
        TurnBox box = TurnBox.Of(cells, clockwise);
        if (!map.Contains(box.X0, box.Y0, z) || !map.Contains(box.X0 + box.Width - 1, box.Y0 + box.Height - 1, z)
            || !map.Contains(box.ToX0, box.ToY0, z) || !map.Contains(box.ToX0 + box.Height - 1, box.ToY0 + box.Width - 1, z))
            return null;
        static IEnumerable<MapMember> Ground(MapModel tile) => tile.Members.Where(m => !MapEdits.IsThing(m.Path));
        string ground = MapDocument.TextOf(Ground(map[cells.First().X, cells.First().Y, z]).ToList());
        bool oneFloor = cells.All(c => MapDocument.TextOf(Ground(map[c.X, c.Y, z]).ToList()) == ground);
        var placed = new List<(int, int, int, MapModel)>();
        foreach ((int x, int y) in cells)
        {
            MapModel tile = map[x, y, z];
            var members = new List<MapMember>(tile.Members.Count);
            for (int i = 0; i < tile.Members.Count; i++)
            {
                if (!oneFloor || MapEdits.IsThing(tile.Members[i].Path))
                    members.Add(Turned(tile.Members[i], clockwise, facing?.Invoke(x, y, i) ?? 0));
            }
            (int tx, int ty) = box.Spot(x, y);
            tiles.Add((x, y, z, oneFloor ? MapEdits.ClearThings(map, tile) : map.Empty(z)));
            // (on one floor a tile the things go to keeps its own turf and area, whatever they are)
            placed.Add((tx, ty, z, map.Model(oneFloor ? members.Concat(Ground(map[tx, ty, z])).ToList() : members)));
        }
        tiles.AddRange(placed);
        return tiles;
    }
}
