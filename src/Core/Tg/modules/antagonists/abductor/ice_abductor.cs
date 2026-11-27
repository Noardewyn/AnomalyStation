// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/abductor/ice_abductor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/antagonists/abductor/ice_abductor.dm:1
public partial class obj_structure_fluff_iced_abductor : obj_structure_fluff
{
    // tg: code/modules/antagonists/abductor/ice_abductor.dm:9
    public override object Destroy(float? force = null)
    {
        turf T = get_turf(this);
        Dm.@new<obj_effect_mob_spawn_corpse_human_abductor>(T);
        object dot = base.Destroy(force);
        return dot;
    }
}
