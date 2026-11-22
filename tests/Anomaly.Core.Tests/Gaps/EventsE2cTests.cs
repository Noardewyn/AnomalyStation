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
/// Gaps package E2c (D327): the other space and station events in a booted toy room of iron floor on the station's level. A radiation
/// leak makes a machine radioactive until the right tool repairs it; wormholes throw whoever walks in out of another; the aurora
/// caelus announces itself and tints the starlight; an earthquake's fault line throws whoever stands on it down and is torn open; a
/// portal storm brings syndicate shocktroops out of green flashes; a syndicate trooper shoots the crewman it sees and leaves its corpse.
/// </summary>
public class EventsE2cTests
{
    private const int W = 31, H = 25;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed, mapExtras: _ =>
        {
            for (int y = 2; y < H; y++)
                for (int x = 2; x < W; x++)
                    Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
        });
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            // the station's area over the floors: change_area keeps the area's lists on the turfs the map extras made
            var station = (area)Dm.@new(typeof(area_station_hallway_primary_central));
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, W - 1, H - 1, 1)))
                t.change_area((area)t.loc, station);
            station.requires_power = FALSE;
            GLOB.the_station_areas = Dm.list(station.type);
            GLOB.station_turfs = Dm.list();
            foreach (turf_open_floor_iron floor in Dm.each<turf_open_floor_iron>(block(2, 2, 1, W - 1, H - 1, 1)))
                Dm.append(GLOB.station_turfs, floor);
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    // the event as its control makes it (_event.dm:178 run_event: new typepath(TRUE, src)), but not processing: the test drives it
    private static T Event<T>(AudioRoom r) where T : datum_round_event => r.In(() =>
    {
        // (an event set aside for this kind of map has no control in SSevents: the earthquake's is null, as its start reads none)
        var control = Dm.each<datum_round_event_control>(SSevents.control).FirstOrDefault(c => c.typepath as Type == typeof(T));
        return Dm.@new<T>(FALSE, control);
    });

    [Fact]
    public void TheE2cEventsAreLiveAndInThePool()
    {
        foreach (Type type in new[]
        {
            typeof(datum_round_event_radiation_leak), typeof(datum_round_event_wormholes), typeof(obj_effect_portal_wormhole),
            typeof(datum_round_event_aurora_caelus), typeof(datum_round_event_control_aurora_caelus), typeof(datum_round_event_earthquake),
            typeof(datum_round_event_control_earthquake), typeof(datum_round_event_tram_malfunction), typeof(datum_round_event_control_tram_malfunction),
            typeof(datum_round_event_portal_storm), typeof(datum_round_event_portal_storm_syndicate_shocktroop), typeof(mob_living_basic_trooper),
            typeof(mob_living_basic_trooper_syndicate_melee_space), typeof(mob_living_basic_trooper_syndicate_melee_space_stormtrooper),
            typeof(mob_living_basic_trooper_syndicate_ranged_space), typeof(mob_living_basic_trooper_syndicate_ranged_infiltrator),
            typeof(mob_living_basic_trooper_nanotrasen_ranged_smg_peaceful), typeof(mob_living_basic_trooper_pirate_ranged_space),
            typeof(mob_living_basic_trooper_russian_ranged), typeof(mob_living_basic_trooper_abductor_ranged), typeof(mob_living_basic_viscerator),
            typeof(datum_component_plundering_attacks),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        var (r, _, _) = Room(4501);
        var names = r.In(() => Dm.each<datum_round_event_control>(SSevents.control).Select(c => Dm.str(c.name)).ToList());
        foreach (string name in new[] { "Radiation Leak", "Wormholes", "Aurora Caelus", "Portal Storm: Syndicate Shocktroops", "Tram Malfunction" })
            Assert.Contains(name, names);
        // the earthquake is for planetary maps: on a space station tg sets it aside (events.dm:31)
        Assert.True(r.In(() => SSevents.nonrunning_events_by_name["Chasmic Earthquake"] != null), "the earthquake is set aside on a space station");
        Assert.Equal("#a2ff80", r.In(() => hsl_gradient(0, 0, "#A2FF80", 1, "#A2FFEE")).ToLowerInvariant());
        Assert.Equal("#a2ffee", r.In(() => hsl_gradient(1, 0, "#A2FF80", 1, "#A2FFEE")).ToLowerInvariant());
        Assert.False(r.In(() => Dm.truthy(Dm.each<datum_round_event_control>(SSevents.control).OfType<datum_round_event_control_tram_malfunction>().First().can_spawn_event(50))),
            "no tram, no malfunction");
    }

    [Fact]
    public void ARadiationLeakMakesAMachineRadioactiveUntilTheRightToolRepairsIt()
    {
        var (r, feed, bob) = Room(4502);
        var lathe = Place<obj_machinery_autolathe>(r, 6, 4);
        Place<obj_effect_landmark_event_spawn>(r, 7, 6);
        var leak = Event<datum_round_event_radiation_leak>(r);
        r.In(() => Waiting.NoSleep(leak.setup()));
        Assert.Same(lathe, r.In(() => leak.picked_machine_ref?.resolve()));
        r.In(() => Waiting.NoSleep(leak.start()));
        Assert.True(feed.Any("starts to emanate a horrible green gas!"));
        Assert.NotNull(r.In(() => lathe.GetComponent(typeof(datum_component_radioactive_emitter))));
        Assert.InRange(r.In(() => leak.signals_to_add.len), 1, 3);
        r.In(() => Waiting.NoSleep(leak.announce__proc(FALSE)));
        Assert.True(feed.Any("A radiation leak has been detected in"));

        string signal = r.In(() => Dm.str(leak.signals_to_add[1]));
        string behaviour = signal.Substring("tool_act_".Length);
        Type toolType = behaviour switch
        {
            TOOL_WRENCH => typeof(obj_item_wrench),
            TOOL_CROWBAR => typeof(obj_item_crowbar),
            TOOL_SCREWDRIVER => typeof(obj_item_screwdriver),
            TOOL_MULTITOOL => typeof(obj_item_multitool),
            TOOL_WELDER => typeof(obj_item_weldingtool_largetank),
            TOOL_ANALYZER => typeof(obj_item_analyzer),
            _ => typeof(obj_item_wirecutters),
        };
        r.In(() =>
        {
            var tool = (obj_item)Dm.@new(toolType, get_turf(bob));
            if (tool is obj_item_weldingtool welder)
                welder.switched_on(bob);
            Waiting.NoSleep(bob.put_in_hands(tool));
            Waiting.Detach(leak.try_remove_radiation(lathe, bob, tool));
        });
        r.Step(32 * Second);
        Assert.Null(r.In(() => lathe.GetComponent(typeof(datum_component_radioactive_emitter))));
        Assert.True(feed.Any("dissipates."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WormholesThrowWhoeverWalksInOutOfAnother()
    {
        var (r, feed, bob) = Room(4503);
        var holes = Event<datum_round_event_wormholes>(r);
        r.In(() =>
        {
            holes.number_of_wormholes = 6;
            Waiting.NoSleep(holes.start());
            Waiting.NoSleep(holes.announce__proc(FALSE));
        });
        Assert.Equal(6, r.In(() => Dm.each<obj_effect_portal_wormhole>(GLOB.all_wormholes).Count()));
        Assert.True(feed.Any("Space-time anomalies detected on the station."));
        var start = r.In(() => (bob.x, bob.y));
        r.In(() => Waiting.NoSleep(Dm.each<obj_effect_portal_wormhole>(GLOB.all_wormholes).First().teleport(bob)));
        Assert.True(r.In(() => Dm.each<obj_effect_portal_wormhole>(GLOB.all_wormholes).Any(h => get_dist(h, bob) <= 1)), "Bob comes out beside a wormhole");
        r.In(() => Waiting.NoSleep(holes.end()));
        Assert.Equal(0, r.In(() => GLOB.all_wormholes.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAuroraCaelusAnnouncesItselfAndTintsTheStarlight()
    {
        var (r, feed, _) = Room(4504);
        object before = r.In(() => GLOB.starlight_color);
        var aurora = Event<datum_round_event_aurora_caelus>(r);
        r.In(() => Waiting.Detach(aurora.announce__proc(FALSE)));
        Assert.True(feed.Any("A harmless cloud of ions is approaching your station"));
        r.Step(45 * Second); // the starlight's five fading steps, 8 seconds apart
        Assert.NotEqual(before, r.In(() => GLOB.starlight_color));
        r.In(() => Waiting.Detach(aurora.end()));
        Assert.True(feed.Any("The aurora caelus event is now ending."));
        r.Step(45 * Second);
        Assert.Equal(before, r.In(() => GLOB.starlight_color));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEarthquakeThrowsWhoeverStandsOnTheFaultAndTearsItOpen()
    {
        var (r, feed, bob) = Room(4505);
        var quake = Event<datum_round_event_earthquake>(r);
        r.In(() =>
        {
            quake.special_spot = locate(15, 12, 1);
            Waiting.NoSleep(quake.setup());
        });
        Assert.True(r.In(() => quake.turfs_to_shred.len) > 0, "a fault line");
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(15, 12, 1))));
        r.In(() =>
        {
            quake.activeFor = quake.end_when - 2;
            Waiting.NoSleep(quake.tick());
        });
        Assert.True(feed.Any("The ground quakes violently beneath you, throwing you off your feet!"));
        r.In(() => Waiting.NoSleep(quake.end()));
        Assert.True(r.In(() => Dm.@in(locate(15, 12, 1), SSexplosions.highturf) || Dm.@in(locate(15, 12, 1), SSexplosions.medturf)),
            "the epicenter is queued for the explosions' damage");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APortalStormBringsSyndicateShocktroopsOutOfGreenFlashes()
    {
        var (r, feed, bob) = Room(4506);
        var storm = Event<datum_round_event_portal_storm_syndicate_shocktroop>(r);
        r.In(() => Waiting.NoSleep(storm.setup()));
        Assert.Equal(10, r.In(() => (int)Dm.num(storm.number_of_hostiles)));
        Assert.Equal(2, r.In(() => (int)Dm.num(storm.number_of_bosses)));
        r.In(() => Waiting.Detach(storm.announce__proc(FALSE)));
        r.Step(11 * Second);
        Assert.True(feed.Any("Massive bluespace anomaly detected en route to"));
        r.In(() =>
        {
            for (int tick = (int)storm.start_when; tick < storm.start_when + 40 && storm.end_when != storm.activeFor; tick++)
            {
                storm.activeFor = tick;
                Waiting.NoSleep(storm.tick());
            }
        });
        Assert.Equal(12, r.In(() => Dm.each<mob_living_basic_trooper_syndicate>(Dm.world).Count()));
        Assert.Equal(2, r.In(() => Dm.each<mob_living_basic_trooper_syndicate_melee_space_stormtrooper>(Dm.world).Count()));
        r.In(() =>
        {
            foreach (mob_living_basic_trooper_syndicate trooper in Dm.each<mob_living_basic_trooper_syndicate>(Dm.world).ToList())
                qdel(trooper);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASyndicateTrooperShootsTheCrewmanItSeesAndTheTroopersLeaveGibsOrACorpse()
    {
        var (r, _, bob) = Room(4507);
        float health = r.In(() => Dm.num(bob.health));
        var trooper = Place<mob_living_basic_trooper_syndicate_ranged>(r, 9, 4);
        for (int i = 0; i < 20 && r.In(() => Dm.num(bob.health)) >= health; i++)
            r.Step(Second);
        Assert.True(r.In(() => Dm.num(bob.health)) < health, "the trooper's shots hit Bob");

        // the gunman's corpse is a gibspawner (syndicate.dm:101); the base syndicate trooper leaves its soldier's corpse
        turf died = r.In(() => get_turf(trooper));
        r.In(() => Waiting.NoSleep(trooper.death(FALSE)));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(trooper)), "a trooper is deleted on death");
        Assert.True(r.In(() => Dm.each<obj_effect_decal_cleanable>(range(2, died)).Any()), "the gunman leaves gibs");

        var soldier = Place<mob_living_basic_trooper_syndicate>(r, 20, 14);
        turf fell = r.In(() => get_turf(soldier));
        r.In(() => Waiting.NoSleep(soldier.death(FALSE)));
        r.Step(Second);
        Assert.True(r.In(() => Dm.each<mob_living_carbon_human>(fell.contents).Any(h => h.stat == DEAD)), "the soldier leaves its corpse where it fell");
        r.AssertNoRuntimes();
    }
}
