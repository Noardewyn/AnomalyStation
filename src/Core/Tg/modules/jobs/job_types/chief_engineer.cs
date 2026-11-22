// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/chief_engineer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G2 (D274): the job's procs; gaps package G4: the message server key's memory.

// tg: code/modules/jobs/job_types/chief_engineer.dm:1
public partial class datum_job_chief_engineer : datum_job
{
    // tg: code/modules/jobs/job_types/chief_engineer.dm:54
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        spawned.add_mob_memory(typeof(datum_memory_key_message_server_key), Dm.named("decrypt_key", GLOB.preset_station_message_server_key));
    }

    // tg: code/modules/jobs/job_types/chief_engineer.dm:58
    public override object get_captaincy_announcement(mob_living captain = null)
    {
        return Dm.text("Due to staffing shortages, newly promoted Acting Captain [] on deck!", captain.real_name);
    }
}
