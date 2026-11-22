using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Contracts;

/// <summary>
/// What the W4.0 contracts translated besides declarations, written from tg's code: REVERSE_DIR
/// (__DEFINES/directional.dm:29), GET_TURF_ABOVE/BELOW (__DEFINES/multiz.dm), can_atmos_pass (LINDA_system.dm:5),
/// string_assoc_list (__HELPERS/string_assoc_lists.dm:6), the wave-4 is_* tests, the inert stub subsystems and the GLOB
/// lists of _globalvars/lists/mobs.dm, mapping.dm and ambience.dm.
/// </summary>
public class W40TranslatedHelperTests
{
    [Fact]
    public void ReverseDirFlipsEachAxisPairUpAndDownIncluded()
    {
        Assert.Equal(SOUTH, REVERSE_DIR(NORTH));
        Assert.Equal(NORTH, REVERSE_DIR(SOUTH));
        Assert.Equal(WEST, REVERSE_DIR(EAST));
        Assert.Equal(EAST, REVERSE_DIR(WEST));
        Assert.Equal(SOUTHWEST, REVERSE_DIR(NORTHEAST));
        Assert.Equal(NORTHWEST, REVERSE_DIR(SOUTHEAST));
        Assert.Equal(DOWN, REVERSE_DIR(UP));
        Assert.Equal(UP, REVERSE_DIR(DOWN));
        Assert.Equal(0f, REVERSE_DIR(0));
    }

    [Fact]
    public void CanAtmosPassFollowsTheAtomsSetting()
    {
        // M4b slice 1: /turf/open overrides the proc (LINDA_system.dm:22, the turf pair's check; AirMovesTests), so /atom's
        // ATMOS_PASS_PROC case is shown on an object
        var box = new obj_test_w40_block();
        box.can_atmos_pass = ATMOS_PASS_PROC;
        Assert.True(Dm.eq(box.can_atmos_pass__proc(), ATMOS_PASS_YES));    // ATMOS_PASS_PROC on /atom's proc
        box.can_atmos_pass = ATMOS_PASS_DENSITY;
        Assert.True(Dm.eq(box.can_atmos_pass__proc(), FALSE));              // dense
        box.can_atmos_pass = ATMOS_PASS_NO;
        Assert.True(Dm.eq(box.can_atmos_pass__proc(), ATMOS_PASS_NO));
    }

    [Fact]
    public void AirUpdateTurfDoesNothingBeforeSsairInitializes()
    {
        // M4b slice 1: translated (LINDA_system.dm:166): before SSair initializes it returns at once, as in tg
        var w = new World();
        using (w.Enter())
        {
            Dm.@new<datum_controller_subsystem_air>();
            new obj_test_w40_block().air_update_turf(TRUE, FALSE);
            Assert.Equal(0, SSair.active_turfs.len);
            Assert.Equal(0, SSair.adjacent_rebuild.len);
        }
    }

    [Fact]
    public void WithNoMultizLevelsNoTurfHasATurfAboveOrBelow()
    {
        var map = new EngineWorld(1, 1, 2);
        using (map.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_mapping>();
            Assert.Null(GET_TURF_ABOVE(locate(1, 1, 1)));
            Assert.Null(GET_TURF_BELOW(locate(1, 1, 2)));
            // With the two levels linked (tg's manage_z_level, controllers/subsystem/mapping.dm), get_step goes up and down.
            SSmapping.multiz_levels.len = 2;
            SSmapping.multiz_levels[1f] = Dm.list_n(2);
            SSmapping.multiz_levels[2f] = Dm.list_n(2);
            ((DmList)SSmapping.multiz_levels[1f])[(float)Z_LEVEL_UP] = TRUE;
            ((DmList)SSmapping.multiz_levels[2f])[(float)Z_LEVEL_DOWN] = TRUE;
            Assert.Same(locate(1, 1, 2), GET_TURF_ABOVE(locate(1, 1, 1)));
            Assert.Same(locate(1, 1, 1), GET_TURF_BELOW(locate(1, 1, 2)));
            Assert.Null(GET_TURF_BELOW(locate(1, 1, 1)));
        }
    }

    [Fact]
    public void StringAssocListCachesEqualListsAsOne()
    {
        using var w = new BootPieces();
        var holder = Dm.@new<datum>();
        DmList first = Dm.list(Dm.kv("a", 1), Dm.kv("b", "x"));
        DmList second = Dm.list(Dm.kv("a", 1), Dm.kv("b", "x"));
        Assert.Same(first, holder.string_assoc_list(first));
        Assert.Same(first, holder.string_assoc_list(second));
        Assert.Same(first, GLOB.string_assoc_lists["a_1-b_x"]);
    }

    [Fact]
    public void TheWave4TypeTestsMatchTheirTypes()
    {
        Assert.True(isliving(new mob_living()));
        Assert.False(isliving(new mob_test_w40_mob()));
        Assert.True(ishuman(new mob_living_carbon_human()));
        Assert.True(isobserver(new mob_dead_observer()));
        Assert.True(isitem(new obj_item()));
        Assert.True(ismachinery(new obj_machinery()));
        Assert.True(isatom(new turf_open_indestructible()));
        Assert.False(isatom(new datum()));
        var box = new obj_test_w40_block();
        Assert.False(is_multi_tile_object(box));
        box.bound_width = 64;
        Assert.True(is_multi_tile_object(box));
    }

    [Fact]
    public void TheNewStubSubsystemsNeitherInitializeNorFire()
    {
        var w = new World();
        using (w.Enter())
        {
            datum_controller_subsystem[] stubs =
            {
                Dm.@new<datum_controller_subsystem_spatial_grid>(), Dm.@new<datum_controller_subsystem_vis_overlays>(),
                Dm.@new<datum_controller_subsystem_sound_tokens>(), Dm.@new<datum_controller_subsystem_air>(),
            };
            foreach (datum_controller_subsystem ss in stubs)
            {
                // M3 I1b: SSair initializes (setup_atmos_machinery, D123); M4b slice 1: it fires (tg's SS_BACKGROUND). M4b audio
                // slice 1: SSspatial_grid is translated (tg's can_fire = FALSE and an Initialize, no flags). M4b audio slice 5:
                // SSsound_tokens is translated (tg's SS_TICKER | SS_BACKGROUND | SS_NO_INIT: it fires, never initializes).
                // M7 slice T3: SSvis_overlays is translated (tg's SS_NO_INIT only: it fires every minute).
                float expected = ss == stubs[3] || ss == stubs[0] ? 0 : ss == stubs[2] || ss == stubs[1] ? SS_NO_INIT : Dm.bitor(SS_NO_INIT, SS_NO_FIRE);
                Assert.True(Dm.eq(Dm.bitand(ss.ss_flags, Dm.bitor(SS_NO_INIT, SS_NO_FIRE)), expected), (string)ss.name);
            }
            Assert.Same(stubs[0], SSspatial_grid);
            Assert.Same(stubs[3], SSair);
            Assert.Equal(new[] { "Spatial Grid", "Vis contents overlays", "Sound Tokens", "Atmospherics" }, stubs.Select(s => (string)s.name));
            Assert.True(Dm.eq(SSspatial_grid.dependencies[1f], typeof(datum_controller_subsystem_mapping)));
            Assert.Equal(0, SSair.planetary.Count);
            Assert.False(Dm.truthy(SSspatial_grid.can_fire));
            // M4b audio slice 1: the grid procs are translated; before Initialize they only key the movable and queue it.
            var box = new obj_test_w40_block();
            SSspatial_grid.add_grid_awareness(box, SPATIAL_GRID_CONTENTS_TYPE_HEARING);
            Assert.Equal(SPATIAL_GRID_CONTENTS_TYPE_HEARING, box.spatial_grid_key);
            SSspatial_grid.force_remove_from_grid(box);
            Assert.Equal(0, DmWorldState.Of(w).DeferredCount);
        }
    }

    [Fact]
    public void TheGlobListsHoldTgsValues()
    {
        using var w = new BootPieces();
        Assert.Equal(new object[] { NORTH, SOUTH, EAST, WEST }, Dm.each(GLOB.cardinals).Cast<object>().ToArray());
        Assert.Equal(8, GLOB.alldirs.Count);
        Assert.True(Dm.eq(GLOB.cardinal_angles["4"], 90)); // "[EAST]" = 90
        Assert.True(Dm.eq(GLOB.megafauna_spawn_list[typeof(mob_living_simple_animal_hostile_megafauna_dragon)], 4));
        Assert.True(Dm.eq(GLOB.ore_vent_sizes[LARGE_VENT_TYPE], 0));
        Assert.Contains(typeof(mob_living_carbon_human_dummy), Dm.each(GLOB.abstract_mob_types).Cast<object>());
        Assert.Equal(3, GLOB.simple_animals.Count);
        Assert.Equal(0, GLOB.mob_list.Count);
        // init_blood_types (M2 P2): one singleton per concrete blood type, keyed by id (the eight human blood types and, since
        // M8 slice C, the universal, lizard, ethereal, slime, water and snail ones, since M9 slice D oil, since gaps C3 meat, and since
        // gaps X5 the xenomorphs' acid; the rest come with their species)
        Assert.Equal(17, GLOB.blood_types.Count);
        Assert.IsType<datum_blood_type_oil>(GLOB.blood_types["Oil"]);
        Assert.IsType<datum_blood_type_human_o_plus>(GLOB.blood_types["O+"]);
        var juggernaut = (image)GLOB.construct_radial_images[CONSTRUCT_JUGGERNAUT];
        Assert.Equal("juggernaut", juggernaut.icon_state);
        Assert.Same(GLOB.generic_ambience, GLOB.ambience_assoc[AMBIENCE_GENERIC]);
        Assert.True(GLOB.generic_ambience.Count > 0);
        Assert.True(Dm.eq(GLOB.trait_to_hud[TRAIT_SECURITY_HUD], DATA_HUD_SECURITY_ADVANCED));
        // the data HUD datums exist since M8 slice F (GLOB.huds, datums/hud.dm:10) and file themselves under their categories
        Assert.Equal(11, GLOB.huds.Count);
        Assert.Contains(HEALTH_HUD, Dm.each(GLOB.huds_by_category));
        Assert.Equal(0, GLOB.active_alternate_appearances.Count);
        Assert.Equal(0, GLOB.areas.Count);
    }
}
