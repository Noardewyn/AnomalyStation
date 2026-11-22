using System.Linq;
using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>Something a check found on a map (docs/ai/specs/map-editor.md 5.13): the tile, the member it is about (-1: the tile), the rule and what to do.</summary>
public readonly record struct MapProblem(int X, int Y, int Z, int Member, string Rule, string Text);

/// <summary>
/// The map editor's checks: tg's own map lint rules that are about one tile (tools/maplint/lints of the tg clone: what must not share a tile, what
/// a thing needs beside it on its tile), and a few of the editor's own that a mapper of a new map meets first.
/// </summary>
public static class MapChecks
{
    // tg's lints of things that must not share a tile. Exact: the type itself and none under it. Identical: only two members that are written alike.
    private sealed record Stack(string Rule, string Subject, bool Exact, string With, bool Identical, string Text);

    private static readonly Stack[] Stacks =
    {
        new("multiple_airlocks", "/obj/machinery/door/airlock", false, "/obj/machinery/door/airlock", false, "two airlocks on one tile"),
        new("multiple_firelocks", "/obj/machinery/door/firedoor", true, "/obj/machinery/door/firedoor", false, "two firelocks on one tile"),
        new("multiple_chairs", "/obj/structure/chair", false, "/obj/structure/chair", false, "two chairs on one tile"),
        new("multiple_closets", "/obj/structure/closet", false, "/obj/structure/closet", false, "two closets on one tile"),
        new("multiple_girders", "/obj/structure/girder", false, "/obj/structure/girder", false, "two girders on one tile"),
        new("multiple_grilles", "/obj/structure/grille", false, "/obj/structure/grille", false, "two grilles on one tile"),
        new("multiple_lattice", "/obj/structure/lattice", false, "/obj/structure/lattice", false, "two lattices on one tile"),
        new("multiple_stairs", "/obj/structure/stairs", false, "/obj/structure/stairs", false, "two stairs on one tile"),
        new("multiple_tables", "/obj/structure/table", false, "/obj/structure/table", false, "two tables on one tile"),
        new("multiple_unary_components", "/obj/machinery/atmospherics/components/unary", false, "/obj/machinery/atmospherics/components/unary", false, "two vents, scrubbers or the like on one tile"),
        new("multiple_machinery", "/obj/machinery", false, "/obj/machinery", true, "the same machine twice on one tile"),
        new("multiple_windows", "/obj/structure/window", false, "/obj/structure/window", true, "the same window twice on one tile"),
        new("identical_cables", "/obj/structure/cable", false, "/obj/structure/cable", true, "the same cable twice on one tile"),
        new("identical_pipes", "/obj/machinery/atmospherics/pipe", false, "/obj/machinery/atmospherics/pipe", true, "the same pipe twice on one tile"),
        new("identical_disposal_pipes", "/obj/structure/disposalpipe", false, "/obj/structure/disposalpipe", true, "the same disposal pipe twice on one tile"),
        new("wall_stacking", "/turf/closed", false, "/obj/machinery/door/airlock", false, "an airlock in a wall: give the tile a floor"),
        new("wall_stacking", "/turf/closed", false, "/obj/structure/lattice", false, "a lattice in a wall"),
        new("wall_stacking", "/turf/closed", false, "/obj/structure/window", false, "a window in a wall"),
        new("window_spawner", "/obj/effect/spawner/structure/window", false, "/turf/closed", false, "a window spawner in a wall"),
        new("grille_stacking", "/obj/structure/grille", true, "/obj/structure/cable", false, "a cable under a plain grille electrifies it: use the electrified grille's spawner if it is meant"),
    };

    /// <summary>
    /// Whether a player who joins a round of the map late has a point of the map's own to arrive at (Play as a new arrival, 5.7): a late-join landmark,
    /// or a dock that loads an arrivals shuttle, whose chairs are tg's late-join points.
    /// </summary>
    public static bool HasArrival(MapDocument map)
    {
        ArgumentNullException.ThrowIfNull(map);
        for (int z = 1; z <= map.Levels; z++)
        {
            for (int y = 1; y <= map.Height; y++)
            {
                for (int x = 1; x <= map.Width; x++)
                {
                    foreach (MapMember member in map[x, y, z].Members)
                    {
                        if (Under(member.Path, "/obj/effect/landmark/latejoin")
                            || (Under(member.Path, "/obj/docking_port/stationary") && (MapEdits.VarOf(member, "roundstart_template")?.Contains("/shuttle/arrival", StringComparison.Ordinal) ?? false)))
                            return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Checks a map: the first <paramref name="limit"/> problems in the order of the levels, the rows from the south and the tiles from the west, and
    /// how many there are in all.
    /// </summary>
    public static List<MapProblem> Run(MapDocument map, int limit, out int total)
    {
        ArgumentNullException.ThrowIfNull(map);
        var found = new List<MapProblem>();
        total = 0;
        void Add(MapProblem problem, ref int count)
        {
            count++;
            if (found.Count < limit)
                found.Add(problem);
        }

        // what a tile's own members say is the same for every tile with those members: each model is checked once
        var local = new Dictionary<MapModel, List<(int Member, string Rule, string Text)>>(ReferenceEqualityComparer.Instance);
        bool joins = false;
        int ports = 0;
        (int X, int Y, int Z) firstPort = default;
        for (int z = 1; z <= map.Levels; z++)
        {
            for (int y = 1; y <= map.Height; y++)
            {
                for (int x = 1; x <= map.Width; x++)
                {
                    MapModel tile = map[x, y, z];
                    if (!local.TryGetValue(tile, out List<(int, string, string)> problems))
                        local[tile] = problems = Local(tile);
                    foreach ((int member, string rule, string text) in problems)
                        Add(new MapProblem(x, y, z, member, rule, text), ref total);
                    for (int i = 0; i < tile.Members.Count; i++)
                    {
                        string path = tile.Members[i].Path;
                        joins |= Under(path, "/obj/effect/landmark/latejoin") || Under(path, "/obj/effect/landmark/start");
                        if (MapDocks.IsMobile(path) && ports++ == 0)
                            firstPort = (x, y, z);
                        // the editor's own: a level under another has that level's floor for its ceiling (D325); its map's word there lays a floor tile
                        // under that floor's plating (DV507), and says nothing else
                        if (z < map.Levels && MapEdits.IsTurf(path) && MapEdits.VarOf(tile.Members[i], "ceiling") != null)
                        {
                            if (MapEdits.LaidCeiling(tile) == null)
                                Add(new MapProblem(x, y, z, i, "ceiling_under_level", "a ceiling that is no floor tile is set under another level: there the floor of the level above is the ceiling, and the game leaves this out (the Tile panel's Ceiling, The game's rule, takes it off)"), ref total);
                            else if (MapEdits.UndersideOf(tile, map[x, y, z + 1]) != MapEdits.LaidCeiling(tile))
                                Add(new MapProblem(x, y, z, i, "ceiling_under_level", "a ceiling tile with no plating over it to lie under: the tile of the level above is open space or glass"), ref total);
                        }
                        // a thing hung on a side of its tile needs something to hang on there
                        if (Side(path) is { } side && World.PropKinds.IsWallMountType(path) && !Holds(map, x + side.Dx, y + side.Dy, z))
                            Add(new MapProblem(x, y, z, i, "wall_mount", "hangs on its " + side.Name + " side, where there is no wall and no window"), ref total);
                    }
                }
            }
        }
        if (ports > 1)
            Add(new MapProblem(firstPort.X, firstPort.Y, firstPort.Z, -1, "shuttle_ports", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"a shuttle has one docking port of its own: this map has {ports}")), ref total);
        // (a shuttle's map is joined at its station)
        if (!joins && ports == 0)
            Add(new MapProblem(1, 1, 1, -1, "no_arrivals", "no arrival point: place /obj/effect/landmark/latejoin where late players arrive, and /obj/effect/landmark/start/... for the jobs"), ref total);
        return found;
    }

    private static List<(int Member, string Rule, string Text)> Local(MapModel tile)
    {
        var problems = new List<(int, string, string)>();
        IReadOnlyList<MapMember> members = tile.Members;
        string[] texts = null;
        foreach (Stack rule in Stacks)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (!(rule.Exact ? members[i].Path == rule.Subject : Under(members[i].Path, rule.Subject)))
                    continue;
                for (int j = 0; j < members.Count; j++)
                {
                    // (a pair of the same family is told once, at its second member)
                    if (j == i || !(rule.Exact && rule.Subject == rule.With ? members[j].Path == rule.With : Under(members[j].Path, rule.With)) || (rule.Subject == rule.With && j < i))
                        continue;
                    if (rule.Identical)
                    {
                        texts ??= members.Select(m => MapDocument.TextOf(new[] { m })).ToArray();
                        if (texts[i] != texts[j])
                            continue;
                    }
                    problems.Add((j, rule.Rule, rule.Text));
                }
            }
        }
        string turf = MapEdits.TurfOf(tile), area = members.Count > 0 && MapEdits.IsArea(members[^1].Path) ? members[^1].Path : null;
        for (int i = 0; i < members.Count; i++)
        {
            // tg's disposals_without_trunks
            if (Under(members[i].Path, "/obj/machinery/disposal") && !members.Any(m => Under(m.Path, "/obj/structure/disposalpipe/trunk")))
                problems.Add((i, "disposals_without_trunks", "a disposal unit with no trunk under it"));
        }
        // the editor's own: a floor or a wall left in the area of space has no power and no air alarm, and tg counts it as the outside
        // (tg's own area of the hull's outside, /area/space/nearstation, is meant for what stands there)
        if (turf != null && area == "/area/space" && !Under(turf, "/turf/open/space") && !Under(turf, "/turf/open/openspace") && !Under(turf, "/turf/template_noop")
            && (Under(turf, "/turf/open/floor") || Under(turf, "/turf/closed/wall")))
            problems.Add((members.Count - 1, "space_area", "a floor or a wall in the area of space: give its room an area (the palette's Areas, the zone shape)"));
        return problems;
    }

    // The side a directional type hangs on (tg's MAPPING_DIRECTIONAL_HELPERS: .../directional/north), or null.
    private static (string Name, int Dx, int Dy)? Side(string path)
    {
        const string helper = "/directional/";
        int at = path.LastIndexOf(helper, StringComparison.Ordinal);
        if (at < 0)
            return null;
        return path.Substring(at + helper.Length) switch
        {
            "north" => ("north", 0, 1),
            "south" => ("south", 0, -1),
            "east" => ("east", 1, 0),
            "west" => ("west", -1, 0),
            _ => null,
        };
    }

    // Whether a tile holds what hangs on it: a wall, or a window that fills it (its spawner too).
    private static bool Holds(MapDocument map, int x, int y, int z)
    {
        if (!map.Contains(x, y, z))
            return false;
        MapModel tile = map[x, y, z];
        return tile.Members.Any(m => Under(m.Path, "/turf/closed") || Under(m.Path, "/obj/effect/spawner/structure/window")
            || (Under(m.Path, "/obj/structure/window") && m.Path.Contains("fulltile", StringComparison.Ordinal)));
    }

    private static bool Under(string path, string root)
        => path != null && path.StartsWith(root, StringComparison.Ordinal) && (path.Length == root.Length || path[root.Length] == '/');
}
