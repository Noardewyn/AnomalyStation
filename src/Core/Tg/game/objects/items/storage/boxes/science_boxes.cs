// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/science_boxes.dm
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

// tg: code/game/objects/items/storage/boxes/science_boxes.dm:32
public partial class obj_item_storage_box_disks : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/science_boxes.dm:32
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 8; i++) // tg: code/game/objects/items/storage/boxes/science_boxes.dm:33
        {
            Dm.@new<obj_item_delivery_small_floppy>(this); // tg: code/game/objects/items/storage/boxes/science_boxes.dm:34
        }
    }
}

// tg: code/game/objects/items/storage/boxes/science_boxes.dm:17
public partial class obj_item_storage_box_petridish : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/science_boxes.dm:17
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/science_boxes.dm:18
        {
            Dm.@new<obj_item_petri_dish>(this); // tg: code/game/objects/items/storage/boxes/science_boxes.dm:19
        }
    }
}

// tg: code/game/objects/items/storage/boxes/science_boxes.dm:37 (gaps package C5)
public partial class obj_item_storage_box_monkeycubes : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/science_boxes.dm:46
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 5; i++)
            Dm.@new(cube_type, this);
    }
}

// Gaps SR1 (D333): the file's other boxes (swabs, gorilla cubes, the stock parts kits, the R&D boards, the stabilized extracts).

// tg: code/game/objects/items/storage/boxes/science_boxes.dm:8
public partial class obj_item_storage_box_swab : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/science_boxes.dm:8
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_swab>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/science_boxes.dm:66
public partial class obj_item_storage_box_gorillacubes : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/science_boxes.dm:66
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++)
        {
            Dm.@new<obj_item_food_monkeycube_gorilla>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/science_boxes.dm:74
public partial class obj_item_storage_box_stockparts_basic : obj_item_storage_box_stockparts
{
    // tg: code/game/objects/items/storage/boxes/science_boxes.dm:74
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_stock_parts_capacitor), 3),
            Dm.kv(typeof(obj_item_stock_parts_servo), 3),
            Dm.kv(typeof(obj_item_stock_parts_matter_bin), 3),
            Dm.kv(typeof(obj_item_stock_parts_micro_laser), 3),
            Dm.kv(typeof(obj_item_stock_parts_scanning_module), 3));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/science_boxes.dm:89
public partial class obj_item_storage_box_stockparts_deluxe : obj_item_storage_box_stockparts
{
    // tg: code/game/objects/items/storage/boxes/science_boxes.dm:89
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_stock_parts_capacitor_quadratic), 3),
            Dm.kv(typeof(obj_item_stock_parts_scanning_module_triphasic), 3),
            Dm.kv(typeof(obj_item_stock_parts_servo_femto), 3),
            Dm.kv(typeof(obj_item_stock_parts_micro_laser_quadultra), 3),
            Dm.kv(typeof(obj_item_stock_parts_matter_bin_bluespace), 3));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/science_boxes.dm:104
public partial class obj_item_storage_box_rndboards : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/science_boxes.dm:104
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_circuitboard_machine_protolathe_offstation>(this);
        Dm.@new<obj_item_circuitboard_machine_destructive_analyzer>(this);
        Dm.@new<obj_item_circuitboard_machine_circuit_imprinter_offstation>(this);
        Dm.@new<obj_item_circuitboard_computer_rdconsole_unlocked>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/science_boxes.dm:115
public partial class obj_item_storage_box_stabilized : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/science_boxes.dm:115
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_slimecross_stabilized_adamantine), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_black), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_blue), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_bluespace), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_cerulean), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_darkblue), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_darkpurple), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_gold), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_green), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_grey), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_lightpink), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_metal), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_oil), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_orange), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_pink), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_purple), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_pyrite), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_rainbow), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_red), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_sepia), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_silver), 1),
            Dm.kv(typeof(obj_item_slimecross_stabilized_yellow), 1));
        generate_items_inside(items_inside, this);
    }
}
