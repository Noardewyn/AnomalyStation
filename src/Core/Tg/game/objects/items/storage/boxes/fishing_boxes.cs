// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/fishing_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the boxes of fishing gear (the hook and line sets and their master sets, the fish revival kit, the tackle box of
// every lure, the aquarium props box). The var blocks are generated (D111).

// tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:1
public partial class obj_item_storage_box_fishing_hooks : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:6
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_fishing_hook_magnet>(this);
        Dm.@new<obj_item_fishing_hook_shiny>(this);
        Dm.@new<obj_item_fishing_hook_weighted>(this);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:11
public partial class obj_item_storage_box_fishing_hooks_master : obj_item_storage_box_fishing_hooks
{
    // tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:11
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        Dm.@new<obj_item_fishing_hook_stabilized>(this);
        Dm.@new<obj_item_fishing_hook_jaws>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:16
public partial class obj_item_storage_box_fishing_lines : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:21
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_fishing_line_bouncy>(this);
        Dm.@new<obj_item_fishing_line_reinforced>(this);
        Dm.@new<obj_item_fishing_line_cloaked>(this);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:26
public partial class obj_item_storage_box_fishing_lines_master : obj_item_storage_box_fishing_lines
{
    // tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:26
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        Dm.@new<obj_item_fishing_line_auto_reel>(this);
    }
}

///From the fishing mystery box. It's basically a lazarus and a few bottles of strange reagents.
// tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:31
public partial class obj_item_storage_box_fish_revival_kit : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:36
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_lazarus_injector>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_fishy_reagent>(this);
        Dm.@new<obj_item_reagent_containers_cup>(this); //to splash the reagents on the fish.
        Dm.@new<obj_item_storage_fish_case>(this);
        Dm.@new<obj_item_storage_fish_case>(this);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:44
public partial class obj_item_storage_box_fishing_lures : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:53
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_paper_lures_instructions>(this);
        DmList typesof = subtypesof(typeof(obj_item_fishing_lure)) as DmList;
        foreach (Type type in Dm.each_cast<Type>(typesof))
            Dm.@new(type, this);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:59
public partial class obj_item_storage_box_aquarium_props : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/fishing_boxes.dm:65
    public override async DmTask PopulateContents()
    {
        foreach (Type prop_type in Dm.each_cast<Type>(subtypesof(typeof(obj_item_aquarium_prop))))
            Dm.@new(prop_type, this);
        await DmTask.CompletedTask;
    }
}
