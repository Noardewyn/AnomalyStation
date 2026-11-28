using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV694, the inventory's right-click wheel, through a real server and a raw headless client: the wheel's entries come from the server's answer to
/// the hover of the item (InventoryRules.MenuEntries) and each is sent as the scene sends it (an action button: a ContextPick of its name; tg's
/// context entry: its click; Examine: a Shift+left click; Use in hand: tg's activate-in-hand key), always on the item named as a screen target.
/// Alice wears a breath mask and a backpack; an oxygen tank in her active hand and one in her suit storage (on a space suit) offer "Set Internals",
/// and picking it opens her internals from that tank; a flashlight in her pocket offers "Toggle Light" and picking it lights it; the tank and a
/// flashlight in her backpack offer no action (tg grants none there) but Examine, which prints tg's examine box; nothing moves to her hand.
/// </summary>
public class InventoryWheelGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // what the wheel offers on the item: the hover the scene sends (the item kept hovered while the wheel is open) and the server's answer
    private static List<RadialEntry> Wheel(RealHeadlessWorld w, RealHeadlessWorld.Player p, obj_item item, bool activeHand)
    {
        ClickTarget target = InventoryRules.MenuTarget(item.dm_id);
        var answers = new List<ContextHintsMessage>();
        p.Client.ContextHintsReceived += answers.Add; // the scene hands them to the session (GameScene.OnHints)
        p.Session.Rehover();
        Assert.True(p.Session.Hover(target, w.Now));
        w.Run(Second);
        p.Client.ContextHintsReceived -= answers.Add;
        ContextHintsMessage hints = answers.LastOrDefault(h => KeyChips.AnswerFor(h, target) != null);
        Assert.True(hints != null, $"no hints for {item.name}: {string.Join(" | ", answers.Select(a => a.TargetKind + " #" + a.Target))}");
        List<RadialEntry> entries = InventoryRules.MenuEntries(hints, activeHand);
        p.Session.Hover(ClickTarget.None, w.Now + 1);
        w.Run(Second);
        return entries;
    }

    [Fact]
    public void The_inventory_wheel_offers_and_runs_an_items_actions_where_tg_grants_them_and_examines_anywhere_without_moving_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7611, TimeoutSteps = 12000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var her = (mob_living_carbon_human)w.MobOf(alice);
        obj_item_tank handTank = null, suitTank = null, bagTank = null;
        obj_item_flashlight pocketLight = null, bagLight = null;
        obj_item bag = null;
        In(w, () =>
        {
            foreach (obj_item item in new[] { her.back, her.belt, her.l_store, her.r_store, her.wear_id, her.s_store, her.wear_mask, her.wear_suit, her.head,
                her.get_item_for_held_index(1) as obj_item, her.get_item_for_held_index(2) as obj_item })
                if (item != null)
                    qdel(item);
            if (her.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(her.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(her), ITEM_SLOT_ICLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(her.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_mask_breath>(get_turf(her)), ITEM_SLOT_MASK))));
            Assert.True(Dm.truthy(Waiting.NoSleep(her.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_suit_space>(get_turf(her)), ITEM_SLOT_OCLOTHING))));
            bag = Dm.@new<obj_item_storage_backpack>(get_turf(her));
            Assert.True(Dm.truthy(Waiting.NoSleep(her.equip_to_slot_if_possible(bag, ITEM_SLOT_BACK))));
            suitTank = Dm.@new<obj_item_tank_internals_oxygen>(get_turf(her));
            Assert.True(Dm.truthy(Waiting.NoSleep(her.equip_to_slot_if_possible(suitTank, ITEM_SLOT_SUITSTORE))));
            pocketLight = Dm.@new<obj_item_flashlight>(get_turf(her));
            Assert.True(Dm.truthy(Waiting.NoSleep(her.equip_to_slot_if_possible(pocketLight, ITEM_SLOT_LPOCKET))));
            bagTank = Dm.@new<obj_item_tank_internals_oxygen>(bag);
            bagLight = Dm.@new<obj_item_flashlight>(bag);
            handTank = Dm.@new<obj_item_tank_internals_oxygen>(get_turf(her));
            Assert.True(Dm.truthy(Waiting.NoSleep(her.put_in_hand(handTank, her.active_hand_index))));
            return 0;
        });
        w.Run(2 * Second);
        int runtimes = w.World.RuntimeErrorCount;
        var chat = new List<ServerText>();
        alice.Client.ServerTextReceived += chat.Add;
        // the bag's items are in her client's Storages message, so the client draws them in the backpack's panel
        Assert.Contains(bagTank.dm_id, alice.Session.Storages.Storages.SelectMany(s => s.Items).Select(i => i.Entity));
        Assert.Contains(bagLight.dm_id, alice.Session.Storages.Storages.SelectMany(s => s.Items).Select(i => i.Entity));

        // the tank in the active hand: its action button first, then (as for every item) Use in hand and Examine
        List<RadialEntry> hand = Wheel(w, alice, handTank, activeHand: true);
        Assert.Equal("Set Internals", hand[0].Label);
        Assert.Equal(RadialKind.Extra, hand[0].Kind);
        Assert.Contains(hand, e => e.Kind == RadialKind.UseInHand);
        Assert.Equal(RadialKind.Examine, hand[^1].Kind);
        // picking it: tg's click on the tank's button opens her internals from that tank; it stays in her hand
        Assert.True(alice.Session.ContextPick(InventoryRules.MenuTarget(handTank.dm_id), hand[0].Label));
        w.Run(Second);
        Assert.Same(handTank, In(w, () => her.@internal));
        Assert.Same(handTank, In(w, () => her.get_active_held_item()));
        // picking it again closes them, as the button does
        Assert.True(alice.Session.ContextPick(InventoryRules.MenuTarget(handTank.dm_id), "Set Internals"));
        w.Run(Second);
        Assert.Null(In(w, () => her.@internal));

        // the tank in the suit storage slot: the same action, which switches her internals to that tank; it stays worn there
        List<RadialEntry> suit = Wheel(w, alice, suitTank, activeHand: false);
        RadialEntry setSuit = Assert.Single(suit, e => e.Label == "Set Internals");
        Assert.DoesNotContain(suit, e => e.Kind == RadialKind.UseInHand);
        Assert.True(alice.Session.ContextPick(InventoryRules.MenuTarget(suitTank.dm_id), setSuit.Label));
        w.Run(Second);
        Assert.Same(suitTank, In(w, () => her.@internal));
        Assert.Same(suitTank, In(w, () => her.s_store));

        // the flashlight in her pocket: "Toggle Light", which lights it there
        Assert.False(In(w, () => Dm.truthy(pocketLight.light_on)));
        List<RadialEntry> pocket = Wheel(w, alice, pocketLight, activeHand: false);
        RadialEntry toggle = Assert.Single(pocket, e => e.Label == "Toggle Light");
        Assert.True(alice.Session.ContextPick(InventoryRules.MenuTarget(pocketLight.dm_id), toggle.Label));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(pocketLight.light_on)));
        Assert.Same(pocketLight, In(w, () => her.l_store));

        // the tank and the flashlight in her backpack: tg grants no action there, so no action entry; Examine prints tg's examine box; nothing moves
        foreach (obj_item stored in new obj_item[] { bagTank, bagLight })
        {
            List<RadialEntry> inBag = Wheel(w, alice, stored, activeHand: false);
            Assert.DoesNotContain(inBag, e => e.Kind is RadialKind.Extra or RadialKind.UseInHand);
            RadialEntry examine = inBag[^1];
            Assert.Equal(RadialKind.Examine, examine.Kind);
            chat.Clear();
            Assert.True(alice.Session.Click(InventoryRules.MenuTarget(stored.dm_id), examine.Button, examine.Modifiers));
            w.Run(Second);
            string box = chat.Select(t => t.Html).LastOrDefault(h => h.Contains("boxed_message", StringComparison.Ordinal));
            Assert.True(box != null && box.Contains(In(w, () => stored.name), StringComparison.OrdinalIgnoreCase), "no examine box: " + string.Join(" | ", chat.Select(t => t.Html)));
            Assert.Same(bag, In(w, () => stored.loc));
        }
        Assert.False(In(w, () => Dm.truthy(bagLight.light_on)));

        // Examine on the held tank too: the box, the tank still in her hand
        chat.Clear();
        Assert.True(alice.Session.Click(InventoryRules.MenuTarget(handTank.dm_id), ClickButton.Left, ClickModifiers.Shift));
        w.Run(Second);
        Assert.Contains(chat, t => t.Html.Contains("boxed_message", StringComparison.Ordinal));
        Assert.Same(handTank, In(w, () => her.get_active_held_item()));
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void An_action_name_picked_on_an_item_that_does_not_offer_it_does_nothing()
    {
        // the server runs only an action button the item's wheel offers now: a forged name, another's tank, a stored tank (no button) do nothing
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7612, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var her = (mob_living_carbon_human)w.MobOf(alice);
        var him = (mob_living_carbon_human)w.MobOf(bob);
        obj_item_tank hers = null, his = null, stored = null;
        In(w, () =>
        {
            foreach (mob_living_carbon_human who in new[] { her, him })
            {
                foreach (obj_item item in new[] { who.back, who.wear_mask, who.get_item_for_held_index(1) as obj_item, who.get_item_for_held_index(2) as obj_item })
                    if (item != null)
                        qdel(item);
                Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_mask_breath>(get_turf(who)), ITEM_SLOT_MASK))));
                var tank = Dm.@new<obj_item_tank_internals_oxygen>(get_turf(who));
                Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hand(tank, who.active_hand_index))));
                if (who == her)
                {
                    hers = tank;
                    var bag = Dm.@new<obj_item_storage_backpack>(get_turf(who));
                    Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(bag, ITEM_SLOT_BACK))));
                    stored = Dm.@new<obj_item_tank_internals_oxygen>(bag);
                }
                else
                    his = tank;
            }
            Waiting.NoSleep(him.forceMove(get_step(her, EAST)));
            return 0;
        });
        w.Run(2 * Second);
        int runtimes = w.World.RuntimeErrorCount;
        Assert.True(alice.Session.ContextPick(InventoryRules.MenuTarget(hers.dm_id), "Toggle Light")); // not the tank's action
        Assert.True(alice.Session.ContextPick(new ClickTarget(TargetKind.Entity, his.dm_id, 0, 0, 0, 16, 16), "Set Internals")); // Bob's
        Assert.True(alice.Session.ContextPick(InventoryRules.MenuTarget(his.dm_id), "Set Internals"));
        Assert.True(alice.Session.ContextPick(InventoryRules.MenuTarget(stored.dm_id), "Set Internals")); // in her bag: no button
        w.Run(2 * Second);
        Assert.Null(In(w, () => her.@internal));
        Assert.Null(In(w, () => him.@internal));
        Assert.Same(hers, In(w, () => her.get_active_held_item()));
        Assert.Same(his, In(w, () => him.get_active_held_item()));
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }
}
