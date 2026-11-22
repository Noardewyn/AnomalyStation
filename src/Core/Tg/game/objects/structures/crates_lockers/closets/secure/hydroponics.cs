// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/hydroponics.dm
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

// tg: code/game/objects/structures/crates_lockers/closets/secure/hydroponics.dm:6
public partial class obj_structure_closet_secure_closet_hydroponics : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/hydroponics.dm:6
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_bag_plants_portaseeder>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/hydroponics.dm:8
        Dm.@new<obj_item_plant_analyzer>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/hydroponics.dm:9
        Dm.@new<obj_item_radio_headset_headset_srv>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/hydroponics.dm:10
        Dm.@new<obj_item_cultivator>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/hydroponics.dm:11
        Dm.@new<obj_item_hatchet>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/hydroponics.dm:12
        Dm.@new<obj_item_secateurs>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/hydroponics.dm:13
    }
}
