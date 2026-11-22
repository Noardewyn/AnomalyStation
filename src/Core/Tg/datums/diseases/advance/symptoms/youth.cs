// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/youth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/*Eternal Youth
 * Greatly increases stealth
 * Tremendous increase to resistance
 * Tremendous increase to stage speed
 * Tremendous reduction to transmissibility
 * Critical level
 * Bonus: Can be used to buff your virus
*/
// tg: code/datums/diseases/advance/symptoms/youth.dm:10
public partial class datum_symptom_youth : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/youth.dm:24
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (ishuman(M))
        {
            mob_living_carbon_human H = (mob_living_carbon_human)M;
            if (A.stage == 1)
            {
                if (H.age > 41)
                {
                    H.age = 41;
                    to_chat(H, span_notice(I18n.T("You haven't had this much energy in years!")));
                }
            }
            else if (A.stage == 2)
            {
                if (H.age > 36)
                {
                    H.age = 36;
                    to_chat(H, span_notice(I18n.T("You're suddenly in a good mood.")));
                }
            }
            else if (A.stage == 3)
            {
                if (H.age > 31)
                {
                    H.age = 31;
                    to_chat(H, span_notice(I18n.T("You begin to feel more lithe.")));
                }
            }
            else if (A.stage == 4)
            {
                if (H.age > 26)
                {
                    H.age = 26;
                    to_chat(H, span_notice(I18n.T("You feel reinvigorated.")));
                }
            }
            else if (A.stage == 5)
            {
                if (H.age > 21)
                {
                    H.age = 21;
                    to_chat(H, span_notice(I18n.T("You feel like you can take on the world!")));
                }
            }
        }
        return dot;
    }
}
