// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/cook.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the outfit's chef and cook split. award_service is a live-partial row (the medals).

// tg: code/modules/jobs/job_types/cook.dm:68
public partial class datum_outfit_job_cook : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/cook.dm:85
    public override async DmTask pre_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.pre_equip(user, visuals_only);
        datum_job_cook other_chefs = SSjob.get_job_type(jobtype) as datum_job_cook;
        if (other_chefs != null) // If there's other Chefs, you're a Cook
        {
            if (other_chefs.cooks > 0) //Cooks
            {
                id_trim = typeof(datum_id_trim_job_cook);
                suit = typeof(obj_item_clothing_suit_apron_chef);
                head = typeof(obj_item_clothing_head_soft_mime);
            }
            if (!Dm.truthy(visuals_only))
                other_chefs.cooks++;
        }
    }

    // tg: code/modules/jobs/job_types/cook.dm:96
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.post_equip(user, visuals_only);
        // Update PDA to match possible new trim.
        obj_item_card_id worn_id = user.wear_id as obj_item_card_id;
        obj_item_modular_computer_pda pda = user.get_item_by_slot(pda_slot) as obj_item_modular_computer_pda;
        if (worn_id == null || pda == null)
            return;
        object assignment = worn_id.get_trim_assignment();
        if (assignment != null)
            pda.imprint_id(user.real_name, assignment);
    }

    // tg: code/modules/jobs/job_types/cook.dm:107
    public override object get_types_to_preload()
    {
        DmList dot = base.get_types_to_preload() as DmList;
        Dm.append(ref dot, typeof(obj_item_clothing_suit_apron_chef));
        Dm.append(ref dot, typeof(obj_item_clothing_head_soft_mime));
        return dot;
    }
}
