// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/telegraph_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The vars are generated.

// tg: code/datums/ai/telegraph_effects.dm:1
public partial class obj_effect_temp_visual_telegraphing : obj_effect_temp_visual
{
    // tg: code/datums/ai/telegraph_effects.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/datums/ai/telegraph_effects.dm:13
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        // DEVIATION(DV12): . += emissive_appearance(icon, icon_state, src, alpha = 90), the box's glow in the dark
        return await base.update_overlays(__extra);
    }
}

// tg: code/datums/ai/telegraph_effects.dm:21
public partial class obj_effect_temp_visual_telegraphing_lift_travel : obj_effect_temp_visual_telegraphing
{
    // tg: code/datums/ai/telegraph_effects.dm:23
    [DmParams("mapload", "duration")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        this.duration = Dm.num(duration);
        return base.Initialize(mapload, __extra);
    }
}
