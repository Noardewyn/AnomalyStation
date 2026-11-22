// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/status_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (scope.md 2e "human"): the human's stun scaling by its species' stunmod and the heavy sleeper
// trait; cure_husk's body redraw is P3's appearance (GAP below).

// tg: code/modules/mob/living/carbon/human/status_procs.dm:34
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/status_procs.dm:2
    public override async DmTask<object> Stun(object amount = null, float? ignore_canstun = null)
    {
        ignore_canstun ??= FALSE;
        amount = spec_stun(amount);
        return await base.Stun(amount, ignore_canstun);
    }

    // tg: code/modules/mob/living/carbon/human/status_procs.dm:6
    public override async DmTask<object> Knockdown(object amount = null, float? daze_amount = null, float? ignore_canstun = null)
    {
        daze_amount ??= 0;
        ignore_canstun ??= FALSE;
        amount = spec_stun(amount);
        return await base.Knockdown(amount, daze_amount, ignore_canstun);
    }

    // tg: code/modules/mob/living/carbon/human/status_procs.dm:10
    public override async DmTask<object> Paralyze(object amount = null, float? ignore_canstun = null)
    {
        ignore_canstun ??= FALSE;
        amount = spec_stun(amount);
        return await base.Paralyze(amount, ignore_canstun);
    }

    // tg: code/modules/mob/living/carbon/human/status_procs.dm:14
    public override async DmTask<object> Immobilize(object amount = null, float? ignore_canstun = null)
    {
        ignore_canstun ??= FALSE;
        amount = spec_stun(amount);
        return await base.Immobilize(amount, ignore_canstun);
    }

    // tg: code/modules/mob/living/carbon/human/status_procs.dm:18
    public override async DmTask<object> Unconscious(object amount = null, float? ignore_canstun = null)
    {
        ignore_canstun ??= FALSE;
        amount = spec_stun(amount);
        if (HAS_TRAIT(this, TRAIT_HEAVY_SLEEPER))
            amount = Dm.mul(amount, (rand(125, 130) * 0.01f));
        return await base.Unconscious(amount, ignore_canstun);
    }

    // tg: code/modules/mob/living/carbon/human/status_procs.dm:24
    public override async DmTask<object> Sleeping(object amount = null)
    {
        if (HAS_TRAIT(this, TRAIT_HEAVY_SLEEPER))
            amount = Dm.mul(amount, (rand(125, 130) * 0.01f));
        return await base.Sleeping(amount);
    }

    // tg: code/modules/mob/living/carbon/human/status_procs.dm:29
    [DmParams("sources")]
    public override async DmTask<object> cure_husk(object source = null)
    {
        object dot = null;
        dot = await base.cure_husk(source);
        if (Dm.truthy(dot))
        {
            await update_body_parts();
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/status_procs.dm:34
    public virtual object spec_stun(object amount = null)
    {
        // DEVIATION(DV83): an inert human (D112: Meta's monkeys until C5, whose Initialize never made their DNA) has no species' stunmod
        if (dna == null && InertSeam.IsInert(this))
            return amount;
        DmList stun_amount = Dm.list(Dm.mul(((datum_species)dna.species).stunmod, amount));
        if (Dm.truthy(_listen_lookup?[COMSIG_HUMAN_SPEC_STUN]))
            _SendSignal(COMSIG_HUMAN_SPEC_STUN, Dm.list(this, stun_amount));
        return stun_amount[1];
    }
}
