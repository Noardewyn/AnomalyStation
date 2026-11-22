// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/magnitis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/magnitis.dm:1
public partial class datum_disease_magnitis : datum_disease
{

    // tg: code/datums/diseases/magnitis.dm:19
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your skin tingles with energy.")));
            if (SPT_PROB(1, seconds_per_tick))
            {
                foreach (obj nearby_object in Dm.each<obj>(orange(2, affected_mob)))
                {
                    if (Dm.truthy(nearby_object.anchored) || !Dm.truthy(Dm.bitand(nearby_object.obj_flags, CONDUCTS_ELECTRICITY)))
                        continue;
                    float move_dir = get_dir(nearby_object, affected_mob);
                    await nearby_object.Move(get_step(nearby_object, move_dir), move_dir);
                }
                foreach (mob_living_silicon nearby_silicon in Dm.each<mob_living_silicon>(orange(2, affected_mob)))
                {
                    if (isAI(nearby_silicon))
                        continue;
                    float move_dir = get_dir(nearby_silicon, affected_mob);
                    await nearby_silicon.Move(get_step(nearby_silicon, move_dir), move_dir);
                }
            }
        }
        else if (stage == 3)
        {
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your hair stands on end.")));
            if (SPT_PROB(2, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a light shock course through your body.")));
                foreach (obj nearby_object in Dm.each<obj>(orange(4, affected_mob)))
                {
                    if (Dm.truthy(nearby_object.anchored) || !Dm.truthy(Dm.bitand(nearby_object.obj_flags, CONDUCTS_ELECTRICITY)))
                        continue;
                    for (float i = 1, i__end = rand(1, 2); i <= i__end; i++)
                        await nearby_object.throw_at(affected_mob, 4, 3);
                }
                foreach (mob_living_silicon nearby_silicon in Dm.each<mob_living_silicon>(orange(4, affected_mob)))
                {
                    if (isAI(nearby_silicon))
                        continue;
                    for (float i = 1, i__end = rand(1, 2); i <= i__end; i++)
                        await nearby_silicon.throw_at(affected_mob, 4, 3);
                }
            }
        }
        else if (stage == 4)
        {
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You query upon the nature of miracles.")));
            if (SPT_PROB(4, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a powerful shock course through your body.")));
                foreach (obj nearby_object in Dm.each<obj>(orange(6, affected_mob)))
                {
                    if (Dm.truthy(nearby_object.anchored) || !Dm.truthy(Dm.bitand(nearby_object.obj_flags, CONDUCTS_ELECTRICITY)))
                        continue;
                    for (float i = 1, i__end = rand(1, 3); i <= i__end; i++)
                        await nearby_object.throw_at(affected_mob, 6, 5); // I really wanted to use addtimers to stagger out when everything gets thrown but it would probably cause a lot of lag.
                }
                foreach (mob_living_silicon nearby_silicon in Dm.each<mob_living_silicon>(orange(6, affected_mob)))
                {
                    if (isAI(nearby_silicon))
                        continue;
                    for (float i = 1, i__end = rand(1, 3); i <= i__end; i++)
                        await nearby_silicon.throw_at(affected_mob, 6, 5);
                }
            }
        }
        return dot;
    }
}
