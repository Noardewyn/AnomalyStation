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
/// Package E8 (airlock_controller.dm, airlock_control.dm's sensor, igniter.dm, access_controller.dm, doorcontrol.dm's igniter
/// controller) on MetaStation through the real server and a client's own stack. An engineer opens the atmos incinerator's access
/// console from the incinerator room, cycles the chamber's inner door open through tg's AirlockController window, steps into the
/// chamber and from inside cycles out (the inner door bolted shut, the chamber siphoned, the outer door opened onto the burn chamber)
/// and back in; the client sees the console's picture change while it cycles; the chamber's sensor button cycles it out again, Abort
/// stops it once the chamber is siphoned, and the sensor's picture alerts. With plasma and oxygen in the burn chamber the
/// combustion chamber's ignition switch turns the igniter on (the client sees "igniter1"), the gas catches fire and heats up, and the
/// switch turns it off again. At virology's airlock an assistant without the access is refused by the access button; with the
/// Captain's ID it opens the inner door, and the outer button cycles (the inner door shut and bolted, the outer one opened).
/// </summary>
public class IncineratorControlsGameplayTests(ITestOutputHelper output)
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

    private static List<string> States(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).ToList();
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static void Put(RealHeadlessWorld w, mob_living me, turf t)
    {
        In(w, () => Waiting.NoSleep(me.forceMove(t)));
        w.Run(Second / 2);
    }

    /// <summary>Runs the world until the console reaches the state (SSmachines runs it every 2 s).</summary>
    private void Until(RealHeadlessWorld w, obj_machinery_airlock_controller console, string state, int seconds = 40)
    {
        for (int i = 0; i < seconds && In(w, () => console.state) != state; i++)
            w.Run(Second);
        output.WriteLine($"console {In(w, () => console.state)} -> {In(w, () => console.target_state)}, chamber {In(w, () => Dm.num(console.last_pressure))} kPa");
        Assert.Equal(state, In(w, () => console.state));
    }

    [MetaServerFact]
    public void The_incinerator_airlock_cycles_from_its_console_the_ignition_switch_lights_the_burn_chamber_and_virologys_access_buttons_check_access()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 88, TimeoutSteps = 30000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        In(w, () => ADD_TRAIT(me, TRAIT_GODMODE, "test")); // the chamber is siphoned around him

        // ---- the atmos incinerator's console, linked at its late init to its doors, dual-port vent and sensor
        var console = In(w, () => Dm.each<obj_machinery_airlock_controller_incinerator_atmos>(Dm.world).Single());
        var (inner, outer, pump, sensor) = In(w, () => (
            (obj_machinery_door_airlock)console.interior_door_ref.resolve(), (obj_machinery_door_airlock)console.exterior_door_ref.resolve(),
            (obj_machinery_atmospherics_components_binary_dp_vent_pump)console.pump_ref.resolve(), (obj_machinery_airlock_sensor)console.sensor_ref.resolve()));
        turf chamber = In(w, () => get_step(inner, get_dir(inner, outer)));
        turf outside = In(w, () => get_step(inner, get_dir(outer, inner)));
        output.WriteLine($"console at {In(w, () => $"{console.x},{console.y}")}, inner {In(w, () => $"{inner.x},{inner.y}")}, chamber {In(w, () => $"{chamber.x},{chamber.y}")}, outer {In(w, () => $"{outer.x},{outer.y}")}, outside {In(w, () => $"{outside.x},{outside.y}")}, sensor {In(w, () => $"{sensor.x},{sensor.y}")}, pump {In(w, () => $"{pump.x},{pump.y}")}");
        In(w, () =>
        {
            Assert.Equal(get_area(chamber), get_area(sensor)); // the sensor reads the chamber's air
            Assert.True(Dm.truthy(inner.density) && Dm.truthy(inner.locked)); // Meta's mapping helper bolts both
            Assert.True(Dm.truthy(outer.density) && Dm.truthy(outer.locked));
            Assert.Equal("closed", console.state);
        });

        // from the incinerator room: the window, then "Open Interior Airlock"
        Put(w, me, outside);
        Assert.True(p.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("AirlockController", window.Interface);
        Assert.Equal("closed", window.Merged.Str("airlockState"));
        Assert.Equal(("closed", "closed"), (window.Merged.Str("interiorStatus"), window.Merged.Str("exteriorStatus")));
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal((500.0, 190.0), (windows.Get(window.Window).Context.WindowWidth, windows.Get(window.Window).Context.WindowHeight));
        Assert.Equal("Inactive", ByKey(content, "status").Text);
        ByKey(content, "cycleInterior").OnClick();
        w.Run(Second);
        Assert.Equal("inopen", In(w, () => console.target_state));
        Until(w, console, "inopen");
        w.Run(2 * Second);
        Assert.False(In(w, () => Dm.truthy(inner.density)));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(("Interior Airlock Open", "Open"), (ByKey(content, "status").Text, ByKey(content, "interior").Text));

        // into the chamber, and from inside: "Cycle to Exterior Airlock"
        Put(w, me, chamber);
        content = Shown(windows, replica, window.Window);
        Assert.NotNull(content); // the window stays open beside the console
        ByKey(content, "cycleExterior").OnClick();
        w.Run(Second);
        Assert.Equal("outopen", In(w, () => console.target_state));
        bool sawCycling = false;
        for (int i = 0; i < 40 && In(w, () => console.state) != "outopen"; i++)
        {
            w.Run(Second);
            sawCycling |= States(replica, console).Contains("airlock_control_process");
        }
        Until(w, console, "outopen");
        Assert.True(sawCycling, "the client saw the console's cycling picture");
        w.Run(2 * Second);
        In(w, () =>
        {
            Assert.True(Dm.truthy(inner.density) && Dm.truthy(inner.locked));
            Assert.False(Dm.truthy(outer.density));
            Assert.Equal((float)ATMOS_DIRECTION_SIPHONING, pump.pump_direction);
        });
        content = Shown(windows, replica, window.Window);
        Assert.Equal(("Exterior Airlock Open", "Open", "Siphon"), (ByKey(content, "status").Text, ByKey(content, "exterior").Text, ByKey(content, "pump").Text));
        Assert.Contains("airlock_control_standby", States(replica, console));

        // and back in: "Cycle to Interior Airlock" (the outer door shut, siphoned, the pump turned round to fill the chamber)
        ByKey(content, "cycleInterior").OnClick();
        Until(w, console, "inopen", 90);
        w.Run(2 * Second);
        In(w, () =>
        {
            Assert.True(Dm.truthy(outer.density) && Dm.truthy(outer.locked));
            Assert.False(Dm.truthy(inner.density));
            Assert.True(Dm.num(((turf_open)chamber).air.return_pressure()) >= ONE_ATMOSPHERE * 0.9f);
        });
        Assert.Contains("airlock_sensor_standby", States(replica, sensor)); // an atmosphere in the chamber: no alert

        // the chamber's sensor button from inside: from inopen it cycles out (airlock_control.dm:78, cycle()); Abort in the window,
        // and the incinerator's sanitizing console siphons the chamber to the end before it rests closed
        Assert.True(p.Session.Click(On(sensor), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal("outopen", In(w, () => console.target_state));
        Until(w, console, "depressurize");
        ByKey(Shown(windows, replica, window.Window), "abort").OnClick();
        w.Run(Second);
        Assert.Equal("closed", In(w, () => console.target_state));
        Until(w, console, "closed", 60);
        In(w, () =>
        {
            Assert.True(Dm.truthy(inner.density) && Dm.truthy(outer.density));
            Assert.True(Dm.num(((turf_open)chamber).air.return_pressure()) <= ONE_ATMOSPHERE * 0.01f);
        });
        w.Run(3 * Second);
        Assert.Contains("airlock_sensor_alert", States(replica, sensor)); // under 80% of an atmosphere

        // out through the inner door again, and shut it behind
        ByKey(Shown(windows, replica, window.Window), "cycleInterior").OnClick();
        Until(w, console, "inopen", 90);
        w.Run(2 * Second);
        Put(w, me, outside);
        ByKey(Shown(windows, replica, window.Window), "cycleClosed").OnClick();
        Until(w, console, "closed");
        p.Session.UiClose(window.Window);
        w.Run(2);

        // ---- the burn chamber: plasma and oxygen around the igniter, then the combustion chamber's ignition switch
        var igniter = In(w, () => Dm.each<obj_machinery_igniter_incinerator_atmos>(Dm.world).Single());
        var button = In(w, () => Dm.each<obj_machinery_button_ignition_incinerator_atmos>(Dm.world).Single());
        turf burn = In(w, () => get_turf(igniter));
        output.WriteLine($"igniter at {In(w, () => $"{igniter.x},{igniter.y}")}, switch at {In(w, () => $"{button.x},{button.y}")}");
        In(w, () =>
        {
            foreach (turf_open t in Dm.each(((turf_open)burn).atmos_adjacent_turfs ?? Dm.list()).OfType<turf_open>().Append((turf_open)burn).ToList())
            {
                t.air = SSair.parse_gas_string("o2=200;plasma=100;TEMP=293.15") as datum_gas_mixture;
                SSair.add_to_active(t);
            }
        });
        Assert.Contains("igniter0", States(replica, igniter));
        float cold = In(w, () => ((turf_open)burn).air.temperature);
        In(w, () => Waiting.NoSleep(me.forceMove(get_step(button, 0))));
        w.Run(Second / 2);
        if (!In(w, () => Dm.truthy(Waiting.NoSleep(me.Adjacent(button)))))
            In(w, () => Waiting.NoSleep(me.forceMove(Dm.each<turf>(oview(1, button)).First(t => t is turf_open && !Dm.truthy(t.density) && Dm.truthy(Waiting.NoSleep(me.Adjacent(button)))))));
        Assert.True(p.Session.Click(On(button), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(1f, In(w, () => igniter.on));
        w.Run(3 * Second);
        Assert.Contains("igniter1", States(replica, igniter));
        Assert.NotNull(In(w, () => ((turf_open)burn).active_hotspot));
        Assert.True(In(w, () => ((turf_open)burn).air.temperature) > cold + 200, "the plasma burns and heats the burn chamber");
        // after the switch's 3-second cooldown it turns the igniter off
        w.Run(Second);
        Assert.True(p.Session.Click(On(button), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(0f, In(w, () => igniter.on));
        Assert.Contains("igniter0", States(replica, igniter));

        // ---- virology's airlock: refused without the access, then opened and cycled with the Captain's ID
        var buttons = In(w, () => Dm.each<obj_machinery_door_buttons_access_button>(Dm.world).Where(b => Dm.str(b.idSelf) == "virology_airlock_control").ToList());
        Assert.Equal(2, buttons.Count);
        var innerButton = buttons.Single(b => In(w, () => Dm.str(b.idDoor)) == "virology_airlock_interior");
        var outerButton = buttons.Single(b => In(w, () => Dm.str(b.idDoor)) == "virology_airlock_exterior");
        var vInner = In(w, () => innerButton.door);
        var vOuter = In(w, () => outerButton.door);
        In(w, () =>
        {
            Assert.NotNull(innerButton.controller);
            Assert.Same(vInner, innerButton.controller.interior_airlock);
            Assert.Same(vOuter, innerButton.controller.exterior_airlock);
            Assert.True(Dm.truthy(vInner.density) && Dm.truthy(vInner.locked));
            if (me.wear_id is obj_item old)
                qdel(old);
        });
        turf Beside(atom a) => In(w, () => Dm.each<turf>(oview(1, a)).First(t => t is turf_open && !Dm.truthy(t.density)
            && !Dm.each_cast<atom>(t.contents).Any(c => Dm.truthy(c.density)) && get_dist(t, a) == 1 && (t.x == a.x || t.y == a.y)));
        Put(w, me, Beside(innerButton));
        texts.Clear();
        Assert.True(p.Session.Click(On(innerButton), ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second);
        Assert.Contains(texts, t => t.Contains("Access denied.", StringComparison.Ordinal));
        Assert.True(In(w, () => Dm.truthy(vInner.density)));

        In(w, () =>
        {
            var card = Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(card, ITEM_SLOT_ID))), "the Captain's ID goes on");
        });
        Assert.True(p.Session.Click(On(innerButton), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Contains("access_button_cycle", States(replica, innerButton));
        w.Run(3 * Second);
        In(w, () =>
        {
            Assert.False(Dm.truthy(vInner.density));
            Assert.True(Dm.truthy(vInner.locked)); // opened and bolted open
            Assert.True(Dm.truthy(vOuter.density));
        });
        Assert.Contains("access_button_standby", States(replica, innerButton));

        Put(w, me, Beside(outerButton));
        Assert.True(p.Session.Click(On(outerButton), ClickButton.Left, ClickModifiers.None));
        w.Run(5 * Second);
        In(w, () =>
        {
            Assert.True(Dm.truthy(vInner.density) && Dm.truthy(vInner.locked)); // cycle_close shut and bolted the inner door
            Assert.False(Dm.truthy(vOuter.density)); // and cycle_open opened the outer one 2 seconds later
            Assert.False(Dm.truthy(innerButton.controller.busy));
        });

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
        // nor did the boot reach a runtime in the package's machines (Meta's two consoles, sensors, igniters, sparkers, access buttons)
        string errors = w.Err.ToString();
        foreach (string mine in new[] { "airlock_controller", "airlock_sensor", "obj_machinery_igniter", "obj_machinery_sparker", "door_buttons", "assembly_control_igniter" })
            Assert.DoesNotContain(mine, errors);
    }
}
