// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/chief_medical_officer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G2 (D274): the job's procs.

// tg: code/modules/jobs/job_types/chief_medical_officer.dm:1
public partial class datum_job_chief_medical_officer : datum_job
{
    // tg: code/modules/jobs/job_types/chief_medical_officer.dm:52
    public override object get_captaincy_announcement(mob_living captain = null)
    {
        return Dm.text("Due to staffing shortages, newly promoted Acting Captain [] on deck!", captain.real_name);
    }
}
