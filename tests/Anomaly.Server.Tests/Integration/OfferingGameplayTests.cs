using Anomaly.Client.Shared;
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
/// M9 slice F's offering and hand items through the real server on MetaStation. Alice holds a crowbar ("Offer item" on Bob's
/// screentip) and presses G (protocol 41's give code): Bob gets tg's give alert and clicks it to take the crowbar. Her "*slap" hand,
/// offered, is a high-five (tg's high_fiver: the slap sound); she slaps Bob with it. A "*kiss" blown at Bob hits him; an offered
/// "*hand" he takes lets her pull him; her "*circle" bops him when he examines her; a "*noogie" on him, aggressively grabbed, starts.
/// </summary>
public class OfferingGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string Slap = "sound/items/weapons/slap.ogg";

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static bool Said(List<ServerText> lines, string text) => lines.Any(t => t.Html.Contains(text, StringComparison.Ordinal));

    private static string Lines(List<ServerText> lines) => string.Join(" | ", lines.Select(t => t.Html));

    [MetaServerFact]
    public void Alice_gives_Bob_a_crowbar_a_high_five_a_slap_a_kiss_her_hand_a_circle_and_a_noogie()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 62, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var them = (mob_living_carbon_human)w.MobOf(bob);
        var aliceChat = new List<ServerText>();
        var bobChat = new List<ServerText>();
        alice.Client.ServerTextReceived += aliceChat.Add;
        bob.Client.ServerTextReceived += bobChat.Add;
        var bobHeard = new List<SoundEvent>();
        bob.Session.Replica.SoundsReceived += bobHeard.AddRange;
        (int sx, int sy, int z) = In(w, () => ((int)me.x, (int)me.y, (int)me.z));
        using (w.World.Enter())
            Waiting.NoSleep(them.forceMove(Builtins.locate(sx - 1, sy, z)));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        string aliceName = In(w, () => Dm.str(me.name));

        long AlertFromAlice() => In(w, () => (them.alerts[aliceName] as atom)?.dm_id ?? 0);

        void ClickAlert()
        {
            long alert = AlertFromAlice();
            Assert.True(alert != 0, "Bob has no alert from Alice: " + Lines(bobChat));
            Assert.True(bob.Session.Click(new ClickTarget(TargetKind.Screen, alert, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }

        void Emote(string key)
        {
            Assert.True(alice.Session.Emote(key));
            w.Run(Second);
        }

        // ---- 1. A crowbar, offered with G and taken from the alert.
        obj_item crowbar = In(w, () =>
        {
            var item = Dm.@new<obj_item_crowbar>(GlobalProcs.get_turf(me));
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(item))));
            return (obj_item)item;
        });
        Assert.Equal("Offer item", In(w, () =>
        {
            DmList context = Dm.list();
            them.add_context(them, context, crowbar, me);
            return Dm.str(context[SCREENTIP_CONTEXT_CTRL_SHIFT_LMB]);
        }));
        alice.Session.Keybinding(KeybindingCode.GiveItem);
        w.Run(Second);
        Assert.True(Said(aliceChat, "You offer"), Lines(aliceChat));
        Assert.True(In(w, () => them.alerts[aliceName] is atom_movable_screen_alert_give));
        ClickAlert();
        Assert.Same(them, In(w, () => crowbar.loc));
        Assert.True(Said(bobChat, "You take"), Lines(bobChat));
        Assert.Equal(0, AlertFromAlice());

        // ---- 2. The slapper offered: a high-five, with tg's slap.
        Emote("slap");
        Assert.True(In(w, () => me.get_active_held_item() is obj_item_hand_item_slapper));
        alice.Session.Keybinding(KeybindingCode.GiveItem);
        w.Run(Second);
        Assert.True(Said(aliceChat, "looking for a high-five!"), Lines(aliceChat));
        Assert.True(In(w, () => them.alerts[aliceName] is atom_movable_screen_alert_give_highfive));
        bobHeard.Clear();
        ClickAlert();
        Assert.True(Said(bobChat, "You high-five"), Lines(bobChat));
        Assert.Contains(bobHeard, e => e.File == Slap);

        // ---- 3. A slap with the same hand.
        bobHeard.Clear();
        Assert.True(alice.Session.Click(them.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(Said(bobChat, "slaps"), Lines(bobChat));
        Assert.Contains(bobHeard, e => e.File == Slap);
        alice.Session.Keybinding(KeybindingCode.DropItem); // a DROPDEL hand item: gone
        w.Run(5);
        Assert.Null(In(w, () => me.get_active_held_item()));

        // ---- 4. A kiss blown at Bob.
        Emote("kiss");
        Assert.True(In(w, () => me.get_active_held_item() is obj_item_hand_item_kisser));
        Assert.True(alice.Session.Click(them.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.True(Said(bobChat, "hit by"), Lines(bobChat));
        Assert.Null(In(w, () => me.get_active_held_item()));

        // ---- 5. Her hand, offered and taken: she pulls him.
        Emote("hand");
        Assert.True(In(w, () => me.get_active_held_item() is obj_item_hand_item_hand));
        alice.Session.Keybinding(KeybindingCode.GiveItem);
        w.Run(Second);
        Assert.True(Said(aliceChat, "You extend out your hand."), Lines(aliceChat));
        ClickAlert();
        Assert.Same(them, In(w, () => me.pulling));
        Assert.True(Said(bobChat, "which allows"), Lines(bobChat));
        alice.Session.Keybinding(KeybindingCode.StopPulling);
        w.Run(5);

        // ---- 6. The circle game: Bob examines Alice and gets bopped.
        Emote("circle");
        Assert.True(In(w, () => me.get_active_held_item() is obj_item_hand_item_circlegame));
        Assert.True(bob.Session.Click(me.dm_id, ClickButton.Left, ClickModifiers.Shift));
        w.Run(2 * Second);
        Assert.True(Said(bobChat, "bops you"), Lines(bobChat));
        Assert.True(In(w, () => Dm.num(them.get_stamina_loss())) > 0);

        // ---- 7. A noogie on Bob, aggressively grabbed (in combat mode, DV66).
        alice.Session.ToggleCombatMode();
        w.Run(5);
        Assert.True(alice.Session.Click(them.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(Second);
        Assert.True(alice.Session.Click(them.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(Second);
        Assert.Equal(GRAB_AGGRESSIVE, In(w, () => me.grab_state));
        Emote("noogie");
        Assert.True(In(w, () => me.get_active_held_item() is obj_item_hand_item_noogie));
        Assert.True(alice.Session.Click(them.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second);
        Assert.True(Said(bobChat, "starts giving you a rough noogie!"), Lines(bobChat));
        output.WriteLine("Bob: " + Lines(bobChat));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, w.Err.ToString());
    }
}
