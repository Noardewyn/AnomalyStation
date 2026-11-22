// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/oxygen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/oxygen.dm:9
public partial class datum_symptom_oxygen : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/oxygen.dm:26
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalResistance()) >= 8) //blood regeneration
            regenerate_blood = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/oxygen.dm:33
    public override async DmTask<object> Activate(datum_disease_advance advanced_disease = null)
    {
        object dot = null;
        dot = await base.Activate(advanced_disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon infected_mob = advanced_disease.affected_mob;
        if (advanced_disease.stage == 4 || advanced_disease.stage == 5)
        {
            infected_mob.losebreath = max(0, infected_mob.losebreath - 4);
            await infected_mob.adjust_oxy_loss(-7);
            if (prob(base_message_chance))
                to_chat(infected_mob, span_notice(I18n.T("You realize you haven't been breathing.")));
            if (Dm.truthy(regenerate_blood))
                infected_mob.adjust_blood_volume(1, maximum: BLOOD_VOLUME_NORMAL);
        }
        else
        {
            if (prob(base_message_chance))
                to_chat(infected_mob, span_notice(I18n.T("Your lungs feel great.")));
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/oxygen.dm:52
    [DmParams("advanced_disease")]
    public override object on_stage_change(datum_disease_advance advanced_disease = null)
    {
        object dot = null;
        dot = base.on_stage_change(advanced_disease);
        if (!Dm.truthy(dot))
            return FALSE;
        mob_living_carbon infected_mob = advanced_disease.affected_mob;
        if (advanced_disease.stage >= 4)
        {
            ADD_TRAIT(infected_mob, TRAIT_NOBREATH, DISEASE_TRAIT);
            if (advanced_disease.stage == 4)
                to_chat(infected_mob, span_notice(I18n.T("You don't feel the need to breathe anymore.")));
        }
        else
        {
            REMOVE_TRAIT(infected_mob, TRAIT_NOBREATH, DISEASE_TRAIT);
            if (Dm.truthy(advanced_disease.stage_peaked) && advanced_disease.stage == 3)
                to_chat(infected_mob, span_notice(I18n.T("You feel the need to breathe again.")));
        }
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/oxygen.dm:67
    [DmParams("advanced_disease")]
    public override async DmTask<object> End(datum_disease_advance advanced_disease = null)
    {
        object dot = null;
        dot = await base.End(advanced_disease);
        if (!Dm.truthy(dot))
            return dot;
        REMOVE_TRAIT(advanced_disease.affected_mob, TRAIT_NOBREATH, DISEASE_TRAIT);
        return dot;
    }
}
