// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/clever_girl.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/elements/clever_girl.dm:1
public partial class datum_element_clever_girl : datum_element
{
    // tg: code/datums/elements/clever_girl.dm:3
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignals(target, Dm.list(COMSIG_MOB_OPENED_AIRLOCK, COMSIG_MOB_OPENED_DOOR), nameof(on_open_door));
        return dot;
    }

    // tg's Detach for this element is declared on /datum/element/cleaning (clever_girl.dm:9), so the signals stay registered
    // on Detach, as in tg.

    // tg: code/datums/elements/clever_girl.dm:13
    [SignalHandler]
    public virtual void on_open_door(mob_living source = null, object forced = null)
    {
        if (!Dm.eq(forced, DEFAULT_DOOR_CHECKS))
            return;
        if (source.client == null)
            return;
        Dm.Deferred("code/datums/elements/clever_girl.dm:17", "on_open_door: source.client?.give_award(/datum/award/achievement/misc/clever_girl, src), the achievements");
    }
}
