// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/scientist.dm
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

// tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:35
public partial class obj_structure_closet_secure_closet_cytology : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:35
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_pushbroom>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:37
        Dm.@new<obj_item_storage_bag_xeno>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:38
        Dm.@new<obj_item_storage_box_petridish>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:39
        for (float i = 1; i <= 2; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:40
        {
            Dm.@new<obj_item_biopsy_tool>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:41
            Dm.@new<obj_item_storage_box_swab>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:42
        }
        Dm.@new<obj_item_reagent_containers_condiment_protein>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:43
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:6
public partial class obj_structure_closet_secure_closet_research_director : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:6
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_suit_bio_suit_scientist>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:9
        Dm.@new<obj_item_clothing_head_bio_hood_scientist>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:10
        Dm.@new<obj_item_storage_bag_garment_research_director>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:11
        Dm.@new<obj_item_disk_computer_command_rd>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:12
        Dm.@new<obj_item_radio_headset_heads_rd>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:13
        Dm.@new<obj_item_megaphone_command>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:14
        Dm.@new<obj_item_storage_lockbox_medal_sci>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:15
        Dm.@new<obj_item_assembly_flash_handheld>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:16
        Dm.@new<obj_item_door_remote_research_director>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:17
        Dm.@new<obj_item_circuitboard_machine_techfab_department_science>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:18
        Dm.@new<obj_item_storage_box_skillchips_science>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:19
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:21
public partial class obj_structure_closet_secure_closet_research_director
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/scientist.dm:21
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        // Traitor steal objectives
        Dm.@new<obj_item_clothing_suit_armor_reactive_teleport>(this);
        Dm.@new<obj_item_laser_pointer>(this);
    }
}
