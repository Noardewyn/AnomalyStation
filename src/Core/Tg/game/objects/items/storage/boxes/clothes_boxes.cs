// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/clothes_boxes.dm
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

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:77
public partial class obj_item_storage_box_deputy : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:77
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:78
        {
            Dm.@new<obj_item_clothing_accessory_armband_deputy>(this); // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:79
        }
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:8
public partial class obj_item_storage_box_gloves : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:8
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:9
        {
            Dm.@new<obj_item_clothing_gloves_latex>(this); // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:10
        }
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:17
public partial class obj_item_storage_box_masks : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:17
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:18
        {
            Dm.@new<obj_item_clothing_mask_surgical>(this); // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:19
        }
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:26
public partial class obj_item_storage_box_rxglasses : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:26
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:27
        {
            Dm.@new<obj_item_clothing_glasses_regular>(this); // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:28
        }
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:82  (gaps J3, D298: the heroic beacon's sets)
public partial class obj_item_storage_box_hero : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:86
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_fedora_curator>(this);
        Dm.@new<obj_item_clothing_shoes_workboots_mining>(this);
        Dm.@new<obj_item_clothing_suit_jacket_curator>(this);
        Dm.@new<obj_item_clothing_under_rank_civilian_curator_treasure_hunter>(this);
        Dm.@new<obj_item_melee_curator_whip>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:93
public partial class obj_item_storage_box_hero_astronaut : obj_item_storage_box_hero
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:97
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_space_nasavoid>(this);
        Dm.@new<obj_item_clothing_head_helmet_space_nasavoid>(this);
        Dm.@new<obj_item_tank_internals_oxygen>(this);
        Dm.@new<obj_item_gps>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:103
public partial class obj_item_storage_box_hero_scottish : obj_item_storage_box_hero
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:107
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_claymore_weak_ceremonial>(this);
        Dm.@new<obj_item_clothing_shoes_sandal>(this);
        Dm.@new<obj_item_clothing_under_costume_kilt>(this);
        Dm.@new<obj_item_toy_crayon_spraycan>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:113
public partial class obj_item_storage_box_hero_carphunter : obj_item_storage_box_hero
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:117
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_mask_gas_carp>(this);
        Dm.@new<obj_item_clothing_suit_hooded_carp_costume_spaceproof_old>(this);
        Dm.@new<obj_item_knife_hunting>(this);
        Dm.@new<obj_item_storage_box_papersack_meat>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:123
public partial class obj_item_storage_box_hero_mothpioneer : obj_item_storage_box_hero
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:127
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_mothcap_original>(this);
        Dm.@new<obj_item_clothing_suit_mothcoat_original>(this);
        Dm.@new<obj_item_crowbar>(this);
        Dm.@new<obj_item_flashlight_lantern>(this);
        Dm.@new<obj_item_screwdriver>(this);
        Dm.@new<obj_item_stack_sheet_glass_fifty>(this);
        Dm.@new<obj_item_stack_sheet_iron_fifty>(this);
        Dm.@new<obj_item_wrench>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:137
public partial class obj_item_storage_box_hero_etherealwarden : obj_item_storage_box_hero
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:141
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_hooded_ethereal_raincoat_trailwarden>(this);
        Dm.@new<obj_item_clothing_under_ethereal_tunic_trailwarden>(this);
        Dm.@new<obj_item_storage_backpack_saddlepack>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:146
public partial class obj_item_storage_box_hero_journalist : obj_item_storage_box_hero
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:150
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_costume_buttondown_slacks>(this);
        Dm.@new<obj_item_clothing_suit_toggle_suspenders>(this);
        Dm.@new<obj_item_clothing_neck_tie_red>(this);
        Dm.@new<obj_item_clothing_head_fedora_beige_press>(this);
        Dm.@new<obj_item_clothing_accessory_press_badge>(this);
        Dm.@new<obj_item_clothing_suit_hazardvest_press>(this);
        Dm.@new<obj_item_radio_entertainment_microphone_physical>(this);
        Dm.@new<obj_item_radio_entertainment_speakers_physical>(this);
        Dm.@new<obj_item_clipboard>(this);
        Dm.@new<obj_item_taperecorder>(this);
        Dm.@new<obj_item_camera>(this);
        Dm.@new<obj_item_wallframe_telescreen_entertainment>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:31
public partial class obj_item_storage_box_rxglasses_spyglasskit : obj_item_storage_box_rxglasses
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:35
    public override async DmTask PopulateContents()
    {
        obj_item_clothing_accessory_spy_bug newbug = Dm.@new<obj_item_clothing_accessory_spy_bug>(this);
        obj_item_clothing_glasses_sunglasses_spy newglasses = Dm.@new<obj_item_clothing_glasses_sunglasses_spy>(this);
        newbug.linked_glasses = newglasses;
        newglasses.linked_bug = newbug;
        Dm.@new<obj_item_paper_fluff_nerddocs>(this);
    }
}

// Gaps SR1 (D333): the file's other boxes (the costume, chaplain, crusader and wizard cosplay kits, the floor camo and the collar
// bomb box). Left: the rumble royale kit (its implanter and remote wait on the battle royale, not-translated.md section 208).

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:46
public partial class obj_item_storage_box_tape_wizard : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:46
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_wizard_tape_fake>(this);
        Dm.@new<obj_item_clothing_suit_wizrobe_tape_fake>(this);
        Dm.@new<obj_item_staff_tape>(this);
        Dm.@new<obj_item_stack_medical_wrap_sticky_tape>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:58
public partial class obj_item_storage_box_fakesyndiesuit : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:58
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_syndicatefake>(this);
        Dm.@new<obj_item_clothing_suit_syndicatefake>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:169
public partial class obj_item_storage_box_holy : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:169
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_helmet_chaplain>(this);
        Dm.@new<obj_item_clothing_suit_chaplainsuit_armor_templar>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:177
public partial class obj_item_storage_box_holy_clock : obj_item_storage_box_holy
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:177
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_helmet_chaplain_clock>(this);
        Dm.@new<obj_item_clothing_suit_chaplainsuit_armor_clock>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:185
public partial class obj_item_storage_box_holy_student : obj_item_storage_box_holy
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:185
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_chaplainsuit_armor_studentuni>(this);
        Dm.@new<obj_item_clothing_head_helmet_chaplain_cage>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:193
public partial class obj_item_storage_box_holy_sentinel : obj_item_storage_box_holy
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:193
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_chaplainsuit_armor_ancient>(this);
        Dm.@new<obj_item_clothing_head_helmet_chaplain_ancient>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:201
public partial class obj_item_storage_box_holy_witchhunter : obj_item_storage_box_holy
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:201
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_chaplainsuit_armor_witchhunter>(this);
        Dm.@new<obj_item_clothing_head_helmet_chaplain_witchunter_hat>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:209
public partial class obj_item_storage_box_holy_adept : obj_item_storage_box_holy
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:209
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_chaplainsuit_armor_adept>(this);
        Dm.@new<obj_item_clothing_head_helmet_chaplain_adept>(this);
        Dm.@new<obj_item_clothing_gloves_bracer>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:218
public partial class obj_item_storage_box_holy_follower : obj_item_storage_box_holy
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:218
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_hooded_chaplain_hoodie>(this);
        Dm.@new<obj_item_clothing_suit_hooded_chaplain_hoodie>(this);
        Dm.@new<obj_item_clothing_suit_hooded_chaplain_hoodie>(this);
        Dm.@new<obj_item_clothing_suit_hooded_chaplain_hoodie>(this);
        Dm.@new<obj_item_clothing_suit_hooded_chaplain_hoodie_leader>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:229
public partial class obj_item_storage_box_holy_divine_archer : obj_item_storage_box_holy
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:229
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_civilian_chaplain_divine_archer>(this);
        Dm.@new<obj_item_clothing_suit_hooded_chaplain_hoodie_divine_archer>(this);
        Dm.@new<obj_item_clothing_gloves_divine_archer>(this);
        Dm.@new<obj_item_clothing_shoes_divine_archer>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:239
public partial class obj_item_storage_box_holy_heretic : obj_item_storage_box_holy
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:239
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_chaplainsuit_armor_heretic>(this);
        Dm.@new<obj_item_clothing_head_helmet_chaplain_heretic>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:251
public partial class obj_item_storage_box_floor_camo : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:251
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_syndicate_floortilecamo>(this);
        Dm.@new<obj_item_clothing_mask_floortilebalaclava>(this);
        Dm.@new<obj_item_clothing_gloves_combat_floortile>(this);
        Dm.@new<obj_item_clothing_shoes_jackboots_floortile>(this);
        Dm.@new<obj_item_storage_backpack_floortile>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:266
public partial class obj_item_storage_box_itemset_crusader_blue : obj_item_storage_box_itemset_crusader
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:266
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_chaplainsuit_armor_crusader_blue>(this);
        Dm.@new<obj_item_clothing_head_helmet_plate_crusader_blue>(this);
        Dm.@new<obj_item_clothing_gloves_plate_blue>(this);
        Dm.@new<obj_item_clothing_shoes_plate_blue>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:272
public partial class obj_item_storage_box_itemset_crusader_red : obj_item_storage_box_itemset_crusader
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:272
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_chaplainsuit_armor_crusader_red>(this);
        Dm.@new<obj_item_clothing_head_helmet_plate_crusader_red>(this);
        Dm.@new<obj_item_clothing_gloves_plate_red>(this);
        Dm.@new<obj_item_clothing_shoes_plate_red>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:281
public partial class obj_item_storage_box_wizard_kit : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:281
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_wizard>(this);
        Dm.@new<obj_item_clothing_suit_wizrobe>(this);
        Dm.@new<obj_item_clothing_shoes_sandal>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:289
public partial class obj_item_storage_box_wizard_kit_red : obj_item_storage_box_wizard_kit
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:289
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_wizard_red>(this);
        Dm.@new<obj_item_clothing_suit_wizrobe_red>(this);
        Dm.@new<obj_item_clothing_shoes_sandal>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:313
public partial class obj_item_storage_box_wizard_kit_yellow : obj_item_storage_box_wizard_kit
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:313 (tg defines this proc three times on this type, at :297, :305
    // and :313, the magus kits' bodies pasted under the yellow kit's path; the last definition is the one that runs and the
    // earlier two are never reached, nothing calls ..())
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_wizard_magus>(this);
        Dm.@new<obj_item_clothing_suit_wizrobe_magusblue>(this);
        Dm.@new<obj_item_clothing_shoes_sandal>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:321
public partial class obj_item_storage_box_wizard_kit_black : obj_item_storage_box_wizard_kit
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:321
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_head_wizard_black>(this);
        Dm.@new<obj_item_clothing_suit_wizrobe_black>(this);
        Dm.@new<obj_item_clothing_shoes_sandal>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:258
public partial class obj_item_storage_box_collar_bomb : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/clothes_boxes.dm:262
    public override async DmTask PopulateContents()
    {
        obj_item_collar_bomb_button button = Dm.@new<obj_item_collar_bomb_button>(this);
        Dm.@new<obj_item_clothing_neck_collar_bomb>(this, button);
    }
}
