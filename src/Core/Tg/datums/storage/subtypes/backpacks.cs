// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/backpacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the regular backpack's storage (the outfit's backpack); syndicate G1c (D316): the smuggler's satchel's. The other backpack
// storages are their types' milestones.

///Regular backpack
// tg: code/datums/storage/subtypes/backpacks.dm:2
public partial class datum_storage_backpack : datum_storage
{
    // tg: code/datums/storage/subtypes/backpacks.dm:6
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(exception_hold_list: typeof(obj_item_fish_tank));
    }
}

///Satchel flat
// tg: code/datums/storage/subtypes/backpacks.dm:21
public partial class datum_storage_backpack_satchel_flat : datum_storage_backpack
{
    // tg: code/datums/storage/subtypes/backpacks.dm:23
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(cant_hold_list: typeof(obj_item_storage_backpack_satchel_flat)); //muh recursive backpacks
    }
}
