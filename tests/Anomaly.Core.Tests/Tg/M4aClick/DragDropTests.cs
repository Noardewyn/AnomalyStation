using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aClick;

/// <summary>
/// Drag and drop (protocol version 17, code/_onclick/drag_drop.dm): BYOND's MouseDrag and MouseDrop through the client seam
/// (ClientSeam.Drop) on a booted toy room with a joined player. A worn backpack dragged onto an empty hand slot comes off into that hand
/// (tg's storage on_mousedrop_onto, the defib unit test's shape); onto its wearer it opens; a storage dragged onto a table dumps its
/// contents onto the table's tile after tg's 2 s; the held item dragged onto a storage goes in; items dragged within an open storage
/// reorder (the item's mousedrop_receive, the free cell's); an open closet takes an item dragged onto it; a drag onto the dragged thing
/// itself or a quick short one is tg's click; the client's drag bookkeeping is reset by the drop; an inert type's own receiver is refused.
/// </summary>
public class DragDropTests
{
    private const int Second = 20;
    private const string Left = "left=1;button=left;drag=left";

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(9, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(2);
        return (r, bob);
    }

    private static void Drop(AudioRoom r, mob who, atom dragged, atom over, int deciseconds = 5, string dropParams = Left, string dragParams = Left)
    {
        r.Command(() => ClientSeam.Drop(who.client, dragged, over, dragParams, dropParams, deciseconds));
        r.Step(2);
    }

    private static obj_item Thing(AudioRoom r, atom loc, float w_class = WEIGHT_CLASS_SMALL) => r.In(() =>
    {
        obj_item thing = Dm.@new<obj_item>(loc);
        thing.w_class = w_class;
        return thing;
    });

    private static T Wear<T>(AudioRoom r, mob_living_carbon_human who, float slot) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_or_del(item, slot))), typeof(T).Name + " goes on");
        return item;
    });

    private static turf T(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));

    private static atom_movable_screen_inventory_hand Hand(AudioRoom r, mob who, int index)
        => r.In(() => who.hud_used.screen_objects[Dm.text("hand_slot:[]", index)] as atom_movable_screen_inventory_hand);

    [Fact]
    public void AWornBackpackDraggedOntoAnEmptyHandSlotComesOffIntoThatHand()
    {
        var (r, bob) = Room(9101);
        var pack = Wear<obj_item_storage_backpack>(r, bob, ITEM_SLOT_BACK);
        atom_movable_screen_inventory_hand left = Hand(r, bob, 1);
        Assert.NotNull(left);
        Drop(r, bob, pack, left, dropParams: Left + ";screen-loc=CENTER:-16,SOUTH:5");
        r.In(() =>
        {
            Assert.Null(bob.back);
            Assert.Same(bob, pack.loc);
            Assert.Same(pack, bob.held_items[1]);
            Assert.Equal(0f, bob.client.drag_start); // tg's MouseDrop resets the drag's bookkeeping
            Assert.Null(bob.client.drag_details);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWornBackpackDraggedOntoItsWearerOpens()
    {
        var (r, bob) = Room(9102);
        var pack = Wear<obj_item_storage_backpack>(r, bob, ITEM_SLOT_BACK);
        Drop(r, bob, pack, bob);
        r.In(() =>
        {
            Assert.Same(pack.atom_storage, bob.active_storage);
            Assert.Same(pack, bob.back); // still worn
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStorageDraggedOntoATableDumpsItsContentsOntoTheTableAfterTwoSeconds()
    {
        var (r, bob) = Room(9103);
        obj_structure_table table = r.In(() => Dm.@new<obj_structure_table>(locate(5, 4, 1)));
        obj_item_storage box = r.In(() => Dm.@new<obj_item_storage>(bob.loc));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(box)))));
        obj_item a = Thing(r, box), b = Thing(r, box);
        r.In(() => Assert.Equal(2f, Dm.length(box.contents)));
        Drop(r, bob, box, table);
        r.In(() => Assert.Same(box, a.loc)); // tg's do_after: 2 seconds
        r.Step(2 * Second + 4);
        r.In(() =>
        {
            Assert.Same(table.loc, a.loc);
            Assert.Same(table.loc, b.loc);
            Assert.Equal(0f, Dm.length(box.contents));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHeldItemDraggedOntoAStorageGoesIn()
    {
        var (r, bob) = Room(9104);
        var pack = Wear<obj_item_storage_backpack>(r, bob, ITEM_SLOT_BACK);
        obj_item thing = Thing(r, bob.loc);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(thing)))));
        Drop(r, bob, thing, pack);
        r.In(() => Assert.Same(pack, thing.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ItemsDraggedWithinAnOpenStorageReorder()
    {
        var (r, bob) = Room(9105);
        var pack = Wear<obj_item_storage_backpack>(r, bob, ITEM_SLOT_BACK);
        obj_item a = Thing(r, pack), b = Thing(r, pack), c = Thing(r, pack);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(pack.atom_storage.open_storage(bob)))));
        r.In(() => Assert.Same(pack.atom_storage, bob.active_storage));

        // c dropped onto a: c takes a's place (the storage's mousedrop_receive, storage.dm:524)
        Drop(r, bob, c, a);
        r.In(() => Assert.Equal(new object[] { c, a, b }, Dm.each_cast<object>(pack.contents).ToArray()));

        // c dropped onto the free cells: it goes last (screen_objects.dm:673)
        atom_movable_screen_storage_cell cells = r.In(() => Dm.each_cast<object>(bob.client.screen).OfType<atom_movable_screen_storage_cell>().FirstOrDefault());
        Assert.NotNull(cells);
        Drop(r, bob, c, cells);
        r.In(() => Assert.Equal(new object[] { a, b, c }, Dm.each_cast<object>(pack.contents).ToArray()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnOpenClosetTakesAnItemDraggedOntoIt()
    {
        var (r, bob) = Room(9106);
        obj_structure_closet closet = r.In(() => Dm.@new<obj_structure_closet>(locate(5, 4, 1)));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(closet.open(bob)))));
        turf spot = T(r, 4, 5);
        obj_item thing = Thing(r, spot);
        Drop(r, bob, thing, closet);
        r.In(() => Assert.Same(closet.loc, thing.loc));

        // shut, it takes nothing
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(closet.close(bob)))));
        obj_item other = Thing(r, spot);
        Drop(r, bob, other, closet);
        r.In(() => Assert.Same(spot, other.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADragOntoItselfOrAQuickShortOneIsTgsClick()
    {
        var (r, bob) = Room(9107);
        obj_structure_table table = r.In(() => Dm.@new<obj_structure_table>(locate(5, 4, 1)));

        // released over itself: tg's click on it (an empty hand picks it up)
        obj_item first = Thing(r, T(r, 4, 5));
        Drop(r, bob, first, first);
        r.In(() => Assert.Same(bob, first.loc));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(first)));
        r.Step(Second); // the click's cooldown

        // a drag shorter than tg's 0.1 s with no screen-loc on either end (16 pixels of leniency): the click on the dragged thing
        r.In(() => bob.swap_hand());
        obj_item second = Thing(r, T(r, 3, 4));
        Drop(r, bob, second, table, deciseconds: 0);
        r.In(() => Assert.Same(bob, second.loc));
        r.Step(Second);

        // a longer one: a drop; tg has no handler for a plain item onto a table, and the port's gesture (D225: picked up into the empty
        // active hand, then the table clicked with it) stops at once: the active hand holds the second (hands full)
        turf third_spot = T(r, 3, 5);
        obj_item third = Thing(r, third_spot);
        Drop(r, bob, third, table, deciseconds: 5);
        r.In(() => Assert.Same(third_spot, third.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMiddleDragIsForgottenByTheDrop()
    {
        var (r, bob) = Room(9108);
        obj_item thing = Thing(r, T(r, 3, 4));
        turf target = T(r, 5, 5);
        const string middle = "middle=1;button=middle;drag=middle";
        Drop(r, bob, thing, target, dragParams: middle, dropParams: middle);
        r.In(() =>
        {
            Assert.Null(bob.client.middle_drag_atom_ref);
            Assert.Equal(0f, bob.client.middragtime);
            Assert.Same(target, bob.client.mouse_object_ref?.resolve());
            Assert.Equal(middle, bob.client.mouseParams);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMobDraggedOntoAHumanWhoIsNotGrabbingItIsNothing()
    {
        // tg's human mouse_buckle_handling (human.dm:870): only a mob held in an aggressive grab is piggybacked or carried
        var (r, bob) = Room(9111);
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        Drop(r, bob, dummy, bob);
        Drop(r, bob, bob, dummy);
        r.In(() =>
        {
            Assert.Same(T(r, 5, 4), dummy.loc);
            Assert.Null(bob.buckled);
            Assert.Null(dummy.buckled);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnInertTypesOwnReceiverIsRefusedNotRunAsItsParents()
    {
        Assert.Contains("mouse_drop_receive", InertSeam.GuardedProcs);
        Assert.Contains("mouse_drop_dragged", InertSeam.GuardedProcs);
        var (r, bob) = Room(9109);
        obj_machinery_sleeper sleeper = r.In(() => Dm.@new<obj_machinery_sleeper>(locate(5, 4, 1)));
        turf spot = T(r, 4, 5);
        obj_item thing = Thing(r, spot);
        Drop(r, bob, thing, sleeper);
        r.In(() =>
        {
            Assert.Same(spot, thing.loc);
            Assert.Contains(DmWorldState.Of(r.World).DeferredSites, k => k.Contains("mouse_drop_receive", StringComparison.Ordinal));
        });
        r.AssertNoRuntimes();
    }

    private static object[] Nums(DmList list) => Dm.each_cast<object>(list).Select(x => (object)Dm.num(x)).ToArray();

    [Fact]
    public void ScreenLocToOffsetReadsTgsScreenLocs()
    {
        var (r, _) = Room(9110);
        r.In(() =>
        {
            Assert.Equal(new object[] { 64f, 64f }, Nums(screen_loc_to_offset(null, "15x15")));
            Assert.Equal(new object[] { 476f, 223f }, Nums(screen_loc_to_offset("EAST-1:28,CENTER-1:15", "15x15")));
            Assert.Equal(new object[] { 32f + 32f, 32f + 5f }, Nums(screen_loc_to_offset("WEST+1,SOUTH:5", "15x15")));
            Assert.Equal(new object[] { 480f, 480f }, Nums(view_to_pixels("15x15")));
            Assert.Equal(new object[] { 15f, 15f }, Nums(getviewsize(7)));
            Assert.Equal("-1", cut_relative_direction("EAST-1"));
        });
    }
}
