using System.Globalization;
using System.Linq;
using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>A docking port of a map (docs/ai/specs/map-editor.md 5.11) with the rectangle of tiles its shuttle takes.</summary>
/// <param name="Member">The port's place among its tile's members.</param>
/// <param name="Id">tg's <c>shuttle_id</c> of the port (<c>cargo_home</c>, <c>emergency</c>); null when it has none.</param>
/// <param name="Mobile">A shuttle's own port; false: a dock, where a shuttle comes to stand.</param>
public readonly record struct MapDock(int X, int Y, int Z, int Member, string Path, string Id, bool Mobile, int Dir, DockSize Size, int X0, int Y0, int X1, int Y1);

/// <summary>
/// A docking port's four numbers, as tg has them (shuttle.dm): the width and the height of its rectangle, seen with the port facing north, and how far
/// the port stands from that rectangle's left and bottom edges.
/// </summary>
public readonly record struct DockSize(int Width, int Height, int Dwidth, int Dheight);

/// <summary>
/// A shuttle stood at a dock of a map (docs/ai/specs/map-editor.md 5.11), as the round's start leaves it: its own port on the dock, the shuttle
/// turned to face as the dock does (tg's <c>action_load</c> and <c>initiate_docking</c>).
/// </summary>
/// <param name="Tiles">The map's tiles with the shuttle on them.</param>
/// <param name="Box">The rectangle the shuttle takes on the map (tg's <c>return_coords</c> of its port at the dock), the lower left corner first.</param>
/// <param name="OffMap">How many of the shuttle's tiles would stand past the map's edge.</param>
/// <param name="Crushed">How many of its tiles stand on a wall or on things of the map: docking destroys those (lattice and effects are not counted).</param>
/// <param name="FirstCrushed">One of them.</param>
public sealed record DockedShuttle(IReadOnlyList<(int X, int Y, int Z, MapModel Tile)> Tiles, (int X0, int Y0, int X1, int Y1) Box, int OffMap, int Crushed, (int X, int Y)? FirstCrushed);

/// <summary>
/// The docking ports of a map and the sizes of shuttles: where a docked shuttle stands, and whether a shuttle fits a dock. The numbers are tg's own
/// (<c>return_coords</c>, <c>calculate_docking_port_information</c>, <c>canDock</c>).
/// </summary>
public static class MapDocks
{
    private const int North = 1, South = 2, East = 4, West = 8;

    public static bool IsPort(string path) => Under(path, "/obj/docking_port");

    public static bool IsMobile(string path) => Under(path, "/obj/docking_port/mobile");

    private static bool Under(string path, string root)
        => path != null && path.StartsWith(root, StringComparison.Ordinal) && (path.Length == root.Length || path[root.Length] == '/');

    /// <summary>
    /// Every docking port of a map with its rectangle. A dock's is by its own vars (<c>width</c>, <c>height</c>, <c>dwidth</c>, <c>dheight</c>, <c>dir</c>);
    /// a shuttle's own port's is the shuttle's map itself: tg takes a shuttle's size from its template, which is its whole file.
    /// </summary>
    public static List<MapDock> Find(MapDocument map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var docks = new List<MapDock>();
        // tiles with the same members share a model: each is read once
        var ports = new Dictionary<MapModel, int[]>(ReferenceEqualityComparer.Instance);
        for (int z = 1; z <= map.Levels; z++)
        {
            for (int y = 1; y <= map.Height; y++)
            {
                for (int x = 1; x <= map.Width; x++)
                {
                    MapModel tile = map[x, y, z];
                    if (!ports.TryGetValue(tile, out int[] members))
                        ports[tile] = members = Enumerable.Range(0, tile.Members.Count).Where(i => IsPort(tile.Members[i].Path)).ToArray();
                    foreach (int member in members)
                    {
                        MapMember port = tile.Members[member];
                        bool mobile = IsMobile(port.Path);
                        int dir = Number(port, "dir", South);
                        DockSize size = mobile ? ShuttleSize(map.Width, map.Height, x, y, dir)
                            : new DockSize(Number(port, "width", 0), Number(port, "height", 0), Number(port, "dwidth", 0), Number(port, "dheight", 0));
                        (int x0, int y0, int x1, int y1) = Coords(x, y, dir, size);
                        docks.Add(new MapDock(x, y, z, member, port.Path, Text(port, "shuttle_id"), mobile, dir, size, x0, y0, x1, y1));
                    }
                }
            }
        }
        return docks;
    }

    /// <summary>
    /// The size a shuttle's map gives its port (tg's <c>calculate_docking_port_information</c> with a template): the map's own width and height, turned
    /// when the port faces east or west, and the port's place in it.
    /// </summary>
    public static DockSize ShuttleSize(int mapWidth, int mapHeight, int portX, int portY, int dir)
    {
        (int width, int height) = dir is East or West ? (mapHeight, mapWidth) : (mapWidth, mapHeight);
        (int dwidth, int dheight) = dir switch
        {
            North => (portX - 1, portY - 1),
            East => (mapHeight - portY, portX - 1),
            West => (portY - 1, mapWidth - portX),
            _ => (mapWidth - portX, mapHeight - portY),
        };
        return new DockSize(width, height, dwidth, dheight);
    }

    /// <summary>The corners of a port's rectangle on the map (tg's <c>return_coords</c>), the lower left one first.</summary>
    public static (int X0, int Y0, int X1, int Y1) Coords(int x, int y, int dir, DockSize size)
    {
        (int cos, int sin) = dir switch { West => (0, 1), South => (-1, 0), East => (0, -1), _ => (1, 0) };
        int ax = x + -size.Dwidth * cos - -size.Dheight * sin, ay = y + -size.Dwidth * sin + -size.Dheight * cos;
        int bx = x + (-size.Dwidth + size.Width - 1) * cos - (-size.Dheight + size.Height - 1) * sin;
        int by = y + (-size.Dwidth + size.Width - 1) * sin + (-size.Dheight + size.Height - 1) * cos;
        return (Math.Min(ax, bx), Math.Min(ay, by), Math.Max(ax, bx), Math.Max(ay, by));
    }

    /// <summary>The size of the shuttle a map is, and how many ports of its own it has: a shuttle has one. Null when it has none.</summary>
    public static (DockSize Size, int Ports)? ShuttleOf(MapDocument shuttle)
    {
        List<MapDock> own = Find(shuttle).Where(dock => dock.Mobile).ToList();
        return own.Count == 0 ? null : (own[0].Size, own.Count);
    }

    private const string NoTurf = "/turf/template_noop", NoArea = "/area/template_noop";

    // What a mapper has under a dock with no harm: the ports themselves, tg's effects (landmarks, helpers, decals: docking leaves abstract ones be)
    // and the lattice of the space about a station, which tg's own stations have under their docks and lose to the first shuttle.
    private static bool Spared(string path) => IsPort(path) || Under(path, "/obj/effect") || Under(path, "/obj/structure/lattice");

    /// <summary>
    /// A shuttle's map stood at a dock of <paramref name="map"/>; null when the shuttle's map has no port of its own (the first one is taken of
    /// several). The shuttle's port goes onto the dock's tile and the shuttle turns about it until it faces as the dock does, its things turned
    /// with it. A tile of the shuttle's map that is no part of it (tg's <c>template_noop</c> turf and area, with nothing on it) takes no tile of
    /// the map. Where the shuttle has a turf, the map's tile gets it with the shuttle's things, and keeps only its own docking ports: what else
    /// stood there is what docking destroys. Where the shuttle has things and no turf, they stand on the map's tile as it is.
    /// </summary>
    public static DockedShuttle Docked(MapDocument map, MapDock dock, MapDocument shuttle)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(shuttle);
        List<MapDock> own = Find(shuttle).Where(port => port.Mobile).ToList();
        if (own.Count == 0)
            return null;
        MapDock port = own[0];
        static int Clock(int dir) => dir switch { North => 0, East => 1, West => 3, _ => 2 };
        int turns = (Clock(dock.Dir) - Clock(port.Dir) + 4) % 4; // quarter turns clockwise
        var tiles = new List<(int, int, int, MapModel)>();
        int offMap = 0, crushed = 0;
        (int, int)? firstCrushed = null;
        for (int sy = 1; sy <= shuttle.Height; sy++)
        {
            for (int sx = 1; sx <= shuttle.Width; sx++)
            {
                MapModel from = shuttle[sx, sy, port.Z];
                List<MapMember> things = from.Members.Where(m => MapEdits.IsThing(m.Path) && !IsPort(m.Path)).ToList();
                // (a member with no path: the shuttle has no turf, or no area, of its own on this tile)
                MapMember turf = from.Members.LastOrDefault(m => MapEdits.IsTurf(m.Path) && m.Path != NoTurf);
                MapMember area = from.Members.LastOrDefault(m => MapEdits.IsArea(m.Path) && m.Path != NoArea);
                if (things.Count == 0 && turf.Path == null)
                    continue;
                (int dx, int dy) = (sx - port.X, sy - port.Y);
                for (int turn = 0; turn < turns; turn++)
                    (dx, dy) = (dy, -dx);
                (int x, int y) = (dock.X + dx, dock.Y + dy);
                if (!map.Contains(x, y, dock.Z))
                {
                    offMap++;
                    continue;
                }
                MapModel under = map[x, y, dock.Z];
                MapMember Turned(MapMember member)
                {
                    for (int turn = 0; turn < turns; turn++)
                        member = MapTurns.Turned(member, clockwise: true);
                    return member;
                }
                var members = things.Select(Turned).ToList();
                if (turf.Path == null)
                    members.AddRange(under.Members); // the shuttle's things on the map's tile as it is
                else
                {
                    if (TurfStyle.Classify(MapEdits.TurfOf(under)) == TurfKind.Closed || under.Members.Any(m => MapEdits.IsThing(m.Path) && !Spared(m.Path)))
                    {
                        crushed++;
                        firstCrushed ??= (x, y);
                    }
                    members.AddRange(under.Members.Where(m => IsPort(m.Path)));
                    members.Add(Turned(turf));
                    members.Add(area.Path != null ? area : under.Members.Last(m => MapEdits.IsArea(m.Path)));
                }
                tiles.Add((x, y, dock.Z, map.Model(members)));
            }
        }
        return new DockedShuttle(tiles, Coords(dock.X, dock.Y, dock.Dir, port.Size), offMap, crushed, firstCrushed);
    }

    /// <summary>Why a shuttle does not fit a dock, in tiles (tg's <c>canDock</c>); null when it fits.</summary>
    public static string Misfit(DockSize shuttle, DockSize dock)
    {
        var why = new List<string>();
        void Over(int by, string where)
        {
            if (by > 0)
                why.Add(string.Create(CultureInfo.InvariantCulture, $"{by} too {where}"));
        }
        Over(shuttle.Dwidth - dock.Dwidth, "wide on the port's left");
        Over(shuttle.Width - shuttle.Dwidth - (dock.Width - dock.Dwidth), "wide on the port's right");
        Over(shuttle.Dheight - dock.Dheight, "long behind the port");
        Over(shuttle.Height - shuttle.Dheight - (dock.Height - dock.Dheight), "long before the port");
        return why.Count == 0 ? null : string.Join(", ", why);
    }

    // A whole number of a port: the member's own var, else its type's, else the fallback.
    private static int Number(MapMember port, string name, int fallback)
    {
        string literal = MapEdits.VarOf(port, name) ?? TypeVars.Of(port.Path).FirstOrDefault(v => v.Name == name)?.Default;
        return literal != null && float.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? (int)value : fallback;
    }

    private static string Text(MapMember port, string name)
        => Unquoted(MapEdits.VarOf(port, name) ?? TypeVars.Of(port.Path).FirstOrDefault(v => v.Name == name)?.Default);

    private static string Unquoted(string literal) => literal is { Length: >= 2 } && literal[0] == '"' && literal[^1] == '"' ? literal.Substring(1, literal.Length - 2) : null;

    private const string ShuttlesPrefix = "_maps/shuttles/";

    /// <summary>
    /// The shuttle a dock loads at a round's start by its own <c>roundstart_template</c> (the var the map gives it, else its type's): the folder
    /// of its file under <c>shuttles</c> (tg's <c>prefix</c> of the template; empty for the folder itself), its port (<c>port_id</c>) and its id
    /// (<c>port_id</c>_<c>suffix</c>), the file's name. Null for a dock that names none, and for a template the game has not got.
    /// </summary>
    public static (string Folder, string Port, string Id)? TemplateOf(MapMember dock)
    {
        string template = MapEdits.VarOf(dock, "roundstart_template") ?? TypeVars.Of(dock.Path).FirstOrDefault(v => v.Name == "roundstart_template")?.Default;
        if (template == null || !template.StartsWith('/'))
            return null;
        IReadOnlyList<TypeVar> vars = TypeVars.Of(template);
        string Of(string name) => Unquoted(vars.FirstOrDefault(v => v.Name == name)?.Default);
        string port = Of("port_id"), suffix = Of("suffix"), prefix = Of("prefix");
        if (port == null || suffix == null || prefix == null || !prefix.StartsWith(ShuttlesPrefix, StringComparison.Ordinal))
            return null;
        return (prefix.Substring(ShuttlesPrefix.Length).Trim('/'), port, port + "_" + suffix);
    }
}
