// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/choking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): choking and acute respiratory distress. The var blocks are generated (D111).

// tg: code/datums/diseases/advance/symptoms/choking.dm:10
public partial class datum_symptom_choking : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/choking.dm:31
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStageSpeed()) >= 8)
            symptom_delay = 15.5f;
        if (Dm.num(A.totalStealth()) >= 4)
            suppress_warning = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/choking.dm:40
    public override async DmTask<object> Activate(datum_disease_advance advanced_disease = null)
    {
        object dot = null;
        dot = await base.Activate(advanced_disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon infected_mob = advanced_disease.affected_mob;
        if (advanced_disease.stage == 1 || advanced_disease.stage == 2)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                to_chat(infected_mob, span_warning(I18n.T(Dm.str(pick(Dm.list("You're having difficulty breathing.", "Your breathing becomes heavy."))))));
        }
        else if (advanced_disease.stage == 3 || advanced_disease.stage == 4)
        {
            if (!Dm.truthy(suppress_warning))
                to_chat(infected_mob, span_warning(I18n.T(Dm.str(pick(Dm.list("Your windpipe feels like a straw.", "Your breathing becomes tremendously difficult."))))));
            else
                to_chat(infected_mob, span_warning(I18n.T("You feel very [].", I18n.T(Dm.str(pick(Dm.list("dizzy", "woozy", "faint"))))))); //fake bloodloss messages
            await Choke_stage_3_4(infected_mob, advanced_disease);
            await infected_mob.emote("gasp");
        }
        else
        {
            to_chat(infected_mob, span_userdanger(I18n.T(Dm.str(pick(Dm.list("You're choking!", "You can't breathe!"))))));
            await Choke(infected_mob, advanced_disease);
            await infected_mob.emote("gasp");
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/choking.dm:63
    public virtual async DmTask<object> Choke_stage_3_4(mob_living M = null, datum_disease_advance A = null)
    {
        await M.adjust_oxy_loss(rand(6, 13));
        return 1;
    }

    // tg: code/datums/diseases/advance/symptoms/choking.dm:67
    public virtual async DmTask<object> Choke(mob_living M = null, datum_disease_advance A = null)
    {
        await M.adjust_oxy_loss(rand(10, 18));
        return 1;
    }
}

/*
//////////////////////////////////////

Asphyxiation

	Very very noticable.
	Decreases stage speed.
	Decreases transmittability.

Bonus
	Inflicts large spikes of oxyloss
	Introduces Asphyxiating drugs to the system

//////////////////////////////////////
*/
// tg: code/datums/diseases/advance/symptoms/choking.dm:87
public partial class datum_symptom_asphyxiation : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/choking.dm:109
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStageSpeed()) >= 8)
            paralysis = TRUE;
        if (Dm.num(A.totalTransmittable()) >= 8)
            power = 2;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/choking.dm:118
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (A.stage == 3 || A.stage == 4)
        {
            to_chat(M, span_warning(Dm.text("<b>[]", I18n.T(Dm.str(pick(Dm.list("Your windpipe feels thin.", "Your lungs feel small.")))))));
            await Asphyxiate_stage_3_4(M, A);
            await M.emote("gasp");
        }
        else if (A.stage == 5)
        {
            to_chat(M, span_userdanger(I18n.T(Dm.str(pick(Dm.list("Your lungs hurt!", "It hurts to breathe!"))))));
            await Asphyxiate(M, A);
            await M.emote("gasp");
            if (Dm.num(M.get_oxy_loss()) >= (M.maxHealth / (200f / 120)))
            {
                M.visible_message(span_warning(I18n.T("[] stops breathing, as if their lungs have totally collapsed!", M)));
                await Asphyxiate_death(M, A);
            }
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/choking.dm:137
    public virtual async DmTask<object> Asphyxiate_stage_3_4(mob_living M = null, datum_disease_advance A = null)
    {
        float get_damage = rand(10, 15) * Dm.num(power);
        await M.adjust_oxy_loss(get_damage);
        return 1;
    }

    // tg: code/datums/diseases/advance/symptoms/choking.dm:142
    public virtual async DmTask<object> Asphyxiate(mob_living M = null, datum_disease_advance A = null)
    {
        float get_damage = rand(15, 21) * Dm.num(power);
        await M.adjust_oxy_loss(get_damage);
        if (Dm.truthy(paralysis))
            await ((datum_reagents)M.reagents).add_reagent_list(Dm.list(Dm.kv(typeof(datum_reagent_toxin_pancuronium), 3), Dm.kv(typeof(datum_reagent_toxin_sodium_thiopental), 3)));
        return 1;
    }

    // tg: code/datums/diseases/advance/symptoms/choking.dm:149
    public virtual async DmTask<object> Asphyxiate_death(mob_living M = null, datum_disease_advance A = null)
    {
        float get_damage = rand(25, 35) * Dm.num(power);
        await M.adjust_oxy_loss(get_damage);
        await M.adjust_organ_loss(ORGAN_SLOT_BRAIN, get_damage / 2);
        return 1;
    }
}
