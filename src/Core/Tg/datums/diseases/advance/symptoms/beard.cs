// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/beard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/** Facial Hypertrichosis
 * No change to stealth.
 * Increases resistance.
 * Increases speed.
 * Slighlty increases transmissibility
 * Intense Level.
 * Bonus: Makes the mob grow a massive beard, regardless of gender.
*/
// tg: code/datums/diseases/advance/symptoms/beard.dm:10
public partial class datum_symptom_beard : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/beard.dm:24
    [DmParams("disease")]
    public override async DmTask<object> Activate(datum_disease_advance disease = null)
    {
        object dot = null;
        dot = await base.Activate(disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living manly_mob = disease.affected_mob;
        if (ishuman(manly_mob))
        {
            mob_living_carbon_human manly_man = (mob_living_carbon_human)manly_mob;
            float index = min(max(beard_order.Find(manly_man.facial_hairstyle) + 1, disease.stage - 1), beard_order.len);
            if (index > 0 && !Dm.eq(manly_man.facial_hairstyle, beard_order[index]))
            {
                to_chat(manly_man, span_warning(I18n.T("Your chin itches.")));
                manly_man.set_facial_hairstyle(beard_order[index], update: TRUE);
            }
        }
        return dot;
    }
}
