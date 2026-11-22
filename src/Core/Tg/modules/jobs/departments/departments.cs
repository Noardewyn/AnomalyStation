// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/departments/departments.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed in wave 3 or later (with its module).
// Only the members that the list, sort and comparator helpers (code/__HELPERS/_lists.dm, cmp.dm, sorts/) name;
// bodies are Dm.NotTranslated until then. M4a P5: add_job (SSjob's setup_occupations).

// tg: code/modules/jobs/departments/departments.dm:2
[DmPath("/datum/job_department")]
public partial class datum_job_department : datum
{
    // tg: code/modules/jobs/departments/departments.dm:13
    public float display_order = 0;

    /// Handles adding jobs to the department and setting up the job bitflags.
    // tg: code/modules/jobs/departments/departments.dm:33  (M4a P5: SSjob's setup_occupations)
    public virtual void add_job(datum_job job = null)
    {
        Dm.append(ref department_jobs, job);
        Dm.combine(ref job.departments_bitflags, department_bitflags);
    }

    /// Handles removing jobs from the department and removing job bitflags.
    // tg: code/modules/jobs/departments/departments.dm:38  (gaps R2: the human AI station trait's remove_ai_job)
    public virtual void remove_job(Type job_type = null)
    {
        foreach (datum_job job_datum in Dm.each_cast<datum_job>(department_jobs))
        {
            if (job_datum.type == job_type)
            {
                Dm.remove(ref department_jobs, job_datum);
                job_datum.departments_bitflags -= department_bitflags;
                job_datum.job_flags = Dm.bitand(job_datum.job_flags, Dm.bitnot(JOB_NEW_PLAYER_JOINABLE));
                job_datum.spawn_positions = 0;
                job_datum.total_positions = 0;
            }
        }
    }
}
