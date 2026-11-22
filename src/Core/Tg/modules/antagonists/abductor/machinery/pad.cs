// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/abductor/machinery/pad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the teleport flash /obj/effect/temp_visual/teleport_abductor translated (the syndicate campaign G2: the
// syndicate teleporter's flash is its subtype); the abductors' pad waits for the abductors.

// tg: code/modules/antagonists/abductor/machinery/pad.dm:53
public partial class obj_effect_temp_visual_teleport_abductor : obj_effect_temp_visual
{
    // tg: code/modules/antagonists/abductor/machinery/pad.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        do_sparks(10, FALSE, loc);
        return dot;
    }
}
