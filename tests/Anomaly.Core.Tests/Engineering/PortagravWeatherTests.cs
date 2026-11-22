using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E16 (machinery/portagrav.dm, machinery/mining_weather_monitor.dm, components/weatherannouncer.dm) in a booted toy
/// room: the anchored portable gravity unit, switched on, forces its gravity on the turfs of its range, lays tg's field tiles in its
/// particles' colour and runs on its cell; its window's acts change the strength (to 2, 4 emagged) and the range; switched off the field
/// goes. The barometric monitor shows the all-clear overlay and green light with no mining level; the doppler radar tower with a core
/// draws its idle power, shows its overlays and window data, and a summon finds no weather to run (weather is not ported).
/// </summary>
public class PortagravWeatherTests
{
    private const int Second = 20;

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed = 16201, Action<World> extras = null)
    {
        var r = new AudioRoom(16, 12, seed: seed, mapExtras: extras);
        mob_living_carbon_human bob = r.Join(7, 6, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static object Act(AudioRoom r, datum src, mob user, string action, DmList @params = null)
        => r.In(() => Waiting.NoSleep(src.ui_act(action, @params ?? Dm.list(), Dm.@new<datum_tgui>(user, src, "x"))));

    [Fact]
    public void ThePortagravForcesGravityAroundItAndRunsOnItsCell()
    {
        var (r, bob) = Room();
        var pg = r.In(() => Dm.@new<obj_machinery_power_portagrav_anchored>(locate(8, 6, 1)));
        float charge = r.In(() =>
        {
            var cell = Assert.IsType<obj_item_stock_parts_power_store_cell_high>(pg.get_cell());
            Assert.Same(pg, cell.loc);
            Assert.False(HAS_TRAIT(locate(10, 6, 1), TRAIT_FORCED_GRAVITY));
            Waiting.NoSleep(pg.toggle_on(bob));
            Assert.True(Dm.truthy(pg.on));
            Assert.NotNull(pg.gravity_field);
            return Dm.num(cell.charge);
        });
        r.In(() =>
        {
            // range 4: the turf two tiles east is in the field, with the field tile in grav_field_down's colour (strength 1)
            turf near = locate(10, 6, 1);
            Assert.True(HAS_TRAIT(near, TRAIT_FORCED_GRAVITY));
            var fluff = Dm.locate<obj_effect_gravity_fluff_field>(near);
            Assert.NotNull(fluff);
            Assert.Equal("#FF0000", Dm.str(fluff.color));
            Assert.Equal(typeof(particles_grav_field_down), fluff.particle_type);
            Assert.False(HAS_TRAIT(locate(14, 6, 1), TRAIT_FORCED_GRAVITY)); // six tiles away
            List<string> overlays = Dm.each(pg.overlays).Select(o => o is IDmAppearance a ? a.icon_state : Dm.str(o)).ToList();
            Assert.Contains("portagrav_anchors", overlays);
            Assert.Contains("portagrav_lights", overlays);
            Assert.Contains("activated", overlays);

            var data = (DmList)Waiting.NoSleep(pg.ui_data(bob));
            Assert.Equal((1f, 4f, 6f, TRUE, FALSE), (Dm.num(data["gravity"]), Dm.num(data["range"]), Dm.num(data["maxrange"]), Dm.num(data["on"]), Dm.num(data["wiremode"])));
            Assert.Equal(Dm.str(display_power(4000)), Dm.str(data["draw"]));
        });

        // the window: stronger (2 at most unemagged), smaller
        Act(r, pg, bob, "adjust_grav", Dm.list(Dm.kv("adjustment", "1")));
        Act(r, pg, bob, "adjust_grav", Dm.list(Dm.kv("adjustment", "1")));
        Act(r, pg, bob, "adjust_range", Dm.list(Dm.kv("adjustment", "-2")));
        r.In(() =>
        {
            Assert.Equal((2f, 2f), (pg.grav_strength, Dm.num(pg.range)));
            Assert.Equal(2f, pg.gravity_field.gravity_value);
            Assert.Equal(typeof(particles_grav_field_down_strong), Dm.locate<obj_effect_gravity_fluff_field>(locate(9, 6, 1)).particle_type);
            Assert.False(HAS_TRAIT(locate(11, 6, 1), TRAIT_FORCED_GRAVITY)); // three tiles: out of range 2
        });

        // the cell pays its draw every machines cycle
        r.Step(4 * Second);
        Assert.True(r.In(() => Dm.num(((obj_item_stock_parts_power_store_cell)pg.get_cell()).charge)) < charge);

        Act(r, pg, bob, "toggle_power");
        r.In(() =>
        {
            Assert.False(Dm.truthy(pg.on));
            Assert.Null(pg.gravity_field);
            Assert.False(HAS_TRAIT(locate(9, 6, 1), TRAIT_FORCED_GRAVITY));
            Assert.Null(Dm.locate<obj_effect_gravity_fluff_field>(locate(9, 6, 1)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnanchoredPortagravWillNotStart()
    {
        var (r, bob) = Room();
        var pg = r.In(() => Dm.@new<obj_machinery_power_portagrav>(locate(8, 6, 1)));
        r.In(() =>
        {
            Waiting.NoSleep(pg.toggle_on(bob));
            Assert.False(Dm.truthy(pg.on));
            Assert.Null(pg.gravity_field);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBarometricMonitorShowsAllClearWithNoMiningLevel()
    {
        var (r, bob) = Room();
        var monitor = r.In(() => Dm.@new<obj_machinery_mining_weather_monitor_directional_east>(locate(10, 8, 1)));
        r.Step(2 * Second);
        r.In(() =>
        {
            var announcer = monitor.GetComponent(typeof(datum_component_weather_announcer)) as datum_component_weather_announcer;
            Assert.NotNull(announcer);
            Assert.Equal(0f, announcer.warning_level);
            Assert.Contains("wallgreen", Dm.each(monitor.overlays).Select(o => o is IDmAppearance a ? a.icon_state : Dm.str(o)));
            Assert.Equal(LIGHT_COLOR_GREEN, Dm.str(monitor.light_color));
            Assert.Null(announcer.time_till_storm()); // the toy room has no mining level
            Assert.Equal("All clear, no weather alerts to report.", Dm.str(announcer.get_warning_message()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRadarTowerRunsOnItsCableAndItsCoreOpensTheWindow()
    {
        var (r, bob) = Room(extras: w =>
        {
            for (int x = 3; x <= 6; x++)
                Dm.@new<obj_structure_cable>(locate(x, 3, 1));
            Dm.@new<obj_machinery_power_rtg_debug>(locate(3, 3, 1));
        });
        var tower = r.In(() => Dm.@new<obj_machinery_power_weather_tower_core>(locate(6, 3, 1)));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(tower.active));
            Assert.NotNull(tower.core);
            Assert.Equal("anomalous doppler radar tower", Dm.str(tower.name));
            List<string> overlays = Dm.each(tower.overlays).Select(o => o is IDmAppearance a ? a.icon_state : Dm.str(o)).ToList();
            Assert.Contains("radar_on", overlays);
            Assert.Contains("radar_core", overlays);
            Assert.Contains(tower, Dm.each(GLOB.weather_towers[Dm.text("[]", tower.z)] as DmList));

            var stat = (DmList)Waiting.NoSleep(tower.ui_static_data(bob));
            var types = (DmList)stat["summonable_weather_types"];
            Assert.True(types.len >= 4);
            Assert.Equal("Ash storm", Dm.str(((DmList)types[1])["name"]));
            Assert.Equal(8f, Dm.num(stat["max_core_charge"]));
            var data = (DmList)Waiting.NoSleep(tower.ui_data(bob));
            Assert.Equal((8f, TRUE), (Dm.num(data["core_charges"]), Dm.num(data["can_summon_weather"])));
            Assert.Equal(0f, ((DmList)data["active_weather_on_z"]).len);
        });
        // a summon runs the snow storm on the tower's level (gaps E2a): the core spends its charge, the summon waits 8 minutes and the clear 4
        float cost = r.In(() => Dm.num(tower.weather_charge_cost()));
        Act(r, tower, bob, "summon_weather", Dm.list(Dm.kv("weather_type", "/datum/weather/snow_storm")));
        r.In(() =>
        {
            Assert.Equal(8f - cost, tower.core.charges);
            Assert.NotNull(SSweather.get_weather_by_type(typeof(datum_weather_snow_storm)));
            Assert.True(tower.summon_weather_cd > world.time);
            Assert.False(Dm.truthy(Waiting.NoSleep(tower.summon_weather(typeof(datum_weather_snow_storm), bob))));
        });
        r.AssertNoRuntimes();
    }
}
