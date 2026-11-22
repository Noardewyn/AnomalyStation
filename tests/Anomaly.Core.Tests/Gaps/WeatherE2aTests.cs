using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package E2a (D327): the weather in a booted toy room whose level is the station's. The radiation storm telegraphs, starts,
/// winds down and ends with its messages and area overlays, mutates the unprotected and spares the maintenance; its event announces
/// it; rain fills an open beaker and its particles grow with the storm; thunder strikes whoever stands under it; the weather anomaly
/// brings a storm indoors and winds it down when neutralized.
/// </summary>
public class WeatherE2aTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, area Room) Room(ulong seed, Type areaType = null)
    {
        var r = new AudioRoom(15, 11, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        area room = r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            var a = (area)Dm.@new(areaType ?? typeof(area_station_hallway_primary_central));
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, 14, 10, 1)))
                t.change_area((area)t.loc, a);
            GLOB.the_station_areas = Dm.list(a.type);
            a.requires_power = FALSE;
            return a;
        });
        r.Step(Second);
        return (r, feed, bob, room);
    }

    private static datum_weather Run(AudioRoom r, Type type, DmList data, area only = null) => r.In(() =>
    {
        if (only != null)
            data[WEATHER_FORCED_AREAS] = Dm.list(only);
        return SSweather.run_weather(type, 1, data);
    });

    private static DmList Timing(float telegraph, float duration, float end) => Dm.list(
        Dm.kv(WEATHER_FORCED_TELEGRAPH, telegraph * SECONDS),
        Dm.kv(WEATHER_FORCED_DURATION, duration * SECONDS),
        Dm.kv(WEATHER_FORCED_END, end * SECONDS));

    [Fact]
    public void TheWeatherIsLiveAndItsEventsAreInThePool()
    {
        foreach (Type type in new[]
        {
            typeof(datum_controller_subsystem_weather), typeof(datum_weather), typeof(datum_weather_particle), typeof(datum_weather_rad_storm),
            typeof(datum_weather_rad_storm_nebula), typeof(datum_weather_particle_rain_storm), typeof(datum_weather_particle_rain_storm_wizard),
            typeof(datum_weather_particle_ash_storm), typeof(datum_weather_sand_storm), typeof(datum_weather_snow_storm),
            typeof(datum_weather_floor_is_lava), typeof(obj_effect_anomaly_weather), typeof(datum_round_event_radiation_storm),
            typeof(datum_round_event_anomaly_anomaly_weather), typeof(datum_round_event_anomaly_anomaly_weather_thundering),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        var (r, _, _, _) = Room(4301);
        Assert.NotNull(r.In(() => SSweather));
        var names = r.In(() => Dm.each<datum_round_event_control>(SSevents.control).Select(c => Dm.str(c.name)).ToList());
        Assert.Contains("Radiation Storm", names);
        Assert.Contains("Anomaly: Weather", names);
        Assert.Contains("Anomaly: Thundering Weather", names);
    }

    [Fact]
    public void TheRadiationStormRunsItsStagesAndMutatesTheUnprotected()
    {
        var (r, feed, bob, room) = Room(4302);
        object identity = r.In(() => bob.dna.unique_identity);
        datum_weather storm = Run(r, typeof(datum_weather_rad_storm), Timing(2, 10, 2));
        Assert.Equal(STARTUP_STAGE, (int)r.In(() => storm.stage));
        Assert.True(feed.Any("The air begins to grow warm."));
        Assert.True(r.In(() => Dm.@in(room, storm.impacted_areas)), "the hallway is in the storm");

        r.Step(3 * Second);
        Assert.Equal(MAIN_STAGE, (int)r.In(() => storm.stage));
        Assert.True(feed.Any("You feel waves of heat wash over you! Find shelter!"));
        Assert.True(r.In(() => room.overlays.len) > 0, "the storm's overlays cover the hallway: " + string.Join("\n", r.Room.RuntimeText));

        r.Step(10 * Second);
        Assert.Equal(WIND_DOWN_STAGE, (int)r.In(() => storm.stage));
        Assert.True(feed.Any("The air seems to be cooling off again."));
        Assert.NotEqual(identity, r.In(() => bob.dna.unique_identity));

        r.Step(3 * Second);
        Assert.Equal(END_STAGE, (int)r.In(() => storm.stage));
        Assert.True(feed.Any("The radiation threat has passed. Please return to your workplaces."));
        Assert.Equal(0, (int)r.In(() => SSweather.processing.len));
        Assert.Equal(0, (int)r.In(() => room.overlays.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMaintenanceIsShelteredFromTheRadiationStorm()
    {
        var (r, _, bob, room) = Room(4303, typeof(area_station_maintenance_department_chapel));
        object identity = r.In(() => bob.dna.unique_identity);
        datum_weather storm = Run(r, typeof(datum_weather_rad_storm), Timing(1, 8, 1));
        Assert.False(r.In(() => Dm.@in(room, storm.impacted_areas)), "the maintenance is protected");
        r.Step(10 * Second);
        Assert.Equal(identity, r.In(() => bob.dna.unique_identity));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRadiationStormEventAnnouncesAndStartsTheStorm()
    {
        var (r, feed, _, _) = Room(4304);
        var control = r.In(() => (datum_round_event_control)Dm.each<datum_round_event_control>(SSevents.control).First(c => c is datum_round_event_control_radiation_storm));
        r.In(() => Waiting.Detach(control.run_event()));
        r.Step(10 * Second);
        Assert.True(feed.Any("High levels of radiation detected near the station. Maintenance is best shielded from radiation."));
        Assert.NotNull(r.In(() => SSweather.get_weather_by_type(typeof(datum_weather_rad_storm))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RainFillsAnOpenBeakerAndItsParticlesGrowWithTheStorm()
    {
        obj_item_reagent_containers_cup_beaker beaker = null;
        var (r, _, _, room) = Room(4305);
        r.In(() => beaker = Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(8, 6, 1)));
        DmList data = Timing(1, 30, 2);
        data[WEATHER_FORCED_FLAGS] = Dm.bitor(Dm.bitor(WEATHER_TURFS, WEATHER_MOBS), WEATHER_INDOORS);
        var rain = (datum_weather_particle)Run(r, typeof(datum_weather_particle_rain_storm), data, room);
        r.Step(12 * Second);
        Assert.Equal(MAIN_STAGE, (int)r.In(() => rain.stage));
        Assert.True(r.In(() => rain.severity) > 0, "the rain's severity rose");
        r.In(() => Waiting.NoSleep(rain.weather_act_turf((turf_open)locate(8, 6, 1))));
        Assert.True(r.In(() => Dm.num(((datum_reagents)beaker.reagents).get_reagent_amount(typeof(datum_reagent_water)))) > 0, "the beaker caught the rain");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThunderStrikesWhoeverStandsUnderIt()
    {
        var (r, feed, bob, room) = Room(4306);
        DmList data = Timing(1, 30, 1);
        data[WEATHER_FORCED_FLAGS] = Dm.bitor(WEATHER_THUNDER, WEATHER_INDOORS);
        var rain = Run(r, typeof(datum_weather_particle_rain_storm), data, room);
        r.Step(2 * Second);
        r.In(() => Waiting.NoSleep(rain.thunder_act_turf((turf_open)get_turf(bob))));
        Assert.True(feed.Any("You've been struck by lightning!"));
        Assert.NotNull(r.In(() => Dm.locate<obj_effect_temp_visual_thunderbolt>(get_turf(bob))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASnowstormOffItsOwnLevelsPlaysItsWindToThePlayersUnderIt()
    {
        var (r, _, bob, room) = Room(4308);
        DmList data = Timing(1, 30, 1);
        data[WEATHER_FORCED_FLAGS] = Dm.bitor(WEATHER_MOBS, WEATHER_INDOORS);
        Run(r, typeof(datum_weather_snow_storm), data, room);
        r.Step(3 * Second);
        var manager = r.In(() => Dm.each<datum_component_area_sound_manager>(bob.GetComponents(typeof(datum_component_area_sound_manager))).FirstOrDefault());
        Assert.NotNull(manager);
        Assert.IsType<datum_looping_sound_snowstorm>(r.In(() => manager.our_loop));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWeatherAnomalyBringsAStormIndoorsUntilNeutralized()
    {
        var (r, _, _, room) = Room(4307);
        var anomaly = r.In(() => Dm.@new<obj_effect_anomaly_weather_thundering>(locate(8, 6, 1)));
        r.Step(Second);
        var storm = r.In(() => SSweather.get_weather_by_type(typeof(datum_weather_particle_rain_storm)));
        Assert.NotNull(storm);
        Assert.True(r.In(() => Dm.@in(room, storm.impacted_areas)), "the storm covers the anomaly's area");
        string mine = r.In(() => Dm.text("[] weathers, the storm among them: []", anomaly.active_weathers?.len ?? -1, Dm.@in(storm, anomaly.active_weathers) ? "yes" : "no"));
        r.In(() => Waiting.NoSleep(anomaly.anomalyNeutralize()));
        Assert.True(WIND_DOWN_STAGE == (int)r.In(() => storm.stage), mine + "; " + string.Join("\n", r.Room.RuntimeText));
        r.AssertNoRuntimes();
    }
}
