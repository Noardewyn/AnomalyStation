using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The blunt head trauma and the self grasp through the real server on the default test room, with headless clients
/// (<see cref="PlaySession"/>). A player in combat mode hits another on the head with a toolbox (aimed at the head, DV30) until tg's
/// roll knocks the victim senseless: the victim's brain is damaged, its client's game plane carries tg's eye blur (protocol version
/// 29) and the client would blur its view by it, and the victim's walk east strays off its row (the confusion's scramble, DV15). A
/// bleeding player selects its left arm on tg's zone selector, swaps to the right hand and ctrl-clicks its own health doll (DV72):
/// after tg's 0.75 seconds the self-grasp is in its hand, drawn in its HUD's hand slot, and the arm bleeds slower.
/// </summary>
public class HeadTraumaGameplayTests(ITestOutputHelper output)
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

    private static obj_item_organ_brain Brain(mob_living_carbon who) => who.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;

    // The screen entry of the server's screen object of type T in the player's client.screen.
    private static ScreenEntry EntryOf<T>(RealHeadlessWorld w, RealHeadlessWorld.Player p) where T : class
        => p.Session.Replica.Screen.Values.Single(e => In(w, () => w.World.Objects.TryGet(e.Id, out object o) && o is T));

    // The health doll as the client knows it: the entry whose picture holds tg's doll zones (D107's HudReader).
    private static bool IsDoll(ReplicaWorld replica, long id)
    {
        if (!replica.Screen.TryGetValue(id, out ScreenEntry entry))
            return false;
        var zones = new List<FoundZone>();
        HudReader.Collect(replica, entry.Appearance, zones);
        return zones.Count > 0;
    }

    [Fact]
    public void A_toolbox_on_the_head_damages_the_brain_blurs_the_victims_view_and_scrambles_its_walk()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 29, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var bobTexts = new List<string>();
        bob.Client.ServerTextReceived += t => bobTexts.Add(t.Html);
        w.Run(10);
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(12, 12, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(13, 12, 1)));
        });
        w.Host.EnqueueConsoleLine("give Alice /obj/item/storage/toolbox/mechanical");
        alice.Session.ToggleCombatMode();
        w.Run(10);
        Assert.IsType<obj_item_storage_toolbox_mechanical>(In(w, () => aliceMob.get_active_held_item()));
        float brainBefore = In(w, () => Dm.num(Brain(bobMob).damage));
        Assert.Empty(bob.Session.Replica.GamePlaneFilters);

        // tg's prob(force) a hit (12 %) knocks the victim senseless; each other hit hurts the brain by a fifth of it. The victim is
        // healed of the brute between the hits, so it never falls into crit (which would spare it the confusion).
        int hits = 0;
        while (In(w, () => bobMob.has_status_effect(typeof(datum_status_effect_confusion))) == null && hits++ < 80)
        {
            alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 27, AimZone.Head), ClickButton.Left, ClickModifiers.None);
            w.Run(Second);
            Do(w, () => Waiting.NoSleep(bobMob.adjust_brute_loss(-200)));
        }
        output.WriteLine($"{hits} hits; the brain's damage {In(w, () => Brain(bobMob).damage)} (was {brainBefore})");
        Assert.NotNull(In(w, () => bobMob.has_status_effect(typeof(datum_status_effect_confusion))));
        Assert.NotNull(In(w, () => bobMob.has_status_effect(typeof(datum_status_effect_eye_blur))));
        Assert.True(In(w, () => Dm.num(Brain(bobMob).damage)) >= brainBefore + 20f, "the senseless hit's 20 brain damage");
        Assert.Contains(bobTexts, t => t.Contains("You're knocked senseless!", StringComparison.Ordinal));
        alice.Session.ToggleCombatMode();
        w.Run(5);

        // The victim's client: tg's gaussian blur on its game plane, which the client draws over its view.
        FilterEntry blur = Assert.Single(bob.Session.Replica.GamePlaneFilters);
        Assert.Equal(FilterKind.Blur, blur.Kind);
        Assert.InRange(blur.Size, 0.6f, 1f); // clamp(seconds left * 0.05, 0.6, 3), 20 seconds at most
        Assert.True(ViewBlur.Sigma(bob.Session.Replica.GamePlaneFilters, 1080) > 0.6f, "the victim's view is blurred");
        Assert.Empty(alice.Session.Replica.GamePlaneFilters); // the attacker's is not

        // The victim walks east: the confusion scrambles tg's steps off its row.
        Do(w, () => Waiting.NoSleep(bobMob.forceMove(locate(4, 12, 1))));
        w.Run(2);
        var rows = new HashSet<int>();
        bob.Yaw = -Math.PI / 2; // Godot's yaw: 0 looks north, +90 degrees west
        bob.Keys = MoveKeys.Forward;
        for (int t = 0; t < 3 * Second; t++)
        {
            w.Run(1);
            rows.Add(In(w, () => (int)bobMob.y));
        }
        bob.Keys = MoveKeys.None;
        w.Run(2);
        output.WriteLine($"the confused walk went over rows {string.Join(", ", rows.OrderBy(r => r))} to x {In(w, () => bobMob.x)}");
        Assert.True(rows.Count > 1, "the confused walk strays off its row");

        // The blur runs out with the effect.
        w.Run(25 * Second);
        Assert.Null(In(w, () => bobMob.has_status_effect(typeof(datum_status_effect_eye_blur))));
        Assert.Empty(bob.Session.Replica.GamePlaneFilters);
    }

    [Fact]
    public void A_bleeding_player_ctrl_clicks_its_health_doll_and_grasps_the_arm_and_bleeds_slower()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 30, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player p = w.Join("Bleeder");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        w.Run(10);
        w.Host.EnqueueConsoleLine("bleed Bleeder l_arm 30");
        w.Run(5);
        obj_item_bodypart arm = In(w, () => me.get_bodypart(BODY_ZONE_L_ARM) as obj_item_bodypart);
        float rate = In(w, () => arm.cached_bleed_rate);
        Assert.True(rate > 0f, "the arm bleeds");

        // The bleeding, unheld, over ten seconds.
        float blood0 = In(w, () => Dm.num(me.get_blood_volume()));
        w.Run(10 * Second);
        float lostUnheld = blood0 - In(w, () => Dm.num(me.get_blood_volume()));

        // tg's zone selector: the left arm (get_zone_at: x 21 to 24, y 10 to 13); then the right hand (X).
        ScreenEntry selector = EntryOf<atom_movable_screen_zone_sel>(w, p);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, selector.Id, 0, 0, 0, 22, 12), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Equal(BODY_ZONE_L_ARM, In(w, () => me.zone_selected));
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(5);
        Assert.Equal(2f, In(w, () => me.active_hand_index));

        // The ctrl-click on the HUD's health doll, which the client sends as a ctrl-click on the own mob (DV72).
        long doll = replica.Screen.Keys.Single(id => IsDoll(replica, id));
        ClickTarget target = DragModel.SelfCtrlClickOnDoll(new ClickTarget(TargetKind.Screen, doll, 0, 0, 0, 16, 16), ClickModifiers.Ctrl, id => IsDoll(replica, id), replica.OwnEntity);
        Assert.Equal(new ClickTarget(TargetKind.Entity, me.dm_id, 0, 0, 0), target);
        Assert.True(p.Session.Click(target, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(Second / 2);
        Assert.Null(In(w, () => me.get_active_held_item())); // tg's 0.75 s do_after
        w.Run(Second);
        var grasp = In(w, () => me.get_active_held_item()) as obj_item_hand_item_self_grasp;
        Assert.NotNull(grasp);
        Assert.Same(grasp, In(w, () => arm.grasped_by));
        Assert.Equal(rate * 0.7f, In(w, () => arm.cached_bleed_rate), 4);
        Assert.Contains(texts, t => t.Contains("You grab hold of your human left arm tightly.", StringComparison.Ordinal));

        // The client's HUD: the self-grasp drawn in the right hand's slot, tg's "latexballoon" of hand.dmi.
        ScreenEntry hand = replica.Screen.Values.Single(e => e.VisChildren != null && e.VisChildren.Contains(grasp.dm_id));
        ScreenChild child = Assert.Single(hand.VisChildren.Items);
        Assert.True(replica.TryGetAppearance(child.Appearance, out AppearanceEntry look));
        Assert.Equal("latexballoon", replica.ResolveString(look.IconState));
        Assert.Equal("icons/obj/weapons/hand.dmi", replica.ResolveResource(look.Icon));

        // Held, the same ten seconds lose less blood.
        float blood1 = In(w, () => Dm.num(me.get_blood_volume()));
        w.Run(10 * Second);
        float lostHeld = blood1 - In(w, () => Dm.num(me.get_blood_volume()));
        output.WriteLine($"blood lost in ten seconds: {lostUnheld:0.00} unheld, {lostHeld:0.00} held");
        Assert.True(lostHeld < lostUnheld * 0.85f, $"the grasped arm bleeds slower ({lostHeld} against {lostUnheld})");
    }
}
