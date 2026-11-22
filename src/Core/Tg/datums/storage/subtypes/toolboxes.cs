// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/toolboxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The monkey gun case's lock (syndicate G4c, D316) and the fishing toolbox's storage (gaps SR1, D333). The var blocks are generated
// (D111).

///Monkey guncase
// tg: code/datums/storage/subtypes/toolboxes.dm:26
public partial class datum_storage_toolbox_guncase_monkey : datum_storage_toolbox_guncase
{
    // tg: code/datums/storage/subtypes/toolboxes.dm:26
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_locked(STORAGE_SOFT_LOCKED);
    }
}

///Fishing toolbox
// tg: code/datums/storage/subtypes/toolboxes.dm:36
public partial class datum_storage_toolbox_fishing : datum_storage_toolbox
{
    // tg: code/datums/storage/subtypes/toolboxes.dm:36
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(exception_hold_list: typeof(obj_item_fishing_rod));
    }
}
