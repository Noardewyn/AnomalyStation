using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aHands.P2;

/// <summary>
/// tg's hands and inventory (M4a P2: mob/inventory.dm, carbon and human inventory.dm, items.dm's pickup and drop, mob.dm's
/// hand procs) on a human in a T2 room, where a reached <c>Dm.NotTranslated</c> throws: put in hands, the active and the
/// other hand, swap, drop with tg's random offsets, and tg's checks (TRAIT_NODROP, MOBILITY_PICKUP).
/// </summary>
public class HandsTests
{
    internal static obj_item Item(T2Room r, int x, int y, string name = "thing") => r.In(() =>
    {
        obj_item it = Dm.@new<obj_item>(locate(x, y, 1));
        it.name = name;
        return it;
    });

    [Fact]
    public void PutInHandsFillsTheActiveHandThenTheOtherAndDropsWhenBothAreFull()
    {
        var r = new T2Room(4201);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        obj_item a = Item(r, 8, 8, "a"), b = Item(r, 8, 8, "b"), c = Item(r, 8, 8, "c");
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(a))));
            Assert.Same(a, bob.get_active_held_item());
            Assert.Same(bob, a.loc);
            Assert.True(Dm.truthy(Dm.bitand(a.item_flags, IN_INVENTORY)), "has_equipped sets IN_INVENTORY");
            Assert.Equal(ABOVE_HUD_PLANE, a.plane);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(b))));
            Assert.Same(b, bob.get_inactive_held_item());
            Assert.Equal(2f, Dm.num(bob.get_num_held_items()));
            Assert.Equal(FALSE, Dm.num(Waiting.NoSleep(bob.put_in_hands(c)))); // both hands full: dropped on the turf
            Assert.Same(bob.loc, c.loc);
            Assert.True(Dm.truthy(bob.is_holding_items()));
            Assert.Equal(1f, Dm.num(bob.is_holding(a)));
            Assert.Same(a, bob.is_holding_item_of_type(typeof(obj_item))); // the first held item, in held_items order
            Assert.Equal("left hand", bob.get_held_index_name(1));
            Assert.Equal("right hand", bob.get_held_index_name(2));
            Assert.Equal(BODY_ZONE_R_ARM, bob.get_hand_zone_of_item(b));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void DropItemToGroundPutsTheItemOnTheTurfWithTgsRandomOffsetsAndClearsTheHand()
    {
        var r = new T2Room(4202);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        obj_item a = Item(r, 8, 8);
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_hands(a));
            object dropped = Waiting.NoSleep(bob.dropItemToGround(a));
            Assert.Same(a, dropped);
            Assert.Same(bob.loc, a.loc);
            Assert.Null(bob.get_active_held_item());
            Assert.False(Dm.truthy(Dm.bitand(a.item_flags, IN_INVENTORY)), "doUnEquip clears IN_INVENTORY");
            Assert.InRange(a.pixel_x, -6f, 6f); // rand(-6, 6), inventory.dm:355
            Assert.InRange(a.pixel_y, -6f, 6f);
            Assert.Equal(Dm.num(Dm.initial(a, (obj_item p) => p.plane)), a.plane);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ANodropItemStaysInTheHandUnlessForced()
    {
        var r = new T2Room(4203);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        obj_item a = Item(r, 8, 8);
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_hands(a));
            ADD_TRAIT(a, TRAIT_NODROP, "test");
            Assert.Null(Waiting.NoSleep(bob.dropItemToGround(a)));
            Assert.Same(a, bob.get_active_held_item());
            Assert.Equal(FALSE, Dm.num(bob.canUnEquip(a)));
            Assert.Same(a, Waiting.NoSleep(bob.dropItemToGround(a, force: TRUE)));
            Assert.Same(bob.loc, a.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutMobilityPickupAHumanCannotPutAnItemInHand()
    {
        var r = new T2Room(4204);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        obj_item a = Item(r, 8, 8);
        r.In(() =>
        {
            bob.mobility_flags = Dm.bitand(bob.mobility_flags, Dm.bitnot(MOBILITY_PICKUP));
            Assert.Equal(FALSE, Dm.num(bob.put_in_hand_check(a)));
            Assert.Equal(FALSE, Dm.num(Waiting.NoSleep(bob.put_in_active_hand(a))));
            Assert.Same(bob.loc, a.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SwapHandChangesTheActiveHandAndTheSwappingSignalCanBlockIt()
    {
        var r = new T2Room(4205);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        r.In(() =>
        {
            Assert.Equal(1f, bob.active_hand_index);
            Assert.Equal(TRUE, Dm.num(bob.swap_hand()));
            Assert.Equal(2f, bob.active_hand_index);
            Assert.Equal(TRUE, Dm.num(bob.swap_hand()));
            Assert.Equal(1f, bob.active_hand_index); // (active_hand_index % held_items.len) + 1
            Assert.Equal(TRUE, Dm.num(bob.swap_hand(2)));
            Assert.Equal(2f, bob.active_hand_index);
            REMOVE_TRAIT(bob, TRAIT_CAN_HOLD_ITEMS, "test-none");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AttackHandOnAnItemOnTheFloorPicksItUpIntoTheActiveHand()
    {
        var r = new T2Room(4206);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        obj_item a = Item(r, 9, 8);
        r.In(() =>
        {
            Assert.Equal(FALSE, Dm.num(Waiting.NoSleep(a.attempt_pickup(bob)))); // . = FALSE once picked up (items.dm:587)
            Assert.Same(a, bob.get_active_held_item());
            Assert.Same(bob, a.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TransferItemToLocMovesTheItemOutOfTheHandIntoAnotherAtom()
    {
        var r = new T2Room(4207);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        obj_item a = Item(r, 8, 8), box = Item(r, 8, 8, "box");
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_hands(a));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.transferItemToLoc(a, box))));
            Assert.Same(box, a.loc);
            Assert.Null(bob.get_active_held_item());
        });
        r.AssertNoRuntimes();
    }
}
