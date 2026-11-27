using System.Text.Json;
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
/// The exploration drones in a toy room (modules/explorer_drone): the scanner array and its console find sites with a wide scan and
/// reveal one with a point or a deep scan, which completes the exploration experiments; the scan conditions weigh the dishes; a scan
/// stops when a dish leaves the array; a drone on its fuelled launcher is taken over at its console, fitted with a tool, flown to a
/// site and home on the launcher's fuel; at a site it meets each kind of event (fluff, danger, resource, trader) and plays one of
/// tg's adventures through to its loot; the self-destruct loses the console's signal; the admins' adventure manager plays an
/// adventure; and every newly live type is made and destroyed. The worlds mount tg's strings files (the drones' names, the sites'
/// fluff and the adventures SSpersistence loads).
/// </summary>
public class ExplorationDroneTests
{
    private const int Decisecond = 2;

    private sealed class Bay
    {
        public AudioRoom R;
        public Feed Feed;
        public mob_living_carbon_human Bob;
        public obj_machinery_exoscanner Dish;
        public obj_machinery_computer_exoscanner_control ScanConsole;
        public obj_machinery_exodrone_launcher Launcher;
        public obj_item_exodrone Drone;
        public obj_machinery_computer_exodrone_control_console DroneConsole;
    }

    private static Bay Room(ulong seed, bool drone = true)
    {
        var bay = new Bay();
        bay.R = new AudioRoom(13, 11, seed: seed, mapExtras: w =>
        {
            bay.Dish = Dm.@new<obj_machinery_exoscanner>(locate(3, 8, 1));
            bay.ScanConsole = Dm.@new<obj_machinery_computer_exoscanner_control>(locate(5, 4, 1));
            bay.Launcher = Dm.@new<obj_machinery_exodrone_launcher>(locate(8, 4, 1));
            if (drone)
                bay.Drone = Dm.@new<obj_item_exodrone>(locate(8, 4, 1));
            bay.DroneConsole = Dm.@new<obj_machinery_computer_exodrone_control_console>(locate(10, 4, 1));
        });
        bay.Feed = Listen(bay.R);
        bay.Bob = bay.R.Join(5, 5, "Bob");
        bay.R.Step(Second);
        return bay;
    }

    private static void Goto(Bay bay, int x, int y)
    {
        bay.R.In(() => Waiting.NoSleep(bay.Bob.forceMove(locate(x, y, 1))));
        bay.R.Step(2);
    }

    private static JsonElement Open(Bay bay, atom machine, int x, int y, out string windowId)
    {
        Goto(bay, x, y);
        Take(bay.Bob);
        Click(bay.R, bay.Bob, machine);
        bay.R.Step(4);
        return LastFullUpdate(Take(bay.Bob), out windowId);
    }

    /// <summary>A site of a type at a distance, with the events and the conditions a test gives it (none by default).</summary>
    private static T Site<T>(Bay bay, float distance, params datum_exploration_event[] events) where T : datum_exploration_site
    {
        return bay.R.In(() =>
        {
            T site = Dm.@new<T>(1);
            site.distance = distance;
            site.scan_conditions = null;
            site.events = Dm.list();
            site.band_info = Dm.list();
            foreach (datum_exploration_event e in events)
                site.add_event(e);
            Dm.append(ref GLOB.exploration_sites, site);
            return site;
        });
    }

    private static void PutDroneAt(Bay bay, datum_exploration_site site)
    {
        bay.R.In(() =>
        {
            Waiting.NoSleep(bay.Drone.moveToNullspace());
            bay.Drone.location = site;
            site.on_drone_arrival(bay.Drone);
            bay.Drone.set_status(EXODRONE_EXPLORATION);
        });
    }

    private static string Log(Bay bay) => bay.R.In(() => string.Join(" | ", Dm.each(bay.Drone.drone_log).Select(Dm.str)));

    [Fact]
    public void TheWideScanFindsSitesWithTheArraysPower()
    {
        var bay = Room(14501, drone: false);
        Assert.Equal("scanner_ready", bay.R.In(() => bay.Dish.icon_state));
        Assert.Equal(1f, bay.R.In(() => Dm.num(GLOB.exoscanner_controller.calculate_scan_power())));

        JsonElement payload = Open(bay, bay.ScanConsole, 5, 5, out string window);
        Assert.Equal("ExoscannerConsole", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(1, data.GetProperty("scan_power").GetDouble());
        Assert.Equal(1200, data.GetProperty("wide_scan_eta").GetDouble()); // WIDE_SCAN_COST(1, 1): 2 minutes
        Assert.Equal(0, data.GetProperty("possible_sites").GetArrayLength());
        Assert.Equal(5, payload.GetProperty("static_data").GetProperty("all_bands").EnumerateObject().Count());

        Act(bay.R, bay.Bob, window, "start_wide_scan");
        data = Data(bay.R, bay.Bob, out _);
        Assert.Equal(1, data.GetProperty("scan_in_progress").GetInt32());
        Assert.Equal("Wide: Scanning sphere starting 1 AU from the station.", data.GetProperty("scan_description").GetString());
        Assert.Equal("scanner_on", bay.R.In(() => bay.Dish.icon_state));
        Assert.Contains(bay.R.Sounds.Heard, h => h.File == "sound/machines/terminal/terminal_processing.ogg");

        bay.R.Step(1200 * Decisecond + Second);
        bay.R.In(() =>
        {
            int sites = (int)length(GLOB.exploration_sites);
            Assert.InRange(sites, 1, 3); // 1 + rand(band - 1, band + 1) with the band at 1
            Assert.Equal(2f, GLOB.exoscanner_controller.wide_scan_band);
            Assert.Null(GLOB.exoscanner_controller.current_scan);
            foreach (datum_exploration_site site in Dm.each<datum_exploration_site>(GLOB.exploration_sites))
            {
                Assert.InRange(site.distance, 1, 3);
                Assert.Contains("°", Dm.str(site.coordinates), StringComparison.Ordinal);
                Assert.Equal("Anomaly", Dm.str(site.display_name()));
            }
        });
        Assert.Equal("scanner_ready", bay.R.In(() => bay.Dish.icon_state));
        data = Data(bay.R, bay.Bob, out _);
        Assert.Equal(0, data.GetProperty("scan_in_progress").GetInt32());
        Assert.InRange(data.GetProperty("possible_sites").GetArrayLength(), 1, 3);
        Assert.Equal(4800, data.GetProperty("wide_scan_eta").GetDouble()); // band 2: 4 * 2 * 60 * 10
        bay.R.AssertNoRuntimes();
    }

    [Fact]
    public void APointAndADeepScanRevealASiteAndCompleteTheExperiments()
    {
        var bay = Room(14502, drone: false);
        var site = Site<datum_exploration_site_asteroid_belt>(bay, 1, bay.R.In(() => Dm.@new<datum_exploration_event_simple_danger_carp_surface_variety>()));
        datum_experiment_exploration_scan_asteroid_belt pointExperiment = bay.R.In(() => Dm.@new<datum_experiment_exploration_scan_asteroid_belt>());
        datum_experiment_exploration_scan deepExperiment = bay.R.In(() =>
        {
            var experiment = Dm.@new<datum_experiment_exploration_scan>();
            experiment.required_scan_type = EXOSCAN_DEEP;
            experiment.required_condition = typeof(datum_scan_condition_easy);
            site.scan_conditions = Dm.list(Dm.locate_type(typeof(datum_scan_condition_easy), GLOB.scan_conditions));
            return experiment;
        });
        Assert.False(bay.R.In(() => Dm.truthy(pointExperiment.is_complete())));

        JsonElement payload = Open(bay, bay.ScanConsole, 5, 5, out string window);
        Act(bay.R, bay.Bob, window, "select_site", ("site_ref", bay.R.In(() => REF(site))));
        JsonElement data = Data(bay.R, bay.Bob, out _);
        Assert.Equal("Anomaly", data.GetProperty("site_data").GetProperty("name").GetString());
        Assert.Equal("No Data", data.GetProperty("site_data").GetProperty("description").GetString());
        Assert.Equal(2, data.GetProperty("scan_power").GetDouble()); // the easy scan doubles the dish
        Assert.Equal(600, data.GetProperty("point_scan_eta").GetDouble());
        Assert.Equal(900, data.GetProperty("deep_scan_eta").GetDouble());
        Assert.Contains(data.GetProperty("scan_conditions").EnumerateArray(), c => c.GetString().Contains("all dish power is doubled", StringComparison.Ordinal));

        Act(bay.R, bay.Bob, window, "start_point_scan");
        Assert.Equal("Point scan of Anomaly", Data(bay.R, bay.Bob, out _).GetProperty("scan_description").GetString());
        bay.R.Step(600 * Decisecond + Second);
        bay.R.In(() =>
        {
            Assert.True(Dm.truthy(site.point_scan_complete));
            Assert.Equal("asteroid belt", Dm.str(site.display_name()));
            Assert.True(Dm.truthy(pointExperiment.is_complete()));
            Assert.False(Dm.truthy(deepExperiment.is_complete()));
        });
        data = Data(bay.R, bay.Bob, out _);
        Assert.Equal("A dense asteroid belt.", data.GetProperty("site_data").GetProperty("description").GetString());

        Act(bay.R, bay.Bob, window, "start_deep_scan");
        bay.R.Step(900 * Decisecond + Second);
        bay.R.In(() =>
        {
            Assert.True(Dm.truthy(site.deep_scan_complete));
            Assert.True(Dm.truthy(deepExperiment.is_complete()));
            Assert.Contains("space carp", Dm.str(site.display_description()), StringComparison.Ordinal); // the carp's deep scan description
        });
        bay.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheScanConditionsWeighTheDishes()
    {
        var bay = Room(14503, drone: false);
        bay.R.In(() =>
        {
            datum_scan_condition Condition(Type type) => Dm.locate_type(type, GLOB.scan_conditions) as datum_scan_condition;
            Assert.Equal(5, length(GLOB.scan_conditions));
            Assert.Equal(2f, Dm.num(GLOB.exoscanner_controller.calculate_scan_power(Dm.list(Condition(typeof(datum_scan_condition_easy))))));
            // seven of the eight tiles around the dish are not walls
            Assert.Equal(0f, Dm.num(GLOB.exoscanner_controller.calculate_scan_power(Dm.list(Condition(typeof(datum_scan_condition_black_hole))))));
            // a lone dish is far enough from the others
            Assert.Equal(1f, Dm.num(GLOB.exoscanner_controller.calculate_scan_power(Dm.list(Condition(typeof(datum_scan_condition_nebula))))));
            // nothing else works within two tiles of the dish
            Assert.Equal(1f, Dm.num(GLOB.exoscanner_controller.calculate_scan_power(Dm.list(Condition(typeof(datum_scan_condition_pulsar))))));
            var second = Dm.@new<obj_machinery_exoscanner>(locate(4, 8, 1));
            Assert.Equal(2, length(GLOB.exoscanner_controller.tracked_dishes));
            Assert.Equal(0f, Dm.num(GLOB.exoscanner_controller.calculate_scan_power(Dm.list(Condition(typeof(datum_scan_condition_nebula))))));
            Assert.Equal(0f, Dm.num(GLOB.exoscanner_controller.calculate_scan_power(Dm.list(Condition(typeof(datum_scan_condition_pulsar))))));
            qdel(second);
            Assert.Equal(1, length(GLOB.exoscanner_controller.tracked_dishes));
        });
        bay.R.AssertNoRuntimes();
    }

    [Fact]
    public void AScanStopsWhenADishLeavesTheArray()
    {
        var bay = Room(14504, drone: false);
        JsonElement payload = Open(bay, bay.ScanConsole, 5, 5, out string window);
        Act(bay.R, bay.Bob, window, "start_wide_scan");
        Assert.NotNull(bay.R.In(() => GLOB.exoscanner_controller.current_scan));
        bay.R.In(() => bay.Dish.set_panel_open(TRUE));
        bay.R.Step(4);
        bay.R.In(() =>
        {
            Assert.Null(GLOB.exoscanner_controller.current_scan);
            Assert.Equal(0, length(GLOB.exoscanner_controller.tracked_dishes));
            Assert.True(Dm.truthy(bay.ScanConsole.failed_popup));
            Assert.Equal("scanner_open", bay.Dish.icon_state);
        });
        JsonElement data = Data(bay.R, bay.Bob, out _);
        Assert.Equal(1, data.GetProperty("failed").GetInt32());
        Assert.Equal(0, data.GetProperty("scan_power").GetDouble());
        Act(bay.R, bay.Bob, window, "confirm_fail");
        Assert.False(bay.R.In(() => Dm.truthy(bay.ScanConsole.failed_popup)));
        bay.R.AssertNoRuntimes();
    }

    [Fact]
    public void ADroneIsFuelledSentToASiteAndFliesHome()
    {
        var bay = Room(14505);
        var site = Site<datum_exploration_site_junkyard>(bay, 2);
        string droneName = bay.R.In(() => bay.Drone.name);
        Assert.Contains(droneName.Split(' ')[0], new[] { "Cassini", "Galileo", "Juno", "Luna", "Mariner", "Pioneer", "Rosetta", "Sojourner", "Stardust", "Vega", "Venera", "Voyager" });

        // the pellet into the launcher: its fuel overlay
        Goto(bay, 8, 5);
        obj_item_fuel_pellet pellet = bay.R.In(() =>
        {
            var made = Dm.@new<obj_item_fuel_pellet>(bay.Bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bay.Bob.put_in_active_hand(made))));
            return made;
        });
        Click(bay.R, bay.Bob, bay.Launcher);
        Assert.Same(pellet, bay.R.In(() => bay.Launcher.fuel_canister));
        Assert.Contains("launchpad_fuel_basic", bay.R.In(() => Dm.each(Waiting.NoSleep(bay.Launcher.update_overlays())).Select(Dm.str).ToList()));

        JsonElement payload = Open(bay, bay.DroneConsole, 10, 5, out string window);
        Assert.Equal("ExodroneConsole", InterfaceOf(payload));
        JsonElement drones = payload.GetProperty("data").GetProperty("all_drones");
        Assert.Equal(droneName, drones[0].GetProperty("name").GetString());
        Assert.Equal("Idle.", drones[0].GetProperty("description").GetString());
        Assert.Equal(5, payload.GetProperty("static_data").GetProperty("all_tools").EnumerateObject().Count());

        Act(bay.R, bay.Bob, window, "select_drone", ("drone_ref", drones[0].GetProperty("ref").GetString()));
        JsonElement data = Data(bay.R, bay.Bob, out _);
        Assert.True(bay.R.In(() => Dm.truthy(bay.Drone.controlled)));
        Assert.Contains(bay.R.Sounds.Heard, h => h.File == "sound/machines/terminal/terminal_on.ogg");
        Assert.Equal("idle", data.GetProperty("drone_status").GetString());
        Assert.Equal(1, data.GetProperty("configurable").GetInt32());
        Assert.Equal(1, data.GetProperty("can_travel").GetInt32());
        Assert.Equal(250, data.GetProperty("drone_travel_coefficent").GetDouble());
        Assert.Equal(6, data.GetProperty("cargo").EnumerateArray().Count(c => c.GetProperty("type").GetString() == "empty"));
        Assert.Equal("alert:2", bay.R.In(() => bay.DroneConsole.icon_screen));

        Act(bay.R, bay.Bob, window, "add_tool", ("tool_type", EXODRONE_TOOL_LASER));
        data = Data(bay.R, bay.Bob, out _);
        Assert.Equal("laser", data.GetProperty("cargo")[0].GetProperty("name").GetString());
        Assert.Equal(5, data.GetProperty("cargo").EnumerateArray().Count(c => c.GetProperty("type").GetString() == "empty"));
        Assert.Equal(5f, bay.R.In(() => bay.Drone.atom_storage.max_slots));

        Act(bay.R, bay.Bob, window, "start_travel", ("target_site", bay.R.In(() => REF(site))));
        bay.R.In(() =>
        {
            Assert.Null(bay.Drone.loc);
            Assert.Equal(EXODRONE_TRAVEL, bay.Drone.drone_status);
            Assert.Equal(500f, Dm.num(bay.Drone.travel_time)); // basic fuel: 250 a distance unit
            Assert.Equal(4f, pellet.uses);
            Assert.NotNull(Dm.locate<obj_effect_particle_effect_fluid_smoke>(Builtins.range(1, bay.Launcher)));
        });
        Assert.Contains(bay.R.Sounds.Heard, h => h.File == "sound/effects/podwoosh.ogg");
        data = Data(bay.R, bay.Bob, out _);
        Assert.Equal("travel", data.GetProperty("drone_status").GetString());
        Assert.InRange(data.GetProperty("travel_time_left").GetDouble(), 480, 500);
        // tg sends the status signal before it sets the status (exodrone.dm:145), so the screen still shows the idle drone's alert
        Assert.Equal("alert:2", bay.R.In(() => bay.DroneConsole.icon_screen));

        bay.R.Step(500 * Decisecond + Second);
        bay.R.In(() =>
        {
            Assert.Equal(EXODRONE_EXPLORATION, bay.Drone.drone_status);
            Assert.Same(site, bay.Drone.location);
            Assert.True(Dm.truthy(site.revealed));
        });
        Assert.Contains("Discovered space junk field at ", Log(bay), StringComparison.Ordinal);
        data = Data(bay.R, bay.Bob, out _);
        Assert.Equal("space junk field", data.GetProperty("site").GetProperty("name").GetString());

        // home: the site's distance on the fuel it left with
        Act(bay.R, bay.Bob, window, "start_travel");
        Assert.Equal(500f, bay.R.In(() => Dm.num(bay.Drone.travel_time)));
        bay.R.Step(500 * Decisecond + Second);
        bay.R.In(() =>
        {
            Assert.Equal(EXODRONE_IDLE, bay.Drone.drone_status);
            Assert.Null(bay.Drone.location);
            Assert.Same(bay.Launcher.loc, bay.Drone.loc);
        });
        Assert.Contains("Landing at the exploration drone launcher.", Log(bay), StringComparison.Ordinal);
        bay.R.AssertNoRuntimes();
    }

    [Fact]
    public void ADroneMeetsTheFluffDangerResourceAndTraderEvents()
    {
        var bay = Room(14506);
        var fluff = bay.R.In(() => Dm.@new<datum_exploration_event_fluff>());
        var collapse = bay.R.In(() => Dm.@new<datum_exploration_event_simple_danger_collapse>());
        var site = Site<datum_exploration_site_spacemine>(bay, 1, fluff, collapse);
        PutDroneAt(bay, site);
        JsonElement payload = Open(bay, bay.DroneConsole, 10, 5, out string window);
        Act(bay.R, bay.Bob, window, "select_drone", ("drone_ref", bay.R.In(() => REF(bay.Drone))));

        // the fluff: a line of the ruins' fluff in the log
        bay.R.In(() => bay.Drone.explore_site(fluff));
        Assert.False(bay.R.In(() => Dm.truthy(fluff.is_targetable())));
        bay.R.In(() => fluff.encounter(bay.Drone));
        string[] ruins = { "You encounter a broken statue.", "You have to backtrack from a collapsed passage.", "You pass through a corridor full of cracked tiles." };
        Assert.Contains(ruins, line => Log(bay).Contains(line, StringComparison.Ordinal));

        // the collapse without the drill: the drone squeezes out and is damaged
        Act(bay.R, bay.Bob, window, "explore");
        JsonElement data = Data(bay.R, bay.Bob, out _);
        JsonElement shown = data.GetProperty("event");
        Assert.Equal("Squeeze.", shown.GetProperty("action_text").GetString());
        Assert.Equal(0, shown.GetProperty("skippable").GetInt32());
        Assert.Equal("default", shown.GetProperty("image").GetString());
        Assert.Contains("scrape a few parts", shown.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Equal("Busy", data.GetProperty("travel_error").GetString());
        float before = bay.R.In(() => Dm.num(bay.Drone.get_integrity()));
        Act(bay.R, bay.Bob, window, "start_event");
        Assert.Equal(before - 30, bay.R.In(() => Dm.num(bay.Drone.get_integrity())));
        Assert.Contains("Sustained 30 damage.", Log(bay), StringComparison.Ordinal);
        Assert.Null(bay.R.In(() => bay.Drone.current_event_ui_data));

        // the same danger with the drill: dug out unharmed
        var collapse2 = bay.R.In(() => Dm.@new<datum_exploration_event_simple_danger_collapse>());
        bay.R.In(() =>
        {
            bay.Drone.tools = Dm.list(EXODRONE_TOOL_DRILL);
            site.add_event(collapse2);
            collapse2.encounter(bay.Drone);
            Assert.Equal("Dig out", Dm.str(bay.Drone.current_event_ui_data["action_text"]));
            Waiting.NoSleep(collapse2.fire(bay.Drone));
        });
        Assert.Equal(before - 30, bay.R.In(() => Dm.num(bay.Drone.get_integrity())));
        Assert.Contains("Dug out of collapsed passage.", Log(bay), StringComparison.Ordinal);

        // a resource: the welder opens the concealed cache after its delay, maintenance loot into the cargo
        var cache = bay.R.In(() => Dm.@new<datum_exploration_event_simple_resource_concealed_cache>());
        bay.R.In(() =>
        {
            bay.Drone.tools = Dm.list(EXODRONE_TOOL_WELDER);
            bay.Drone.update_storage_size();
            site.add_event(cache);
            bay.Drone.explore_site(); // the only event not met yet
        });
        Assert.Same(cache, bay.R.In(() => Dm.locate_ref(bay.Drone.current_event_ui_data?["ref"])));
        Act(bay.R, bay.Bob, window, "start_event");
        data = Data(bay.R, bay.Bob, out _);
        Assert.Equal("busy", data.GetProperty("drone_status").GetString());
        Assert.Equal("Welding open the cache...", data.GetProperty("wait_message").GetString());
        bay.R.Step(300 * Decisecond + Second);
        bay.R.In(() =>
        {
            Assert.Equal(EXODRONE_EXPLORATION, bay.Drone.drone_status);
            Assert.Equal(1, length(bay.Drone.contents));
            Assert.Equal(0f, Dm.num(cache.amount));
        });
        Assert.Contains("Acquired ", Log(bay), StringComparison.Ordinal);

        // a trader: the vending machine takes a high capacity cell for its pills
        var vendor = bay.R.In(() => Dm.@new<datum_exploration_event_simple_trader_vendor_ai>());
        bay.R.In(() =>
        {
            foreach (atom_movable thing in Dm.each<atom_movable>(bay.Drone.contents).ToList())
                qdel(thing);
            var cell = Dm.@new<obj_item_stock_parts_power_store_cell_high>(bay.Drone);
            site.add_event(vendor);
            vendor.encounter(bay.Drone);
            Assert.Contains("high-capacity power cell", Dm.str(bay.Drone.current_event_ui_data["description"]), StringComparison.Ordinal);
            Assert.True(Dm.truthy(vendor.action_enabled(bay.Drone)));
            Waiting.NoSleep(vendor.fire(bay.Drone));
            Assert.True(QDELETED(cell));
            Assert.NotNull(Dm.locate<obj_item_storage_pill_bottle_happy>(bay.Drone));
            Assert.Equal(2f, Dm.num(vendor.amount));
            Assert.True(Dm.truthy(vendor.is_targetable()));
        });
        Assert.Contains("Traded ", Log(bay), StringComparison.Ordinal);
        data = Data(bay.R, bay.Bob, out _, Second); // no act: the window's next update from SStgui
        Assert.Contains(data.GetProperty("site").GetProperty("events").EnumerateArray(), e => e.GetProperty("name").GetString() == "sentient drug vending machine");
        bay.R.AssertNoRuntimes();
    }

    [Fact]
    public void ADronePlaysAnAdventureThroughToItsLoot()
    {
        var bay = Room(14507);
        datum_adventure_db_entry entry = bay.R.In(() =>
            Dm.each<datum_adventure_db_entry>(GLOB.explorer_drone_adventure_db_entries).Single(e => Dm.eq(e.filename, "robots_wingman.json")));
        Assert.Equal(5, bay.R.In(() => length(GLOB.explorer_drone_adventure_db_entries)));
        var adventureEvent = bay.R.In(() =>
        {
            var made = Dm.@new<datum_exploration_event_adventure>();
            made.adventure = entry.create_adventure() as datum_adventure;
            made.band_values = made.adventure.band_modifiers;
            return made;
        });
        var site = Site<datum_exploration_site_junkyard>(bay, 1, adventureEvent);
        Assert.Equal(2f, bay.R.In(() => Dm.num(site.band_info[EXOSCANNER_BAND_TECH])));
        PutDroneAt(bay, site);
        bay.R.In(() => bay.Drone.tools = Dm.list(EXODRONE_TOOL_TRANSLATOR));
        JsonElement payload = Open(bay, bay.DroneConsole, 10, 5, out string window);
        Act(bay.R, bay.Bob, window, "select_drone", ("drone_ref", bay.R.In(() => REF(bay.Drone))));
        Act(bay.R, bay.Bob, window, "explore");

        JsonElement data = Data(bay.R, bay.Bob, out _);
        Assert.Equal("adventure", data.GetProperty("drone_status").GetString());
        JsonElement adventure = data.GetProperty("adventure_data");
        Assert.StartsWith("data:image/png;base64,", adventure.GetProperty("raw_image").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, bay.R.In(() => Dm.num(bay.Drone.current_adventure.qualities[EXODRONE_TOOL_TRANSLATOR]))); // the tools are qualities

        void Choose(string choice, bool delayed)
        {
            Act(bay.R, bay.Bob, window, "adventure_choice", ("choice", choice));
            if (delayed)
            {
                Assert.Equal(EXODRONE_BUSY, bay.R.In(() => bay.Drone.drone_status));
                bay.R.Step(5 * Decisecond + 2);
            }
        }
        Choose("choice 0", delayed: true); // Hail other Drone
        Choose("choice 2", delayed: true); // Wait for Program to boot.
        Assert.Equal("Sentience Achieved", bay.R.In(() => Dm.str(bay.Drone.current_adventure.current_node.id)));
        Choose("choice 5", delayed: false); // Love + 1
        data = Data(bay.R, bay.Bob, out _);
        Assert.Contains("4", data.GetProperty("adventure_data").GetProperty("description").GetString(), StringComparison.Ordinal); // $$Love
        Assert.DoesNotContain("$$", data.GetProperty("adventure_data").GetProperty("description").GetString(), StringComparison.Ordinal);
        Choose("choice 11", delayed: false); // Love + 1
        Choose("choice 15", delayed: true); // Love + 1
        Assert.Equal(6f, bay.R.In(() => Dm.num(bay.Drone.current_adventure.qualities["Love"])));
        Choose("choice 18", delayed: true); // Accept Contract: WIN

        bay.R.In(() =>
        {
            Assert.Equal(EXODRONE_EXPLORATION, bay.Drone.drone_status);
            Assert.Null(bay.Drone.current_adventure);
            var chip = Dm.locate<obj_item_trade_chip>(bay.Drone);
            Assert.NotNull(chip);
            Assert.StartsWith("trade contract chip- ", chip.name, StringComparison.Ordinal);
            var pack = SSshuttle.supply_packs[chip.unlocked_pack_type] as datum_supply_pack;
            chip.try_to_unlock_contract(bay.Bob);
            Assert.True(Dm.truthy(Dm.bitand(pack.order_flags, ORDER_SPECIAL_ENABLED)));
        });
        Assert.Contains("Acquired trade contract chip", Log(bay), StringComparison.Ordinal);
        bay.R.AssertNoRuntimes();
    }

    [Fact]
    public void AFailedAdventureDamagesTheDroneAndItsSelfDestructLosesTheSignal()
    {
        var bay = Room(14508);
        var site = Site<datum_exploration_site_junkyard>(bay, 1);
        PutDroneAt(bay, site);
        bay.R.In(() =>
        {
            var entry = Dm.each<datum_adventure_db_entry>(GLOB.explorer_drone_adventure_db_entries).Single(e => Dm.eq(e.filename, "robots_wingman.json"));
            bay.Drone.start_adventure(entry.create_adventure() as datum_adventure);
            bay.Drone.current_adventure.select_choice("choice 1"); // Ignore other Drone: FAIL
            Assert.Equal(bay.Drone.max_integrity * 0.5f, Dm.num(bay.Drone.get_integrity()));
            Assert.Equal(EXODRONE_EXPLORATION, bay.Drone.drone_status);
        });

        JsonElement payload = Open(bay, bay.DroneConsole, 10, 5, out string window);
        Act(bay.R, bay.Bob, window, "select_drone", ("drone_ref", bay.R.In(() => REF(bay.Drone))));
        // tg's self-destruct is the drone's handle_deconstruct, which no longer deletes it (obj_defense.dm:183): its explosion and
        // sparks go off where it is, nowhere at a site, and the drone flies on
        Act(bay.R, bay.Bob, window, "self_destruct");
        bay.R.Step(Second);
        Assert.False(bay.R.In(() => QDELETED(bay.Drone)));
        // the rest of its integrity breaks it, and the console loses its signal
        bay.R.In(() => Waiting.NoSleep(bay.Drone.damage(Dm.num(bay.Drone.get_integrity()))));
        bay.R.Step(Second);
        Assert.True(bay.R.In(() => QDELETED(bay.Drone)));
        Assert.Contains(bay.R.Sounds.Heard, h => h.File == "sound/machines/terminal/terminal_alert.ogg");
        JsonElement data = Data(bay.R, bay.Bob, out _);
        Assert.Equal(1, data.GetProperty("signal_lost").GetInt32());
        Assert.Equal(0, data.GetProperty("all_drones").GetArrayLength());
        Act(bay.R, bay.Bob, window, "confirm_signal_lost");
        Assert.False(bay.R.In(() => Dm.truthy(bay.DroneConsole.signal_lost)));
        bay.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheAdventureManagerPlaysAnAdventure()
    {
        var bay = Room(14509, drone: false);
        bay.R.In(() => Dm.@new<datum_admins>(Dm.list(Dm.@new<datum_admin_rank>("!localhost!", RANK_SOURCE_LOCAL, R_EVERYTHING, R_DBRANKS, R_EVERYTHING)), bay.Bob.ckey, 1, 1));
        Assert.True(bay.R.In(() => bay.Bob.client.holder != null), "the admin's rights");
        bay.R.In(() => Waiting.Detach(bay.Bob.client.adventure_manager()));
        JsonElement data = Data(bay.R, bay.Bob, out string window);
        Assert.Equal(5, data.GetProperty("adventures").GetArrayLength());
        Assert.Equal(0, data.GetProperty("play_mode").GetInt32());
        string entry = bay.R.In(() => Dm.str(REF(Dm.each<datum_adventure_db_entry>(GLOB.explorer_drone_adventure_db_entries).Single(e => Dm.eq(e.filename, "space_yacht.json")))));
        Act(bay.R, bay.Bob, window, "play", ("ref", entry));
        data = Data(bay.R, bay.Bob, out _);
        Assert.Equal("Adventure started", data.GetProperty("feedback_message").GetString());
        Assert.Equal(1, data.GetProperty("play_mode").GetInt32());
        Assert.True(data.GetProperty("adventure_data").GetProperty("choices").GetArrayLength() > 0);
        Act(bay.R, bay.Bob, window, "end_play");
        data = Data(bay.R, bay.Bob, out _);
        Assert.Equal("Adventure stopped", data.GetProperty("feedback_message").GetString());
        Assert.Equal(0, data.GetProperty("play_mode").GetInt32());
        bay.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheLootGeneratorsFillTheCargo()
    {
        var bay = Room(14510);
        bay.R.In(() =>
        {
            Assert.Equal(9, length(GLOB.adventure_loot_generator_index));
            foreach (object id in Dm.each(GLOB.adventure_loot_generator_index).ToList())
            {
                foreach (atom_movable thing in Dm.each<atom_movable>(bay.Drone.contents).ToList())
                    qdel(thing);
                var generator = Dm.@new((Type)GLOB.adventure_loot_generator_index[id]) as datum_adventure_loot_generator;
                Waiting.NoSleep(generator.transfer_loot(bay.Drone));
                Assert.True(length(bay.Drone.contents) >= 1, "the " + Dm.str(id) + " loot");
            }
            // a full cargo abandons the loot
            bay.Drone.tools = Dm.list(EXODRONE_TOOL_WELDER, EXODRONE_TOOL_LASER, EXODRONE_TOOL_DRILL, EXODRONE_TOOL_MULTITOOL, EXODRONE_TOOL_TRANSLATOR);
            Assert.Equal(0f, Dm.num(bay.Drone.space_left()));
            var antique = Dm.@new<obj_item_antique>();
            Waiting.NoSleep(bay.Drone.try_transfer(antique));
            Assert.True(QDELETED(antique));
        });
        Assert.Contains("Abandoned antique due to lack of space.", Log(bay), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryNewlyLiveTypeIsMadeAndDestroyed()
    {
        var bay = Room(14511);
        var types = new List<Type>();
        bay.R.In(() =>
        {
            foreach (Type root in new[] { typeof(datum_exploration_site), typeof(datum_exploration_event), typeof(datum_scan_condition), typeof(datum_adventure_loot_generator) })
                types.AddRange(Dm.each(typesof(root)).Cast<Type>());
            types.AddRange(new[] { typeof(datum_adventure), typeof(datum_adventure_node), typeof(datum_adventure_db_entry), typeof(datum_adventure_browser), typeof(datum_scanner_controller) });
            foreach (Type type in types)
                Assert.False(InertSeam.IsInertType(type), DmTypes.PathOf(type) + " is inert");
            foreach (Type type in types)
            {
                datum made = Dm.@new(type, 1) as datum;
                Assert.NotNull(made);
                qdel(made);
            }
            foreach (Type type in new[] { typeof(obj_item_exodrone), typeof(obj_item_fuel_pellet), typeof(obj_item_fuel_pellet_advanced), typeof(obj_item_fuel_pellet_exotic),
                typeof(obj_item_firelance), typeof(obj_item_antique), typeof(obj_item_trade_chip), typeof(obj_machinery_exodrone_launcher), typeof(obj_machinery_exoscanner),
                typeof(obj_machinery_computer_exoscanner_control), typeof(obj_machinery_computer_exodrone_control_console) })
            {
                Assert.False(InertSeam.IsInertType(type), DmTypes.PathOf(type) + " is inert");
                var thing = Dm.@new(type, locate(6, 7, 1)) as atom;
                Assert.NotNull(thing);
                qdel(thing);
            }
        });
        bay.R.Step(Second);
        bay.R.AssertNoRuntimes();
    }
}
