// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/parasitic_infection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/parasitic_infection.dm:1
public partial class datum_disease_parasite : datum_disease
{
    // tg: code/datums/diseases/parasitic_infection.dm:18
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 1)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("cough");
        }
        else if (stage == 2)
        {
            if (SPT_PROB(5, seconds_per_tick))
            {
                if (prob(50))
                    to_chat(affected_mob, span_notice(I18n.T("You feel the weight loss already!")));
                await affected_mob.adjust_nutrition(-3);
            }
        }
        else if (stage == 3)
        {
            if (SPT_PROB(10, seconds_per_tick))
            {
                if (prob(20))
                    to_chat(affected_mob, span_notice(I18n.T("You're... REALLY starting to feel the weight loss.")));
                await affected_mob.adjust_nutrition(-6);
            }
        }
        else if (stage == 4)
        {
            if (SPT_PROB(16, seconds_per_tick))
            {
                if (affected_mob.nutrition >= 100)
                {
                    if (prob(10))
                        to_chat(affected_mob, span_warning(I18n.T("You feel like your body's shedding weight rapidly!")));
                    await affected_mob.adjust_nutrition(-12);
                }
                else
                {
                    to_chat(affected_mob, span_warning(I18n.T("You feel much, MUCH lighter!")));
                    await affected_mob.vomit(VOMIT_CATEGORY_BLOOD, lost_nutrition: 20);
                    // disease code already checks if the liver exists otherwise it is cured
                    obj_item_organ_liver affected_liver = affected_mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
                    await affected_liver.Remove(affected_mob);
                    await affected_liver.forceMove(get_turf(affected_mob));
                    await cure();
                    return FALSE;
                }
            }
        }
        return dot;
    }
}
