// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/toolboxes/service.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T1 (D164): the electrical toolbox's PopulateContents. The rest of the file (the artistic and other service
// toolboxes) is the service jobs' milestone.

// tg: code/game/objects/items/storage/toolboxes/service.dm:2
public partial class obj_item_storage_toolbox_electrical : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/service.dm:7
    public override async DmTask PopulateContents()
    {
        object pickedcolor = pick(GLOB.cable_colors);
        Dm.@new<obj_item_screwdriver>(this); // tg: code/game/objects/items/storage/toolboxes/service.dm:9
        Dm.@new<obj_item_wirecutters>(this); // tg: code/game/objects/items/storage/toolboxes/service.dm:10
        Dm.@new<obj_item_t_scanner>(this); // tg: code/game/objects/items/storage/toolboxes/service.dm:11
        Dm.@new<obj_item_crowbar>(this); // tg: code/game/objects/items/storage/toolboxes/service.dm:12
        obj_item_stack_cable_coil new_cable_one = Dm.@new<obj_item_stack_cable_coil>(this, MAXCOIL);
        new_cable_one.set_cable_color(pickedcolor);
        obj_item_stack_cable_coil new_cable_two = Dm.@new<obj_item_stack_cable_coil>(this, MAXCOIL);
        new_cable_two.set_cable_color(pickedcolor);
        if (prob(5))
            Dm.@new<obj_item_clothing_gloves_color_yellow>(this); // tg: code/game/objects/items/storage/toolboxes/service.dm:19
        else
        {
            obj_item_stack_cable_coil new_cable_three = Dm.@new<obj_item_stack_cable_coil>(this, MAXCOIL);
            new_cable_three.set_cable_color(pickedcolor);
        }
    }
}
