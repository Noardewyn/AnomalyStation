// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/mob_grabber.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Grab onto mobs we attack
// tg: code/datums/elements/mob_grabber.dm:2
public partial class datum_element_mob_grabber : datum_element
{
    // tg: code/datums/elements/mob_grabber.dm:10
    [DmParams("target", "minimum_stat", "steal_from_others")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        this.minimum_stat = Dm.arg(__extra, 0) ?? SOFT_CRIT;
        this.steal_from_others = Dm.arg(__extra, 1) ?? TRUE;
        RegisterSignals(target, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_HOSTILE_PRE_ATTACKINGTARGET), nameof(grab_mob));
        return dot;
    }

    // tg: code/datums/elements/mob_grabber.dm:18
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_HOSTILE_PRE_ATTACKINGTARGET));
        return base.Detach(source, __extra);
    }

    /// Try and grab something we attacked
    // tg: code/datums/elements/mob_grabber.dm:23
    [SignalHandler]
    public virtual object grab_mob(mob_living source = null, atom target_ = null, object proximity = null, DmList modifiers = null)
    {
        if (target_ is not mob_living target || !Dm.truthy(proximity) || target.stat < Dm.num(minimum_stat))
            return NONE;
        atom currently_pulled = target.pulledby;
        if (currently_pulled != null && (!Dm.truthy(steal_from_others) || currently_pulled == source))
            return NONE;
        Waiting.Detach(target.grabbedby(source)); // INVOKE_ASYNC
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}
