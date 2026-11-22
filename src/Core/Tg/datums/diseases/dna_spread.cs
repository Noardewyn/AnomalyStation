// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/dna_spread.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/dna_spread.dm:1
public partial class datum_disease_dnaspread : datum_disease
{

    // tg: code/datums/diseases/dna_spread.dm:20
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (affected_mob.dna == null)
        {
            await cure();
            return FALSE;
        }

        //Only species that can be spread by transformation sting can be spread by the retrovirus
        if (HAS_TRAIT(affected_mob, TRAIT_NO_DNA_COPY))
        {
            await cure();
            return FALSE;
        }

        if (!Dm.truthy(strain_data["dna"]))
        {
            //Absorbs the target DNA.
            strain_data["dna"] = Dm.@new(affected_mob.dna.type);
            await affected_mob.dna.copy_dna((datum_dna)strain_data["dna"]);
            carrier = TRUE;
            await update_stage(4);
            return dot;
        }

        if (stage == 2 || stage == 3) //Pretend to be a cold and give time to spread.
        {
            if (SPT_PROB(4, seconds_per_tick))
                await affected_mob.emote("sneeze");
            if (SPT_PROB(4, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("Your muscles ache.")));
                if (prob(20))
                    await affected_mob.take_bodypart_damage(1, updating_health: FALSE);
            }
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("Your stomach hurts.")));
                if (prob(20))
                    await affected_mob.adjust_tox_loss(2, FALSE);
            }
        }
        else if (stage == 4)
        {
            if (!Dm.truthy(transformed) && !Dm.truthy(carrier))
            {
                //Save original dna for when the disease is cured.
                original_dna = Dm.@new(affected_mob.dna.type) as datum_dna;
                await affected_mob.dna.copy_dna(original_dna);

                to_chat(affected_mob, span_danger(I18n.T("You don't feel like yourself..")));
                datum_dna transform_dna = (datum_dna)strain_data["dna"];

                await transform_dna.copy_dna(affected_mob.dna, COPY_DNA_SE | COPY_DNA_SPECIES);
                affected_mob.real_name = affected_mob.dna.real_name;
                await affected_mob.updateappearance(mutcolor_update: 1);
                await affected_mob.domutcheck();

                transformed = 1;
                carrier = 1; //Just chill out at stage 4
            }
        }
        return dot;
    }


    // tg: code/datums/diseases/dna_spread.dm:74
    public override object Destroy(float? force = null)
    {
        if (original_dna != null && Dm.truthy(transformed) && affected_mob != null)
        {
            Waiting.NoSleep(original_dna.copy_dna(affected_mob.dna, COPY_DNA_SE | COPY_DNA_SPECIES));
            affected_mob.real_name = affected_mob.dna.real_name;
            Waiting.NoSleep(affected_mob.updateappearance(mutcolor_update: 1));
            Waiting.NoSleep(affected_mob.domutcheck());

            to_chat(affected_mob, span_notice(I18n.T("You feel more like yourself.")));
        }
        return base.Destroy(force);
    }
}
