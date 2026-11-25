// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/tenacious.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Species package SP2b (D349): the whole file (the lustrous's brain gives it; the sparring sect's rite is not ported).

/**
 * tenacious element; which makes the parent move faster while crawling
 *
 * Used by sparring sect!
 */
// tg: code/datums/elements/tenacious.dm:8
public partial class datum_element_tenacious : datum_element
{
    // tg: code/datums/elements/tenacious.dm:8
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (!ishuman(target))
            return COMPONENT_INCOMPATIBLE;
        mob_living_carbon_human valid_target = (mob_living_carbon_human)target;
        on_stat_change(valid_target, valid_target.stat); //immediately try adding movement bonus if they're in soft crit
        RegisterSignal(target, COMSIG_MOB_STATCHANGE, nameof(on_stat_change));
        ADD_TRAIT(target, TRAIT_TENACIOUS, ELEMENT_TRAIT(type));
        return dot;
    }

    // tg: code/datums/elements/tenacious.dm:18
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, COMSIG_MOB_STATCHANGE);
        REMOVE_TRAIT(target, TRAIT_TENACIOUS, ELEMENT_TRAIT(type));
        mob_living_carbon_human valid_target = (mob_living_carbon_human)target;
        if (Dm.truthy(valid_target.remove_movespeed_modifier(typeof(datum_movespeed_modifier_tenacious))))
            valid_target.balloon_alert(valid_target, I18n.T("your tenacity wears off"));
        return base.Detach(target, __extra);
    }

    ///signal called by the stat of the target changing
    // tg: code/datums/elements/tenacious.dm:27
    [SignalHandler]
    public virtual void on_stat_change(mob_living_carbon_human target = null, object new_stat = null)
    {
        if (Dm.eq(new_stat, SOFT_CRIT))
        {
            target.balloon_alert(target, I18n.T("your tenacity kicks in"));
            Waiting.NoSleep(target.add_movespeed_modifier(typeof(datum_movespeed_modifier_tenacious)));
        }
        else if (Dm.truthy(target.remove_movespeed_modifier(typeof(datum_movespeed_modifier_tenacious))))
            target.balloon_alert(target, I18n.T("your tenacity wears off"));
    }
}
