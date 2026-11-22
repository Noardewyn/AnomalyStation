// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/weight.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/*Weight Loss
 * No effect on stealth
 * Increases resistance
 * Reduces stage speed
 * Reduces transmissibility
 * Bonus: Drains nutrition from the host
*/
// tg: code/datums/diseases/advance/symptoms/weight.dm:8
public partial class datum_symptom_weight_loss : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/weight.dm:27
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 4) //warn less often
            base_message_chance = 25;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/weight.dm:34
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (A.stage == 1 || A.stage == 2 || A.stage == 3 || A.stage == 4)
        {
            if (prob(base_message_chance))
                to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel hungry.", "You crave for food."))))));
        }
        else
        {
            to_chat(M, span_warning(Dm.text("<i>[]</i>", I18n.T(Dm.str(pick(Dm.list("So hungry...", "You'd kill someone for a bite of food...", "Hunger cramps seize you...")))))));
            M.overeatduration = max(M.overeatduration - 200 * SECONDS, 0);
            await M.adjust_nutrition(-100);
        }
        return dot;
    }
}
