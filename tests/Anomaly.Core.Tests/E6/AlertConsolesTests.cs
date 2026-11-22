using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.E6;

/// <summary>
/// The engineering package E6 (D287): tg's alarm plumbing (alarm.dm: the handlers' alarms reach the listeners of their types, areas and
/// levels, one entry per area with its sources), the station alert console (station_alert.dm and datums/station_alert.dm: its alarm
/// categories and its alert:2 screen), the atmospheric alert console (atmos_alert.dm: the air alarms in danger and in warning, its
/// alert screens), Canary on the engineering console (programs/alarm.dm: its alert-red screen and header) and the power flow control
/// console (apc_control.dm: the APC list, the log-in by access, a channel and a breaker set, the emag's open console and its restore).
/// </summary>
public class AlertConsolesTests
{
    private const int Second = 20;

    private static string[] States(object overlays) => Dm.each<object>(overlays as DmList)
        .Select(o => o is mutable_appearance ma ? Dm.str(ma.icon_state) : Dm.str(o)).ToArray();

    private static DmList Alarm(datum_alarm_listener listener, string type, area where) => (listener.alarms[type] as DmList)?[where.name] as DmList;

    [Fact]
    public void A_listener_takes_the_alarms_of_its_types_areas_and_levels_one_entry_per_area()
    {
        var r = new T2Room(2861);
        r.In(() =>
        {
            area room = r.Room.Room;
            var listener = Dm.@new<datum_alarm_listener>(Dm.list(ALARM_FIRE, ALARM_POWER), null, null);
            var elsewhere = Dm.@new<datum_alarm_listener>(Dm.list(ALARM_FIRE), Dm.list(2f), null);
            var otherAreas = Dm.@new<datum_alarm_listener>(Dm.list(ALARM_FIRE), null, Dm.list(typeof(area_station_engineering_main)));
            var ourArea = Dm.@new<datum_alarm_listener>(Dm.list(ALARM_FIRE), null, Dm.list(room.type));
            var first = Dm.@new<datum_alarm_handler>(locate(3, 3, 1));
            var second = Dm.@new<datum_alarm_handler>(locate(4, 3, 1));

            // two sources in one area: one entry (the area, its cameras, the sources)
            Assert.True(Dm.truthy(first.send_alarm(ALARM_FIRE)));
            Assert.True(Dm.truthy(second.send_alarm(ALARM_FIRE)));
            Assert.False(Dm.truthy(first.send_alarm(ALARM_FIRE))); // a handler counts once per area
            DmList fire = Alarm(listener, ALARM_FIRE, room);
            Assert.Same(room, fire[1]);
            Assert.Equal(new object[] { first, second }, Dm.each<object>(fire[3] as DmList).ToArray());
            Assert.Equal(1, (listener.alarms[ALARM_FIRE] as DmList).len);
            // the other listeners: a level and areas of their own
            Assert.Equal(0, elsewhere.alarms.len);
            Assert.Equal(0, otherAreas.alarms.len);
            Assert.NotNull(Alarm(ourArea, ALARM_FIRE, room));
            // a type it does not listen for
            first.send_alarm(ALARM_ATMOS);
            Assert.Null(listener.alarms[ALARM_ATMOS]);

            // cleared source by source: the entry goes with the last one, and the type with its last entry
            first.clear_alarm(ALARM_FIRE);
            Assert.Equal(new object[] { second }, Dm.each<object>(Alarm(listener, ALARM_FIRE, room)[3] as DmList).ToArray());
            second.clear_alarm(ALARM_FIRE);
            Assert.Equal(0, listener.alarms.len);

            // a listener that refuses changes (a broken console) keeps what it had
            listener.prevent_alarm_changes();
            first.send_alarm(ALARM_POWER);
            Assert.Equal(0, listener.alarms.len);
            first.clear_alarm(ALARM_POWER);
            listener.allow_alarm_changes();
            first.send_alarm(ALARM_POWER);
            Assert.NotNull(Alarm(listener, ALARM_POWER, room));
        });
        r.AssertNoRuntimes();
    }

    private static void SetAir(T2Room r, string mix) => r.In(() =>
    {
        for (int x = 1; x < T2Room.Size; x++)
            for (int y = 1; y < T2Room.Size; y++)
                if (locate(x, y, 1) is turf_open t)
                {
                    t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
                    SSair.add_to_active(t);
                }
    });

    [Fact]
    public void A_depressurised_room_shows_on_the_station_alert_and_atmos_alert_consoles_and_canary_until_its_air_returns()
    {
        var r = new T2Room(2862);
        obj_machinery_computer_station_alert station = null;
        obj_machinery_computer_atmos_alert_station_only atmos = null;
        obj_machinery_modular_computer_preset_engineering engineering = null;
        datum_computer_file_program_alarm_monitor canary = null;
        obj_machinery_airalarm alarm = null;
        r.In(() =>
        {
            // the test room counts as a station area (the consoles listen to the station's areas)
            GLOB.the_station_areas ??= Dm.list();
            if (!Dm.@in(r.Room.Room.type, GLOB.the_station_areas))
                Dm.append(ref GLOB.the_station_areas, r.Room.Room.type);
            station = Dm.@new<obj_machinery_computer_station_alert>(locate(3, 8, 1));
            atmos = Dm.@new<obj_machinery_computer_atmos_alert_station_only>(locate(4, 8, 1));
            engineering = Dm.@new<obj_machinery_modular_computer_preset_engineering>(locate(5, 8, 1));
            canary = Dm.each<datum_computer_file_program_alarm_monitor>(engineering.cpu.stored_files).Single();
            alarm = Dm.@new<obj_machinery_airalarm>(locate(8, 8, 1));
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(("alert:0", "atmos_key"), (station.icon_screen, station.icon_keyboard));
            Assert.Equal(1f, Dm.num(((obj_item_circuitboard_computer_atmos_alert)atmos.circuit_value).station_only));
            // nominal: every category empty, in the console's order
            var data = (DmList)Waiting.NoSleep(station.alert_control.ui_data(null));
            Assert.Equal(0f, Dm.num(data["cameraView"]));
            Assert.Equal(new[] { "Atmosphere", "Fire", "Power" }, Dm.each<DmList>(data["alarms"]).Select(c => Dm.str(c["name"])));
            Assert.All(Dm.each<DmList>(data["alarms"]), c => Assert.Equal(0, (c["alerts"] as DmList).len));
            Assert.DoesNotContain("alert:2", States(Waiting.NoSleep(station.update_overlays())));
            Assert.Equal(TRUE, atmos.process());
            Assert.Equal((0, 0), (atmos.priority_alarms.len, atmos.minor_alarms.len));
            Assert.Equal(("alert-green", "alarm_green.gif"), (Dm.str(canary.program_open_overlay), Dm.str(canary.ui_header)));
        });

        SetAir(r, AIRLESS_ATMOS);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Equal(AIR_ALARM_ALERT_HAZARD, alarm.danger_level);
            string areaName = Dm.str(get_area_name(alarm, TRUE));
            // the station alert console: the area under Atmosphere, its red screen
            var data = (DmList)Waiting.NoSleep(station.alert_control.ui_data(null));
            DmList first = Dm.each<DmList>(data["alarms"]).First();
            Assert.Equal("Atmosphere", Dm.str(first["name"]));
            DmList alert = Assert.Single(Dm.each<DmList>(first["alerts"]));
            Assert.Equal(areaName, Dm.str(alert["name"]));
            Assert.Null(alert["cameras"]); // no camera view on a console
            Assert.Equal(new[] { "Fire", "Power" }, Dm.each<DmList>(data["alarms"]).Skip(1).Select(c => Dm.str(c["name"])));
            Assert.Contains("alert:2", States(Waiting.NoSleep(station.update_overlays())));
            // the atmospheric alert console: a priority alarm at its next process
            atmos.process();
            Assert.Equal(new object[] { areaName }, Dm.each<object>(atmos.priority_alarms).ToArray());
            var atmosData = (DmList)Waiting.NoSleep(atmos.ui_data(null));
            Assert.Equal(new object[] { areaName }, Dm.each<object>(atmosData["priority"] as DmList).ToArray());
            Assert.Contains("alert:2", States(Waiting.NoSleep(atmos.update_overlays())));
            // Canary: the alert-red screen and header, the same alarms in its window's data
            Assert.Equal(("alert-red", "alarm_red.gif", 1f), (Dm.str(canary.program_open_overlay), Dm.str(canary.ui_header), canary.has_alert));
            var canaryData = (DmList)Waiting.NoSleep(canary.ui_data(null));
            Assert.Equal(areaName, Dm.str(Dm.each<DmList>(Dm.each<DmList>(canaryData["alarms"]).First()["alerts"]).Single()["name"]));

            // a warning-level alarm is a minor one (alert:1)
            alarm.danger_level = AIR_ALARM_ALERT_WARNING;
            atmos.process();
            Assert.Equal((0, 1), (atmos.priority_alarms.len, atmos.minor_alarms.len));
            var overlays = States(Waiting.NoSleep(atmos.update_overlays()));
            Assert.Contains("alert:1", overlays);
            Assert.DoesNotContain("alert:2", overlays);
        });

        SetAir(r, OPENTURF_DEFAULT_ATMOS);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Equal(AIR_ALARM_ALERT_NONE, alarm.danger_level);
            Assert.Equal(0, station.alert_control.listener.alarms.len);
            Assert.DoesNotContain("alert:2", States(Waiting.NoSleep(station.update_overlays())));
            atmos.process();
            Assert.Equal((0, 0), (atmos.priority_alarms.len, atmos.minor_alarms.len));
            Assert.Equal(("alert-green", "alarm_green.gif", 0f), (Dm.str(canary.program_open_overlay), Dm.str(canary.ui_header), canary.has_alert));

            // a broken station alert console stops taking alarms
            Waiting.NoSleep(station.set_machine_stat(Dm.bitor(station.machine_stat, BROKEN)));
            Assert.Equal(0f, station.alert_control.listener.accepting_alarm_changes);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_power_flow_console_logs_in_by_access_lists_the_apcs_and_sets_their_channels_and_breakers()
    {
        obj_machinery_power_apc apc = null;
        obj_machinery_computer_apc_control console = null;
        var r = new AudioRoom(13, 10, seed: 2863, mapExtras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            for (int x = 2; x <= 7; x++)
                Dm.@new<obj_structure_cable>(locate(x, 2, 1));
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(3, 2, 1));
            Dm.@new<Anomaly.Core.Tests.M7.obj_machinery_power_test_m7apc_source>(locate(7, 2, 1));
            console = Dm.@new<obj_machinery_computer_apc_control>(locate(5, 4, 1));
        });
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.Step(Second);
        r.In(() =>
        {
            var ui = Dm.@new<datum_tgui>(bob, console, "ApcControl");
            object Act(string action, DmList p = null) => Waiting.NoSleep(console.ui_act(action, p ?? Dm.list(), ui, null));
            DmList Data() => (DmList)Waiting.NoSleep(console.ui_data(bob));

            Assert.Equal(("solar", "power_key"), (console.icon_screen, console.icon_keyboard));
            Assert.Equal("[NULL]:", Data()["auth_id"]);
            Assert.Equal(0f, Dm.num(Data()["authenticated"]));
            // no ID: an unknown attempt; an ID without the chief engineer's access is refused
            Act("log-in");
            Assert.Equal(0f, console.authenticated);
            Assert.Equal("Unknown (Unknown):", console.auth_id);
            var assistant = Dm.@new<obj_item_card_id>(bob.loc);
            assistant.registered_name = "Bob";
            assistant.assignment = "Assistant";
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(assistant))));
            Act("log-in");
            Assert.Equal(0f, console.authenticated);
            Assert.Equal("Bob (Assistant):", console.auth_id);
            Waiting.NoSleep(bob.dropItemToGround(assistant));
            var debug = Dm.@new<obj_item_card_id_advanced_debug>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(debug))));
            Act("log-in");
            Assert.Equal(1f, console.authenticated);
            string authId = Dm.text("[] ([]):", debug.registered_name, debug.assignment);
            Assert.Equal(authId, console.auth_id);
            Assert.EndsWith(authId + " " + authId + " logged in to the terminal", Dm.str(console.logs[console.logs.len]));

            // the APC listed with its channels, its cell's charge and its reference
            DmList listed = Assert.Single(Dm.each<DmList>(Data()["apcs"]));
            Assert.Equal(apc.area.name, listed["name"]);
            Assert.Equal(REF(apc), listed["ref"]);
            Assert.Equal((3f, 3f, 3f), (Dm.num(listed["eqp"]), Dm.num(listed["lgt"]), Dm.num(listed["env"])));
            Assert.Equal(Dm.num(apc.cell.percent()), Dm.num(listed["charge"]));

            // the lighting channel off (the window's 3 -> 0), then on by hand (0 -> 2): the APC's channel and the log
            Act("toggle-minor", Dm.list(Dm.kv("type", "lighting"), Dm.kv("value", 0), Dm.kv("ref", REF(apc))));
            Assert.Equal(APC_CHANNEL_OFF, apc.lighting);
            Assert.EndsWith("Set APC " + Dm.str(apc.area.name) + " lighting to Off", Dm.str(console.logs[console.logs.len]));
            Act("toggle-minor", Dm.list(Dm.kv("type", "lighting"), Dm.kv("value", 2), Dm.kv("ref", REF(apc))));
            Assert.Equal(APC_CHANNEL_ON, apc.lighting);
            Assert.EndsWith("Set APC " + Dm.str(apc.area.name) + " lighting to On", Dm.str(console.logs[console.logs.len]));
            // the breaker
            Assert.Equal(TRUE, Act("breaker", Dm.list(Dm.kv("ref", REF(apc)))));
            Assert.False(Dm.truthy(apc.operating));
            Assert.EndsWith("Turned APC " + Dm.str(apc.area.name) + "'s breaker Off", Dm.str(console.logs[console.logs.len]));
            Act("breaker", Dm.list(Dm.kv("ref", REF(apc))));
            Assert.True(Dm.truthy(apc.operating));
            // an APC whose AI control is cut is not listed nor reachable
            apc.aidisabled = TRUE;
            Assert.Equal(0, (Data()["apcs"] as DmList).len);
            Assert.Null(Act("breaker", Dm.list(Dm.kv("ref", REF(apc)))));
            Assert.True(Dm.truthy(apc.operating));
            apc.aidisabled = FALSE;

            Act("log-out");
            Assert.Equal((0f, "[NULL]"), (console.authenticated, console.auth_id));

            // an emag shorts the access: anyone opens it as Unknown; logging stopped, then the console reset restores it
            Assert.Equal(TRUE, Waiting.NoSleep(console.emag_act(bob, null)));
            Assert.Equal(FALSE, Waiting.NoSleep(console.emag_act(bob, null)));
            Assert.Equal(EMAGGED, Dm.num(Data()["emagged"]));
            Act("log-in");
            Assert.Equal((1f, "Unknown (Unknown):"), (console.authenticated, console.auth_id));
            Act("toggle-logs");
            Assert.Equal(0f, console.should_log);
            float logged = console.logs.len;
            Act("check-logs");
            Assert.Equal(logged, console.logs.len);
            Act("restore-console");
            Assert.Equal(1f, console.restoring);
        });
        r.Step(46 * Second); // rand(3, 5) * 9 seconds
        r.In(() =>
        {
            Assert.Equal((0f, 1f), (console.restoring, console.should_log));
            Assert.Equal(0f, Dm.num(Dm.bitand(console.obj_flags, EMAGGED)));
            Assert.EndsWith("-=- Logging restored to full functionality at this point -=-", Dm.str(console.logs[console.logs.len]));
        });
        r.AssertNoRuntimes();
    }
}
