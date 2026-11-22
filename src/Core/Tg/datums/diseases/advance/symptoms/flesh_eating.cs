// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/flesh_eating.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): necrotizing fasciitis and autophagocytosis necrosis. The var blocks are generated (D111).

/*
Necrotizing Fasciitis (AKA Flesh-Eating Disease)

	Very very noticable.
	Lowers resistance tremendously.
	No changes to stage speed.
	Decreases transmissibility tremendously.
	Fatal Level.

Bonus
	Deals brute damage over time.
*/
// tg: code/datums/diseases/advance/symptoms/flesh_eating.dm:11
public partial class datum_symptom_flesh_eating : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/flesh_eating.dm:32
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalResistance()) >= 7) //extra bleeding
            bleed = TRUE;
        if (Dm.num(A.totalTransmittable()) >= 8) //extra stamina damage
            pain = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/flesh_eating.dm:41
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (A.stage == 2 || A.stage == 3)
        {
            if (prob(base_message_chance))
                to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel a sudden pain across your body.", "Drops of blood appear suddenly on your skin."))))));
        }
        else if (A.stage == 4 || A.stage == 5)
        {
            to_chat(M, span_userdanger(I18n.T(Dm.str(pick(Dm.list("You cringe as a violent pain takes over your body.", "It feels like your body is eating itself inside out.", "IT HURTS."))))));
            await Flesheat(M, A);
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/flesh_eating.dm:54
    public virtual async DmTask<object> Flesheat(mob_living M = null, datum_disease_advance A = null)
    {
        float get_damage = rand(15, 25) * Dm.num(power);
        await M.take_overall_damage(brute: get_damage, required_bodytype: BODYTYPE_ORGANIC);
        if (Dm.truthy(pain))
            await M.adjust_stamina_loss(get_damage * 2);
        if (Dm.truthy(bleed))
        {
            if (ishuman(M))
            {
                mob_living_carbon_human H = (mob_living_carbon_human)M;
                obj_item_bodypart random_part = (obj_item_bodypart)pick(H.get_bodyparts() as DmList);
                random_part.adjustBleedStacks(5 * Dm.num(power));
            }
        }
        return 1;
    }
}

/*
//////////////////////////////////////
Autophagocytosis (AKA Programmed mass cell death)

	Very noticable.
	Lowers resistance.
	Fast stage speed.
	Decreases transmittability.
	Fatal Level.

Bonus
	Deals brute damage over time.

//////////////////////////////////////
*/
// tg: code/datums/diseases/advance/symptoms/flesh_eating.dm:83
public partial class datum_symptom_flesh_death : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/flesh_eating.dm:105
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 5)
            suppress_warning = TRUE;
        if (Dm.num(A.totalStageSpeed()) >= 7) //bleeding and hunger
            chems = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/flesh_eating.dm:114
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (A.stage == 2 || A.stage == 3)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel your body break apart.", "Your skin rubs off like dust."))))));
        }
        else if (A.stage == 4 || A.stage == 5)
        {
            if (prob(base_message_chance / 2)) //reduce spam
                to_chat(M, span_userdanger(I18n.T(Dm.str(pick(Dm.list("You feel your muscles weakening.", "Some of your skin detaches itself.", "You feel sandy."))))));
            await Flesh_death(M, A);
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/flesh_eating.dm:128
    public virtual async DmTask<object> Flesh_death(mob_living M = null, datum_disease_advance A = null)
    {
        float get_damage = rand(6, 10);
        await M.take_overall_damage(brute: get_damage, required_bodytype: BODYTYPE_ORGANIC);
        if (Dm.truthy(chems))
            await ((datum_reagents)M.reagents).add_reagent_list(Dm.list(Dm.kv(typeof(datum_reagent_toxin_heparin), 2), Dm.kv(typeof(datum_reagent_toxin_lipolicide), 2)));
        return 1;
    }
}
