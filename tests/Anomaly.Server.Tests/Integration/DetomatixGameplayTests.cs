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
/// Syndicate stage G3d (D316) through the real server on MetaStation with headless clients: Alice slots a Detomatix disk into her PDA,
/// turns on its messenger's "Attach Virus", picks Bob and sends a line; tg's three prompts forge the sender (a name, a job, no photo),
/// and the rigged message goes through the telecomms hub to Bob's PDA as from the forged sender, its (Reply) link rigged. Bob's link
/// blows his PDA up in his belt.
/// </summary>
public class DetomatixGameplayTests(ITestOutputHelper output)
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

    private static void Answer(RealHeadlessWorld w, RealHeadlessWorld.Player p, string ui, string action, string key, string value)
    {
        w.Run(2 * Second);
        UiWindowState input = p.Session.Replica.UiWindows.Values.Single(v => v.Interface == ui);
        Assert.True(p.Session.UiAct(input.Window, action, UiValue.ObjectOf((key, UiValue.Of(value)))));
    }

    [MetaServerFact]
    public void Alice_rigs_a_message_to_Bob_and_his_reply_link_blows_up_his_pda()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 95, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var chat = new Dictionary<string, List<ServerText>> { ["Alice"] = new(), ["Bob"] = new() };
        alice.Client.ServerTextReceived += t => chat["Alice"].Add(t);
        bob.Client.ServerTextReceived += t => chat["Bob"].Add(t);
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };

        // The disk goes into Alice's PDA from her hand.
        var bobPda = In(w, () => Assert.IsType<obj_item_modular_computer_pda_crew_assistant>(bobMob.belt));
        Do(w, () =>
        {
            var pda = Assert.IsType<obj_item_modular_computer_pda_crew_assistant>(aliceMob.belt);
            var disk = Dm.@new<obj_item_disk_computer_virus_detomatix>(get_turf(aliceMob));
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_hands(disk))));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(pda.item_interaction(aliceMob, disk)));
            Assert.Same(disk, pda.inserted_disk);
        });

        // Her messenger shows "Attach Virus"; she turns it on, picks Bob and sends.
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        alice.Session.Keybinding(KeybindingCode.OpenMessenger);
        UiNode messenger = Shown(w, windows, alice, "NtosMessenger");
        messenger.Walk().Single(n => n.Key == "virus").OnClick();
        messenger = Shown(w, windows, alice, "NtosMessenger");
        Assert.Equal("Attach Virus: Yes", messenger.Walk().Single(n => n.Key == "virus").Text);
        messenger.Walk().Single(n => n.Key == "chat").OnClick();
        messenger = Shown(w, windows, alice, "NtosMessenger");
        messenger.Walk().Single(n => n.Key == "chatInput").OnInput("Free donuts at the bar");
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        windows.Get(alice.Session.Replica.UiWindows.Values.Single(v => v.Interface == "NtosMessenger").Window).Content.Walk().Single(n => n.Key == "send").OnClick();

        // tg's prompts forge the sender.
        Answer(w, alice, "TextInputModal", "submit", "entry", "Jane Roe");
        Answer(w, alice, "TextInputModal", "submit", "entry", "Captain");
        Answer(w, alice, "AlertModal", "choose", "choice", "No");
        w.Run(3 * Second);

        Assert.True(chat["Alice"].Any(t => t.Html.Contains("Success!", StringComparison.Ordinal)),
            "Alice: " + string.Join(" | ", chat["Alice"].Select(t => t.Html)) + "\nruntimes: " + string.Join("\n----\n", runtimes));
        Assert.Equal(5, In(w, () => (Dm.each<obj_item_disk_computer_virus_detomatix>(aliceMob.belt.contents).Single()).charges));
        ServerText line = chat["Bob"].LastOrDefault(t => t.Html.Contains("Free donuts at the bar", StringComparison.Ordinal));
        Assert.True(line.Html != null, "Bob got no PDA message: " + string.Join(" | ", chat["Bob"].Select(t => t.Html)));
        output.WriteLine("Bob: " + line.Html);
        Assert.Contains("Jane Roe (Captain)", line.Html, StringComparison.Ordinal);
        Assert.True(In(w, () => HAS_TRAIT(bobPda, TRAIT_PDA_CAN_EXPLODE)));

        // Bob's (Reply) link is rigged: his PDA explodes in his belt.
        string href = System.Text.RegularExpressions.Regex.Match(line.Html, "href='(byond://[^']*choice=explode[^']*)'").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(href), "no rigged link");
        Assert.True(bob.Session.Topic(href.Replace("&amp;", "&", StringComparison.Ordinal)));
        w.Run(2 * Second);
        Assert.True(In(w, () => QDELETED(bobPda)));
        Assert.Contains(chat["Bob"], t => t.Html.Contains("explodes!", StringComparison.Ordinal));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
