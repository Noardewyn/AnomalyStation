using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Science's anomaly lab through the real server with a headless client: a joined scientist stands on a quantum pad linked to a
/// far one and works it, and arrives on the far pad; a scientist puts a raw core and a transfer valve in the anomaly refinery, reads
/// them in its window (drawn by her client from the server's data), presses Implode Core, and the finished core comes out.
/// </summary>
public class AnomalyGameplayTests(ITestOutputHelper output)
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

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static void Fill(datum_gas_mixture mix, Type gas, float moles, float temperature)
    {
        mix.assert_gases(gas);
        mix.moles[gas] = moles;
        mix.temperature = temperature;
    }

    private static void EmptyHands(RealHeadlessWorld w, mob_living_carbon_human me) => Do(w, () =>
    {
        foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
            Waiting.NoSleep(me.dropItemToGround(held));
    });

    [Fact]
    public void A_scientist_crosses_the_station_by_quantum_pad()
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 32, RoomHeight = 16, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 15061, TimeoutSteps = 8000 }, config);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        w.Run(Second);

        turf here = In(w, () => me.loc as turf);
        turf far = In(w, () => locate(Math.Min(here.x + 12, 30), Math.Min(here.y + 4, 14), here.z));
        var (pad, other) = In(w, () =>
        {
            (get_area(here) as area).requires_power = FALSE;
            (get_area(far) as area).requires_power = FALSE;
            var near = Dm.@new<obj_machinery_quantumpad>(here);
            var away = Dm.@new<obj_machinery_quantumpad>(far);
            near.linked_pad = away;
            near.last_teleport = -10 * 60 * Second; // the test round has just begun
            near.power_change();
            away.power_change();
            return (near, away);
        });
        EmptyHands(w, me);
        w.Run(Second);

        // she stands on the pad and works it
        Assert.Same(In(w, () => pad.loc), In(w, () => me.loc));
        Assert.True(alice.Session.Click(On(pad), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(pad.teleporting)), "the pad did not charge");
        w.Run(5 * Second);
        Assert.Same(In(w, () => other.loc), In(w, () => me.loc));
        output.WriteLine($"Alice went from {here.x},{here.y} to {far.x},{far.y}");
    }

    [Fact]
    public void A_scientist_implodes_a_raw_core_in_the_refinery()
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 16, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 15062, TimeoutSteps = 8000 }, config);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        ReplicaWorld replica = alice.Session.Replica;
        var heard = new List<string>();
        alice.Client.ServerTextReceived += t => heard.Add(t.Html ?? "");
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        w.Run(Second);

        turf here = In(w, () => me.loc as turf);
        var (refinery, raw, valve) = In(w, () =>
        {
            (get_area(here) as area).requires_power = FALSE;
            var made = Dm.@new<obj_machinery_research_anomaly_refinery>(get_step(me, EAST));
            made.power_change();
            var core = Dm.@new<obj_item_raw_anomaly_core_bluespace>(here);
            var ttv = Dm.@new<obj_item_transfer_valve>(here);
            var one = Dm.@new<obj_item_tank_internals_plasma>(ttv);
            var two = Dm.@new<obj_item_tank_internals_oxygen>(ttv);
            Fill(Waiting.NoSleep(one.return_air()) as datum_gas_mixture, typeof(datum_gas_plasma), 15, 1500); // below the leak pressure
            Fill(Waiting.NoSleep(two.return_air()) as datum_gas_mixture, typeof(datum_gas_oxygen), 60, T20C);
            ttv.tank_one = one;
            ttv.tank_two = two;
            ttv.update_appearance();
            return (made, core, ttv);
        });
        w.Run(Second);

        // the core and the bomb go in from her hand
        foreach (obj_item item in new obj_item[] { raw, valve })
        {
            EmptyHands(w, me);
            Do(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(item)))));
            w.Run(Second / 2);
            Assert.True(alice.Session.Click(On(refinery), ClickButton.Left, ClickModifiers.None));
            w.Run(Second / 2);
        }
        Assert.Same(raw, In(w, () => refinery.inserted_core));
        Assert.Same(valve, In(w, () => refinery.inserted_bomb));

        // its window shows them; she implodes the core
        EmptyHands(w, me);
        w.Run(Second / 2);
        Assert.True(alice.Session.Click(On(refinery), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        UiWindowState window = replica.UiWindows.Tgui().Single(u => u.Interface == "AnomalyRefinery");
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Contains(content.Walk(), n => n.Text == "raw bluespace core");
        Assert.Contains(content.Walk(), n => n.Text == "4 tiles");
        content.Walk().Single(n => n.Key == "start_implosion").OnClick();
        w.Run(Second / 2);
        Assert.Contains(heard, h => h.Contains("Beginning compression test.", StringComparison.Ordinal));

        // the blast is caught inside; the test ends after its timeout, and the refinery hands the finished core out
        for (int i = 0; i < 30 && !heard.Any(h => h.Contains("Core production complete.", StringComparison.Ordinal)); i++)
            w.Run(Second);
        Assert.Contains(heard, h => h.Contains("Core production complete.", StringComparison.Ordinal));
        var core = In(w, () => Dm.each<obj_item_assembly_signaler_anomaly_bluespace>().FirstOrDefault());
        Assert.NotNull(core);
        Assert.Same(In(w, () => refinery.loc), In(w, () => core.loc));
        output.WriteLine("the refinery: " + string.Join(" | ", heard.Where(h => h.Contains("anomaly refinery", StringComparison.Ordinal))));
    }
}
