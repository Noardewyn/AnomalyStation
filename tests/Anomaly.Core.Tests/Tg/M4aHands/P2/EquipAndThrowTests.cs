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
/// tg's equip path per human slot (equip_to_slot_if_possible, mob_can_equip with the slot flags, human equip_to_slot and
/// doUnEquip) and tg's throwing (throw_item, throw_at, SSthrowing's tick, finalize, throw_impact and the hitby chain) in a
/// T2 room, where a reached <c>Dm.NotTranslated</c> throws.
/// </summary>
public class EquipAndThrowTests
{
    private static obj_item Worn(T2Room r, mob_living_carbon_human who, float slot, string name) => r.In(() =>
    {
        obj_item it = Dm.@new<obj_item>(who.loc);
        it.name = name;
        it.slot_flags = slot;
        it.w_class = WEIGHT_CLASS_SMALL;
        return it;
    });

    public static IEnumerable<object[]> Slots() => new[]
    {
        new object[] { ITEM_SLOT_FEET, "shoes" },
        new object[] { ITEM_SLOT_GLOVES, "gloves" },
        new object[] { ITEM_SLOT_EARS, "ears" },
        new object[] { ITEM_SLOT_EYES, "glasses" },
        new object[] { ITEM_SLOT_HEAD, "head" },
        new object[] { ITEM_SLOT_MASK, "wear_mask" },
        new object[] { ITEM_SLOT_NECK, "wear_neck" },
        new object[] { ITEM_SLOT_BACK, "back" },
        new object[] { ITEM_SLOT_OCLOTHING, "wear_suit" },
        new object[] { ITEM_SLOT_BELT, "belt" },
        new object[] { ITEM_SLOT_ID, "wear_id" },
        new object[] { ITEM_SLOT_LPOCKET, "l_store" },
        new object[] { ITEM_SLOT_RPOCKET, "r_store" },
    };

    [Theory]
    [MemberData(nameof(Slots))]
    public void AnItemEquipsIntoItsSlotOverAJumpsuitAndDropsBackToTheFloor(float slot, string field)
    {
        var r = new T2Room(4301);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        obj_item suit = Worn(r, bob, ITEM_SLOT_ICLOTHING, "jumpsuit");
        obj_item it = Worn(r, bob, slot, field);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING))), "the jumpsuit");
            Assert.Same(suit, bob.w_uniform);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(it, slot))), field);
            Assert.Same(it, Dm.get_var(bob, field));
            Assert.Same(bob, it.loc);
            Assert.Equal(slot, Dm.num(bob.get_slot_by_item(it)));
            Assert.Same(it, bob.get_item_by_slot(slot));
            Assert.Same(it, Waiting.NoSleep(bob.dropItemToGround(it)));
            Assert.Null(Dm.get_var(bob, field));
            Assert.Same(bob.loc, it.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemWithoutTheSlotFlagIsRefusedByMobCanEquip()
    {
        var r = new T2Room(4302);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        obj_item it = Worn(r, bob, ITEM_SLOT_HEAD, "hat");
        r.In(() =>
        {
            Assert.Equal(FALSE, Dm.num(Waiting.NoSleep(bob.equip_to_slot_if_possible(it, ITEM_SLOT_FEET, disable_warning: TRUE))));
            Assert.Null(bob.shoes);
            Assert.Same(bob.loc, it.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TakingOffTheJumpsuitDropsWhatNeedsIt()
    {
        var r = new T2Room(4303);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        obj_item suit = Worn(r, bob, ITEM_SLOT_ICLOTHING, "jumpsuit");
        obj_item pocket = Worn(r, bob, ITEM_SLOT_LPOCKET, "pocket");
        r.In(() =>
        {
            Waiting.NoSleep(bob.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING));
            Waiting.NoSleep(bob.equip_to_slot_if_possible(pocket, ITEM_SLOT_LPOCKET));
            Assert.Same(pocket, bob.l_store);
            Waiting.NoSleep(bob.dropItemToGround(suit));
            Assert.Null(bob.w_uniform);
            Assert.Null(bob.l_store); // human doUnEquip: the pockets cannot stay without the jumpsuit (inventory.dm:241)
            Assert.Same(bob.loc, pocket.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AThrownItemFliesTgsTilesAndLandsOnTheTargetTurf()
    {
        var r = new T2Room(4304);
        mob_living_carbon_human bob = r.Join(3, 8, "Bob", name: "bob");
        obj_item it = HandsTests.Item(r, 3, 8, "rock");
        turf target = r.In(() => locate(7, 8, 1));
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_hands(it));
            bob.throw_mode_on(THROW_MODE_TOGGLE);
            Assert.Equal(THROW_MODE_TOGGLE, bob.throw_mode);
        });
        bool flew = false;
        r.Command(() => Waiting.Detach(bob.throw_item(target)));
        for (int i = 0; i < 30; i++)
        {
            r.Step();
            flew |= r.In(() => it.throwing != null);
        }
        Assert.True(flew, "the item was thrown (throwing set while it flew: the Thrown flag)");
        r.In(() =>
        {
            Assert.Null(it.throwing);
            Assert.Same(target, it.loc);
            Assert.Null(bob.get_active_held_item());
            Assert.Equal(THROW_MODE_DISABLED, bob.throw_mode); // throw_item turns throw mode off
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemThrownAtAMobHitsItWhereItStands()
    {
        var r = new T2Room(4305);
        mob_living_carbon_human bob = r.Join(3, 8, "Bob", name: "bob");
        mob_living_carbon_human dummy = r.Dummy(5, 8, name: "dummy");
        obj_item it = HandsTests.Item(r, 3, 8, "rock");
        var impacts = new List<object>();
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_hands(it));
            // Not dense, so the flight crosses the mob's tile and tg's tick hits its intended target there (throwing.dm:190).
            // A dense mob is reached through /mob/living/CanAllowThrough, whose thrower GAP (living_movement.dm:63, not P2's
            // file) is reported to the lead; a hurting throw (throwforce > 0) reaches run_armor_check, NotTranslated and in
            // no M4a list (reported). The rock's throwforce is 0: tg's hitby shows the hit and returns before ..().
            dummy.density = FALSE;
            Anomaly.Core.Tests.Tg.Wave4.Integration.Toy.On(it, COMSIG_MOVABLE_IMPACT, "impact", 3, act: a => impacts.AddRange(a));
        });
        r.Command(() => Waiting.Detach(bob.throw_item(dummy)));
        r.Step(30);
        r.In(() =>
        {
            Assert.True(it.throwing == null, $"still thrown at {it.loc?.x},{it.loc?.y}");
            Assert.True(impacts.Contains(dummy), "throw_impact hit the mob (COMSIG_MOVABLE_IMPACT with the mob)");
            Assert.Same(dummy.loc, it.loc); // the flight ended on the mob's tile
        });
        r.AssertNoRuntimes();
    }
}
