// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/cargo_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SR1 (D333): the file whole (the shipping supplies box and its debug kind). The var blocks are generated (D111).

// This file contains all boxes used by the Cargo department and its purpose on the station.

// tg: code/game/objects/items/storage/boxes/cargo_boxes.dm:8
public partial class obj_item_storage_box_shipping : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/cargo_boxes.dm:8
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_dest_tagger), 1),
            Dm.kv(typeof(obj_item_universal_scanner), 1),
            Dm.kv(typeof(obj_item_stack_package_wrap_small), 2),
            Dm.kv(typeof(obj_item_stack_wrapping_paper_small), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/cargo_boxes.dm:21
public partial class obj_item_storage_box_shipping_debug : obj_item_storage_box_shipping
{
    // tg: code/game/objects/items/storage/boxes/cargo_boxes.dm:21
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_universal_scanner), 1),
            Dm.kv(typeof(obj_item_stack_package_wrap_small), 2),
            Dm.kv(typeof(obj_item_stack_wrapping_paper_small), 2),
            Dm.kv(typeof(obj_item_boulder_beacon), 1),
            Dm.kv(typeof(obj_item_stack_conveyor_thirty), 1),
            Dm.kv(typeof(obj_item_conveyor_switch_construct), 1),
            Dm.kv(typeof(obj_item_construction_plumbing), 1));
        generate_items_inside(items_inside, this);
    }
}
