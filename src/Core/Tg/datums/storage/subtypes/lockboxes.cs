// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/lockboxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The lockbox and the medal box (gaps J4, D298); the dueling and the decrypted bitrunning boxes (gaps SR1, D333).

// tg: code/datums/storage/subtypes/lockboxes.dm:2
public partial class datum_storage_lockbox : datum_storage
{
    // tg: code/datums/storage/subtypes/lockboxes.dm:6
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_locked(STORAGE_FULLY_LOCKED);
    }
}

// tg: code/datums/storage/subtypes/lockboxes.dm:11
public partial class datum_storage_lockbox_medal : datum_storage_lockbox
{
    // tg: code/datums/storage/subtypes/lockboxes.dm:16
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_clothing_accessory_medal));
    }
}

///Dueling lockbox (gaps SR1, D333; its case and pistols: gaps GU1)
// tg: code/datums/storage/subtypes/lockboxes.dm:21
public partial class datum_storage_lockbox_dueling : datum_storage_lockbox
{
    // tg: code/datums/storage/subtypes/lockboxes.dm:25
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_gun_energy_dueling));
    }
}

///Bitrunning decrypted lockbox (gaps SR1, D333; its curiosity waits on bitrunning, not-translated.md section 208)
// tg: code/datums/storage/subtypes/lockboxes.dm:30
public partial class datum_storage_lockbox_bitrunning_decrypted : datum_storage_lockbox
{
    // tg: code/datums/storage/subtypes/lockboxes.dm:34
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_locked(STORAGE_NOT_LOCKED);
    }
}
