// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/toolboxes/fishing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch). Gaps SR1 (D333): the toolbox's init (with its fishing difficulty
// element) and the compact toolbox; the super fishing toolbox with its master hooks and lines, the
// omni-baits, the fish feed, the aquarium kit and the fish analyzer.

// tg: code/game/objects/items/storage/toolboxes/fishing.dm:18
public partial class obj_item_storage_toolbox_fishing : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/fishing.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), fishing_modifier, ITEM_SLOT_HANDS));
        return dot;
    }

    // tg: code/game/objects/items/storage/toolboxes/fishing.dm:18
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_bait_can_worm>(this); // tg: code/game/objects/items/storage/toolboxes/fishing.dm:19
        Dm.@new<obj_item_fishing_rod_unslotted>(this); // tg: code/game/objects/items/storage/toolboxes/fishing.dm:20
        Dm.@new<obj_item_fishing_hook>(this); // tg: code/game/objects/items/storage/toolboxes/fishing.dm:21
        Dm.@new<obj_item_fishing_line>(this); // tg: code/game/objects/items/storage/toolboxes/fishing.dm:22
        Dm.@new<obj_item_paper_paperslip_fishing_tip>(this); // tg: code/game/objects/items/storage/toolboxes/fishing.dm:23
    }
}

// tg: code/game/objects/items/storage/toolboxes/fishing.dm:33
public partial class obj_item_storage_toolbox_fishing_small : obj_item_storage_toolbox_fishing
{
    // tg: code/game/objects/items/storage/toolboxes/fishing.dm:33
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_fishing_rod_unslotted>(this);
        Dm.@new<obj_item_fishing_hook>(this);
        Dm.@new<obj_item_fishing_line>(this);
        Dm.@new<obj_item_paper_paperslip_fishing_tip>(this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/fishing.dm:40
public partial class obj_item_storage_toolbox_fishing_master : obj_item_storage_toolbox_fishing
{
    // tg: code/game/objects/items/storage/toolboxes/fishing.dm:47
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_fishing_rod_telescopic_master>(this);
        Dm.@new<obj_item_storage_box_fishing_hooks_master>(this);
        Dm.@new<obj_item_storage_box_fishing_lines_master>(this);
        Dm.@new<obj_item_bait_can_super_baits>(this);
        Dm.@new<obj_item_reagent_containers_cup_fish_feed>(this);
        Dm.@new<obj_item_aquarium_kit>(this);
        Dm.@new<obj_item_fish_analyzer>(this);
        await DmTask.CompletedTask;
    }
}
