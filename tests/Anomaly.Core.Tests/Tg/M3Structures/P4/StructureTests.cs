using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.P4;

/// <summary>A random spawner fixture (rules 15) whose loot is two probe types, five picks with repeats.</summary>
[DmPath("/obj/effect/spawner/random/test_p4_loot")]
public class test_p4_loot_spawner : obj_effect_spawner_random
{
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        loot = Dm.list(Dm.kv(typeof(test_p4_live_probe), 3), Dm.kv(typeof(test_p4_coloured_probe), 1));
        spawn_loot_count = 5;
    }
}

// M3 P4's smoke tests of the structures, spawners, decals and the undertile element (tasks 4-7).
public class StructureTests
{
    private static TgToyRoom Room(int size = 9, ulong seed = 21) => TgToyRoom.Create(size, size, seed: seed);

    [Fact]
    public void ADirectionalWindowBlocksOnlyItsFace()
    {
        var room = Room();
        using (room.World.Enter())
        {
            turf at = locate(4, 4, 1);
            var window = Dm.@new<obj_structure_window>(at, NORTH);
            Assert.Equal((float)NORTH, window.dir);
            var mover = Dm.@new<test_p4_live_probe>(at);

            // CanAllowThrough by border_dir (window.dm:133-151)
            Assert.False(Dm.truthy(Waiting.NoSleep(window.CanAllowThrough(mover, NORTH))));
            Assert.True(Dm.truthy(Waiting.NoSleep(window.CanAllowThrough(mover, EAST))));
            Assert.True(Dm.truthy(Waiting.NoSleep(window.CanAllowThrough(mover, SOUTH))));
            // on_exit by the leaving direction (window.dm:153-170)
            Assert.True(Dm.eq(window.on_exit(at, mover, NORTH), COMPONENT_ATOM_BLOCK_EXIT));
            Assert.Null(window.on_exit(at, mover, EAST));

            // through the move chain: leaving north is blocked, leaving east is not
            Assert.False(Dm.truthy(Waiting.NoSleep(mover.Move(locate(4, 5, 1), NORTH))));
            Assert.Same(at, mover.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(mover.Move(locate(5, 4, 1), EAST))));
            // entering from the north is blocked, from the east it is not
            var from_north = Dm.@new<test_p4_live_probe>(locate(4, 5, 1));
            Assert.False(Dm.truthy(Waiting.NoSleep(from_north.Move(at, SOUTH))));
            Assert.True(Dm.truthy(Waiting.NoSleep(mover.Move(at, WEST))));
        }
    }

    [Fact]
    public void AFulltileWindowBlocksEverySideButLetsItsTileBeLeft()
    {
        var room = Room();
        using (room.World.Enter())
        {
            turf at = locate(4, 4, 1);
            var window = Dm.@new<obj_structure_window_fulltile>(at);
            var mover = Dm.@new<test_p4_live_probe>(locate(5, 4, 1));
            foreach (float d in new[] { NORTH, SOUTH, EAST, WEST })
                Assert.False(Dm.truthy(Waiting.NoSleep(window.CanAllowThrough(mover, d))));
            Assert.Null(window.on_exit(at, mover, NORTH)); // window.dm:166: a fulltile window never blocks the exit
            Assert.False(Dm.truthy(Waiting.NoSleep(mover.Move(at, WEST))));
        }
    }

    [Fact]
    public void ARailingBlocksItsFaceButNotAThrownMover()
    {
        var room = Room();
        using (room.World.Enter())
        {
            turf at = locate(4, 4, 1);
            var railing = Dm.@new<obj_structure_railing>(at);
            Waiting.NoSleep(railing.setDir(EAST));
            var mover = Dm.@new<test_p4_live_probe>(at);
            Assert.True(Dm.eq(railing.on_exit(at, mover, EAST), COMPONENT_ATOM_BLOCK_EXIT));
            Assert.Null(railing.on_exit(at, mover, NORTH));
            Assert.False(Dm.truthy(Waiting.NoSleep(railing.CanPass(mover, EAST))));
            Assert.True(Dm.truthy(Waiting.NoSleep(railing.CanPass(mover, NORTH))));
            mover.throwing = new datum_thrownthing(); // only its truth is read here
            Assert.Null(railing.on_exit(at, mover, EAST)); // railings.dm:157-158
        }
    }

    [Fact]
    public void AWindowSpawnerMakesAGrilleAndItsWindowAndDeletesItself()
    {
        var room = Room();
        using (room.World.Enter())
        {
            turf at = locate(4, 4, 1);
            var spawner = Dm.@new<obj_effect_spawner_structure_window_reinforced>(at);
            Assert.True(QDELETED(spawner)); // INITIALIZE_HINT_QDEL (misc.dm:34)
            Assert.NotNull(Dm.locate<obj_structure_grille>(at));
            var window = Dm.locate<obj_structure_window_reinforced_fulltile>(at);
            Assert.NotNull(window);
            Assert.False(InertSeam.IsInert(window));
            Assert.True(Dm.eq(at.rcd_memory, RCD_MEMORY_WINDOWGRILLE)); // structure.dm:29-30
        }
    }

    [Fact]
    public void ARandomSpawnerIsDeterministicPerSeed()
    {
        static string Loot(ulong seed)
        {
            var room = Room(seed: seed);
            using (room.World.Enter())
            {
                turf at = locate(4, 4, 1);
                Dm.@new<test_p4_loot_spawner>(at);
                return string.Join(",", Dm.each_cast<atom>(at).Select(a => a.GetType().Name));
            }
        }
        string first = Loot(3);
        Assert.Equal(first, Loot(3));
        Assert.Equal(5, first.Split(',').Length); // five picks with repeats (spawn_loot_double)
        Assert.Contains(Enumerable.Range(4, 12).Select(s => Loot((ulong)s)), other => other != first); // the world RNG decides
    }

    [Fact]
    public void ATurfDecalGoesIntoItsTurfsOverlaysAndDeletesItself()
    {
        var room = Room();
        using (room.World.Enter())
        {
            turf at = locate(4, 4, 1);
            var decal = Dm.@new<obj_effect_turf_decal_tile_blue>(at);
            Assert.True(room.Runtimes.Count == 0, string.Join("\n", room.RuntimeText));
            Assert.True(QDELETED(decal)); // INITIALIZE_HINT_QDEL (decal.dm:83)
            Assert.Null(decal.loc);
            Assert.Contains(Dm.each_cast<object>(at.overlays), o => o is IDmAppearance a && a.icon_state == decal.icon_state);
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void AnUndertileObjectUnderAFloorTileIsInvisible()
    {
        var room = Room();
        using (room.World.Enter())
        {
            var floor = Dm.@new<turf_open_floor_iron>(locate(4, 4, 1));
            var pipe = Dm.@new<test_p4_live_probe>(floor);
            Waiting.NoSleep(pipe.AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE));
            floor.levelupdate(); // turf.dm:517: COMSIG_OBJ_HIDE with the floor's UNDERFLOOR_HIDDEN
            Assert.True(Dm.eq(pipe.invisibility, INVISIBILITY_MAXIMUM)); // undertile.dm:41-42
            Assert.True(pipe.invisibility > 25); // D115: not an entity
            Assert.True(Dm.eq(pipe.alpha, 128));
            Assert.True(HAS_TRAIT(pipe, TRAIT_T_RAY_VISIBLE));
            Assert.True(Dm.eq(pipe.layer, BELOW_CATWALK_LAYER));

            // on plating (UNDERFLOOR_INTERACTABLE) it shows again
            if (Dm.truthy(pipe._listen_lookup?[COMSIG_OBJ_HIDE]))
                pipe._SendSignal(COMSIG_OBJ_HIDE, Dm.list(pipe, UNDERFLOOR_INTERACTABLE));
            Assert.True(Dm.eq(pipe.invisibility, 0));
            Assert.True(Dm.eq(pipe.alpha, 255));
            Assert.False(HAS_TRAIT(pipe, TRAIT_T_RAY_VISIBLE));
        }
    }

    [Fact]
    public void SSatomsWorldWalkHidesTheInfrastructureUnderFloorTilesAndShowsItOnPlating()
    {
        // The map's atoms are made during the boot's map step and wait for SSatoms, whose InitializeAtoms walks the world
        // (atoms.dm:62): the undertile element is attached by each pipe's own Initialize, not by the test.
        var built = new List<(atom thing, bool covered)>();
        var room = TgToyRoom.Boot(9, 9, game: false, mapExtras: _ =>
        {
            turf covered = Dm.@new<turf_open_floor_iron>(locate(3, 3, 1));
            turf covered_2 = Dm.@new<turf_open_floor_iron>(locate(4, 3, 1));
            turf covered_3 = Dm.@new<turf_open_floor_iron_dark>(locate(5, 3, 1));
            turf exposed = Dm.@new<turf_open_floor_plating>(locate(3, 5, 1));
            foreach (turf t in new[] { covered, exposed })
            {
                built.Add((Dm.@new<obj_structure_cable>(t), t == covered));
                built.Add((Dm.@new<obj_structure_disposalpipe_segment>(t), t == covered));
                built.Add((Dm.@new<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_hidden>(t), t == covered));
            }
            built.Add((Dm.@new<obj_structure_disposalpipe_trunk>(covered_2), true));
            built.Add((Dm.@new<obj_machinery_atmospherics_pipe_layer_manifold_supply_hidden>(covered_2), true));
            built.Add((Dm.@new<obj_structure_cable_multilayer_connected>(covered_3), true));
            built.Add((Dm.@new<obj_structure_disposalpipe_sorting_mail>(covered_3), true));
            built.Add((Dm.@new<obj_machinery_atmospherics_pipe_bridge_pipe_supply_hidden>(covered_3), true));
            foreach (var (thing, _) in built)
                Assert.False(Dm.truthy(Dm.bitand(thing.flags_1, INITIALIZED_1))); // waiting for SSatoms
        });
        using (room.World.Enter())
        {
            Assert.True(room.Runtimes.Count == 0, string.Join("\n", room.Runtimes));
            Assert.Equal(11, built.Count);
            foreach (var (thing, covered) in built)
            {
                string name = thing.GetType().Name;
                Assert.False(InertSeam.IsInert(thing), name);
                Assert.True(Dm.truthy(Dm.bitand(thing.flags_1, INITIALIZED_1)), name); // SSatoms' walk initialized it
                if (covered)
                {
                    Assert.True(Dm.eq(thing.invisibility, INVISIBILITY_MAXIMUM), name); // undertile.dm:41-42
                    Assert.True(thing.invisibility > 25, name); // D115: not an entity
                    Assert.True(HAS_TRAIT(thing, TRAIT_T_RAY_VISIBLE), name);
                }
                else
                {
                    Assert.True(Dm.eq(thing.invisibility, 0), name); // on plating it shows
                    Assert.False(HAS_TRAIT(thing, TRAIT_T_RAY_VISIBLE), name);
                }
            }
        }
    }

    [Fact]
    public void TheSpawnersScatterLinesAndViewFollowTg()
    {
        var room = Room();
        using (room.World.Enter())
        {
            // get_line (maths.dm:46): both ends, Bresenham's steps with tg's half-distance counters
            var line = Dm.each_cast<turf>(get_line(locate(2, 2, 1), locate(5, 3, 1))).Select(t => (t.x, t.y)).ToArray();
            Assert.Equal(new[] { (2f, 2f), (3f, 2f), (4f, 2f), (5f, 3f) }, line);

            // view (OpenDream's view): the wall is seen, the tile straight behind it is not, the open tiles are
            var wall = Dm.@new<turf_closed_wall>(locate(5, 4, 1));
            var seen = Dm.each_cast<object>(view(3, locate(4, 4, 1))).ToList();
            Assert.Same(locate(4, 4, 1), seen[0]); // the centre first
            Assert.Contains(wall, seen);
            Assert.DoesNotContain(locate(6, 4, 1), seen);
            Assert.Contains(locate(4, 6, 1), seen);
            Assert.Contains(locate(3, 4, 1), seen);
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void TablesSmoothWithEachOther()
    {
        var room = Room();
        using (room.World.Enter())
        {
            var west = Dm.@new<obj_structure_table>(locate(4, 4, 1));
            var east = Dm.@new<obj_structure_table>(locate(5, 4, 1));
            Waiting.NoSleep(SSicon_smooth.Initialize());
            Assert.Equal("table-" + EAST_JUNCTION, west.icon_state);
            Assert.Equal("table-" + WEST_JUNCTION, east.icon_state);
        }
    }
}
