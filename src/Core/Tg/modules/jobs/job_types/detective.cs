// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/detective.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The outfit's two procs (gaps J4, D298); the job datum is J2's and generated.

// tg: code/modules/jobs/job_types/detective.dm:50
public partial class datum_outfit_job_detective : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/detective.dm:83
    public override async DmTask pre_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.pre_equip(user, visuals_only);
        if (Dm.num(user.age) < AGE_MINOR)
        {
            mask = typeof(obj_item_cigarette_candy);
            head = typeof(obj_item_clothing_head_fedora_det_hat_minor);
        }
    }

    // tg: code/modules/jobs/job_types/detective.dm:89
    public override async DmTask post_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        await base.post_equip(H, visuals_only);
        obj_item_cigarette cig = H.wear_mask as obj_item_cigarette;
        if (cig != null) //Some species specfic changes can mess this up (plasmamen)
            await cig.light__proc();

        if (Dm.truthy(visuals_only))
            return;
    }
}
