// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/new_player/latejoin_menu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice F: the whole file but the polls (viewpoll, votepollref: the database) and the fallback menu's BYOND input() (the
// port's client has no input() list; a Ctrl+click on Join opens the tgui menu as a plain click does).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:5  GLOBAL_DATUM_INIT(latejoin_menu, /datum/latejoin_menu, new)
    public datum_latejoin_menu latejoin_menu;

    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:5  (GLOBAL_MANAGED(latejoin_menu, new), __DEFINES/__globals.dm:5)
    public virtual void InitGloballatejoin_menu()
    {
        latejoin_menu = Dm.@new<datum_latejoin_menu>();
        Dm.append(ref gvars_datum_init_order, "latejoin_menu");
    }
}

// tg: code/modules/mob/dead/new_player/latejoin_menu.dm:8
[DmPath("/datum/latejoin_menu")]
public partial class datum_latejoin_menu : datum
{
    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:1  #define JOB_CHOICE_YES "Yes" (file-local, #undef at :220)
    private const string JOB_CHOICE_YES = "Yes";
    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:2  #define JOB_CHOICE_REROLL "Reroll" (file-local, #undef at :221)
    private const string JOB_CHOICE_REROLL = "Reroll";
    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:3  #define JOB_CHOICE_CANCEL "Cancel" (file-local, #undef at :222)
    private const string JOB_CHOICE_CANCEL = "Cancel";

    /// Makes a list of jobs and pushes them to a DM list selector. Just in case someone did a special kind of fucky-wucky with TGUI.
    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:8
    public virtual async DmTask fallback_ui(mob_dead_new_player user = null)
    {
        // input(user, "Pick a job to join as:", "Latejoin Job Selection") as null|anything in jobs: BYOND's input() list has no
        // counterpart in the port's client; the tgui menu opens instead.
        await ui_interact(user);
    }

    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:21
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        if (user is mob_dead_new_player new_player)
            new_player.jobs_menu_mounted = TRUE; // Don't flood a user's chat if they open and close the UI.
    }

    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:26
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            // In case they reopen the GUI
            // FIXME: this can cause a runtime since user can be a living mob
            if (user is mob_dead_new_player new_player)
            {
                new_player.jobs_menu_mounted = FALSE;
                _addtimer(Dm.@new<datum_callback>(this, nameof(scream_at_player), user), 5 * SECONDS, file: "code/modules/mob/dead/new_player/latejoin_menu.dm", line: 33);
            }

            ui = Dm.@new<datum_tgui>(user, this, "JobSelection", "Latejoin Menu");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:38
    public virtual void scream_at_player(mob_dead_new_player player = null)
    {
        if (!Dm.truthy(player.jobs_menu_mounted))
            to_chat(player, span_notice("If the late join menu isn't showing, hold CTRL while clicking the join button!"));
    }

    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:42
    public override async DmTask<object> ui_data(mob user = null)
    {
        mob_dead_new_player owner = user as mob_dead_new_player;
        DmList departments = Dm.list();
        DmList data = Dm.list(
            // GAP(lead): SSlag_switch (controllers/subsystem/lag_switch.dm) is not declared; its measures are off by default
            Dm.kv("disable_jobs_for_non_observers", FALSE),
            Dm.kv("round_duration", DisplayTimeText(Dm.world.time - SSticker.round_start_time, round_seconds_to: 1)),
            Dm.kv("departments", departments));
        if (SSshuttle.emergency != null)
        {
            string shuttle_mode = SSshuttle.emergency.mode;
            if (Dm.eq(shuttle_mode, SHUTTLE_ESCAPE))
                data["shuttle_status"] = "The station has been evacuated.";
            else if (Dm.eq(shuttle_mode, SHUTTLE_CALL) || Dm.eq(shuttle_mode, SHUTTLE_DOCKED) || Dm.eq(shuttle_mode, SHUTTLE_IGNITING))
                data["shuttle_status"] = "The station is currently undergoing evacuation procedures.";
        }

        foreach (datum_job prioritized_job in Dm.each<datum_job>(SSjob.prioritized_jobs.Copy()))
        {
            if (prioritized_job.current_positions >= prioritized_job.total_positions)
                Dm.remove(ref SSjob.prioritized_jobs, prioritized_job);
        }

        foreach (datum_job_department department in Dm.each<datum_job_department>(SSjob.joinable_departments))
        {
            DmList department_jobs = Dm.list();
            DmList department_data = Dm.list(
                Dm.kv("jobs", department_jobs),
                Dm.kv("open_slots", 0));
            departments[department.department_name] = department_data;

            foreach (datum_job job_datum in Dm.each<datum_job>(department.department_jobs))
            {
                //Jobs under multiple departments should only be displayed if this is their first department or the command department
                if (LAZYLEN(job_datum.departments_list) > 1 && !Dm.eq(job_datum.departments_list[1], department.type) && !Dm.truthy(Dm.bitand(job_datum.departments_bitflags, DEPARTMENT_BITFLAG_COMMAND)))
                    continue;

                object job_availability = owner.IsJobUnavailable(job_datum.title, latejoin: TRUE);

                DmList job_data = Dm.list(
                    Dm.kv("prioritized", Dm.@in(job_datum, SSjob.prioritized_jobs) ? TRUE : FALSE),
                    Dm.kv("used_slots", job_datum.current_positions),
                    Dm.kv("open_slots", job_datum.total_positions < 0 ? "∞" : job_datum.total_positions),
                    Dm.kv("jobIcon", job_datum.tgui_icon));

                if (!Dm.eq(job_availability, JOB_AVAILABLE))
                {
                    if (Dm.truthy(Dm.bitand(job_datum.job_flags, JOB_HIDE_WHEN_EMPTY)))
                        continue;
                    job_data["unavailable_reason"] = get_job_unavailable_error_message(job_availability, job_datum.title);
                }

                if (job_datum.total_positions < 0)
                    department_data["open_slots"] = "∞";

                if (!Dm.eq(department_data["open_slots"], "∞"))
                {
                    if (job_datum.total_positions - job_datum.current_positions > 0)
                        department_data["open_slots"] = Dm.num(department_data["open_slots"]) + (job_datum.total_positions - job_datum.current_positions);
                }

                department_jobs[job_datum.title] = job_data;
            }
        }

        return data;
    }

    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:99
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList departments = Dm.list();
        mob_dead_new_player owner = user as mob_dead_new_player;

        foreach (datum_job_department department in Dm.each<datum_job_department>(SSjob.joinable_departments))
        {
            DmList department_jobs = Dm.list();
            DmList department_data = Dm.list(
                Dm.kv("jobs", department_jobs),
                Dm.kv("color", department.ui_color));
            departments[department.department_name] = department_data;

            foreach (datum_job job_datum in Dm.each<datum_job>(department.department_jobs))
            {
                //Jobs under multiple departments should only be displayed if this is their first department or the command department
                if (LAZYLEN(job_datum.departments_list) > 1 && !Dm.eq(job_datum.departments_list[1], department.type) && !Dm.truthy(Dm.bitand(job_datum.departments_bitflags, DEPARTMENT_BITFLAG_COMMAND)))
                    continue;
                if (Dm.truthy(Dm.bitand(job_datum.job_flags, JOB_HIDE_WHEN_EMPTY)) && !Dm.eq(owner.IsJobUnavailable(job_datum.title, latejoin: TRUE), JOB_AVAILABLE))
                    continue;

                DmList job_data = Dm.list(
                    Dm.kv("command", Dm.truthy(Dm.bitand(job_datum.departments_bitflags, DEPARTMENT_BITFLAG_COMMAND)) ? TRUE : FALSE),
                    Dm.kv("description", job_datum.description));

                department_jobs[job_datum.title] = job_data;
            }
        }

        return Dm.list(Dm.kv("departments_static", departments));
    }

    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:127
    public override object ui_state(mob user = null)
    {
        return GLOB.new_player_state;
    }

    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:130
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);

        if (ui.user.client == null || Dm.truthy(ui.user.client.interviewee) || !isnewplayer(ui.user))
            return TRUE;

        mob_dead_new_player owner = ui.user as mob_dead_new_player;

        if (Dm.eq(action, "ui_mounted_with_no_bluescreen"))
            owner.jobs_menu_mounted = TRUE;
        else if (Dm.eq(action, "select_job"))
        {
            if (Dm.eq(@params["job"], "Random"))
            {
                object job = await get_random_job(owner);
                if (!Dm.truthy(job))
                    return TRUE;

                @params["job"] = job;
            }

            if (!Dm.truthy(SSticker?.IsRoundInProgress()))
            {
                await tgui_alert(owner, "The round is either not ready, or has already finished...", "Oh No!");
                return TRUE;
            }

            // if(SSlag_switch.measures[DISABLE_NON_OBSJOBS]): GAP(lead), SSlag_switch is not declared; its measures are off by default

            //Determines Relevent Population Cap
            object relevant_cap;
            object hard_popcap = config.Get(typeof(datum_config_entry_number_hard_popcap));
            object extreme_popcap = config.Get(typeof(datum_config_entry_number_extreme_popcap));
            if (Dm.truthy(hard_popcap) && Dm.truthy(extreme_popcap))
                relevant_cap = Dm.min(Dm.num(hard_popcap), Dm.num(extreme_popcap));
            else
                relevant_cap = Dm.max(Dm.num(hard_popcap), Dm.num(extreme_popcap));

            if (Dm.truthy(SSticker.queued_players.len) && !Dm.@in(ckey(owner.key), GLOB.admin_datums))
            {
                if (Dm.ge(living_player_count(), relevant_cap) || owner != SSticker.queued_players[1])
                {
                    await tgui_alert(owner, "The server is full!", "Oh No!");
                    return TRUE;
                }
            }

            // SAFETY: AttemptLateSpawn has it's own sanity checks. This is perfectly safe.
            await owner.AttemptLateSpawn(@params["job"]);
        }
        else if (Dm.eq(action, "viewpoll") || Dm.eq(action, "votepollref"))
        {
            Dm.NotTranslated("code/modules/mob/dead/new_player/latejoin_menu.dm:174", "ui_act: the polls (the database's poll questions, admin tooling)");
            return TRUE;
        }
        return dot;
    }

    /// Gives the user a random job that they can join as, and prompts them if they'd actually like to keep it, rerolling if not. Cancellable by the user.
    /// WARNING: BLOCKS THREAD!
    // tg: code/modules/mob/dead/new_player/latejoin_menu.dm:191
    public virtual async DmTask<object> get_random_job(mob_dead_new_player owner = null)
    {
        DmList dept_data = Dm.list();

        foreach (datum_job_department department in Dm.each<datum_job_department>(SSjob.joinable_departments))
        {
            foreach (datum_job job_datum in Dm.each<datum_job>(department.department_jobs))
            {
                if (!Dm.eq(owner.IsJobUnavailable(job_datum.title, latejoin: TRUE), JOB_AVAILABLE))
                    continue;
                Dm.append(ref dept_data, job_datum.title);
            }
        }

        if (dept_data.len <= 0) //Congratufuckinglations
        {
            await tgui_alert(owner, "There are literally no random jobs available for you on this server, ahelp for assistance.", "Oh No!");
            return null;
        }

        object random_job = null;

        while (!Dm.eq(random_job, JOB_CHOICE_YES))
        {
            if (dept_data.len <= 0)
            {
                await tgui_alert(owner, "It seems that there are no more random jobs available for you!", "Oh No!");
                return null;
            }

            object random = pick_n_take(dept_data);
            DmList random_job_options = Dm.list(JOB_CHOICE_YES, JOB_CHOICE_REROLL, JOB_CHOICE_CANCEL);

            random_job = await tgui_alert(owner, Dm.text("[]?", random), "Random Job", random_job_options);

            if (Dm.eq(random_job, JOB_CHOICE_CANCEL))
                return null;
            if (Dm.eq(random_job, JOB_CHOICE_YES))
                return random;
        }
        return null;
    }
}
