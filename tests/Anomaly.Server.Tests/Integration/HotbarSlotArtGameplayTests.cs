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
/// ui-overhaul.md section 16 through a real server and a headless client: the hand boxes, the pocket, ID and worn slot entries as the real replicator
/// encodes them give the bar its pictures. tg's active mark (a text overlay, "lhandactive", that BYOND and the port's build_appearance_list turn
/// into an appearance of the hand's icon file) resolves to a picture of the player's HUD style, and moves with a swap; a slot tg locks (no jumpsuit:
/// the ID, belt and pockets; no suit: suit storage) comes with tg's alpha 128, which the slot's picture is drawn at.
/// </summary>
public class HotbarSlotArtGameplayTests
{
    private const int Second = 20;
    private const string Midnight = "icons/hud/screen_midnight.dmi";

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static ScreenEntry Hand(ReplicaWorld r, int index) => r.Screen.Values.First(e => e.HandIndex == index);

    private static ScreenEntry Slot(ReplicaWorld r, float slotId) => r.Screen.Values.First(e => e.SlotId == (uint)slotId && e.HandIndex == 0);

    [Fact]
    public void The_real_servers_active_mark_resolves_to_the_hand_styles_picture_and_follows_a_swap()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7201, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(Second);
        ReplicaWorld seen = alice.Session.Replica;
        ScreenEntry left = Hand(seen, 1), right = Hand(seen, 2);
        Assert.Equal((Midnight, "hand_l"), LiveHud.SlotArt(seen, left));
        Assert.Equal((Midnight, "hand_r"), LiveHud.SlotArt(seen, right));
        // the mark as the replicator sends it: an overlay whose icon file is the hand's (tg's text overlay through build_appearance_list)
        Assert.True(seen.TryGetAppearance(LiveHud.ActiveMark(seen, left), out AppearanceEntry mark));
        Assert.Equal(Midnight, seen.ResolveResource(mark.Icon));
        Assert.Equal((Midnight, "lhandactive"), LiveHud.ActiveMarkArt(seen, left));
        Assert.Null(LiveHud.ActiveMarkArt(seen, right));
        alice.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(Second / 2);
        Assert.Null(LiveHud.ActiveMarkArt(seen, Hand(seen, 1)));
        Assert.Equal((Midnight, "rhandactive"), LiveHud.ActiveMarkArt(seen, Hand(seen, 2)));
    }

    [Fact]
    public void A_slot_tg_locks_comes_at_alpha_128_and_its_picture_is_drawn_at_half()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7202, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        In(w, () =>
        {
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
        });
        w.Run(Second);
        ReplicaWorld seen = alice.Session.Replica;
        // dressed: the belt and the ID slot at tg's own alpha; suit storage, with no suit, locked (human.dm update_locked_slots)
        Assert.Equal(0xFFFFFFFFu, LiveHud.SlotTint(seen, Slot(seen, ITEM_SLOT_BELT)));
        Assert.Equal(0xFFFFFFFFu, LiveHud.SlotTint(seen, Slot(seen, ITEM_SLOT_ID)));
        Assert.Equal(0xFFFFFF80u, LiveHud.SlotTint(seen, Slot(seen, ITEM_SLOT_SUITSTORE)));
        // the jumpsuit off: tg locks the ID, the belt and both pockets, and the client draws their pictures at half (a quarter on a half-seen cell)
        In(w, () => qdel(body.w_uniform));
        w.Run(Second);
        foreach (float slot in new[] { ITEM_SLOT_BELT, ITEM_SLOT_ID, ITEM_SLOT_LPOCKET, ITEM_SLOT_RPOCKET })
        {
            Assert.True(seen.TryGetAppearance(Slot(seen, slot).Appearance, out AppearanceEntry look));
            Assert.Equal(128, look.Alpha);
            Assert.Equal(0xFFFFFF80u, LiveHud.SlotTint(seen, Slot(seen, slot)));
            Assert.Equal(0xFFFFFF40u, LiveHud.SlotTint(seen, Slot(seen, slot), 0.5f));
        }
    }
}
