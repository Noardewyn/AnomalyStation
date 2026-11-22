// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/implant_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186): Meta's chemical and tracking implant kits and their siblings. The implant pad, the locator and the
// exile, beacon, deathrattle and teleport blocker cases are inert objects (docs/ai/specs/not-translated.md section 55).

// tg: code/game/objects/items/storage/boxes/implant_boxes.dm:1
public partial class obj_item_storage_box_trackimp : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/implant_boxes.dm:7
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_implantcase_tracking), 4),
            Dm.kv(typeof(obj_item_implanter), 1),
            Dm.kv(typeof(obj_item_implantpad), 1),
            Dm.kv(typeof(obj_item_locator), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/implant_boxes.dm:16
public partial class obj_item_storage_box_minertracker : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/implant_boxes.dm:21
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_implantcase_tracking), 2),
            Dm.kv(typeof(obj_item_implantcase_beacon), 2),
            Dm.kv(typeof(obj_item_implanter), 1),
            Dm.kv(typeof(obj_item_implantpad), 1),
            Dm.kv(typeof(obj_item_locator), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/implant_boxes.dm:31
public partial class obj_item_storage_box_minerdeathrattle : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/implant_boxes.dm:37
    public override async DmTask PopulateContents()
    {
        DmList items_inside = TgState.Current.obj_item_storage_box_minerdeathrattle__PopulateContents__items_inside;
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/implant_boxes.dm:45
public partial class obj_item_storage_box_chemimp : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/implant_boxes.dm:50
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_implantcase_chem), 5),
            Dm.kv(typeof(obj_item_implanter), 1),
            Dm.kv(typeof(obj_item_implantpad), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/implant_boxes.dm:58
public partial class obj_item_storage_box_exileimp : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/implant_boxes.dm:63
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_implantcase_exile), 5),
            Dm.kv(typeof(obj_item_implanter), 1),
            Dm.kv(typeof(obj_item_implantpad), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/implant_boxes.dm:71
public partial class obj_item_storage_box_beaconimp : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/implant_boxes.dm:77
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_implantcase_beacon), 3),
            Dm.kv(typeof(obj_item_implanter), 1),
            Dm.kv(typeof(obj_item_implantpad), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/implant_boxes.dm:85
public partial class obj_item_storage_box_teleport_blocker : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/implant_boxes.dm:90
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_implantcase_teleport_blocker), 2),
            Dm.kv(typeof(obj_item_implanter), 1),
            Dm.kv(typeof(obj_item_implantpad), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/implant_boxes.dm:99
public partial class obj_item_storage_box_cyber_implants : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/implant_boxes.dm:104
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_autosurgeon_syndicate_xray_eyes>(this);
        Dm.@new<obj_item_autosurgeon_syndicate_anti_stun>(this);
        Dm.@new<obj_item_autosurgeon_syndicate_reviver>(this);
    }
}

// The static list of the deathrattle kit (rules 10.6: proc-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/game/objects/items/storage/boxes/implant_boxes.dm:38  var/static/items_inside
    internal DmList obj_item_storage_box_minerdeathrattle__PopulateContents__items_inside = Dm.list(
            Dm.kv(typeof(obj_item_implantcase_deathrattle_lavaland), 5),
            Dm.kv(typeof(obj_item_implanter), 1),
            Dm.kv(typeof(obj_item_implantpad), 1));
}
