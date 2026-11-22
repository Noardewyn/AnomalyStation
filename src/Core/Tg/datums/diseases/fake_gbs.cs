// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/fake_gbs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/fake_gbs.dm:1
public partial class datum_disease_fake_gbs : datum_disease
{
    // tg: code/datums/diseases/fake_gbs.dm:14
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("sneeze");
        }
        else if (stage == 3)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("cough");
            else if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("gasp");
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You're starting to feel very weak...")));
        }
        else if (stage == 4)
        {
            if (SPT_PROB(5, seconds_per_tick))
                await affected_mob.emote("cough");
        }
        else if (stage == 5)
        {
            if (SPT_PROB(5, seconds_per_tick))
                await affected_mob.emote("cough");
        }
        return dot;
    }
}
