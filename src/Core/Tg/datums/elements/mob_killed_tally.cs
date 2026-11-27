// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/mob_killed_tally.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Mob Killed Tally; which ticks up a blackbox when the mob dies
 *
 * Used for all the mining mobs!
 */
// tg: code/datums/elements/mob_killed_tally.dm:6
public partial class datum_element_mob_killed_tally : datum_element
{
    // tg: code/datums/elements/mob_killed_tally.dm:12
    [DmParams("target", "tally_string")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object tally_string = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);

        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_death));

        this.tally_string = tally_string;
        return dot;
    }

    // tg: code/datums/elements/mob_killed_tally.dm:22
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, COMSIG_LIVING_DEATH);
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/mob_killed_tally.dm:26
    [SignalHandler]
    public virtual void on_death(mob_living target = null, object gibbed = null)
    {
        SSblackbox.record_feedback("tally", tally_string, 1, target.type);
    }
}
