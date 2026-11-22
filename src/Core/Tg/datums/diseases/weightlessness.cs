// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/weightlessness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). The spacer quirk check reads has_quirk, which is false while
// the quirks wait (D332, D333).

// tg: code/datums/diseases/weightlessness.dm:1
public partial class datum_disease_weightlessness : datum_disease
{


    // tg: code/datums/diseases/weightlessness.dm:18
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 1)
        {
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You almost lose your balance for a second.")));
        }
        else if (stage == 2)
        {
            if (SPT_PROB(3, seconds_per_tick) && !HAS_TRAIT_FROM(affected_mob, TRAIT_MOVE_FLOATING, NO_GRAVITY_TRAIT))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel yourself lift off the ground.")));
                await ((datum_reagents)affected_mob.reagents).add_reagent(typeof(datum_reagent_gravitum), 1);
            }
        }

        else if (stage == 4)
        {
            if (SPT_PROB(3, seconds_per_tick) && !Dm.truthy(affected_mob.has_quirk(typeof(datum_quirk_spacer_born))))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel sick as the world starts moving around you.")));
                await affected_mob.adjust_confusion(3 * SECONDS);
            }
            if (SPT_PROB(8, seconds_per_tick) && !HAS_TRAIT_FROM(affected_mob, TRAIT_MOVE_FLOATING, NO_GRAVITY_TRAIT))
            {
                to_chat(affected_mob, span_danger(I18n.T("You suddenly lift off the ground.")));
                await ((datum_reagents)affected_mob.reagents).add_reagent(typeof(datum_reagent_gravitum), 5);
            }
        }
        return dot;
    }

    // tg: code/datums/diseases/weightlessness.dm:40
    public override async DmTask cure(float? add_resistance = null)
    {
        mob_living_carbon affected_mob__cure = affected_mob; // the parent's qdel clears affected_mob before tg's lines below read it (DV559)
        await base.cure(add_resistance);
        await affected_mob__cure.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: 95, purge_ratio: 0.4f);
        to_chat(affected_mob__cure, span_danger(I18n.T("You fall to the floor as your body stops rejecting gravity.")));
    }
}
