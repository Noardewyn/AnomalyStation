using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// Smoke tests of package B's translation (wave 4: game/turfs/turf.dm, open/_open.dm, closed/_closed.dm,
/// game/area/areas.dm, controllers/subsystem/area_contents.dm and the partial files): the translated code runs on a toy
/// map and does what tg's code says. The behaviour tests are the independent test-writer's.
/// </summary>
public class TurfAreaSmokeTests
{
    [Fact]
    public void TheToyRoomInitializesItsTurfsAndItsAreaThroughSSatoms()
    {
        var t = new TurfAreaWorld(3, 2, 1);
        using (t.World.Enter())
        {
            var room = (area)locate(1, 1, 1).loc;
            // /area/New (areas.dm:177): the unique area registers itself, gets its energy list and its alarm handler
            Assert.Same(room, GLOB.areas_by_type[typeof(area_misc_testroom)]);
            Assert.True(Dm.@in(room, GLOB.areas));
            Assert.Equal(AREA_USAGE_LEN, room.energy_usage.len);
            Assert.Same(room, room.alarm_manager.source_atom);

            TurfAreaWorld.InitializeSSatoms();

            foreach (turf T in Dm.each<turf>(block(1, 1, 1, 3, 2, 1)))
            {
                Assert.IsType<turf_open_indestructible>(T);
                Assert.True(Dm.truthy(Dm.bitand(T.flags_1, INITIALIZED_1)));
                // a single baseturf: assemble_baseturfs keeps the type (turf.dm:497-500)
                Assert.Same(typeof(turf_baseturf_bottom), T.baseturfs);
            }
            // /area/Initialize (areas.dm:195): the testroom needs no power and has no static lighting
            Assert.True(Dm.truthy(Dm.bitand(room.flags_1, INITIALIZED_1)));
            Assert.Equal("", room.icon_state);
            Assert.Equal(BLEND_MULTIPLY, room.blend_mode);
            Assert.True(Dm.eq(room.power_light, TRUE));
            Assert.True(Dm.eq(room.luminosity, 1));
            Assert.Empty(t.Runtimes);
            // the deferred leaves on this path: the area's base lighting (D24), reached through LateInitialize, power_change and
            // update_appearance; the weather loop of /area/update_icon is real since gaps E2a; the open turfs' air is real since M2 P2
            // (LINDA_turf_tile.dm:81, TurfBaseTests)
            IReadOnlyList<string> sites = DmWorldState.Current.DeferredSites;
            Assert.DoesNotContain(sites, s => s.Contains("LINDA_turf_tile.dm:81"));
            Assert.Contains(sites, s => s.Contains("lighting_area.dm:39"));
            Assert.DoesNotContain(sites, s => s.Contains("areas.dm:467"));
        }
    }

    [Fact]
    public void AWallAssemblesItsBaseturfsAsAStringList()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            TurfAreaWorld.InitializeSSatoms();
            // /turf/closed/indestructible's baseturf is the plating, whose own baseturf is the bottom (turf.dm:501-513)
            var baseturfs = wall.baseturfs as DmList;
            Assert.NotNull(baseturfs);
            Assert.Equal(2, baseturfs.len);
            Assert.Same(typeof(turf_baseturf_bottom), baseturfs[1f]);
            Assert.Same(typeof(turf_open_indestructible_plating), baseturfs[2f]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ChangeAreaMovesTheTurfAndRecordsItInBothAreasLists()
    {
        var t = new TurfAreaWorld(2, 2, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            turf T = locate(2, 1, 1);
            var space = (area)T.loc;
            var room = Dm.@new<area_misc_testroom>();
            Assert.Null(room.loc);

            T.change_area(space, room);

            Assert.Same(room, T.loc);
            Assert.True(Dm.@in(T, room.contents));
            Assert.False(Dm.@in(T, space.contents));
            Assert.True(Dm.@in(T, (DmList)room.turfs_by_zlevel[1f]));
            Assert.True(Dm.@in(T, (DmList)space.turfs_to_uncontain_by_zlevel[1f]));
            Assert.True(Dm.eq(room.has_contained_turfs(), TRUE));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TurfEnterBumpsADenseTurfAndLetsAPhasingMoverThroughAfterTheBump()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            var mover = Dm.@new<obj_test_turfarea_mover>(locate(1, 1, 1));

            // CanPass fails on the dense wall: the wall is the first bump, and Enter returns the mover's PHASING bit
            Assert.True(Dm.eq(Waiting.NoSleep(wall.Enter(mover)), 0));
            Assert.Equal(new atom[] { wall }, mover.bumped);

            // a phasing mover passes CanPass (_atom.dm:230), so nothing is bumped and Enter is TRUE
            mover.bumped.Clear();
            mover.movement_type = PHASING;
            Assert.True(Dm.eq(Waiting.NoSleep(wall.Enter(mover)), TRUE));
            Assert.Empty(mover.bumped);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TurfEnterBumpsTheHighestRefusingOccupant()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            turf floor = locate(2, 1, 1);
            var low = Dm.@new<obj_test_turfarea_blocker>(floor);
            var high = Dm.@new<obj_test_turfarea_blocker>(floor);
            high.layer = low.layer + 1;
            var mover = Dm.@new<obj_test_turfarea_mover>(locate(1, 1, 1));

            Assert.True(Dm.eq(Waiting.NoSleep(floor.Enter(mover)), 0));
            Assert.Equal(new atom[] { high }, mover.bumped);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void SSareaContentsTrimsAnAreaWithMoreThanAHundredLooseTurfs()
    {
        var t = new TurfAreaWorld(11, 11, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = (area)locate(1, 1, 1).loc;
            // what SSmapping's build_area_turfs does for a z level of default tiles (controllers/subsystem/mapping.dm:779-781)
            space.turfs_by_zlevel.len = 1;
            space.turfs_by_zlevel[1f] = block(1, 1, 1, 11, 11, 1);
            var room = Dm.@new<area_misc_testroom>();
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, 11, 11, 1)))
                T.change_area(space, room);
            Assert.Equal(121, ((DmList)space.turfs_to_uncontain_by_zlevel[1f]).len);

            var ss = Dm.@new<datum_controller_subsystem_area_contents>();
            Assert.Same(ss, SSarea_contents);
            ss.state = SS_RUNNING; // as the MC sets it before fire (MC_TICK_CHECK pauses a subsystem that is not running)
            Waiting.NoSleep(ss.fire(FALSE));

            Assert.Equal(0, ((DmList)space.turfs_by_zlevel[1f]).len);
            Assert.Equal(0, space.turfs_to_uncontain_by_zlevel.len);
            Assert.Equal(0, ss.marked_for_clearing.len);
            Assert.Equal(0, ss.currentrun.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void SpaceTurfsInitializeThroughTheirOwnInitialize()
    {
        var t = new TurfAreaWorld(2, 1, 1, turfType: typeof(turf_open_space_basic), areaType: typeof(area_space));
        using (t.World.Enter())
        {
            // /turf/open/space/basic/New does not call ..() (space.dm:79), so /atom/New never runs for it; SSatoms then
            // initializes every atom of the world that is not initialized yet, through space's own Initialize
            turf basic = locate(1, 1, 1);
            var space = Dm.@new<turf_open_space>(locate(2, 1, 1));
            TurfAreaWorld.InitializeSSatoms();
            Assert.True(Dm.truthy(Dm.bitand(basic.flags_1, INITIALIZED_1)));
            Assert.True(Dm.truthy(Dm.bitand(space.flags_1, INITIALIZED_1)));
            // air = space_gas (space_EXPENSIVE.dm:15): the static is made by TgState.InitStatics, which this world without a
            // boot never ran, so it is null here (HealAndProgressBarTests checks a booted world's space air)
            Assert.Same(turf_open_space.space_gas, ((turf_open_space)space).air);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AreaEnteredAndExitedSendTheirSignals()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = (area)locate(1, 1, 1).loc;
            var mover = Dm.@new<obj_test_turfarea_mover>();
            Assert.True(Dm.eq(Waiting.NoSleep(room.Entered(mover, null, null)), null));
            Assert.True(Dm.eq(Waiting.NoSleep(room.Exited(mover, NORTH)), null));
            Assert.Empty(t.Runtimes);
        }
    }
}

/// <summary>SSarea_contents, a live subsystem (SS_NO_INIT, runlevels lobby and default), in the booted world.</summary>
public class AreaContentsBootTests
{
    [Fact]
    public void SSareaContentsIsCreatedAtBootAndFiresInTheLobby()
    {
        World w = Anomaly.Core.Tests.Tg.Boot.TgTestWorld.Start(out _);
        var runtimes = new List<Exception>();
        w.RuntimeErrorSink = runtimes.Add;
        w.Step(100);
        using (w.Enter())
        {
            Assert.NotNull(SSarea_contents);
            Assert.True(Dm.@in(SSarea_contents, Master.subsystems));
            Assert.Equal("Area Contents", SSarea_contents.name);
            Assert.True(Dm.eq(SSarea_contents.runlevels, Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT)));
            Assert.True(SSarea_contents.times_fired > 0);
            Assert.Equal(0, SSarea_contents.currentrun.len); // every area scanned, none with more than 100 loose turfs
        }
        Assert.Empty(runtimes);
    }
}
