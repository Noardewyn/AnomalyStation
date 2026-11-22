// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates/wooden.dm
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

// tg: code/game/objects/structures/crates_lockers/crates/wooden.dm:21
public partial class obj_structure_closet_crate_wooden_toy : obj_structure_closet_crate_wooden
{
    // tg: code/game/objects/structures/crates_lockers/crates/wooden.dm:21
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_megaphone_clown>(this); // tg: code/game/objects/structures/crates_lockers/crates/wooden.dm:23
        Dm.@new<obj_item_reagent_containers_cup_soda_cans_canned_laughter>(this); // tg: code/game/objects/structures/crates_lockers/crates/wooden.dm:24
        Dm.@new<obj_item_pneumatic_cannon_pie>(this); // tg: code/game/objects/structures/crates_lockers/crates/wooden.dm:25
        Dm.@new<obj_item_food_pie_cream>(this); // tg: code/game/objects/structures/crates_lockers/crates/wooden.dm:26
        Dm.@new<obj_item_storage_crayons>(this); // tg: code/game/objects/structures/crates_lockers/crates/wooden.dm:27
    }
}
