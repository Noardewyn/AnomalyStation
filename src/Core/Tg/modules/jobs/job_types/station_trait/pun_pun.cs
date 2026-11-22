// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/station_trait/pun_pun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file: Pun Pun's job, which the Pun Pun is a Crewmember station trait opens (and monkey day, with the
// holidays): the player who signs up in the lobby spawns as Pun Pun, the bar's monkey, dressed as a crewman. The job's and the
// outfit's var blocks are generated (D111).

///Special job, active during monkey day.
// tg: code/modules/jobs/job_types/station_trait/pun_pun.dm:2
public partial class datum_job_pun_pun : datum_job
{
    // tg: code/modules/jobs/job_types/station_trait/pun_pun.dm:30
    public override async DmTask<object> get_spawn_mob(client player_client = null, atom spawn_point = null)
    {
        if (player_client == null)
            return null;
        var monky = Dm.@new(spawn_type, get_turf(spawn_point)) as mob_living;
        if (GLOB.the_one_and_only_punpun == null)
            GLOB.the_one_and_only_punpun = monky;
        return monky;
    }

    // tg: code/modules/jobs/job_types/station_trait/pun_pun.dm:38
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        var monkey = (mob_living_carbon_human)spawned;
        await monkey.crewlike_monkify();
    }
}
