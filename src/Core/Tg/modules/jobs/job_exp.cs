// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_exp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F: the job eligibility and the preferences): job_exp.dm is the earliest file of tgstation.dme that
// declares something on /datum/job (/datum/job/proc/required_playtime_remaining, job_exp.dm:6), so this part carries
// its [DmPath] (rules 2.3). required_playtime_remaining (the database is never connected, M10+, so it stops there), the
// experience requirement getters, calc_exp_type, get_exp_living and get_exp_format; the database's experience updates are M10+.

// tg: code/modules/jobs/job_exp.dm:6
[DmPath("/datum/job")]
public partial class datum_job : datum
{
    // Procs
    // tg: code/modules/jobs/job_exp.dm:6
    public virtual object required_playtime_remaining(client C = null)
    {
        if (C == null)
            return 0;
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_exp_tracking))))
            return 0;
        // if(!SSdbcore.Connect()) return 0: the database (M10+) is never connected
        return 0;
    }

    // tg: code/modules/jobs/job_exp.dm:28
    public virtual object get_exp_req_amount()
    {
        if (Dm.truthy(exp_required_type_department))
        {
            object uerhh = config.Get(typeof(datum_config_entry_number_use_exp_restrictions_heads_hours));
            if (Dm.truthy(uerhh))
                return Dm.num(uerhh) * 60;
        }
        return exp_requirements;
    }

    // tg: code/modules/jobs/job_exp.dm:36
    public virtual object get_exp_req_type()
    {
        if (Dm.truthy(exp_required_type_department) && Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_exp_restrictions_heads_department))))
            return exp_required_type_department;
        return exp_required_type;
    }
}

// The [DmPath] of /client, a BYOND builtin type, is on its engine part Builtin/client.builtin.cs (rules 2.3).
public partial class client : datum
{
    // tg: code/modules/jobs/job_exp.dm:53
    public virtual object get_exp_living(float? pure_numeric = null)
    {
        pure_numeric ??= FALSE;
        if (prefs.exp == null || !Dm.truthy(prefs.exp[EXP_TYPE_LIVING]))
            return Dm.truthy(pure_numeric) ? 0 : "No data";
        float exp_living = Dm.num(text2num(prefs.exp[EXP_TYPE_LIVING]));
        return Dm.truthy(pure_numeric) ? exp_living : get_exp_format(exp_living);
    }

    // tg: code/modules/jobs/job_exp.dm:44
    public virtual object calc_exp_type(object exptype = null)
    {
        DmList job_list = SSjob.experience_jobs_map[exptype] as DmList;
        if (job_list == null)
            return -1;
        DmList exp_map = prefs.exp.Copy();
        float dot = 0;
        foreach (datum_job job in Dm.each<datum_job>(job_list))
            dot += Dm.num(exp_map[job.title]);
        return dot;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/jobs/job_exp.dm:59
    public static object get_exp_format(object expnum = null)
    {
        if (Dm.gt(expnum, 60))
            return num2text(round(Dm.num(expnum) / 60)) + "h";
        else if (Dm.gt(expnum, 0))
            return num2text(Dm.num(expnum)) + "m";
        else
            return "0h";
    }
}
