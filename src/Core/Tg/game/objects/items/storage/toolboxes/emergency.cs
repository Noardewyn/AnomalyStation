// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/toolboxes/emergency.dm
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

// tg: code/game/objects/items/storage/toolboxes/emergency.dm:8
public partial class obj_item_storage_toolbox_emergency : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/emergency.dm:8
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_crowbar_red>(this); // tg: code/game/objects/items/storage/toolboxes/emergency.dm:9
        Dm.@new<obj_item_weldingtool_mini>(this); // tg: code/game/objects/items/storage/toolboxes/emergency.dm:10
        Dm.@new<obj_item_extinguisher_mini>(this); // tg: code/game/objects/items/storage/toolboxes/emergency.dm:11
        float sw__1 = rand(1, 3); // tg: code/game/objects/items/storage/toolboxes/emergency.dm:12
        if (sw__1 == 1)
        {
            Dm.@new<obj_item_flashlight>(this); // tg: code/game/objects/items/storage/toolboxes/emergency.dm:14
        }
        else if (sw__1 == 2)
        {
            Dm.@new<obj_item_flashlight_glowstick>(this); // tg: code/game/objects/items/storage/toolboxes/emergency.dm:16
        }
        else if (sw__1 == 3)
        {
            Dm.@new<obj_item_flashlight_flare>(this); // tg: code/game/objects/items/storage/toolboxes/emergency.dm:18
        }
        Dm.@new<obj_item_radio_off>(this); // tg: code/game/objects/items/storage/toolboxes/emergency.dm:19
    }
}
