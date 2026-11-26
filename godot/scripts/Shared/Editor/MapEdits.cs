using System.Globalization;
using System.Linq;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The map editor's changes of one tile (docs/ai/specs/map-editor.md 5.2): each gives the tile's new model, with the members in the format's
/// order (the things, the turfs, the area: map-format.md 4.3). A change that changes nothing gives the model it was given.
/// </summary>
public static class MapEdits
{
    /// <summary>True for a turf type's path.</summary>
    public static bool IsTurf(string path) => Under(path, "/turf");

    /// <summary>True for an area type's path.</summary>
    public static bool IsArea(string path) => Under(path, "/area");

    /// <summary>True for the path of a thing: what stands on a tile (an object, a mob, an effect), neither its turf nor its area.</summary>
    public static bool IsThing(string path) => !IsTurf(path) && !IsArea(path);

    /// <summary>
    /// Whether a mob stands on a tile: its turf (the last of them, the one tg leaves) is an open one that is neither space nor a hole to fall into. The map
    /// editor's Play starts its player on the nearest such tile (docs/ai/specs/map-editor.md 5.7).
    /// </summary>
    public static bool Standable(MapModel tile)
    {
        string turf = tile.Members.LastOrDefault(member => IsTurf(member.Path)).Path;
        return turf != null && Under(turf, "/turf/open") && !Under(turf, "/turf/open/space") && !Under(turf, "/turf/open/openspace")
            && !Under(turf, "/turf/open/chasm") && !Under(turf, "/turf/open/lava");
    }

    private static bool Under(string path, string root)
        => path != null && path.StartsWith(root, StringComparison.Ordinal) && (path.Length == root.Length || path[root.Length] == '/');

    /// <summary>
    /// Places a member on a tile: a turf takes the place of the tile's turfs (and keeps the ceiling the map set over the tile), an area of its
    /// area, and a thing is one more thing, after the tile's things. A thing the tile has already (the same type with the same vars) is not
    /// placed again.
    /// </summary>
    public static MapModel Place(MapDocument map, MapModel tile, MapMember member)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        List<MapMember> members = tile.Members.ToList();
        if (IsThing(member.Path))
        {
            string text = MapDocument.TextOf(new[] { member });
            if (members.Any(m => IsThing(m.Path) && MapDocument.TextOf(new[] { m }) == text))
                return tile;
            int at = members.FindIndex(m => !IsThing(m.Path));
            members.Insert(at < 0 ? members.Count : at, member);
        }
        else if (IsTurf(member.Path))
        {
            // the ceiling the map set over the tile is the tile's, not its floor's (docs/ai/specs/ceilings.md): another turf keeps it, unless it says its own
            int old = TurfIndex(tile);
            if (old >= 0 && VarOf(tile.Members[old], "ceiling") is { } ceiling && VarOf(member, "ceiling") == null)
                member = WithVar(member, "ceiling", ceiling);
            members.RemoveAll(m => IsTurf(m.Path));
            int area = members.FindIndex(m => IsArea(m.Path));
            members.Insert(area < 0 ? members.Count : area, member);
        }
        else
        {
            members.RemoveAll(m => IsArea(m.Path));
            members.Add(member);
            // tg's open space of space is space whatever area it is in: a room's area painted over it means the room's own open space, which
            // holds the room's air (/turf/open/openspace); else the floors under it lose their air to it
            int turf = members.FindIndex(m => IsTurf(m.Path));
            if (turf >= 0 && members[turf].Path == SpaceOpenSpace && !Under(member.Path, "/area/space"))
                members[turf] = new MapMember(RoomOpenSpace, members[turf].Overrides);
        }
        return map.Model(members);
    }

    private const string SpaceOpenSpace = "/turf/open/space/openspace", RoomOpenSpace = "/turf/open/openspace";

    /// <summary>
    /// Takes a member of a tile away: a thing is removed; a turf or the area, which a tile cannot be without, gives its place to the one of
    /// the map's default tile.
    /// </summary>
    public static MapModel Remove(MapDocument map, MapModel tile, int index, MapModel empty = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        if (index < 0 || index >= tile.Members.Count)
            return tile;
        string path = tile.Members[index].Path;
        if (IsThing(path))
        {
            List<MapMember> members = tile.Members.ToList();
            members.RemoveAt(index);
            return map.Model(members);
        }
        foreach (MapMember fallback in (empty ?? map.Default).Members)
        {
            if (!IsThing(fallback.Path) && IsTurf(fallback.Path) == IsTurf(path))
                return Place(map, tile, fallback);
        }
        return tile;
    }

    /// <summary>
    /// Sets a var of a member of a tile to a literal of the format (<c>"text"</c>, a number, <c>null</c>, a type path, a <c>list(...)</c>); a
    /// null or empty literal takes the var's override away, so the member has its type's value again. A literal the format does not allow
    /// throws <see cref="MapTextException"/>.
    /// </summary>
    public static MapModel SetVar(MapDocument map, MapModel tile, int index, string name, string literal)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (index < 0 || index >= tile.Members.Count)
            return tile;
        MapMember member = tile.Members[index];
        if (string.IsNullOrEmpty(literal?.Trim()) && VarOf(member, name) == null)
            return tile;
        List<MapMember> members = tile.Members.ToList();
        members[index] = WithVar(member, name, literal);
        return map.Model(members);
    }

    /// <summary>A member with a var set to a literal of the format, or (a null or empty literal) with the var's override taken away.</summary>
    public static MapMember WithVar(MapMember member, string name, string literal)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        List<MapOverride> overrides = (member.Overrides ?? Array.Empty<MapOverride>()).ToList();
        int at = overrides.FindIndex(o => o.Var == name);
        literal = literal?.Trim();
        if (string.IsNullOrEmpty(literal))
        {
            if (at >= 0)
                overrides.RemoveAt(at);
        }
        else
        {
            var value = new MapOverride(name, MapValue.Parse(literal));
            if (at >= 0)
            {
                overrides[at] = value;
            }
            else
            {
                // tg's map files keep a member's vars in the order of their names: a new one goes to its place among them
                int before = overrides.FindIndex(o => string.CompareOrdinal(o.Var, name) > 0);
                overrides.Insert(before < 0 ? overrides.Count : before, value);
            }
        }
        return new MapMember(member.Path, overrides.Count > 0 ? overrides : null);
    }

    /// <summary>The literal of a var's override in a member, or null when the member keeps its type's value.</summary>
    public static string VarOf(MapMember member, string name)
    {
        foreach (MapOverride o in member.Overrides ?? Array.Empty<MapOverride>())
        {
            if (o.Var == name)
                return o.Value.Literal;
        }
        return null;
    }

    /// <summary>A tile with its things taken away: its turfs and its area stay.</summary>
    public static MapModel ClearThings(MapDocument map, MapModel tile)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        return tile.Members.Any(m => IsThing(m.Path)) ? map.Model(tile.Members.Where(m => !IsThing(m.Path)).ToList()) : tile;
    }

    /// <summary>
    /// A tile with its area taken away, which leaves it the area of the map's default tile: its turf and what stands there stay. It is what the
    /// erase tool takes while the palette shows the areas.
    /// </summary>
    public static MapModel Unareaed(MapDocument map, MapModel tile)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        MapMember own = tile.Members.LastOrDefault(m => IsArea(m.Path)), left = map.Default.Members.LastOrDefault(m => IsArea(m.Path));
        if (own.Path == null || left.Path == null || MapDocument.TextOf(new[] { own }) == MapDocument.TextOf(new[] { left }))
            return tile;
        return Place(map, tile, left);
    }

    /// <summary>A tile with one member in the place of another (found by its type and vars); the tile as it is when it has no such member.</summary>
    public static MapModel Replace(MapDocument map, MapModel tile, MapMember old, MapMember with)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        string text = MapDocument.TextOf(new[] { old });
        List<MapMember> members = tile.Members.ToList();
        int at = members.FindIndex(m => MapDocument.TextOf(new[] { m }) == text);
        if (at < 0)
            return tile;
        members[at] = with;
        return map.Model(members);
    }

    /// <summary>The floor a wall leaves when a thing takes its place (<see cref="ReplaceOn"/>: a window in a wall's place).</summary>
    public const string Plating = "/turf/open/floor/plating";

    /// <summary>The turf of a tile (the last of them, the one tg leaves), or null.</summary>
    public static string TurfOf(MapModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return tile.Members.LastOrDefault(member => IsTurf(member.Path)).Path;
    }

    /// <summary>
    /// The place tool's replace mode (docs/ai/specs/map-editor.md 5.8): what a tile becomes when what was clicked gives its place to the brush.
    /// <paramref name="target"/> is the clicked type's path. A thing's: every thing of that type on the tile gives its place (a thing brush stands
    /// where the first stood, a turf brush becomes the tile's turf and the things are taken away). A turf's: a tile whose turf it is takes the
    /// brush (a turf in the turf's place; a thing is placed, on plating where the turf was a wall). A tile that has not the target is given back
    /// as it is, so a shape replaces one kind of thing or of floor and leaves the rest of its tiles alone.
    /// </summary>
    public static MapModel ReplaceOn(MapDocument map, MapModel tile, string target, MapMember brush)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        if (string.IsNullOrEmpty(target))
            return tile;
        if (IsThing(target))
        {
            if (!tile.Members.Any(m => m.Path == target))
                return tile;
            bool thing = IsThing(brush.Path);
            string text = thing ? MapDocument.TextOf(new[] { brush }) : null;
            var members = new List<MapMember>();
            bool placed = tile.Members.Any(m => m.Path != target && IsThing(m.Path) && MapDocument.TextOf(new[] { m }) == text); // (the tile has the brush already)
            foreach (MapMember member in tile.Members)
            {
                if (member.Path != target)
                    members.Add(member);
                else if (thing && !placed)
                {
                    members.Add(brush);
                    placed = true;
                }
            }
            MapModel without = map.Model(members);
            return thing ? without : Place(map, without, brush);
        }
        if (TurfOf(tile) != target)
            return tile;
        if (IsThing(brush.Path) && Under(target, "/turf/closed"))
            tile = Place(map, tile, new MapMember(Plating, null));
        return Place(map, tile, brush);
    }

    /// <summary>
    /// The ceiling a tile's map sets over it (docs/ai/specs/map-editor.md 5.12): the <c>ceiling</c> var of its turf, without the quotes; null when the map
    /// leaves it to the game's rule.
    /// </summary>
    public static string CeilingOf(MapModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        int turf = TurfIndex(tile);
        string literal = turf < 0 ? null : VarOf(tile.Members[turf], "ceiling");
        return literal is { Length: >= 2 } && literal[0] == '"' && literal[^1] == '"' ? literal.Substring(1, literal.Length - 2) : null;
    }

    /// <summary>
    /// A tile with another ceiling over it: <c>none</c> (open to the sky), <c>lattice</c>, <c>catwalk</c>, the path of a floor type, or null for the
    /// game's rule again. It is the <c>ceiling</c> var of the tile's turf.
    /// </summary>
    public static MapModel WithCeiling(MapDocument map, MapModel tile, string ceiling)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        int turf = TurfIndex(tile);
        return turf < 0 ? tile : SetVar(map, tile, turf, "ceiling", ceiling == null ? null : "\"" + ceiling + "\"");
    }

    /// <summary>
    /// The gas a tile starts the round with, as tg's gas string: the <c>initial_gas_mix</c> var the map sets on its turf, else the turf type's own;
    /// null for a tile with no turf or a turf that holds no air (a wall). <paramref name="set"/> says whether the map sets it.
    /// </summary>
    public static string GasMixOf(MapModel tile, out bool set)
    {
        ArgumentNullException.ThrowIfNull(tile);
        set = false;
        int turf = TurfIndex(tile);
        if (turf < 0 || TurfStyle.Classify(tile.Members[turf].Path) == TurfKind.Closed)
            return null;
        string literal = VarOf(tile.Members[turf], "initial_gas_mix");
        set = literal != null;
        literal ??= TypeVars.Of(tile.Members[turf].Path).FirstOrDefault(v => v.Name == "initial_gas_mix")?.Default;
        return literal is { Length: >= 2 } && literal[0] == '"' && literal[^1] == '"' ? literal.Substring(1, literal.Length - 2) : literal;
    }

    /// <summary>A tile whose turf starts the round with another gas (tg's gas string), or (null) with its type's own again. A wall is left as it is.</summary>
    public static MapModel WithGasMix(MapDocument map, MapModel tile, string mix)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        int turf = TurfIndex(tile);
        return turf < 0 || TurfStyle.Classify(tile.Members[turf].Path) == TurfKind.Closed ? tile
            : SetVar(map, tile, turf, "initial_gas_mix", mix == null ? null : "\"" + mix + "\"");
    }

    // ---- the layers of a tile (docs/ai/specs/map-editor.md 5.17) ----

    /// <summary>
    /// The layer a turf is to the erase tool: a wall, a floor tile (any floor of tg's that is laid on plating), or the base under them (plating,
    /// and every other open turf: space, a planet's ground, water).
    /// </summary>
    public static TurfLayer LayerOf(string turf)
        => Under(turf, "/turf/closed") ? TurfLayer.Wall : Under(turf, "/turf/open/floor") && !Under(turf, Plating) ? TurfLayer.Tile : TurfLayer.Base;

    /// <summary>
    /// A tile with one layer of its turf taken away: a wall or a floor tile leaves the plating it stood on (airless where it was), plating or any
    /// other open turf leaves the turf and the area of the map's default tile. What stands on the tile stays, and so does the ceiling the map set
    /// over it. <paramref name="only"/>: the one layer that is taken (a stroke and a shape take one kind away); a tile whose turf is another is
    /// given back as it is. <paramref name="empty"/>: the empty tile of the tile's level (<see cref="MapDocument.Empty"/>), whose turf and area are
    /// left in the place of the default tile's.
    /// </summary>
    public static MapModel Peel(MapDocument map, MapModel tile, TurfLayer? only = null, MapModel empty = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        int at = TurfIndex(tile);
        if (at < 0)
            return tile;
        string turf = tile.Members[at].Path;
        TurfLayer layer = LayerOf(turf);
        if (only is { } wanted && wanted != layer)
            return tile;
        if (layer != TurfLayer.Base)
            return Place(map, tile, new MapMember(turf.EndsWith("/airless", StringComparison.Ordinal) ? Plating + "/airless" : Plating, null));
        return Unturfed(map, tile, empty);
    }

    /// <summary>
    /// A tile with its floor or its wall taken away whole, whatever its layers: the turf and the area of the map's default tile are left, and what
    /// stands there stays. It is what the erase tool takes from under a level (docs/ai/specs/map-editor.md 5.18): the underside seen there is the
    /// plating everything else of the tile stands on.
    /// </summary>
    public static MapModel Unturfed(MapDocument map, MapModel tile, MapModel empty = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        foreach (MapMember fallback in (empty ?? map.Default).Members)
        {
            if (!IsThing(fallback.Path))
                tile = Place(map, tile, fallback);
        }
        return tile;
    }

    /// <summary>
    /// What the place tool makes of a ceiling that is the floor of the level above, clicked from under it (docs/ai/specs/map-editor.md 5.18).
    /// <paramref name="room"/> is the tile under the ceiling and <paramref name="slab"/> the tile over it, on the level above.
    /// Glass takes the slab's place whole, plating and all: it is seen through from both sides (a wall of that level stays). Any other floor's type
    /// is laid under the slab's plating, on the side that is looked at: it is the ceiling var of the room's turf (plating itself takes a laid tile
    /// off), and where there is no plating to lie under, open space or glass, the slab is made of plating. A lattice or a catwalk is placed where
    /// the slab has neither a floor nor a wall. Anything else changes nothing. A slab made over a room is in the room's area where its own tile was
    /// in the map's default one (space), as the room's own floor is.
    /// </summary>
    public static (MapModel Room, MapModel Slab) Ceiled(MapDocument map, MapModel room, MapModel slab, MapMember brush)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(slab);
        string over = TurfOf(slab);
        TurfKind kind = TurfStyle.Classify(over);
        if (CeilingFrom(brush.Path) == null)
            return (room, slab);
        if (!IsTurf(brush.Path))
            return kind == TurfKind.Void ? (room, Place(map, slab, brush)) : (room, slab);
        if (TurfStyle.GlassFloor(brush.Path))
            return kind == TurfKind.Closed ? (room, slab) : (WithCeiling(map, room, null), InAreaOf(map, slab, Place(map, slab, brush), room));
        MapModel solid = kind == TurfKind.Void || TurfStyle.GlassFloor(over) ? InAreaOf(map, slab, Place(map, slab, new MapMember(Plating, null)), room) : slab;
        return (WithCeiling(map, room, LayerOf(brush.Path) == TurfLayer.Base ? null : brush.Path), solid);
    }

    // A tile made over a room is in the room's area where it was in the map's default one.
    private static MapModel InAreaOf(MapDocument map, MapModel was, MapModel made, MapModel room)
    {
        MapMember area = room.Members.LastOrDefault(m => IsArea(m.Path));
        return AreaOf(was) == AreaOf(map.Default) && area.Path != null && area.Path != AreaOf(was) && !ReferenceEquals(made, was) ? Place(map, made, area) : made;
    }

    private static string AreaOf(MapModel tile) => tile.Members.LastOrDefault(m => IsArea(m.Path)).Path;

    /// <summary>
    /// The floor tile a map lays under the plating of the level above: the ceiling var of a tile under another level, when it names a floor's type
    /// that is neither plating nor glass (docs/ai/specs/map-editor.md 5.12). Null when there is none.
    /// </summary>
    public static string LaidCeiling(MapModel room)
    {
        ArgumentNullException.ThrowIfNull(room);
        string set = CeilingOf(room);
        return set != null && Under(set, "/turf/open/floor") && LayerOf(set) == TurfLayer.Tile && !TurfStyle.GlassFloor(set) ? set : null;
    }

    /// <summary>
    /// What a ceiling that is the floor of the level above shows to the tile under it: the tile laid under its plating; else glass, for a glass
    /// floor; else plating, under any other floor and under a wall; null where the tile above is open space. The replace mode finds by it the
    /// ceilings that are like the one clicked.
    /// </summary>
    public static string UndersideOf(MapModel room, MapModel slab)
    {
        ArgumentNullException.ThrowIfNull(slab);
        string over = TurfOf(slab);
        return TurfStyle.Classify(over) == TurfKind.Void ? null : TurfStyle.GlassFloor(over) ? over : LaidCeiling(room) ?? Plating;
    }

    /// <summary>
    /// The ceiling a member makes when the place tool puts it on a ceiling: a floor's type its own path, tg's lattice and catwalk their names;
    /// null for anything else, which is no ceiling.
    /// </summary>
    public static string CeilingFrom(string path)
        => Under(path, "/obj/structure/lattice/catwalk") ? "catwalk" : Under(path, "/obj/structure/lattice") ? "lattice" : Under(path, "/turf/open/floor") ? path : null;

    /// <summary>
    /// A ceiling with one layer taken off: a floor tile's leaves plating; plating, a lattice or a catwalk leaves nothing (<c>none</c>).
    /// <paramref name="shown"/> is what is over the tile now: a floor type's path, <c>lattice</c>, <c>catwalk</c>; null or <c>none</c>: nothing
    /// is there, and the answer is null.
    /// </summary>
    public static string CeilingPeeled(string shown)
        => string.IsNullOrEmpty(shown) || shown == "none" ? null : LayerOf(shown) == TurfLayer.Tile ? Plating : "none";

    private static int TurfIndex(MapModel tile)
    {
        for (int i = tile.Members.Count - 1; i >= 0; i--)
        {
            if (IsTurf(tile.Members[i].Path))
                return i;
        }
        return -1;
    }

    // ---- a selection copied and moved ----

    /// <summary>Copies the whole tiles of the rectangle between two tiles of a level (the part of it that is on the map).</summary>
    public static MapClip Copy(MapDocument map, int x0, int y0, int x1, int y1, int z)
    {
        ArgumentNullException.ThrowIfNull(map);
        (int left, int right) = (Math.Max(1, Math.Min(x0, x1)), Math.Min(map.Width, Math.Max(x0, x1)));
        (int bottom, int top) = (Math.Max(1, Math.Min(y0, y1)), Math.Min(map.Height, Math.Max(y0, y1)));
        var tiles = new List<string>();
        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
                tiles.Add(map[x, y, z].Text);
        }
        return new MapClip(Math.Max(0, right - left + 1), Math.Max(0, top - bottom + 1), tiles);
    }

    /// <summary>
    /// Copies tiles of a level that need not make a rectangle (several selected rectangles): the clip is the box that holds them, with no tile
    /// where none was given. The tiles outside the map are left out.
    /// </summary>
    public static MapClip Copy(MapDocument map, IEnumerable<(int X, int Y)> cells, int z)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(cells);
        var given = new HashSet<(int X, int Y)>(cells.Where(c => map.Contains(c.X, c.Y, z)));
        if (given.Count == 0)
            return new MapClip(0, 0, Array.Empty<string>());
        (int left, int right, int bottom, int top) = (given.Min(c => c.X), given.Max(c => c.X), given.Min(c => c.Y), given.Max(c => c.Y));
        var tiles = new List<string>();
        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
                tiles.Add(given.Contains((x, y)) ? map[x, y, z].Text : null);
        }
        return new MapClip(right - left + 1, top - bottom + 1, tiles);
    }

    /// <summary>
    /// The tiles a clip changes when its south-west tile goes to (x, y, z): the whole tiles of a rectangle take the place of the map's, and a
    /// thing is one more thing of its tile. What would lie outside the map is left out.
    /// </summary>
    public static List<(int X, int Y, int Z, MapModel Model)> Paste(MapDocument map, MapClip clip, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(clip);
        var tiles = new List<(int, int, int, MapModel)>();
        if (clip.Thing is { } thing)
        {
            if (map.Contains(x, y, z))
                tiles.Add((x, y, z, Place(map, map[x, y, z], thing)));
            return tiles;
        }
        for (int dy = 0; dy < clip.Height; dy++)
        {
            for (int dx = 0; dx < clip.Width; dx++)
            {
                // (a clip of several rectangles has no tile between them)
                if (clip.Tiles[dy * clip.Width + dx] is { } text && map.Contains(x + dx, y + dy, z))
                    tiles.Add((x + dx, y + dy, z, map.Model(text)));
            }
        }
        return tiles;
    }

    /// <summary>
    /// The tiles that change when the rectangle between two tiles of a level moves by (dx, dy): the tiles it leaves become the level's empty
    /// tile, and its own take the place of the tiles there (a later entry of the list is the one that holds for a tile named twice).
    /// </summary>
    public static List<(int X, int Y, int Z, MapModel Model)> Move(MapDocument map, int x0, int y0, int x1, int y1, int z, int dx, int dy)
        => Move(map, MapShapes.Box(x0, y0, x1, y1, filled: true), z, dx, dy);

    /// <summary>As the rectangle's, for tiles of a level that need not make one (several selected rectangles).</summary>
    public static List<(int X, int Y, int Z, MapModel Model)> Move(MapDocument map, IEnumerable<(int X, int Y)> cells, int z, int dx, int dy)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(cells);
        var tiles = new List<(int, int, int, MapModel)>();
        if (dx == 0 && dy == 0)
            return tiles;
        var moved = cells.Where(c => map.Contains(c.X, c.Y, z)).Distinct().ToList();
        if (moved.Count == 0)
            return tiles;
        MapClip clip = Copy(map, moved, z);
        foreach ((int x, int y) in moved)
            tiles.Add((x, y, z, map.Empty(z)));
        tiles.AddRange(Paste(map, clip, moved.Min(c => c.X) + dx, moved.Min(c => c.Y) + dy, z));
        return tiles;
    }

    /// <summary>The tiles that change when a thing of a tile moves to another tile: none when it is no thing or does not move.</summary>
    public static List<(int X, int Y, int Z, MapModel Model)> MoveThing(MapDocument map, (int X, int Y, int Z) from, int member, (int X, int Y, int Z) to)
    {
        ArgumentNullException.ThrowIfNull(map);
        var tiles = new List<(int, int, int, MapModel)>();
        if (from == to || !map.Contains(from.X, from.Y, from.Z) || !map.Contains(to.X, to.Y, to.Z))
            return tiles;
        MapModel tile = map[from.X, from.Y, from.Z];
        if (member < 0 || member >= tile.Members.Count || !IsThing(tile.Members[member].Path))
            return tiles;
        tiles.Add((from.X, from.Y, from.Z, Remove(map, tile, member)));
        tiles.Add((to.X, to.Y, to.Z, Place(map, map[to.X, to.Y, to.Z], tile.Members[member])));
        return tiles;
    }

    // ---- the pieces that join by the way they are placed ----

    private static readonly string[] RunFamilies = { "/obj/structure/disposalpipe/segment", "/obj/machinery/atmospherics/pipe/heat_exchanging/simple" };

    /// <summary>
    /// True for a piece of a run with two ends that tg turns by its <c>dir</c>: straight along it, bent with a diagonal one (a disposal pipe's
    /// segment, disposal/pipe.dm:41; a heat exchange pipe, heat_exchange/simple.dm:17). The place tool turns such a piece along the way it is
    /// drawn. Cables and the other pipes need none of this: tg joins them to what is beside them when they are made.
    /// </summary>
    public static bool IsRunPiece(string path) => RunFamilies.Any(family => Under(path, family));

    /// <summary>
    /// The pieces of a run drawn over tiles that share sides, one a tile: each is the brush turned to join the tile before it and the tile after it
    /// (the first and the last lie along their one neighbour; a lone piece is the brush as it is).
    /// </summary>
    public static List<MapMember> RunPieces(MapMember brush, IReadOnlyList<(int X, int Y)> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var pieces = new List<MapMember>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            byte before = i > 0 ? WallMounts.DirOfStep(cells[i - 1].X - cells[i].X, cells[i - 1].Y - cells[i].Y) : (byte)0;
            byte after = i + 1 < cells.Count ? WallMounts.DirOfStep(cells[i + 1].X - cells[i].X, cells[i + 1].Y - cells[i].Y) : (byte)0;
            // a bend is tg's diagonal dir of the two sides it joins; a straight piece lies along the way to the next tile
            bool bend = before != 0 && after != 0 && before != after && before != Opposite(after);
            int dir = bend ? before | after : after != 0 ? after : before;
            pieces.Add(dir == 0 ? brush : WithVar(brush, "dir", dir.ToString(CultureInfo.InvariantCulture)));
        }
        return pieces;
    }

    private static byte Opposite(byte dir) => dir switch { 1 => 2, 2 => 1, 4 => 8, 8 => 4, _ => 0 };

    private static readonly string[] SideNames = { "north", "south", "east", "west" };

    /// <summary>
    /// The type of the same family that tg's directional helpers hang on a side of its tile (<c>MAPPING_DIRECTIONAL_HELPERS</c>:
    /// <c>.../directional/north</c> and the three others, each with the side's dir and pixel offset); null when the family has none.
    /// <paramref name="side"/> is the way from the tile to the wall, as BYOND's dir.
    /// </summary>
    public static string Directional(string path, byte side)
    {
        string name = side switch { 1 => "north", 2 => "south", 4 => "east", 8 => "west", _ => null };
        if (name == null || string.IsNullOrEmpty(path))
            return null;
        const string helper = "/directional/";
        int at = path.LastIndexOf(helper, StringComparison.Ordinal);
        string family = at >= 0 && SideNames.Contains(path.Substring(at + helper.Length)) ? path.Substring(0, at) : path;
        string hung = family + helper + name;
        return DmTypes.FromPath(hung) != null ? hung : null;
    }

    /// <summary>
    /// A thing as it hangs on the wall at a side of its tile: its family's directional type when tg has one (its own vars kept, but for the
    /// dir and the pixel offsets, which are the type's), else wall furniture turned to the wall, else the thing as it is.
    /// </summary>
    public static MapMember Mounted(MapMember thing, byte side)
    {
        if (Directional(thing.Path, side) is { } hung)
        {
            IReadOnlyList<MapOverride> kept = thing.Overrides?.Where(o => o.Var is not ("dir" or "pixel_x" or "pixel_y")).ToList();
            return new MapMember(hung, kept is { Count: > 0 } ? kept : null);
        }
        return side != 0 && PropKinds.IsWallMountType(thing.Path) ? WithVar(thing, "dir", side.ToString(CultureInfo.InvariantCulture)) : thing;
    }
}

/// <summary>The layers of a tile's turf, as the erase tool takes them away one at a time (<see cref="MapEdits.Peel"/>).</summary>
public enum TurfLayer
{
    /// <summary>A wall: taken away it leaves plating.</summary>
    Wall,

    /// <summary>A floor tile on its plating: taken away it leaves the plating.</summary>
    Tile,

    /// <summary>Plating, or any other open turf: taken away it leaves the map's default turf.</summary>
    Base,
}

/// <summary>
/// What the map editor copied (docs/ai/specs/map-editor.md 5.5): the whole tiles of a rectangle, as their texts row by row from the southern
/// row, each from west to east (null where the clip has no tile: a copy of several rectangles), or one thing. A text is of no map, so a clip
/// is pasted into any, and as a text of its own (<see cref="ToText"/>) it goes through the system's clipboard from one editor to another.
/// </summary>
public sealed record MapClip(int Width, int Height, IReadOnlyList<string> Tiles, MapMember? Thing = null)
{
    private const string Header = "anomaly-clip 1";

    /// <summary>A clip of one thing.</summary>
    public static MapClip Of(MapMember thing) => new(1, 1, Array.Empty<string>(), thing);

    /// <summary>The tiles the clip holds.</summary>
    public int Count => Thing != null ? 1 : Tiles.Count(t => t != null);

    /// <summary>
    /// The clip as a text: the header line, then <c>thing MEMBER</c>, or <c>size W H</c> and a line <c>X,Y: MEMBERS</c> for each tile it holds
    /// (X east and Y north of its south-west tile, from 0), the members as a map file writes them.
    /// </summary>
    public string ToText()
    {
        var text = new System.Text.StringBuilder(Header).Append('\n');
        if (Thing is { } thing)
            return text.Append("thing ").Append(MapDocument.TextOf(new[] { thing })).Append('\n').ToString();
        text.Append(CultureInfo.InvariantCulture, $"size {Width} {Height}\n");
        for (int i = 0; i < Tiles.Count; i++)
        {
            if (Tiles[i] != null)
                text.Append(CultureInfo.InvariantCulture, $"{i % Width},{i / Width}: {Tiles[i]}\n");
        }
        return text.ToString();
    }

    /// <summary>The clip of a text <see cref="ToText"/> wrote; null for any other text (what else the clipboard may hold).</summary>
    public static MapClip Parse(string text)
    {
        if (text == null || !text.StartsWith(Header, StringComparison.Ordinal))
            return null;
        string[] lines = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        try
        {
            if (lines.Length == 2 && lines[1].StartsWith("thing ", StringComparison.Ordinal))
                return MapDocument.Create(1, 1, 1, lines[1].Substring(6)).Default.Members is { Count: 1 } one ? Of(one[0]) : null;
            string[] size = lines.Length > 1 ? lines[1].Split(' ') : Array.Empty<string>();
            if (size.Length != 3 || size[0] != "size" || !int.TryParse(size[1], NumberStyles.None, CultureInfo.InvariantCulture, out int width)
                || !int.TryParse(size[2], NumberStyles.None, CultureInfo.InvariantCulture, out int height)
                || width < 1 || height < 1 || width > MapDocument.MaxWidth || height > MapDocument.MaxHeight)
                return null;
            var tiles = new string[width * height];
            for (int i = 2; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(": ", StringComparison.Ordinal), comma = lines[i].IndexOf(',', StringComparison.Ordinal);
                if (comma < 1 || colon < comma || !int.TryParse(lines[i].AsSpan(0, comma), NumberStyles.None, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(lines[i].AsSpan(comma + 1, colon - comma - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int y) || x >= width || y >= height)
                    return null;
                tiles[y * width + x] = lines[i].Substring(colon + 2);
            }
            return tiles.Any(t => t != null) ? new MapClip(width, height, tiles) : null;
        }
        catch (MapTextException)
        {
            return null; // (a thing's line the format refuses)
        }
    }
}
