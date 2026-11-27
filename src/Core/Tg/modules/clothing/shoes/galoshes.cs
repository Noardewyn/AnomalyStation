// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/galoshes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the absorbent galoshes dry the wet floor under each step; the fishing difficulty element waits for
// fishing). The var blocks are generated (D111).

// tg: code/modules/clothing/shoes/galoshes.dm:1
public partial class obj_item_clothing_shoes_galoshes : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/galoshes.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), fishing_modifier));
        return dot;
    }
}

// tg: code/modules/clothing/shoes/galoshes.dm:22
public partial class obj_item_clothing_shoes_galoshes_dry : obj_item_clothing_shoes_galoshes
{
    // tg: code/modules/clothing/shoes/galoshes.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_SHOES_STEP_ACTION, nameof(on_step));
        return dot;
    }

    // tg: code/modules/clothing/shoes/galoshes.dm:37
    [SignalHandler]
    public virtual void on_step()
    {
        turf_open t_loc = get_turf(this) as turf_open;
        // SEND_SIGNAL(t_loc, COMSIG_TURF_MAKE_DRY, TURF_WET_WATER, TRUE, INFINITY) (rules 11.1 (e), a statement)
        if (Dm.truthy(t_loc?._listen_lookup?[COMSIG_TURF_MAKE_DRY]))
            t_loc._SendSignal(COMSIG_TURF_MAKE_DRY, Dm.list(t_loc, TURF_WET_WATER, TRUE, INFINITY));
    }
}
