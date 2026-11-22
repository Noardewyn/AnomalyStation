// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/job_closets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J5 (D298): the wardrobes of medical, science and engineering.

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:155  (gaps J5, D298)
public partial class obj_structure_closet_wardrobe_atmospherics_yellow : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:155
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_clothing_accessory_pocketprotector), 1),
            Dm.kv(typeof(obj_item_storage_backpack_duffelbag_engineering), 1),
            Dm.kv(typeof(obj_item_storage_backpack_satchel_eng), 1),
            Dm.kv(typeof(obj_item_storage_backpack_industrial), 1),
            Dm.kv(typeof(obj_item_storage_backpack_messenger_eng), 1),
            Dm.kv(typeof(obj_item_clothing_suit_atmos_overalls), 3),
            Dm.kv(typeof(obj_item_clothing_suit_hooded_wintercoat_engineering_atmos), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_engineering_atmospheric_technician), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_sneakers_black), 3)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:173  (gaps J5, D298)
public partial class obj_structure_closet_wardrobe_engineering_yellow : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:173
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_clothing_accessory_pocketprotector), 1),
            Dm.kv(typeof(obj_item_storage_backpack_duffelbag_engineering), 1),
            Dm.kv(typeof(obj_item_storage_backpack_industrial), 1),
            Dm.kv(typeof(obj_item_storage_backpack_satchel_eng), 1),
            Dm.kv(typeof(obj_item_storage_backpack_messenger_eng), 1),
            Dm.kv(typeof(obj_item_clothing_suit_hooded_wintercoat_engineering), 1),
            Dm.kv(typeof(obj_item_clothing_under_rank_engineering_engineer), 3),
            Dm.kv(typeof(obj_item_clothing_suit_hazardvest), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_workboots), 3),
            Dm.kv(typeof(obj_item_clothing_head_utility_hardhat), 3)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:192  (gaps J5, D298)
public partial class obj_structure_closet_wardrobe_white_medical : obj_structure_closet_wardrobe_white
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:192
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_clothing_accessory_pocketprotector), 1),
            Dm.kv(typeof(obj_item_storage_backpack_duffelbag_med), 1),
            Dm.kv(typeof(obj_item_storage_backpack_medic), 1),
            Dm.kv(typeof(obj_item_storage_backpack_satchel_med), 1),
            Dm.kv(typeof(obj_item_storage_backpack_messenger_med), 1),
            Dm.kv(typeof(obj_item_clothing_suit_hooded_wintercoat_medical), 1),
            Dm.kv(typeof(obj_item_clothing_head_costume_nursehat), 1),
            Dm.kv(typeof(obj_item_clothing_under_rank_medical_scrubs_blue), 1),
            Dm.kv(typeof(obj_item_clothing_under_rank_medical_scrubs_green), 1),
            Dm.kv(typeof(obj_item_clothing_under_rank_medical_scrubs_purple), 1),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_labcoat), 3),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_labcoat_paramedic), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_sneakers_white), 3),
            Dm.kv(typeof(obj_item_clothing_head_soft_paramedic), 3)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:235  (gaps J5, D298)
public partial class obj_structure_closet_wardrobe_chemistry_white : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:235
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_clothing_under_rank_medical_chemist), 2),
            Dm.kv(typeof(obj_item_clothing_shoes_sneakers_white), 2),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_labcoat_chemist), 2),
            Dm.kv(typeof(obj_item_storage_backpack_chemistry), 2),
            Dm.kv(typeof(obj_item_storage_backpack_satchel_chem), 2),
            Dm.kv(typeof(obj_item_storage_backpack_messenger_chem), 2),
            Dm.kv(typeof(obj_item_storage_backpack_duffelbag_chemistry), 2),
            Dm.kv(typeof(obj_item_storage_bag_chemistry), 2)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:253  (gaps J5, D298)
public partial class obj_structure_closet_wardrobe_genetics_white : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:253
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_clothing_under_rank_rnd_geneticist), 2),
            Dm.kv(typeof(obj_item_clothing_shoes_sneakers_white), 2),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_labcoat_genetics), 2),
            Dm.kv(typeof(obj_item_storage_backpack_genetics), 2),
            Dm.kv(typeof(obj_item_storage_backpack_satchel_gen), 2),
            Dm.kv(typeof(obj_item_storage_backpack_messenger_gen), 2),
            Dm.kv(typeof(obj_item_storage_backpack_duffelbag_genetics), 2)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:270  (gaps J5, D298)
public partial class obj_structure_closet_wardrobe_virology_white : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:270
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_clothing_under_rank_medical_virologist), 2),
            Dm.kv(typeof(obj_item_clothing_shoes_sneakers_white), 2),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_labcoat_virologist), 2),
            Dm.kv(typeof(obj_item_clothing_mask_surgical), 2),
            Dm.kv(typeof(obj_item_storage_backpack_virology), 2),
            Dm.kv(typeof(obj_item_storage_backpack_satchel_vir), 2),
            Dm.kv(typeof(obj_item_storage_backpack_messenger_vir), 2),
            Dm.kv(typeof(obj_item_storage_backpack_duffelbag_virology), 2)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:287  (gaps J5, D298)
public partial class obj_structure_closet_wardrobe_science_white : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:287
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_clothing_accessory_pocketprotector), 1),
            Dm.kv(typeof(obj_item_storage_backpack_science), 2),
            Dm.kv(typeof(obj_item_storage_backpack_satchel_science), 2),
            Dm.kv(typeof(obj_item_storage_backpack_duffelbag_science), 2),
            Dm.kv(typeof(obj_item_clothing_suit_hooded_wintercoat_science), 1),
            Dm.kv(typeof(obj_item_clothing_under_rank_rnd_scientist), 3),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_labcoat_science), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_sneakers_white), 3),
            Dm.kv(typeof(obj_item_radio_headset_headset_sci), 2),
            Dm.kv(typeof(obj_item_clothing_mask_gas), 3)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:215  (gaps J5, D298)
public partial class obj_structure_closet_wardrobe_robotics_black : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:215
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_clothing_glasses_hud_diagnostic), 2),
            Dm.kv(typeof(obj_item_clothing_under_rank_rnd_roboticist), 2),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_labcoat), 2),
            Dm.kv(typeof(obj_item_clothing_shoes_sneakers_black), 2),
            Dm.kv(typeof(obj_item_clothing_gloves_fingerless), 2),
            Dm.kv(typeof(obj_item_clothing_head_soft_black), 2)
        );
        generate_items_inside(items_inside, this);
        if (prob(40))
            Dm.@new<obj_item_clothing_mask_bandana_skull_black>(this);
        if (prob(40))
            Dm.@new<obj_item_clothing_mask_bandana_skull_black>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:4  (gaps ST3, D333)
public partial class obj_structure_closet_gmcloset : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:8
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:9
        DmList items_inside = Dm.list( // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:10
            Dm.kv(typeof(obj_item_clothing_head_hats_tophat), 2),
            Dm.kv(typeof(obj_item_radio_headset_headset_srv), 2),
            Dm.kv(typeof(obj_item_clothing_under_costume_buttondown_slacks_service), 2),
            Dm.kv(typeof(obj_item_clothing_accessory_waistcoat), 2),
            Dm.kv(typeof(obj_item_clothing_head_soft_black), 2),
            Dm.kv(typeof(obj_item_clothing_shoes_sneakers_black), 2),
            Dm.kv(typeof(obj_item_rag), 2),
            Dm.kv(typeof(obj_item_storage_box_beanbag), 1),
            Dm.kv(typeof(obj_item_clothing_suit_armor_vest_alt), 1),
            Dm.kv(typeof(obj_item_circuitboard_machine_dish_drive), 1),
            Dm.kv(typeof(obj_item_clothing_glasses_sunglasses_reagent), 1),
            Dm.kv(typeof(obj_item_clothing_neck_petcollar), 1),
            Dm.kv(typeof(obj_item_storage_belt_bandolier), 1)
        );
        generate_items_inside(items_inside, this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:24
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:27  (gaps ST3, D333)
public partial class obj_structure_closet_chefcloset : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:31
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:32
        DmList items_inside = Dm.list( // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:33
            Dm.kv(typeof(obj_item_clothing_under_suit_waiter), 2),
            Dm.kv(typeof(obj_item_radio_headset_headset_srv), 2),
            Dm.kv(typeof(obj_item_clothing_accessory_waistcoat), 2),
            Dm.kv(typeof(obj_item_clothing_suit_apron_chef), 3),
            Dm.kv(typeof(obj_item_clothing_head_soft_mime), 2),
            Dm.kv(typeof(obj_item_storage_box_mousetraps), 2),
            Dm.kv(typeof(obj_item_circuitboard_machine_dish_drive), 1),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_chef), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_buttondown_slacks_service), 1),
            Dm.kv(typeof(obj_item_clothing_head_utility_chefhat), 1),
            Dm.kv(typeof(obj_item_rag), 1)
        );
        generate_items_inside(items_inside, this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:45
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:48  (gaps ST3, D333)
public partial class obj_structure_closet_jcloset : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:52
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:53
        Dm.@new<obj_item_clothing_under_rank_civilian_janitor>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:54
        Dm.@new<obj_item_clothing_gloves_color_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:55
        Dm.@new<obj_item_clothing_head_soft_purple>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:56
        Dm.@new<obj_item_paint_paint_remover>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:57
        Dm.@new<obj_item_melee_flyswatter>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:58
        Dm.@new<obj_item_flashlight>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:59
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:60
        {
            Dm.@new<obj_item_clothing_suit_caution>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:61
        }
        Dm.@new<obj_item_holosign_creator>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:62
        Dm.@new<obj_item_lightreplacer>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:63
        Dm.@new<obj_item_soap>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:64
        Dm.@new<obj_item_storage_bag_trash>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:65
        Dm.@new<obj_item_clothing_shoes_galoshes>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:66
        Dm.@new<obj_item_watertank_janitor>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:67
        Dm.@new<obj_item_storage_belt_janitor>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:68
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:72  (gaps ST3, D333)
public partial class obj_structure_closet_lawcloset : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:76
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:77
        Dm.@new<obj_item_clothing_under_suit_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:78
        Dm.@new<obj_item_clothing_under_rank_civilian_lawyer_beige>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:79
        Dm.@new<obj_item_clothing_under_rank_civilian_lawyer_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:80
        Dm.@new<obj_item_clothing_under_rank_civilian_lawyer_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:81
        Dm.@new<obj_item_clothing_under_rank_civilian_lawyer_bluesuit>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:82
        Dm.@new<obj_item_clothing_neck_tie_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:83
        Dm.@new<obj_item_clothing_suit_toggle_lawyer>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:84
        Dm.@new<obj_item_clothing_under_rank_civilian_lawyer_purpsuit>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:85
        Dm.@new<obj_item_clothing_suit_toggle_lawyer_purple>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:86
        Dm.@new<obj_item_clothing_under_costume_buttondown_slacks_service>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:87
        Dm.@new<obj_item_clothing_neck_tie_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:88
        Dm.@new<obj_item_clothing_suit_toggle_lawyer_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:89
        Dm.@new<obj_item_clothing_shoes_laceup>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:90
        Dm.@new<obj_item_clothing_shoes_laceup>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:91
    }

    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:93
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate(); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:94
        Dm.@new<obj_item_clothing_accessory_lawyers_badge>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:95
        Dm.@new<obj_item_clothing_accessory_lawyers_badge>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:96
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:99  (gaps ST3, D333)
public partial class obj_structure_closet_wardrobe_chaplain_black : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:103
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_choice_beacon_holy>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:104
        Dm.@new<obj_item_clothing_accessory_pocketprotector_cosmetology>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:105
        Dm.@new<obj_item_clothing_under_rank_civilian_chaplain>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:106
        Dm.@new<obj_item_clothing_shoes_sneakers_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:107
        Dm.@new<obj_item_clothing_suit_chaplainsuit_nun>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:108
        Dm.@new<obj_item_clothing_head_chaplain_nun_hood>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:109
        Dm.@new<obj_item_clothing_suit_hooded_chaplainsuit_monkhabit>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:110
        Dm.@new<obj_item_clothing_suit_chaplainsuit_holidaypriest>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:111
        Dm.@new<obj_item_storage_backpack_cultpack>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:112
        Dm.@new<obj_item_storage_fancy_candle_box>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:113
        Dm.@new<obj_item_storage_fancy_candle_box>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:114
        return; // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:115
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:118  (gaps ST3, D333)
public partial class obj_structure_closet_wardrobe_red : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:121
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list( // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:122
            Dm.kv(typeof(obj_item_clothing_suit_hooded_wintercoat_security), 1),
            Dm.kv(typeof(obj_item_storage_backpack_security), 1),
            Dm.kv(typeof(obj_item_storage_backpack_satchel_sec), 1),
            Dm.kv(typeof(obj_item_storage_backpack_duffelbag_sec), 2),
            Dm.kv(typeof(obj_item_storage_backpack_messenger_sec), 1),
            Dm.kv(typeof(obj_item_clothing_under_rank_security_officer), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_security_officer_skirt), 2),
            Dm.kv(typeof(obj_item_clothing_shoes_jackboots), 3),
            Dm.kv(typeof(obj_item_clothing_head_beret_sec), 3),
            Dm.kv(typeof(obj_item_clothing_head_soft_sec), 3),
            Dm.kv(typeof(obj_item_clothing_mask_bandana_red), 2)
        );
        generate_items_inside(items_inside, this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:134
        return; // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:135
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:138  (gaps ST3, D333)
public partial class obj_structure_closet_wardrobe_cargotech : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:141
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list( // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:142
            Dm.kv(typeof(obj_item_clothing_suit_hooded_wintercoat_cargo), 1),
            Dm.kv(typeof(obj_item_clothing_under_rank_cargo_tech), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_sneakers_black), 3),
            Dm.kv(typeof(obj_item_clothing_gloves_fingerless), 3),
            Dm.kv(typeof(obj_item_clothing_head_soft), 3),
            Dm.kv(typeof(obj_item_radio_headset_headset_cargo), 1)
        );
        generate_items_inside(items_inside, this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:149
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:303  (gaps ST3, D333)
public partial class obj_structure_closet_wardrobe_botanist : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:306
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList items_inside = Dm.list( // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:307
            Dm.kv(typeof(obj_item_storage_backpack_botany), 2),
            Dm.kv(typeof(obj_item_storage_backpack_satchel_hyd), 2),
            Dm.kv(typeof(obj_item_storage_backpack_messenger_hyd), 2),
            Dm.kv(typeof(obj_item_storage_backpack_duffelbag_hydroponics), 2),
            Dm.kv(typeof(obj_item_clothing_suit_hooded_wintercoat_hydro), 1),
            Dm.kv(typeof(obj_item_clothing_suit_apron), 2),
            Dm.kv(typeof(obj_item_clothing_suit_apron_overalls), 2),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_hydroponics), 3),
            Dm.kv(typeof(obj_item_clothing_mask_bandana_striped_botany), 3)
        );
        generate_items_inside(items_inside, this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:317
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:320  (gaps ST3, D333)
public partial class obj_structure_closet_wardrobe_curator : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:323
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_head_fedora_curator>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:324
        Dm.@new<obj_item_clothing_suit_jacket_curator>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:325
        Dm.@new<obj_item_clothing_under_rank_civilian_curator_treasure_hunter>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:326
        Dm.@new<obj_item_clothing_shoes_workboots_mining>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:327
        Dm.@new<obj_item_storage_backpack_satchel_explorer>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:328
        Dm.@new<obj_item_storage_backpack_messenger_explorer>(this); // tg: code/game/objects/structures/crates_lockers/closets/job_closets.dm:329
    }
}
