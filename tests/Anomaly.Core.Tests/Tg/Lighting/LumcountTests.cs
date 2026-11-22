using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Lighting;

/// <summary>A white lamp fixture (rules 15: a fixture path no tg path starts with): range 3, power 1, tg's default height.</summary>
[DmPath("/obj/structure/test_lum_lamp")]
public class test_lum_lamp : obj_structure
{
    public test_lum_lamp()
    {
        light_range = 3;
        light_power = 1;
    }
}

/// <summary>
/// M7 lumcount, the data path: SSlighting's light sources and corners and the turf's lumcount procs, against tg's formulas
/// (lighting_source.dm:270 falloff_at_coord, lighting_turf.dm:60 get_static_lumcount).
/// </summary>
public class LumcountTests
{
    private const float Tolerance = 1e-4f;

    /// <summary>
    /// A toy room whose area takes static lighting (tg's testroom does not, misc.dm:41) and an initialized SSlighting: every
    /// floor gets its lighting mark (DV85) and the queues are fired.
    /// </summary>
    private static TgToyRoom LitRoom()
    {
        var room = TgToyRoom.Create(width: 16, height: 11); // the floor is (2..15, 2..10)
        room.In(() =>
        {
            ((area)TgToyRoom.T(5, 5).loc).static_lighting = TRUE;
            Waiting.NoSleep(SSlighting.Initialize());
        });
        return room;
    }

    /// <summary>One fire of SSlighting with init tick checks (as Initialize fires it, lighting.dm:29).</summary>
    private static void Fire() => Waiting.NoSleep(SSlighting.fire(FALSE, TRUE));

    // lighting_source.dm:270-280: 1 - CLAMP01(sqrt(x^2 + y^2 + z^2 + height) / max(1, range)), for a full circle
    private static float Falloff(float x, float y, float range = 3, float height = LIGHTING_HEIGHT)
        => 1 - Math.Clamp(MathF.Sqrt(x * x + y * y + height) / Math.Max(1, range), 0, 1);

    // lighting_turf.dm:60-79: the four corners' three channels over 12; a white light gives each channel the falloff
    private static float TurfLum(float dx, float dy)
        => (Falloff(dx + 0.5f, dy + 0.5f) + Falloff(dx + 0.5f, dy - 0.5f) + Falloff(dx - 0.5f, dy - 0.5f) + Falloff(dx - 0.5f, dy + 0.5f)) / 4;

    private static float Static(int x, int y) => Dm.num(TgToyRoom.T(x, y).get_static_lumcount());

    [Fact]
    public void ALampLightsItsCornersAndTurfsByTgsFalloff()
    {
        var room = LitRoom();
        room.In(() =>
        {
            Assert.True(Dm.truthy(TgToyRoom.T(5, 5).dm_lighting_object));
            Assert.Equal(0f, Static(5, 5), Tolerance); // dark before the lamp
            var lamp = Dm.@new<test_lum_lamp>(TgToyRoom.T(5, 5));
            Assert.NotNull(lamp.light);
            Fire();

            // the lamp's own corner NE at (+0.5, +0.5): each channel 1 - sqrt(0.5 + 1) / 3
            datum_lighting_corner ne = TgToyRoom.T(5, 5).lighting_corner_NE;
            Assert.Equal(Falloff(0.5f, 0.5f), ne.lum_r, Tolerance);
            Assert.Equal(ne.lum_r, ne.lum_g, Tolerance);
            Assert.Equal(ne.lum_r, ne.lum_b, Tolerance);
            Assert.Equal(TurfLum(0, 0), Static(5, 5), Tolerance);
            Assert.Equal(TurfLum(2, 0), Static(7, 5), Tolerance);
            Assert.Equal(TurfLum(1, -2), Static(6, 3), Tolerance);
            // beyond the range every corner is at or past the falloff's 0
            Assert.Equal(0f, Static(9, 5), Tolerance);
            // get_lumcount clamps the sum to 0..1 (lighting_turf.dm:28); no overlay light here
            Assert.Equal(TurfLum(0, 0), Dm.num(TgToyRoom.T(5, 5).get_lumcount()), Tolerance);
            Assert.True(Dm.truthy(TgToyRoom.T(5, 5).check_lumcount_above(0.5f)));
            Assert.True(Dm.truthy(TgToyRoom.T(9, 5).check_lumcount_below(0.1f)));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ATurfWithoutTheLightingMarkCountsAsLit()
    {
        var room = TgToyRoom.Create(); // the testroom: static_lighting FALSE, SSlighting not initialized
        room.In(() =>
        {
            Assert.False(Dm.truthy(TgToyRoom.T(5, 5).dm_lighting_object));
            Assert.Equal(1f, Dm.num(TgToyRoom.T(5, 5).get_lumcount())); // lighting_turf.dm:21-22
            Assert.True(Dm.truthy(TgToyRoom.T(5, 5).check_lumcount_above(0.5f)));
            Assert.False(Dm.truthy(TgToyRoom.T(5, 5).check_lumcount_below(0.5f)));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ALampTurnedOffOrMovedTakesItsLightAlong()
    {
        var room = LitRoom();
        room.In(() =>
        {
            var lamp = Dm.@new<test_lum_lamp>(TgToyRoom.T(5, 5));
            Fire();
            Assert.Equal(TurfLum(0, 0), Static(5, 5), Tolerance);

            // moved two tiles east: the old tile keeps only what reaches it from the new one
            Waiting.NoSleep(lamp.forceMove(TgToyRoom.T(7, 5)));
            Fire();
            Assert.Equal(TurfLum(0, 0), Static(7, 5), Tolerance);
            Assert.Equal(TurfLum(-2, 0), Static(5, 5), Tolerance);

            // off: update_light deletes the source (lighting_atom.dm:50-51) and remove_lum takes its light out at once
            Waiting.NoSleep(lamp.set_light_on(FALSE));
            lamp.update_light();
            Assert.Null(lamp.light);
            Fire();
            Assert.Equal(0f, Static(7, 5), Tolerance);
            Assert.Equal(0f, Static(5, 5), Tolerance);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AClosedDoorInAWallKeepsTheLightOnItsSide()
    {
        var room = LitRoom();
        obj_machinery_door_airlock door = null;
        room.In(() =>
        {
            // a wall across the room at x = 8 with an airlock at (8, 5)
            for (int y = 2; y <= 10; y++)
                if (y != 5)
                    Waiting.NoSleep(TgToyRoom.T(8, y).ChangeTurf(typeof(turf_closed_wall)));
            door = Dm.@new<obj_machinery_door_airlock>(TgToyRoom.T(8, 5));
            Assert.Equal(ALL_CARDINALS, TgToyRoom.T(8, 5).directional_opacity); // light_blocking (atoms_movable.dm:206)
            Dm.@new<test_lum_lamp>(TgToyRoom.T(6, 5));
            Fire();
            Assert.Equal(TurfLum(1, 0), Static(7, 5), Tolerance);
            // behind the door: its corners at x = 8.5 belong only to opaque or unseen turfs, never inserted
            Assert.Equal(0f, Static(9, 5), Tolerance);
            Waiting.Detach(door.open());
        });
        room.Step(30);
        room.In(() =>
        {
            Assert.False(Dm.truthy(door.opacity));
            Assert.Equal(NONE, TgToyRoom.T(8, 5).directional_opacity);
            Fire(); // reconsider_lights queued the vis_update (lighting_turf.dm:157)
            // the open door's tile is seen: its corners at x = 8.5 (2.5 from the lamp) take the light
            Assert.Equal((Falloff(2.5f, 0.5f) + Falloff(2.5f, -0.5f)) / 4, Static(9, 5), Tolerance);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AHeldFlashlightGivesItsHoldersTileTheOverlayLightsLumPower()
    {
        var room = LitRoom();
        room.In(() =>
        {
            var holder = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(5, 5));
            var flashlight = Dm.@new<obj_item_flashlight>(TgToyRoom.T(5, 5));
            Assert.True(Dm.truthy(Waiting.NoSleep(holder.put_in_hands(flashlight))));
            Assert.Equal(0f, Dm.num(TgToyRoom.T(5, 5).get_dynamic_lumcount()), Tolerance);
            Waiting.NoSleep(flashlight.toggle_light(holder));
            Assert.True(Dm.truthy(flashlight.light_on));
            // overlay_lighting.dm:32 lum_power 0.5, collected through the holder's oranges ear (lighting_turf.dm:89)
            Assert.Equal(0.5f, Dm.num(TgToyRoom.T(5, 5).get_dynamic_lumcount()), Tolerance);
            Assert.Equal(0.5f, Dm.num(TgToyRoom.T(5, 5).get_lumcount()), Tolerance);
            Assert.False(Dm.truthy(TgToyRoom.T(5, 5).is_softly_lit()));
        });
        Assert.Empty(room.RuntimeText);
    }
}

/// <summary>M7 lumcount: space's starlight (space.dm:1-122, change_turf.dm:173-194) as a light source of the lumcount.</summary>
public class StarlightTests
{
    [Fact]
    public void SpaceBesideAFloorIsStarlitAndLightsTheFloorUntilItIsFloorAgain()
    {
        var room = TgToyRoom.Create(width: 12, height: 9);
        room.In(() =>
        {
            ((area)TgToyRoom.T(5, 5).loc).static_lighting = TRUE;
            Waiting.NoSleep(SSlighting.Initialize());
            var space = (turf_open_space)Waiting.NoSleep(TgToyRoom.T(5, 5).ChangeTurf(typeof(turf_open_space)));
            // update_starlight: a non-space neighbour, so enable_starlight (space.dm:106-122) with GLOB's values
            Assert.True(Dm.truthy(space.light_on));
            Assert.True(Dm.@in(space, GLOB.starlight));
            Assert.NotNull(space.light);
            Assert.True(Dm.eq(space.light_range, GLOB.starlight_range));
            Assert.True(Dm.eq(GLOB.starlight_range, 2) && Dm.eq(GLOB.starlight_power, 1)); // initial() of /turf/open/space (:2-19)
            Waiting.NoSleep(SSlighting.fire(FALSE, TRUE));
            Assert.True(Dm.num(TgToyRoom.T(6, 5).get_static_lumcount()) > 0);

            var floor = Waiting.NoSleep(space.ChangeTurf(typeof(turf_open_floor_plating))) as turf;
            Assert.False(Dm.@in(space, GLOB.starlight)); // /turf/open/space/Destroy (:85)
            Waiting.NoSleep(SSlighting.fire(FALSE, TRUE));
            Assert.Equal(0f, Dm.num(floor.get_static_lumcount()), 1e-4f);
        });
        Assert.Empty(room.RuntimeText);
    }
}
