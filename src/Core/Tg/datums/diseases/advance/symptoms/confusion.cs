// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/confusion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/confusion.dm:9
public partial class datum_symptom_confusion : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/confusion.dm:31
    public override object Start(datum_disease_advance advanced_disease = null)
    {
        object dot = null;
        dot = base.Start(advanced_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(advanced_disease.totalStageSpeed()) >= 6)
            causes_illiteracy = TRUE;
        if (Dm.num(advanced_disease.totalResistance()) >= 6)
            brain_damage = TRUE;
        if (Dm.num(advanced_disease.totalTransmittable()) >= 6)
            power = 1.5f;
        if (Dm.num(advanced_disease.totalStealth()) >= 4)
            suppress_warning = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/confusion.dm:44
    [DmParams("advanced_disease")]
    public override async DmTask<object> End(datum_disease_advance advanced_disease = null)
    {
        advanced_disease.affected_mob.remove_status_effect(typeof(datum_status_effect_confusion));
        return await base.End(advanced_disease);
    }

    // tg: code/datums/diseases/advance/symptoms/confusion.dm:48
    public override async DmTask<object> Activate(datum_disease_advance advanced_disease = null)
    {
        object dot = null;
        dot = await base.Activate(advanced_disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon infected_mob = advanced_disease.affected_mob;
        if (advanced_disease.stage == 1 || advanced_disease.stage == 2 || advanced_disease.stage == 3 || advanced_disease.stage == 4)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                to_chat(infected_mob, span_warning(I18n.T(Dm.str(pick(Dm.list("Your head hurts.", "Your mind blanks for a moment."))))));
        }
        else
        {
            to_chat(infected_mob, span_userdanger(I18n.T("You can't think straight!")));
            await infected_mob.adjust_confusion_up_to(16 * SECONDS * Dm.num(power), 30 * SECONDS);
            if (Dm.truthy(brain_damage))
            {
                await infected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 3 * Dm.num(power), 80);
                await infected_mob.updatehealth();
            }
        }
        return dot;
    }
}
