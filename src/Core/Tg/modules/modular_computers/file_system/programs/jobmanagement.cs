// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/jobmanagement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4a, D327): Plexagon HR Core, the job slot manager of the HoP and the captain, with its NtosJobManager window:
// opening and closing a job's slots against the crew's size and the config's cooldown, and the prioritized jobs. The var block is
// generated (D111).

public partial class datum_controller_global_vars : datum_controller
{
    /// The time since the last job opening was created
    // tg: code/modules/modular_computers/file_system/programs/jobmanagement.dm:2  GLOBAL_VAR_INIT(time_last_changed_position, 0)
    public object time_last_changed_position;

    // tg: code/modules/modular_computers/file_system/programs/jobmanagement.dm:2
    public virtual void InitGlobaltime_last_changed_position()
    {
        time_last_changed_position = 0;
        Dm.append(ref gvars_datum_init_order, "time_last_changed_position");
    }
}

// tg: code/modules/modular_computers/file_system/programs/jobmanagement.dm:4
public partial class datum_computer_file_program_job_management : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/jobmanagement.dm:25
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        change_position_cooldown = Dm.num(config.Get(typeof(datum_config_entry_number_id_console_jobslot_delay)));
    }

    // tg: code/modules/modular_computers/file_system/programs/jobmanagement.dm:30
    public virtual bool can_edit_job(datum_job job = null)
    {
        if (job == null)
            return false;
        if (!Dm.truthy(Dm.bitand(job.job_flags, JOB_CREW_MEMBER)))
            return false;
        if (Dm.truthy(Dm.bitand(job.job_flags, JOB_CANNOT_OPEN_SLOTS)))
            return false;
        return true;
    }

    // tg: code/modules/modular_computers/file_system/programs/jobmanagement.dm:40
    public virtual bool can_open_job(datum_job job = null)
    {
        if (job.total_positions <= Dm.num(length(GLOB.player_list)) * (max_relative_positions / 100))
        {
            float delta = world.time / 10 - Dm.num(GLOB.time_last_changed_position);
            if (change_position_cooldown < delta || Dm.num(opened_positions[job.title]) < 0)
                return true;
        }
        return false;
    }

    // tg: code/modules/modular_computers/file_system/programs/jobmanagement.dm:48
    public virtual bool can_close_job(datum_job job = null)
    {
        if (job.total_positions > Dm.num(length(GLOB.player_list)) * (max_relative_positions / 100))
        {
            float delta = world.time / 10 - Dm.num(GLOB.time_last_changed_position);
            if (change_position_cooldown < delta || Dm.num(opened_positions[job.title]) > 0)
                return true;
        }
        return false;
    }

    // tg: code/modules/modular_computers/file_system/programs/jobmanagement.dm:56
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        obj_item_card_id user_id = computer.stored_id;
        if (user_id == null || !Dm.@in(ACCESS_CHANGE_IDS, user_id.access))
            return TRUE;

        switch (action)
        {
            case "PRG_open_job":
            {
                object edit_job_target = @params["target"];
                datum_job j = SSjob.get_job(edit_job_target) as datum_job;
                if (!can_edit_job(j) || !can_open_job(j))
                    return TRUE;
                if (Dm.num(opened_positions[edit_job_target]) >= 0)
                    GLOB.time_last_changed_position = world.time / 10;
                j.total_positions++;
                opened_positions[edit_job_target] = Dm.num(opened_positions[edit_job_target]) + 1;
                log_job_debug(Dm.text("[] opened a [] job position, for a total of [] open job slots.", key_name(usr), j.title, j.total_positions));
                playsound(computer, Dm.resource("sound/machines/terminal/terminal_prompt_confirm.ogg"), 50, FALSE);
                return TRUE;
            }
            case "PRG_close_job":
            {
                object edit_job_target = @params["target"];
                datum_job j = SSjob.get_job(edit_job_target) as datum_job;
                if (!can_edit_job(j) || !can_close_job(j))
                    return TRUE;
                //Allow instant closing without cooldown if a position has been opened before
                if (Dm.num(opened_positions[edit_job_target]) <= 0)
                    GLOB.time_last_changed_position = world.time / 10;
                j.total_positions--;
                opened_positions[edit_job_target] = Dm.num(opened_positions[edit_job_target]) - 1;
                log_job_debug(Dm.text("[] closed a [] job position, leaving [] open job slots.", key_name(usr), j.title, j.total_positions));
                playsound(computer, Dm.resource("sound/machines/terminal/terminal_prompt_confirm.ogg"), 50, FALSE);
                return TRUE;
            }
            case "PRG_priority":
            {
                object priority_target = @params["target"];
                datum_job j = SSjob.get_job(priority_target) as datum_job;
                if (!can_edit_job(j))
                    return TRUE;
                if (j.total_positions <= j.current_positions)
                    return TRUE;
                if (Dm.@in(j, SSjob.prioritized_jobs))
                    SSjob.prioritized_jobs.Remove(j);
                else
                {
                    if (Dm.num(length(SSjob.prioritized_jobs)) < 5)
                        SSjob.prioritized_jobs.Add(j);
                    else
                        await computer.say("Error: CentCom employment protocols restrict prioritising more than 5 jobs.");
                }
                playsound(computer, Dm.resource("sound/machines/terminal/terminal_prompt_confirm.ogg"), 50, FALSE);
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/jobmanagement.dm:106
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        bool authed = false;
        obj_item_card_id user_id = computer.stored_id;
        if (user_id != null && Dm.@in(ACCESS_CHANGE_IDS, user_id.access))
            authed = true;

        data["authed"] = authed ? TRUE : FALSE;

        DmList pos = Dm.list();
        DmList priority = Dm.list();
        foreach (datum_job job in Dm.each<datum_job>(SSjob.joinable_occupations))
        {
            if (!can_edit_job(job))
                continue;
            if (Dm.@in(job, SSjob.prioritized_jobs))
                priority.Add(job.title);

            pos.Add(Dm.list(Dm.list(
                Dm.kv("title", job.title),
                Dm.kv("current", job.current_positions),
                Dm.kv("total", job.total_positions),
                Dm.kv("status_open", authed && can_open_job(job) ? TRUE : FALSE),
                Dm.kv("status_close", authed && can_close_job(job) ? TRUE : FALSE))));
        }
        data["slots"] = pos;
        data["prioritized"] = priority;
        float delta = Dm.round(change_position_cooldown - (world.time / 10 - Dm.num(GLOB.time_last_changed_position)), 1);
        data["cooldown"] = delta < 0 ? 0 : delta;
        return data;
    }
}
