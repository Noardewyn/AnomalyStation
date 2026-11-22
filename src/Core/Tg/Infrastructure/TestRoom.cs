using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Owner: M1 P3 (server host); the M2 layout option and the rooms layout are M2 P6's. Signature by the M1 contracts step;
// Build's body is P3's.

/// <summary>A floor cell where the host may place a joining player's avatar (BYOND coordinates).</summary>
public readonly record struct SpawnPoint(int X, int Y, int Z);

/// <summary>Which map the test room builds (<c>ServerConfig.RoomLayout</c>, the config key <c>ROOM_LAYOUT</c>).</summary>
public enum TestRoomLayout : byte
{
    /// <summary>M2 (scope 2k): four rooms of about 10 x 10 with doorways, a 1-tile and a 2-tile corridor, a dead-end wall face and pillars, for the capsule locomotion's feel check and the bots.</summary>
    Rooms = 0,

    /// <summary>M1's room: a rim, four short interior wall runs and a lattice of pillars.</summary>
    Classic = 1,
}

/// <summary>
/// The M1 test room (scope 2a): the map the server runs until M3's map loader, host code next to <see cref="ToyRoom"/> and
/// built the same way (tg's real types, <c>world.turf</c> set before the map grows, walls placed before the area is
/// assigned, one <c>/area/misc/testroom</c> and tg's <c>change_area</c> per turf), but larger: about 48 x 48 with a few
/// interior wall runs and pillars, so that 50 to 150 wandering players do not jam it (scope risk 12), and with a list of
/// spawn points spread over the floor. It runs as the <c>compiledMap</c> step of <see cref="TgWorld.Boot"/>, in the world's
/// scope, before <c>world/New</c>. At most <see cref="MaxSize"/> on a side until the boot scaling is fixed (scope risk 3:
/// <c>change_area</c> is superlinear in the room's size).
/// <para>
/// Classic layout (<see cref="TestRoomLayout.Classic"/>; <see cref="IsWall"/>, the same for a given size in every run): the rim is wall. A room of at least
/// <see cref="FeaturedSize"/> on both sides also has four short wall runs, none touching another wall, at fixed fractions
/// of the size (for 48 x 48: y 16 from x 9 to 19, y 32 from x 28 to 38, x 32 from y 8 to 14, x 16 from y 34 to 40), and
/// single-tile pillars on a lattice of <c>min(width, height) / 6</c> tiles that keep one free tile around every run. No
/// wall encloses a region, so every floor tile is reachable. Spawn points are the floor tiles of a 3-tile lattice with no
/// wall in their 8 neighbours (a room too small for that uses every floor tile), ordered by a fixed stride through the
/// lattice so that consecutive joins land far apart.
/// </para>
/// <para>
/// Rooms layout (<see cref="TestRoomLayout.Rooms"/>, M2, scope 2k): four rooms with a floor of <c>r</c> x <c>r</c> tiles
/// (<c>r</c> = 10 at 48 x 48, smaller in a smaller map, at least 3) in the four corners, each walled off from the open hall
/// that fills the rest of the room (a plus-shaped hall) and joined to it by two 1-tile doorways without doors (doors are
/// M3). When the hall is big enough (at least 12 wide and 20 high outside the rooms: 44 x 44 and up) it also holds a 1-tile corridor (open at both ends:
/// two players side by side pass in it at 2 m per tile), a 2-tile corridor closed at its east end (a dead-end wall face to
/// shove against), and pillars; a room of 6 x 6 or more has a pillar of its own. Every floor tile is reachable (a flood fill
/// test). Every open turf keeps tg's <c>initial_gas_mix</c> (<c>OPENTURF_DEFAULT_ATMOS</c>, or the mix given to
/// <see cref="Build"/>), which tg's <c>create_gas_mixture</c> turns into the turf's air once M2 P2 translates it.
/// </para>
/// <para>
/// Space around (the owner, 2026-10-13: a deleted rim wall let no air out, as nothing lay beyond the map's edge): with a
/// <c>space</c> margin, the room is moved <c>space</c> cells in from the map's corner and ringed by that many cells of tg's
/// <c>/turf/open/space</c> in the world area (<c>/area/space</c>), as a station is; the map is then
/// <c>width + 2 * space</c> by <c>height + 2 * space</c>, the room's cell (x, y) is the map's (x + space, y + space), and the
/// spawn points are map cells. The server's room has <see cref="ServerSpace"/>; 0 (the default) is the room alone.
/// </para>
/// </summary>
public sealed class TestRoom
{
    /// <summary>The default width and height, rim included.</summary>
    public const int DefaultSize = 48;

    /// <summary>The cells of space around the server's room (<c>--room-space</c>).</summary>
    public const int ServerSpace = 3;

    /// <summary>The largest space margin.</summary>
    public const int MaxSpace = 16;

    /// <summary>The largest width and height the boot handles in about a second (scope 1.6: 64 boots in 1.0 s, 128 in 9.7 s).</summary>
    public const int MaxSize = 64;

    /// <summary>The smallest room: a rim and one floor tile (as <see cref="ToyRoom.MinSize"/>).</summary>
    public const int MinSize = ToyRoom.MinSize;

    /// <summary>The smallest side for which the room gets its interior wall runs and pillars.</summary>
    public const int FeaturedSize = 14;

    // The spacing of the spawn lattice, in tiles.
    private const int SpawnSpacing = 3;

    internal TestRoom(area_misc_testroom room, IReadOnlyList<SpawnPoint> spawnPoints)
    {
        Area = room;
        SpawnPoints = spawnPoints;
    }

    /// <summary>The room's area.</summary>
    public area_misc_testroom Area { get; }

    /// <summary>Floor cells for avatars, spread over the room, in a fixed order (the host picks deterministically).</summary>
    public IReadOnlyList<SpawnPoint> SpawnPoints { get; }

    /// <summary>
    /// Builds the room of <paramref name="width"/> x <paramref name="height"/> turfs (rim included, one z level) in the
    /// current world, whose map must still be empty, as <see cref="ToyRoom.Build"/> requires. <paramref name="gasMix"/> is the
    /// <c>initial_gas_mix</c> of every floor turf (null: tg's <c>OPENTURF_DEFAULT_ATMOS</c>).
    /// </summary>
    public static TestRoom Build(int width = DefaultSize, int height = DefaultSize, TestRoomLayout layout = TestRoomLayout.Rooms, string gasMix = null, int space = 0)
    {
        if (width < MinSize || height < MinSize || width > MaxSize || height > MaxSize)
            throw new ArgumentOutOfRangeException(nameof(width), $"the test room is {MinSize} to {MaxSize} turfs on a side (rim included), not {width} x {height}");
        if (space < 0 || space > MaxSpace)
            throw new ArgumentOutOfRangeException(nameof(space), $"the space around the test room is 0 to {MaxSpace} cells, not {space}");
        World world = World.Current;
        if (Dm.world.maxx != 0 || Dm.world.maxy != 0 || Dm.world.maxz != 0)
            throw new InvalidOperationException("the test room is the whole map: the world's map must be empty (maxx, maxy and maxz are 0)");
        if (GLOB__var == null)
            throw new InvalidOperationException("the test room needs GLOB (its area registers in GLOB.areas)");
        if (SSmapping == null)
            throw new InvalidOperationException("the test room needs SSmapping (its per-z lists are grown to world.maxz)");

        bool[,] walls = Layout(layout, width, height);

        // From here on the steps of ToyRoom.Build, for one level, with the interior walls placed with the rim.
        MapGrid.Attach(world);

        // R13: world.turf is the floor type before the map grows, so no floor is ever replaced by a floor.
        Dm.world.turf = typeof(turf_open_indestructible);
        Dm.world.maxx = width + 2 * space;
        Dm.world.maxy = height + 2 * space;
        // The level is the station's (tg loads it through SSmapping's add_new_zlevel, which grows the map with
        // world.incrementMaxZ() and so SSmobs' per-z lists, game/world.dm:491-494).
        Dm.world.incrementMaxZ();

        // mapping.dm:756-768 (manage_z_level): the per-z lists the code reads, grown to world.maxz.
        SSmapping.gravity_by_z_level.len = 1;
        ToyRoom.LinkLevels(1); // generate_linkages_for_z_level (mapping.dm:226), as tg does for each level it adds
        SSmapping.z_level_to_plane_offset.len = 1;
        SSmapping.z_level_to_lowest_plane_offset.len = 1;
        SSmapping.z_level_to_plane_offset[1f] = 0;
        SSmapping.z_level_to_lowest_plane_offset[1f] = 0;

        // The walls replace their floor cells before any area list is written (BYOND: new /turf at an occupied cell
        // replaces the turf, so the replaced one is dead), in BYOND's turf order (x fastest).
        int maxx = width + 2 * space, maxy = height + 2 * space;
        for (int y = 1; y <= maxy; y++)
        {
            for (int x = 1; x <= maxx; x++)
            {
                int rx = x - space, ry = y - space;
                if (rx < 1 || ry < 1 || rx > width || ry > height)
                    Dm.@new<turf_open_space>(locate(x, y, 1)); // the ring of space stays in the world area, /area/space
                else if (walls[rx, ry])
                    Dm.@new<turf_closed_indestructible>(locate(x, y, 1));
            }
        }

        // mapping.dm:776-781 (build_area_turfs, the space-guaranteed branch), after the turfs are final.
        var world_area = (area)locate(1, 1, 1).loc;
        world_area.turfs_by_zlevel.len = 1;
        world_area.turfs_by_zlevel[1f] = block(1, 1, 1, maxx, maxy, 1);

        // reader.dm:929-937: a new area with a null location, then change_area per turf.
        area_misc_testroom room = Dm.@new<area_misc_testroom>();
        foreach (turf T in Dm.each<turf>(block(1 + space, 1 + space, 1, width + space, height + space, 1)))
            T.change_area((area)T.loc, room);

        // M2 (scope 2k): the floors keep tg's initial_gas_mix (set before the atoms initialize, where tg's open turfs make
        // their air from it); the closed turfs block air (blocks_air) as in tg.
        string mix = gasMix ?? OPENTURF_DEFAULT_ATMOS;
        for (int y = 1; y <= height; y++)
        {
            for (int x = 1; x <= width; x++)
            {
                if (!walls[x, y] && locate(x + space, y + space, 1) is turf_open open)
                    open.initial_gas_mix = mix;
            }
        }

        var spawns = SpawnOrder(walls, width, height).ConvertAll(p => new SpawnPoint(p.X + space, p.Y + space, p.Z));
        return new TestRoom(room, spawns);
    }

    /// <summary>Whether the cell (<paramref name="x"/>, <paramref name="y"/>) of a <paramref name="width"/> x <paramref name="height"/> test room of <paramref name="layout"/> is a wall (the layout <see cref="Build"/> makes).</summary>
    public static bool IsWall(int width, int height, int x, int y, TestRoomLayout layout = TestRoomLayout.Rooms)
    {
        if (x < 1 || y < 1 || x > width || y > height) throw new ArgumentOutOfRangeException(nameof(x), "the cell is outside the room");
        return Layout(layout, width, height)[x, y];
    }

    // The walls of the room, indexed [x, y] from 1 (index 0 unused).
    private static bool[,] Layout(TestRoomLayout layout, int width, int height)
        => layout == TestRoomLayout.Classic ? ClassicLayout(width, height) : RoomsLayout(width, height);

    // M1's layout (see the class comment).
    private static bool[,] ClassicLayout(int width, int height)
    {
        var walls = new bool[width + 1, height + 1];
        for (int y = 1; y <= height; y++)
        {
            for (int x = 1; x <= width; x++)
                walls[x, y] = x == 1 || y == 1 || x == width || y == height;
        }
        if (width < FeaturedSize || height < FeaturedSize)
            return walls;

        // Four short runs at fixed fractions of the room (for 48: (9..19, 16), (28..38, 32), (32, 8..14), (16, 34..40)).
        var runs = new List<(int X0, int Y0, int X1, int Y1)>
        {
            (width / 5, height / 3, 2 * width / 5, height / 3),
            (3 * width / 5, 2 * height / 3, 4 * width / 5, 2 * height / 3),
            (2 * width / 3, height / 6, 2 * width / 3, height / 3 - 2),
            (width / 3, 2 * height / 3 + 2, width / 3, 5 * height / 6),
        };
        var run = new bool[width + 1, height + 1];
        foreach (var (x0, y0, x1, y1) in runs)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (x > 2 && y > 2 && x < width - 1 && y < height - 1)
                        run[x, y] = true;
                }
            }
        }

        // Pillars on a lattice, keeping one free tile between a pillar and a run (so a pillar never closes a corridor).
        int spacing = Math.Max(4, Math.Min(width, height) / 6);
        for (int y = spacing; y < height - 2; y += spacing)
        {
            for (int x = spacing; x < width - 2; x += spacing)
            {
                if (!NearAny(run, x, y, 2, width, height))
                    walls[x, y] = true;
            }
        }
        for (int y = 1; y <= height; y++)
        {
            for (int x = 1; x <= width; x++)
                walls[x, y] |= run[x, y];
        }
        return walls;
    }

    // M2's layout (see the class comment). The map is scanned in four steps: the rim, the four corner rooms with their two
    // doorways each, then (in a hall big enough) the two corridors and the pillars. Every size works the same way; the
    // numbers below are for 48 x 48 (r = 10: rooms at x 2 to 11 and 38 to 47, the hall's centre block x 13 to 36).
    private static bool[,] RoomsLayout(int width, int height)
    {
        var walls = new bool[width + 1, height + 1];
        for (int y = 1; y <= height; y++)
        {
            for (int x = 1; x <= width; x++)
                walls[x, y] = x == 1 || y == 1 || x == width || y == height;
        }
        if (width < FeaturedSize || height < FeaturedSize)
            return walls;

        int r = Math.Clamp((Math.Min(width, height) - 6) / 3, 3, 10);
        int half = r / 2;

        // The rooms: left ones' floor is x 2 to r + 1 (inner wall at r + 2), right ones' x width - r to width - 1 (inner
        // wall at width - r - 1); the same for y. A room is walled on its two inner sides, with a doorway in the middle of each
        // (the rim closes the other two).
        foreach (bool right in new[] { false, true })
        {
            foreach (bool top in new[] { false, true })
            {
                int xi = right ? width - r - 1 : r + 2;      // the inner wall column
                int yi = top ? height - r - 1 : r + 2;       // the inner wall row
                int xFrom = right ? xi : 1, xTo = right ? width : xi;
                int yFrom = top ? yi : 1, yTo = top ? height : yi;
                for (int y = yFrom; y <= yTo; y++)
                    walls[xi, y] = true;
                for (int x = xFrom; x <= xTo; x++)
                    walls[x, yi] = true;
                int doorY = top ? height - 1 - half : 2 + half; // a doorway in the inner column, into the vertical band
                int doorX = right ? width - 1 - half : 2 + half; // a doorway in the inner row, into the horizontal band
                walls[xi, doorY] = false;
                walls[doorX, yi] = false;
                if (r >= 6)
                {
                    // A pillar of the room's own, off the line of both doorways.
                    int px = right ? width - r + half + 1 : 2 + half + 1;
                    int py = top ? height - r + half + 1 : 2 + half + 1;
                    walls[px, py] = true;
                }
            }
        }

        int bandX0 = r + 3, bandX1 = width - r - 2;   // the vertical band between the left and right rooms
        int rowY0 = r + 3, rowY1 = height - r - 2;    // the horizontal band between the bottom and top rooms
        if (bandX1 - bandX0 + 1 < 12 || rowY1 - rowY0 + 1 < 20)
            return walls;

        int cx = width / 2, cy = height / 2;
        int x0 = bandX0 + 3, x1 = bandX1 - 3;         // the corridors' span (one free hall column stays at each end)
        // The 1-tile corridor: wall rows at cy - 5 and cy - 3, floor row cy - 4, open at both ends.
        for (int x = x0; x <= x1; x++)
        {
            walls[x, cy - 5] = true;
            walls[x, cy - 3] = true;
        }
        // The 2-tile corridor: wall rows at cy + 3 and cy + 6, floor rows cy + 4 and cy + 5, closed at its east end.
        for (int x = x0; x <= x1; x++)
        {
            walls[x, cy + 3] = true;
            walls[x, cy + 6] = true;
        }
        walls[x1, cy + 4] = true;
        walls[x1, cy + 5] = true;
        // Pillars in the free rows of the hall, away from the corridors and the walls.
        foreach (int py in new[] { rowY0 + 2, rowY1 - 2 })
        {
            foreach (int px in new[] { cx - 6, cx, cx + 6 })
                walls[px, py] = true;
        }
        return walls;
    }

    // Whether a cell within Chebyshev distance d of (x, y) is set.
    private static bool NearAny(bool[,] cells, int x, int y, int d, int width, int height)
    {
        for (int dy = -d; dy <= d; dy++)
        {
            for (int dx = -d; dx <= d; dx++)
            {
                int cx = x + dx, cy = y + dy;
                if (cx >= 1 && cy >= 1 && cx <= width && cy <= height && cells[cx, cy])
                    return true;
            }
        }
        return false;
    }

    // The spawn points: the free lattice cells in a fixed stride order (consecutive joins far apart).
    private static List<SpawnPoint> SpawnOrder(bool[,] walls, int width, int height)
    {
        var lattice = new List<SpawnPoint>();
        for (int y = SpawnSpacing; y < height; y += SpawnSpacing)
        {
            for (int x = SpawnSpacing; x < width; x += SpawnSpacing)
            {
                if (!NearAny(walls, x, y, 1, width, height))
                    lattice.Add(new SpawnPoint(x, y, 1));
            }
        }
        if (lattice.Count == 0)
        {
            for (int y = 2; y < height; y++)
            {
                for (int x = 2; x < width; x++)
                {
                    if (!walls[x, y])
                        lattice.Add(new SpawnPoint(x, y, 1));
                }
            }
        }

        int n = lattice.Count;
        int stride = Math.Max(1, (int)Math.Round(n * 0.618));
        while (Gcd(stride, n) != 1) stride++;
        var ordered = new List<SpawnPoint>(n);
        for (int i = 0; i < n; i++)
            ordered.Add(lattice[(int)((long)i * stride % n)]);
        return ordered;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }
}
