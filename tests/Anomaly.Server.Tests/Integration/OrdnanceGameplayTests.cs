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
/// The ordnance lab through the real server with a headless client: a joined scientist stands by a doppler array facing a range,
/// where a transfer valve with a hot plasma tank and an oxygen tank waits with a signaler on its valve; she sends the signal from
/// her own signaler's window, the valve opens, the merged tank bursts, the array states the blast, and she reads the record in the
/// array's window, which her client draws from the server's data.
/// </summary>
public class OrdnanceGameplayTests(ITestOutputHelper output)
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

    [Fact]
    public void A_scientist_blows_a_bomb_in_the_range_and_reads_it_on_the_doppler_array()
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 64, RoomHeight = 16, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14450, TimeoutSteps = 8000 }, config);
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

        // the array beside her, facing the range to the east; the bomb far down the range
        turf here = In(w, () => me.loc as turf);
        output.WriteLine($"Alice at {In(w, () => here.x)}, {In(w, () => here.y)}");
        turf beside = In(w, () => get_step(me, WEST) as turf);
        turf range = In(w, () => locate(Math.Min(here.x + 40, 60), here.y, here.z));
        var doppler = In(w, () =>
        {
            (get_area(here) as area).requires_power = FALSE;
            (get_area(range) as area).requires_power = FALSE;
            var made = Dm.@new<obj_machinery_doppler_array>(beside);
            Waiting.NoSleep(made.setDir(EAST));
            made.power_change();
            return made;
        });
        var (valve, fuse, remote) = In(w, () =>
        {
            var ttv = Dm.@new<obj_item_transfer_valve>(range);
            var one = Dm.@new<obj_item_tank_internals_plasma>(ttv);
            var two = Dm.@new<obj_item_tank_internals_oxygen>(ttv);
            Fill(Waiting.NoSleep(one.return_air()) as datum_gas_mixture, typeof(datum_gas_plasma), 120, 1500);
            Fill(Waiting.NoSleep(two.return_air()) as datum_gas_mixture, typeof(datum_gas_oxygen), 240, T20C);
            ttv.tank_one = one;
            ttv.tank_two = two;
            ttv.update_appearance();
            var onValve = Dm.@new<obj_item_assembly_signaler>(range);
            Waiting.NoSleep(onValve.toggle_secure());
            Waiting.NoSleep(ttv.item_interaction(me, onValve));
            var hers = Dm.@new<obj_item_assembly_signaler>(here);
            hers.set_frequency(onValve.frequency);
            hers.code = onValve.code;
            foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
                Waiting.NoSleep(me.dropItemToGround(held));
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(hers))));
            return (ttv, onValve, hers);
        });
        w.Run(Second);
        Assert.Same(valve, In(w, () => fuse.holder));

        // her signaler's window: Send Signal
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second / 2);
        UiWindowState window = replica.UiWindows.Tgui().Single(u => u.Interface == "Signaler");
        UiNode content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "signal" && n.Kind == UiKind.Button).OnClick();
        w.Run(Second / 2);
        Assert.Contains(heard, h => h.Contains("signaled", StringComparison.Ordinal));
        // the valve opened: the merged tank bursts at once and takes the valve with it
        Assert.True(In(w, () => QDELETED(valve) || Dm.truthy(valve.valve_open)), "the signal did not open the valve");
        Assert.True(alice.Session.UiClose(window.Window));

        bool recorded = false;
        for (int i = 0; i < 20 && !recorded; i++)
        {
            w.Run(Second);
            recorded = In(w, () => Dm.length(doppler.records) > 0);
        }
        Assert.True(recorded, "the doppler array recorded no blast");
        Assert.Contains(heard, h => h.Contains("Explosive disturbance detected.", StringComparison.Ordinal));

        // the array's window with the record
        Do(w, () =>
        {
            foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
                Waiting.NoSleep(me.dropItemToGround(held));
        });
        w.Run(Second);
        Assert.True(alice.Session.Click(On(doppler), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        window = replica.UiWindows.Tgui().Single(u => u.Interface == "DopplerArray");
        content = Shown(windows, replica, window.Window);
        Assert.True(content.Walk().Single(n => n.Key == "tab-Log Recording #1").Selected);
        string coordinates = In(w, () => (doppler.records[1] as datum_data_tachyon_record).coordinates);
        Assert.Equal(coordinates, content.Walk().Single(n => n.Key == "coordinates").Text);
        Assert.False(string.IsNullOrEmpty(content.Walk().Single(n => n.Key == "shockwave").Text));
        output.WriteLine("record: " + content.AllText());
    }
}
