using System.Net;
using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 6, slice E (DV139): the give key is gone and "Offer item" is in the right-click wheel, through a real server and two
/// clients. The server offers it on the person beside Alice while she holds an item, as the wheel's Extra entry (tg's own Ctrl+Shift-click
/// screentip is not in the wheel); her pick reaches Bob's client as tg's offer alert, which takes the crowbar into his hands; it is not offered
/// with an empty hand, to an unconscious person, to herself or from two tiles away, and a pick sent all the same does nothing.
/// </summary>
public class RadialOfferingGameplayTests
{
    private const int Second = 20;
    private const string Label = "Offer item";

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

    private sealed record Pair(RealHeadlessWorld W, RealHeadlessWorld.Player Alice, RealHeadlessWorld.Player Bob, mob_living_carbon_human AliceMob,
        mob_living_carbon_human BobMob, List<string> AliceTexts, List<string> BobTexts)
    {
        public string AliceName => In(W, () => Dm.str(AliceMob.name));

        // tg's give alert from Alice on Bob's side, its screen object's id (0: none)
        public long Alert => In(W, () => (BobMob.alerts[AliceName] as atom_movable_screen_alert_give)?.dm_id ?? 0);
    }

    // Alice and Bob on neighbouring tiles, a crowbar in Alice's active hand unless told otherwise.
    private static Pair Make(ulong seed, bool hold = true)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var aliceTexts = new List<string>();
        var bobTexts = new List<string>();
        alice.Client.ServerTextReceived += t => aliceTexts.Add(WebUtility.HtmlDecode(t.Html));
        bob.Client.ServerTextReceived += t => bobTexts.Add(WebUtility.HtmlDecode(t.Html));
        In(w, () =>
        {
            turf start = get_turf(aliceMob);
            Waiting.NoSleep(bobMob.forceMove(locate(Dm.num(start.x) + 1, Dm.num(start.y), Dm.num(start.z))));
            if (hold)
                Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_hands(Dm.@new<obj_item_crowbar>(start)))));
        });
        w.Run(Second);
        return new Pair(w, alice, bob, aliceMob, bobMob, aliceTexts, bobTexts);
    }

    private static ClickTarget On(atom target) => new(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16, AimZone.None);

    // The hints the server sends the viewer for a fresh hover on the target (what the wheel shows).
    private static ContextHintsMessage Hints(RealHeadlessWorld w, RealHeadlessWorld.Player viewer, atom target)
    {
        var hints = new List<ContextHintsMessage>();
        void Take(ContextHintsMessage m) => hints.Add(m);
        viewer.Client.ContextHintsReceived += Take;
        try
        {
            w.Run(4);
            viewer.Session.Rehover();
            Assert.True(viewer.Session.Hover(On(target), w.Now), "the hover went out");
            w.Run(6);
            ContextHintsMessage last = hints.LastOrDefault(h => h.Target == target.dm_id);
            Assert.NotNull(last);
            return last;
        }
        finally
        {
            viewer.Client.ContextHintsReceived -= Take;
        }
    }

    // Whether the wheel offers "Offer item": the Extra entry, and never tg's Ctrl+Shift-click hint of that name beside it
    private static bool Offered(RealHeadlessWorld w, RealHeadlessWorld.Player viewer, atom target)
    {
        ContextHintsMessage message = Hints(w, viewer, target);
        Assert.DoesNotContain(message.Hints, h => h.Label == Label && h.Action != ContextHintAction.Extra);
        return message.Hints.Count(h => h.Label == Label && h.Action == ContextHintAction.Extra) == 1;
    }

    [Fact]
    public void The_pick_reaches_the_other_player_as_tgs_offer_alert_and_he_takes_the_item()
    {
        Pair p = Make(5891);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            int runtimes = w.World.RuntimeErrorCount;
            Assert.True(Offered(w, p.Alice, p.BobMob), "on Bob beside her with a crowbar in hand");
            // after Strip, as the radial lists the server's entries
            List<string> labels = Hints(w, p.Alice, p.BobMob).Hints.Select(h => h.Label).ToList();
            Assert.True(labels.IndexOf("Strip") < labels.IndexOf(Label), string.Join(", ", labels));
            Assert.Equal(0, p.Alert);
            Assert.True(p.Alice.Session.ContextPick(On(p.BobMob), Label));
            w.Run(Second);
            Assert.Contains(p.AliceTexts, t => t.Contains("You offer", StringComparison.Ordinal));
            long alert = p.Alert;
            Assert.True(alert != 0, "Bob has tg's offer alert: " + string.Join(" | ", p.BobTexts));
            // Bob's client has it among his screen entries, as a chip on the human HUD
            ReplicaWorld bobs = p.Bob.Session.Replica;
            Assert.True(bobs.Screen.ContainsKey(alert), "Bob's client receives the alert");
            Assert.Equal(LiveHudRole.Alert, LiveHud.RoleOf(bobs, bobs.Screen[alert]));
            Assert.False(p.Alice.Session.Replica.Screen.ContainsKey(alert), "Alice's does not");
            // his click on it takes the crowbar (tg's alert/give Click)
            Assert.True(p.Bob.Session.Click(new ClickTarget(TargetKind.Screen, alert, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            Assert.True(In(w, () => p.BobMob.get_active_held_item() is obj_item_crowbar || p.BobMob.get_inactive_held_item() is obj_item_crowbar), string.Join(" | ", p.BobTexts));
            Assert.Null(In(w, () => p.AliceMob.get_active_held_item()));
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }

    [Fact]
    public void It_is_not_offered_with_an_empty_hand_and_a_crafted_pick_does_nothing()
    {
        Pair p = Make(5892, hold: false);
        using (p.W)
        {
            Assert.False(Offered(p.W, p.Alice, p.BobMob), "an empty hand");
            Assert.True(p.Alice.Session.ContextPick(On(p.BobMob), Label)); // sent all the same
            p.W.Run(Second);
            Assert.Equal(0, p.Alert);
            Assert.DoesNotContain(p.AliceTexts, t => t.Contains("offer", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void It_is_not_offered_to_an_incapacitated_person_to_herself_or_from_two_tiles_away()
    {
        Pair p = Make(5893);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            // to herself: no entry of hers
            Assert.DoesNotContain(In(w, () => RadialExtras.Of(p.AliceMob, p.AliceMob)), e => e.Kind == RadialExtra.Offer);
            Assert.False(Offered(w, p.Alice, p.AliceMob), "not on herself");
            // to Bob unconscious
            In(w, () => Waiting.NoSleep(p.BobMob.Unconscious(60 * SECONDS)));
            w.Run(4);
            Assert.True(In(w, () => Dm.truthy(p.BobMob.incapacitated)));
            Assert.False(Offered(w, p.Alice, p.BobMob), "not to an unconscious person");
            Assert.True(p.Alice.Session.ContextPick(On(p.BobMob), Label)); // a crafted pick
            w.Run(Second);
            Assert.Equal(0, p.Alert);
            In(w, () => Waiting.NoSleep(p.BobMob.SetUnconscious(0)));
            w.Run(Second);
            Assert.True(Offered(w, p.Alice, p.BobMob), "awake again");
            // from two tiles away
            In(w, () =>
            {
                turf start = get_turf(p.AliceMob);
                Waiting.NoSleep(p.BobMob.forceMove(locate(Dm.num(start.x) + 2, Dm.num(start.y), Dm.num(start.z))));
            });
            w.Run(Second);
            Assert.False(Offered(w, p.Alice, p.BobMob), "not from two tiles away");
            Assert.True(p.Alice.Session.ContextPick(On(p.BobMob), Label));
            w.Run(Second);
            Assert.Equal(0, p.Alert);
        }
    }
}
