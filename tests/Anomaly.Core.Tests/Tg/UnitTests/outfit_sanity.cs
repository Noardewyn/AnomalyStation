// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/outfit_sanity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M8 slice F, closing D180): the room is the toy room, a strict world (a reached Dm.NotTranslated throws), and the outfits checked
// are the station jobs' (/datum/outfit/job and its subtypes: security with its mindshield, the chips of the engineers, doctors, cooks
// and the rest); the other outfits of tg (antagonists, event roles, ghost roles) dress with items the port does not carry. Left out of
// the job set, with the reasons in docs/ai/specs/not-translated.md section 55: the gimmick assistants (large worn sprites, M4a rows) and
// the bitrunning domain's outfit (the MODsuit outfits are checked since gaps MO1, which made the MODsuits live); the duffel bag user
// (duffel bags are an inert type, their storage waits); and the suit_store check of the two miner outfits whose explorer suit takes
// its allowed list from GLOB.mining_suit_allowed in its own Initialize (explorer_gear.dm is inert: mining, M10). The
// "Mr. Runtime" outfits (stacks in hands, duffel, satchel and backpack users) and the final backpack_contents and belt_contents
// checks are tg's. EQUIP_OUTFIT_ITEM's whole path is also run: equip() itself, which implants and activates the chips.

/// See #66313 and #60901. outfit_sanity used to runtime whenever you had two mergable sheets in either hand. Previously, this only had a 3% chance of occuring. Now 100%.
// tg: code/modules/unit_tests/outfit_sanity.dm:15 (/datum/outfit/stacks_in_hands)
public sealed class datum_outfit_stacks_in_hands : datum_outfit
{
    public datum_outfit_stacks_in_hands()
    {
        name = "Mr. Runtime";
        uniform = typeof(obj_item_clothing_under_suit_tuxedo);
        glasses = typeof(obj_item_clothing_glasses_sunglasses);
        mask = typeof(obj_item_cigarette_cigar_havana);
        shoes = typeof(obj_item_clothing_shoes_laceup);
        l_hand = typeof(obj_item_stack_spacecash_c1000);
        r_hand = typeof(obj_item_stack_spacecash_c1000);
    }
}

/// outfit_sanity needs to cover insertions into duffelbags
// tg: code/modules/unit_tests/outfit_sanity.dm:26 (/datum/outfit/duffel_user)
public sealed class datum_outfit_duffel_user : datum_outfit
{
    public datum_outfit_duffel_user()
    {
        name = "Mr. Runtime";
        back = typeof(obj_item_storage_backpack_duffelbag);
        backpack_contents = Dm.list(Dm.kv(typeof(obj_item_cigarette_cigar_havana), 1));
    }
}

/// Satchels too
// tg: code/modules/unit_tests/outfit_sanity.dm:32 (/datum/outfit/stachel_user)
public sealed class datum_outfit_stachel_user : datum_outfit
{
    public datum_outfit_stachel_user()
    {
        name = "Mr. Runtime";
        back = typeof(obj_item_storage_backpack_satchel);
        backpack_contents = Dm.list(Dm.kv(typeof(obj_item_cigarette_cigar_havana), 1));
    }
}

/// And just in case we'll check backpacks
// tg: code/modules/unit_tests/outfit_sanity.dm:38 (/datum/outfit/backpack_user)
public sealed class datum_outfit_backpack_user : datum_outfit
{
    public datum_outfit_backpack_user()
    {
        name = "Mr. Runtime";
        back = typeof(obj_item_storage_backpack);
        backpack_contents = Dm.list(Dm.kv(typeof(obj_item_cigarette_cigar_havana), 1));
    }
}

// tg: code/modules/unit_tests/outfit_sanity.dm:44 (/datum/unit_test/outfit_sanity)
public sealed class datum_unit_test_outfit_sanity : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        TgStrings.Mount(DmWorldState.Of(World.Current).Files, Anomaly.Core.Tests.Tools.TgClone.Root); // J2: the french beret and the italian moustache load their speech at init
        if (SSjob == null)
            Dm.@new<datum_controller_subsystem_job>(); // the cook's pre_equip reads SSjob (no cook was hired here: tg's first cook, the chef)
    }

    // CHECK_OUTFIT_SLOT(outfit_key, slot_name) (outfit_sanity.dm:1-12), a file-local macro (#undef at the end of the file) that reads the
    // test's H and outfit: a private member with them as parameters (rules 11.4, 11.1).
    private async DmTask CHECK_OUTFIT_SLOT(mob_living_carbon_human H, datum_outfit outfit, Type item_type, string outfit_key, float slot_name)
    {
        if (item_type == null)
            return;
        await H.equip_to_slot_or_del(Dm.@new(item_type, H) as obj_item, slot_name, TRUE);
        /* We don't check the result of equip_to_slot_or_del because it returns false for random jumpsuits, as they delete themselves on init */
        obj_item outfit_item = H.get_item_by_slot(slot_name) as obj_item;
        if (outfit_item == null && slot_name == ITEM_SLOT_SUITSTORE && NoSuitStoreAllowed.Contains((string)outfit.name))
            return;
        if (outfit_item == null)
            TEST_FAIL(Dm.text("[]'s [] is invalid! Could not equip a [] into that slot.", outfit.name, outfit_key, item_type));
        else
            await outfit_item.on_outfit_equip(H, FALSE, slot_name);
    }

    internal static bool OutsideScope(Type outfit_type, datum_outfit outfit)
        => outfit_type.Name.Contains("assistant_gimmick", StringComparison.Ordinal) || outfit_type.Name.Contains("virtual_domain", StringComparison.Ordinal);

    // the miner outfits whose explorer suit has no allowed list yet (its Initialize reads GLOB.mining_suit_allowed; explorer_gear.dm, mining M10)
    private static readonly HashSet<string> NoSuitStoreAllowed = new() { "Shaft Miner (Equipment)", "Shaft Miner (Combat-Ready)" };

    // tg: code/modules/unit_tests/outfit_sanity.dm:46
    public override async DmTask Run()
    {
        datum_outfit prototype_outfit = Dm.@new<datum_outfit>();
        string prototype_name = prototype_outfit.name;
        mob_living_carbon_human H = allocate<mob_living_carbon_human_consistent>();

        // the station jobs' outfits and tg's own test outfits
        var outfits_to_check = Dm.each_cast<Type>(subtypesof(typeof(datum_outfit_job))).ToList();
        outfits_to_check.Add(typeof(datum_outfit_job));
        outfits_to_check.AddRange(new[] { typeof(datum_outfit_stacks_in_hands), typeof(datum_outfit_stachel_user), typeof(datum_outfit_backpack_user) });

        foreach (Type outfit_type in outfits_to_check)
        {
            // Only make one human and keep undressing it because it's much faster
            foreach (obj_item I in Dm.each<obj_item>(H.get_equipped_items(INCLUDE_POCKETS | INCLUDE_ABSTRACT)))
                qdel(I);

            datum_outfit outfit = Dm.@new(outfit_type) as datum_outfit;
            if (OutsideScope(outfit_type, outfit))
                continue;

            if (Dm.eq(outfit.name, prototype_name) && outfit_type != typeof(datum_outfit_job))
                TEST_FAIL(Dm.text("[]'s name is invalid! Uses default outfit name!", outfit.type));
            try
            {
            await outfit.pre_equip(H, TRUE);

            await CHECK_OUTFIT_SLOT(H, outfit, outfit.uniform, "uniform", ITEM_SLOT_ICLOTHING);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.suit, "suit", ITEM_SLOT_OCLOTHING);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.belt, "belt", ITEM_SLOT_BELT);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.gloves, "gloves", ITEM_SLOT_GLOVES);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.shoes, "shoes", ITEM_SLOT_FEET);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.head, "head", ITEM_SLOT_HEAD);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.mask, "mask", ITEM_SLOT_MASK);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.neck, "neck", ITEM_SLOT_NECK);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.ears, "ears", ITEM_SLOT_EARS);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.glasses, "glasses", ITEM_SLOT_EYES);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.back, "back", ITEM_SLOT_BACK);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.id, "id", ITEM_SLOT_ID);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.l_pocket, "l_pocket", ITEM_SLOT_LPOCKET);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.r_pocket, "r_pocket", ITEM_SLOT_RPOCKET);
            await CHECK_OUTFIT_SLOT(H, outfit, outfit.suit_store, "suit_store", ITEM_SLOT_SUITSTORE);
            if (Dm.truthy(outfit.backpack_contents) || outfit.box != null)
            {
                DmList backpack_contents = outfit.backpack_contents?.Copy();
                if (outfit.box != null)
                {
                    if (!Dm.truthy(backpack_contents))
                        backpack_contents = Dm.list();
                    backpack_contents.Insert(1, outfit.box);
                    backpack_contents[outfit.box] = 1;
                }

                foreach (Type path in Dm.each_cast<Type>(backpack_contents))
                {
                    float number = Dm.num(Dm.truthy(backpack_contents[path]) ? backpack_contents[path] : 1);
                    for (float _ = 1; _ <= number; _++)
                    {
                        if (!Dm.truthy(H.equip_to_storage(Dm.@new(path, H) as obj_item, ITEM_SLOT_BACK, indirect_action: TRUE, del_on_fail: TRUE)))
                            TEST_FAIL(Dm.text("[]'s backpack_contents are invalid! Couldn't add [] to backpack.", Dm.text("[] ([])", outfit.name, outfit.type), path));
                    }
                }
            }

            if (Dm.truthy(outfit.belt_contents))
            {
                DmList belt_contents = outfit.belt_contents?.Copy();
                foreach (Type path in Dm.each_cast<Type>(belt_contents))
                {
                    float number = Dm.num(Dm.truthy(belt_contents[path]) ? belt_contents[path] : 1);
                    for (float _ = 1; _ <= number; _++)
                    {
                        if (!Dm.truthy(H.equip_to_storage(Dm.@new(path, H) as obj_item, ITEM_SLOT_BELT, indirect_action: TRUE, del_on_fail: TRUE)))
                            TEST_FAIL(Dm.text("[]'s belt_contents are invalid! Couldn't add [] to backpack.", outfit.name, path));
                    }
                }
            }
            }
            catch (DmNotTranslatedException e)
            {
                TEST_FAIL(Dm.text("[] reached a not-translated leaf: []", outfit.name, e.Message));
            }
        }
    }
}
