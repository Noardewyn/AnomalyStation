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
/// A test power source (fixture, rules 15): early in a round every APC is still topping up its cell (90 % at map load) and, charging before
/// the machines process (SSmachines' APC steps, machines.dm), takes the grid's whole surplus; this source leaves a surplus of 2 MW on its
/// network each cycle so a recharging station's grid-only draw (charge_cell, power.dm:253) gets some.
/// </summary>
[DmPath("/obj/machinery/power/test_si2_grid")]
public class obj_machinery_power_test_si2_grid : obj_machinery_power
{
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (powernet is datum_powernet net)
            add_avail(Dm.num(net.load) + 2000000);
        return null;
    }
}

/// <summary>
/// Silicons SI2 (D326) through the real server on MetaStation with headless clients: Alice is made a cyborg and takes the engineering
/// model (its modules and the cyborg HUD's module slots reach her client); Bob, with the captain's spare ID, opens Meta's robotics
/// console, sees her in its window and locks her down (the window's confirm button twice), then releases her; on a recharging station
/// her drained cell charges from the grid.
/// </summary>
public class CyborgGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode Shown(RealHeadlessWorld w, TguiWindows windows, RealHeadlessWorld.Player p, string ui)
    {
        w.Run(Second);
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        UiWindowState state = p.Session.Replica.UiWindows.Values.Single(v => v.Interface == ui);
        return windows.Get(state.Window).Content;
    }

    private static turf FreeTileBy(atom a) => Dm.each<turf>(Dm.list(get_step(a, SOUTH), get_step(a, NORTH), get_step(a, EAST), get_step(a, WEST)))
        .First(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each<atom>(t.contents).Any(c => Dm.truthy(c.density)));

    [MetaServerFact]
    public void Bob_locks_Alice_the_cyborg_down_from_the_robotics_console_and_a_station_charges_her()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 62, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var aliceChat = new List<ServerText>();
        alice.Client.ServerTextReceived += t => aliceChat.Add(t);
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };

        // Alice becomes a cyborg and takes the engineering model.
        var aliceHuman = (mob_living_carbon_human)w.MobOf(alice);
        var borg = In(w, () => (mob_living_silicon_robot)Waiting.NoSleep(aliceHuman.Robotize()));
        w.Run(2 * Second);
        Assert.Same(borg, w.MobOf(alice));
        Do(w, () => Waiting.NoSleep(borg.model.transform_to(typeof(obj_item_robot_model_engineering), forced: TRUE)));
        w.Run(2 * Second);
        Assert.IsType<obj_item_robot_model_engineering>(In(w, () => borg.model));
        Assert.Contains(In(w, () => Dm.each<obj_item>(borg.model.modules).ToList()), m => m is obj_item_construction_rcd_borg);
        // her HUD's three module slots are on her client's screen
        Assert.Equal(3, In(w, () => Dm.each<atom_movable_screen_robot_module_slot>(borg.client.screen).Count()));
        // her module button opens her module storage; a click on its RCD equips it to slot 1, which shows it (a player's report: modules
        // could not be taken out)
        w.Run(2 * Second); // the model's transform animation (do_transform_delay, about 3 s) ends first
        long moduleButton = In(w, () => Dm.each<atom_movable_screen_robot_module>(borg.client.screen).Single().dm_id);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, moduleButton, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        var rcd = In(w, () => Dm.each<obj_item_construction_rcd_borg>(borg.model.modules).Single());
        Assert.True(In(w, () => Dm.@in(rcd, borg.client.screen)), "the module storage shows the RCD");
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, In(w, () => rcd.dm_id), 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(rcd, In(w, () => borg.held_items[1]));
        Assert.True(In(w, () => Dm.each<atom_movable_screen_robot_module_slot>(borg.client.screen).Any(slot => Dm.@in(rcd, slot.vis_contents))), "a module slot shows the RCD");

        // Bob, with the captain's spare ID, by Meta's robotics console.
        var console = In(w, () => Dm.each<obj_machinery_computer_robotics>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_robotics))).Single());
        output.WriteLine("console at " + In(w, () => $"{console.x},{console.y},{console.z}"));
        Do(w, () =>
        {
            Waiting.NoSleep(bobMob.forceMove(FreeTileBy(console)));
            var card = Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(bobMob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bobMob.put_in_hands(card)))); // held: his own ID stays on
            Waiting.Detach(console.ui_interact(bobMob));
        });
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => bob.Session.UiAct(window, action, parameters),
            SendClose = window => bob.Session.UiClose(window),
        };
        UiNode ui = Shown(w, windows, bob, "RoboticsControlConsole");
        string borgRef = In(w, () => Dm.str(REF(borg)));
        Assert.Contains(ui.Walk(), n => n.Key == borgRef && n.Title == In(w, () => borg.name));

        // Lockdown: the confirm button takes two clicks.
        ui.Walk().Single(n => n.Key == "stop:" + borgRef).OnClick();
        ui = Shown(w, windows, bob, "RoboticsControlConsole");
        UiNode confirm = ui.Walk().Single(n => n.Key == "stop:" + borgRef);
        Assert.Equal("Confirm?", confirm.Text);
        confirm.OnClick();
        w.Run(2 * Second);
        Assert.True(In(w, () => Dm.truthy(borg.lockcharge)), "the console locks Alice down; runtimes: " + string.Join("\n----\n", runtimes));
        Assert.Contains(aliceChat, t => t.Html != null && t.Html.Contains("You have been locked down!", StringComparison.Ordinal));
        ui = Shown(w, windows, bob, "RoboticsControlConsole");
        Assert.Equal("Release", ui.Walk().Single(n => n.Key == "stop:" + borgRef).Text);

        // Release.
        ui.Walk().Single(n => n.Key == "stop:" + borgRef).OnClick();
        ui = Shown(w, windows, bob, "RoboticsControlConsole");
        ui.Walk().Single(n => n.Key == "stop:" + borgRef).OnClick();
        w.Run(2 * Second);
        Assert.False(In(w, () => Dm.truthy(borg.lockcharge)), "the console releases Alice");
        Assert.Contains(aliceChat, t => t.Html != null && t.Html.Contains("Your lockdown has been lifted!", StringComparison.Ordinal));

        // A recharging station (Meta has ten) charges her drained cell from the grid (tg's grid_only draw): a test source on its APC's network.
        var station = In(w, () => Dm.each<obj_machinery_recharge_station>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_recharge_station)))
            .First(s => Dm.truthy(s.is_operational) && (get_area(s) as area)?.apc?.terminal != null));
        Do(w, () =>
        {
            var terminal = (get_area(station) as area).apc.terminal;
            var source = Dm.@new<obj_machinery_power_test_si2_grid>(terminal.loc);
            Assert.True(Dm.truthy(source.connect_to_network()));
            Assert.Same(terminal.powernet, source.powernet);
        });
        w.Run(2 * Second);
        var cell = In(w, () => (obj_item_stock_parts_power_store)borg.cell);
        float drained = In(w, () =>
        {
            cell.charge = Dm.num(cell.maxcharge) / 10;
            Waiting.NoSleep(borg.forceMove(station.loc));
            Waiting.NoSleep(station.close_machine(borg));
            return Dm.num(cell.charge);
        });
        Assert.Same(borg, In(w, () => station.occupant));
        w.Run(10 * Second);
        Assert.True(In(w, () => Dm.num(cell.charge)) > drained, "the station charges Alice's cell");
        Assert.Equal("borgcharger1", In(w, () => station.icon_state));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
