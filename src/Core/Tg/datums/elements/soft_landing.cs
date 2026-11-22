// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/soft_landing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice A: the whole file (the beds and sofas cushion a fall). The var block is generated.

/**
 * ## soft landing element!
 *
 * Non bespoke element (1 in existence) that makes objs provide a soft landing when you fall on them!
 */
// tg: code/datums/elements/soft_landing.dm:6
public partial class datum_element_soft_landing : datum_element
{
    // tg: code/datums/elements/soft_landing.dm:9
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_ATOM_INTERCEPT_Z_FALL, nameof(intercept_z_fall));
        return dot;
    }

    // tg: code/datums/elements/soft_landing.dm:15
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_ATOM_INTERCEPT_Z_FALL);
        return dot;
    }

    ///signal called by the stat of the target changing
    // tg: code/datums/elements/soft_landing.dm:20
    [SignalHandler]
    public virtual object intercept_z_fall(atom soft_object = null, object falling_movables = null, object levels = null)
    {
        turf falling_spot = get_turf(soft_object);

        if (Dm.locate<obj_structure_stairs>(falling_spot) != null)
            return Dm.bitor(FALL_INTERCEPTED, FALL_NO_MESSAGE);

        foreach (mob_living falling_victim in Dm.each<mob_living>(falling_movables))
        {
            if (soft_object == falling_victim)
                to_chat(falling_victim, span_notice(I18n.T("Your fall is cushioned by your body to provide a soft landing!")));
            else
                to_chat(falling_victim, span_notice(I18n.T("[] provides a soft landing for you!", soft_object)));
        }
        return Dm.bitor(FALL_INTERCEPTED, FALL_NO_MESSAGE);
    }
}
