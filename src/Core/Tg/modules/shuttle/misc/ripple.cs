// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/misc/ripple.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D301).

// tg: code/modules/shuttle/misc/ripple.dm:1
public partial class obj_effect_abstract_ripple : obj_effect_abstract
{
    // tg: code/modules/shuttle/misc/ripple.dm:15
    [DmParams("mapload", "time_left")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object time_left = Dm.arg(__extra, 0);
        Dm.animate(this, Dm.named("alpha", 255), Dm.named("time", time_left));
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_animation)), 8, TIMER_CLIENT_TIME);
        return dot;
    }

    // tg: code/modules/shuttle/misc/ripple.dm:20
    public virtual void stop_animation()
    {
        icon_state = "medi_holo_no_anim";
    }
}
