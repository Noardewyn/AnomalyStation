// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/antagonists/nuclear_operative.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316). The var blocks are generated (D111).

// tg: code/modules/jobs/job_types/antagonists/nuclear_operative.dm:1
public partial class datum_job_nuclear_operative : datum_job
{
    // tg: code/modules/jobs/job_types/antagonists/nuclear_operative.dm:4
    public override DmTask<object> get_roundstart_spawn_point()
    {
        return DmTask<object>.FromResult(pick(GLOB.nukeop_elevator_start));
    }

    // tg: code/modules/jobs/job_types/antagonists/nuclear_operative.dm:7
    public override DmTask<object> get_latejoin_spawn_point()
    {
        return DmTask<object>.FromResult(pick(GLOB.nukeop_base_start));
    }
}

// tg: code/modules/jobs/job_types/antagonists/nuclear_operative.dm:10
public partial class datum_job_nuclear_operative_leader : datum_job_nuclear_operative
{
    // tg: code/modules/jobs/job_types/antagonists/nuclear_operative.dm:12
    public override DmTask<object> get_latejoin_spawn_point()
    {
        return DmTask<object>.FromResult(pick(GLOB.nukeop_base_leader_start));
    }
}

// tg: code/modules/jobs/job_types/antagonists/nuclear_operative.dm:15
public partial class datum_job_nuclear_operative_clown_operative : datum_job_nuclear_operative
{
}

// tg: code/modules/jobs/job_types/antagonists/nuclear_operative.dm:18
public partial class datum_job_nuclear_operative_clown_operative_leader : datum_job_nuclear_operative_clown_operative
{
    // tg: code/modules/jobs/job_types/antagonists/nuclear_operative.dm:20
    public override DmTask<object> get_latejoin_spawn_point()
    {
        return DmTask<object>.FromResult(pick(GLOB.nukeop_base_leader_start));
    }
}
