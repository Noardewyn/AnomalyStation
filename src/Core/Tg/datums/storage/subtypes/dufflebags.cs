// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/dufflebags.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///Normal duffle bag
// tg: code/datums/storage/subtypes/dufflebags.dm:2
public partial class datum_storage_duffel : datum_storage
{
    // tg: code/datums/storage/subtypes/dufflebags.dm:6
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(exception_hold_list: typeof(obj_item_fish_tank));
    }
}

// Syndi bags get some FUN extras
// You can fit any 2 bulky objects (assuming they're in the whitelist)
// Should have traitorus stuff in here, not just useful big things
// Idea is to allow for things we typically restrict in exchange for going loud
// tg: code/datums/storage/subtypes/dufflebags.dm:19
public partial class datum_storage_duffel_syndicate : datum_storage_duffel
{
    // tg: code/datums/storage/subtypes/dufflebags.dm:23
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // var/static in DM: a constant list of paths, rebuilt per New here (set_holdable caches it by its joined text, so the result is the same).
        DmList exception_type_list = Dm.list(
            // Gun and gun-related accessories
            typeof(obj_item_gun),
            typeof(obj_item_pneumatic_cannon),
            // Melee
            typeof(obj_item_kinetic_crusher), //mostly
            typeof(obj_item_dualsaber),
            typeof(obj_item_staff_bostaff),
            typeof(obj_item_fireaxe),
            typeof(obj_item_crowbar_mechremoval),
            typeof(obj_item_spear),
            typeof(obj_item_nullrod),
            typeof(obj_item_melee_cleric_mace),
            typeof(obj_item_melee_ghost_sword),
            typeof(obj_item_melee_cleaving_saw),
            // Deployables
            typeof(obj_item_transfer_valve),
            typeof(obj_item_powersink),
            typeof(obj_item_deployable_turret_folded),
            typeof(obj_item_cardboard_cutout),
            typeof(obj_item_gibtonite),
            // Sustenance
            typeof(obj_item_food_cheese_royal),
            typeof(obj_item_food_powercrepe),
            // Back Items
            typeof(obj_item_tank_jetpack),
            typeof(obj_item_watertank),
            // Skub
            typeof(obj_item_skub),
            // Bulky Supplies
            typeof(obj_item_mecha_ammo),
            typeof(obj_item_golem_shell),
            // Clothing
            typeof(obj_item_clothing_shoes_winterboots_ice_boots_eva),
            typeof(obj_item_clothing_suit_space),
            typeof(obj_item_clothing_suit_armor_heavy),
            typeof(obj_item_clothing_suit_bio_suit),
            typeof(obj_item_clothing_suit_utility),
            // Storage
            typeof(obj_item_storage_bag_money),
            // Heads!
            typeof(obj_item_bodypart_head),
            // Fish
            typeof(obj_item_fish),
            typeof(obj_item_fish_tank)
        );

        set_holdable(exception_hold_list: exception_type_list);

        //...So we can run this without it generating a line for every subtype.
        DmList desc = Dm.list();
        foreach (object valid_item in Dm.each(exception_type_list))
            Dm.append(desc, I18n.T("\\a []", Dm.initial(valid_item, "name")));
        can_hold_description = Dm.text("\n\t[]", span_notice(desc.Join("\n\t")));
    }
}
