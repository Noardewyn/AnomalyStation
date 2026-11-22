// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/security.dm
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

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:240
public partial class obj_structure_closet_secure_closet_brig : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:240
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_under_rank_prisoner>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:243
        Dm.@new<obj_item_clothing_under_rank_prisoner_skirt>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:244
        Dm.@new<obj_item_clothing_shoes_sneakers_orange>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:245
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:6
public partial class obj_structure_closet_secure_closet_captains : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:6
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_neck_petcollar>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:8
        Dm.@new<obj_item_pet_carrier>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:9
        Dm.@new<obj_item_storage_bag_garment_captain>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:10
        Dm.@new<obj_item_disk_computer_command_captain>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:11
        Dm.@new<obj_item_radio_headset_heads_captain_alt>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:12
        Dm.@new<obj_item_radio_headset_heads_captain>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:13
        Dm.@new<obj_item_door_remote_captain>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:14
        Dm.@new<obj_item_megaphone_command>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:15
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:251
public partial class obj_structure_closet_secure_closet_courtroom : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:251
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_shoes_laceup>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:253
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:254
        {
            Dm.@new<obj_item_paper_fluff_jobs_security_court_judgement>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:255
        }
        Dm.@new<obj_item_pen>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:256
        Dm.@new<obj_item_clothing_suit_costume_judgerobe>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:257
        Dm.@new<obj_item_clothing_head_costume_powdered_wig>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:258
        Dm.@new<obj_item_storage_briefcase>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:259
        Dm.@new<obj_item_clothing_under_suit_black_really>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:260
        Dm.@new<obj_item_clothing_neck_tie_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:261
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:165
public partial class obj_structure_closet_secure_closet_detective : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:165
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_box_evidence>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:167
        Dm.@new<obj_item_radio_headset_headset_sec>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:168
        Dm.@new<obj_item_detective_scanner>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:169
        Dm.@new<obj_item_flashlight_seclite>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:170
        Dm.@new<obj_item_holosign_creator_security>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:171
        Dm.@new<obj_item_reagent_containers_spray_pepper>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:172
        Dm.@new<obj_item_clothing_suit_armor_vest_det_suit>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:173
        Dm.@new<obj_item_storage_belt_holster_detective_full>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:174
        Dm.@new<obj_item_pinpointer_crew>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:175
        Dm.@new<obj_item_binoculars>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:176
        Dm.@new<obj_item_storage_box_rxglasses_spyglasskit>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:177
        Dm.@new<obj_item_clothing_head_fedora_inspector_hat>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:178
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:28
public partial class obj_structure_closet_secure_closet_hop : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:28
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_dog_bone>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:30
        Dm.@new<obj_item_storage_bag_garment_hop>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:31
        Dm.@new<obj_item_storage_lockbox_medal_service>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:32
        Dm.@new<obj_item_disk_computer_command_hop>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:33
        Dm.@new<obj_item_radio_headset_heads_hop>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:34
        Dm.@new<obj_item_storage_box_ids>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:35
        Dm.@new<obj_item_storage_box_silver_ids>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:36
        Dm.@new<obj_item_megaphone_command>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:37
        Dm.@new<obj_item_assembly_flash_handheld>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:38
        Dm.@new<obj_item_clothing_neck_petcollar>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:39
        Dm.@new<obj_item_pet_carrier>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:40
        Dm.@new<obj_item_door_remote_head_of_personnel>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:41
        Dm.@new<obj_item_circuitboard_machine_techfab_department_service>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:42
        Dm.@new<obj_item_storage_lockbox_medal_hop>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:43
        Dm.@new<obj_item_storage_box_stamps>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:44
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:56
public partial class obj_structure_closet_secure_closet_hos : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:56
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_disk_computer_command_hos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:59
        Dm.@new<obj_item_radio_headset_heads_hos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:60
        Dm.@new<obj_item_radio_headset_heads_hos_alt>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:61
        Dm.@new<obj_item_storage_bag_garment_hos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:62
        Dm.@new<obj_item_storage_lockbox_medal_sec>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:63
        Dm.@new<obj_item_megaphone_sec>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:64
        Dm.@new<obj_item_holosign_creator_security>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:65
        Dm.@new<obj_item_storage_lockbox_loyalty>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:66
        Dm.@new<obj_item_storage_box_flashbangs>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:67
        Dm.@new<obj_item_shield_riot_tele>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:68
        Dm.@new<obj_item_storage_belt_security_full>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:69
        Dm.@new<obj_item_circuitboard_machine_techfab_department_security>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:70
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:184
public partial class obj_structure_closet_secure_closet_injection : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:184
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:186
        {
            Dm.@new<obj_item_reagent_containers_syringe_lethal_execution>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:187
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:111
public partial class obj_structure_closet_secure_closet_security : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:111
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_suit_armor_vest_alt_sec>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:113
        Dm.@new<obj_item_clothing_head_helmet_sec>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:114
        Dm.@new<obj_item_radio_headset_headset_sec>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:115
        Dm.@new<obj_item_radio_headset_headset_sec_alt>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:116
        Dm.@new<obj_item_clothing_glasses_hud_security_sunglasses>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:117
        Dm.@new<obj_item_flashlight_seclite>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:118
        Dm.@new<obj_item_clothing_gloves_tackler>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:119
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:129
public partial class obj_structure_closet_secure_closet_security_cargo : obj_structure_closet_secure_closet_security
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:129
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_accessory_armband_cargo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:131
        Dm.@new<obj_item_encryptionkey_headset_cargo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:132
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:136
public partial class obj_structure_closet_secure_closet_security_engine : obj_structure_closet_secure_closet_security
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:136
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_accessory_armband_engine>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:138
        Dm.@new<obj_item_encryptionkey_headset_eng>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:139
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:150
public partial class obj_structure_closet_secure_closet_security_med : obj_structure_closet_secure_closet_security
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:150
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_accessory_armband_medblue>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:152
        Dm.@new<obj_item_encryptionkey_headset_med>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:153
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:143
public partial class obj_structure_closet_secure_closet_security_science : obj_structure_closet_secure_closet_security
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:143
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_accessory_armband_science>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:145
        Dm.@new<obj_item_encryptionkey_headset_sci>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:146
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:123
public partial class obj_structure_closet_secure_closet_security_sec : obj_structure_closet_secure_closet_security
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:123
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_belt_security_full>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:125
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:86
public partial class obj_structure_closet_secure_closet_warden : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:86
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_dog_bone>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:88
        Dm.@new<obj_item_radio_headset_headset_sec>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:89
        Dm.@new<obj_item_holosign_creator_security>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:90
        Dm.@new<obj_item_storage_bag_garment_warden>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:91
        Dm.@new<obj_item_storage_box_zipties>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:92
        Dm.@new<obj_item_storage_box_flashbangs>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:93
        Dm.@new<obj_item_storage_belt_security_full>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:94
        Dm.@new<obj_item_flashlight_seclite>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:95
        Dm.@new<obj_item_door_remote_head_of_security>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:96
        Dm.@new<obj_item_storage_belt_bandolier>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:97
    }
}

// M7x slice B: the populate_contents_immediate of the lockers that hold the station's guns (security.dm:17, :46, :72, :100, :287, :305,
// :322; the immediate part is made at init, before the contents the PopulateContents adds). The items that are not translated yet
// (the sabre belt, the photo albums, the nuke pinpointer, the ablative coat, the shotguns) are created as their generated types.

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:17
public partial class obj_structure_closet_secure_closet_captains : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:17
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();
        Dm.@new<obj_item_gun_energy_e_gun>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:18
        Dm.@new<obj_item_storage_belt_sheath_sabre>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:19

        Dm.@new<obj_item_storage_photo_album_captain>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:21
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:46
public partial class obj_structure_closet_secure_closet_hop : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:46
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();
        Dm.@new<obj_item_gun_energy_e_gun>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:47

        Dm.@new<obj_item_storage_photo_album_hop>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:49
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:72
public partial class obj_structure_closet_secure_closet_hos : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:72
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        // Traitor steal objectives
        Dm.@new<obj_item_gun_energy_e_gun_hos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:76
        Dm.@new<obj_item_pinpointer_nuke>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:77

        Dm.@new<obj_item_storage_photo_album_hos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:79
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:100
public partial class obj_structure_closet_secure_closet_warden : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:100
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        // Traitor steal objective
        Dm.@new<obj_item_gun_ballistic_shotgun_automatic_combat_compact>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:104
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:287
public partial class obj_structure_closet_secure_closet_armory1 : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:287
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        // Traitor steal objective
        Dm.@new<obj_item_clothing_suit_hooded_ablative>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:291
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:305
public partial class obj_structure_closet_secure_closet_armory2 : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:305
    public override async DmTask populate_contents_immediate()
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:306
        {
            Dm.@new<obj_item_gun_ballistic_shotgun_riot>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:307
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:322
public partial class obj_structure_closet_secure_closet_armory3 : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:322
    public override async DmTask populate_contents_immediate()
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:323
        {
            Dm.@new<obj_item_gun_energy_e_gun>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:324
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:325
        {
            Dm.@new<obj_item_gun_energy_laser>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:326
        }
    }
}

// M7x slice B: the PopulateContents of the armory lockers (security.dm:278-345), so that the energy gun locker is live.

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:278
public partial class obj_structure_closet_secure_closet_armory1 : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:278
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:280
        {
            Dm.@new<obj_item_clothing_suit_armor_riot>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:281
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:282
        {
            Dm.@new<obj_item_clothing_head_helmet_toggleable_riot>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:283
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:284
        {
            Dm.@new<obj_item_shield_riot>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:285
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:299
public partial class obj_structure_closet_secure_closet_armory2 : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:299
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_box_firingpins>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:301
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:302
        {
            Dm.@new<obj_item_storage_box_rubbershot>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:303
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:315
public partial class obj_structure_closet_secure_closet_armory3 : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:315
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_box_firingpins>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:317
        Dm.@new<obj_item_gun_energy_ionrifle>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:318
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:319
        {
            Dm.@new<obj_item_gun_energy_laser_pistol>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:320
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:333
public partial class obj_structure_closet_secure_closet_tac : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:333
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_gun_ballistic_automatic_wt550>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:335
        Dm.@new<obj_item_clothing_head_helmet_alt>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:336
        Dm.@new<obj_item_clothing_mask_gas_sechailer>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:337
        Dm.@new<obj_item_clothing_suit_armor_bulletproof>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:338
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:199
public partial class obj_structure_closet_secure_closet_brig_genpop : obj_structure_closet_secure_closet_brig
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:204
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("<b>Right-click</b> with a Security-level ID to reset []'s registered ID.", this)));
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:208
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(secure) || !istype(tool, typeof(obj_item_card_id)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (id_card != null)
            return ITEM_INTERACT_BLOCKING;

        await say(I18n.T("Prisoner ID linked to locker."));
        id_card = WEAKREF(tool) as datum_weakref;
        name = I18n.T("genpop storage locker - []", (tool as obj_item_card_id).registered_name); // astype to the prisoner card: tg reads a plain card's name too, registered_name is on /id
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:220
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        DmList id_access = (tool as obj_item_card_id)?.GetAccess() as DmList;
        if (id_card == null || !Dm.@in(ACCESS_BRIG, id_access))
            return NONE;

        await clear_access();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:228
    public virtual async DmTask clear_access()
    {
        await say(I18n.T("Authorized ID detected. Unlocking locker and resetting ID."));
        locked = FALSE;
        id_card = null;
        name = Dm.initial(this, (obj_structure_closet_secure_closet_brig_genpop p) => p.name);
        update_appearance();
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:341
public partial class obj_structure_closet_secure_closet_labor_camp_security : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/security.dm:345
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_suit_armor_vest>(this);
        Dm.@new<obj_item_clothing_head_helmet_sec>(this);
        Dm.@new<obj_item_clothing_under_rank_security_officer>(this);
        Dm.@new<obj_item_clothing_under_rank_security_officer_skirt>(this);
        Dm.@new<obj_item_clothing_glasses_hud_security_sunglasses>(this);
        Dm.@new<obj_item_flashlight_seclite>(this);
    }
}
