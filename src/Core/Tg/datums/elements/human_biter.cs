// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/human_biter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5).

/// Allows carbons with heads to attempt to bite mobs if attacking with cuffed hands / missing arms
// tg: code/datums/elements/human_biter.dm:4
public partial class datum_element_human_biter : datum_element
{
    // tg: code/datums/elements/human_biter.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!iscarbon(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_LIVING_EARLY_UNARMED_ATTACK, nameof(try_bite));
        return dot;
    }

    // tg: code/datums/elements/human_biter.dm:11
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_LIVING_EARLY_UNARMED_ATTACK);
        return dot;
    }

    // tg: code/datums/elements/human_biter.dm:15
    public virtual object try_bite(mob_living_carbon_human source = null, atom target = null, object proximity_flag = null, DmList modifiers = null)
    {
        if (!Dm.truthy(proximity_flag) || !Dm.truthy(source.combat_mode) || Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) || !isliving(target))
            return NONE;

        // If we can attack like normal, just go ahead and do that
        if (Dm.truthy(source.can_unarmed_attack()))
            return NONE;

        if (Dm.truthy(Waiting.NoSleep(target.attack_paw(source, modifiers))))
            return COMPONENT_CANCEL_ATTACK_CHAIN; // bite successful!

        return COMPONENT_SKIP_ATTACK; // we will fail anyways if we try to attack normally, so skip the rest
    }
}
