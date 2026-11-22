// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/simple_flying.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/**
 * # simple flying element!
 *
 * Non bespoke element (1 in existence) that makes animals fly while living and... not while dead!
 * Note: works for carbons and above, but please do something better. humans have wings got dangit!
 */
// tg: code/datums/elements/simple_flying.dm:7
public partial class datum_element_simple_flying : datum_element
{
    // tg: code/datums/elements/simple_flying.dm:9
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        mob_living valid_target = (mob_living)target;
        on_stat_change(valid_target, valid_target.stat); //immediately try adding flight if they're conscious
        RegisterSignal(target, COMSIG_MOB_STATCHANGE, nameof(on_stat_change));
        return dot;
    }

    // tg: code/datums/elements/simple_flying.dm:17
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_MOB_STATCHANGE);
        REMOVE_TRAIT(source, TRAIT_MOVE_FLYING, ELEMENT_TRAIT(type));
        return dot;
    }

    ///signal called by the stat of the target changing
    // tg: code/datums/elements/simple_flying.dm:23
    [SignalHandler]
    public virtual void on_stat_change(mob_living target = null, object new_stat = null)
    {
        if (Dm.eq(new_stat, STABLE))
            ADD_TRAIT(target, TRAIT_MOVE_FLYING, ELEMENT_TRAIT(type));
        else
            REMOVE_TRAIT(target, TRAIT_MOVE_FLYING, ELEMENT_TRAIT(type));
    }
}
