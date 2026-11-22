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
/// M9 slice E through the real server on MetaStation with headless clients: Alice and Bob join with their assistant PDAs on the belt,
/// imprinted and in the crew manifest. Alice's chat window Message button (protocol 40) opens her PDA's NtosMessenger; she picks Bob and
/// sends a line, which goes through telecomms and the messaging server (logged there) to Bob's PDA: tg's ring and his "PDA message from"
/// line on the Messages tab's type, with its (Reply) link; Bob's link asks tg's text input and his answer reaches Alice. An engineering
/// headset lists ":e " in Alice's Radio list. With the telecomms room's APCs off the server does not answer.
/// </summary>
public class PdaGameplayTests(ITestOutputHelper output)
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

    [MetaServerFact]
    public void Alice_messages_Bob_from_her_pda_and_he_replies_from_the_chat_link()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 94, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var chat = new Dictionary<string, List<ServerText>> { ["Alice"] = new(), ["Bob"] = new() };
        alice.Client.ServerTextReceived += t => chat["Alice"].Add(t);
        bob.Client.ServerTextReceived += t => chat["Bob"].Add(t);
        var bobSounds = new List<SoundEvent>();
        bob.Session.Replica.SoundsReceived += events => bobSounds.AddRange(events);
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };

        // The job's PDA, imprinted (tg's post_equip) and registered; the crew manifest has both (the late join's inject).
        string aliceName = In(w, () => Dm.str(aliceMob.real_name));
        string bobName = In(w, () => Dm.str(bobMob.real_name));
        Do(w, () =>
        {
            var pda = Assert.IsType<obj_item_modular_computer_pda_crew_assistant>(aliceMob.belt);
            Assert.Equal(aliceName, Dm.str(pda.saved_identification));
            Assert.Equal("Assistant", Dm.str(pda.saved_job));
            Assert.Equal(2, Dm.each(GLOB.pda_messengers).Count());
            Assert.Equal(new[] { aliceName, bobName }.OrderBy(n => n), Dm.each<datum_record_crew>(GLOB.manifest.general).Select(r => r.name).OrderBy(n => n));
        });

        // The Message button: Alice's PDA boots and shows its messenger with Bob among the contacts.
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        alice.Session.Keybinding(KeybindingCode.OpenMessenger);
        UiNode messenger = Shown(w, windows, alice, "NtosMessenger");
        UiNode contact = messenger.Walk().Single(n => n.Key == "chat");
        Assert.Equal(" " + bobName + " (Assistant)", contact.Text);
        contact.OnClick();
        messenger = Shown(w, windows, alice, "NtosMessenger");
        messenger.Walk().Single(n => n.Key == "chatInput").OnInput("Meet me at the bar");
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        windows.Get(alice.Session.Replica.UiWindows.Values.Single(v => v.Interface == "NtosMessenger").Window).Content.Walk().Single(n => n.Key == "send").OnClick();
        w.Run(3 * Second);

        // Bob's PDA rings and his chat has tg's line, typed for the Messages tab; Alice's says it was sent.
        ServerText line = chat["Bob"].LastOrDefault(t => t.Html.Contains("Meet me at the bar", StringComparison.Ordinal));
        Assert.True(line.Html != null, "Bob got no PDA message: " + string.Join(" | ", chat["Bob"].Select(t => t.Html)) + "\nAlice: " + string.Join(" | ", chat["Alice"].Select(t => t.Html)) + "\nruntimes: " + string.Join("\n----\n", runtimes));
        output.WriteLine("Bob: " + line.Html);
        Assert.Equal("pda", line.Type);
        Assert.Contains("PDA message from " + aliceName + " (Assistant)", line.Html, StringComparison.Ordinal);
        Assert.Contains(bobSounds, e => e.File == "sound/machines/beep/twobeep_high.ogg");
        Assert.Contains(chat["Alice"], t => t.Type == "pda" && t.Html.Contains("PDA message sent to " + bobName, StringComparison.Ordinal));
        Do(w, () =>
        {
            var server = Dm.each<obj_machinery_telecomms_message_server>(GLOB.telecomm_machines).First();
            Assert.Contains(Dm.each<datum_data_tablet_msg>(server.pda_msgs), m => m.message.Contains("Meet me at the bar", StringComparison.Ordinal));
        });

        // Bob's (Reply) link: tg's text input; his answer reaches Alice.
        string href = System.Text.RegularExpressions.Regex.Match(line.Html, "href='(byond://[^']*choice=message[^']*)'").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(href), "no Reply link");
        Assert.True(bob.Session.Topic(href.Replace("&amp;", "&", StringComparison.Ordinal)));
        w.Run(2 * Second);
        UiWindowState input = bob.Session.Replica.UiWindows.Values.Single(v => v.Interface == "TextInputModal");
        Assert.True(bob.Session.UiAct(input.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of("On my way")))));
        w.Run(3 * Second);
        Assert.Contains(chat["Alice"], t => t.Type == "pda" && t.Html.Contains("On my way", StringComparison.Ordinal) && t.Html.Contains("PDA message from " + bobName, StringComparison.Ordinal));

        // The Radio list: an engineering headset lists ":e ".
        Do(w, () =>
        {
            if (aliceMob.ears != null)
                qdel(aliceMob.ears);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.equip_to_slot_if_possible(Dm.@new<obj_item_radio_headset_headset_eng>(get_turf(aliceMob)), ITEM_SLOT_EARS))));
        });
        w.Run(Second);
        Assert.Contains(":e ", alice.Session.Replica.RadioPrefixes);

        // The telecomms room's APCs off: the messaging server does not answer.
        Do(w, () =>
        {
            foreach (obj_machinery_power_apc apc in Dm.each<obj_machinery_telecomms>(GLOB.telecomm_machines).Select(m => (get_area(m) as area)?.apc).Where(a => a != null).Distinct().ToList())
                Waiting.NoSleep(apc.toggle_breaker(null));
        });
        w.Run(5 * Second);
        int before = chat["Alice"].Count;
        messenger = Shown(w, windows, alice, "NtosMessenger");
        messenger.Walk().Single(n => n.Key == "chatInput").OnInput("Hello?");
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        windows.Get(alice.Session.Replica.UiWindows.Values.Single(v => v.Interface == "NtosMessenger").Window).Content.Walk().Single(n => n.Key == "send").OnClick();
        w.Run(3 * Second);
        Assert.Contains(chat["Alice"].Skip(before), t => t.Html.Contains("ERROR: Server is not responding.", StringComparison.Ordinal));
        Assert.DoesNotContain(chat["Bob"], t => t.Html.Contains("Hello?", StringComparison.Ordinal));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
