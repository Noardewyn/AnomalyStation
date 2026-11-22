using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aStorage.P3;

/// <summary>
/// M4a P3: tg's storage datum and closets on a booted toy room (M2 P4's capsule room): the insert limits (weight class
/// against max_specific_storage, max_slots, max_total_storage), show_contents' private screen entries in the HUD's
/// storage group and hide_contents, a closet's lazy fill, dump on open and take on close, the lock and the weld, and a
/// Meta toolbox's tg contents.
/// </summary>
public class StorageClosetTests
{
    // GAP(P2): /obj/item/on_found (items.dm:724, tg's body is `return`) is still M2's skeleton on this branch; show_contents
    // asks every stored item, so the stored items are stubs that answer as tg's base does.
    private static obj_item Stub(CapsuleRoom room, int x, int y, float w_class) => room.In(() =>
    {
        obj_item thing = Dm.@new<obj_item_test_p3_stub>(locate(x, y, 1));
        thing.w_class = w_class;
        return thing;
    });

    private static obj_item Thing(CapsuleRoom room, int x, int y, float w_class) => room.In(() =>
    {
        obj_item thing = Dm.@new<obj_item>(locate(x, y, 1));
        thing.w_class = w_class;
        return thing;
    });

    [Fact]
    public void AStorageTakesItemsUpToItsSlotsWeightAndSize()
    {
        var room = new CapsuleRoom(31);
        obj_item_storage bag = room.In(() => Dm.@new<obj_item_storage>(locate(5, 5, 1)));
        datum_storage storage = room.In(() => bag.atom_storage);
        Assert.NotNull(storage);
        Assert.Equal(7f, storage.max_slots);
        Assert.Equal((float)WEIGHT_CLASS_NORMAL, storage.max_specific_storage);
        Assert.Equal(14f, storage.max_total_storage);

        // Too big: a bulky item against WEIGHT_CLASS_NORMAL.
        obj_item bulky = Thing(room, 5, 5, WEIGHT_CLASS_BULKY);
        Assert.False(Dm.truthy(room.In(() => storage.attempt_insert(bulky, null))));
        Assert.NotEqual<object>(bag, room.In(() => bulky.loc));

        // The total weight: four normal items (12) and a small one (14) fit, a tiny one more (15 > 14) does not.
        for (int i = 0; i < 4; i++)
        {
            obj_item normal = Thing(room, 5, 5, WEIGHT_CLASS_NORMAL);
            Assert.True(Dm.truthy(room.In(() => storage.attempt_insert(normal, null))));
            Assert.Same(bag, room.In(() => normal.loc));
            Assert.True(Dm.truthy(room.In(() => Dm.bitand(normal.item_flags, IN_STORAGE))));
        }
        obj_item small = Thing(room, 5, 5, WEIGHT_CLASS_SMALL);
        Assert.True(Dm.truthy(room.In(() => storage.attempt_insert(small, null))));
        obj_item heavy = Thing(room, 5, 5, WEIGHT_CLASS_TINY);
        Assert.False(Dm.truthy(room.In(() => storage.attempt_insert(heavy, null))));
        Assert.Equal(14f, Dm.num(room.In(() => storage.get_total_weight())));
        room.In(() => Waiting.NoSleep(storage.remove_all()));
        Assert.Equal(0, room.In(() => (int)bag.contents.len));

        // The slots: seven tiny items fill the seven slots, the eighth is refused.
        for (int i = 0; i < 7; i++)
        {
            obj_item tiny = Thing(room, 5, 5, WEIGHT_CLASS_TINY);
            Assert.True(Dm.truthy(room.In(() => storage.attempt_insert(tiny, null))));
        }
        obj_item extra = Thing(room, 5, 5, WEIGHT_CLASS_TINY);
        Assert.False(Dm.truthy(room.In(() => storage.attempt_insert(extra, null))));
        Assert.Equal(7, room.In(() => (int)bag.contents.len));

        // Removing one frees a slot again; remove_all drops the rest on the turf.
        obj_item first = room.In(() => bag.contents[1] as obj_item);
        Assert.True(Dm.truthy(room.In(() => storage.attempt_remove(first, locate(5, 5, 1)))));
        Assert.False(Dm.truthy(room.In(() => Dm.bitand(first.item_flags, IN_STORAGE))));
        Assert.True(Dm.truthy(room.In(() => storage.attempt_insert(extra, null))));
        room.In(() => Waiting.NoSleep(storage.remove_all()));
        Assert.Equal(0, room.In(() => (int)bag.contents.len));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ALockedStorageRefusesAndForceOpensIt()
    {
        var room = new CapsuleRoom(32);
        obj_item_storage bag = room.In(() => Dm.@new<obj_item_storage>(locate(5, 5, 1)));
        datum_storage storage = room.In(() => bag.atom_storage);
        room.In(() => storage.set_locked(STORAGE_FULLY_LOCKED));
        obj_item tiny = Thing(room, 5, 5, WEIGHT_CLASS_TINY);
        Assert.False(Dm.truthy(room.In(() => storage.attempt_insert(tiny, null))));
        Assert.True(Dm.truthy(room.In(() => storage.attempt_insert(tiny, null, force: STORAGE_FULLY_LOCKED))));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ShowContentsPutsTheUiAndTheItemsInTheViewersStorageGroup()
    {
        var room = new CapsuleRoom(33);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        obj_item_storage bag = room.In(() => Dm.@new<obj_item_storage>(alice.loc));
        datum_storage storage = room.In(() => bag.atom_storage);
        obj_item a = Stub(room, 5, 5, WEIGHT_CLASS_TINY);
        obj_item b = Stub(room, 5, 5, WEIGHT_CLASS_SMALL);
        room.In(() => storage.attempt_insert(a, null));
        room.In(() => storage.attempt_insert(b, null));

        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(storage.show_contents(alice)))));
        DmList screen = room.In(() => alice.client.screen);
        DmList group = room.In(() => alice.hud_used.screen_groups[HUD_GROUP_STORAGE] as DmList);
        DmList ui = room.In(() => (storage.storage_interfaces[alice] as datum_storage_interface).list_ui_elements() as DmList);
        Assert.Equal(9, (int)ui.len);
        Assert.Same(storage, room.In(() => alice.active_storage));
        room.In(() =>
        {
            foreach (object element in Dm.each(ui))
            {
                Assert.True(Dm.@in(element, screen));
                Assert.True(Dm.@in(element, group));
            }
            foreach (obj_item item in new[] { a, b })
            {
                Assert.True(Dm.@in(item, screen));
                Assert.True(Dm.@in(item, group));
                Assert.NotNull(item.screen_loc);
            }
            // orient_storage's cells: the box starts at tg's screen_start_x 4, pixel 16; the items fill it left to right.
            Assert.Equal("4:16,2:16", a.screen_loc);
            Assert.Equal("5:16,2:16", b.screen_loc);
        });

        Assert.True(Dm.truthy(room.In(() => storage.hide_contents(alice))));
        room.In(() =>
        {
            Assert.Null(alice.active_storage);
            foreach (object element in Dm.each(ui))
                Assert.False(Dm.@in(element, alice.client.screen));
            Assert.False(Dm.@in(a, alice.client.screen));
            Assert.False(Dm.@in(b, alice.hud_used.screen_groups[HUD_GROUP_STORAGE]));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AClosetTakesOnCloseAndDumpsOnOpen()
    {
        var room = new CapsuleRoom(34);
        obj_structure_closet closet = room.In(() => Dm.@new<obj_structure_closet>(locate(7, 5, 1)));
        Assert.Equal(0f, room.In(() => closet.opened));
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(closet.open()))));
        Assert.Equal(1f, room.In(() => closet.opened));
        Assert.False(room.In(() => Dm.truthy(closet.density)));

        obj_item sock = Thing(room, 7, 5, WEIGHT_CLASS_SMALL);
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(closet.close()))));
        Assert.Same(closet, room.In(() => sock.loc));
        Assert.True(room.In(() => Dm.truthy(closet.density)));

        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(closet.toggle()))));
        Assert.Same(room.In(() => locate(7, 5, 1)), room.In(() => sock.loc));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AWeldedOrLockedClosetDoesNotOpenAndTheLockToggles()
    {
        var room = new CapsuleRoom(35);
        mob_living_carbon_human alice = room.Join<mob_living_carbon_human_test_p3_user>(5, 5, "Alice");
        obj_structure_closet closet = room.In(() => Dm.@new<obj_structure_closet>(locate(6, 5, 1)));

        room.In(() => closet.welded = TRUE);
        Assert.False(Dm.truthy(room.In(() => Waiting.NoSleep(closet.open(alice)))));
        Assert.Equal(0f, room.In(() => closet.opened));
        room.In(() => closet.welded = FALSE);

        room.In(() => { closet.secure = TRUE; closet.locked = TRUE; });
        Assert.False(Dm.truthy(room.In(() => closet.can_open(alice))));
        // No access requirement: tg's allowed() lets anyone unlock it.
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(closet.togglelock(alice)))));
        Assert.Equal(0f, room.In(() => closet.locked));
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(closet.open(alice)))));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMetaClosetFillsAtItsFirstOpen()
    {
        var room = new CapsuleRoom(36);
        obj_structure_closet_firecloset closet = room.In(() => Dm.@new<obj_structure_closet_firecloset>(locate(8, 5, 1)));
        Assert.Equal(0, room.In(() => (int)closet.contents.len));
        Assert.Equal(0f, room.In(() => closet.contents_initialized));
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(closet.open()))));
        Assert.Equal(1f, room.In(() => closet.contents_initialized));
        // tg's firecloset: a suit, a mask, a tank, an extinguisher, a hard hat (closets/utility_closets.dm).
        int dumped = room.In(() => Dm.each<obj_item>(locate(8, 5, 1)).Count());
        Assert.True(dumped >= 5, "firecloset dumped " + dumped);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMechanicalToolboxHasTgsTools()
    {
        var room = new CapsuleRoom(37);
        obj_item_storage_toolbox_mechanical box = room.In(() => Dm.@new<obj_item_storage_toolbox_mechanical>(locate(5, 6, 1)));
        Assert.NotNull(room.In(() => box.atom_storage));
        string[] got = room.In(() => Dm.each<obj_item>(box.contents).Select(i => i.GetType().Name).OrderBy(n => n).ToArray());
        Assert.Equal(new[] { "obj_item_analyzer", "obj_item_crowbar", "obj_item_screwdriver", "obj_item_weldingtool", "obj_item_wirecutters", "obj_item_wrench" }, got);
        room.AssertNoRuntimes();
    }
}

public class SurvivalBoxTests
{
    [Fact]
    public void TheSurvivalBoxGoesIntoAWornBackpackThroughTheStorageInsert()
    {
        var room = new CapsuleRoom(38);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        obj_item_storage_backpack pack = room.In(() => Dm.@new<obj_item_storage_backpack>(alice.loc));
        Assert.Equal(21f, room.In(() => pack.atom_storage.max_slots));
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(alice.equip_to_slot_if_possible(pack, ITEM_SLOT_BACK)))));
        obj_item_storage_box_survival box = room.In(() => Dm.@new<obj_item_storage_box_survival>(alice.loc));
        Assert.True(room.In(() => box.contents.len) >= 3); // tg's mask, internals tank and medipen
        Assert.True(Dm.truthy(room.In(() => alice.equip_to_storage(box, ITEM_SLOT_BACK, indirect_action: TRUE))));
        Assert.Same(pack, room.In(() => box.loc));
        room.AssertNoRuntimes();
    }
}

/// <summary>A stored item that answers on_found as tg's base does (P2's translation is not on this branch yet).</summary>
[DmPath("/obj/item/test_p3_stub")]
public class obj_item_test_p3_stub : obj_item
{
    public override async DmTask<object> on_found(mob finder = null) => null;
}

/// <summary>A user without an ID: get_idcard is M4a P5's; tg's answer for a mob with no card is null.</summary>
[DmPath("/mob/living/carbon/human/test_p3_user")]
public class mob_living_carbon_human_test_p3_user : mob_living_carbon_human
{
    public override object get_idcard(object hand_first = null) => null;
}
