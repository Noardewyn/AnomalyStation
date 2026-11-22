// Translated from tgstation (snapshot 1d99bd7f): code/datums/id_trim/jobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P5 (the lead): the job trims' procs (the var blocks are generated). The security officer's, warden's and veteran
// advisor's refresh_trim_access (departmental access by population, M10) are not translated: their base runs.

/**
 * This file contains all the trims associated with station jobs.
 * It also contains special prisoner trims and the miner's spare ID trim.
 */

/// ID Trims for station jobs.
// tg: code/datums/id_trim/jobs.dm:7
[DmPath("/datum/id_trim/job")]
public partial class datum_id_trim_job : datum_id_trim
{
    /// The typepath to the job datum from the id_trim. This is converted to one of the job singletons in New().
    // tg: code/datums/id_trim/jobs.dm:23  var/datum/job/job = /datum/job/unassigned
    // M4a P5: object, not the generator's Type (rules 3.2 V3): New replaces the typepath with SSjob's job singleton, so
    // it holds a typepath or a datum (V7). A hand-written var of a hand-written class: the generator leaves it out.
    public object job = typeof(datum_job_unassigned);

    // tg: code/datums/id_trim/jobs.dm:8  The type's override (rules 10.2), hand-written with the var block.
    public datum_id_trim_job()
    {
        trim_state = "trim_assistant";
    }

    /// Static list. Cache of any mapping config job changes.
    // tg: code/datums/id_trim/jobs.dm:19  var/static/list/job_changes
    public static ref DmList job_changes => ref TgState.Current.datum_id_trim_job__job_changes;

    // tg: code/datums/id_trim/jobs.dm:25
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        if (ispath(job))
        {
            job = SSjob.get_job_type(job);
            if (job == null)
            {
                // Not a valid job. Maybe removed by the map.
                return;
            }
        }

        if (job_changes == null)
            job_changes = SSmapping?.current_map?.job_changes;

        if (!Dm.truthy(length(job_changes)))
        {
            refresh_trim_access();
            return;
        }

        DmList access_changes = job_changes[(job as datum_job).title] as DmList;

        if (!Dm.truthy(length(access_changes)))
        {
            refresh_trim_access();
            return;
        }

        if (islist(access_changes["additional_access"]))
            Dm.combine(ref extra_access, access_changes["additional_access"]);
        if (islist(access_changes["additional_minimal_access"]))
            Dm.combine(ref minimal_access, access_changes["additional_minimal_access"]);
        if (islist(access_changes["additional_wildcard_access"]))
            Dm.combine(ref extra_wildcard_access, access_changes["additional_wildcard_access"]);
        if (islist(access_changes["additional_minimal_wildcard_access"]))
            Dm.combine(ref minimal_wildcard_access, access_changes["additional_minimal_wildcard_access"]);

        refresh_trim_access();
    }

    /**
     * Goes through various non-map config settings and modifies the trim's access based on this.
     *
     * Returns TRUE if the config is loaded, FALSE otherwise.
     */
    // tg: code/datums/id_trim/jobs.dm:61
    public virtual object refresh_trim_access()
    {
        // If there's no config loaded then assume minimal access.
        if (!Dm.truthy(config))
        {
            access = minimal_access.Copy();
            wildcard_access = minimal_wildcard_access.Copy();
            return FALSE;
        }

        // There is a config loaded. Check for the jobs_have_minimal_access flag being set.
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_jobs_have_minimal_access))))
        {
            access = minimal_access.Copy();
            wildcard_access = minimal_wildcard_access.Copy();
        }
        else
        {
            access = Dm.bitor(minimal_access, extra_access) as DmList;
            wildcard_access = Dm.bitor(minimal_wildcard_access, extra_wildcard_access) as DmList;
        }

        // If the config has global maint access set, we always want to add maint access.
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_everyone_has_maint_access))))
            Dm.combine(ref access, Dm.list(ACCESS_MAINT_TUNNELS));

        return TRUE;
    }

    // tg: code/datums/id_trim/jobs.dm:82
    public override object find_job()
    {
        return job;
    }
}

// tg: code/datums/id_trim/jobs.dm:85
[DmPath("/datum/id_trim/job/assistant")]
public partial class datum_id_trim_job_assistant : datum_id_trim_job
{
    // tg: code/datums/id_trim/jobs.dm:101
    public override object refresh_trim_access()
    {
        object dot = base.refresh_trim_access();

        if (!Dm.truthy(dot))
            return dot;

        // Config has assistant maint access set.
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_assistants_have_maint_access))))
            Dm.combine(ref access, Dm.list(ACCESS_MAINT_TUNNELS));
        return dot;
    }
}

// tg: code/datums/id_trim/jobs.dm:263
public partial class datum_id_trim_job_captain : datum_id_trim_job
{
    // tg: code/datums/id_trim/jobs.dm:282
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        Dm.combine(ref extra_access, Dm.add(SSid_access.get_flag_access_list(ACCESS_FLAG_COMMON), SSid_access.get_flag_access_list(ACCESS_FLAG_COMMAND)));
        Dm.combine(ref extra_wildcard_access, Dm.add(SSid_access.get_flag_access_list(ACCESS_FLAG_PRV_COMMAND), SSid_access.get_flag_access_list(ACCESS_FLAG_CAPTAIN)));
        Dm.combine(ref minimal_access, Dm.add(SSid_access.get_flag_access_list(ACCESS_FLAG_COMMON), SSid_access.get_flag_access_list(ACCESS_FLAG_COMMAND)));
        Dm.combine(ref minimal_wildcard_access, Dm.add(SSid_access.get_flag_access_list(ACCESS_FLAG_PRV_COMMAND), SSid_access.get_flag_access_list(ACCESS_FLAG_CAPTAIN)));

        await base.New(__extra);
    }
}

// tg: code/datums/id_trim/jobs.dm:561
public partial class datum_id_trim_job_detective : datum_id_trim_job
{
    // tg: code/datums/id_trim/jobs.dm:591
    public override object refresh_trim_access()
    {
        object dot = base.refresh_trim_access();

        if (!Dm.truthy(dot))
            return dot;

        // Config check for if sec has maint access.
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_security_has_maint_access))))
            Dm.combine(ref access, Dm.list(ACCESS_MAINT_TUNNELS));
        return dot;
    }
}

// tg: code/datums/id_trim/jobs.dm:685
public partial class datum_id_trim_job_head_of_security : datum_id_trim_job
{
    // tg: code/datums/id_trim/jobs.dm:739
    public override object refresh_trim_access()
    {
        object dot = base.refresh_trim_access();

        if (!Dm.truthy(dot))
            return dot;

        // Config check for if sec has maint access.
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_security_has_maint_access))))
            Dm.combine(ref access, Dm.list(ACCESS_MAINT_TUNNELS));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/id_trim/jobs.dm:19  /datum/id_trim/job/var/static/list/job_changes
    internal DmList datum_id_trim_job__job_changes;
}

// tg: code/datums/id_trim/jobs.dm:1108
public partial class datum_id_trim_job_security_officer : datum_id_trim_job
{
    // tg: code/datums/id_trim/jobs.dm:1174  (gaps P4c)
    public override Type get_random_bounty_type(object input_bounty_type = null)
    {
        if (!Dm.eq(input_bounty_type, CIV_JOB_SEC) || prob(33) || patrol_type == null)
            return base.get_random_bounty_type(input_bounty_type);

        return patrol_type as Type;
    }

    // tg: code/datums/id_trim/jobs.dm:1142
    public override object refresh_trim_access()
    {
        object dot = base.refresh_trim_access();

        if (!Dm.truthy(dot))
            return dot;

        Dm.combine(ref access, department_access);

        // Config check for if sec has maint access.
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_security_has_maint_access))))
            Dm.combine(ref access, Dm.list(ACCESS_MAINT_TUNNELS));

        // Scaling access (POPULATION_SCALED_ACCESS) is a system directly tied into calculations derived via a config entered variable, as well as the amount of players in the shift.
        // Thus, it makes it possible to judge if departmental security officers should have more access to their department on a lower population shift.
        // Server operators can modify config to change it such that security officers can use this system, or alternatively either: A) always give the "elevated" access (ALWAYS_GETS_ACCESS) or B) never give this access (null value).

        const int POPULATION_SCALED_ACCESS = 1;
        const int ALWAYS_GETS_ACCESS = 2;

        // If null, then the departmental security officer will not get any elevated access.
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_number_depsec_access_level))))
            return dot;

        if (Dm.eq(config.Get(typeof(datum_config_entry_number_depsec_access_level)), POPULATION_SCALED_ACCESS))
        {
            int minimal_security_officers = 3; // We do not spawn in any more lockers if there are 5 or less security officers, so let's keep it lower than that number.
            datum_job J = SSjob.get_job(JOB_SECURITY_OFFICER) as datum_job;
            if ((J.spawn_positions - minimal_security_officers) <= 0)
                Dm.combine(ref access, elevated_access);
        }

        if (Dm.eq(config.Get(typeof(datum_config_entry_number_depsec_access_level)), ALWAYS_GETS_ACCESS))
            Dm.combine(ref access, elevated_access);
        return dot;
    }
}
