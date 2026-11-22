using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.T2;

/// <summary>A plain movable that crosses and is crossed (an /obj with no density), the mover of the passage tests.</summary>
[DmPath("/obj/t2_mover")]
public class obj_t2_mover : obj
{
}

/// <summary>
/// M3 T2 phase B: what a real <c>Move</c> (turf.dm:434 Enter and its Cross chain, the Exit chain with the COMSIG_ATOM_EXIT
/// handlers) lets through a cell that holds a window, grille, railing or windoor, against tg's rules written down here from
/// window.dm:133-170, grille.dm:201-204, railings.dm:134-166 and windowdoor.dm. The faces and solidity the server's collision
/// (and, through the same function, P5's replicated cell flags) use are compared with the same table.
/// </summary>
public class PassageTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;

    public PassageTests(MetaWorldFixture meta) => _meta = meta;

    private const int DN = 1, DS = 2, DE = 4, DW = 8, DNE = 5, DNW = 9, DSE = 6, DSW = 10;
    private const int C = 5; // the obstacle's cell is (5, 5) in a 9 x 9 room

    private static readonly int[] Cardinals = { DN, DS, DE, DW };

    private static (int dx, int dy) Step(int dir) => dir switch
    {
        DN => (0, 1),
        DS => (0, -1),
        DE => (1, 0),
        _ => (-1, 0),
    };

    private static int Opposite(int dir) => dir switch { DN => DS, DS => DN, DE => DW, _ => DE };

    private static TgToyRoom Room() => TgToyRoom.Create(9, 9);

    /// <summary>
    /// Whether a mover standing on the neighbour toward <paramref name="side"/> of the obstacle's cell gets in: it moves one
    /// step toward the cell. The mover is made fresh and the room is left as it was.
    /// </summary>
    private static bool Enters(int side, Action<obj_t2_mover> configure = null)
    {
        (int dx, int dy) = Step(side);
        var mover = Dm.@new<obj_t2_mover>(locate(C + dx, C + dy, 1));
        configure?.Invoke(mover);
        Waiting.NoSleep(mover.Move(locate(C, C, 1), Opposite(side)));
        bool entered = mover.loc == locate(C, C, 1);
        qdel(mover);
        return entered;
    }

    /// <summary>Whether a mover standing in the obstacle's cell gets out toward <paramref name="toward"/>.</summary>
    private static bool Leaves(int toward, Action<obj_t2_mover> configure = null)
    {
        var mover = Dm.@new<obj_t2_mover>(locate(C, C, 1));
        configure?.Invoke(mover);
        (int dx, int dy) = Step(toward);
        Waiting.NoSleep(mover.Move(locate(C + dx, C + dy, 1), toward));
        bool left = mover.loc == locate(C + dx, C + dy, 1);
        qdel(mover);
        return left;
    }

    /// <summary>The sides from which a mover cannot enter, and the directions in which it cannot leave, by a real Move.</summary>
    private static (int enterBlocked, int exitBlocked) Blocked(Action<obj_t2_mover> configure = null)
    {
        int enter = 0, exit = 0;
        foreach (int d in Cardinals)
        {
            if (!Enters(d, configure)) enter |= d;
            if (!Leaves(d, configure)) exit |= d;
        }
        return (enter, exit);
    }

    private static TileEdges Edges(int dirs) => TileFaces.OfDir(dirs);

    private static void AssertSeam(World world, int solid, int faces, string who)
    {
        Assert.Equal(solid != 0, LocomotionSeam.IsSolid(world, C, C, 1));
        Assert.Equal(Edges(faces), LocomotionSeam.FacesOf(world, C, C, 1));
        // the controller's view (the server's collision) is the same function
        Assert.Equal(Edges(faces), LocomotionSeam.CollisionView(world).EdgesOf(C, C, 1));
        foreach (int d in Cardinals)
        {
            (int dx, int dy) = Step(d);
            // the face between the cell and its neighbour toward d: the cell's own face d, or the neighbour's face back
            Assert.True((faces & d) != 0 == LocomotionSeam.IsFaceBlocked(world, C, C, 1, (byte)d), $"{who}: face toward {d} of the cell");
            Assert.True((faces & d) != 0 == LocomotionSeam.IsFaceBlocked(world, C + dx, C + dy, 1, (byte)Opposite(d)), $"{who}: the neighbour's face back, from {d}");
        }
    }

    [Theory]
    [InlineData(DN)]
    [InlineData(DS)]
    [InlineData(DE)]
    [InlineData(DW)]
    public void A_directional_window_blocks_entering_from_its_side_and_leaving_toward_it(int dir)
    {
        var room = Room();
        using (room.World.Enter())
        {
            var window = Dm.@new<obj_structure_window>(locate(C, C, 1), (float)dir);
            Assert.True(Dm.truthy(Dm.bitand(window.flags_1, ON_BORDER_1)));
            Assert.True(Dm.truthy(window.density));
            // CanAllowThrough: `if(border_dir == dir) return FALSE` (window.dm:140), else TRUE; on_exit: `direction == dir && density`
            (int enter, int exit) = Blocked();
            Assert.Equal(dir, enter);
            Assert.Equal(dir, exit);
            AssertSeam(room.World, 0, dir, "window");

            // pass_flags_self = PASSGLASS: atom/CanAllowThrough returns TRUE (_atom.dm:246) and on_exit returns (window.dm:162)
            (int enter_glass, int exit_glass) = Blocked(m => m.pass_flags = PASSGLASS);
            Assert.Equal(0, enter_glass);
            Assert.Equal(0, exit_glass);

            // a window that is not dense blocks nothing and is no face
            window.density = 0f;
            (int enter_open, int exit_open) = Blocked();
            Assert.Equal(0, enter_open);
            Assert.Equal(0, exit_open);
            AssertSeam(room.World, 0, 0, "non-dense window");
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void A_full_tile_window_and_a_grille_block_every_entry_but_not_leaving_and_make_the_tile_solid()
    {
        var room = Room();
        using (room.World.Enter())
        {
            var window = Dm.@new<obj_structure_window_reinforced_fulltile>(locate(C, C, 1));
            Assert.False(Dm.truthy(Dm.bitand(window.flags_1, ON_BORDER_1)));
            // fulltile: CanAllowThrough returns FALSE for every side (window.dm:137), on_exit returns early (:164)
            (int enter, int exit) = Blocked();
            Assert.Equal(DN | DS | DE | DW, enter);
            Assert.Equal(0, exit);
            AssertSeam(room.World, 1, 0, "fulltile window");
            Assert.Equal(0, Blocked(m => m.pass_flags = PASSGLASS).enterBlocked); // pass_flags_self
            qdel(window);

            var grille = Dm.@new<obj_structure_grille>(locate(C, C, 1));
            // grille.dm:201: only a projectile may get through (30%); a plain mover is stopped from every side, and leaves freely
            (enter, exit) = Blocked();
            Assert.Equal(DN | DS | DE | DW, enter);
            Assert.Equal(0, exit);
            AssertSeam(room.World, 1, 0, "grille");
            Assert.Equal(0, Blocked(m => m.pass_flags = PASSGRILLE).enterBlocked);
            Assert.Empty(room.RuntimeText);
        }
    }

    [Theory]
    [InlineData(DN)]
    [InlineData(DS)]
    [InlineData(DE)]
    [InlineData(DW)]
    [InlineData(DNE)]
    [InlineData(DNW)]
    [InlineData(DSE)]
    [InlineData(DSW)]
    public void A_railing_blocks_the_sides_its_dir_names_both_ways(int dir)
    {
        var room = Room();
        using (room.World.Enter())
        {
            var railing = Dm.@new<obj_structure_railing>(locate(C, C, 1));
            Waiting.NoSleep(railing.setDir((float)dir));
            Assert.True(Dm.truthy(Dm.bitand(railing.flags_1, ON_BORDER_1)));
            // CanPass (railings.dm:134): `if(border_dir & dir) return . || throwing || NOT_TOUCHING_GROUND`, else TRUE;
            // on_exit (:145): `if(!(direction & dir)) return`, then density, throwing, movement_type, move_force
            (int enter, int exit) = Blocked();
            int expected = dir & (DN | DS | DE | DW);
            Assert.Equal(expected, enter);
            Assert.Equal(expected, exit);
            AssertSeam(room.World, 0, expected, "railing");

            // a flying mover (MOVETYPES_NOT_TOUCHING_GROUND) is let through both ways
            (int enter_fly, int exit_fly) = Blocked(m => m.movement_type = FLYING);
            Assert.Equal(0, enter_fly);
            Assert.Equal(0, exit_fly);
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void A_corner_railing_is_a_border_piece_but_not_dense_so_blocks_nothing_and_is_no_face()
    {
        var room = Room();
        using (room.World.Enter())
        {
            // railings.dm:31-33: /corner has `density = FALSE` and keeps its parent's ON_BORDER_1: Initialize adds no exit handler
            // (`if(density && flags_1 & ON_BORDER_1)`, :57), CanPass is the parent's `!density`, and no dense piece is no face
            var corner = Dm.@new<obj_structure_railing_corner>(locate(C, C, 1));
            Assert.True(Dm.truthy(Dm.bitand(corner.flags_1, ON_BORDER_1)));
            Assert.False(Dm.truthy(corner.density));
            (int enter, int exit) = Blocked();
            Assert.Equal(0, enter);
            Assert.Equal(0, exit);
            AssertSeam(room.World, 0, 0, "corner railing");
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void Plastic_flaps_stop_a_standing_mob_from_every_side_and_let_everything_else_in()
    {
        var room = Room();
        using (room.World.Enter())
        {
            var flaps = Dm.@new<obj_structure_plasticflaps>(locate(C, C, 1));
            Assert.False(Dm.truthy(flaps.density)); // plasticflaps.dm:11: no density: not a solid tile
            Assert.False(Dm.truthy(Dm.bitand(flaps.flags_1, ON_BORDER_1)));
            AssertSeam(room.World, 0, 0, "plastic flaps");
            var obj_mover = Dm.@new<obj_t2_mover>(locate(C - 1, C, 1));
            // plasticflaps.dm:203-247, by what the mover is
            Assert.True(Dm.truthy(Waiting.NoSleep(flaps.CanAllowThrough(obj_mover, DW)))); // a plain object: `return .` (!density)
            var standing = Dm.@new<mob_living>(locate(C - 1, C, 1));
            Assert.False(Dm.truthy(Waiting.NoSleep(flaps.CanAllowThrough(standing, DW)))); // STANDING_UP and not tiny
            foreach (int side in Cardinals)
                Assert.False(Dm.truthy(Waiting.NoSleep(flaps.CanAllowThrough(standing, side))), $"side {side}");
            standing.body_position = LYING_DOWN;
            Assert.True(Dm.truthy(Waiting.NoSleep(flaps.CanAllowThrough(standing, DW)))); // crawling under
            standing.body_position = STANDING_UP;
            standing.mob_size = MOB_SIZE_TINY;
            Assert.True(Dm.truthy(Waiting.NoSleep(flaps.CanAllowThrough(standing, DW)))); // too small to be stopped
            standing.mob_size = MOB_SIZE_HUMAN;
            standing.pass_flags = PASSFLAPS;
            Assert.True(Dm.truthy(Waiting.NoSleep(flaps.CanAllowThrough(standing, DW)))); // engineered to cross flaps
            standing.pass_flags = 0;
            flaps.require_resting = 0f;
            Assert.True(Dm.truthy(Waiting.NoSleep(flaps.CanAllowThrough(standing, DW)))); // the cold room's kitchen flaps
            Assert.Empty(room.RuntimeText);
        }
    }

    [Theory]
    [InlineData(DN)]
    [InlineData(DS)]
    [InlineData(DE)]
    [InlineData(DW)]
    public void A_closed_windoor_blocks_its_face_only_and_an_open_one_nothing(int dir)
    {
        var room = Room();
        using (room.World.Enter())
        {
            var windoor = Dm.@new<obj_machinery_door_window>(locate(C, C, 1));
            Waiting.NoSleep(windoor.setDir((float)dir));
            Assert.True(Dm.truthy(Dm.bitand(windoor.flags_1, ON_BORDER_1)));
            Assert.True(Dm.truthy(windoor.density));
            // windowdoor.dm:196-211 CanAllowThrough (`border_dir == dir` is blocked) and its exit handler (connect_loc, :58)
            (int enter, int exit) = Blocked();
            Assert.Equal(dir, enter);
            Assert.Equal(dir, exit);
            AssertSeam(room.World, 0, dir, "closed windoor");

            // an open windoor has no density: nothing blocks, no face
            windoor.density = 0f;
            (enter, exit) = Blocked();
            Assert.Equal(0, enter);
            Assert.Equal(0, exit);
            AssertSeam(room.World, 0, 0, "open windoor");
            Assert.True(room.Runtimes.All(r => !r.Message.Contains("windowdoor", StringComparison.Ordinal)), string.Join("\n", room.RuntimeText));
        }
    }

    [MetaFact]
    public void Every_dense_border_piece_of_Meta_blocks_the_sides_of_its_dir_and_no_others()
    {
        World world = _meta.World;
        int pieces = 0;
        var wrong = new List<string>();
        using (world.Enter())
        {
            var mover = Dm.@new<obj_t2_mover>(null); // in nullspace: a mover for CanAllowThrough, on no tile
            for (int y = 1; y <= Dm.world.maxy; y++)
            {
                for (int x = 1; x <= Dm.world.maxx; x++)
                {
                    TileEdges seam = LocomotionSeam.FacesOf(world, x, y, 1);
                    TileEdges union = TileEdges.None;
                    foreach (atom_movable piece in locate(x, y, 1).contents.Items().Cast<atom_movable>())
                    {
                        if (piece is mob || !Dm.truthy(Dm.bitand(piece.flags_1, ON_BORDER_1)) || !Dm.truthy(piece.density)) continue;
                        pieces++;
                        int dir = (int)piece.dir;
                        union |= Edges(dir);
                        // CanPass of the live types (window.dm:133 via CanAllowThrough, railings.dm:134, windowdoor.dm:196): by border_dir
                        if (InertSeam.IsInert(piece)) continue;
                        foreach (int side in Cardinals)
                        {
                            bool allowed = Dm.truthy(Waiting.NoSleep(piece.CanPass(mover, (float)side))); // what Cross asks (atoms_movable.dm:939)
                            if (allowed == ((dir & side) != 0)) // blocked exactly on the sides of its dir
                                wrong.Add($"{DmTypes.PathOf(piece.GetType())} at ({x},{y}) dir {dir}: from side {side} {(allowed ? "passes" : "is stopped")}");
                        }
                    }
                    if (seam != union) wrong.Add($"({x},{y}): the server's faces are {seam}, the dense border pieces give {union}");
                }
            }
        }
        Assert.True(pieces > 300, $"{pieces} dense border pieces");
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(15)));
    }
}
