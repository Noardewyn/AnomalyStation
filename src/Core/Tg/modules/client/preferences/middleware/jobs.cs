// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/middleware/jobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (the lobby's occupations page): the middleware datum itself is not translated (as random.cs and the names' middleware in
// preferences.cs); its procs are the preferences' own, prefixed jobs_, and preferences.cs calls them where tg loops over its middleware.

public partial class datum_preferences : datum
{
    // tg: code/modules/client/preferences/middleware/jobs.dm:7
    public virtual object jobs_set_job_preference(DmList @params = null, mob user = null)
    {
        object job_title = @params["job"];
        object level = @params["level"];

        if (level != null && !Dm.eq(level, JP_LOW) && !Dm.eq(level, JP_MEDIUM) && !Dm.eq(level, JP_HIGH))
            return FALSE;

        datum_job job = SSjob.get_job(job_title) as datum_job;

        if (job == null)
            return FALSE;

        if (!Dm.eq(job.faction, FACTION_STATION))
            return FALSE;

        if (!Dm.truthy(set_job_preference_level(job, level)))
            return FALSE;

        if (character_preview_view != null)
            Waiting.NoSleep(character_preview_view.update_body());

        return TRUE;
    }

    // tg: code/modules/client/preferences/middleware/jobs.dm:30
    public virtual object jobs_set_job_to_profile(DmList @params = null, mob user = null)
    {
        object job_title = @params["job"];
        object profile_slot = @params["profile"];

        if (!Dm.isnum(profile_slot) || Dm.eq(profile_slot, -1))
        {
            LAZYREMOVE(ref job_assigned_profiles, job_title);
            return TRUE;
        }

        LAZYSET(ref job_assigned_profiles, job_title, profile_slot);
        return TRUE;
    }

    // tg: code/modules/client/preferences/middleware/jobs.dm:41
    public virtual DmList jobs_get_constant_data()
    {
        DmList data = Dm.list();

        DmList departments = Dm.list();
        DmList jobs = Dm.list();
        DmList jobs_sorted = Dm.list();

        foreach (datum_job job in Dm.each<datum_job>(SSjob.joinable_occupations))
        {
            if (Dm.truthy(Dm.bitand(job.job_flags, JOB_LATEJOIN_ONLY)))
                continue;
            Type department_type = job.department_for_prefs ?? (job.departments_list != null && job.departments_list.Count > 0 ? job.departments_list[1] as Type : null);
            if (department_type == null)
            {
                stack_trace(Dm.text("[] does not have a department set, yet is a joinable occupation!", job), "code/modules/client/preferences/middleware/jobs.dm", 52);
                continue;
            }

            if (job.description == null)
            {
                stack_trace(Dm.text("[] does not have a description set, yet is a joinable occupation!", job), "code/modules/client/preferences/middleware/jobs.dm", 56);
                continue;
            }

            object department_name = Dm.initial(department_type, (datum_job_department p) => p.department_name);
            if (departments[department_name] == null)
            {
                Type department_head_type = Dm.initial(department_type, (datum_job_department p) => p.department_head);

                departments[department_name] = Dm.list(
                    Dm.kv("head", department_head_type != null ? Dm.initial(department_head_type, (datum_job p) => p.title) : null),
                    Dm.kv("color", Dm.initial(department_type, (datum_job_department p) => p.ui_color))); // Prob shouldnt be here.
            }

            // Use the built in sorting of the main occupation list to keep it sorted how we want instead of asc
            jobs_sorted.Add(job.title);

            jobs[job.title] = Dm.list(
                Dm.kv("description", job.description),
                Dm.kv("department", department_name));
        }

        data["departments"] = departments;
        data["jobs"] = jobs;
        data["jobs_sorted"] = jobs_sorted;

        return data;
    }

    // tg: code/modules/client/preferences/middleware/jobs.dm:84
    public virtual DmList jobs_get_ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList job_preferences_data = Dm.list();
        data["job_preferences"] = job_preferences_data;
        foreach (object job in Dm.each(job_preferences))
        {
            job_preferences_data.Add(Dm.list(Dm.list(
                Dm.kv("job", job),
                Dm.kv("priority", job_preferences[job]),
                Dm.kv("assigned_profile_slot", LAZYACCESS(job_assigned_profiles, job)))));
        }

        DmList unprioritized = Dm.sub(SANITIZE_LIST(job_assigned_profiles), SANITIZE_LIST(job_preferences)) as DmList;
        foreach (object job in Dm.each(unprioritized))
        {
            job_preferences_data.Add(Dm.list(Dm.list(
                Dm.kv("job", job),
                Dm.kv("priority", null),
                Dm.kv("assigned_profile_slot", unprioritized[job]))));
        }

        return data;
    }

    // tg: code/modules/client/preferences/middleware/jobs.dm:103
    public virtual DmList jobs_get_ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList required_job_playtime = jobs_get_required_job_playtime(user);
        if (required_job_playtime != null)
            data.Add(required_job_playtime);

        DmList job_bans = jobs_get_job_bans(user);
        if (job_bans.Count > 0)
            data["job_bans"] = job_bans;

        return data.Count > 0 ? data : null;
    }

    // tg: code/modules/client/preferences/middleware/jobs.dm:116
    public virtual DmList jobs_get_required_job_playtime(mob user = null)
    {
        DmList data = Dm.list();

        DmList job_days_left = Dm.list();
        DmList job_required_experience = Dm.list();

        foreach (datum_job job in Dm.each<datum_job>(SSjob.all_occupations))
        {
            if (Dm.truthy(Dm.bitand(job.job_flags, JOB_LATEJOIN_ONLY)))
                continue;
            object required_playtime_remaining = job.required_playtime_remaining(user.client);
            if (Dm.truthy(required_playtime_remaining))
            {
                job_required_experience[job.title] = Dm.list(
                    Dm.kv("experience_type", job.get_exp_req_type()),
                    Dm.kv("required_playtime", required_playtime_remaining));

                continue;
            }

            if (!Dm.truthy(job.player_old_enough(user.client)))
                job_days_left[job.title] = job.available_in_days(user.client);
        }

        if (job_days_left.Count > 0)
            data["job_days_left"] = job_days_left;

        // if (job_required_experience): a list is always true
        data["job_required_experience"] = job_required_experience;

        return data;
    }

    // tg: code/modules/client/preferences/middleware/jobs.dm:145
    public virtual DmList jobs_get_job_bans(mob user = null)
    {
        DmList data = Dm.list();

        foreach (datum_job job in Dm.each<datum_job>(SSjob.all_occupations))
        {
            if (Dm.truthy(is_banned_from(user.client?.ckey, job.title)))
                data.Add(job.title);
        }

        return data;
    }
}
