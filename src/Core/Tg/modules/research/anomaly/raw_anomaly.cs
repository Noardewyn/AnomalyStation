// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/anomaly/raw_anomaly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the raw anomaly cores that the anomaly refinery implodes into anomaly cores (create_core counts the new core in
// SSresearch's tally), the random raw core Meta's science lab maps (it turns into one of the kinds), and the inert core a
// neutralized anomaly leaves when the station has too many of its kind. The var blocks are generated (D111).

/**
 * # Raw Anomaly Cores
 *
 * The current precursor to anomaly cores, these are manufactured into 'finished' anomaly cores for use in research, items, and more.
 *
 * The current amounts created is stored in `SSresearch.created_anomaly_types[ANOMALY_CORE_TYPE_DEFINE] = amount`.
 * The hard limits are in `code/__DEFINES/anomalies.dm`.
 */
// tg: code/modules/research/anomaly/raw_anomaly.dm:9
public partial class obj_item_raw_anomaly_core : obj_item
{
    /**
     * Created the resulting core after being "made" into it.
     *
     * Arguments:
     * * newloc - Where the new core will be created
     * * del_self - should we qdel(src)
     * * count_towards_limit - should we increment the amount of created cores on SSresearch
     */
    // tg: code/modules/research/anomaly/raw_anomaly.dm:98
    public virtual object create_core(object newloc = null, float? del_self = null, float? count_towards_limit = null)
    {
        del_self ??= FALSE;
        count_towards_limit ??= FALSE;
        object dot = Dm.@new(anomaly_type, newloc);
        if (Dm.truthy(count_towards_limit))
            SSresearch.increment_existing_anomaly_cores(anomaly_type);
        if (Dm.truthy(del_self))
            qdel(this);
        return dot;
    }
}

// tg: code/modules/research/anomaly/raw_anomaly.dm:78
public partial class obj_item_raw_anomaly_core_random : obj_item_raw_anomaly_core
{
    // tg: code/modules/research/anomaly/raw_anomaly.dm:84
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        Type path = pick(subtypesof(typeof(obj_item_raw_anomaly_core))) as Type;
        Dm.@new(path, loc);
        return INITIALIZE_HINT_QDEL;
    }
}
