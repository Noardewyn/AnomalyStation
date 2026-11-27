using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.UiOverhaul;

/// <summary>
/// ui-overhaul.md section 12, slice 4B-1 (DV692): the Tab inventory's carried storages (<see cref="CarriedStorages.List"/>) and the moves of a client
/// (<see cref="CarriedStorages.Move"/>), in a booted toy room with one joined player, Alice, in a jumpsuit. Every move is tg's own: a storage's
/// attempt_insert and remove_single, a slot's mob_can_equip and equip_to_slot, put_in_hand, dropItemToGround; after each the item is in exactly one
/// place and tg's own refusal is shown as tg words it.
/// </summary>
public class CarriedStoragesTests
{
    private const int Second = 20;

    private sealed class ChatCapture : IMessageSink
    {
        public List<string> Lines { get; } = new();
        public void Chat(object target, DmList message) => Lines.Add(Dm.str(message["html"] ?? message["text"]));
        public void Output(object target, string text) => Lines.Add(text);
        public void Balloon(object viewer, object atom, string text) => Lines.Add("balloon:" + text);
        public bool Saw(string text) => Lines.Any(l => l != null && l.Contains(text, StringComparison.Ordinal));
    }

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Alice, ChatCapture Chat)
    {
        public obj_item Backpack, Belt, PocketBox, HeldBox;
    }

    private static Rig Build(ulong seed, bool backpack = true, bool belt = true, bool pocketBox = false, bool heldBox = false)
    {
        var r = new AudioRoom(9, 7, seed: seed);
        mob_living_carbon_human alice = r.Join(4, 3, "Alice");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        var g = new Rig(r, alice, chat);
        r.In(() =>
        {
            foreach (obj_item worn in new[] { alice.back, alice.belt, alice.l_store, alice.r_store, alice.wear_id, alice.s_store, alice.wear_suit,
                alice.get_item_for_held_index(1) as obj_item, alice.get_item_for_held_index(2) as obj_item })
                if (worn != null)
                    qdel(worn);
            if (alice.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(alice.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(alice), ITEM_SLOT_ICLOTHING))));
            if (backpack)
                g.Backpack = WearNow(alice, Dm.@new<obj_item_storage_backpack>(get_turf(alice)), ITEM_SLOT_BACK);
            if (belt)
                g.Belt = WearNow(alice, Dm.@new<obj_item_storage_belt_utility>(get_turf(alice)), ITEM_SLOT_BELT);
            if (pocketBox)
            {
                // tg's box is normal-sized and does not fit a pocket by hand (mob_can_equip); it is put there as an outfit's equip_to_slot would
                g.PocketBox = Dm.@new<obj_item_storage_box>(get_turf(alice));
                Waiting.NoSleep(alice.equip_to_slot(g.PocketBox, ITEM_SLOT_RPOCKET));
                Assert.Same(g.PocketBox, alice.r_store);
            }
            if (heldBox)
            {
                g.HeldBox = Dm.@new<obj_item_storage_box>(get_turf(alice));
                Assert.True(Dm.truthy(Waiting.NoSleep(alice.put_in_hand(g.HeldBox, Inactive(alice))))); // the active hand stays empty for taking worn things off
            }
        });
        r.Step(1);
        chat.Lines.Clear();
        return g;
    }

    private static int Active(mob_living_carbon_human who) => (int)Dm.num(who.active_hand_index);

    private static int Inactive(mob_living_carbon_human who) => 3 - Active(who);

    private static obj_item WearNow(mob_living_carbon_human who, obj_item item, int slot)
    {
        Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(item, slot))), "worn in " + slot);
        return item;
    }

    private static T Make<T>(Rig g) where T : obj_item => g.R.In(() => Dm.@new<T>(get_turf(g.Alice)));

    private static void Store(Rig g, obj_item storage, obj_item item)
        => g.R.In(() => Assert.True(Dm.truthy(storage.atom_storage.attempt_insert(item, null, messages: FALSE)), "stored in " + storage.name));

    private static void Move(Rig g, obj_item item, MoveTarget to, obj_item storage = null, int index = 0, int ticks = 4)
    {
        g.R.Command(() => CarriedStorages.Move(g.Alice.client, item.dm_id, to, storage?.dm_id ?? 0, index));
        g.R.Step(ticks);
    }

    private static int Refusals(Rig g) => g.R.In(() => g.Alice.dm_carried?.Refusals ?? 0);

    // Every place the item is found in: a hand, a worn slot, a carried storage's contents, the floor; for "exactly one place".
    private static List<string> Places(Rig g, obj_item item) => g.R.In(() =>
    {
        var places = new List<string>();
        if (QDELETED(item))
            return places;
        for (int i = 1; i <= Dm.length(g.Alice.held_items); i++)
            if (g.Alice.get_item_for_held_index(i) == item)
                places.Add("hand " + i);
        foreach (int slot in CarriedStorages.WornSlots)
            if (g.Alice.get_item_by_slot(slot) == item)
                places.Add("slot " + slot);
        foreach (obj_item s in new[] { g.Backpack, g.Belt, g.PocketBox, g.HeldBox })
            if (s != null && !QDELETED(s) && item.loc == s.atom_storage.real_location)
                places.Add("in " + s.dm_id);
        if (item.loc is turf)
            places.Add("floor");
        return places;
    });

    private static string Place(Rig g, obj_item item)
    {
        List<string> places = Places(g, item);
        Assert.True(places.Count == 1, "in exactly one place: " + string.Join(", ", places));
        return places[0];
    }

    private static string In(obj_item storage) => "in " + storage.dm_id;

    // ---- the list ----

    [Fact]
    public void TheCarriedStoragesAreListedInTheTabInventorysOrderWithTheirItemsInTgsOrder()
    {
        var g = Build(9701, pocketBox: true, heldBox: true);
        obj_item wallet = Make<obj_item_storage_wallet>(g);
        g.R.In(() => Waiting.NoSleep(g.Alice.equip_to_slot(wallet, ITEM_SLOT_ID)));
        obj_item a = Make<obj_item_screwdriver>(g), b = Make<obj_item_wrench>(g);
        Store(g, g.Backpack, a);
        Store(g, g.Backpack, b);
        List<CarriedStorages.Carried> list = g.R.In(() => CarriedStorages.List(g.Alice));
        Assert.Equal(new[] { CarriedPlace.Back, CarriedPlace.Belt, CarriedPlace.RightPocket, CarriedPlace.Id, CarriedPlace.Hand }, list.Select(c => c.Place));
        Assert.Equal(new obj_item[] { g.Backpack, g.Belt, g.PocketBox, wallet, g.HeldBox }, list.Select(c => c.Item));
        Assert.Equal(Inactive(g.Alice), list[4].Hand);
        Assert.Equal(new[] { a, b }, g.R.In(() => CarriedStorages.ShownItems(list[0].Storage)));
        // a locked storage shows nothing
        obj_item lockbox = Make<obj_item_storage_lockbox>(g);
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(lockbox, Active(g.Alice))))));
        datum_storage locked = g.R.In(() => CarriedStorages.List(g.Alice).Single(c => c.Item == lockbox).Storage);
        Assert.False(CarriedStorages.ShowsItems(locked));
        Assert.Empty(g.R.In(() => CarriedStorages.ShownItems(locked)));
        g.R.AssertNoRuntimes();
    }

    // ---- every source kind to every target kind ----

    public static IEnumerable<object[]> Pairs()
    {
        foreach (string source in new[] { "hand", "slot", "back", "belt", "pocketbox", "heldbox" })
            foreach (string target in new[] { "hand", "slot", "back", "belt", "pocketbox", "heldbox", "floor" })
                yield return new object[] { source, target };
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void AScrewdriverMovesFromEverySourceKindToEveryTargetKind(string source, string target)
    {
        bool pocket = source == "pocketbox" || target == "pocketbox", held = source == "heldbox" || target == "heldbox";
        var g = Build(9710, pocketBox: pocket, heldBox: held);
        obj_item tool = Make<obj_item_screwdriver>(g);
        obj_item StorageOf(string kind) => kind switch { "back" => g.Backpack, "belt" => g.Belt, "pocketbox" => g.PocketBox, "heldbox" => g.HeldBox, _ => null };
        g.R.In(() =>
        {
            switch (source)
            {
                case "hand":
                    Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(tool, Active(g.Alice)))));
                    break;
                case "slot":
                    Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.equip_to_slot_if_possible(tool, ITEM_SLOT_LPOCKET))));
                    break;
            }
        });
        if (StorageOf(source) is obj_item from)
            Store(g, from, tool);
        string before = Place(g, tool);
        int hand = g.R.In(() => Enumerable.Range(1, 2).FirstOrDefault(i => g.Alice.get_item_for_held_index(i) == null));
        int slot = source == "slot" ? ITEM_SLOT_RPOCKET : ITEM_SLOT_LPOCKET;
        string expected;
        switch (target)
        {
            case "hand":
                Move(g, tool, MoveTarget.Hand, index: hand);
                expected = "hand " + hand;
                break;
            case "slot":
                Move(g, tool, MoveTarget.Slot, index: slot);
                expected = "slot " + slot;
                break;
            case "floor":
                Move(g, tool, MoveTarget.Floor);
                expected = "floor";
                break;
            default:
                Move(g, tool, MoveTarget.Storage, StorageOf(target));
                expected = In(StorageOf(target));
                break;
        }
        Assert.True(expected == Place(g, tool), source + " to " + target + ": was " + before + ", is " + Place(g, tool));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void EachWornSlotTakesWhatTgLetsOnItAndNoMore()
    {
        var g = Build(9711);
        // shoes, a jumpsuit, gloves, a hat, glasses, a mask, a headset, a tie from the backpack onto their slots
        (obj_item item, int slot)[] wear = g.R.In(() => new (obj_item, int)[]
        {
            (Dm.@new<obj_item_clothing_shoes_sneakers_black>(get_turf(g.Alice)), ITEM_SLOT_FEET),
            (Dm.@new<obj_item_clothing_gloves_color_black>(get_turf(g.Alice)), ITEM_SLOT_GLOVES),
            (Dm.@new<obj_item_clothing_head_soft>(get_turf(g.Alice)), ITEM_SLOT_HEAD),
            (Dm.@new<obj_item_clothing_glasses_meson>(get_turf(g.Alice)), ITEM_SLOT_EYES),
            (Dm.@new<obj_item_clothing_mask_breath>(get_turf(g.Alice)), ITEM_SLOT_MASK),
            (Dm.@new<obj_item_radio_headset>(get_turf(g.Alice)), ITEM_SLOT_EARS),
            (Dm.@new<obj_item_clothing_neck_tie>(get_turf(g.Alice)), ITEM_SLOT_NECK),
        });
        foreach ((obj_item item, int slot) in wear)
        {
            Store(g, g.Backpack, item);
            Move(g, item, MoveTarget.Slot, index: slot);
            Assert.True(Place(g, item) == "slot " + slot, item.name + " on " + slot);
        }
        // a screwdriver on the head, a suit in the back slot that is taken: tg refuses, the item stays where it was
        obj_item tool = Make<obj_item_screwdriver>(g);
        Store(g, g.Backpack, tool);
        Move(g, tool, MoveTarget.Slot, index: ITEM_SLOT_HEAD);
        Assert.Equal(In(g.Backpack), Place(g, tool));
        Assert.True(g.Chat.Saw("You are unable to equip that!"));
        Assert.Equal(1, Refusals(g));
        // the jumpsuit off its slot and back on from a hand (a held jumpsuit onto the uniform slot)
        obj_item suit = g.R.In(() => g.Alice.w_uniform);
        g.R.In(() => { foreach (int s in new[] { ITEM_SLOT_ID, ITEM_SLOT_BELT, ITEM_SLOT_LPOCKET, ITEM_SLOT_RPOCKET }) if (g.Alice.get_item_by_slot(s) is obj_item o) qdel(o); });
        g.R.Step(1);
        Move(g, suit, MoveTarget.Hand, index: 2);
        Assert.Equal("hand 2", Place(g, suit));
        Move(g, suit, MoveTarget.Slot, index: ITEM_SLOT_ICLOTHING);
        Assert.Equal("slot " + ITEM_SLOT_ICLOTHING, Place(g, suit));
        g.R.AssertNoRuntimes();
    }

    // ---- what tg refuses ----

    [Fact]
    public void AFullTargetAndAnItemTooBigAreRefusedWithTgsMessages()
    {
        var g = Build(9712);
        g.R.In(() => qdel(g.Belt));
        g.Belt = g.R.In(() => WearNow(g.Alice, Dm.@new<obj_item_storage_belt_utility_full>(get_turf(g.Alice)), ITEM_SLOT_BELT));
        obj_item tool = Make<obj_item_screwdriver>(g);
        g.R.In(() => Waiting.NoSleep(g.Alice.put_in_hand(tool, 2)));
        Move(g, tool, MoveTarget.Storage, g.Belt); // seven tools: no room
        Assert.Equal("hand 2", Place(g, tool));
        Assert.True(g.Chat.Saw("balloon:no room!"));
        obj_item toolbox = Make<obj_item_storage_toolbox_mechanical>(g);
        g.R.In(() => Waiting.NoSleep(g.Alice.put_in_hand(toolbox, 1)));
        Move(g, toolbox, MoveTarget.Storage, g.Backpack); // bulky into a backpack
        Assert.True(g.R.In(() => g.Alice.get_item_for_held_index(1) == toolbox));
        Assert.True(g.Chat.Saw("balloon:too big!"));
        Assert.Equal(2, Refusals(g));
        Assert.Equal(toolbox.dm_id, g.R.In(() => g.Alice.dm_carried.RefusedItem));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AStorageIntoItselfOrIntoAStorageInsideItIsRefused()
    {
        var g = Build(9713, heldBox: true);
        Move(g, g.HeldBox, MoveTarget.Storage, g.HeldBox);
        Assert.True(g.R.In(() => g.Alice.get_item_for_held_index(Inactive(g.Alice)) == g.HeldBox));
        Assert.Equal(1, Refusals(g));
        // a box inside the held box is no carried storage: a move into it is nothing at all (not even a refusal)
        obj_item inner = Make<obj_item_storage_box_matches>(g);
        Store(g, g.HeldBox, inner);
        Move(g, g.HeldBox, MoveTarget.Storage, inner);
        Assert.True(g.R.In(() => g.Alice.get_item_for_held_index(Inactive(g.Alice)) == g.HeldBox && inner.loc == g.HeldBox));
        Assert.Equal(1, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ANoDropItemStaysOnTheBody()
    {
        var g = Build(9714);
        obj_item tool = Make<obj_item_screwdriver>(g);
        obj_item gloves = Make<obj_item_clothing_gloves_color_black>(g);
        g.R.In(() =>
        {
            Waiting.NoSleep(g.Alice.put_in_hand(tool, Inactive(g.Alice))); // the active hand empty: the gloves' take-off is tried by tg's click
            WearNow(g.Alice, gloves, ITEM_SLOT_GLOVES);
            ADD_TRAIT(tool, TRAIT_NODROP, "test");
            ADD_TRAIT(gloves, TRAIT_NODROP, "test");
        });
        Move(g, tool, MoveTarget.Storage, g.Backpack);
        Assert.True(g.Chat.Saw("balloon:stuck on your hand!"));
        Move(g, tool, MoveTarget.Floor);
        Move(g, tool, MoveTarget.Hand, index: Active(g.Alice));
        Assert.Equal("hand " + Inactive(g.Alice), Place(g, tool));
        Move(g, gloves, MoveTarget.Storage, g.Backpack);
        Move(g, gloves, MoveTarget.Hand, index: Active(g.Alice));
        Move(g, gloves, MoveTarget.Floor);
        Assert.Equal("slot " + ITEM_SLOT_GLOVES, Place(g, gloves));
        Assert.Equal(6, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ALockedStorageGivesNothingAndTakesNothing()
    {
        var g = Build(9715);
        obj_item lockbox = Make<obj_item_storage_lockbox>(g);
        obj_item inside = Make<obj_item_screwdriver>(g);
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(lockbox.atom_storage.attempt_insert(inside, null, force: STORAGE_FULLY_LOCKED, messages: FALSE)));
            Waiting.NoSleep(g.Alice.put_in_hand(lockbox, 1));
        });
        // as source: its items are not the client's to name, so nothing happens and nothing is reported
        Move(g, inside, MoveTarget.Hand, index: 2);
        Assert.True(g.R.In(() => inside.loc == lockbox));
        Assert.Equal(0, Refusals(g));
        // as target: tg's "closed!"
        obj_item tool = Make<obj_item_wrench>(g);
        g.R.In(() => Waiting.NoSleep(g.Alice.put_in_hand(tool, 2)));
        Move(g, tool, MoveTarget.Storage, lockbox);
        Assert.Equal("hand 2", Place(g, tool));
        Assert.True(g.Chat.Saw("balloon:closed!"));
        Assert.Equal(1, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AStackIntoAStorageHoldingTheSameStackMergesIntoItAsInTg()
    {
        // tg's stack merges with a stack of its kind wherever it moves to (stack.dm Moved, merge_with_loc), a storage's contents included
        var g = Build(9716);
        obj_item_stack first = g.R.In(() => Dm.@new<obj_item_stack_cable_coil>(g.Backpack.atom_storage.real_location, 5));
        obj_item_stack second = g.R.In(() => Dm.@new<obj_item_stack_cable_coil>(g.Alice, 7));
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(second, 2)))));
        Assert.Equal(5f, g.R.In(() => Dm.num(first.amount)));
        Move(g, second, MoveTarget.Storage, g.Backpack);
        Assert.Equal(In(g.Backpack), Place(g, first));
        Assert.True(g.R.In(() => QDELETED(second)));
        Assert.Equal(12f, g.R.In(() => Dm.each<obj_item_stack>(g.Backpack.atom_storage.real_location.contents).Sum(s => Dm.num(s.amount))));
        Assert.Equal(0, Refusals(g));
        // onto the floor where a coil lies: from a hand and from the backpack, each merges there and is no refusal
        obj_item_stack lying = g.R.In(() => Dm.@new<obj_item_stack_cable_coil>(get_turf(g.Alice), 3));
        obj_item_stack held = g.R.In(() => Dm.@new<obj_item_stack_cable_coil>(g.Alice, 2));
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(held, 2)))));
        Move(g, held, MoveTarget.Floor);
        Move(g, first, MoveTarget.Floor);
        Assert.True(g.R.In(() => QDELETED(held) && QDELETED(first)));
        Assert.Equal(17f, g.R.In(() => Dm.num(lying.amount)));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadStunnedOrCuffedBodyMovesNothingAndABuckledOneDoes()
    {
        foreach (string how in new[] { "dead", "stunned", "cuffed", "buckled" })
        {
            var g = Build(9717);
            obj_item tool = Make<obj_item_screwdriver>(g);
            Store(g, g.Backpack, tool);
            g.R.In(() =>
            {
                switch (how)
                {
                    case "cuffed":
                        Waiting.NoSleep(g.Alice.set_handcuffed(Dm.@new<obj_item_restraints_handcuffs>(g.Alice)));
                        break;
                    case "stunned":
                        Waiting.NoSleep(g.Alice.Paralyze(30 * SECONDS));
                        break;
                    case "buckled":
                        var chair = Dm.@new<obj_structure_chair>(get_turf(g.Alice));
                        Assert.True(Dm.truthy(Waiting.NoSleep(chair.buckle_mob(g.Alice, force: TRUE))));
                        break;
                    default:
                        Waiting.Detach(g.Alice.death());
                        break;
                }
            });
            g.R.Step(2);
            Move(g, tool, MoveTarget.Storage, g.Belt);
            if (how == "buckled")
            {
                Assert.True(In(g.Belt) == Place(g, tool), how);
                Assert.True(Refusals(g) == 0, how);
            }
            else
            {
                Assert.True(In(g.Backpack) == Place(g, tool), how);
                Assert.True(Refusals(g) == 1, how);
            }
        }
    }

    [Fact]
    public void AnIncapacitatedPlayerStillDropsAsTgsDropKeyLetsHerButTakesNothingOutOfABag()
    {
        // tg's drop key has no check of its own (keybinding/mob.dm:90-102). Cuffs and stuns block the hands, and tg drops what they hold then
        // (living.dm:2743-2746), so the case left is an incapacitated body whose hands are free: held in an aggressive grab (INCAPABLE_GRAB,
        // set here as tg's update_incapacitated sets it, in the command that moves)
        var g = Build(9738);
        obj_item held = Make<obj_item_wrench>(g), stored = Make<obj_item_screwdriver>(g);
        Store(g, g.Backpack, stored);
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(held, Active(g.Alice))))));
        void Grabbed(obj_item item)
        {
            g.R.Command(() =>
            {
                float before = g.Alice.incapacitated;
                g.Alice.incapacitated = INCAPABLE_GRAB;
                CarriedStorages.Move(g.Alice.client, item.dm_id, MoveTarget.Floor, 0, 0);
                g.Alice.incapacitated = before;
            });
            g.R.Step(4);
        }
        Grabbed(held);
        Assert.Equal("floor", Place(g, held));
        Assert.Equal(0, Refusals(g));
        // a stored item must be taken out first, and tg's click on it while incapacitated (but for restraints and stasis) does nothing (click.dm:115)
        Grabbed(stored);
        Assert.Equal(In(g.Backpack), Place(g, stored));
        Assert.Equal(1, Refusals(g));
        // cuffed: the take-out's click with blocked hands does nothing either (click.dm:126, other_mobs.dm:17-18)
        g.R.In(() => Waiting.NoSleep(g.Alice.set_handcuffed(Dm.@new<obj_item_restraints_handcuffs>(g.Alice))));
        g.R.Step(2);
        Move(g, stored, MoveTarget.Floor);
        Assert.Equal(In(g.Backpack), Place(g, stored));
        Assert.Equal(2, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    // ---- interleaving ----

    [Fact]
    public void DuringTheEquipDelayTheItemStaysInTheBagAndASecondMoveOfItIsRefused()
    {
        var g = Build(9718);
        obj_item gauntlets = Make<obj_item_clothing_gloves_cargo_gauntlet>(g);
        Store(g, g.Backpack, gauntlets);
        Move(g, gauntlets, MoveTarget.Slot, index: ITEM_SLOT_GLOVES, ticks: 2); // tg's 3 s equip delay, the gauntlets still in the bag
        Assert.True(g.Chat.Saw("You start putting on"));
        Assert.Equal(In(g.Backpack), Place(g, gauntlets));
        Move(g, gauntlets, MoveTarget.Floor, ticks: 2); // the same item again: refused, nothing moves
        Assert.Equal(In(g.Backpack), Place(g, gauntlets));
        Assert.Equal(1, Refusals(g));
        g.R.Step(4 * Second);
        Assert.Equal("slot " + ITEM_SLOT_GLOVES, Place(g, gauntlets));
        Assert.Equal(1, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ASlotFilledABodyStunnedOrTheBagDroppedDuringTheDelayPutsNothingOn()
    {
        foreach (string how in new[] { "filled", "stunned", "bag dropped" })
        {
            var g = Build(9719);
            obj_item gauntlets = Make<obj_item_clothing_gloves_cargo_gauntlet>(g);
            Store(g, g.Backpack, gauntlets);
            Move(g, gauntlets, MoveTarget.Slot, index: ITEM_SLOT_GLOVES, ticks: 2);
            obj_item other = null;
            g.R.In(() =>
            {
                switch (how)
                {
                    case "filled": // someone puts gloves on her meanwhile
                        other = Dm.@new<obj_item_clothing_gloves_color_black>(get_turf(g.Alice));
                        Waiting.NoSleep(g.Alice.equip_to_slot(other, ITEM_SLOT_GLOVES));
                        break;
                    case "stunned": // tg's do_after of the delay breaks
                        Waiting.NoSleep(g.Alice.Paralyze(30 * SECONDS));
                        break;
                    default: // the bag comes off meanwhile, the gauntlets in it: nothing is put on
                        Waiting.NoSleep(g.Alice.dropItemToGround(g.Backpack, force: TRUE));
                        break;
                }
            });
            g.R.Step(4 * Second);
            Assert.True(g.R.In(() => gauntlets.loc == g.Backpack), how + ": still in the bag");
            Assert.True(g.R.In(() => g.Alice.gloves == other), how);
            Assert.True(Refusals(g) == 1, how);
            g.R.AssertNoRuntimes();
        }
    }

    [Fact]
    public void TwoMovesOfOneItemInOneTickLeaveItInOnePlace()
    {
        var g = Build(9720);
        obj_item tool = Make<obj_item_screwdriver>(g);
        g.R.In(() => Waiting.NoSleep(g.Alice.put_in_hand(tool, 2)));
        g.R.Command(() =>
        {
            CarriedStorages.Move(g.Alice.client, tool.dm_id, MoveTarget.Storage, g.Backpack.dm_id, 0);
            CarriedStorages.Move(g.Alice.client, tool.dm_id, MoveTarget.Storage, g.Belt.dm_id, 0);
            CarriedStorages.Move(g.Alice.client, tool.dm_id, MoveTarget.Floor, 0, 0);
        });
        g.R.Step(4);
        string place = Place(g, tool); // each move was done in turn from where the last one left it
        Assert.Equal("floor", place);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AWishedCellOrdersTheStorageAsTgsDragOntoAnItemDoes()
    {
        var g = Build(9721);
        obj_item a = Make<obj_item_screwdriver>(g), b = Make<obj_item_wrench>(g), c = Make<obj_item_crowbar>(g), d = Make<obj_item_wirecutters>(g);
        Store(g, g.Backpack, a);
        Store(g, g.Backpack, b);
        Store(g, g.Backpack, c);
        g.R.In(() => Waiting.NoSleep(g.Alice.put_in_hand(d, 2)));
        Move(g, d, MoveTarget.Storage, g.Backpack, index: 1); // before the first
        List<obj_item> Order() => g.R.In(() => Dm.each<obj_item>(g.Backpack.atom_storage.real_location.contents).ToList());
        Assert.Equal(new[] { d, a, b, c }, Order());
        // within the storage, onto the last from before it: tg reads the drop index before taking the item out, so it lands after it
        // (storage.dm mousedrop_receive: to_move is empty when the list got shorter than the index)
        Move(g, a, MoveTarget.Storage, g.Backpack, index: 4);
        Assert.Equal(new[] { d, b, c, a }, Order());
        Move(g, a, MoveTarget.Storage, g.Backpack, index: 2); // onto the second from behind it: before it
        Assert.Equal(new[] { d, a, b, c }, Order());
        Move(g, d, MoveTarget.Storage, g.Backpack, index: 0); // no cell: last (tg's drop onto an empty cell)
        Assert.Equal(new[] { a, b, c, d }, Order());
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    // ---- the Tab notice ----

    [Fact]
    public void TheTabNoticePlaysTgsRustleOnceASecondForEveryoneNearAndOpensNoScreen()
    {
        var g = Build(9722, heldBox: true);
        mob_living_carbon_human bob = g.R.Join(5, 3, "Bob");
        g.R.Step(2);
        g.R.In(() => Waiting.NoSleep(g.HeldBox.atom_storage.open_storage(g.Alice))); // another container open in tg's screen
        g.R.Step(1);
        datum_storage open = g.R.In(() => g.Alice.active_storage);
        Assert.Same(g.HeldBox.atom_storage, open);
        int Rustles(mob who) => g.R.Sounds.Heard.Count(h => ReferenceEquals(h.Target, who) && h.File != null && h.File.Contains("rustle", StringComparison.Ordinal));
        int aliceBefore = Rustles(g.Alice), bobBefore = Rustles(bob);
        for (int i = 0; i < 5; i++)
        {
            g.R.Command(() => CarriedStorages.Opened(g.Alice.client));
            g.R.Step(2);
        }
        Assert.Equal(aliceBefore + 1, Rustles(g.Alice));
        Assert.Equal(bobBefore + 1, Rustles(bob)); // tg's playsound: those near hear it
        Assert.Same(open, g.R.In(() => g.Alice.active_storage)); // tg's screen is not opened for the backpack
        g.R.Step(Second);
        g.R.Command(() => CarriedStorages.Opened(g.Alice.client));
        g.R.Step(2);
        Assert.Equal(aliceBefore + 2, Rustles(g.Alice)); // a second later, again
        // stunned: tg's open_storage refuses (can_perform_action), no rustle
        g.R.In(() => Waiting.NoSleep(g.Alice.Paralyze(30 * SECONDS)));
        g.R.Step(Second + 2);
        g.R.Command(() => CarriedStorages.Opened(g.Alice.client));
        g.R.Step(2);
        Assert.Equal(aliceBefore + 2, Rustles(g.Alice));
        g.R.AssertNoRuntimes();
    }

    // ---- tg's special cases go through (review round 1) ----

    // A trash bag in the left pocket (tg's trash bag does not fit a pocket by hand; put there as an outfit would), holding a screwdriver.
    private static (obj_item Bag, obj_item Tool) PocketTrashBag(Rig g)
    {
        obj_item bag = Make<obj_item_storage_bag_trash>(g), tool = Make<obj_item_screwdriver>(g);
        g.R.In(() =>
        {
            Waiting.NoSleep(g.Alice.equip_to_slot(bag, ITEM_SLOT_LPOCKET));
            Assert.True(Dm.truthy(bag.atom_storage.attempt_insert(tool, null, messages: FALSE)));
        });
        g.R.Step(1);
        g.Chat.Lines.Clear();
        return (bag, tool);
    }

    [Fact]
    public void AnItemLeavesItsStorageByThatStoragesOwnRemoval()
    {
        // a trash bag's own remove_single: tg's 1.5 s search (bags.dm:18-26) before the screwdriver comes out
        var g = Build(9730);
        (obj_item bag, obj_item tool) = PocketTrashBag(g);
        Move(g, tool, MoveTarget.Storage, g.Backpack, ticks: 2);
        Assert.True(g.Chat.Saw("fishing around inside") || g.Chat.Saw("digging around in"), string.Join(" | ", g.Chat.Lines));
        Assert.True(g.R.In(() => tool.loc == bag), "still in the trash bag while she digs");
        g.R.Step(2 * Second);
        Assert.Equal(In(g.Backpack), Place(g, tool));
        // an evidence bag's own removal signal resets its size (evidence.dm on_remove, from attempt_remove's COMSIG_STORAGE_REMOVED_ITEM)
        obj_item evidence = Make<obj_item_evidencebag>(g), wrench = Make<obj_item_wrench>(g);
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(evidence, Inactive(g.Alice)))));
            Assert.True(Dm.truthy(evidence.atom_storage.attempt_insert(wrench, g.Alice, messages: FALSE)));
        });
        Assert.Equal(g.R.In(() => Dm.num(wrench.w_class)), g.R.In(() => Dm.num(evidence.w_class)));
        Move(g, wrench, MoveTarget.Storage, g.Backpack);
        Assert.Equal(In(g.Backpack), Place(g, wrench));
        Assert.Equal((float)WEIGHT_CLASS_TINY, g.R.In(() => Dm.num(evidence.w_class)));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AWornItemComesOffByTgsClickWithTheEmptyActiveHand()
    {
        var g = Build(9731);
        // an unwarped tinfoil hat refuses to come off (tinfoilhat.dm:83-87)
        obj_item hat = Make<obj_item_clothing_head_costume_foilhat>(g);
        g.R.In(() => WearNow(g.Alice, hat, ITEM_SLOT_HEAD));
        Move(g, hat, MoveTarget.Storage, g.Backpack);
        Assert.Equal("slot " + ITEM_SLOT_HEAD, Place(g, hat));
        Assert.True(g.Chat.Saw("Why would you want to take this off?"));
        Assert.Equal(1, Refusals(g));
        // with something in the active hand nothing comes off (tg's click would use it on the worn item)
        obj_item glasses = Make<obj_item_clothing_glasses_meson>(g), tool = Make<obj_item_screwdriver>(g);
        g.R.In(() =>
        {
            WearNow(g.Alice, glasses, ITEM_SLOT_EYES);
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(tool, Active(g.Alice)))));
        });
        Move(g, glasses, MoveTarget.Storage, g.Backpack);
        Assert.Equal("slot " + ITEM_SLOT_EYES, Place(g, glasses));
        Assert.Equal(2, Refusals(g));
        // the hand empty: the glasses come off into it and go on into the bag
        Move(g, tool, MoveTarget.Storage, g.Backpack);
        Move(g, glasses, MoveTarget.Storage, g.Backpack);
        Assert.Equal(In(g.Backpack), Place(g, glasses));
        // a target that refuses an item taken off leaves it in the hand, as in tg after taking it off
        obj_item lockbox = Make<obj_item_storage_lockbox>(g), shoes = Make<obj_item_clothing_shoes_sneakers_black>(g);
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(lockbox, Inactive(g.Alice)))));
            WearNow(g.Alice, shoes, ITEM_SLOT_FEET);
        });
        Move(g, shoes, MoveTarget.Storage, lockbox);
        Assert.Equal("hand " + Active(g.Alice), Place(g, shoes));
        Assert.True(g.Chat.Saw("balloon:closed!"));
        Assert.Equal(3, Refusals(g));
        // a worn backpack comes off by tg's drag of it onto the active hand (storage.dm on_mousedrop_onto), its contents with it
        g.R.In(() => qdel(shoes));
        Move(g, g.Backpack, MoveTarget.Hand, index: Active(g.Alice));
        Assert.Equal("hand " + Active(g.Alice), Place(g, g.Backpack));
        Assert.True(g.R.In(() => glasses.loc == g.Backpack));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AHandItemGoesNowhereButToTheFloorWhereTgsDropDeletesIt()
    {
        var g = Build(9732);
        obj_item slapper = g.R.In(() =>
        {
            var s = Dm.@new<obj_item_hand_item_slapper>(g.Alice);
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(s, Active(g.Alice)))));
            return (obj_item)s;
        });
        Move(g, slapper, MoveTarget.Hand, index: Inactive(g.Alice));
        Move(g, slapper, MoveTarget.Storage, g.Backpack);
        Move(g, slapper, MoveTarget.Slot, index: ITEM_SLOT_LPOCKET);
        Assert.Equal("hand " + Active(g.Alice), Place(g, slapper)); // refused, not deleted
        Assert.Equal(3, Refusals(g));
        Move(g, slapper, MoveTarget.Floor);
        Assert.True(g.R.In(() => QDELETED(slapper))); // tg's drop of a DROPDEL item
        Assert.Equal(3, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ATargetThatNoLongerTakesAnItemOutSendsItBackWhereItWas()
    {
        foreach (string branch in new[] { "slot", "storage", "hand", "floor" })
        {
            var g = Build(9733);
            (obj_item bag, obj_item tool) = PocketTrashBag(g);
            if (branch == "floor")
                g.R.In(() => ADD_TRAIT(tool, TRAIT_NODROP, "test")); // tg's drop refuses it once it is out
            switch (branch)
            {
                case "slot":
                    Move(g, tool, MoveTarget.Slot, index: ITEM_SLOT_RPOCKET, ticks: 2);
                    break;
                case "storage":
                    Move(g, tool, MoveTarget.Storage, g.Backpack, ticks: 2);
                    break;
                case "hand":
                    Move(g, tool, MoveTarget.Hand, index: Inactive(g.Alice), ticks: 2);
                    break;
                default:
                    Move(g, tool, MoveTarget.Floor, ticks: 2);
                    break;
            }
            // during the trash bag's search the target stops taking it: the pocket is filled, the backpack dropped, the hand filled
            g.R.In(() =>
            {
                switch (branch)
                {
                    case "slot":
                        Waiting.NoSleep(g.Alice.equip_to_slot(Dm.@new<obj_item_pen>(get_turf(g.Alice)), ITEM_SLOT_RPOCKET));
                        break;
                    case "storage":
                        Waiting.NoSleep(g.Alice.dropItemToGround(g.Backpack, force: TRUE));
                        break;
                    case "hand":
                        Waiting.NoSleep(g.Alice.put_in_hand(Dm.@new<obj_item_wrench>(g.Alice), Inactive(g.Alice)));
                        break;
                }
            });
            g.R.Step(2 * Second);
            if (branch == "floor") // the trash bag refuses a NODROP item too (can_insert): tg's put_in_hands takes it, the last fallback
                Assert.True(g.R.In(() => Dm.truthy(g.Alice.is_holding(tool))), branch + ": in a hand");
            else
                Assert.True(g.R.In(() => tool.loc == bag), branch + ": back in the trash bag");
            Assert.True(Refusals(g) == 1, branch);
            g.R.AssertNoRuntimes();
        }
    }

    [Fact]
    public void AnItemWhoseTgCodeIsNotPortedIsRefusedBeforeItLeavesItsPlace()
    {
        // wheelys' dropped is not translated (DV27): tg's equip_to_slot would refuse them only after they left the bag
        var g = Build(9734);
        obj_item wheelys = Make<obj_item_clothing_shoes_wheelys>(g);
        g.R.In(() => Waiting.NoSleep(wheelys.forceMove(g.Backpack.atom_storage.real_location)));
        Move(g, wheelys, MoveTarget.Slot, index: ITEM_SLOT_FEET);
        Assert.Equal(In(g.Backpack), Place(g, wheelys));
        Move(g, wheelys, MoveTarget.Hand, index: Active(g.Alice));
        Assert.Equal(In(g.Backpack), Place(g, wheelys));
        Assert.Equal(2, Refusals(g));
    }

    [Fact]
    public void TheTabNoticeSearchesTheCarriedStoragesAndAnArmedMousetrapSnapsOnce()
    {
        var g = Build(9735, pocketBox: true);
        obj_item_assembly_mousetrap inBag = Make<obj_item_assembly_mousetrap_armed>(g), inBox = Make<obj_item_assembly_mousetrap_armed>(g);
        g.R.In(() =>
        {
            Waiting.NoSleep(inBag.forceMove(g.Backpack.atom_storage.real_location));
            Waiting.NoSleep(inBox.forceMove(g.PocketBox.atom_storage.real_location));
        });
        g.R.Step(1);
        int Rustles() => g.R.Sounds.Heard.Count(h => ReferenceEquals(h.Target, g.Alice) && h.File != null && h.File.Contains("rustle", StringComparison.Ordinal));
        int Snaps() => g.Chat.Lines.Count(l => l != null && l.Contains("You accidentally trigger", StringComparison.Ordinal));
        int before = Rustles();
        g.R.Command(() => CarriedStorages.Opened(g.Alice.client));
        g.R.Step(2);
        Assert.False(g.R.In(() => Dm.truthy(inBag.armed)), "the backpack's trap snapped");
        Assert.True(g.R.In(() => Dm.truthy(inBox.armed)), "the box is not opened: the snap stunned her (tg's 6 s) and tg's opening checks refuse it");
        Assert.Equal(1, Snaps());
        Assert.Equal(before, Rustles()); // the trap ended the backpack's opening (show_contents returned FALSE): no rustle
        g.R.Step(7 * Second); // past tg's 6 s stun of a snapped hand
        g.R.Command(() => CarriedStorages.Opened(g.Alice.client));
        g.R.Step(2);
        Assert.False(g.R.In(() => Dm.truthy(inBox.armed)), "the pocket box's trap snapped on the next notice");
        Assert.Equal(2, Snaps());
        Assert.Equal(before + 1, Rustles()); // the backpack, its trap spent, opened with its rustle
        g.R.Step(7 * Second);
        g.R.Command(() => CarriedStorages.Opened(g.Alice.client));
        g.R.Step(2);
        Assert.Equal(2, Snaps()); // each trap once
        Assert.Equal(before + 2, Rustles());
        g.R.AssertNoRuntimes();
    }

    // ---- the hotbar and the moves agree ----

    private static void TrackHotbar(Rig g) => g.R.World.AddPhase("hotbar tracking", _ => BeltHotbar.Track(g.Alice));

    private static void Press(Rig g, int cell)
    {
        g.R.Command(() => BeltHotbar.Press(g.Alice.client, cell));
        g.R.Step(4);
    }

    [Fact]
    public void ABeltItemMovedToAHandKeepsItsCellOutAndAnywhereElseFreesIt()
    {
        var g = Build(9736);
        TrackHotbar(g);
        obj_item a = Make<obj_item_screwdriver>(g), b = Make<obj_item_wrench>(g);
        Store(g, g.Belt, a);
        Store(g, g.Belt, b);
        g.R.Step(2);
        Assert.Same(a, g.R.In(() => BeltHotbar.Peek(g.Alice).ItemOf(1)));
        Move(g, a, MoveTarget.Hand, index: Active(g.Alice));
        Assert.Equal(HotbarState.Out, g.R.In(() => BeltHotbar.Peek(g.Alice).StateOf(g.Alice, 1)));
        Press(g, 1); // the same key puts it back
        Assert.True(g.R.In(() => a.loc == g.Belt));
        Assert.Equal(HotbarState.Holding, g.R.In(() => BeltHotbar.Peek(g.Alice).StateOf(g.Alice, 1)));
        Move(g, b, MoveTarget.Storage, g.Backpack); // elsewhere: its cell is free
        Assert.Null(g.R.In(() => BeltHotbar.Peek(g.Alice).ItemOf(2)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheKeysCheckWhatTgsQuickEquipChecksAndAPressCannotRaceAMoveOfTheSameItem()
    {
        var g = Build(9737);
        TrackHotbar(g);
        // blocked hands: tg's smart_equip_targeted checks only incapacitated, and the press goes on into tg's procs, which refuse by themselves:
        // the trait takes MOBILITY_PICKUP away (init_signals.dm:176-179), so attempt_pickup does not take the tool from the belt
        obj_item tool = Make<obj_item_screwdriver>(g);
        Store(g, g.Belt, tool);
        g.R.In(() => ADD_TRAIT(g.Alice, TRAIT_HANDS_BLOCKED, "test"));
        g.R.Step(2);
        Assert.False(g.R.In(() => Dm.truthy(Dm.bitand(g.Alice.mobility_flags, MOBILITY_PICKUP))));
        Press(g, 1);
        Assert.True(g.R.In(() => tool.loc == g.Belt));
        Assert.Equal(1, g.R.In(() => g.Alice.dm_hotbar.Refusals));
        g.R.In(() => REMOVE_TRAIT(g.Alice, TRAIT_HANDS_BLOCKED, "test"));
        // gauntlets in the active hand going on with tg's 3 s delay; a press meanwhile that would store them in an empty belt cell is refused
        obj_item gauntlets = Make<obj_item_clothing_gloves_cargo_gauntlet>(g);
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(gauntlets, Active(g.Alice))))));
        Move(g, gauntlets, MoveTarget.Slot, index: ITEM_SLOT_GLOVES, ticks: 2);
        Press(g, 2);
        Assert.Equal(2, g.R.In(() => g.Alice.dm_hotbar.Refusals));
        g.R.Step(4 * Second);
        Assert.True(g.R.In(() => g.Alice.gloves == gauntlets));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    // ---- review round 2 ----

    [Fact]
    public void AWornItemTgTakesOffByADragComesOffByTheDragWithNothingDeployed()
    {
        // the backpack watertank's click toggles its mister, the defibrillator's takes its paddles: tg takes both off by drag_pickup's drag
        foreach (string kind in new[] { "watertank", "defibrillator" })
        {
            var g = Build(9740, backpack: false);
            obj_item worn = kind == "watertank" ? Make<obj_item_watertank>(g) : Make<obj_item_defibrillator>(g);
            g.R.In(() => WearNow(g.Alice, worn, ITEM_SLOT_BACK));
            obj_item part = g.R.In(() => kind == "watertank" ? ((obj_item_watertank)worn).noz : ((obj_item_defibrillator)worn).paddles);
            Assert.True(g.R.In(() => part != null && part.loc == worn), kind + ": its part is inside it");
            Move(g, worn, MoveTarget.Hand, index: Active(g.Alice));
            Assert.True("hand " + Active(g.Alice) == Place(g, worn), kind + " came off into the active hand");
            Assert.True(g.R.In(() => part.loc == worn), kind + ": nothing deployed");
            Assert.True(Refusals(g) == 0, kind);
            g.R.AssertNoRuntimes();
        }
    }

    [Fact]
    public void AfterTheTakeOffsWaitATargetStorageNoLongerCarriedTakesNothing()
    {
        // heavy gravity: attempt_pickup's 3 s wait (items.dm:556-561) in the click that takes the shoes off; the box is dropped meanwhile
        var g = Build(9741, heldBox: true);
        obj_item shoes = Make<obj_item_clothing_shoes_sneakers_black>(g);
        g.R.In(() =>
        {
            WearNow(g.Alice, shoes, ITEM_SLOT_FEET);
            Waiting.NoSleep(g.Alice.AddElement(typeof(datum_element_forced_gravity), 2));
        });
        g.R.Step(1);
        Move(g, shoes, MoveTarget.Storage, g.HeldBox, ticks: 2);
        Assert.True(g.Chat.Saw("You start picking up"), string.Join(" | ", g.Chat.Lines));
        g.R.In(() => Waiting.NoSleep(g.Alice.dropItemToGround(g.HeldBox, force: TRUE)));
        g.R.Step(4 * Second);
        Assert.Equal("hand " + Active(g.Alice), Place(g, shoes)); // off, in the hand, as in tg after taking them off
        Assert.True(g.R.In(() => shoes.loc != g.HeldBox));
        Assert.Equal(1, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ASlotThatCannotTakeTheItemIsAskedBeforeAnythingMoves()
    {
        var g = Build(9742);
        (obj_item bag, obj_item tool) = PocketTrashBag(g);
        obj_item other = Make<obj_item_wrench>(g);
        Store(g, bag, other); // the screwdriver first, the wrench after it
        obj_item hat = Make<obj_item_clothing_head_soft>(g);
        g.R.In(() => WearNow(g.Alice, hat, ITEM_SLOT_HEAD));
        g.Chat.Lines.Clear();
        Move(g, tool, MoveTarget.Slot, index: ITEM_SLOT_HEAD, ticks: 2);
        Assert.True(g.Chat.Saw("You are unable to equip that!"));
        Assert.False(g.Chat.Saw("fishing around inside") || g.Chat.Saw("digging around in"), "no search of the trash bag");
        Assert.Equal(new[] { tool, other }, g.R.In(() => Dm.each<obj_item>(bag.atom_storage.real_location.contents).ToList())); // in its cell
        Assert.Equal(1, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheTabNoticeAsksTgsOpeningChecksForEachStorage()
    {
        foreach (bool backpack in new[] { true, false })
            foreach (bool cuffed in new[] { true, false })
            {
                var g = Build(9743, backpack: backpack, pocketBox: true);
                obj_item_assembly_mousetrap inBox = Make<obj_item_assembly_mousetrap_armed>(g), inBag = null; // a trap in the backpack too would stun her first (tg's 6 s) and stop the box's opening
                g.R.In(() =>
                {
                    Waiting.NoSleep(inBox.forceMove(g.PocketBox.atom_storage.real_location));
                    if (inBag != null)
                        Waiting.NoSleep(inBag.forceMove(g.Backpack.atom_storage.real_location));
                    if (cuffed)
                        Waiting.NoSleep(g.Alice.set_handcuffed(Dm.@new<obj_item_restraints_handcuffs>(g.Alice)));
                });
                g.R.Step(2);
                g.R.Command(() => CarriedStorages.Opened(g.Alice.client));
                g.R.Step(2);
                string how = (backpack ? "with" : "without") + " a backpack, " + (cuffed ? "cuffed" : "free");
                // cuffed: tg's opening refuses (MOBILITY_STORAGE, storage.dm:975), so nothing is searched and no trap snaps
                Assert.True(g.R.In(() => Dm.truthy(inBox.armed)) == cuffed, how + ": the pocket box's trap");
                if (inBag != null)
                    Assert.True(g.R.In(() => Dm.truthy(inBag.armed)) == cuffed, how + ": the backpack's trap");
                g.R.AssertNoRuntimes();
            }
    }

    [Fact]
    public void AStoredItemStaysInItsStorageThroughTheEquipDelayAndAGibLeavesItThere()
    {
        var g = Build(9744);
        obj_item gauntlets = Make<obj_item_clothing_gloves_cargo_gauntlet>(g);
        Store(g, g.Backpack, gauntlets);
        Move(g, gauntlets, MoveTarget.Slot, index: ITEM_SLOT_GLOVES, ticks: 2);
        Assert.True(g.Chat.Saw("You start putting on"));
        Assert.True(g.R.In(() => gauntlets.loc == g.Backpack), "in the bag through the delay");
        obj_item bag = g.Backpack;
        g.R.In(() => Waiting.Detach(g.Alice.gib(DROP_ALL_REMAINS))); // tg's gib that drops what the body carries (DROP_ITEMS)
        g.R.Step(4 * Second);
        Assert.True(g.R.In(() => !QDELETED(gauntlets) && gauntlets.loc == bag), "the gauntlets are still in the bag tg dropped");
        Assert.True(g.R.In(() => !QDELETED(bag)));
    }

    [Fact]
    public void ARuntimeInsideAMoveAndInItsRecoveryStillCountsTheRefusalAndLeavesTheItemSomewhere()
    {
        var g = Build(9745);
        var thrower = Make<obj_item_test_carried_thrower>(g);
        Store(g, g.Backpack, thrower);
        g.R.In(() => thrower.Armed = true);
        int runtimes = g.R.Room.Runtimes.Count;
        Move(g, thrower, MoveTarget.Hand, index: Inactive(g.Alice)); // the other hand: attempt_pickup's steps, whose pickup() throws
        g.R.In(() => thrower.Armed = false);
        Assert.Equal(1, Refusals(g));
        Assert.True(g.R.In(() => !QDELETED(thrower) && thrower.loc is turf), "put on her turf");
        Assert.True(g.R.Room.Runtimes.Count >= runtimes + 3, "the pickup, the put_in_hands and the fallback are reported: " + string.Join(" / ", g.R.Room.RuntimeText));
    }

    [Fact]
    public void TheCoreBoundsAreTheWiresBounds()
    {
        Assert.Equal(Anomaly.Protocol.Limits.MaxStorages, CarriedStorages.MaxStorages);
        Assert.Equal(Anomaly.Protocol.Limits.MaxStorageItems, CarriedStorages.MaxItems);
        Assert.Equal(Anomaly.Protocol.Limits.MaxStorageHands, CarriedStorages.MaxHands);
        Assert.Equal((byte)Anomaly.Protocol.StoragePlace.Hand, (byte)CarriedPlace.Hand);
        Assert.Equal((byte)Anomaly.Protocol.InventoryTarget.Floor, (byte)MoveTarget.Floor);
        foreach (int slot in CarriedStorages.WornSlots)
            Assert.True(Anomaly.Protocol.InventorySlots.IsWorn(slot), "slot " + slot);
        Assert.Equal(CarriedStorages.WornSlots.Length, System.Numerics.BitOperations.PopCount((uint)Anomaly.Protocol.InventorySlots.Worn));
    }
}

/// <summary>A stored test item for DV692's recovery after a runtime: when armed its pickup() throws, and so does a move into a mob it is already in.</summary>
[DmPath("/obj/item/test_carried_thrower")]
public class obj_item_test_carried_thrower : obj_item
{
    public bool Armed;

    public override async DmTask<object> pickup(mob user = null)
    {
        if (Armed)
            throw new InvalidOperationException("test: pickup");
        return await base.pickup(user);
    }

    public override async DmTask<object> forceMove(atom destination = null, params object[] __extra)
    {
        if (Armed && destination is mob && loc == destination)
            throw new InvalidOperationException("test: into the mob it is in");
        return await base.forceMove(destination, __extra);
    }
}
