// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (Gaps H3, the bar): only the vortex energy's Initialize, so its plain telegraphs (the disco machine's hierofunk spawns
// the edge telegraph) are live. The Hierophant itself, its chasers, blasts, walls, squares and beacon come with Lavaland's
// megafauna.

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:522
public partial class obj_effect_temp_visual_hierophant : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:528
    [DmParams("mapload", "new_caster")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        var new_caster = Dm.arg(__extra, 0) as mob_living;
        object dot = base.Initialize(mapload, __extra);
        if (new_caster != null)
            caster = new_caster;
        return dot;
    }
}
