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
/// Syndicate stage G4a (D316) through the real server on MetaStation with headless clients: Bob uses a GLA-2 mail counterfeit device
/// holding a signaler in his hand; tg's four prompts (an envelope, armed, to anyone, a title) turn it into an armed envelope in his
/// hand. Alice opens it: the signaler lands in her hand and its button goes off.
/// </summary>
public class CounterfeitMailGameplayTests(ITestOutputHelper output)
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

    private static void Answer(RealHeadlessWorld w, RealHeadlessWorld.Player p, string ui, string action, string key, string value)
    {
        w.Run(2 * Second);
        UiWindowState input = p.Session.Replica.UiWindows.Values.Single(v => v.Interface == ui);
        Assert.True(p.Session.UiAct(input.Window, action, UiValue.ObjectOf((key, UiValue.Of(value)))));
    }

    [MetaServerFact]
    public void Bob_counterfeits_an_armed_envelope_and_its_signaler_goes_off_when_Alice_opens_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 41, TimeoutSteps = 20000 },
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

        // The device, a signaler inside, in Bob's active hand.
        var signaler = In(w, () =>
        {
            var device = Dm.@new<obj_item_storage_mail_counterfeit_device>(get_turf(bobMob));
            var inside = Dm.@new<obj_item_assembly_signaler>(device);
            Assert.True(Dm.truthy(Waiting.NoSleep(bobMob.put_in_active_hand(device))));
            return inside;
        });

        // Z uses it; tg's prompts make it an armed envelope for anyone.
        bob.Session.Keybinding(KeybindingCode.ActivateInhand);
        Answer(w, bob, "AlertModal", "choose", "choice", "Envelope");
        Answer(w, bob, "AlertModal", "choose", "choice", "Yes");
        Answer(w, bob, "ListInputWindow", "submit", "entry", "Anyone");
        Answer(w, bob, "TextInputModal", "submit", "entry", "for Alice");
        w.Run(2 * Second);
        var envelope = In(w, () => Assert.IsType<obj_item_mail_traitor_envelope>(bobMob.get_active_held_item()));
        Assert.Equal("for Alice", In(w, () => envelope.name));
        Assert.Equal(TRUE, In(w, () => envelope.armed));
        Assert.Same(envelope, In(w, () => signaler.loc));

        // Alice gets it and opens it with Z: the signaler is in her hand and its button went off.
        Do(w, () =>
        {
            Waiting.NoSleep(bobMob.dropItemToGround(envelope));
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_active_hand(envelope))));
        });
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(3 * Second);
        Assert.True(In(w, () => QDELETED(envelope)));
        Assert.Same(aliceMob, In(w, () => signaler.loc));
        Assert.True(chat["Alice"].Any(t => t.Html.Contains("you accidentally press a button on", StringComparison.Ordinal)),
            "Alice: " + string.Join(" | ", chat["Alice"].Select(t => t.Html)));
        output.WriteLine("Alice: " + chat["Alice"].Last(t => t.Html.Contains("accidentally", StringComparison.Ordinal)).Html);

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
