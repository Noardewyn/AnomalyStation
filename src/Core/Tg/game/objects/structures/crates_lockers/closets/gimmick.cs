// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/gimmick.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The PopulateContents of Meta's mini-fridges, so they initialize as closets (D112) and show their door from the start.

// tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:133
public partial class obj_structure_closet_mini_fridge : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:133
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_effect_spawner_random_food_or_drink_refreshing_beverage>(this);
        Dm.@new<obj_effect_spawner_random_food_or_drink_refreshing_beverage>(this);
        if (prob(50))
            Dm.@new<obj_effect_spawner_random_food_or_drink_refreshing_beverage>(this);
        if (prob(40))
            Dm.@new<obj_item_reagent_containers_cup_glass_bottle_beer>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:146
public partial class obj_structure_closet_mini_fridge_grimy : obj_structure_closet_mini_fridge
{
    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:146
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        if (prob(40))
        {
            if (prob(50))
                Dm.@new<obj_item_food_pizzaslice_moldy_bacteria>(this);
            else
                Dm.@new<obj_item_food_breadslice_moldy_bacteria>(this);
        }
        else if (prob(40))
        {
            if (prob(50))
                Dm.@new<obj_item_food_syndicake>(this);
            else
                Dm.@new<mob_living_basic_cockroach>(this);
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:28  (gaps ST3, D333)
public partial class obj_structure_closet_gimmick_russian : obj_structure_closet_gimmick
{
    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:31
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:32
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:33
        {
            Dm.@new<obj_item_clothing_head_costume_ushanka>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:34
        }
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:35
        {
            Dm.@new<obj_item_clothing_under_costume_soviet>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:36
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:39  (gaps ST3, D333)
public partial class obj_structure_closet_gimmick_tacticool : obj_structure_closet_gimmick
{
    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:42
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:43
        Dm.@new<obj_item_clothing_glasses_eyepatch>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:44
        Dm.@new<obj_item_clothing_gloves_tackler_combat>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:45
        Dm.@new<obj_item_clothing_gloves_tackler_combat>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:46
        Dm.@new<obj_item_clothing_head_helmet_swat>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:47
        Dm.@new<obj_item_clothing_head_helmet_swat>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:48
        Dm.@new<obj_item_clothing_mask_gas_sechailer_swat>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:49
        Dm.@new<obj_item_clothing_mask_gas_sechailer_swat>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:50
        Dm.@new<obj_item_clothing_shoes_combat_swat>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:51
        Dm.@new<obj_item_clothing_shoes_combat_swat>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:52
        Dm.@new<obj_item_mod_control_pre_equipped_apocryphal>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:53
        Dm.@new<obj_item_mod_control_pre_equipped_apocryphal>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:54
        Dm.@new<obj_item_clothing_under_syndicate_tacticool>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:55
        Dm.@new<obj_item_clothing_under_syndicate_tacticool>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:56
    }

    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:58
    public override async DmTask populate_contents_immediate()
    {
        Dm.@new<obj_item_clothing_glasses_sunglasses>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:59
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:67  (gaps ST3, D333)
public partial class obj_structure_closet_thunderdome_tdred : obj_structure_closet_thunderdome
{
    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:70
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:71
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:72
        {
            Dm.@new<obj_item_clothing_suit_armor_tdome_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:73
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:74
        {
            Dm.@new<obj_item_melee_energy_sword_saber>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:75
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:76
        {
            Dm.@new<obj_item_melee_baton_security_loaded>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:77
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:78
        {
            Dm.@new<obj_item_storage_box_flashbangs>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:79
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:80
        {
            Dm.@new<obj_item_clothing_head_helmet_thunderdome>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:81
        }
    }

    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:83
    public override async DmTask populate_contents_immediate()
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:84
        {
            Dm.@new<obj_item_gun_energy_laser>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:85
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:88  (gaps ST3, D333)
public partial class obj_structure_closet_thunderdome_tdgreen : obj_structure_closet_thunderdome
{
    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:91
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:92
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:93
        {
            Dm.@new<obj_item_clothing_suit_armor_tdome_green>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:94
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:95
        {
            Dm.@new<obj_item_melee_energy_sword_saber>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:96
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:97
        {
            Dm.@new<obj_item_melee_baton_security_loaded>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:98
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:99
        {
            Dm.@new<obj_item_storage_box_flashbangs>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:100
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:101
        {
            Dm.@new<obj_item_clothing_head_helmet_thunderdome>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:102
        }
    }

    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:104
    public override async DmTask populate_contents_immediate()
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:105
        {
            Dm.@new<obj_item_gun_energy_laser>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:106
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:109  (gaps ST3, D333)
public partial class obj_structure_closet_malf_suits : obj_structure_closet_malf
{
    // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:112
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:113
        Dm.@new<obj_item_tank_jetpack_void>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:114
        Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:115
        Dm.@new<obj_item_clothing_head_helmet_space_nasavoid>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:116
        Dm.@new<obj_item_clothing_suit_space_nasavoid>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:117
        Dm.@new<obj_item_crowbar>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:118
        Dm.@new<obj_item_stock_parts_power_store_cell>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:119
        Dm.@new<obj_item_multitool>(this); // tg: code/game/objects/structures/crates_lockers/closets/gimmick.dm:120
    }
}
