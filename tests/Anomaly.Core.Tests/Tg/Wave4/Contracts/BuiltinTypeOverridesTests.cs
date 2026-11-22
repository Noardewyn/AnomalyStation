using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.Tg.Wave4.Contracts;

/// <summary>
/// The constructor hook of the builtin types (lead decision D1, translation-rules.md 10.2 "Builtin types"): the engine
/// constructor sets BYOND's defaults and ends with <c>dm_tg_vars()</c>, which the mirror implements with tg's overrides,
/// so per type BYOND's defaults come first, tg's after, and the most derived override wins; <c>initial()</c> reads the
/// same values (a prototype is built by the constructor alone, object-model.md section 5).
/// </summary>
public class BuiltinTypeOverridesTests
{
    [Fact]
    public void MovableObjAndMobTakeTgsOverridesAfterByondsDefaults()
    {
        var box = new obj_test_w40_block();
        Assert.Equal(typeof(obj), box.abstract_type);                         // objs.dm:3 after atoms_movable.dm:2
        Assert.Equal(Dm.OBJ_LAYER, box.layer);
        Assert.Equal(8f, box.glide_size);                                      // atoms_movable.dm:4
        Assert.True(Dm.eq(Dm.bitor(Dm.bitor(TILE_BOUND, PIXEL_SCALE), LONG_GLIDE), box.appearance_flags));
        Assert.Equal(Dm.SLIDE_STEPS, box.animate_movement);                   // objs.dm:4
        Assert.Equal(SPAN_ROBOT, box.speech_span);                            // objs.dm:5
        Assert.Equal(Dm.TRUE, box.uses_integrity);                        // objs.dm:51

        var m = new mob_test_w40_mob();
        Assert.Equal(typeof(mob), m.abstract_type);
        Assert.Equal(Dm.MOB_LAYER, m.layer);                                  // BYOND's and tg's value
        Assert.Equal(1e6f, m.see_in_dark);                                    // mob_defines.dm:22, BYOND's is 2
        Assert.Equal(8f, m.pressure_resistance);                              // mob_defines.dm:15 over LINDA_turf_tile.dm:395
        Assert.True(Dm.eq(m.mouse_drag_pointer, Dm.MOUSE_ACTIVE_POINTER));    // mob_defines.dm:16
        Assert.Equal((float)PASSMOB, m.pass_flags_self);
        Assert.Equal(10f, m.throwforce);
        Assert.Equal((float)EMISSIVE_BLOCK_GENERIC, m.blocks_emissive);
        Assert.Null(m.faction);                                               // list(FACTION_NEUTRAL) is dm_init_vars'
    }

    [Fact]
    public void TurfAndAreaTakeTgsOverridesFromEveryFileThatOverridesThem()
    {
        var floor = new turf_open_indestructible();
        Assert.Equal(1f, floor.luminosity);                                   // turf.dm:9
        Assert.Equal(Dm.VIS_INHERIT_ID, floor.vis_flags);                     // turf.dm:8
        Assert.Equal((float)ATMOS_PASS_PROC, floor.can_atmos_pass);           // /turf/open, LINDA_system.dm:18
        Assert.Equal(LOW_FLOOR_LAYER, floor.layer);                           // _open.dm:3
        Assert.Equal("floor", floor.name);                                    // _open.dm:295
        Assert.Equal(typeof(turf_open), floor.abstract_type);                 // inherited, not the floor's own type

        var wall = new turf_closed_indestructible();
        Assert.Equal((float)ATMOS_PASS_NO, wall.can_atmos_pass);              // /turf, LINDA_system.dm:15
        Assert.Equal(1f, wall.density);
        Assert.Equal(1f, wall.opacity);
        Assert.True(Dm.eq(wall.baseturfs, typeof(turf_open_indestructible_plating)));

        var room = new area_misc_testroom();
        Assert.Equal("Test Room", room.name);                                 // misc.dm:43 over areas.dm:7
        Assert.Equal((float)STANDARD_GRAVITY, room.default_gravity);
        Assert.Equal(Dm.AREA_LAYER, room.layer);
        Assert.Equal(1f, room.luminosity);
        Assert.Equal((float)INVISIBILITY_LIGHTING, room.invisibility);

        var space = new area_space();
        Assert.Equal("Space", space.name);                                    // areas.dm:7 (tg's /world/area)
        Assert.Equal((float)NO_GRAVITY, space.area_flags);                    // misc.dm:13
        Assert.Null(space.ambient_buzz);                                      // misc.dm:18
    }

    [Fact]
    public void InitialReadsTheConstructorValues()
    {
        Assert.Equal(1e6f, Dm.initial(typeof(mob), (mob p) => p.see_in_dark));
        Assert.Equal(8f, Dm.initial(typeof(obj), (obj p) => p.glide_size));
        Assert.Equal("Test Room", Dm.initial(typeof(area_misc_testroom), (area p) => p.name));
    }

    [Fact]
    public void ObjectCreatingDefaultsRunInDmInitVars()
    {
        var w = new World();
        using (w.Enter())
        {
            var m = Dm.@new<mob_test_w40_mob>();
            Assert.Equal(new object[] { FACTION_NEUTRAL }, Dm.each(m.faction).Cast<object>().ToArray());
            Assert.Equal(new object[] { ANTAG_HUD }, Dm.each(m.hud_possible).Cast<object>().ToArray());
            Assert.Equal(0, m.held_items.Count);
            var room = Dm.@new<area_misc_testroom_test_w40_room>();
            Assert.Equal(0, room.turfs_by_zlevel.Count);
            Assert.Equal(0, room.firealarms.Count);
        }
    }

    [Fact]
    public void NextMobIdIsAPerWorldStatic()
    {
        var a = new World();
        var b = new World();
        using (a.Enter())
            mob.next_mob_id = 5;
        using (b.Enter())
            Assert.Equal(0f, mob.next_mob_id);
        using (a.Enter())
            Assert.Equal(5f, mob.next_mob_id);
    }
}
