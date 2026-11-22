using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.T2;

/// <summary>
/// M3 T2 phase B: the junction states that tg's <c>/atom/proc/bitmask_smooth</c> (__HELPERS/icon_smoothing.dm:175-258) gives walls,
/// full-tile windows and tables, for every pattern of the eight neighbours. The expected value comes from <see cref="Oracle"/>,
/// written from the DM here and sharing no code with the port.
/// </summary>
internal static class Oracle
{
    // __DEFINES/icon_smoothing.dm:36-43
    public const int N = 1, S = 2, E = 4, W = 8, NE = 16, SE = 32, SW = 64, NW = 128;

    /// <summary>The neighbour offsets by bit index of a pattern: N, S, E, W, NE, SE, SW, NW.</summary>
    public static readonly (int dx, int dy, int flag)[] Neighbours =
    {
        (0, 1, N), (0, -1, S), (1, 0, E), (-1, 0, W), (1, 1, NE), (1, -1, SE), (-1, -1, SW), (-1, 1, NW),
    };

    /// <summary>
    /// bitmask_smooth for SMOOTH_BITMASK (no SMOOTH_BORDER, no border object or proc filter): the four cardinals, then (unless
    /// the atom is cardinals-only, or lacks a north/south or an east/west junction) the diagonals whose both cardinals were found.
    /// </summary>
    public static int Junction(Func<int, bool> found, bool cardinalsOnly)
    {
        int j = 0;
        if (found(N)) j |= N;
        if (found(S)) j |= S;
        if (found(E)) j |= E;
        if (found(W)) j |= W;
        // `if(smoothing_flags & SMOOTH_BITMASK_CARDINALS || !(new_junction & (NORTH|SOUTH)) || !(new_junction & (EAST|WEST)))`
        if (cardinalsOnly || (j & (N | S)) == 0 || (j & (E | W)) == 0) return j;
        if ((j & N) != 0)
        {
            if ((j & W) != 0 && found(NW)) j |= NW;
            if ((j & E) != 0 && found(NE)) j |= NE;
        }
        if ((j & S) != 0)
        {
            if ((j & W) != 0 && found(SW)) j |= SW;
            if ((j & E) != 0 && found(SE)) j |= SE;
        }
        return j;
    }

    /// <summary>The 8 junctions that give a diagonal wall its "-diagonal" icon state (icon_smoothing.dm:288-298).</summary>
    public static bool IsDiagonalState(int j)
        => j == (N | W) || j == (N | E) || j == (S | W) || j == (S | E)
        || j == (N | W | NW) || j == (N | E | NE) || j == (S | W | SW) || j == (S | E | SE);

    /// <summary>
    /// `canSmoothWith[target] & their_groups[target]` for any key of canSmoothWith (SMOOTH_AGAINST, icon_smoothing.dm:208-215),
    /// on the bitflag lists SETUP_SMOOTHING made (a text key of the group's index, the bit set of its groups).
    /// </summary>
    public static bool Matches(atom self, atom other)
    {
        if (self.canSmoothWith is not DmList can || other.smoothing_groups is not DmList theirs) return false;
        foreach (object key in can.Items())
        {
            if (Dm.truthy(Dm.bitand(can[key], theirs[key]))) return true;
        }
        return false;
    }

    /// <summary>SEARCH_ADJ_IN_DIR (icon_smoothing.dm:185-205) for the world's real neighbours of <paramref name="self"/>.</summary>
    public static bool FoundInWorld(atom self, int dx, int dy)
    {
        turf here = self as turf ?? (self.loc as turf);
        turf neighbour = locate(here.x + dx, here.y + dy, here.z) as turf;
        area home = get_area(self) as area;
        bool smooth_obj = ((int)self.smoothing_flags & SMOOTH_OBJ) != 0;
        bool smooth_border = ((int)self.smoothing_flags & SMOOTH_BORDER) != 0;
        if (neighbour != null && (home?.area_limited_icon_smoothing == null || istype(neighbour.loc, home.area_limited_icon_smoothing)))
        {
            if (neighbour.smoothing_groups != null && Matches(self, neighbour)) return true;
            if (smooth_obj)
            {
                foreach (atom_movable thing in neighbour.contents.Items())
                {
                    if (!Dm.truthy(thing.anchored) || thing.smoothing_groups == null) continue;
                    if (Matches(self, thing)) return true;
                }
            }
            return false;
        }
        return smooth_border;
    }
}

public class SmoothingJunctionTests
{
    private const int Stride = 4;       // a pattern's 3x3 block and one empty column or row
    private const int Origin = 3;       // the first block's centre
    private const int Blocks = 16;      // 16 x 16 = 256 patterns
    private const int RoomSize = Origin + Stride * (Blocks - 1) + 3;

    private static (int x, int y) Centre(int pattern) => (Origin + Stride * (pattern % Blocks), Origin + Stride * (pattern / Blocks));

    private static void Smooth() => Waiting.NoSleep(SSicon_smooth.Initialize());

    /// <summary>Builds the 256 blocks: block p has a centre <typeparamref name="T"/> and a neighbour for each set bit of p.</summary>
    private static List<(int pattern, T centre)> Lay<T>(Func<turf, T> make) where T : atom
    {
        var centres = new List<(int, T)>();
        for (int p = 0; p < 256; p++)
        {
            (int cx, int cy) = Centre(p);
            T centre = make(locate(cx, cy, 1));
            centres.Add((p, centre));
            for (int bit = 0; bit < 8; bit++)
            {
                if ((p & (1 << bit)) == 0) continue;
                (int dx, int dy, _) = Oracle.Neighbours[bit];
                make(locate(cx + dx, cy + dy, 1));
            }
        }
        return centres;
    }

    private static int PatternJunction(int pattern, bool cardinalsOnly)
        => Oracle.Junction(flag =>
        {
            for (int bit = 0; bit < 8; bit++)
            {
                if (Oracle.Neighbours[bit].flag == flag) return (pattern & (1 << bit)) != 0;
            }
            return false;
        }, cardinalsOnly);

    private static int Junction(atom a) => Convert.ToInt32(a.smoothing_junction);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_wall_takes_tgs_junction_for_each_of_the_256_neighbour_patterns(bool cardinalsOnly)
    {
        var room = TgToyRoom.Create(RoomSize, RoomSize);
        using (room.World.Enter())
        {
            var centres = Lay(t => Dm.@new<turf_closed_wall>(t));
            if (cardinalsOnly)
            {
                // SMOOTH_BITMASK_CARDINALS (icon_smoothing.dm:20-22): only the four junctions, never a diagonal
                foreach (var (_, centre) in centres) centre.smoothing_flags = SMOOTH_BITMASK_CARDINALS | SMOOTH_QUEUED;
            }
            Smooth();
            int distinct = 0;
            var seen = new HashSet<int>();
            foreach (var (pattern, wall) in centres)
            {
                // the centre is a replaced turf: find it again
                (int cx, int cy) = Centre(pattern);
                var turf = Assert.IsAssignableFrom<turf_closed_wall>(locate(cx, cy, 1));
                int expected = PatternJunction(pattern, cardinalsOnly);
                Assert.True(expected == Junction(turf), $"pattern {pattern}: junction {turf.smoothing_junction}, tg's bitmask_smooth gives {expected}");
                Assert.Equal("wall-" + expected, turf.icon_state); // set_smoothed_icon_state: "[base_icon_state]-[junction]"
                Assert.False(Dm.truthy(Dm.bitand(turf.smoothing_flags, SMOOTH_QUEUED)));
                if (seen.Add(expected)) distinct++;
            }
            // 256 patterns give 47 distinct junctions for BITMASK (the 4 cardinals and the diagonals both of whose cardinals are set)
            // and 16 for CARDINALS
            Assert.Equal(cardinalsOnly ? 16 : 47, distinct);
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void A_full_tile_window_takes_tgs_junction_for_each_of_the_256_neighbour_patterns()
    {
        var room = TgToyRoom.Create(RoomSize, RoomSize);
        using (room.World.Enter())
        {
            var centres = Lay(t => Dm.@new<obj_structure_window_reinforced_fulltile>(t));
            Smooth();
            foreach (var (pattern, window) in centres)
            {
                int expected = PatternJunction(pattern, false);
                Assert.True(expected == Junction(window), $"pattern {pattern}: junction {window.smoothing_junction}, tg's bitmask_smooth gives {expected}");
                Assert.False(Dm.truthy(Dm.bitand(window.smoothing_flags, SMOOTH_QUEUED)));
            }
            // SETUP_SMOOTHING turned the object group into SMOOTH_OBJ (icon_smoothing.dm: PARSE_CAN_SMOOTH_WITH)
            Assert.True(Dm.truthy(Dm.bitand(centres[0].centre.smoothing_flags, SMOOTH_OBJ)));
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void A_table_takes_tgs_junction_for_each_of_the_256_neighbour_patterns()
    {
        var room = TgToyRoom.Create(RoomSize, RoomSize);
        using (room.World.Enter())
        {
            var centres = Lay(t => Dm.@new<obj_structure_table>(t));
            Smooth();
            foreach (var (pattern, table) in centres)
            {
                int expected = PatternJunction(pattern, false);
                Assert.True(expected == Junction(table), $"pattern {pattern}: junction {table.smoothing_junction}, tg's bitmask_smooth gives {expected}");
            }
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void A_diagonal_wall_gets_the_diagonal_icon_state_for_exactly_tgs_eight_junctions()
    {
        var room = TgToyRoom.Create(RoomSize, RoomSize);
        using (room.World.Enter())
        {
            var centres = Lay(t => Dm.@new<turf_closed_wall_mineral_titanium>(t));
            Smooth();
            int diagonals = 0;
            foreach (var (pattern, _) in centres)
            {
                (int cx, int cy) = Centre(pattern);
                var wall = Assert.IsAssignableFrom<turf_closed_wall_mineral_titanium>(locate(cx, cy, 1));
                Assert.True(((int)wall.smoothing_flags & SMOOTH_DIAGONAL_CORNERS) != 0);
                int expected = PatternJunction(pattern, false);
                Assert.True(expected == Junction(wall), $"pattern {pattern}: junction {wall.smoothing_junction}, tg's bitmask_smooth gives {expected}");
                // turf/closed/set_smoothed_icon_state (icon_smoothing.dm:277-322)
                string state = (string)wall.base_icon_state + "-" + expected + (Oracle.IsDiagonalState(expected) ? "-diagonal" : "");
                Assert.Equal(state, wall.icon_state);
                if (Oracle.IsDiagonalState(expected)) diagonals++;
            }
            Assert.True(diagonals > 0);
            // the mineral walls' Initialize reaches set_custom_materials (atom_materials.dm:13, M4a), the known open item
            var others = room.Runtimes.Where(r => !r.Message.Contains("set_custom_materials", StringComparison.Ordinal)).ToList();
            Assert.True(others.Count == 0, string.Join("\n", others.Select(r => r.Message).Distinct().Take(5)));
        }
    }

    // ---- what does not smooth with what

    [Fact]
    public void Things_of_another_smoothing_group_or_unanchored_or_in_a_limited_area_do_not_connect()
    {
        var room = TgToyRoom.Create(15, 15);
        using (room.World.Enter())
        {
            // a wall (canSmoothWith = walls only, walls.dm:19) beside a window, a reinforced wall's group is the same as a wall's
            var wall = Dm.@new<turf_closed_wall>(locate(3, 3, 1));
            Dm.@new<obj_structure_window_reinforced_fulltile>(locate(4, 3, 1));
            Dm.@new<turf_open_floor_plating>(locate(2, 3, 1));
            Dm.@new<turf_closed_wall_r_wall>(locate(3, 4, 1));
            // a table: beside a wood table (SMOOTH_GROUP_WOOD_TABLES, tables_racks.dm:629), beside an unanchored table
            var table = Dm.@new<obj_structure_table>(locate(8, 8, 1));
            Dm.@new<obj_structure_table_wood>(locate(9, 8, 1));
            var loose = Dm.@new<obj_structure_table>(locate(7, 8, 1));
            loose.anchored = 0f; // `if(!thing.anchored || isnull(thing_smoothing_groups)) continue` (icon_smoothing.dm:197)
            // a window in a limited area (area_limited_icon_smoothing, areas.dm:113) beside a window outside it
            var window = Dm.@new<obj_structure_window_reinforced_fulltile>(locate(12, 12, 1));
            Dm.@new<obj_structure_window_reinforced_fulltile>(locate(11, 12, 1));
            var limited = Dm.@new<area_shuttle>();
            limited.area_limited_icon_smoothing = typeof(area_shuttle);
            limited.contents.Add(locate(12, 12, 1));
            Smooth();
            // the wall touches only the reinforced wall to its north: reinforced walls are in SMOOTH_GROUP_WALLS (walls.dm:18)
            Assert.Equal(Oracle.N, Junction((atom)locate(3, 3, 1)));
            Assert.Equal(0, Junction(table));
            Assert.Equal(0, Junction(window));
            Assert.Empty(room.RuntimeText);
        }
    }

    // ---- QUEUE_SMOOTH_NEIGHBORS (__DEFINES/icon_smoothing.dm:61-63: every atom of orange(1, thing) that uses smoothing is queued)

    private static void Fire()
    {
        SSicon_smooth.state = SS_RUNNING; // as the Master's RunQueue sets it, so that MC_TICK_CHECK does not pause
        Waiting.NoSleep(SSicon_smooth.fire());
    }

    private static bool Queued(atom a) => Dm.truthy(Dm.bitand(a.smoothing_flags, SMOOTH_QUEUED));

    [Fact]
    public void A_changed_turf_queues_its_neighbours_and_they_smooth_without_it()
    {
        var room = TgToyRoom.Create(11, 11);
        using (room.World.Enter())
        {
            for (int x = 4; x <= 6; x++)
                for (int y = 4; y <= 6; y++)
                    Dm.@new<turf_closed_wall>(locate(x, y, 1));
            Dm.@new<turf_closed_wall>(locate(8, 8, 1)); // two cells away: not a neighbour
            Smooth();
            Assert.Equal(255, Junction((atom)locate(5, 5, 1)));
            Assert.Equal(Oracle.S | Oracle.W | Oracle.SW, Junction((atom)locate(6, 6, 1)));

            // change_turf.dm:200-203: `if(flags_1 & INITIALIZED_1) QUEUE_SMOOTH_NEIGHBORS(src); QUEUE_SMOOTH(src)` on the new turf
            Waiting.NoSleep(((turf)locate(5, 5, 1)).dm_change_turf(typeof(turf_open_floor_plating), null, null));
            for (int x = 4; x <= 6; x++)
            {
                for (int y = 4; y <= 6; y++)
                {
                    if (x == 5 && y == 5) continue;
                    Assert.True(Queued((atom)locate(x, y, 1)), $"({x},{y}) is a neighbour of the changed turf and was not queued");
                }
            }
            Assert.False(Queued((atom)locate(8, 8, 1)));
            Fire();
            Assert.False(Queued((atom)locate(4, 4, 1)));
            // the 8 walls around the hole: each one's junction is tg's for the ring without the centre
            for (int x = 4; x <= 6; x++)
            {
                for (int y = 4; y <= 6; y++)
                {
                    if (x == 5 && y == 5) continue;
                    int j = Oracle.Junction(flag =>
                    {
                        foreach (var (dx, dy, f) in Oracle.Neighbours)
                        {
                            if (f != flag) continue;
                            int nx = x + dx, ny = y + dy;
                            return nx >= 4 && nx <= 6 && ny >= 4 && ny <= 6 && !(nx == 5 && ny == 5);
                        }
                        return false;
                    }, false);
                    Assert.True(j == Junction((atom)locate(x, y, 1)), $"({x},{y}): junction {Junction((atom)locate(x, y, 1))}, expected {j} for the ring without its centre");
                }
            }
        }
    }

    [Fact]
    public void A_new_or_deleted_window_or_table_queues_its_neighbours()
    {
        var room = TgToyRoom.Create(11, 11);
        using (room.World.Enter())
        {
            var left = Dm.@new<obj_structure_window_reinforced_fulltile>(locate(4, 5, 1));
            var tleft = Dm.@new<obj_structure_table>(locate(4, 8, 1));
            Smooth();
            Assert.Equal(0, Junction(left));
            Assert.Equal(0, Junction(tleft));

            // structures.dm:21-26: Initialize of a smoothing structure queues itself and its neighbours
            var right = Dm.@new<obj_structure_window_reinforced_fulltile>(locate(5, 5, 1));
            var tright = Dm.@new<obj_structure_table>(locate(5, 8, 1));
            Assert.True(Queued(left) && Queued(right) && Queued(tleft) && Queued(tright));
            Smooth();
            Assert.Equal(Oracle.E, Junction(left));
            Assert.Equal(Oracle.W, Junction(right));
            Assert.Equal(Oracle.E, Junction(tleft));

            // window.dm:401-405 (Destroy -> update_nearby_icons) and structures.dm:28-31 (Destroy)
            qdel(right);
            qdel(tright);
            Assert.True(Queued(left) && Queued(tleft), "the neighbours of a deleted window or table are queued");
            Fire();
            Assert.Equal(0, Junction(left));
            Assert.Equal(0, Junction(tleft));
            Assert.Empty(room.RuntimeText);
        }
    }
}
