// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/mine_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/modules/mining/mine_items.dm:39
public partial class obj_structure_closet_wardrobe_miner : obj_structure_closet_wardrobe
{
    // tg: code/modules/mining/mine_items.dm:39
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_storage_backpack_duffelbag_explorer>(this); // tg: code/modules/mining/mine_items.dm:40
        Dm.@new<obj_item_storage_backpack_explorer>(this); // tg: code/modules/mining/mine_items.dm:41
        Dm.@new<obj_item_storage_backpack_satchel_explorer>(this); // tg: code/modules/mining/mine_items.dm:42
        Dm.@new<obj_item_storage_backpack_messenger_explorer>(this); // tg: code/modules/mining/mine_items.dm:43
        Dm.@new<obj_item_clothing_under_rank_cargo_miner_lavaland>(this); // tg: code/modules/mining/mine_items.dm:44
        Dm.@new<obj_item_clothing_under_rank_cargo_miner_lavaland>(this); // tg: code/modules/mining/mine_items.dm:45
        Dm.@new<obj_item_clothing_under_rank_cargo_miner_lavaland>(this); // tg: code/modules/mining/mine_items.dm:46
        Dm.@new<obj_item_clothing_shoes_workboots_mining>(this); // tg: code/modules/mining/mine_items.dm:47
        Dm.@new<obj_item_clothing_shoes_workboots_mining>(this); // tg: code/modules/mining/mine_items.dm:48
        Dm.@new<obj_item_clothing_shoes_workboots_mining>(this); // tg: code/modules/mining/mine_items.dm:49
        Dm.@new<obj_item_clothing_gloves_color_black>(this); // tg: code/modules/mining/mine_items.dm:50
        Dm.@new<obj_item_clothing_gloves_color_black>(this); // tg: code/modules/mining/mine_items.dm:51
        Dm.@new<obj_item_clothing_gloves_color_black>(this); // tg: code/modules/mining/mine_items.dm:52
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_miner>(this); // tg: code/modules/mining/mine_items.dm:53
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_miner>(this); // tg: code/modules/mining/mine_items.dm:54
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_miner>(this); // tg: code/modules/mining/mine_items.dm:55
    }
}

// tg: code/modules/mining/mine_items.dm:65
public partial class obj_structure_closet_secure_closet_miner : obj_structure_closet_secure_closet
{
    // tg: code/modules/mining/mine_items.dm:65
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_stack_sheet_mineral_sandbags>(this, 5);
        Dm.@new<obj_item_storage_box_emptysandbags>(this);
        Dm.@new<obj_item_card_mining_point_card>(this);
        Dm.@new<obj_item_shovel>(this);
        Dm.@new<obj_item_pickaxe_mini>(this);
        Dm.@new<obj_item_radio_headset_headset_cargo_mining>(this);
        Dm.@new<obj_item_flashlight_seclite>(this);
        Dm.@new<obj_item_storage_bag_plants>(this);
        Dm.@new<obj_item_storage_bag_ore>(this);
        Dm.@new<obj_item_mining_scanner>(this);
        Dm.@new<obj_item_clothing_glasses_meson>(this);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_SMALLER_PODS))
            Dm.@new<obj_item_survivalcapsule_bathroom>(this);
        else
            Dm.@new<obj_item_survivalcapsule>(this);
        Dm.@new<obj_item_assault_pod_mining>(this);
    }

    // tg: code/modules/mining/mine_items.dm:85
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        Dm.@new<obj_item_gun_energy_recharge_kinetic_accelerator>(this);
    }
}

// tg: code/modules/mining/mine_items.dm:92
public partial class obj_machinery_computer_shuttle_mining : obj_machinery_computer_shuttle
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/mining/mine_items.dm:101
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (HAS_TRAIT(user, TRAIT_FORBID_MINING_SHUTTLE_CONSOLE_OUTSIDE_STATION) && !Dm.truthy(is_station_level(user.z)))
        {
            to_chat(user, span_warning("You get the feeling you shouldn't mess with this."));
            return null;
        }

        if (user is mob_living living_user)
        {
            foreach (obj_item_implant_exile exile_implant in Dm.each<obj_item_implant_exile>(living_user.implants))
            {
                to_chat(living_user, span_warning("A warning flashes across the screen, and the shuttle controls lock in response to your exile implant."));
                return null;
            }
        }

        return await base.attack_hand(user, modifiers, __extra);
    }
}
