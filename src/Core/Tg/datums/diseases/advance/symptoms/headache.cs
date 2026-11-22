// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/headache.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/*Headache
 * Slightly reduces stealth
 * Increases resistance tremendously
 * Increases stage speed
 * No change to transmissibility
 * Low level
 * Bonus: Displays an annoying message! Should be used for buffing your disease.
*/
// tg: code/datums/diseases/advance/symptoms/headache.dm:9
public partial class datum_symptom_headache : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/headache.dm:28
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 4)
            base_message_chance = 50;
        if (Dm.num(A.totalStageSpeed()) >= 6) //severe pain
            power = 2;
        if (Dm.num(A.totalStageSpeed()) >= 9) //cluster headaches
        {
            symptom_delay = 45;
            power = 3;
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/headache.dm:40
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (HAS_TRAIT(M, TRAIT_SOOTHED_HEADACHE))
            return dot;
        if (Dm.num(power) < 2)
        {
            if (prob(base_message_chance) || A.stage >= 4)
                to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("Your head hurts.", "Your head pounds."))))));
        }
        if (Dm.num(power) >= 2 && A.stage >= 4)
        {
            to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("Your head hurts a lot.", "Your head pounds incessantly."))))));
            await M.adjust_stamina_loss(25);
        }
        if (Dm.num(power) >= 3 && A.stage >= 5)
        {
            to_chat(M, span_userdanger(I18n.T(Dm.str(pick(Dm.list("Your head hurts!", "You feel a burning knife inside your brain!", "A wave of pain fills your head!"))))));
            await M.Stun(35);
        }
        return dot;
    }
}
