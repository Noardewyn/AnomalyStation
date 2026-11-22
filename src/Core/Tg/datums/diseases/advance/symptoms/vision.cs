// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/vision.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/vision.dm:9
public partial class datum_symptom_visionloss : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/vision.dm:32
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 4)
            suppress_warning = TRUE;
        if (Dm.num(A.totalResistance()) >= 12) //goodbye eyes
            remove_eyes = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/vision.dm:41
    [DmParams("source_disease")]
    public override async DmTask<object> Activate(datum_disease_advance source_disease = null)
    {
        object dot = null;
        dot = await base.Activate(source_disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon infected_mob = source_disease.affected_mob;
        obj_item_organ_eyes eyes = infected_mob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;

        if (source_disease.stage == 1 || source_disease.stage == 2)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                to_chat(infected_mob, span_warning(I18n.T("Your eyes itch.")));
        }
        else if (source_disease.stage == 3 || source_disease.stage == 4)
        {
            to_chat(infected_mob, span_boldwarning(I18n.T("Your eyes burn!")));
            await infected_mob.set_eye_blur_if_lower(20 * SECONDS);
            await eyes.apply_organ_damage(1);
        }
        else
        {
            await infected_mob.set_eye_blur_if_lower(40 * SECONDS);
            await eyes.apply_organ_damage(5);

            // Applies nearsighted at minimum
            if (!Dm.truthy(infected_mob.is_nearsighted_from(EYE_DAMAGE)) && eyes.damage <= eyes.low_threshold)
                await eyes.set_organ_damage(eyes.low_threshold);

            if (prob(Dm.num(eyes.damage) - Dm.num(eyes.low_threshold) + 1))
            {
                if (Dm.truthy(remove_eyes))
                {
                    infected_mob.visible_message(
                        span_warning(I18n.T("[]'s eyes fall out of their sockets!", infected_mob)),
                        span_userdanger(I18n.T("Your eyes fall out of their sockets!"))
                    );
                    await eyes.Remove(infected_mob);
                    await eyes.forceMove(get_turf(infected_mob));
                }
                else if (!Dm.truthy(infected_mob.is_blind_from(EYE_DAMAGE)))
                {
                    to_chat(infected_mob, span_userdanger(I18n.T("You go blind!")));
                    await eyes.apply_organ_damage(eyes.maxHealth);
                }
            }
            else
                to_chat(infected_mob, span_userdanger(I18n.T("Your eyes burn horrifically!")));
        }
        return dot;
    }
}
