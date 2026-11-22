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
/// Gaps package E1a (D327): the anomaly events and the vent clogs in a booted toy room whose level and area are the station's. An
/// anomaly event drops its anomaly in a safe station area, announces it and alerts the ghosts; the bluespace anomaly's detonation moves
/// the area to a tracking beacon; the ectoplasmic outburst haunts the items around it and opens a ghost portal; a vent clog spews filth
/// and a creature until a plunger sends it back; the fuel pool burns and lights its neighbours; the plunger and the vent clogs' new
/// creatures; the corgi's deadchat control.
/// </summary>
public class EventsE1aTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 15)
    {
        var r = new AudioRoom(width, 11, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() =>
        {
            // the toy room's level is the station's (as SyndicateN1cTests), its floor a station hallway (as BountyTests): the vent clogs
            // take only /area/station vents, and the anomaly placer picks a station area
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            var hall = Dm.@new<area_station_hallway_primary_central>();
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, width - 1, 10, 1)))
                t.change_area((area)t.loc, hall);
            GLOB.the_station_areas = Dm.list(hall.type);
            hall.requires_power = FALSE;
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Control<T>(AudioRoom r) where T : datum_round_event_control =>
        r.In(() => (T)Dm.each<datum_round_event_control>(SSevents.control).First(c => c.GetType() == typeof(T)));

    [Fact]
    public void TheE1aPiecesAreLiveAndTheEventsAreInTheRoundsPool()
    {
        foreach (Type type in new[]
        {
            typeof(datum_round_event_anomaly), typeof(datum_round_event_anomaly_anomaly_bioscrambler), typeof(datum_round_event_anomaly_anomaly_bluespace),
            typeof(datum_round_event_anomaly_anomaly_dimensional), typeof(datum_round_event_anomaly_anomaly_ectoplasm), typeof(datum_round_event_anomaly_anomaly_flux),
            typeof(datum_round_event_anomaly_anomaly_grav), typeof(datum_round_event_anomaly_anomaly_grav_high), typeof(datum_round_event_anomaly_anomaly_hallucination),
            typeof(datum_round_event_anomaly_anomaly_pyro), typeof(datum_round_event_anomaly_anomaly_vortex),
            typeof(datum_round_event_vent_clog), typeof(datum_round_event_vent_clog_major), typeof(datum_round_event_vent_clog_critical), typeof(datum_round_event_vent_clog_strange),
            typeof(obj_effect_anomaly_bluespace), typeof(obj_effect_anomaly_bluespace_big), typeof(obj_effect_anomaly_ectoplasm), typeof(obj_structure_ghost_portal),
            typeof(datum_component_haunted_item), typeof(datum_element_haunted), typeof(datum_ai_controller_haunted),
            typeof(datum_component_deadchat_control), typeof(datum_component_deadchat_control_cardinal_movement), typeof(obj_item_beacon),
            typeof(obj_item_plunger), typeof(obj_item_plunger_reinforced), typeof(obj_effect_decal_cleanable_fuel_pool), typeof(mob_living_basic_spider_maintenance),
            typeof(mob_living_basic_viscerator), typeof(mob_living_basic_pet_gondola), typeof(datum_element_web_walker), typeof(datum_status_effect_teleport_madness),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");

        var (r, _, _) = Room(4101);
        var controls = r.In(() => Dm.each<datum_round_event_control>(SSevents.control).Select(c => c.GetType()).ToList());
        Assert.Contains(typeof(datum_round_event_control_anomaly_anomaly_flux), controls);
        Assert.Contains(typeof(datum_round_event_control_anomaly_anomaly_ectoplasm), controls);
        Assert.Contains(typeof(datum_round_event_control_vent_clog_strange), controls);
        // the weather anomaly came with the weather (gaps E2a)
        Assert.Contains(typeof(datum_round_event_control_anomaly_anomaly_weather), controls);
    }

    [Fact]
    public void AnAnomalyEventDropsItsAnomalyInTheStationAndAnnouncesItToTheCrewAndTheGhosts()
    {
        var (r, feed, _) = Room(4102);
        mob_living_carbon_human al = r.Join(11, 4, "Al");
        r.In(() => al.ghostize(TRUE));
        r.Step(2);

        var control = Control<datum_round_event_control_anomaly_anomaly_bluespace>(r);
        r.In(() => Waiting.Detach(control.run_event()));
        obj_effect_anomaly_bluespace anomaly = null;
        for (int sec = 0; sec < 20 && anomaly == null; sec++)
        {
            r.Step(Second);
            anomaly = r.In(() => Dm.each<obj_effect_anomaly_bluespace>(Dm.world).FirstOrDefault());
        }
        Assert.NotNull(anomaly);
        Assert.True(feed.Any("Bluespace instability detected on long range scanners. Expected location:"), "the anomaly alert");
        Assert.True(feed.Any("Anomaly: Bluespace has an object of interest: the bluespace anomaly!"), "the ghosts' alert");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBluespaceDetonationMovesTheAreaToATrackingBeacon()
    {
        var (r, feed, bob) = Room(4103);
        var beacon = Place<obj_item_beacon>(r, 12, 8);
        Assert.Contains(beacon, r.In(() => Dm.each<obj_item_beacon>(GLOB.teleportbeacons).ToList()));
        var anomaly = Place<obj_effect_anomaly_bluespace>(r, 6, 5);
        r.In(() => Waiting.NoSleep(anomaly.detonate()));
        r.Step(4);
        Assert.True(feed.Any("Massive bluespace translocation detected."));

        // a beacon turned off is no destination
        r.In(() => { Waiting.NoSleep(beacon.attack_self(bob)); return 0; });
        Assert.Empty(r.In(() => Dm.each<obj_item_beacon>(GLOB.teleportbeacons).ToList()));
        Assert.Equal("beacon-off", r.In(() => beacon.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEctoplasmicOutburstHauntsTheItemsAroundAndOpensAGhostPortal()
    {
        var (r, feed, bob) = Room(4104);
        var items = new List<obj_item>();
        for (int x = 4; x <= 10; x++)
            items.Add(Place<obj_item_crowbar>(r, x, 7));
        var anomaly = Place<obj_effect_anomaly_ectoplasm>(r, 7, 5);
        r.In(() =>
        {
            anomaly.override_ghosts = TRUE;
            anomaly.effect_power = 50;
            anomaly.ghosts_orbiting = 2;
            anomaly.intensity_update();
            return 0;
        });
        Assert.Equal("ectoplasm_heavy", r.In(() => anomaly.icon_state));
        r.In(() => Waiting.NoSleep(anomaly.detonate()));
        r.Step(4);

        Assert.True(feed.Any("Anomaly has reached critical mass. Ectoplasmic outburst detected."));
        Assert.NotNull(r.In(() => Dm.each<obj_structure_ghost_portal>(Dm.world).FirstOrDefault()));
        var haunted = items.Where(i => r.In(() => i.GetComponent(typeof(datum_component_haunted_item)) != null)).ToList();
        Assert.NotEmpty(haunted);
        obj_item spooky = haunted[0];
        Assert.True(r.In(() => HAS_TRAIT(spooky, TRAIT_MOVE_FLYING)), "a haunted item floats");
        Assert.IsType<datum_ai_controller_haunted>(r.In(() => spooky.ai_controller));
        Assert.True(feed.Any("slowly rises upward, hanging menacingly in the air..."));

        // the haunting ends: the item lands, unhaunted
        r.In(() => { ((datum_component_haunted_item)spooky.GetComponent(typeof(datum_component_haunted_item))).clear_haunting(); return 0; });
        r.Step(2);
        Assert.False(r.In(() => HAS_TRAIT(spooky, TRAIT_MOVE_FLYING)));
        Assert.Null(r.In(() => spooky.ai_controller));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AVentClogSpewsFilthAndCreaturesUntilAPlungerSendsThemBack()
    {
        var (r, feed, bob) = Room(4105);
        var vent = Place<obj_machinery_atmospherics_components_unary_vent_pump_on>(r, 4, 5);
        var control = Control<datum_round_event_control_vent_clog>(r);
        r.In(() => Waiting.Detach(control.run_event()));
        datum_round_event_vent_clog clog = null;
        for (int sec = 0; sec < 10 && (clog == null || r.In(() => clog.living_mobs.len) == 0); sec++)
        {
            r.Step(Second);
            clog = r.In(() => Dm.each<datum_round_event_vent_clog>(SSevents.running).FirstOrDefault());
        }
        Assert.NotNull(clog);
        Assert.Same(vent, r.In(() => clog.vent));
        Assert.True(r.In(() => clog.living_mobs.len) >= 1, "a creature crawled out");
        Assert.True(feed.Any("crawls out of"));
        Assert.NotEmpty(r.In(() => Dm.each<obj_effect_decal_cleanable_dirt>(range(2, vent)).ToList()));

        // a plunger on the clogged vent ends the event
        var plunger = Place<obj_item_plunger>(r, 3, 5);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(plunger)));
        Click(r, bob, vent);
        r.Step(7 * Second);
        Assert.True(feed.Any("plunging clogged vent..."), "the plunging balloon");
        Assert.DoesNotContain(clog, r.In(() => Dm.each<datum_round_event_vent_clog>(SSevents.running).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFuelPoolBurnsAndLightsItsNeighbours()
    {
        var (r, _, _) = Room(4106);
        var pool = Place<obj_effect_decal_cleanable_fuel_pool>(r, 7, 7);
        var other = Place<obj_effect_decal_cleanable_fuel_pool>(r, 8, 7);
        r.In(() => Waiting.NoSleep(pool.fire_act(1000, 100)));
        Assert.Equal(TRUE, r.In(() => pool.burning));
        Assert.NotNull(r.In(() => Dm.locate<obj_effect_hotspot>(locate(7, 7, 1))));
        r.Step(Second);
        Assert.Equal(TRUE, r.In(() => other.burning));
    }

    [Fact]
    public void ThePlungerAndTheVentClogsCreatures()
    {
        var (r, _, bob) = Room(4107);
        var plunger = Place<obj_item_plunger>(r, 3, 5);
        r.In(() => Waiting.NoSleep(plunger.attack_self(bob)));
        Assert.Equal((TRUE, "plunger_layer"), r.In(() => (plunger.layer_mode, plunger.icon_state)));

        var spider = Place<mob_living_basic_spider_maintenance>(r, 9, 5);
        Assert.True(r.In(() => HAS_TRAIT(spider, TRAIT_VENTCRAWLER_ALWAYS)));
        r.In(() => Waiting.NoSleep(spider.Move(locate(10, 5, 1))));
        Assert.NotNull(r.In(() => spider.has_movespeed_modifier(typeof(datum_movespeed_modifier_average_web))));

        var gondola = Place<mob_living_basic_pet_gondola>(r, 11, 7);
        Assert.True(r.In(() => HAS_TRAIT(gondola, TRAIT_MUTE)));
        Assert.Equal(3, r.In(() => Dm.length(gondola.overlays)));

        var viscerator = Place<mob_living_basic_viscerator>(r, 12, 3);
        Assert.True(r.In(() => HAS_TRAIT(viscerator, TRAIT_MOVE_FLYING)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGhostsCanSteerACorgi()
    {
        var (r, feed, _) = Room(4108);
        mob_living_carbon_human al = r.Join(11, 4, "Al");
        r.In(() => al.ghostize(TRUE));
        r.Step(2);
        var corgi = Place<mob_living_basic_pet_dog_corgi>(r, 7, 7);
        r.In(() => Waiting.NoSleep(corgi.deadchat_plays()));
        Assert.NotNull(r.In(() => corgi.GetComponent(typeof(datum_component_deadchat_control))));
        Assert.Null(r.In(() => corgi.ai_controller));
        Assert.True(feed.Any("is now deadchat controllable!"));
        r.AssertNoRuntimes();
    }
}
