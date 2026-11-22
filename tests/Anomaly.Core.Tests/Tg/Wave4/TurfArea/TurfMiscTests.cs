using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>The remaining small procs and vars of package B's files (turf.dm, _open.dm, areas.dm, lighting_area.dm), from the DM.</summary>
public class TurfMiscTests
{
    private static List<string> Lines(TurfAreaWorld t) => TurfAreaTrace.Of(t.World).Lines;

    // ---- RemoveLattice (turf.dm:527-530) ----

    [Fact]
    public void RemoveLatticeDeletesAnInitializedLatticeAndLeavesAnUninitializedOneAlone()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1), other = locate(2, 1, 1);
            var raw = Dm.@new<obj_structure_lattice>(floor);
            floor.RemoveLattice();
            Assert.False(QDELETED(raw));                       // `L.flags_1 & INITIALIZED_1` is false: not deleted

            Dm.combine(ref raw.flags_1, INITIALIZED_1);
            floor.RemoveLattice();
            Assert.True(QDELETED(raw));

            other.RemoveLattice();                              // none there: nothing happens
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- drop_location through the turf (turf.dm:666-668, _atom.dm:609-612) ----

    [Fact]
    public void ATurfAllowsDropsSoAnAtomsDropLocationIsItsTurfOrAContainerThatAllowsIt()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var box = Dm.@new<obj_test_turfarea_sitter>(floor);
            var thing = Dm.@new<obj_test_turfarea_sitter>(box);
            Assert.True(Dm.eq(floor.AllowDrop(), TRUE));
            Assert.Same(floor, box.drop_location());
            Assert.Same(floor, thing.drop_location());   // the box does not allow drops (atom default), so it asks its own loc
        }
    }

    [Fact]
    public void AcidMeltIsABareReturnOnEveryTurfAndTheSmallFlagsAreFalse()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            Waiting.NoSleep(floor.acid_melt());
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- MakeDry (_open.dm:605-607) ----

    [Fact]
    public void MakeDrySendsTheWetSettingTheImmediateFlagAndTheAmountWithTheirDefaults()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var open = (turf_open)locate(1, 1, 1);
            var watch = Dm.@new<datum_test_turfarea_listener>();
            watch.label = "dry";
            watch.RegisterSignal(open, COMSIG_TURF_MAKE_DRY, nameof(datum_test_turfarea_listener.on_any));

            open.MakeDry();
            open.MakeDry(TURF_WET_LUBE, TRUE, 30);

            Assert.Equal(new[]
            {
                "dry turf(1,1,1)(" + Dm.str(TURF_WET_WATER) + "," + Dm.str(FALSE) + "," + Dm.str(INFINITY) + ",null)",
                "dry turf(1,1,1)(" + Dm.str(TURF_WET_LUBE) + "," + Dm.str(TRUE) + ",30,null)",
            }, Lines(t));
        }
    }

    // ---- broken and burnt tiles (_open.dm:62-95) ----

    [Fact]
    public void ABrokenTileWithoutADamageIconIsAStackTraceAndTheBrokenBranchWinsOverBurnt()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var open = (turf_open)locate(1, 1, 1);
            open.broken = TRUE;
            open.burnt = TRUE;
            var overlays = (DmList)Waiting.NoSleep(open.update_overlays());
            Assert.Equal(0, overlays.len);
            Assert.Single(t.Runtimes);
            Assert.Contains("has broken set to TRUE but doesn't have a damaged_dmi set", t.Runtimes[0].Message);   // `else if(burnt)` is not reached

            open.broken = FALSE;
            Waiting.NoSleep(open.update_overlays());
            Assert.Equal(2, t.Runtimes.Count);
            Assert.Contains("has burnt set to TRUE but doesn't have a damaged_dmi set", t.Runtimes[1].Message);
        }
    }

    // ---- /area/golem, lighting vars and the GLOB lists ----

    [Fact]
    public void TheGolemAreaIsABlankAreaWithItsOwnName()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var golem = Dm.@new<area_golem>();
            Assert.Equal("Golem Territory", golem.name);
            Assert.True(Dm.truthy(Dm.bitand(golem.area_flags, VALID_TERRITORY)));   // everything else is /area's
            golem.on_joining_game(null);   // a bare return
        }
    }

    [Fact]
    public void AnAreasBaseLightingStartsOffAndWhite()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            Assert.True(Dm.eq(room.area_has_base_lighting, FALSE));
            Assert.True(Dm.eq(room.base_lighting_alpha, 0));
            Assert.Equal(COLOR_WHITE, room.base_lighting_color);
            Assert.True(Dm.truthy(room.static_lighting));
            // update_base_lighting is the deferred leaf (D24): area_has_base_lighting stays FALSE (lighting_area.dm:39)
            room.update_base_lighting();
            Assert.True(Dm.eq(room.area_has_base_lighting, FALSE));
            Assert.Contains(DmWorldState.Current.DeferredSites, s => s.Contains("lighting_area.dm:39"));
        }
    }

    [Fact]
    public void TheGlobalListsOfTurfsAndTeleportLocationsStartEmpty()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            Assert.Equal(0, GLOB.station_turfs.len);
            Assert.Equal(0, GLOB.teleportlocs.len);
            Assert.NotNull(GLOB.areas);
            Assert.NotNull(GLOB.areas_by_type);
        }
    }

    [Fact]
    public void TheTurfAndAreaTypesCarryTheirDmPaths()
    {
        // rules 2.3: [DmPath] on the part of the earliest .dme file; the engine part carries the BYOND builtin ones
        Assert.Equal("/turf", DmTypes.PathOf(typeof(turf)));
        Assert.Equal("/turf/open", DmTypes.PathOf(typeof(turf_open)));
        Assert.Equal("/turf/open/space", DmTypes.PathOf(typeof(turf_open_space)));
        Assert.Equal("/turf/open/space/basic", DmTypes.PathOf(typeof(turf_open_space_basic)));
        Assert.Equal("/turf/closed", DmTypes.PathOf(typeof(turf_closed)));
        Assert.Equal("/turf/closed/indestructible", DmTypes.PathOf(typeof(turf_closed_indestructible)));
        Assert.Equal("/turf/open/indestructible", DmTypes.PathOf(typeof(turf_open_indestructible)));
        Assert.Equal("/area", DmTypes.PathOf(typeof(area)));
        Assert.Equal("/area/space", DmTypes.PathOf(typeof(area_space)));
        Assert.Equal("/area/misc/testroom", DmTypes.PathOf(typeof(area_misc_testroom)));
        Assert.Equal("/area/golem", DmTypes.PathOf(typeof(area_golem)));
        Assert.Equal("/datum/alarm_handler", DmTypes.PathOf(typeof(datum_alarm_handler)));
        Assert.Equal("/datum/controller/subsystem/area_contents", DmTypes.PathOf(typeof(datum_controller_subsystem_area_contents)));
    }
}
