// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/gbs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var blocks are generated (D111).

// tg: code/datums/diseases/gbs.dm:1
public partial class datum_disease_gbs : datum_disease
{
    // tg: code/datums/diseases/gbs.dm:17
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("cough");
        }
        else if (stage == 3)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("gasp");
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your body hurts all over!")));
        }
        else if (stage == 4)
        {
            to_chat(affected_mob, span_userdanger(I18n.T("Your body feels as if it's trying to rip itself apart!")));
            if (SPT_PROB(30, seconds_per_tick))
            {
                affected_mob.investigate_log("has been gibbed by GBS.", INVESTIGATE_DEATHS);
                await affected_mob.gib(DROP_ALL_REMAINS);
                return FALSE;
            }
        }
        return dot;
    }
}
