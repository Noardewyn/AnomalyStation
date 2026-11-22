// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/content_barfer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * Content Barfer; which expels the contents of a mob when it dies, or is transformed
 *
 * Used for morphs and bileworms!
 */
// tg: code/datums/elements/content_barfer.dm:8
public partial class datum_element_content_barfer : datum_element
{
    // tg: code/datums/elements/content_barfer.dm:8
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignals(target, Dm.list(COMSIG_LIVING_DEATH, COMSIG_LIVING_ON_WABBAJACKED, COMSIG_LIVING_UNSHAPESHIFTED, COMSIG_MOB_CHANGED_TYPE), nameof(barf_contents));
        return dot;
    }

    // tg: code/datums/elements/content_barfer.dm:16
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, Dm.list(COMSIG_LIVING_DEATH, COMSIG_LIVING_ON_WABBAJACKED, COMSIG_LIVING_UNSHAPESHIFTED, COMSIG_MOB_CHANGED_TYPE));
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/content_barfer.dm:20
    [SignalHandler]
    public virtual void barf_contents(mob_living target = null)
    {
        foreach (atom_movable barfed_out in Dm.each<atom_movable>(target))
        {
            if (HAS_TRAIT(barfed_out, TRAIT_NOT_BARFABLE))
                continue;
            Waiting.NoSleep(barfed_out.forceMove(target.loc));
            if (prob(90))
                Waiting.NoSleep(step(barfed_out, Dm.num(pick(GLOB.alldirs))));
        }
    }
}
