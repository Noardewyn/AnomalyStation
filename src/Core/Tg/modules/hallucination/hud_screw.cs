// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/hud_screw.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var block is generated (D111).

/// Screwyhud, makes the user's health bar hud wonky
// tg: code/modules/hallucination/hud_screw.dm:2
public partial class datum_hallucination_screwy_hud : datum_hallucination
{
    // tg: code/modules/hallucination/hud_screw.dm:10
    public override async DmTask<object> start()
    {
        await hallucinator.apply_status_effect(screwy_hud_type as Type, type);
        QDEL_IN_HALLUCINATION(this, rand(10 * SECONDS, 25 * SECONDS), "code/modules/hallucination/hud_screw.dm", 12);
        return TRUE;
    }

    // tg: code/modules/hallucination/hud_screw.dm:15
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(hallucinator))
            hallucinator.remove_status_effect(screwy_hud_type as Type, type);
        return base.Destroy(force);
    }
}
