// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/clothing_drops_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// PARTIAL (M4a clothing): test_human, in tg's order. test_android needs /datum/species/android (the other species are
// translated with them, M9): it is left out.
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install).

/// Tests that removing a piece of clothing drops items that hold said piece of clothing
// tg: code/modules/unit_tests/clothing_drops_items.dm:2 (/datum/unit_test/clothing_drops_items; class named by rules 2.3)
public sealed class datum_unit_test_clothing_drops_items : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/clothing_drops_items.dm:4
    public override async DmTask Run()
    {
        await test_human();
        // test_android(): left out (/datum/species/android, see the header)
    }

    // tg: code/modules/unit_tests/clothing_drops_items.dm:8
    private async DmTask test_human()
    {
        DmList dummy_items = allocate_items();
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();

        foreach (object slot in Dm.each(dummy_items))
        {
            TEST_ASSERT(Dm.truthy(await dummy.equip_to_slot_if_possible(dummy_items[slot] as obj_item, text2num(slot))),
                Dm.text("[] Dummy failed to equip one of the starting items ([]). Test aborted.", Dm.initial(typeof(datum_species_human), (datum_species p) => p.name), dummy_items[slot]));
        }

        await dummy.dropItemToGround(dummy.w_uniform);

        foreach (object slot in Dm.each(dummy_items))
        {
            obj_item item = dummy_items[slot] as obj_item;
            if (Dm.truthy(Dm.bitand(item.slot_flags, ITEM_SLOT_ICLOTHING)))
                continue;
            else if (Dm.truthy(Dm.bitand(item.slot_flags, Dm.bitor(ITEM_SLOT_BACK, ITEM_SLOT_FEET))))
                TEST_ASSERT_EQUAL(item.loc, dummy, Dm.text("[] should not have been dropped when unequipping the jumpsuit from \\a [].", item, Dm.initial(typeof(datum_species_human), (datum_species p) => p.name)));
            else
                TEST_ASSERT_EQUAL(item.loc, dummy.loc, Dm.text("[] should have been dropped when unequipping the jumpsuit from \\a [].", item, Dm.initial(typeof(datum_species_human), (datum_species p) => p.name)));
        }
    }

    // tg: code/modules/unit_tests/clothing_drops_items.dm:43
    private DmList allocate_items()
    {
        return Dm.list(
            Dm.kv(Dm.text("[]", ITEM_SLOT_ICLOTHING), allocate(typeof(obj_item_clothing_under_color_rainbow))), // do this one first, it holds everything
            Dm.kv(Dm.text("[]", ITEM_SLOT_FEET), allocate(typeof(obj_item_clothing_shoes_jackboots))),
            Dm.kv(Dm.text("[]", ITEM_SLOT_BELT), allocate(typeof(obj_item_storage_belt_utility))),
            Dm.kv(Dm.text("[]", ITEM_SLOT_BACK), allocate(typeof(obj_item_storage_backpack))),
            Dm.kv(Dm.text("[]", ITEM_SLOT_ID), allocate(typeof(obj_item_card_id_advanced_gold_captains_spare))),
            Dm.kv(Dm.text("[]", ITEM_SLOT_RPOCKET), allocate(typeof(obj_item_assembly_flash_handheld))),
            Dm.kv(Dm.text("[]", ITEM_SLOT_LPOCKET), allocate(typeof(obj_item_toy_plush_lizard_plushie))));
    }
}
