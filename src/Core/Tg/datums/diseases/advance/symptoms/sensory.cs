// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/sensory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): mind restoration and sensory restoration. The var blocks are generated (D111).

// tg: code/datums/diseases/advance/symptoms/sensory.dm:8
public partial class datum_symptom_mind_restoration : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/sensory.dm:26
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalResistance()) >= 6) //heal brain damage
            trauma_heal_mild = TRUE;
        if (Dm.num(A.totalResistance()) >= 9) //heal severe traumas
            trauma_heal_severe = TRUE;
        if (Dm.num(A.totalTransmittable()) >= 8) //purge alcohol
            purge_alcohol = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/sensory.dm:37
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;

        if (A.stage >= 3)
        {
            await M.adjust_dizzy(-4 * SECONDS);
            await M.adjust_drowsiness(-4 * SECONDS);
            // All slurring effects get reduced down a bit
            foreach (datum_status_effect_speech_slurring slur in Dm.each<datum_status_effect_speech_slurring>(M.status_effects))
                slur.remove_duration(1 * SECONDS);
            await M.adjust_confusion(-2 * SECONDS);
            if (Dm.truthy(purge_alcohol))
            {
                await ((datum_reagents)M.reagents).remove_reagent(typeof(datum_reagent_consumable_ethanol), 3, include_subtypes: TRUE);
                await M.adjust_drunk_effect(-5);
            }
        }

        if (A.stage >= 4)
        {
            await M.adjust_drowsiness(-4 * SECONDS);
            if (Dm.truthy(((datum_reagents)M.reagents).has_reagent(typeof(datum_reagent_toxin_mindbreaker))))
                await ((datum_reagents)M.reagents).remove_reagent(typeof(datum_reagent_toxin_mindbreaker), 5);
            if (Dm.truthy(((datum_reagents)M.reagents).has_reagent(typeof(datum_reagent_toxin_histamine))))
                await ((datum_reagents)M.reagents).remove_reagent(typeof(datum_reagent_toxin_histamine), 5);
            await M.adjust_hallucinations(-20 * SECONDS);
        }

        if (A.stage >= 5)
        {
            await M.adjust_organ_loss(ORGAN_SLOT_BRAIN, -3);
            if (Dm.truthy(trauma_heal_mild) && iscarbon(M))
            {
                mob_living_carbon C = (mob_living_carbon)M;
                if (prob(10))
                {
                    if (Dm.truthy(trauma_heal_severe))
                        C.cure_trauma_type(resilience: TRAUMA_RESILIENCE_SURGERY);
                    else
                        C.cure_trauma_type(resilience: TRAUMA_RESILIENCE_BASIC);
                }
            }
        }
        return dot;
    }
}

// tg: code/datums/diseases/advance/symptoms/sensory.dm:77
public partial class datum_symptom_sensory_restoration : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/sensory.dm:89
    public override async DmTask<object> Activate(datum_disease_advance advanced_disease = null)
    {
        object dot = null;
        dot = await base.Activate(advanced_disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon infected_mob = advanced_disease.affected_mob;
        if (advanced_disease.stage == 4 || advanced_disease.stage == 5)
        {
            if (Dm.truthy(advanced_disease.has_required_infectious_organ(infected_mob, ORGAN_SLOT_EARS)))
            {
                obj_item_organ_ears ears = infected_mob.get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;
                if (ears != null)
                    await ears.apply_organ_damage(-4);
                ears?.adjust_temporary_deafness(-8 * SECONDS);
            }

            if (!Dm.truthy(advanced_disease.has_required_infectious_organ(infected_mob, ORGAN_SLOT_EYES)))
                return dot;

            obj_item_organ_eyes eyes = infected_mob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
            await infected_mob.adjust_temp_blindness(-4 * SECONDS);
            await infected_mob.adjust_eye_blur(-4 * SECONDS);
            await eyes.apply_organ_damage(-2);
            if (prob(20))
            {
                if (Dm.truthy(infected_mob.is_blind_from(EYE_DAMAGE)))
                {
                    to_chat(infected_mob, span_warning(I18n.T("Your vision slowly returns...")));
                    await infected_mob.adjust_eye_blur(20 * SECONDS);
                }
                else if (Dm.truthy(infected_mob.is_nearsighted_from(EYE_DAMAGE)))
                {
                    to_chat(infected_mob, span_warning(I18n.T("The blackness in your peripheral vision begins to fade.")));
                    await infected_mob.adjust_eye_blur(5 * SECONDS);
                }
            }
        }
        else
        {
            if (prob(base_message_chance))
                to_chat(infected_mob, span_notice(I18n.T(Dm.str(pick(Dm.list("Your eyes feel great.", "You feel like your eyes can focus more clearly.", "You don't feel the need to blink.", "Your ears feel great.", "Your hearing feels more acute."))))));
        }
        return dot;
    }
}
