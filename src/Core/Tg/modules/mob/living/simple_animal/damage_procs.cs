// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/damage_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file.

// tg: code/modules/mob/living/simple_animal/damage_procs.dm:1
public partial class mob_living_simple_animal : mob_living
{
    // tg: code/modules/mob/living/simple_animal/damage_procs.dm:1
    public override async DmTask<object> on_damage_loss(object amount = null, object updating_health = null, object forced = null, object damage_type = null, object difference = null)
    {
        object transfered_loss = null;
        if (!Dm.eq(damage_type, BRUTE) && !Dm.eq(damage_type, STAMINA))
            transfered_loss = simple_transfer_to_brute_loss(amount);
        if (!Dm.truthy(ckey) && stat == STABLE && Dm.num(amount) > 0)
            toggle_ai_on_damage();
        object dot = await base.on_damage_loss(amount, updating_health, forced, damage_type, difference);
        return Dm.truthy(transfered_loss) ? transfered_loss : dot;
    }

    // tg: code/modules/mob/living/simple_animal/damage_procs.dm:10
    public virtual void toggle_ai_on_damage(params object[] __extra)
    {
        if (Dm.eq(AIStatus, AI_IDLE))
            toggle_ai(AI_ON);
    }
}
