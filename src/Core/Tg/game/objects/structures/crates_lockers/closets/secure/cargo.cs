// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/cargo.dm
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

// tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:6
public partial class obj_structure_closet_secure_closet_quartermaster : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:6
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_lockbox_medal_cargo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:8
        Dm.@new<obj_item_radio_weather_monitor>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:9
        Dm.@new<obj_item_radio_headset_heads_qm>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:10
        Dm.@new<obj_item_megaphone_cargo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:11
        Dm.@new<obj_item_assembly_flash_handheld>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:12
        Dm.@new<obj_item_tank_internals_emergency_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:13
        Dm.@new<obj_item_universal_scanner>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:14
        Dm.@new<obj_item_door_remote_quartermaster>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:15
        Dm.@new<obj_item_circuitboard_machine_techfab_department_cargo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:16
        Dm.@new<obj_item_circuitboard_machine_ore_silo>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:17
        Dm.@new<obj_item_storage_bag_garment_quartermaster>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:18
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:20
public partial class obj_structure_closet_secure_closet_quartermaster
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/cargo.dm:20
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        // Traitor steal objective
        Dm.@new<obj_item_card_id_departmental_budget_car>(this);
    }
}
