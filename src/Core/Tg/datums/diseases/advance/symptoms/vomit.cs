// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/vomit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var blocks are generated (D111). The vomit splatter is the carbon's vomit (its decal, sound
// and message); a host's vomit spreads the disease to those who walk on it.

// tg: code/datums/diseases/advance/symptoms/vomit.dm:10
public partial class datum_symptom_vomit : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/vomit.dm:35
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 4)
            suppress_warning = TRUE;
        if (Dm.num(A.totalResistance()) >= 9) //blood vomit
            vomit_blood = TRUE;
        if (Dm.num(A.totalTransmittable()) >= 9) //projectile vomit
            proj_vomit = 5;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/vomit.dm:46
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (A.stage == 1 || A.stage == 2 || A.stage == 3 || A.stage == 4)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel nauseated.", "You feel like you're going to throw up!"))))));
        }
        else
            await vomit(M as mob_living_carbon);
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/vomit.dm:58
    public virtual async DmTask vomit(mob_living_carbon vomiter = null)
    {
        float deductable_nutrition = 0;
        float constructed_flags = (MOB_VOMIT_MESSAGE | MOB_VOMIT_HARM);
        Type type_of_vomit = typeof(obj_effect_decal_cleanable_vomit_toxic);
        if (Dm.truthy(vomit_nebula))
        {
            type_of_vomit = typeof(obj_effect_decal_cleanable_vomit_nebula);
            deductable_nutrition = 10;
        }
        else
        {
            Dm.combine(ref constructed_flags, MOB_VOMIT_STUN);
            deductable_nutrition = 20;
        }
        if (Dm.truthy(vomit_blood))
            Dm.combine(ref constructed_flags, MOB_VOMIT_BLOOD);
        await vomiter.vomit(vomit_flags: constructed_flags, vomit_type: type_of_vomit, lost_nutrition: deductable_nutrition, distance: proj_vomit);
    }
}
