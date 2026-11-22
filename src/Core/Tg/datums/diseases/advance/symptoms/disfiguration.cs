// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/disfiguration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/**Disfiguration
 * Increases stealth
 * No change to resistance
 * Increases stage speed
 * Slightly increases transmissibility
 * Critical level
 * Bonus: Adds disfiguration trait making the mob appear as "Unknown" to others.
 */
// tg: code/datums/diseases/advance/symptoms/disfiguration.dm:9
public partial class datum_symptom_disfiguration : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/disfiguration.dm:22
    [DmParams("disease")]
    public override async DmTask<object> Activate(datum_disease_advance disease = null)
    {
        object dot = null;
        dot = await base.Activate(disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon victim = disease.affected_mob;
        obj_item_bodypart head = victim.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head == null || HAS_TRAIT_FROM(head, TRAIT_DISFIGURED, DISEASE_TRAIT))
            return dot;
        if (disease.stage == 5)
        {
            ADD_TRAIT(head, TRAIT_DISFIGURED, DISEASE_TRAIT);
            victim.visible_message(span_warning(I18n.T("[]'s face appears to cave in!", victim)), span_notice(I18n.T("You feel your face crumple and cave in!")));
        }
        else
            victim.visible_message(span_warning(I18n.T("[]'s face begins to contort...", victim)), span_notice(I18n.T("Your face feels wet and malleable...")));
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/disfiguration.dm:39
    [DmParams("disease")]
    public override async DmTask<object> End(datum_disease_advance disease = null)
    {
        object dot = null;
        dot = await base.End(disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon victim = disease.affected_mob;
        obj_item_bodypart head = victim?.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head != null)
            REMOVE_TRAIT(head, TRAIT_DISFIGURED, DISEASE_TRAIT);
        return dot;
    }
}
