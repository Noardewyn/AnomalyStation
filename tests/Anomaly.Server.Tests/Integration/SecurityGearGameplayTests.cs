using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package J4 (D298) through the real server on MetaStation: a crewman scans a thing with the forensic scanner and the client's ForensicScanner window shows the result; Meta's keycard device opens
/// its KeycardAuth window, and a swipe from a second device raises the alert.
/// </summary>
public class SecurityGearGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public SecurityGearGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    [MetaServerFact]
    public void A_crewman_scans_with_the_forensic_scanner_and_works_metas_keycard_device_and_the_client_sees_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 41, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Detective");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // the detective's scanner, in hand; a thing with prints beside him
        w.Host.EnqueueConsoleLine("give Detective /obj/item/detective_scanner");
        w.Run(5);
        var scanner = In(w, () => me.get_active_held_item()) as obj_item_detective_scanner;
        Assert.NotNull(scanner);
        obj_item_shard shard = In(w, () =>
        {
            var s = Dm.@new<obj_item_shard>(me.loc);
            s.add_fingerprint_list(Dm.list(Dm.kv("printofthecrime", "printofthecrime")));
            return s;
        });
        w.Run(Second);
        Assert.True(p.Session.Click(On(shard), ClickButton.Left, default));
        w.Run(4 * Second);
        Assert.Equal(1, In(w, () => (int)Dm.length(scanner.log_data)));

        // the scanner's window on the client: the result
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("ForensicScanner", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        _output.WriteLine(content.AllText());
        Assert.Contains("printofthecrime", content.AllText());
        p.Session.UiClose(window.Window);
        w.Run(Second);

        // Meta's keycard devices
        List<obj_machinery_keycard_auth> devices = In(w, () => Dm.each<obj_machinery_keycard_auth>().ToList());
        _output.WriteLine($"{devices.Count} keycard devices");
        Assert.True(devices.Count >= 2);
        obj_machinery_keycard_auth device = devices[0];
        Assert.False(In(w, () => InertSeam.IsInert(device)));
        Do(w, () => Waiting.NoSleep(me.forceMove(device.loc as atom)));
        w.Run(Second);
        Do(w, () => Waiting.NoSleep(me.dropItemToGround(scanner)));
        w.Run(Second);
        Assert.True(p.Session.Click(On(device), ClickButton.Left, default));
        w.Run(Second);
        window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("KeycardAuth", window.Interface);
        content = Shown(windows, replica, window.Window);
        Assert.Contains(content.Walk(), n => n.Key == "red_alert");
        // no keycard access on a bare crewman: tg's device ignores the button
        content.Walk().First(n => n.Key == "red_alert").OnClick();
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(device.waiting)));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
