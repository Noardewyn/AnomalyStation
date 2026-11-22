// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/medical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'. M8 slice C:
// the medicine closet (medical1) and the paramedics' locker.

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:120
public partial class obj_structure_closet_secure_closet_chemical : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:120
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_box_pillbottles>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:122
        Dm.@new<obj_item_storage_box_pillbottles>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:123
        Dm.@new<obj_item_storage_box_medigels>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:124
        Dm.@new<obj_item_storage_box_medigels>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:125
        Dm.@new<obj_item_ph_booklet>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:126
        Dm.@new<obj_item_reagent_containers_dropper>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:127
        Dm.@new<obj_item_reagent_containers_cup_bottle_acidic_buffer>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:128
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:77
public partial class obj_structure_closet_secure_closet_chief_medical : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:77
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_suit_bio_suit_cmo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:80
        Dm.@new<obj_item_clothing_head_bio_hood_cmo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:81
        Dm.@new<obj_item_storage_bag_garment_chief_medical>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:82
        Dm.@new<obj_item_disk_computer_command_cmo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:83
        Dm.@new<obj_item_radio_headset_heads_cmo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:84
        Dm.@new<obj_item_megaphone_command>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:85
        Dm.@new<obj_item_assembly_flash_handheld>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:86
        Dm.@new<obj_item_autosurgeon_medical_hud>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:87
        Dm.@new<obj_item_door_remote_chief_medical_officer>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:88
        Dm.@new<obj_item_clothing_neck_petcollar>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:89
        Dm.@new<obj_item_pet_carrier>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:90
        Dm.@new<obj_item_circuitboard_machine_techfab_department_medical>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:91
        Dm.@new<obj_item_storage_lockbox_medal_med>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:92
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:27
public partial class obj_structure_closet_secure_closet_medical2 : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:27
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:29
        {
            Dm.@new<obj_item_tank_internals_anesthetic>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:30
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:31
        {
            Dm.@new<obj_item_clothing_mask_breath_muzzle>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:32
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:58
public partial class obj_structure_closet_secure_closet_psychology : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:58
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_under_costume_buttondown_slacks_service>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:60
        Dm.@new<obj_item_clothing_under_costume_buttondown_skirt_service>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:61
        Dm.@new<obj_item_clothing_neck_tie_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:62
        Dm.@new<obj_item_clothing_shoes_laceup>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:63
        Dm.@new<obj_item_storage_backpack_medic>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:64
        Dm.@new<obj_item_radio_headset_headset_srvmed>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:65
        Dm.@new<obj_item_clipboard>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:66
        Dm.@new<obj_item_clothing_suit_jacket_straight_jacket>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:67
        Dm.@new<obj_item_clothing_ears_earmuffs>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:68
        Dm.@new<obj_item_clothing_mask_muzzle>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:69
        Dm.@new<obj_item_clothing_glasses_blindfold>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:70
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:1 (M8 slice C)
public partial class obj_structure_closet_secure_closet_medical1 : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:7
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_reagent_containers_cup_beaker), 2),
            Dm.kv(typeof(obj_item_reagent_containers_dropper), 2),
            Dm.kv(typeof(obj_item_storage_belt_medical), 1),
            Dm.kv(typeof(obj_item_storage_box_syringes), 1),
            Dm.kv(typeof(obj_item_reagent_containers_cup_bottle_toxin), 1),
            Dm.kv(typeof(obj_item_reagent_containers_cup_bottle_morphine), 2),
            Dm.kv(typeof(obj_item_reagent_containers_cup_bottle_epinephrine), 3),
            Dm.kv(typeof(obj_item_reagent_containers_cup_bottle_multiver), 3),
            Dm.kv(typeof(obj_item_storage_box_rxglasses), 1));
        generate_items_inside(items_inside, this);

        Dm.@new<obj_item_clothing_glasses_science>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:147 (M8 slice C)
public partial class obj_structure_closet_secure_closet_paramedic : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:152
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);

        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_storage_medkit_emergency), 1),
            Dm.kv(typeof(obj_item_storage_box_bandages), 1),
            Dm.kv(typeof(obj_item_pinpointer_crew), 1),
            Dm.kv(typeof(obj_item_storage_belt_medical_paramedic), 1),
            Dm.kv(typeof(obj_item_radio_headset_headset_med), 2),
            Dm.kv(typeof(obj_item_emergency_bed), 2),
            Dm.kv(typeof(obj_item_storage_bag_garment_paramedic), 2)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:94
public partial class obj_structure_closet_secure_closet_chief_medical
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:94
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        // Traitor steal objective
        Dm.@new<obj_item_reagent_containers_hypospray_cmo>(this);
        Dm.@new<obj_item_defibrillator_compact_loaded_cmo>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:39  (gaps J5, D298)
public partial class obj_structure_closet_secure_closet_medical3 : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:39
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents();
        Dm.@new<obj_item_radio_headset_headset_med>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:41
        Dm.@new<obj_item_defibrillator_loaded>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:42
        Dm.@new<obj_item_clothing_gloves_latex_nitrile>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:43
        Dm.@new<obj_item_storage_belt_medical>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:44
        Dm.@new<obj_item_clothing_glasses_hud_health>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:45
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:108  (gaps J5, D298)
public partial class obj_structure_closet_secure_closet_animal : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:108
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents();
        Dm.@new<obj_item_assembly_signaler>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:110
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:111
            Dm.@new<obj_item_electropack>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:131  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_chemical_heisenberg : obj_structure_closet_secure_closet_chemical
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:134
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:135
        Dm.@new<obj_item_reagent_containers_dropper>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:136
        Dm.@new<obj_item_reagent_containers_dropper>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:137
        Dm.@new<obj_item_storage_box_syringes_variety>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:138
        Dm.@new<obj_item_storage_box_beakers_variety>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:139
        Dm.@new<obj_item_clothing_glasses_science>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/medical.dm:140
    }
}
