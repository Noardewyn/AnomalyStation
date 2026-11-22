// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/engineering.dm
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

// tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:87
public partial class obj_structure_closet_secure_closet_atmospherics : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:87
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_radio_headset_headset_eng>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:89
        Dm.@new<obj_item_storage_toolbox_mechanical>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:91
        Dm.@new<obj_item_tank_internals_emergency_oxygen_engi>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:92
        Dm.@new<obj_item_holosign_creator_atmos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:93
        Dm.@new<obj_item_watertank_atmos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:94
        Dm.@new<obj_item_clothing_suit_utility_fire_atmos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:95
        Dm.@new<obj_item_clothing_gloves_atmos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:96
        Dm.@new<obj_item_clothing_mask_gas_atmos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:97
        Dm.@new<obj_item_clothing_head_utility_hardhat_welding_atmos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:98
        Dm.@new<obj_item_clothing_glasses_meson_engine_tray>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:99
        Dm.@new<obj_item_extinguisher_advanced>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:100
    }

    // M7 slice D172: the atmospheric technicians' RPD.
    // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:102
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        Dm.@new<obj_item_pipe_dispenser>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:6
public partial class obj_structure_closet_secure_closet_engineering_chief : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:6
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_bag_garment_engineering_chief>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:9
        Dm.@new<obj_item_disk_computer_command_ce>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:10
        Dm.@new<obj_item_radio_headset_heads_ce>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:11
        Dm.@new<obj_item_megaphone_command>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:12
        Dm.@new<obj_item_holosign_creator_atmos>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:13
        Dm.@new<obj_item_assembly_flash_handheld>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:14
        Dm.@new<obj_item_door_remote_chief_engineer>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:15
        Dm.@new<obj_item_storage_lockbox_medal_engineering>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:16
        Dm.@new<obj_item_circuitboard_machine_techfab_department_engineering>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:17
        Dm.@new<obj_item_extinguisher_advanced>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:18
        Dm.@new<obj_item_storage_box_skillchips_engineering>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:19
        Dm.@new<obj_item_storage_box_stickers_chief_engineer>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:20
    }

    // M7 slice D172: the CE's RPD (the traitor objective's items, put in before the locker is opened).
    // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:22
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        // Traitor steal objective
        Dm.@new<obj_item_blueprints>(this);
        Dm.@new<obj_item_pipe_dispenser>(this);

        Dm.@new<obj_item_storage_photo_album_ce>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:70
public partial class obj_structure_closet_secure_closet_engineering_personal : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:70
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_radio_headset_headset_eng>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:72
        Dm.@new<obj_item_storage_toolbox_mechanical>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:73
        Dm.@new<obj_item_tank_internals_emergency_oxygen_engi>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:74
        Dm.@new<obj_item_holosign_creator_engineering>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:75
        Dm.@new<obj_item_clothing_mask_gas>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:76
        Dm.@new<obj_item_clothing_glasses_meson_engine>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:77
        Dm.@new<obj_item_storage_box_emptysandbags>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:78
        Dm.@new<obj_item_storage_bag_construction>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:79
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:58
public partial class obj_structure_closet_secure_closet_engineering_welding : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:58
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:60
        {
            Dm.@new<obj_item_clothing_head_utility_welding>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:61
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:62
        {
            Dm.@new<obj_item_weldingtool>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:63
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:37
public partial class obj_structure_closet_secure_closet_engineering_electrical : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:37
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_inducer), 2),
            Dm.kv(typeof(obj_item_storage_toolbox_electrical), 3),
            Dm.kv(typeof(obj_item_electronics_apc), 3),
            Dm.kv(typeof(obj_item_multitool), 3));
        generate_items_inside(items_inside, this);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/secure/engineering.dm:46
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        Dm.@new<obj_item_clothing_gloves_color_yellow>(this);
        Dm.@new<obj_item_clothing_gloves_color_yellow>(this);
    }
}
