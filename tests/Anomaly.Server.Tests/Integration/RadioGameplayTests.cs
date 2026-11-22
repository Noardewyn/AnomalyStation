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
/// M9 slice D through the real server on MetaStation with headless clients: MetaStation's telecomms room links at boot (tg's
/// autolinkers); Alice, Bob and Carol stand far apart, each with an assistant's headset (Common). Alice's ";" line reaches Bob and
/// Carol across the station through the receivers, buses, processors, servers, hub and broadcasters, as tg's "[Common]" line; with an
/// engineering key Alice's ":e" line reaches Carol (also given one) and not Bob; with the telecomms room's APCs switched off the
/// machines go off and her next headset line reaches nobody (tg's headsets are subspace-only). The announcement system on the
/// medical channel and a priority announcement reach the players too. Slice D2: the telecomms monitor lists the network's machines
/// and the server monitoring console shows Alice's line among the Common server's packets.
/// </summary>
public class RadioGameplayTests(ITestOutputHelper output)
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

    private static void Say(RealHeadlessWorld.Player p, string entry)
    {
        p.Session.Say(new SayOut(SayTypes.Open, SayChannels.Say));
        p.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, entry));
        p.Session.Say(new SayOut(SayTypes.Close));
    }

    // An open floor with normal air.
    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.truthy(t.opacity)
        && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || Dm.truthy(a.opacity))
        && Waiting.NoSleep(t.return_air()) is datum_gas_mixture air && Dm.num(air.return_pressure()) > 90;

    // The first clear floor at least `distance` tiles from `from` on the station level.
    private static turf Far(turf from, int distance)
    {
        for (int y = 1; y <= Dm.num(world.maxy); y++)
        {
            for (int x = 1; x <= Dm.num(world.maxx); x++)
            {
                if (locate(x, y, 1) is turf t && get_dist(from, t) >= distance && Clear(t))
                    return t;
            }
        }
        throw new InvalidOperationException("no far floor");
    }

    // Stands the player on a clear floor next to `console` and clicks it with an empty hand: its tgui window, shown by the client.
    private static UiNode Open(RealHeadlessWorld w, RealHeadlessWorld.Player p, TguiWindows windows, obj_machinery_computer console, string ui)
    {
        Do(w, () =>
        {
            turf spot = Dm.each<turf>(orange(1, get_turf(console))).First(Clear);
            Waiting.NoSleep(w.MobOf(p).forceMove(spot));
        });
        w.Run(Second);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, console.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = p.Session.Replica.UiWindows.Values.Single(v => v.Interface == ui);
        return Shown(w, windows, p, window.Window);
    }

    private static UiNode Shown(RealHeadlessWorld w, TguiWindows windows, RealHeadlessWorld.Player p, int window)
    {
        w.Run(Second);
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Get(window)?.Content;
    }

    private static int WindowOf(RealHeadlessWorld.Player p, string ui) => p.Session.Replica.UiWindows.Values.Single(v => v.Interface == ui).Window;

    private static void Wear(mob_living_carbon_human human, obj_item_radio_headset headset)
    {
        if (human.ears != null)
            qdel(human.ears);
        Assert.True(Dm.truthy(Waiting.NoSleep(human.equip_to_slot_if_possible(headset, ITEM_SLOT_EARS))));
    }

    [MetaServerFact]
    public void A_headset_line_crosses_the_station_until_telecomms_goes_dark()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var chat = new Dictionary<string, List<string>> { ["Alice"] = new(), ["Bob"] = new(), ["Carol"] = new() };
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob, carol })
        {
            string name = p.Name;
            p.Client.ServerTextReceived += t => chat[name].Add(t.Html);
        }
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };

        // The telecomms room linked itself at boot: the hub reaches the receivers, buses, servers and broadcasters.
        Do(w, () =>
        {
            var machines = Dm.each<obj_machinery_telecomms>(GLOB.telecomm_machines).ToList();
            output.WriteLine($"telecomms machines: {machines.Count}");
            obj_machinery_telecomms_hub hub = machines.OfType<obj_machinery_telecomms_hub>().Single();
            Assert.Contains(Dm.each<obj_machinery_telecomms>(hub.links), m => m is obj_machinery_telecomms_receiver);
            Assert.Contains(Dm.each<obj_machinery_telecomms>(hub.links), m => m is obj_machinery_telecomms_bus);
            Assert.Contains(Dm.each<obj_machinery_telecomms>(hub.links), m => m is obj_machinery_telecomms_broadcaster);
            Assert.All(machines, m => Assert.True(Dm.truthy(m.on), $"{m.name} is on"));
        });

        // Far apart: Alice where she joined, Bob and Carol on breathable floors 40 and 60 tiles off (beyond any voice).
        Do(w, () =>
        {
            turf start = get_turf(aliceMob);
            Waiting.NoSleep(bobMob.forceMove(Far(start, 40)));
            Waiting.NoSleep(carolMob.forceMove(Far(start, 60)));
            Assert.True(get_dist(aliceMob, bobMob) >= 40 && get_dist(aliceMob, carolMob) >= 60);
        });
        w.Run(Second);

        // 1. ";": Common through telecomms, heard by both as tg's radio line (the Common span and the [Common] channel name).
        Say(alice, ";Can anyone hear me");
        w.Run(3 * Second);
        string heard = chat["Bob"].LastOrDefault(t => t.Contains("Can anyone hear me", StringComparison.Ordinal));
        Assert.NotNull(heard);
        output.WriteLine("Bob heard: " + heard);
        Assert.Contains("[Common]", heard, StringComparison.Ordinal);
        Assert.Contains("radio", heard, StringComparison.Ordinal);
        Assert.Contains(chat["Carol"], t => t.Contains("Can anyone hear me", StringComparison.Ordinal));
        Assert.Contains(chat["Alice"], t => t.Contains("Can anyone hear me", StringComparison.Ordinal) && t.Contains("[Common]", StringComparison.Ordinal));

        // 1b. The telecomms consoles (slice D2): the monitor probes the "tcommsat" network for its machines and shows the hub's links;
        // the server monitoring console finds the servers in 25 tiles and shows Alice's line in the Common & AI server's packets.
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        obj_machinery_computer_telecomms_monitor monitor = In(w, () => Dm.each<obj_machinery_computer_telecomms_monitor>(SSmachines.get_machines_by_type(typeof(obj_machinery_computer_telecomms_monitor))).First());
        UiNode shown = Open(w, alice, windows, monitor, "TelecommsMonitor");
        int monitorWindow = WindowOf(alice, "TelecommsMonitor");
        shown.Walk().Single(n => n.Key == "networkId").OnInput("tcommsat");
        windows.Get(monitorWindow).Content.Walk().Single(n => n.Key == "probe").OnClick();
        shown = Shown(w, windows, alice, monitorWindow);
        int machineCount = In(w, () => Dm.each<obj_machinery_telecomms>(GLOB.telecomm_machines).Count(m => Dm.eq(m.network, "tcommsat")));
        output.WriteLine($"monitor: {shown.Walk().Single(n => n.Key == "status").Text}");
        Assert.Equal($"{machineCount} ENTITIES LOCATED & BUFFERED", shown.Walk().Single(n => n.Key == "status").Text);
        Assert.Equal(machineCount, shown.Walk().Count(n => n.Key == "select"));
        Assert.Contains(shown.Walk(), n => n.Key == "icon" && n.DmIconState == "hub");
        shown.Walk().First(n => n.Key == "select" && n.Tooltip == "Telecommunication Hub").OnClick();
        shown = Shown(w, windows, alice, monitorWindow);
        Assert.Contains(shown.Walk(), n => n.Label == "Network" && n.Text == "tcommsat");
        Assert.True(shown.Walk().Count(n => n.Key == "select") > 3, "the hub's linked entities");

        obj_machinery_computer_telecomms_server logs = In(w, () => Dm.each<obj_machinery_computer_telecomms_server>(SSmachines.get_machines_by_type(typeof(obj_machinery_computer_telecomms_server))).First());
        windows.SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters);
        shown = Open(w, alice, windows, logs, "ServerMonitor");
        int logsWindow = WindowOf(alice, "ServerMonitor");
        shown.Walk().Single(n => n.Key == "networkId").OnInput("tcommsat");
        windows.Get(logsWindow).Content.Walk().Single(n => n.Key == "scan").OnClick();
        shown = Shown(w, windows, alice, logsWindow);
        output.WriteLine("servers: " + string.Join(", ", shown.Walk().Where(n => n.Key == "view").Select(n => n.Text)));
        shown.Walk().Single(n => n.Key == "view" && n.Text == "Common & AI Server").OnClick();
        shown = Shown(w, windows, alice, logsWindow);
        UiNode contents = shown.Walk().Last(n => n.Label == "Contents");
        output.WriteLine("packet: " + contents.Text);
        Assert.Equal("\"Can anyone hear me\"", contents.Text);
        Assert.Contains(shown.Walk(), n => n.Label == "Class" && n.Text == "Humanoid");
        Assert.Contains(shown.Walk(), n => n.Label == "Data Type" && n.Text == "Speech File");

        // 2. An engineering key: ":e" reaches Carol's engineering headset, not Bob's plain one.
        Do(w, () =>
        {
            Wear(aliceMob, Dm.@new<obj_item_radio_headset_headset_eng>(get_turf(aliceMob)));
            Wear(carolMob, Dm.@new<obj_item_radio_headset_headset_eng>(get_turf(carolMob)));
        });
        w.Run(Second);
        Say(alice, ":e The engine is fine");
        w.Run(3 * Second);
        foreach (string name in chat.Keys)
            output.WriteLine($"{name}: " + string.Join(" | ", chat[name].Where(t => t.Contains("engine", StringComparison.Ordinal))));
        output.WriteLine(In(w, () => $"alice ears {aliceMob.ears?.GetType().Name} channels {string.Join(",", Dm.each((aliceMob.ears as obj_item_radio)?.channels))}"));
        Assert.Contains(chat["Carol"], t => t.Contains("The engine is fine", StringComparison.Ordinal) && t.Contains("[Engineering]", StringComparison.Ordinal));
        Assert.DoesNotContain(chat["Bob"], t => t.Contains("The engine is fine", StringComparison.Ordinal));

        // 3. The announcement system and a priority announcement.
        Do(w, () => priority_announce("Test of the announcement system.", "Test Alert"));
        w.Run(Second);
        Assert.Contains(chat["Bob"], t => t.Contains("Test of the announcement system.", StringComparison.Ordinal) && t.Contains("chat_alert_default", StringComparison.Ordinal));

        // 3b. The Automated Announcement System's arrival line goes out on Common through telecomms.
        Do(w, () => Waiting.NoSleep(aas_config_announce(typeof(datum_aas_config_entry_arrival), Dm.list(Dm.kv("PERSON", "Dana Doe"), Dm.kv("RANK", "Assistant")))));
        w.Run(3 * Second);
        output.WriteLine("AAS: " + chat["Bob"].Last());
        Assert.Contains(chat["Bob"], t => t.Contains("Dana Doe has signed up as Assistant", StringComparison.Ordinal) && t.Contains("coldly states", StringComparison.Ordinal));

        // 4. The telecomms room's APCs off: the machines go off with their power, and the next headset line reaches nobody.
        Do(w, () =>
        {
            var apcs = Dm.each<obj_machinery_telecomms>(GLOB.telecomm_machines).Select(m => (get_area(m) as area)?.apc).Where(a => a != null).Distinct().ToList();
            output.WriteLine($"telecomms APCs: {apcs.Count}");
            foreach (obj_machinery_power_apc apc in apcs)
                Waiting.NoSleep(apc.toggle_breaker(null));
        });
        w.Run(5 * Second);
        Do(w, () => Assert.All(Dm.each<obj_machinery_telecomms>(GLOB.telecomm_machines).Where(m => m.z == 1), m => Assert.False(Dm.truthy(m.on), $"{m.name} is off")));
        Say(alice, ";Hello? Hello?");
        w.Run(5 * Second);
        Assert.DoesNotContain(chat["Bob"], t => t.Contains("Hello? Hello?", StringComparison.Ordinal));
        Assert.DoesNotContain(chat["Carol"], t => t.Contains("Hello? Hello?", StringComparison.Ordinal));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
