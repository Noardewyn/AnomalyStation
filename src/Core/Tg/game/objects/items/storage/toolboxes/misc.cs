// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/toolboxes/misc.dm
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

// tg: code/game/objects/items/storage/toolboxes/misc.dm:27
public partial class obj_item_storage_toolbox_artistic : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/misc.dm:27
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_storage_crayons>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:28
        Dm.@new<obj_item_toy_crayon_spraycan>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:29
        Dm.@new<obj_item_toy_crayon_spraycan>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:30
        Dm.@new<obj_item_paint_palette>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:31
        Dm.@new<obj_item_paint_anycolor>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:32
        Dm.@new<obj_item_paint_anycolor>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:33
        Dm.@new<obj_item_paint_anycolor>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:34
    }
}

// tg: code/game/objects/items/storage/toolboxes/misc.dm:49
public partial class obj_item_storage_toolbox_crafter : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/misc.dm:49
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_storage_crayons>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:50
        Dm.@new<obj_item_camera>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:51
        Dm.@new<obj_item_camera_film>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:52
        Dm.@new<obj_item_chisel>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:53
        Dm.@new<obj_item_stack_pipe_cleaner_coil_red>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:54
        Dm.@new<obj_item_stack_pipe_cleaner_coil_yellow>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:55
        Dm.@new<obj_item_stack_pipe_cleaner_coil_blue>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:56
        Dm.@new<obj_item_stack_pipe_cleaner_coil_green>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:57
        Dm.@new<obj_item_stack_pipe_cleaner_coil_pink>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:58
        Dm.@new<obj_item_stack_pipe_cleaner_coil_orange>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:59
        Dm.@new<obj_item_stack_pipe_cleaner_coil_cyan>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:60
        Dm.@new<obj_item_stack_pipe_cleaner_coil_white>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:61
        Dm.@new<obj_item_stack_pipe_cleaner_coil_brown>(this); // tg: code/game/objects/items/storage/toolboxes/misc.dm:62
    }
}

// Gaps SR1 (D333): the drone's toolbox.
// tg: code/game/objects/items/storage/toolboxes/misc.dm:1
public partial class obj_item_storage_toolbox_drone : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/misc.dm:8
    public override async DmTask PopulateContents()
    {
        object pickedcolor = pick("red", "yellow", "green", "blue", "pink", "orange", "cyan", "white");
        Dm.@new<obj_item_screwdriver>(this);
        Dm.@new<obj_item_wrench>(this);
        Dm.@new<obj_item_weldingtool>(this);
        Dm.@new<obj_item_crowbar>(this);
        // tg passes pickedcolor as the coil's third argument, its Initialize's `merge` (:515 of cable.dm), where only its truth is
        // read: a colour name is true, so the coil merges as with TRUE (the port's typed `merge` takes the number)
        Dm.@new<obj_item_stack_cable_coil>(this, MAXCOIL, Dm.truthy(pickedcolor) ? TRUE : FALSE);
        Dm.@new<obj_item_wirecutters>(this);
        Dm.@new<obj_item_multitool>(this);
    }
}
