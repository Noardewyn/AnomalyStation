// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/warden.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the job's after_spawn.

// tg: code/modules/jobs/job_types/warden.dm:1
public partial class datum_job_warden : datum_job
{
    // tg: code/modules/jobs/job_types/warden.dm:46
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        if (!prob(PIG_COP_PROBABILITY))
            return;
        foreach (obj_item_bodypart ham in Dm.each<obj_item_bodypart>(((mob_living_carbon)spawned).get_bodyparts()))
            ham.butcher_drops_override = Dm.list(Dm.kv(typeof(obj_item_food_meat_slab_pig), ham.base_meat_amount));
    }
}
