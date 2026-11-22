// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/engineering_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:3  (gaps F1)
public partial class obj_item_storage_box_metalfoam : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:8
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
            Dm.@new<obj_item_grenade_chem_grenade_metalfoam>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:12  (gaps F1)
public partial class obj_item_storage_box_smart_metal_foam : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:17
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
            Dm.@new<obj_item_grenade_chem_grenade_smart_metal_foam>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:81  (gaps J5, D298)
public partial class obj_item_storage_box_stickers_chief_engineer : obj_item_storage_box_stickers
{
    // tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:81
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:82
            Dm.@new<obj_item_sticker_chief_engineer>(this);
    }
}

// Gaps SR1 (D333): the debug tools box and the emergency and engineering tank boxes.

// tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:26
public partial class obj_item_storage_box_debugtools : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:26
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_card_emag), 1),
            Dm.kv(typeof(obj_item_construction_rcd_combat_admin), 1),
            Dm.kv(typeof(obj_item_disk_tech_disk_debug), 1),
            Dm.kv(typeof(obj_item_flashlight_emp_debug), 1),
            Dm.kv(typeof(obj_item_geiger_counter), 1),
            Dm.kv(typeof(obj_item_healthanalyzer_super), 1),
            Dm.kv(typeof(obj_item_modular_computer_pda_crew_heads_captain), 1),
            Dm.kv(typeof(obj_item_pipe_dispenser), 1),
            Dm.kv(typeof(obj_item_stack_spacecash_c1000), 50),
            Dm.kv(typeof(obj_item_storage_box_beakers_bluespace), 1),
            Dm.kv(typeof(obj_item_storage_box_beakers_variety), 1),
            Dm.kv(typeof(obj_item_storage_bag_sheetsnatcher_debug), 1),
            Dm.kv(typeof(obj_item_uplink_debug), 1),
            Dm.kv(typeof(obj_item_uplink_nuclear_debug), 1),
            Dm.kv(typeof(obj_item_clothing_ears_earmuffs_debug), 1),
            Dm.kv(typeof(obj_item_gps_visible_debug), 1),
            Dm.kv(typeof(obj_item_clothing_glasses_meson_engine_admin), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:61
public partial class obj_item_storage_box_emergencytank : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:61
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_tank_internals_emergency_oxygen>(this); //in case anyone ever wants to do anything with spawning them, apart from crafting the box
        }
    }
}

// tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:71
public partial class obj_item_storage_box_engitank : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/engineering_boxes.dm:71
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_tank_internals_emergency_oxygen_engi>(this); //in case anyone ever wants to do anything with spawning them, apart from crafting the box
        }
    }
}
