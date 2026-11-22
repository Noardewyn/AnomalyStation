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
/// Gaps package E1b (D327): the machinery and command events in a booted toy room whose level and area are the station's. The grid
/// check darkens the station's APCs; the electrical storm blows the lights around it; the grey tide bolts open a department's airlocks
/// and unlocks its lockers; the brand intelligence turns the vendors against the crew until patient zero falls silent; the market
/// crash raises the vendors' prices; the night shift dims the station's lights; the bureaucratic error reshuffles the job slots; the
/// false alarm announces an event that never comes; the supermatter surge raises the crystal's output for a while.
/// </summary>
public class EventsE1bTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, Action<World> extras = null)
    {
        var r = new AudioRoom(15, 11, seed: seed, mapExtras: extras);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            // and a managed station level, which SSmapping keeps for a loaded map (power_fail reads ZTRAIT_STATION)
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            var room = (area)get_area(bob);
            GLOB.the_station_areas = Dm.list(room.type);
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Control<T>(AudioRoom r) where T : datum_round_event_control =>
        r.In(() => (T)Dm.each<datum_round_event_control>(SSevents.control).First(c => c.GetType() == typeof(T)));

    private static T RunEvent<T>(AudioRoom r, datum_round_event_control control, int seconds) where T : datum_round_event
    {
        r.In(() => Waiting.Detach(control.run_event()));
        r.Step(seconds * Second);
        return r.In(() => Dm.each<T>(SSevents.running).FirstOrDefault());
    }

    [Fact]
    public void TheE1bEventsAreLiveAndInThePool()
    {
        foreach (Type type in new[]
        {
            typeof(datum_round_event_electrical_storm), typeof(datum_round_event_grid_check), typeof(datum_round_event_communications_blackout),
            typeof(datum_round_event_processor_overload), typeof(datum_round_event_gravity_generator_blackout), typeof(datum_round_event_supermatter_surge),
            typeof(datum_round_event_supermatter_surge_poly), typeof(datum_round_event_grey_tide), typeof(datum_round_event_brand_intelligence),
            typeof(datum_round_event_falsealarm), typeof(datum_round_event_nightshift), typeof(datum_round_event_bureaucratic_error),
            typeof(datum_round_event_market_crash), typeof(datum_ai_controller_vending_machine), typeof(datum_ai_controller_vending_machine_eventspawn),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        var (r, _, _) = Room(4201);
        var names = r.In(() => Dm.each<datum_round_event_control>(SSevents.control).Select(c => Dm.str(c.name)).ToList());
        Assert.Contains("Grid Check", names);
        Assert.Contains("Brand Intelligence", names);
        Assert.Contains("False Alarm", names);
    }

    [Fact]
    public void TheGridCheckDarkensTheStationsApcs()
    {
        obj_machinery_power_apc apc = null;
        var (r, feed, _) = Room(4202, w => apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(7, 2, 1)));
        Assert.NotNull(r.In(() => apc.cell));
        Assert.True(r.In(() => Dm.truthy(SSmapping.level_trait(apc.z, ZTRAIT_STATION))), "a station level");
        RunEvent<datum_round_event_grid_check>(r, Control<datum_round_event_control_grid_check>(r), 6);
        Assert.True(feed.Any("power will be shut off for an indeterminate duration."));
        Assert.True(r.In(() => apc.failure_timer) > 0, "the APC fails for a while");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGreyTideBoltsOpenTheDepartmentsAirlocksAndUnlocksItsLockers()
    {
        var (r, _, bob) = Room(4203);
        var door = Place<obj_machinery_door_airlock>(r, 9, 6);
        var locker = Place<obj_structure_closet_secure_closet>(r, 11, 6);
        Assert.Equal(TRUE, r.In(() => locker.locked));
        var tide = r.In(() => Dm.@new<datum_round_event_grey_tide>(TRUE, Control<datum_round_event_control_grey_tide>(r)));
        r.In(() =>
        {
            tide.grey_tide_areas = Dm.list(((area)get_area(bob)).type);
            Waiting.NoSleep(tide.end());
            return 0;
        });
        r.Step(4 * Second);
        Assert.Equal(FALSE, r.In(() => door.density));
        Assert.True(r.In(() => Dm.truthy(door.locked)), "bolted open");
        Assert.Equal(FALSE, r.In(() => locker.locked));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBrandIntelligenceSpreadsUntilTheVendorsRiseUp()
    {
        var (r, feed, _) = Room(4204);
        var first = Place<obj_machinery_vending_cola>(r, 6, 8);
        var second = Place<obj_machinery_vending_snack>(r, 9, 8);
        var brand = RunEvent<datum_round_event_brand_intelligence>(r, Control<datum_round_event_control_brand_intelligence>(r), 4);
        Assert.NotNull(brand);
        var zero = r.In(() => brand.origin_machine);
        Assert.Contains(zero, new obj_machinery_vending[] { first, second });
        Assert.Equal(TRUE, r.In(() => zero.shoot_inventory));
        obj_machinery_vending other = zero == first ? second : (obj_machinery_vending)first;
        // the other vendor is infected on an even event tick; with every vendor infected, the infected ones rise up
        for (int sec = 0; sec < 20 && r.In(() => other.ai_controller) == null; sec++)
            r.Step(Second);
        Assert.Equal(TRUE, r.In(() => other.shoot_inventory));
        Assert.IsType<datum_ai_controller_vending_machine_eventspawn>(r.In(() => other.ai_controller));
        Assert.Equal(FALSE, r.In(() => other.tiltable));
        Assert.DoesNotContain(brand, r.In(() => Dm.each<datum_round_event_brand_intelligence>(SSevents.running).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASilencedPatientZeroEndsTheBrandIntelligence()
    {
        var (r, feed, _) = Room(4210);
        var vendors = new List<obj_machinery_vending>();
        for (int x = 4; x <= 12; x += 2)
            vendors.Add(Place<obj_machinery_vending_cola>(r, x, 8));
        var brand = RunEvent<datum_round_event_brand_intelligence>(r, Control<datum_round_event_control_brand_intelligence>(r), 4);
        Assert.NotNull(brand);
        var zero = r.In(() => brand.origin_machine);
        // patient zero's speaker switched off: the vendors calm down
        r.In(() => zero.shut_up = TRUE);
        r.Step(4 * Second);
        Assert.True(feed.Any("beeps and seems lifeless."));
        // the infected ones stop throwing their stock (tg leaves patient zero's own throwing on)
        Assert.All(vendors.Where(v => v != zero), v => Assert.Equal(FALSE, r.In(() => v.shoot_inventory)));
        Assert.DoesNotContain(brand, r.In(() => Dm.each<datum_round_event_brand_intelligence>(SSevents.running).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMarketCrashRaisesTheVendorsPricesUntilItEnds()
    {
        var (r, feed, _) = Room(4205);
        var vendor = Place<obj_machinery_vending_cola>(r, 6, 8);
        // an account on the books: with none, the economy's inflation is 1 (economy.dm:231)
        r.In(() => Dm.@new<datum_bank_account>("Bob", null, 1, TRUE));
        float before = r.In(() => vendor.default_price);
        var crash = RunEvent<datum_round_event_market_crash>(r, Control<datum_round_event_control_market_crash>(r), 14); // the fifth event tick reprices
        Assert.NotNull(crash);
        Assert.True(r.In(() => HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING)));
        Assert.True(feed.Any("prices for on-station vendors will be increased for a short period."));
        Assert.True(r.In(() => vendor.default_price) > before, "the vendor's prices climb: " + before + " -> " + r.In(() => vendor.default_price) + ", inflation " + r.In(() => SSeconomy.inflation_value__proc()) + ", accounts " + r.In(() => SSeconomy.bank_accounts_by_id.len) + ", active " + r.In(() => crash.activeFor));
        r.In(() => Waiting.NoSleep(crash.end()));
        Assert.False(r.In(() => HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING)));
        Assert.True(feed.Any("Prices for on-station vendors have now stabilized."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheNightShiftDimsTheStationsLights()
    {
        obj_machinery_power_apc apc = null;
        var (r, feed, _) = Room(4206, w => apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(7, 2, 1)));
        RunEvent<datum_round_event_nightshift>(r, Control<datum_round_event_control_nightshift>(r), 6);
        Assert.True(feed.Any("Good evening, crew."));
        Assert.Equal(TRUE, r.In(() => apc.nightshift_lights));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBureaucraticErrorReshufflesTheJobSlots()
    {
        var (r, feed, _) = Room(4207);
        var before = r.In(() => Dm.each<datum_job>(SSjob.get_valid_overflow_jobs()).Select(j => j.total_positions).ToList());
        Assert.NotEmpty(before);
        RunEvent<datum_round_event_bureaucratic_error>(r, Control<datum_round_event_control_bureaucratic_error>(r), 6);
        var after = r.In(() => Dm.each<datum_job>(SSjob.get_valid_overflow_jobs()).Select(j => j.total_positions).ToList());
        Assert.Equal(before.Count, after.Count); // the static list keeps every job
        Assert.NotEqual(before, after);
        Assert.True(feed.Any("A recent bureaucratic error in the Organic Resources Department"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFalseAlarmAnnouncesAnEventThatNeverComes()
    {
        var (r, feed, _) = Room(4208);
        int before = r.In(() => (int)SSevents.running.len);
        RunEvent<datum_round_event_falsealarm>(r, Control<datum_round_event_control_falsealarm>(r), 4);
        r.AssertNoRuntimes();
        string chat = string.Join("\n", feed.Chat.Select(c => c.Html));
        Assert.True(chat.Contains("major_announcement_text", StringComparison.Ordinal), "an alarm: " + chat);
        Assert.True(r.In(() => (int)SSevents.running.len) <= before, "nothing runs behind the alarm");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSupermatterSurgeRaisesTheCrystalsOutputForAWhile()
    {
        var (r, feed, _) = Room(4209);
        var sm = r.In(() =>
        {
            var crystal = Dm.@new<obj_machinery_power_supermatter_crystal_engine>(locate(10, 7, 1));
            crystal.time_frozen();
            crystal.activation_logged = TRUE;
            return crystal;
        });
        r.Step(Second);
        Assert.Same(sm, r.In(() => GLOB.main_supermatter_engine));
        var surge = RunEvent<datum_round_event_supermatter_surge>(r, Control<datum_round_event_control_supermatter_surge>(r), 4);
        Assert.NotNull(surge);
        float surgeClass = r.In(() => surge.surge_class);
        Assert.InRange(surgeClass, 1, 4);
        Assert.Equal(surgeClass + 5, r.In(() => sm.bullet_energy));
        r.Step(10 * Second);
        Assert.True(feed.Any("Supermatter Surge Alert") || feed.Any("energy output from the supermatter crystal has increased significantly"));
        r.In(() => Waiting.NoSleep(surge.end()));
        Assert.Equal(2f, r.In(() => sm.bullet_energy));
        Assert.True(feed.Any("The supermatter surge has dissipated"));
        r.AssertNoRuntimes();
    }
}
