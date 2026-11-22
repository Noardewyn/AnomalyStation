// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/garment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/objects/items/storage/garment.dm:111  (gaps J5, D298)
public partial class obj_item_storage_bag_garment_research_director : obj_item_storage_bag_garment
{
    // tg: code/game/objects/items/storage/garment.dm:111
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_rnd_research_director>(this); // tg: code/game/objects/items/storage/garment.dm:112
        Dm.@new<obj_item_clothing_under_rank_rnd_research_director_skirt>(this); // tg: code/game/objects/items/storage/garment.dm:113
        Dm.@new<obj_item_clothing_under_rank_rnd_research_director_alt>(this); // tg: code/game/objects/items/storage/garment.dm:114
        Dm.@new<obj_item_clothing_under_rank_rnd_research_director_alt_skirt>(this); // tg: code/game/objects/items/storage/garment.dm:115
        Dm.@new<obj_item_clothing_under_rank_rnd_research_director_turtleneck>(this); // tg: code/game/objects/items/storage/garment.dm:116
        Dm.@new<obj_item_clothing_under_rank_rnd_research_director_turtleneck_skirt>(this); // tg: code/game/objects/items/storage/garment.dm:117
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_science_rd>(this); // tg: code/game/objects/items/storage/garment.dm:118
        Dm.@new<obj_item_clothing_head_beret_science_rd>(this); // tg: code/game/objects/items/storage/garment.dm:119
        Dm.@new<obj_item_clothing_gloves_color_black>(this); // tg: code/game/objects/items/storage/garment.dm:120
        Dm.@new<obj_item_clothing_neck_cloak_rd>(this); // tg: code/game/objects/items/storage/garment.dm:121
        Dm.@new<obj_item_clothing_shoes_jackboots>(this); // tg: code/game/objects/items/storage/garment.dm:122
    }
}

// tg: code/game/objects/items/storage/garment.dm:124  (gaps J5, D298)
public partial class obj_item_storage_bag_garment_chief_medical : obj_item_storage_bag_garment
{
    // tg: code/game/objects/items/storage/garment.dm:124
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_medical_chief_medical_officer>(this); // tg: code/game/objects/items/storage/garment.dm:125
        Dm.@new<obj_item_clothing_under_rank_medical_chief_medical_officer_skirt>(this); // tg: code/game/objects/items/storage/garment.dm:126
        Dm.@new<obj_item_clothing_under_rank_medical_chief_medical_officer_scrubs>(this); // tg: code/game/objects/items/storage/garment.dm:127
        Dm.@new<obj_item_clothing_under_rank_medical_chief_medical_officer_turtleneck>(this); // tg: code/game/objects/items/storage/garment.dm:128
        Dm.@new<obj_item_clothing_under_rank_medical_chief_medical_officer_turtleneck_skirt>(this); // tg: code/game/objects/items/storage/garment.dm:129
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_medical_cmo>(this); // tg: code/game/objects/items/storage/garment.dm:130
        Dm.@new<obj_item_clothing_suit_toggle_labcoat_cmo>(this); // tg: code/game/objects/items/storage/garment.dm:131
        Dm.@new<obj_item_clothing_gloves_latex_nitrile>(this); // tg: code/game/objects/items/storage/garment.dm:132
        Dm.@new<obj_item_clothing_head_beret_medical_cmo>(this); // tg: code/game/objects/items/storage/garment.dm:133
        Dm.@new<obj_item_clothing_head_utility_surgerycap_cmo>(this); // tg: code/game/objects/items/storage/garment.dm:134
        Dm.@new<obj_item_clothing_neck_cloak_cmo>(this); // tg: code/game/objects/items/storage/garment.dm:135
        Dm.@new<obj_item_clothing_shoes_sneakers_white>(this); // tg: code/game/objects/items/storage/garment.dm:136
        Dm.@new<obj_item_storage_backpack_chief_medic>(this); // tg: code/game/objects/items/storage/garment.dm:137
        Dm.@new<obj_item_storage_backpack_satchel_chief_medic>(this); // tg: code/game/objects/items/storage/garment.dm:138
        Dm.@new<obj_item_storage_backpack_duffelbag_chief_medic>(this); // tg: code/game/objects/items/storage/garment.dm:139
        Dm.@new<obj_item_storage_backpack_messenger_chief_medic>(this); // tg: code/game/objects/items/storage/garment.dm:140
    }
}

// tg: code/game/objects/items/storage/garment.dm:142  (gaps J5, D298)
public partial class obj_item_storage_bag_garment_engineering_chief : obj_item_storage_bag_garment
{
    // tg: code/game/objects/items/storage/garment.dm:142
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_engineering_chief_engineer>(this); // tg: code/game/objects/items/storage/garment.dm:143
        Dm.@new<obj_item_clothing_under_rank_engineering_chief_engineer_skirt>(this); // tg: code/game/objects/items/storage/garment.dm:144
        Dm.@new<obj_item_clothing_under_rank_engineering_chief_engineer_turtleneck>(this); // tg: code/game/objects/items/storage/garment.dm:145
        Dm.@new<obj_item_clothing_under_rank_engineering_chief_engineer_turtleneck_skirt>(this); // tg: code/game/objects/items/storage/garment.dm:146
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_engineering_ce>(this); // tg: code/game/objects/items/storage/garment.dm:147
        Dm.@new<obj_item_clothing_glasses_meson_engine>(this); // tg: code/game/objects/items/storage/garment.dm:148
        Dm.@new<obj_item_clothing_gloves_chief_engineer>(this); // tg: code/game/objects/items/storage/garment.dm:149
        Dm.@new<obj_item_clothing_head_utility_hardhat_white>(this); // tg: code/game/objects/items/storage/garment.dm:150
        Dm.@new<obj_item_clothing_head_utility_hardhat_welding_white>(this); // tg: code/game/objects/items/storage/garment.dm:151
        Dm.@new<obj_item_clothing_neck_cloak_ce>(this); // tg: code/game/objects/items/storage/garment.dm:152
        Dm.@new<obj_item_clothing_shoes_workboots>(this); // tg: code/game/objects/items/storage/garment.dm:153
    }
}

// Gaps SR1 (D333): the captain's, the head of personnel's and the paramedics' garment bags (the bags' storage, holding clothes and
// empty backpacks, is in datums/storage/subtypes/bags.cs).

// tg: code/game/objects/items/storage/garment.dm:46
public partial class obj_item_storage_bag_garment_captain : obj_item_storage_bag_garment
{
    // tg: code/game/objects/items/storage/garment.dm:46
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_captain>(this);
        Dm.@new<obj_item_clothing_under_rank_captain_skirt>(this);
        Dm.@new<obj_item_clothing_under_rank_captain_parade>(this);
        Dm.@new<obj_item_clothing_under_rank_captain_royal>(this);
        Dm.@new<obj_item_clothing_suit_armor_vest_capcarapace>(this);
        Dm.@new<obj_item_clothing_suit_armor_vest_capcarapace_captains_formal>(this);
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_captain>(this);
        Dm.@new<obj_item_clothing_suit_jacket_capjacket>(this);
        Dm.@new<obj_item_clothing_glasses_sunglasses_gar_giga>(this);
        Dm.@new<obj_item_clothing_gloves_captain>(this);
        Dm.@new<obj_item_clothing_head_costume_crown_fancy>(this);
        Dm.@new<obj_item_clothing_head_hats_caphat>(this);
        Dm.@new<obj_item_clothing_head_hats_caphat_parade>(this);
        Dm.@new<obj_item_clothing_head_hats_caphat_bicorne>(this);
        Dm.@new<obj_item_clothing_neck_cloak_cap>(this);
        Dm.@new<obj_item_clothing_shoes_laceup>(this);
        Dm.@new<obj_item_storage_backpack_captain>(this);
        Dm.@new<obj_item_storage_backpack_satchel_cap>(this);
        Dm.@new<obj_item_storage_backpack_duffelbag_captain>(this);
        Dm.@new<obj_item_storage_backpack_messenger_cap>(this);
    }
}

// tg: code/game/objects/items/storage/garment.dm:68
public partial class obj_item_storage_bag_garment_hop : obj_item_storage_bag_garment
{
    // tg: code/game/objects/items/storage/garment.dm:68
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_civilian_head_of_personnel>(this);
        Dm.@new<obj_item_clothing_under_rank_civilian_head_of_personnel_skirt>(this);
        Dm.@new<obj_item_clothing_suit_armor_vest_hop>(this);
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_hop>(this);
        Dm.@new<obj_item_clothing_glasses_sunglasses>(this);
        Dm.@new<obj_item_clothing_head_hats_hopcap>(this);
        Dm.@new<obj_item_clothing_neck_cloak_hop>(this);
        Dm.@new<obj_item_clothing_shoes_laceup>(this);
    }
}

// tg: code/game/objects/items/storage/garment.dm:167
public partial class obj_item_storage_bag_garment_paramedic : obj_item_storage_bag_garment
{
    // tg: code/game/objects/items/storage/garment.dm:167
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_medical_paramedic>(this);
        Dm.@new<obj_item_clothing_under_rank_medical_paramedic_skirt>(this);
        Dm.@new<obj_item_clothing_gloves_latex_nitrile>(this);
        Dm.@new<obj_item_clothing_shoes_workboots_black>(this);
        Dm.@new<obj_item_clothing_glasses_hud_health>(this);
    }
}
