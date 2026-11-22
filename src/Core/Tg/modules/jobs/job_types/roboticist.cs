// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/roboticist.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4a P5, the lead): New, which SSjob's setup_occupations runs.

// tg: code/modules/jobs/job_types/roboticist.dm:1
public partial class datum_job_roboticist : datum_job
{
    // tg: code/modules/jobs/job_types/roboticist.dm:38
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.append(ref family_heirlooms, subtypesof(typeof(obj_item_toy_mecha)));
    }
}
