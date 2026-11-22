using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The engineering package E6 (D287) on MetaStation through the real server and a client's own stack. An engineer pulls a fire alarm:
/// one of the station alert consoles lists its area under Fire Alarms and lights its red alert screen; the alarm reset (a right
/// click) clears it. A room depressurised: its air alarm's hazard reaches the station alert console (Atmosphere), one of Meta's
/// atmospheric alert consoles (a priority alert, cleared by its button until the console's next check) and Canary on an engineering
/// console (NtosMain to NtosStationAlertConsole, the console's alert-red screen); the room's air back clears them. The power flow
/// control console: refused without access, logged in by a debug ID, an APC's lighting channel and breaker set from its table, the
/// APC's own window opened remotely, closed with the console's window. No new runtime.
/// </summary>
public class AlertConsolesGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf Beside(atom thing)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        if (seen == null || !replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look))
            return shown;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        }
        return shown;
    }

    private static bool Working(obj_machinery m) => !Dm.truthy(Dm.bitand(m.machine_stat, Dm.bitor(NOPOWER, BROKEN))) && Beside(m) != null;

    private static void SetAir(area room, string mix)
    {
        foreach (DmList zlevel in Dm.each_cast<DmList>(room.get_zlevel_turf_lists()))
        {
            foreach (turf_open t in Dm.each<turf_open>(zlevel))
            {
                t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
                SSair.add_to_active(t);
            }
        }
    }

    private static List<string> AlertsOf(UiNode content, string category) => content.Walk().First(n => n.Kind == UiKind.Section && n.Key == category)
        .Walk().Where(n => n.Key == "name" || n.Key == "nominal").Select(n => n.Text).ToList();

    [MetaServerFact]
    public void The_alert_consoles_list_the_stations_alarms_and_the_power_flow_console_sets_an_apc()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 2860, TimeoutSteps = 30000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        void CloseAll()
        {
            foreach (UiWindowState open in replica.UiWindows.Tgui().ToList())
                p.Session.UiClose(open.Window);
            w.Run(Second / 2);
            windows.Sync(replica.UiWindows, 1280, 720, 1); // the client sees them gone (it syncs every frame)
        }

        UiWindowState Open(obj_machinery machine, string expected)
        {
            CloseAll();
            In(w, () => Waiting.NoSleep(me.forceMove(Beside(machine))));
            w.Run(Second / 2);
            Assert.True(p.Session.Click(On(machine), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
            Assert.Equal(expected, window.Interface);
            return window;
        }

        // Meta's consoles: 6 station alert, 3 atmospheric alert (station only), 1 power flow control
        var stationConsoles = In(w, () => Dm.each_cast<obj_machinery_computer_station_alert>(SSmachines.get_machines_by_type(typeof(obj_machinery_computer_station_alert))).ToList());
        var atmosConsoles = In(w, () => Dm.each_cast<obj_machinery_computer_atmos_alert>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_atmos_alert))).ToList());
        var apcConsoles = In(w, () => Dm.each_cast<obj_machinery_computer_apc_control>(SSmachines.get_machines_by_type(typeof(obj_machinery_computer_apc_control))).ToList());
        output.WriteLine("station alert consoles: " + string.Join(", ", stationConsoles.Select(c => In(w, () => $"{Dm.str(c.name)} at {c.x},{c.y},{c.z}"))));
        // Meta's six station consoles, the shuttle's and CentCom's (D299) (a local alert console: its own level's alarms)
        Assert.Equal((8, 3, 1), (stationConsoles.Count, atmosConsoles.Count, apcConsoles.Count));
        Assert.Equal(2, stationConsoles.Count(c => In(w, () => Dm.str(c.name)) == "local alert console"));
        obj_machinery_computer_station_alert station = In(w, () => stationConsoles.First(Working));
        obj_machinery_computer_atmos_alert atmos = In(w, () => atmosConsoles.First(Working));
        output.WriteLine($"the station alert console at {In(w, () => $"{station.x},{station.y} ({Dm.str(get_area_name(station, TRUE))})")}, the atmospheric one at {In(w, () => $"{atmos.x},{atmos.y}")}");

        // a fire alarm pulled (a click): its area under Fire Alarms, the console's red screen
        obj_machinery_firealarm fireAlarm = In(w, () => Dm.each_cast<obj_machinery_firealarm>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_firealarm)))
            .First(f => f.buildstage == FIRE_ALARM_BUILD_SECURED && f.loc is turf_open && Working(f) && Dm.@in(f.my_area.type, GLOB.the_station_areas) && !Dm.truthy(f.my_area.fire)));
        string fireArea = In(w, () => Dm.str(get_area_name(fireAlarm, TRUE)));
        output.WriteLine($"the fire alarm of {fireArea}");
        In(w, () => Waiting.NoSleep(me.forceMove(fireAlarm.loc)));
        w.Run(Second / 2);
        Assert.True(p.Session.Click(On(fireAlarm), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(fireAlarm.my_area.fire)));
        UiWindowState window = Open(station, "StationAlertConsole");
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal(new[] { "Fire Alarms", "Atmosphere Alarms", "Power Alarms" }, content.Walk().Where(n => n.Kind == UiKind.Section).Select(s => s.Title));
        Assert.Contains("• " + fireArea, AlertsOf(content, "Fire"));
        Assert.Contains("alert:2", OverlaysOf(replica, station));
        // the reset (a right click on the alarm): gone from the console
        In(w, () => Waiting.NoSleep(me.forceMove(fireAlarm.loc)));
        w.Run(Second / 2);
        Assert.True(p.Session.Click(On(fireAlarm), ClickButton.Right, ClickModifiers.None));
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(fireAlarm.my_area.fire)));
        window = Open(station, "StationAlertConsole"); // walking to the alarm closed the console's window
        content = Shown(windows, replica, window.Window);
        Assert.DoesNotContain("• " + fireArea, AlertsOf(content, "Fire"));

        // a room depressurised: its air alarm's hazard
        obj_machinery_airalarm airAlarm = In(w, () => Dm.each_cast<obj_machinery_airalarm>(GLOB.air_alarms)
            .Where(a => a.buildstage == AIR_ALARM_BUILD_COMPLETE && Working(a) && a.loc is turf_open && a.danger_level == AIR_ALARM_ALERT_NONE
                && Dm.@in(a.my_area.type, GLOB.the_station_areas) && a.my_area != station.loc.loc && a.my_area != atmos.loc.loc)
            .Select(a => (Alarm: a, Turfs: Dm.each_cast<DmList>(a.my_area.get_zlevel_turf_lists()).Sum(l => l.len)))
            .Where(a => a.Turfs <= 60).OrderByDescending(a => a.Turfs).First().Alarm); // an ordinary room
        area room = In(w, () => airAlarm.my_area);
        string roomName = In(w, () => Dm.str(get_area_name(airAlarm, TRUE)));
        output.WriteLine($"depressurising {roomName}");
        In(w, () => SetAir(room, AIRLESS_ATMOS));
        w.Run(2 * Second);
        Assert.Equal(AIR_ALARM_ALERT_HAZARD, In(w, () => airAlarm.danger_level));
        content = Shown(windows, replica, window.Window);
        Assert.Contains("• " + roomName, AlertsOf(content, "Atmosphere"));

        // the atmospheric alert console: a priority alert, cleared by its button until the console's next check
        window = Open(atmos, "AtmosAlertConsole");
        w.Run(3 * Second); // its process
        content = Shown(windows, replica, window.Window);
        Assert.Contains(roomName, replica.UiWindows[window.Window].Merged.Arr("priority").Select(v => v.AsStr()));
        Assert.Contains("alert:2", OverlaysOf(replica, atmos));
        UiNode clear = ByKey(content, "priority:" + roomName).Walk().First(n => n.Kind == UiKind.Button);
        Assert.Equal(("times", "bad"), (clear.Icon, clear.Color));
        clear.OnClick();
        bool cleared = false;
        for (int i = 0; i < 5 && !cleared; i++) // its next check raises it again, so this follows the act's own tick
        {
            w.Run(1);
            cleared = In(w, () => (float)atmos.priority_alarms.len) == 0f;
        }
        Assert.True(cleared, "the button cleared the alert");
        w.Run(3 * Second);
        Assert.Contains(roomName, replica.UiWindows[window.Window].Merged.Arr("priority").Select(v => v.AsStr()));

        // Canary on an engineering console: the alert in its window, the console's alert-red screen
        var engineering = In(w, () => Dm.each_cast<obj_machinery_modular_computer_preset_engineering>(SSmachines.get_machines_by_type(typeof(obj_machinery_modular_computer_preset_engineering))).First(Working));
        window = Open(engineering, "NtosMain");
        content = Shown(windows, replica, window.Window);
        UiNode canary = ByKey(content, "program:alarmmonitor");
        Assert.Contains("Canary", canary.AllText());
        ByKey(canary, "run").OnClick();
        w.Run(Second);
        window = replica.UiWindows[window.Window];
        Assert.Equal("NtosStationAlertConsole", window.Interface);
        content = Shown(windows, replica, window.Window);
        Assert.Contains("• " + roomName, AlertsOf(content, "Atmosphere"));
        Assert.Contains("alert-red", OverlaysOf(replica, engineering));

        // the room's air back: the alarm cleared everywhere
        In(w, () => SetAir(room, OPENTURF_DEFAULT_ATMOS));
        w.Run(3 * Second);
        Assert.Equal(AIR_ALARM_ALERT_NONE, In(w, () => airAlarm.danger_level));
        content = Shown(windows, replica, window.Window);
        Assert.DoesNotContain("• " + roomName, AlertsOf(content, "Atmosphere"));
        Assert.Equal(0f, In(w, () => (float)atmos.priority_alarms.len));

        // the power flow control console: no access, then a debug ID
        obj_machinery_computer_apc_control apcConsole = apcConsoles[0];
        window = Open(apcConsole, "ApcControl");
        content = Shown(windows, replica, window.Window);
        Assert.Equal("Log In", ByKey(content, "login").Text);
        ByKey(content, "login").OnClick();
        w.Run(Second / 2);
        Assert.Equal(0f, In(w, () => apcConsole.authenticated));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<obj_item_card_id_advanced_debug>(me.loc))))));
        ByKey(Shown(windows, replica, window.Window), "login").OnClick();
        w.Run(Second); // the log-in act returns no update (apc_control.dm), so the window waits for SStgui's next fire (wait 9)
        Assert.Equal(1f, In(w, () => apcConsole.authenticated));
        content = Shown(windows, replica, window.Window);
        Assert.True(ByKey(content, "tab:apcs").Selected);

        // an APC of a station room in the table: its lighting channel off, its breaker off and on
        obj_machinery_power_apc apc = In(w, () => Dm.each_cast<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc)))
            .First(a => apcConsole.check_apc(a) && a.cell != null && Dm.truthy(a.operating) && a.lighting == APC_CHANNEL_AUTO_ON && Dm.@in(a.area.type, GLOB.the_station_areas)));
        string apcName = In(w, () => Dm.str(apc.area.name));
        UiNode row = content.Walk().First(n => n.Kind == UiKind.TableRow && n.Key != null && n.Key.StartsWith("apc:" + apcName));
        Assert.Equal(apcName, ByKey(row, "access").Text);
        ByKey(row, "lighting").OnClick(); // 3 (auto on) -> 0 (off)
        w.Run(Second / 2);
        Assert.Equal((float)APC_CHANNEL_OFF, In(w, () => apc.lighting));
        ByKey(row, "breaker").OnClick();
        w.Run(Second / 2);
        Assert.False(In(w, () => Dm.truthy(apc.operating)));
        content = Shown(windows, replica, window.Window);
        row = content.Walk().First(n => n.Kind == UiKind.TableRow && n.Key != null && n.Key.StartsWith("apc:" + apcName));
        Assert.Equal(("times", "bad"), (ByKey(row, "breaker").Icon, ByKey(row, "breaker").Color));
        Assert.Equal(("power-off", "bad"), (ByKey(row, "lighting").Icon, ByKey(row, "lighting").Color));
        ByKey(row, "breaker").OnClick();
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(apc.operating)));

        // the remote access: the APC's own window, unlocked for the engineer; the console's window closed disconnects it
        ByKey(row, "access").OnClick();
        w.Run(Second);
        Assert.Same(apc, In(w, () => apcConsole.active_apc));
        Assert.Same(me, In(w, () => apc.remote_control_user));
        UiWindowState apcWindow = Assert.Single(replica.UiWindows.Tgui(), u => u.Interface == "Apc");
        Assert.Equal(1, apcWindow.Merged.Num("remoteAccess"));
        Assert.Equal(0, apcWindow.Merged.Num("locked"));
        p.Session.UiClose(window.Window);
        w.Run(Second);
        Assert.Null(In(w, () => apcConsole.active_apc));
        Assert.Null(In(w, () => apc.remote_control_user));
        Assert.Equal(1f, In(w, () => apc.locked));
        Assert.Contains(In(w, () => Dm.each<object>(apcConsole.logs).Select(Dm.str).ToList()), l => l.EndsWith("remotely accessed APC in " + Dm.str(In(w, () => get_area_name(apc.area, TRUE)))));

        Assert.True(errors.Count == 0, string.Join("\n", errors.Take(3).Select(e => e.Message)));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
