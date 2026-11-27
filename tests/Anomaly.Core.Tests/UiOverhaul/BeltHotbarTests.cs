using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.UiOverhaul;

/// <summary>
/// ui-overhaul.md section 10, slice 3A (DV693): the belt hotbar's cells (<see cref="BeltHotbar"/>) and the five rules of a press, in a booted toy room
/// with one joined player, Alice, in a jumpsuit. The cells are brought up to date every tick as the server's command phase does (a world phase), and a
/// press is the server's (<see cref="BeltHotbar.Press"/>, from her client). Every move is tg's own: the belt's storage, the slot's equip, put in hands.
/// </summary>
public class BeltHotbarTests
{
    private const int Second = 20;

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Alice);

    private static Rig Build(ulong seed)
    {
        var r = new AudioRoom(9, 7, seed: seed);
        mob_living_carbon_human alice = r.Join(4, 3, "Alice");
        r.World.AddPhase("hotbar tracking", _ => BeltHotbar.Track(alice)); // the server's command phase (ServerHost.TrackHotbars)
        r.Step(Second);
        r.In(() =>
        {
            if (alice.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(alice.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(alice), ITEM_SLOT_ICLOTHING))));
            foreach (obj_item worn in new[] { alice.belt, alice.l_store, alice.r_store, alice.wear_id })
                if (worn != null)
                    qdel(worn);
        });
        r.Step(1);
        return new Rig(r, alice);
    }

    private static T Make<T>(Rig g) where T : obj_item => g.R.In(() => Dm.@new<T>(get_turf(g.Alice)));

    private static T Wear<T>(Rig g, int slot) where T : obj_item
    {
        T item = Make<T>(g);
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.equip_to_slot_if_possible(item, slot))), "worn in " + slot));
        g.R.Step(1);
        return item;
    }

    private static obj_item Hold<T>(Rig g) where T : obj_item
    {
        T item = Make<T>(g);
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_active_hand(item)))));
        g.R.Step(1);
        return item;
    }

    private static obj_item_storage_belt_utility_full Toolbelt(Rig g) => Wear<obj_item_storage_belt_utility_full>(g, ITEM_SLOT_BELT);

    private static void Press(Rig g, int cell)
    {
        g.R.Command(() => BeltHotbar.Press(g.Alice.client, cell));
        g.R.Step(4);
    }

    private static HotbarState State(Rig g, int cell) => g.R.In(() => BeltHotbar.Peek(g.Alice).StateOf(g.Alice, cell));

    private static obj_item Item(Rig g, int cell) => g.R.In(() => BeltHotbar.Peek(g.Alice).ItemOf(cell));

    private static obj_item Active(Rig g) => g.R.In(() => g.Alice.get_active_held_item() as obj_item);

    private static obj_item Inactive(Rig g) => g.R.In(() => g.Alice.get_inactive_held_item() as obj_item);

    private static int Refusals(Rig g) => g.R.In(() => g.Alice.dm_hotbar?.Refusals ?? 0);

    private static List<obj_item> Contents(Rig g, obj_item belt) => g.R.In(() => Dm.each<obj_item>(belt.atom_storage.real_location.contents).ToList());

    private static bool InBelt(Rig g, obj_item belt, obj_item item) => g.R.In(() => item.loc == belt.atom_storage.real_location);

    // every cell's state and item, for "nothing changed"
    private static string Snapshot(Rig g) => g.R.In(() =>
    {
        BeltHotbar bar = BeltHotbar.Peek(g.Alice);
        return string.Join(",", Enumerable.Range(1, BeltHotbar.CellCount).Select(c => bar.StateOf(g.Alice, c) + ":" + (bar.ItemOf(c)?.dm_id ?? 0)))
            + "|" + (g.Alice.get_active_held_item() as obj_item)?.dm_id + "|" + (g.Alice.get_inactive_held_item() as obj_item)?.dm_id;
    });

    [Fact]
    public void AWornToolbeltsSevenToolsTakeCellsOneToSevenInTheStoragesOrder()
    {
        var g = Build(9901);
        obj_item_storage_belt_utility_full belt = Toolbelt(g);
        List<obj_item> tools = Contents(g, belt);
        Assert.Equal(7, tools.Count);
        for (int c = 1; c <= 7; c++)
        {
            Assert.Same(tools[c - 1], Item(g, c));
            Assert.Equal(HotbarState.Holding, State(g, c));
        }
        for (int c = 8; c <= 10; c++)
            Assert.Equal(HotbarState.Empty, State(g, c));
        Assert.Same(belt, g.R.In(() => BeltHotbar.Peek(g.Alice).Belt));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void PressThreeTakesTheThirdToolAndPressThreeAgainPutsItBackInTheSameCell()
    {
        var g = Build(9902);
        obj_item_storage_belt_utility_full belt = Toolbelt(g);
        obj_item third = Item(g, 3);
        Press(g, 3); // rule 2
        Assert.Same(third, Active(g));
        Assert.False(InBelt(g, belt, third));
        Assert.Equal(HotbarState.Out, State(g, 3));
        Assert.Same(third, Item(g, 3));
        Press(g, 3); // rule 1
        Assert.Null(Active(g));
        Assert.True(InBelt(g, belt, third));
        Assert.Equal(HotbarState.Holding, State(g, 3));
        Assert.Same(third, Item(g, 3));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnOutItemGoesBackFromTheOtherHandToo()
    {
        var g = Build(9903);
        obj_item_storage_belt_utility_full belt = Toolbelt(g);
        obj_item third = Item(g, 3);
        Press(g, 3);
        g.R.In(() => g.Alice.swap_hand());
        g.R.Step(1);
        Assert.Same(third, Inactive(g));
        Assert.Equal(HotbarState.Out, State(g, 3)); // still in a hand: still the cell's
        obj_item other = Hold<obj_item_analyzer>(g); // the active hand is not empty: rule 1 still applies
        Press(g, 3);
        Assert.True(InBelt(g, belt, third));
        Assert.Same(other, Active(g));
        Assert.Equal(HotbarState.Holding, State(g, 3));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void PressFiveWithTheToolOfThreeInHandSwapsThem()
    {
        var g = Build(9904);
        obj_item_storage_belt_utility_full belt = Toolbelt(g);
        obj_item third = Item(g, 3), fifth = Item(g, 5);
        Press(g, 3);
        Press(g, 5); // rule 3
        Assert.Same(fifth, Active(g));
        Assert.True(InBelt(g, belt, third));
        Assert.Equal(HotbarState.Holding, State(g, 3));
        Assert.Same(third, Item(g, 3));
        Assert.Equal(HotbarState.Out, State(g, 5));
        Assert.Same(fifth, Item(g, 5));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ASwapWhoseItemCannotGoBackDoesNothing()
    {
        var g = Build(9905);
        obj_item_storage_belt_utility_full belt = Toolbelt(g);
        obj_item third = Item(g, 3), fifth = Item(g, 5);
        Press(g, 3);
        // the belt filled up meanwhile (a foreign tool put in another way): seven items again, the out tool's place is taken
        obj_item_screwdriver extra = Make<obj_item_screwdriver>(g);
        g.R.In(() => Assert.True(Dm.truthy(belt.atom_storage.attempt_insert(extra, g.Alice))));
        g.R.Step(1);
        Assert.Equal(-1, g.R.In(() => Enumerable.Range(1, 10).Select(c => BeltHotbar.Peek(g.Alice).ItemOf(c)).ToList().IndexOf(extra))); // no free cell for it
        string before = Snapshot(g);
        Press(g, 5);
        Assert.Equal(before, Snapshot(g));
        Assert.Same(third, Active(g));
        Assert.True(InBelt(g, belt, fifth));
        Assert.Equal(1, Refusals(g));
        Assert.Equal(5, g.R.In(() => g.Alice.dm_hotbar.RefusedCell));
        // and the out tool cannot go back with a press of its own cell either (rule 1 refused by tg's storage)
        Press(g, 3);
        Assert.Same(third, Active(g));
        Assert.Equal(HotbarState.Out, State(g, 3));
        Assert.Equal(2, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AHeldItemGoesIntoAnEmptyCellAndAnItemPutInAnotherWayTakesTheLowestFreeCell()
    {
        var g = Build(9906);
        obj_item_storage_belt_utility belt = Wear<obj_item_storage_belt_utility>(g, ITEM_SLOT_BELT);
        Assert.True(g.R.In(() => BeltHotbar.Peek(g.Alice).Belt == belt));
        obj_item wrench = Hold<obj_item_wrench>(g);
        Press(g, 4); // rule 4
        Assert.Null(Active(g));
        Assert.True(InBelt(g, belt, wrench));
        Assert.Same(wrench, Item(g, 4));
        Assert.Equal(HotbarState.Holding, State(g, 4));
        // a tool put into the belt by tg's storage itself (a click, a drag): the lowest free cell, 1; the next one 2
        obj_item_crowbar crowbar = Make<obj_item_crowbar>(g);
        g.R.In(() => Assert.True(Dm.truthy(belt.atom_storage.attempt_insert(crowbar, g.Alice))));
        g.R.Step(1);
        obj_item_wirecutters cutters = Make<obj_item_wirecutters>(g);
        g.R.In(() => Assert.True(Dm.truthy(belt.atom_storage.attempt_insert(cutters, g.Alice))));
        g.R.Step(1);
        Assert.Same(crowbar, Item(g, 1));
        Assert.Same(cutters, Item(g, 2));
        Assert.Same(wrench, Item(g, 4));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemTheBeltDoesNotTakeIsRefusedAndStaysInHand()
    {
        var g = Build(9907);
        obj_item_storage_belt_utility belt = Wear<obj_item_storage_belt_utility>(g, ITEM_SLOT_BELT);
        obj_item box = Hold<obj_item_storage_toolbox_mechanical>(g); // too big for a toolbelt (tg's can_insert)
        string before = Snapshot(g);
        Press(g, 1);
        Assert.Equal(before, Snapshot(g));
        Assert.Same(box, Active(g));
        Assert.False(InBelt(g, belt, box));
        Assert.Equal(1, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyHandOnAnEmptyCellAndAForeignItemOnAFullCellAreRefused()
    {
        var g = Build(9908);
        Toolbelt(g);
        Press(g, 8); // an empty hand, an empty pocket cell (rule 5)
        Assert.Equal(1, Refusals(g));
        Assert.Equal(8, g.R.In(() => g.Alice.dm_hotbar.RefusedCell));
        obj_item foreign = Hold<obj_item_analyzer>(g); // not from the bar, and cell 2 holds a tool (rule 5)
        string before = Snapshot(g);
        Press(g, 2);
        Assert.Equal(before, Snapshot(g));
        Assert.Same(foreign, Active(g));
        Assert.Equal(2, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void NoBeltWornMeansEmptyBeltCellsAndRefusedPresses()
    {
        var g = Build(9909);
        Assert.Null(g.R.In(() => BeltHotbar.Peek(g.Alice).Belt));
        Press(g, 1);
        Assert.Equal(1, Refusals(g));
        obj_item wrench = Hold<obj_item_wrench>(g);
        Press(g, 1); // nothing to store it in
        Assert.Same(wrench, Active(g));
        Assert.Equal(2, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void CuffedStunnedOrDeadNothingHappens()
    {
        foreach (string how in new[] { "cuffed", "stunned", "dead" })
        {
            var g = Build(9910);
            obj_item_storage_belt_utility_full belt = Toolbelt(g);
            obj_item third = Item(g, 3);
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
                    default:
                        Waiting.Detach(g.Alice.death());
                        break;
                }
            });
            g.R.Step(2);
            string before = Snapshot(g);
            Press(g, 3);
            Assert.True(before == Snapshot(g), how);
            Assert.True(InBelt(g, belt, third), how);
            Assert.True(Refusals(g) == 1, how);
        }
    }

    [Fact]
    public void DroppingAnOutItemFreesItsCellAndAToolTakenAnotherWayHasNoCell()
    {
        var g = Build(9911);
        obj_item_storage_belt_utility_full belt = Toolbelt(g);
        obj_item third = Item(g, 3), sixth = Item(g, 6);
        Press(g, 3);
        g.R.In(() => Waiting.NoSleep(g.Alice.dropItemToGround(third)));
        g.R.Step(1);
        Assert.Equal(HotbarState.Empty, State(g, 3));
        Assert.Null(Item(g, 3));
        // the sixth tool taken out by tg's own click on it in the open belt, not by the bar: its cell is freed, it is not out
        g.R.In(() => Waiting.NoSleep(sixth.attack_hand(g.Alice)));
        g.R.Step(1);
        Assert.Same(sixth, Active(g));
        Assert.Equal(HotbarState.Empty, State(g, 6));
        // so a press of 6 with it in hand stores it back into cell 6 (rule 4), and the dropped tool put in another way takes cell 3
        Press(g, 6);
        Assert.Same(sixth, Item(g, 6));
        g.R.In(() => Assert.True(Dm.truthy(belt.atom_storage.attempt_insert(third, g.Alice))));
        g.R.Step(1);
        Assert.Same(third, Item(g, 3));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TakingTheBeltOffEmptiesTheSevenCellsAndPuttingItOnFillsThemAgain()
    {
        var g = Build(9912);
        obj_item_storage_belt_utility_full belt = Toolbelt(g);
        Press(g, 2);
        obj_item second = Active(g);
        g.R.In(() => Waiting.NoSleep(g.Alice.dropItemToGround(belt)));
        g.R.Step(1);
        for (int c = 1; c <= 7; c++)
            Assert.Equal(HotbarState.Empty, State(g, c));
        Assert.Null(g.R.In(() => BeltHotbar.Peek(g.Alice).Belt));
        Press(g, 2); // the out tool has no cell any more: an empty cell and no belt
        Assert.Same(second, Active(g));
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT)))));
        g.R.Step(1);
        List<obj_item> tools = Contents(g, belt);
        Assert.Equal(6, tools.Count);
        for (int c = 1; c <= 6; c++)
            Assert.Same(tools[c - 1], Item(g, c));
        Assert.Equal(HotbarState.Empty, State(g, 7));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePocketsAndTheIdSlotTakeOutPutBackAndStore()
    {
        var g = Build(9913);
        obj_item_pen pen = Wear<obj_item_pen>(g, ITEM_SLOT_LPOCKET);
        Assert.Same(pen, Item(g, 8));
        Assert.Equal(HotbarState.Holding, State(g, 8));
        Press(g, 8);
        Assert.Same(pen, Active(g));
        Assert.Equal(HotbarState.Out, State(g, 8));
        Press(g, 8);
        Assert.Same(pen, g.R.In(() => g.Alice.l_store));
        Assert.Equal(HotbarState.Holding, State(g, 8));
        // an ID card in hand into the empty ID cell (rule 4, tg's equip into ITEM_SLOT_ID)
        obj_item card = Hold<obj_item_card_id>(g);
        Press(g, 10);
        Assert.Same(card, g.R.In(() => g.Alice.wear_id));
        Assert.Same(card, Item(g, 10));
        // the right pocket refuses a toolbox (tg's equip rules)
        obj_item box = Hold<obj_item_storage_toolbox_mechanical>(g);
        Press(g, 9);
        Assert.Same(box, Active(g));
        Assert.Null(g.R.In(() => g.Alice.r_store));
        Assert.Equal(1, Refusals(g));
        Assert.Equal(9, g.R.In(() => g.Alice.dm_hotbar.RefusedCell));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void APocketItemSwapsWithABeltToolAndAnOutItemPutInAPocketLeavesItsBeltCell()
    {
        var g = Build(9914);
        obj_item_storage_belt_utility_full belt = Toolbelt(g);
        obj_item_pen pen = Wear<obj_item_pen>(g, ITEM_SLOT_LPOCKET);
        obj_item first = Item(g, 1);
        Press(g, 8);
        Press(g, 1); // rule 3 across the groups
        Assert.Same(pen, g.R.In(() => g.Alice.l_store));
        Assert.Same(first, Active(g));
        Assert.Equal(HotbarState.Out, State(g, 1));
        // the out tool into the empty right pocket by a press (rule 4): it is cell 9's now, and cell 1 is free
        Press(g, 9);
        Assert.Same(first, g.R.In(() => g.Alice.r_store));
        Assert.Same(first, Item(g, 9));
        Assert.Equal(HotbarState.Empty, State(g, 1));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void APressOfACellOutOfRangeDoesNothing()
    {
        var g = Build(9915);
        Toolbelt(g);
        string before = Snapshot(g);
        foreach (int cell in new[] { 0, 11, -1, 255 })
            Press(g, cell);
        Assert.Equal(before, Snapshot(g));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void APressFromAGhostOrFromAClientWithoutAMobDoesNothing()
    {
        foreach (string who in new[] { "ghost", "no mob" })
        {
            var g = Build(9916);
            obj_item_storage_belt_utility_full belt = Toolbelt(g);
            obj_item third = Item(g, 3);
            client player = g.R.In(() => g.Alice.client);
            g.R.In(() =>
            {
                if (who == "ghost")
                    g.Alice.ghostize(Dm.TRUE);
                else
                    player.mob = null;
            });
            g.R.Step(2);
            Assert.True(who == "ghost" ? g.R.In(() => player.mob) is mob_dead_observer : g.R.In(() => player.mob) == null, who);
            string before = Snapshot(g);
            for (int cell = 1; cell <= BeltHotbar.CellCount; cell++)
            {
                int pressed = cell;
                g.R.Command(() => BeltHotbar.Press(player, pressed));
                g.R.Step(2);
            }
            Assert.True(before == Snapshot(g), who + ": nothing moved");
            Assert.True(InBelt(g, belt, third), who);
            Assert.True(Refusals(g) == 0, who + ": the body's cells counted no refusal");
            g.R.Command(() => BeltHotbar.Press(null, 3)); // no client at all
            g.R.Step(2);
            Assert.True(before == Snapshot(g), who);
            g.R.AssertNoRuntimes();
        }
    }

    // ---- slice 4B-2 (DV693, DV696): a move into the worn belt wishing for a bar cell (CarriedStorages.Move, BeltHotbar.PlaceInCell) ----

    private static void MoveToCell(Rig g, obj_item item, obj_item belt, int cell)
    {
        g.R.Command(() => CarriedStorages.Move(g.Alice.client, item.dm_id, MoveTarget.Storage, belt.dm_id, cell));
        g.R.Step(Second);
    }

    private static obj_item[] Bar(Rig g) => Enumerable.Range(1, BeltHotbar.BeltCells).Select(c => Item(g, c)).ToArray();

    [Fact]
    public void A_move_into_the_belt_wishing_for_a_bar_cell_puts_the_item_in_that_cell()
    {
        Rig g = Build(9301);
        var belt = Wear<obj_item_storage_belt_utility>(g, ITEM_SLOT_BELT);
        obj_item a = g.R.In(() => (obj_item)Dm.@new<obj_item_screwdriver>(belt));
        obj_item b = g.R.In(() => (obj_item)Dm.@new<obj_item_wrench>(belt));
        g.R.Step(2);
        Assert.Equal(new[] { a, b, null, null, null, null, null }, Bar(g));
        // a held item onto the empty cell 6: in the belt (tg's insert), in cell 6
        obj_item c = Hold<obj_item_crowbar>(g);
        MoveToCell(g, c, belt, 6);
        Assert.True(InBelt(g, belt, c));
        Assert.Equal(new[] { a, b, null, null, null, c, null }, Bar(g));
        // an item already in the belt onto an empty cell: only its cell changes, tg's order in the belt is left as it was
        List<obj_item> order = Contents(g, belt);
        MoveToCell(g, a, belt, 4);
        Assert.Equal(new[] { null, b, null, a, null, c, null }, Bar(g));
        Assert.Equal(order, Contents(g, belt));
        // onto a cell holding another belt item, the moved item having a cell: the two swap
        MoveToCell(g, a, belt, 6);
        Assert.Equal(new[] { null, b, null, c, null, a, null }, Bar(g));
        // a wish for the cell it already has: nothing changes
        MoveToCell(g, a, belt, 6);
        Assert.Equal(new[] { null, b, null, c, null, a, null }, Bar(g));
        // a held item new to the bar onto an occupied cell: it takes the cell, the item there the lowest free one
        obj_item d = Hold<obj_item_wirecutters>(g);
        MoveToCell(g, d, belt, 2);
        Assert.Equal(new[] { b, d, null, c, null, a, null }, Bar(g));
        // an item out of its cell in a hand (taken with its key) dropped onto another cell: it swaps with the item there, as having a cell
        Press(g, 4);
        Assert.Equal((c, HotbarState.Out), (Active(g), State(g, 4)));
        MoveToCell(g, c, belt, 1);
        Assert.True(InBelt(g, belt, c));
        Assert.Equal(new[] { c, d, null, b, null, a, null }, Bar(g));
        // no wish: as before, the lowest free cell
        obj_item e = Hold<obj_item_multitool>(g);
        MoveToCell(g, e, belt, 0);
        Assert.Equal(new[] { c, d, e, b, null, a, null }, Bar(g));
        // a wish past the bar's seven cells names no bar cell: the move is not taken at all (nothing moves, nothing refused)
        obj_item f = Hold<obj_item_weldingtool>(g);
        int refused = g.R.In(() => g.Alice.dm_carried?.Refusals ?? 0);
        MoveToCell(g, f, belt, 8);
        Assert.Equal(f, Active(g));
        Assert.Equal(refused, g.R.In(() => g.Alice.dm_carried?.Refusals ?? 0));
        Assert.Equal(0, Refusals(g));
    }

    [Fact]
    public void With_a_full_bar_a_wished_cell_swaps_within_the_belt_and_tg_refuses_an_eighth_item()
    {
        Rig g = Build(9302);
        obj_item_storage_belt_utility_full belt = Toolbelt(g);
        obj_item[] full = Bar(g);
        Assert.DoesNotContain(null, full);
        // the seventh cell's item onto the first: they swap
        MoveToCell(g, full[6], belt, 1);
        Assert.Equal(new[] { full[6], full[1], full[2], full[3], full[4], full[5], full[0] }, Bar(g));
        // a held item into the full belt at cell 3: tg's insert refuses it, the bar stays as it was
        obj_item extra = Hold<obj_item_crowbar>(g);
        obj_item[] before = Bar(g);
        MoveToCell(g, extra, belt, 3);
        Assert.Equal(extra, Active(g));
        Assert.Equal(before, Bar(g));
    }

    // no cell names two items, and no item has two cells
    private static void AssertOneToOne(Rig g)
    {
        obj_item[] cells = Enumerable.Range(1, BeltHotbar.CellCount).Select(c => Item(g, c)).Where(i => i != null).ToArray();
        Assert.Equal(cells.Length, cells.Distinct().Count());
    }

    [Fact]
    public void A_wish_for_a_cell_whose_item_a_press_is_taking_out_leaves_that_cell_to_the_press()
    {
        // heavy gravity: the press of cell 5 waits in attempt_pickup's 3 s (items.dm:556-561) holding E; meanwhile X (cell 2) is dropped onto cell 5
        Rig g = Build(9303);
        var belt = Wear<obj_item_storage_belt_utility>(g, ITEM_SLOT_BELT);
        obj_item[] items = g.R.In(() => new obj_item[] { Dm.@new<obj_item_crowbar>(belt), Dm.@new<obj_item_screwdriver>(belt), Dm.@new<obj_item_wrench>(belt),
            Dm.@new<obj_item_wirecutters>(belt), Dm.@new<obj_item_multitool>(belt) });
        g.R.Step(2);
        obj_item x = items[1], e = items[4];
        Assert.Equal((x, e), (Item(g, 2), Item(g, 5)));
        g.R.In(() => Waiting.NoSleep(g.Alice.AddElement(typeof(datum_element_forced_gravity), 2)));
        g.R.Step(1);
        g.R.Command(() => BeltHotbar.Press(g.Alice.client, 5));
        g.R.Step(2);
        Assert.Null(Active(g)); // the press is still waiting
        Assert.True(g.R.In(() => CarriedStorages.IsClaimed(g.Alice, e)));
        g.R.Command(() => CarriedStorages.Move(g.Alice.client, x.dm_id, MoveTarget.Storage, belt.dm_id, 5));
        g.R.Step(2);
        // the move took place (X stays in the belt) without taking E's cell
        Assert.True(InBelt(g, belt, x));
        Assert.Equal((x, e), (Item(g, 2), Item(g, 5)));
        g.R.Step(4 * Second);
        // both done: E out in the hand with its cell 5, X in the belt in its cell, one item a cell
        Assert.Equal(e, Active(g));
        Assert.Equal(HotbarState.Out, State(g, 5));
        Assert.Equal(e, Item(g, 5));
        Assert.Equal((x, HotbarState.Holding), (Item(g, 2), State(g, 2)));
        AssertOneToOne(g);
        // the same key puts E back
        Press(g, 5);
        g.R.Step(Second);
        Assert.True(InBelt(g, belt, e));
        Assert.Equal((e, HotbarState.Holding), (Item(g, 5), State(g, 5)));
        Assert.Null(Active(g));
        AssertOneToOne(g);
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_press_during_a_moves_wait_on_the_wished_cells_item_keeps_that_item_its_cell()
    {
        // a screwdriver in a trash bag in a pocket goes into the belt at cell 2, where X is, after the trash bag's own search (its removal's 1.5 s
        // wait); during that wait X's cell is pressed and X comes into the hand, out of its cell: when the move ends it must not take X's cell
        Rig g = Build(9304);
        var belt = Wear<obj_item_storage_belt_utility>(g, ITEM_SLOT_BELT);
        obj_item[] items = g.R.In(() => new obj_item[] { Dm.@new<obj_item_crowbar>(belt), Dm.@new<obj_item_wrench>(belt) });
        obj_item m = g.R.In(() =>
        {
            var bag = Dm.@new<obj_item_storage_bag_trash>(get_turf(g.Alice));
            Waiting.NoSleep(g.Alice.equip_to_slot(bag, ITEM_SLOT_LPOCKET));
            var tool = Dm.@new<obj_item_screwdriver>(get_turf(g.Alice));
            Assert.True(Dm.truthy(bag.atom_storage.attempt_insert(tool, null, messages: Dm.FALSE)));
            return (obj_item)tool;
        });
        g.R.Step(2);
        obj_item x = items[1];
        Assert.Equal(x, Item(g, 2));
        g.R.Command(() => CarriedStorages.Move(g.Alice.client, m.dm_id, MoveTarget.Storage, belt.dm_id, 2));
        g.R.Step(2);
        Assert.False(InBelt(g, belt, m)); // still searched for
        g.R.Command(() => BeltHotbar.Press(g.Alice.client, 2));
        g.R.Step(2);
        Assert.Equal((x, HotbarState.Out), (Active(g), State(g, 2)));
        g.R.Step(3 * Second);
        // tg's do_after of the trash bag's search ends when the searching hand's item changes (X came into it): the move is refused and M stays in
        // the bag; X keeps its cell 2, out in the hand; no cell names two items
        Assert.False(InBelt(g, belt, m));
        Assert.Equal(1, g.R.In(() => g.Alice.dm_carried?.Refusals ?? 0));
        Assert.Equal(x, Active(g));
        Assert.Equal((x, HotbarState.Out), (Item(g, 2), State(g, 2)));
        AssertOneToOne(g);
        // the same key puts X back into its cell
        Press(g, 2);
        g.R.Step(Second);
        Assert.True(InBelt(g, belt, x));
        Assert.Equal((x, HotbarState.Holding), (Item(g, 2), State(g, 2)));
        AssertOneToOne(g);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_wish_for_a_cell_whose_item_is_out_in_a_hand_leaves_that_item_its_cell()
    {
        // X is taken from cell 2 with its key and held; a screwdriver from the backpack is then moved onto cell 2: it goes into the belt
        // and takes the lowest free cell; X keeps cell 2 while in the hand (section 10), and its key puts it back
        Rig g = Build(9305);
        var belt = Wear<obj_item_storage_belt_utility>(g, ITEM_SLOT_BELT);
        var bag = Wear<obj_item_storage_backpack>(g, ITEM_SLOT_BACK);
        obj_item[] items = g.R.In(() => new obj_item[] { Dm.@new<obj_item_crowbar>(belt), Dm.@new<obj_item_wrench>(belt) });
        obj_item m = g.R.In(() => (obj_item)Dm.@new<obj_item_screwdriver>(bag));
        g.R.Step(2);
        obj_item x = items[1];
        Press(g, 2);
        Assert.Equal((x, HotbarState.Out), (Active(g), State(g, 2)));
        g.R.Command(() => CarriedStorages.Move(g.Alice.client, m.dm_id, MoveTarget.Storage, belt.dm_id, 2));
        g.R.Step(Second);
        Assert.True(InBelt(g, belt, m));
        Assert.Equal((x, HotbarState.Out), (Item(g, 2), State(g, 2)));
        Assert.Equal((m, HotbarState.Holding), (Item(g, 3), State(g, 3)));
        AssertOneToOne(g);
        Press(g, 2);
        g.R.Step(Second);
        Assert.Equal((x, HotbarState.Holding), (Item(g, 2), State(g, 2)));
        AssertOneToOne(g);
        g.R.AssertNoRuntimes();
    }
}
