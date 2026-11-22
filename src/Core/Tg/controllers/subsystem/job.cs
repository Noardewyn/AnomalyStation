// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/job.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4a P5, the lead): tg's subsystem with its M4a procs (docs/ai/specs/m4a-ownership.md, P5's list): Initialize's
// job setup (the job datums with their config and map checks, the job lists, the overflow role), the job and department
// getters. M10 slice F: the round-start and late-join assignment (assign_role, divide_occupations and its helpers, equip_rank, the
// late join's spawn points, JoinPlayerHere). M10 slice G2: promote_to_captain (DV191). Syndicate A3: try_reject_player keeps Dynamic's selected minds. Not translated:
// the heads' lists but get_all_heads (syndicate A2: the head of personnel's traitor objective) and Recover. M10 slice I: the admins'
// auto deadmin (handle_auto_deadmin_roles). Gaps E1b: get_valid_overflow_jobs and has_minimum_jobs (the random events).

// tg: code/controllers/subsystem/job.dm:1 SUBSYSTEM_DEF(job)
// The [DmPath] of /datum/controller/subsystem/job is on its part in controllers/configuration/entries/jobs.cs, the earliest .dme file that declares it (rules 2.3).
public partial class datum_controller_subsystem_job : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/job.dm:2
    public datum_controller_subsystem_job()
    {
        name = "Jobs";
        ss_flags = SS_NO_FIRE;
    }

    // tg: code/controllers/subsystem/job.dm:3  dependencies = list(...) (an object-creating default, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dependencies = Dm.list(
            typeof(datum_controller_subsystem_processing_station));
        // The object-creating defaults of tg's var block (the types generator's GAP list, rules 10.2; M3 I1b), in tg's order.
        all_occupations = Dm.list(); // tg: code/controllers/subsystem/job.dm:9
        joinable_occupations = Dm.list(); // tg: code/controllers/subsystem/job.dm:11
        name_occupations = Dm.list(); // tg: code/controllers/subsystem/job.dm:13
        type_occupations = Dm.list(); // tg: code/controllers/subsystem/job.dm:15
        experience_jobs_map = Dm.list(); // tg: code/controllers/subsystem/job.dm:18
        joinable_departments = Dm.list(); // tg: code/controllers/subsystem/job.dm:21
        joinable_departments_by_type = Dm.list(); // tg: code/controllers/subsystem/job.dm:23
        unassigned = Dm.list(); // tg: code/controllers/subsystem/job.dm:25
        prioritized_jobs = Dm.list(); // tg: code/controllers/subsystem/job.dm:31
        latejoin_trackers = Dm.list(); // tg: code/controllers/subsystem/job.dm:32
        level_order = Dm.list(JP_HIGH, JP_MEDIUM, JP_LOW); // tg: code/controllers/subsystem/job.dm:36
        chain_of_command = Dm.list( // tg: code/controllers/subsystem/job.dm:50
            Dm.kv(JOB_CAPTAIN, 1),
            Dm.kv(JOB_HEAD_OF_PERSONNEL, 2),
            Dm.kv(JOB_RESEARCH_DIRECTOR, 3),
            Dm.kv(JOB_CHIEF_ENGINEER, 4),
            Dm.kv(JOB_CHIEF_MEDICAL_OFFICER, 5),
            Dm.kv(JOB_HEAD_OF_SECURITY, 6),
            Dm.kv(JOB_QUARTERMASTER, 7));
        job_config_datum_singletons = Dm.list(); // tg: code/controllers/subsystem/job.dm:76
    }

    /// DEVIATION(DV180): the turfs of the start landmarks the round start deleted (obj_effect_landmark_start.after_round_start), the late
    /// join's last resort until slice G loads the arrivals shuttle.
    public DmList last_resort_turfs = Dm.list();

    /// DEVIATION(DV180): the same turfs by the landmark's name (the job's title): a late joiner arrives at its own job's start, where its
    /// access opens the doors, not at another job's (a scientist's office locks an engineer in).
    public DmList job_start_turfs = Dm.list();

    /// DEVIATION(DV180): one of <see cref="job_start_turfs"/> of the job titled <paramref name="title"/>, null when it had none.
    public object job_start_turf(object title)
        => title != null && job_start_turfs[title] is DmList own && Dm.truthy(length(own)) ? pick(own) : null;

    // The New() that SUBSYSTEM_DEF(job) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSjob) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/job.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSjob != this)
        {
            if (SSjob is datum_controller_subsystem_job)
            {
                Recover();
                qdel(SSjob);
            }
            SSjob = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/job.dm:92
    [DmParams()]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        setup_job_lists();
        job_config_datum_singletons = generate_config_singletons() as DmList; // we set this up here regardless in case someone wants to use the verb to generate the config file.
        if (!Dm.truthy(length(all_occupations)))
            setup_occupations();
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_load_jobs_from_txt))))
            load_jobs_from_config();
        set_overflow_role(config.Get(typeof(datum_config_entry_string_overflow_job))); // this must always go after load_jobs_from_config() due to how the legacy systems operate, this always takes precedent.

        // Initialize RETA system - must be after setup_occupations() so all jobs are loaded - code/modules/reta/reta_system.dm
        log_game("RETA: Jobs subsystem Initialize() calling RETA initialization");
        initialize_reta_system();
        populate_reta_job_trims();
        log_game("RETA: Jobs subsystem Initialize() RETA initialization completed");

        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/job.dm:122
    public virtual void set_overflow_role(object new_overflow_role = null)
    {
        datum_job new_overflow = (ispath(new_overflow_role) ? get_job_type(new_overflow_role) : get_job(new_overflow_role)) as datum_job;
        if (!Dm.truthy(new_overflow))
        {
            job_debug(Dm.text("SET_OVRFLW: Failed to set new overflow role: []", new_overflow_role));
            throw Dm.CRASH(Dm.text("set_overflow_role failed | new_overflow_role: []", new_overflow_role == null ? "null" : new_overflow_role), "code/controllers/subsystem/job.dm", 126);
        }
        object cap = config.Get(typeof(datum_config_entry_number_overflow_cap));

        new_overflow.allow_bureaucratic_error = FALSE;
        new_overflow.spawn_positions = Dm.num(cap);
        new_overflow.total_positions = Dm.num(cap);
        Dm.combine(ref new_overflow.job_flags, JOB_CANNOT_OPEN_SLOTS);

        if (new_overflow.type == overflow_role)
            return;
        datum_job old_overflow = get_job_type(overflow_role) as datum_job;
        old_overflow.allow_bureaucratic_error = Dm.initial(old_overflow, (datum_job p) => p.allow_bureaucratic_error);
        old_overflow.spawn_positions = Dm.initial(old_overflow, (datum_job p) => p.spawn_positions);
        old_overflow.total_positions = Dm.initial(old_overflow, (datum_job p) => p.total_positions);
        if (!Dm.truthy(Dm.bitand(Dm.initial(old_overflow, (datum_job p) => p.job_flags), JOB_CANNOT_OPEN_SLOTS)))
            old_overflow.job_flags = Dm.bitand(old_overflow.job_flags, Dm.bitnot(JOB_CANNOT_OPEN_SLOTS));
        overflow_role = new_overflow.type;
        job_debug(Dm.text("SET_OVRFLW: Overflow role set to: []", new_overflow.type));
    }

    // tg: code/controllers/subsystem/job.dm:145
    public virtual object setup_occupations()
    {
        name_occupations = Dm.list();
        type_occupations = Dm.list();

        DmList all_jobs = subtypesof(typeof(datum_job));
        if (!Dm.truthy(length(all_jobs)))
        {
            all_occupations = Dm.list();
            joinable_occupations = Dm.list();
            joinable_departments = Dm.list();
            joinable_departments_by_type = Dm.list();
            experience_jobs_map = Dm.list();
            to_chat(world, span_boldannounce(I18n.T("Error setting up jobs, no job datums found")));
            return FALSE;
        }

        DmList new_all_occupations = Dm.list();
        DmList new_joinable_occupations = Dm.list();
        DmList new_joinable_departments = Dm.list();
        DmList new_joinable_departments_by_type = Dm.list();
        DmList new_experience_jobs_map = Dm.list();

        foreach (object job_type in Dm.each(all_jobs))
        {
            datum_job job = Dm.@new(job_type as Type) as datum_job;
            if (!Dm.truthy(job.config_check()))
                continue;
            if (!Dm.truthy(job.map_check())) //Even though we initialize before mapping, this is fine because the config is loaded at new
            {
                log_job_debug(Dm.text("Removed [] due to map config", job.title));
                continue;
            }
            Dm.append(ref new_all_occupations, job);
            name_occupations[job.title] = job;
            foreach (object alt_title in Dm.each(job.alternate_titles))
                name_occupations[alt_title] = job;
            type_occupations[job_type] = job;

            if (Dm.truthy(Dm.bitand(job.job_flags, JOB_NEW_PLAYER_JOINABLE)))
            {
                Dm.append(ref new_joinable_occupations, job);
                if (!Dm.truthy(LAZYLEN(job.departments_list)))
                {
                    datum_job_department department = new_joinable_departments_by_type[typeof(datum_job_department_undefined)] as datum_job_department;
                    if (!Dm.truthy(department))
                    {
                        department = Dm.@new<datum_job_department_undefined>();
                        new_joinable_departments_by_type[typeof(datum_job_department_undefined)] = department;
                    }
                    department.add_job(job);
                    continue;
                }
                foreach (object department_type in Dm.each(job.departments_list))
                {
                    datum_job_department department = new_joinable_departments_by_type[department_type] as datum_job_department;
                    if (!Dm.truthy(department))
                    {
                        department = Dm.@new(department_type as Type) as datum_job_department;
                        new_joinable_departments_by_type[department_type] = department;
                    }
                    department.add_job(job);
                }
            }
        }

        sortTim(new_all_occupations, "/proc/" + nameof(GlobalProcs.cmp_job_display_with_departments_asc));
        foreach (datum_job job in Dm.each(new_all_occupations))
        {
            if (!Dm.truthy(job.exp_granted_type))
                continue;
            new_experience_jobs_map[job.exp_granted_type] = Dm.add(new_experience_jobs_map[job.exp_granted_type], Dm.list(job));
        }

        sortTim(new_joinable_departments_by_type, "/proc/" + nameof(GlobalProcs.cmp_department_display_asc), associative: TRUE);
        foreach (object department_type in Dm.each(new_joinable_departments_by_type))
        {
            datum_job_department department = new_joinable_departments_by_type[department_type] as datum_job_department;
            sortTim(department.department_jobs, "/proc/" + nameof(GlobalProcs.cmp_job_display_with_departments_asc));
            Dm.append(ref new_joinable_departments, department);
            if (Dm.truthy(department.department_experience_type))
                new_experience_jobs_map[department.department_experience_type] = department.department_jobs.Copy();
        }

        all_occupations = new_all_occupations;
        joinable_occupations = sortTim(new_joinable_occupations, "/proc/" + nameof(GlobalProcs.cmp_job_display_with_departments_asc)) as DmList;
        joinable_departments = new_joinable_departments;
        joinable_departments_by_type = new_joinable_departments_by_type;
        experience_jobs_map = new_experience_jobs_map;

        if (Dm.truthy(this._listen_lookup?[COMSIG_OCCUPATIONS_SETUP]))
            this._SendSignal(COMSIG_OCCUPATIONS_SETUP, Dm.list(this));

        return TRUE;
    }

    // tg: code/controllers/subsystem/job.dm:219
    public virtual object get_job(object rank = null)
    {
        if (!Dm.truthy(length(all_occupations)))
            setup_occupations();
        return name_occupations[rank];
    }

    // tg: code/controllers/subsystem/job.dm:224
    public virtual object get_job_type(object jobtype = null)
    {
        if (!Dm.truthy(length(all_occupations)))
            setup_occupations();
        return type_occupations[jobtype];
    }

    // tg: code/controllers/subsystem/job.dm:230
    public virtual object get_department_type(object department_type = null)
    {
        if (!Dm.truthy(length(all_occupations)))
            setup_occupations();
        return joinable_departments_by_type[department_type];
    }

    // tg: code/controllers/subsystem/job.dm:787  (gaps V1)
    public virtual object get_living_heads()
    {
        DmList dot = Dm.list();
        foreach (datum_mind head in Dm.each_cast<datum_mind>(get_crewmember_minds()))
        {
            if (!Dm.truthy(Dm.bitand((head.assigned_role as datum_job).job_flags, JOB_HEAD_OF_STAFF)))
                continue;
            if (head.current == null || head.current.stat == DEAD)
                continue;
            Dm.append(ref dot, head);
        }
        return dot;
    }

    /// Returns a list of minds of all heads of staff
    // tg: code/controllers/subsystem/job.dm:797
    public virtual object get_all_heads()
    {
        DmList dot = Dm.list();
        foreach (datum_mind head in Dm.each<datum_mind>(get_crewmember_minds()))
        {
            if (Dm.truthy(Dm.bitand((head.assigned_role as datum_job).job_flags, JOB_HEAD_OF_STAFF)))
                Dm.append(dot, head);
        }
        return dot;
    }

    /// Returns a list of minds of all security members who are alive
    // tg: code/controllers/subsystem/job.dm:804  (gaps V1)
    public virtual object get_living_sec()
    {
        DmList dot = Dm.list();
        foreach (datum_mind sec in Dm.each_cast<datum_mind>(get_crewmember_minds()))
        {
            if (!Dm.truthy(Dm.bitand((sec.assigned_role as datum_job).departments_bitflags, DEPARTMENT_BITFLAG_SECURITY)))
                continue;
            if (sec.current == null || sec.current.stat == DEAD)
                continue;
            Dm.append(ref dot, sec);
        }
        return dot;
    }

    /// Returns a list of minds of all security members
    // tg: code/controllers/subsystem/job.dm:814  (gaps V1)
    public virtual object get_all_sec()
    {
        DmList dot = Dm.list();
        foreach (datum_mind sec in Dm.each_cast<datum_mind>(get_crewmember_minds()))
        {
            if (Dm.truthy(Dm.bitand((sec.assigned_role as datum_job).departments_bitflags, DEPARTMENT_BITFLAG_SECURITY)))
                Dm.append(ref dot, sec);
        }
        return dot;
    }

    // tg: code/controllers/subsystem/job.dm:820
    public virtual void job_debug(object message = null)
    {
        log_job_debug(message);
    }

    /// Builds various lists of jobs based on station, centcom and additional jobs with icons associated with them.
    // tg: code/controllers/subsystem/job.dm:824
    public virtual void setup_job_lists()
    {
        job_priorities_to_strings = Dm.list(
            Dm.kv(Dm.text("[]", JP_LOW), "Low Priority"),
            Dm.kv(Dm.text("[]", JP_MEDIUM), "Medium Priority"),
            Dm.kv(Dm.text("[]", JP_HIGH), "High Priority"));
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/job.dm:1  SUBSYSTEM_DEF(job): GLOBAL_REAL(SSjob, /datum/controller/subsystem/job)
    internal datum_controller_subsystem_job SSjob;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_job SSjob => ref TgState.Current.SSjob;
}

// tg: code/controllers/subsystem/job.dm:244 (M10 slice F)
public partial class datum_controller_subsystem_job : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/job.dm:244
    public virtual object assign_role(mob_dead_new_player player = null, datum_job job = null, object latejoin = null, object do_eligibility_checks = null)
    {
        latejoin ??= FALSE;
        do_eligibility_checks ??= TRUE;
        job_debug(Dm.text("AR: Running, Player: [], Job: [], LateJoin: []", player, job == null ? "null" : job, latejoin));
        if (player?.mind == null || job == null)
        {
            job_debug(Dm.text("AR: Failed, player has no mind or job is null. Player: [], Rank: []", player, job == null ? "null" : job.type));
            return FALSE;
        }

        if (Dm.truthy(do_eligibility_checks) && !Dm.eq(check_job_eligibility(player, job, "AR", add_job_to_log: TRUE), JOB_AVAILABLE))
            return FALSE;

        job_debug(Dm.text("AR: Role now set and assigned - [] is [], JCP:[], JPL:[]", player, job.title, job.current_positions, Dm.truthy(latejoin) ? job.total_positions : job.spawn_positions));
        player.mind.set_assigned_role(job);
        Dm.remove(ref unassigned, player);
        job.current_positions++;
        return TRUE;
    }

    // tg: code/controllers/subsystem/job.dm:259
    public virtual DmList find_occupation_candidates(datum_job job = null, object level = null)
    {
        level ??= 0;
        job_debug(Dm.text("FOC: Now running, Job: [], Level: []", job, job_priority_level_to_string(level)));
        DmList candidates = Dm.list();
        foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(unassigned.Copy()))
        {
            if (player == null)
            {
                job_debug("FOC: Player no longer exists.");
                continue;
            }

            if (player.client == null)
            {
                job_debug(Dm.text("FOC: Player client no longer exists, Player: []", player));
                continue;
            }

            // Initial screening check. Does the player even have the job enabled, if they do - Is it at the correct priority level?
            object player_job_level = player.client?.prefs.job_preferences[job.title];
            if (player_job_level == null)
            {
                job_debug(Dm.text("FOC: Player job not enabled, Player: []", player));
                continue;
            }

            if (Dm.truthy(level) && !Dm.eq(player_job_level, level))
            {
                job_debug(Dm.text("FOC: Player job enabled at wrong level, Player: [], TheirLevel: [], ReqLevel: []", player, job_priority_level_to_string(player_job_level), job_priority_level_to_string(level)));
                continue;
            }

            // This check handles its own output to job_debug.
            if (!Dm.eq(check_job_eligibility(player, job, "FOC", add_job_to_log: FALSE), JOB_AVAILABLE))
                continue;

            // They have the job enabled, at this priority level, with no restrictions applying to them.
            job_debug(Dm.text("FOC: Player eligible, Player: [], Level: []", player, job_priority_level_to_string(level)));
            Dm.append(ref candidates, player);
        }
        return candidates;
    }

    // tg: code/controllers/subsystem/job.dm:291
    public virtual object give_random_job(mob_dead_new_player player = null)
    {
        job_debug(Dm.text("GRJ: Giving random job, Player: []", player));
        object dot = FALSE;
        foreach (datum_job job in Dm.each<datum_job>(shuffle(joinable_occupations)))
        {
            if (QDELETED(player))
            {
                job_debug("GRJ: Player is deleted, aborting");
                break;
            }

            if ((job.current_positions >= job.spawn_positions) && job.spawn_positions != -1)
            {
                job_debug(Dm.text("GRJ: Job lacks spawn positions to be eligible, Player: [], Job: []", player, job));
                continue;
            }

            if (istype(job, get_job_type(overflow_role) is datum_job overflow ? overflow.type : null)) // We don't want to give him assistant, that's boring!
            {
                job_debug(Dm.text("GRJ: Skipping overflow role, Player: [], Job: []", player, job));
                continue;
            }

            if (Dm.truthy(Dm.bitand(job.departments_bitflags, DEPARTMENT_BITFLAG_COMMAND))) //If you want a command position, select it!
            {
                job_debug(Dm.text("GRJ: Skipping command role, Player: [], Job: []", player, job));
                continue;
            }

            // This check handles its own output to job_debug.
            if (!Dm.eq(check_job_eligibility(player, job, "GRJ", add_job_to_log: TRUE), JOB_AVAILABLE))
                continue;

            if (Dm.truthy(assign_role(player, job, do_eligibility_checks: FALSE)))
            {
                job_debug(Dm.text("GRJ: Random job given, Player: [], Job: []", player, job));
                return TRUE;
            }

            job_debug(Dm.text("GRJ: Player eligible but assign_role failed, Player: [], Job: []", player, job));
        }
        return dot;
    }

    // tg: code/controllers/subsystem/job.dm:322
    public virtual void reset_occupations()
    {
        job_debug("RO: Occupations reset.");
        foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(GLOB.new_player_list))
        {
            if (player?.mind == null)
                continue;
            player.mind.set_assigned_role(get_job_type(typeof(datum_job_unassigned)) as datum_job);
        }
        setup_occupations();
        unassigned = Dm.list();
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_load_jobs_from_txt))))
        {
            // Any errors with the configs has already been said, we don't need to repeat them here.
            load_jobs_from_config(silent: TRUE);
        }
        set_overflow_role(overflow_role);
    }

    /*
     * Forces a random Head of Staff role to be assigned to a random eligible player.
     * Returns TRUE if a player was selected and assigned the role. FALSE otherwise.
     */
    // tg: code/controllers/subsystem/job.dm:341
    public virtual object force_one_head_assignment()
    {
        datum_job_department command_department = get_department_type(typeof(datum_job_department_command)) as datum_job_department;
        if (command_department == null)
            return FALSE;
        foreach (object level in Dm.each(level_order))
        {
            foreach (datum_job job in Dm.each<datum_job>(command_department.department_jobs))
            {
                if ((job.current_positions >= job.total_positions) && job.total_positions != -1)
                    continue;
                DmList candidates = find_occupation_candidates(job, level);
                if (!Dm.truthy(candidates.len))
                    continue;
                mob_dead_new_player candidate = pick(candidates) as mob_dead_new_player;
                // Eligibility checks done as part of find_occupation_candidates.
                if (Dm.truthy(assign_role(candidate, job, do_eligibility_checks: FALSE)))
                    return TRUE;
            }
        }
        return FALSE;
    }

    /**
     * Attempts to fill out all possible head positions for players with that job at a a given job priority level.
     * Returns the number of Head positions assigned.
     *
     * Arguments:
     * * level - One of the JP_LOW, JP_MEDIUM, JP_HIGH or JP_ANY defines. Attempts to find candidates with head jobs at that priority only.
     */
    // tg: code/controllers/subsystem/job.dm:366
    public virtual float fill_all_head_positions_at_priority(object level = null)
    {
        float dot = 0;
        datum_job_department command_department = get_department_type(typeof(datum_job_department_command)) as datum_job_department;

        if (command_department == null)
            return dot;

        foreach (datum_job job in Dm.each<datum_job>(command_department.department_jobs))
        {
            if ((job.current_positions >= job.total_positions) && job.total_positions != -1)
                continue;

            DmList candidates = find_occupation_candidates(job, level);
            if (!Dm.truthy(candidates.len))
                continue;

            mob_dead_new_player candidate = pick(candidates) as mob_dead_new_player;

            // Eligibility checks done as part of find_occupation_candidates() above.
            if (!Dm.truthy(assign_role(candidate, job, do_eligibility_checks: FALSE)))
                continue;

            dot++;

            if ((job.current_positions >= job.spawn_positions) && job.spawn_positions != -1)
                job_debug(Dm.text("JOBS: Command Job is now full, Job: [], Positions: [], Limit: []", job, job.current_positions, job.spawn_positions));
        }
        return dot;
    }

    /// Attempts to fill out all available AI positions.
    // tg: code/controllers/subsystem/job.dm:393
    public virtual void fill_ai_positions()
    {
        datum_job ai_job = get_job(JOB_AI) as datum_job;
        if (ai_job == null)
            return;
        // In byond for(in to) loops, the iteration is inclusive so we need to stop at ai_job.total_positions - 1
        for (float i = ai_job.current_positions; i <= ai_job.total_positions - 1; i++)
        {
            foreach (object level in Dm.each(level_order))
            {
                DmList candidates = find_occupation_candidates(ai_job, level);
                if (Dm.truthy(candidates.len))
                {
                    mob_dead_new_player candidate = pick(candidates) as mob_dead_new_player;
                    // Eligibility checks done as part of find_occupation_candidates
                    if (Dm.truthy(assign_role(candidate, get_job_type(typeof(datum_job_ai)) as datum_job, do_eligibility_checks: FALSE)))
                        break;
                }
            }
        }
    }

    /** Proc divide_occupations
     *  fills var "assigned_role" for all ready players.
     *  This proc must not have any side effect besides of modifying "assigned_role".
     **/
    // tg: code/controllers/subsystem/job.dm:413
    public virtual async DmTask<object> divide_occupations(object pure = null, object allow_all = null)
    {
        pure ??= FALSE;
        allow_all ??= FALSE;
        //Setup new player list and get the jobs list
        job_debug(Dm.text("DO: Running, allow_all = [], pure = []", allow_all, pure));
        run_divide_occupation_pure = Dm.num(pure);
        // SEND_SIGNAL(src, COMSIG_OCCUPATIONS_DIVIDED, pure, allow_all) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_OCCUPATIONS_DIVIDED]))
            this._SendSignal(COMSIG_OCCUPATIONS_DIVIDED, Dm.list(this, pure, allow_all));

        //Get the players who are ready
        foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(GLOB.new_player_list))
        {
            if (Dm.eq(player.ready, PLAYER_READY_TO_PLAY) && Dm.truthy(player.check_job_preferences(!Dm.truthy(pure) ? TRUE : FALSE)) && player.mind != null && is_unassigned_job(player.mind.assigned_role))
                Dm.append(ref unassigned, player);
        }

        initial_players_to_assign = length(unassigned);

        job_debug(Dm.text("DO: Player count to assign roles to: []", initial_players_to_assign));

        //Scale number of open security officer slots to population
        setup_officer_positions();

        //Jobs will have fewer access permissions if the number of players exceeds the threshold defined in game_options.txt
        object min_access_threshold = config.Get(typeof(datum_config_entry_number_minimal_access_threshold));
        if (Dm.truthy(min_access_threshold))
        {
            if (Dm.gt(min_access_threshold, initial_players_to_assign))
                Waiting.NoSleep(config.Set(typeof(datum_config_entry_flag_jobs_have_minimal_access), FALSE));
            else
                Waiting.NoSleep(config.Set(typeof(datum_config_entry_flag_jobs_have_minimal_access), TRUE));
        }

        //Shuffle player list.
        shuffle_inplace(unassigned);

        handle_feedback_gathering();

        // Assign any priority positions before all other standard job selections.
        job_debug("DO: Assigning priority positions");
        assign_priority_positions();
        job_debug("DO: Priority assignment complete");

        // The overflow role has limitless slots, plus having the Overflow box ticked in prefs should (with one exception) set the priority to JP_HIGH.
        // So everyone with overflow enabled will get that job. Thus we can assign it immediately to all players that have it enabled.
        job_debug("DO: Assigning early overflow roles");
        assign_all_overflow_positions();
        job_debug("DO: Early overflow roles assigned.");

        // Copy the joinable occupation list and filter out ineligible occupations due to above job assignments.
        DmList available_occupations = joinable_occupations.Copy();
        datum_job_department command_department = get_department_type(typeof(datum_job_department_command)) as datum_job_department;

        foreach (datum_job job in Dm.each<datum_job>(available_occupations.Copy()))
        {
            // Make sure the job isn't filled. If it is, remove it from the list so it doesn't get checked.
            if ((job.current_positions >= job.spawn_positions) && job.spawn_positions != -1)
            {
                job_debug(Dm.text("DO: Job is now filled, Job: [], Current: [], Limit: []", job, job.current_positions, job.spawn_positions));
                Dm.remove(ref available_occupations, job);
                continue;
            }

            // Command jobs are handled via fill_all_head_positions_at_priority(...)
            // Remove these jobs from the list of available occupations to prevent multiple players being assigned to the same
            // limited role without constantly having to iterate over the available_occupations list and re-check them.
            if (command_department != null && Dm.@in(job, command_department.department_jobs))
                Dm.remove(ref available_occupations, job);
        }

        job_debug("DO: Running standard job assignment");

        foreach (object level in Dm.each(level_order))
        {
            job_debug(Dm.text("JOBS: Filling in head roles, Level: []", job_priority_level_to_string(level)));
            // Fill the head jobs first each level
            fill_all_head_positions_at_priority(level);

            // Loop through all unassigned players
            foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(unassigned.Copy()))
            {
                if (!Dm.truthy(allow_all))
                {
                    if (Dm.truthy(popcap_reached()))
                    {
                        job_debug(Dm.text("JOBS: Popcap reached, trying to reject player: []", player));
                        try_reject_player(player);
                    }
                }

                job_debug(Dm.text("JOBS: Finding a job for player: [], at job priority pref: []", player, job_priority_level_to_string(level)));

                // Loop through all jobs and build a list of jobs this player could be eligible for.
                DmList possible_jobs = Dm.list();
                foreach (datum_job job in Dm.each<datum_job>(available_occupations))
                {
                    // Filter any job that doesn't fit the current level.
                    object player_job_level = player.client?.prefs.job_preferences[job.title];
                    if (player_job_level == null)
                    {
                        job_debug(Dm.text("JOBS: Job not enabled, Job: []", job));
                        continue;
                    }
                    if (!Dm.eq(player_job_level, level))
                    {
                        job_debug(Dm.text("JOBS: Job enabled at different priority pref, Job: [], TheirLevel: [], ReqLevel: []", job, job_priority_level_to_string(player_job_level), job_priority_level_to_string(level)));
                        continue;
                    }

                    if (!Dm.eq(check_job_eligibility(player, job, "JOBS", add_job_to_log: TRUE), JOB_AVAILABLE))
                        continue;

                    Dm.append(ref possible_jobs, job);
                }

                // If there are no possible jobs for them at this priority, skip them.
                if (!Dm.truthy(length(possible_jobs)))
                {
                    job_debug(Dm.text("JOBS: Player not eligible for any available jobs at this priority level: []", player));
                    continue;
                }

                // Otherwise, pick one of those jobs at random.
                datum_job picked_job = pick(possible_jobs) as datum_job;

                job_debug(Dm.text("JOBS: Now assigning role to player: [], Job:[]", player, picked_job.title));
                assign_role(player, picked_job, do_eligibility_checks: FALSE);
                if ((picked_job.current_positions >= picked_job.spawn_positions) && picked_job.spawn_positions != -1)
                {
                    job_debug(Dm.text("JOBS: Job is now full, Job: [], Positions: [], Limit: []", picked_job, picked_job.current_positions, picked_job.spawn_positions));
                    Dm.remove(ref available_occupations, picked_job);
                }
            }
        }

        job_debug("DO: Ending standard job assignment");

        job_debug("DO: Handle unassigned");
        // For any players that didn't get a job, fall back on their pref setting for what to do.
        foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(unassigned.Copy()))
            handle_unassigned(player, allow_all);
        job_debug("DO: Ending handle unassigned");

        job_debug("DO: Handle unrejectable unassigned");
        //Mop up people who can't leave.
        foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(unassigned.Copy())) //Players that wanted to back out but couldn't because they're antags (can you feel the edge case?)
        {
            if (!Dm.truthy(give_random_job(player)))
            {
                if (!Dm.truthy(assign_role(player, get_job_type(overflow_role) as datum_job))) //If everything is already filled, make them an assistant
                {
                    job_debug("DO: Forced antagonist could not be assigned any random job or the overflow role. divide_occupations failed.");
                    job_debug("---------------------------------------------------");
                    run_divide_occupation_pure = FALSE;
                    return FALSE; //Living on the edge, the forced antagonist couldn't be assigned to overflow role (bans, client age) - just reroll
                }
            }
        }
        job_debug("DO: Ending handle unrejectable unassigned");

        job_debug("All divide occupations tasks completed.");
        job_debug("---------------------------------------------------");
        run_divide_occupation_pure = FALSE;
        return TRUE;
    }

    //We couldn't find a job from prefs for this guy.
    // tg: code/controllers/subsystem/job.dm:553
    public virtual void handle_unassigned(mob_dead_new_player player = null, object allow_all = null)
    {
        allow_all ??= FALSE;
        object jobless_role = player.client.prefs.read_preference(typeof(datum_preference_choiced_jobless_role));

        if (!Dm.truthy(allow_all))
        {
            if (Dm.truthy(popcap_reached()))
            {
                job_debug(Dm.text("HU: Popcap reached, trying to reject player: []", player));
                try_reject_player(player);
                return;
            }
        }

        if (Dm.eq(jobless_role, BEOVERFLOW))
        {
            datum_job overflow_role_datum = get_job_type(overflow_role) as datum_job;

            if ((overflow_role_datum.current_positions >= overflow_role_datum.spawn_positions) && overflow_role_datum.spawn_positions != -1)
            {
                job_debug(Dm.text("HU: Overflow role player cap reached, trying to reject: []", player));
                try_reject_player(player);
                return;
            }

            if (!Dm.eq(check_job_eligibility(player, overflow_role_datum, debug_prefix: "HU", add_job_to_log: TRUE), JOB_AVAILABLE))
            {
                job_debug(Dm.text("HU: Player cannot be overflow, trying to reject: []", player));
                try_reject_player(player);
                return;
            }

            if (!Dm.truthy(assign_role(player, overflow_role_datum, do_eligibility_checks: FALSE)))
            {
                job_debug(Dm.text("HU: Player could not be assigned overflow role, trying to reject: []", player));
                try_reject_player(player);
                return;
            }
        }
        else if (Dm.eq(jobless_role, BERANDOMJOB))
        {
            if (!Dm.truthy(give_random_job(player)))
            {
                job_debug(Dm.text("HU: Player cannot be given a random job, trying to reject: []", player));
                try_reject_player(player);
                return;
            }
        }
        else if (Dm.eq(jobless_role, RETURNTOLOBBY))
        {
            job_debug(Dm.text("HU: Player unable to be assigned job, return to lobby enabled: []", player));
            try_reject_player(player);
            return;
        }
        else //Something gone wrong if we got here.
        {
            job_debug(Dm.text("HU: [] has an invalid jobless_role var: []", player, jobless_role));
            log_game(Dm.text("[] has an invalid jobless_role var: []", player, jobless_role));
            message_admins(Dm.text("[] has an invalid jobless_role, this shouldn't happen.", player));
            try_reject_player(player);
        }
    }

    //Gives the player the stuff he should have with his rank
    // tg: code/controllers/subsystem/job.dm:597
    public virtual async DmTask equip_rank(mob_living equipping = null, datum_job job = null, client player_client = null)
    {
        equipping.job = job.title;

        // SEND_SIGNAL(equipping, COMSIG_JOB_RECEIVED, job) (rules 11.1 (e), a statement)
        if (Dm.truthy(equipping._listen_lookup?[COMSIG_JOB_RECEIVED]))
            equipping._SendSignal(COMSIG_JOB_RECEIVED, Dm.list(equipping, job));

        equipping.mind?.set_assigned_role_with_greeting(job, player_client);
        await equipping.on_job_equipping(job, player_client);
        await job.announce_job(equipping);

        if (player_client?.holder != null)
        {
            if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_auto_deadmin_always))) || ((int)Dm.num(player_client.prefs?.toggles) & DEADMIN_ALWAYS) != 0)
                player_client.holder.auto_deadmin();
            else
                handle_auto_deadmin_roles(player_client, job.title);
        }

        await job.after_spawn(equipping, player_client);
    }

    // tg: code/controllers/subsystem/job.dm:614
    public virtual object handle_auto_deadmin_roles(client C = null, object rank = null)
    {
        if (C?.holder == null)
            return TRUE;
        datum_job job = get_job(rank) as datum_job;

        bool timegate_expired = false;
        // allow only forcing deadminning in the first X seconds of the round if auto_deadmin_timegate is set in config
        object timegate = config.Get(typeof(datum_config_entry_number_auto_deadmin_timegate));
        if (Dm.truthy(timegate) && (Dm.num(Dm.world.time) - Dm.num(SSticker.round_start_time) > Dm.num(timegate)))
            timegate_expired = true;

        if (job == null)
            return null;
        int flags = (int)job.auto_deadmin_role_flags, toggles = (int)Dm.num(C.prefs?.toggles);
        if ((flags & DEADMIN_POSITION_HEAD) != 0 && ((Dm.truthy(config.Get(typeof(datum_config_entry_flag_auto_deadmin_heads))) && !timegate_expired) || (toggles & DEADMIN_POSITION_HEAD) != 0))
            return C.holder.auto_deadmin();
        else if ((flags & DEADMIN_POSITION_SECURITY) != 0 && ((Dm.truthy(config.Get(typeof(datum_config_entry_flag_auto_deadmin_security))) && !timegate_expired) || (toggles & DEADMIN_POSITION_SECURITY) != 0))
            return C.holder.auto_deadmin();
        else if ((flags & DEADMIN_POSITION_SILICON) != 0 && ((Dm.truthy(config.Get(typeof(datum_config_entry_flag_auto_deadmin_silicons))) && !timegate_expired) || (toggles & DEADMIN_POSITION_SILICON) != 0)) //in the event there's ever psuedo-silicon roles added, ie synths.
            return C.holder.auto_deadmin();
        return null;
    }

    // tg: code/controllers/subsystem/job.dm:634
    public virtual void setup_officer_positions()
    {
        datum_job J = SSjob.get_job(JOB_SECURITY_OFFICER) as datum_job;
        if (J == null)
            throw Dm.CRASH("setup_officer_positions(): Security officer job is missing", "code/controllers/subsystem/job.dm", 637);

        object ssc = config.Get(typeof(datum_config_entry_number_security_scaling_coeff));
        if (Dm.gt(ssc, 0))
        {
            if (J.spawn_positions > 0)
            {
                float officer_positions = Dm.min(12, Dm.max(J.spawn_positions, round(Dm.num(unassigned.len) / Dm.num(ssc)))); //Scale between configured minimum and 12 officers
                job_debug(Dm.text("SOP: Setting open security officer positions to []", officer_positions));
                J.total_positions = officer_positions;
                J.spawn_positions = officer_positions;
            }
        }

        //Spawn some extra eqipment lockers if we have more than 5 officers
        // for(var/i=equip_needed-5, i>0, i--) a security closet at the next of GLOB.secequipment (:653-663): the spare security lockers'
        // landmarks (/obj/effect/landmark/secequipment) are not translated, so GLOB.secequipment is empty and the loop breaks at once
        // //We ran out of spare locker spawns!
    }

    // tg: code/controllers/subsystem/job.dm:659
    public virtual void handle_feedback_gathering()
    {
        foreach (datum_job job in Dm.each<datum_job>(joinable_occupations))
        {
            float high = 0; //high
            float medium = 0; //medium
            float low = 0; //low
            float never = 0; //never
            float banned = 0; //banned
            float young = 0; //account too young
            float newbie = 0; //exp too low
            foreach (object i in Dm.each(GLOB.new_player_list))
            {
                mob_dead_new_player player = i as mob_dead_new_player;
                if (!(Dm.eq(player.ready, PLAYER_READY_TO_PLAY) && player.mind != null && is_unassigned_job(player.mind.assigned_role)))
                    continue; //This player is not ready
                if (Dm.truthy(is_banned_from(player.ckey, job.title)) || QDELETED(player))
                {
                    banned++;
                    continue;
                }
                if (!Dm.truthy(job.player_old_enough(player.client)))
                {
                    young++;
                    continue;
                }
                if (Dm.truthy(job.required_playtime_remaining(player.client)))
                {
                    newbie++;
                    continue;
                }
                object level = player.client.prefs.job_preferences[job.title];
                if (Dm.eq(level, JP_HIGH))
                    high++;
                else if (Dm.eq(level, JP_MEDIUM))
                    medium++;
                else if (Dm.eq(level, JP_LOW))
                    low++;
                else
                    never++;
            }
            SSblackbox.record_feedback("nested tally", "job_preferences", high, Dm.list(Dm.text("[]", job.title), "high"));
            SSblackbox.record_feedback("nested tally", "job_preferences", medium, Dm.list(Dm.text("[]", job.title), "medium"));
            SSblackbox.record_feedback("nested tally", "job_preferences", low, Dm.list(Dm.text("[]", job.title), "low"));
            SSblackbox.record_feedback("nested tally", "job_preferences", never, Dm.list(Dm.text("[]", job.title), "never"));
            SSblackbox.record_feedback("nested tally", "job_preferences", banned, Dm.list(Dm.text("[]", job.title), "banned"));
            SSblackbox.record_feedback("nested tally", "job_preferences", young, Dm.list(Dm.text("[]", job.title), "young"));
            SSblackbox.record_feedback("nested tally", "job_preferences", newbie, Dm.list(Dm.text("[]", job.title), "newbie"));
        }
    }

    // tg: code/controllers/subsystem/job.dm:698
    public virtual object popcap_reached()
    {
        object hpc = config.Get(typeof(datum_config_entry_number_hard_popcap));
        object epc = config.Get(typeof(datum_config_entry_number_extreme_popcap));
        if (Dm.truthy(hpc) || Dm.truthy(epc))
        {
            float relevent_cap = Dm.max(Dm.num(hpc), Dm.num(epc));
            if ((initial_players_to_assign - unassigned.len) >= relevent_cap)
                return 1;
        }
        return 0;
    }

    // tg: code/controllers/subsystem/job.dm:707
    public virtual object try_reject_player(mob_dead_new_player player = null)
    {
        foreach (datum_dynamic_ruleset_roundstart ruleset in Dm.each<datum_dynamic_ruleset_roundstart>(SSdynamic.queued_rulesets))
        {
            if (Dm.@in(player.mind, ruleset.selected_minds))
            {
                job_debug(Dm.text("RJCT: Player unable to be rejected due to being selected by dynamic, Player: [], Ruleset: []", player, ruleset));
                return FALSE;
            }
        }

        job_debug(Dm.text("RJCT: Player rejected, Player: []", player));
        Dm.remove(ref unassigned, player);
        if (!Dm.truthy(run_divide_occupation_pure))
        {
            to_chat(player, span_infoplain("<b>You have failed to qualify for any job you desired.</b>"));
            player.ready = PLAYER_NOT_READY;
        }
        return null;
    }

    // tg: code/controllers/subsystem/job.dm:751
    public virtual object send_to_late_join(mob M = null, object buckle = null)
    {
        buckle ??= TRUE;
        atom destination;
        if (M.mind != null && !is_unassigned_job(M.mind.assigned_role) && Dm.truthy(length(GLOB.jobspawn_overrides[M.mind.assigned_role.title]))) //We're doing something special today.
        {
            destination = pick(GLOB.jobspawn_overrides[M.mind.assigned_role.title]) as atom;
            Waiting.NoSleep(destination.JoinPlayerHere(M, FALSE));
            return TRUE;
        }

        if (Dm.truthy(latejoin_trackers.len))
        {
            destination = pick(latejoin_trackers) as atom;
            Waiting.NoSleep(destination.JoinPlayerHere(M, buckle));
            return TRUE;
        }

        // DEVIATION(DV180): without the arrivals shuttle (slice G), the joiner's own job's start (where its access opens the doors)
        // before the last resort, which picks any job's
        destination = (job_start_turf(M.mind?.assigned_role?.title) ?? Waiting.NoSleep(get_last_resort_spawn_points())) as atom;
        Waiting.NoSleep(destination.JoinPlayerHere(M, buckle));
        return null;
    }

    // tg: code/controllers/subsystem/job.dm:767
    public virtual async DmTask<object> get_last_resort_spawn_points()
    {
        area_shuttle_arrival arrivals_area = GLOB.areas_by_type[typeof(area_shuttle_arrival)] as area_shuttle_arrival;
        if (arrivals_area != null)
        {
            DmList available_turfs = Dm.list();
            foreach (DmList zlevel_turfs in Dm.each<DmList>(arrivals_area.get_zlevel_turf_lists()))
            {
                foreach (turf arrivals_turf in Dm.each<turf>(zlevel_turfs))
                {
                    obj_structure_chair shuttle_chair = locate<obj_structure_chair>(arrivals_turf);
                    if (shuttle_chair != null)
                        return shuttle_chair;
                    if (Dm.truthy(await arrivals_turf.is_blocked_turf(TRUE)))
                        continue;
                    Dm.append(ref available_turfs, arrivals_turf);
                }
            }

            if (Dm.truthy(length(available_turfs)))
                return pick(available_turfs);
        }

        // DEVIATION(DV180): until slice G loads the arrivals shuttle (SSshuttle's round-start templates), MetaStation has no arrivals
        // area, and tg's GET_ERROR_ROOM (locate(4,4,1), space on the port's station level) would kill the joiner: a station start landmark
        // stands in for the arrivals shuttle's chairs; once the round start deleted them, the turf one of them stood on.
        foreach (obj_effect_landmark_start start in Dm.each<obj_effect_landmark_start>(GLOB.start_landmarks_list))
            return start;
        if (Dm.truthy(length(last_resort_turfs)))
            return pick(last_resort_turfs);

        stack_trace("Unable to find last resort spawn point.", "code/controllers/subsystem/job.dm", 783);
        return (locate<obj_effect_landmark_error>(GLOB.landmarks_list) as atom) ?? locate(4, 4, 1); // GET_ERROR_ROOM (__HELPERS/game.dm:2)
    }

    // tg: code/controllers/subsystem/job.dm:849
    public virtual async DmTask promote_to_captain(mob_living_carbon_human new_captain = null, object acting_captain = null)
    {
        acting_captain ??= FALSE;
        object id_safe_code = SSid_access.spare_id_safe_code;

        if (!Dm.truthy(id_safe_code))
            throw Dm.CRASH(Dm.text("Cannot promote [] to Captain, there is no id_safe_code.", new_captain.real_name), "code/controllers/subsystem/job.dm", 853);

        object paper = Dm.@new<obj_item_folder_biscuit_confidential_spare_id_safe_code>();
        DmList slots = Dm.list(
            LOCATION_LPOCKET,
            LOCATION_RPOCKET,
            LOCATION_BACKPACK,
            LOCATION_HANDS);
        object where = await new_captain.equip_in_one_of_slots(paper as obj_item, slots, FALSE, indirect_action: TRUE) is var slot && Dm.truthy(slot) ? slot : I18n.T("at your feet");

        if (Dm.truthy(acting_captain))
            to_chat(new_captain, span_notice(I18n.T("Due to your position in the chain of command, you have been promoted to Acting Captain. You can find in important note about this [].", where)));
        else
        {
            to_chat(new_captain, span_notice(I18n.T("You can find the code to obtain your spare ID from the secure safe on the Bridge [].", where)));
            new_captain.add_mob_memory(typeof(datum_memory_key_captains_spare_code), Dm.named("safe_code", SSid_access.spare_id_safe_code));
        }

        // Force-give their ID card bridge access.
        obj_item id_slot = new_captain.get_item_by_slot(ITEM_SLOT_ID) as obj_item;
        if (id_slot != null)
        {
            var id_card = id_slot.GetID() as obj_item_card_id;
            if (!Dm.@in(ACCESS_COMMAND, id_card.access))
                id_card.add_wildcards(Dm.list(ACCESS_COMMAND), mode: FORCE_ADD_ALL);
        }

        assigned_captain = TRUE;
    }

    /// Send a drop pod containing a piece of paper with the spare ID safe code to loc
    // tg: code/controllers/subsystem/job.dm:880  (gaps CM1: the communications console's safe code request)
    public virtual void send_spare_id_safe_code(object loc = null)
    {
        Dm.@new<obj_effect_pod_landingzone>(loc, typeof(obj_structure_closet_supplypod_centcompod), Dm.@new<obj_item_folder_biscuit_confidential_emergency_spare_id_safe_code>());
        safe_code_timer_id = null;
        safe_code_request_loc = null;
    }

    /// Takes a job priority #define such as JP_LOW and gets its string representation for logging.
    // tg: code/controllers/subsystem/job.dm:935
    public virtual object job_priority_level_to_string(object priority = null)
    {
        return job_priorities_to_strings?[Dm.text("[]", priority)] ?? Dm.text("Undefined Priority \\[[]\\]", priority);
    }

    // tg: code/controllers/subsystem/job.dm:887
    public virtual void assign_priority_positions()
    {
        job_debug(Dm.text("APP: Assigning Dynamic ruleset forced occupations: []", LAZYLEN(forced_occupations)));
        foreach (datum_mind mind in Dm.each<datum_mind>(forced_occupations))
        {
            mob_dead_new_player new_player = mind.current as mob_dead_new_player;
            // Eligibility checks already carried out as part of the dynamic ruleset trim_candidates proc.
            // However no guarantee of game state between then and now, so don't skip eligibility checks on assign_role.
            assign_role(new_player, get_job_type(LAZYACCESS(forced_occupations, mind)) as datum_job);
        }

        // Get JP_HIGH department Heads of Staff in place. Indirectly useful for the Revolution ruleset to have as many Heads as possible.
        job_debug("APP: Assigning all JP_HIGH head of staff roles.");
        float head_count = fill_all_head_positions_at_priority(JP_HIGH);

        // If nobody has JP_HIGH on a Head role, try to force at least one Head of Staff so every shift has the best chance
        // of having at least one leadership role.
        if (head_count == 0)
            force_one_head_assignment();

        // Fill out all AI positions.
        job_debug("APP: Filling all AI positions");
        fill_ai_positions();
    }

    // tg: code/controllers/subsystem/job.dm:908
    public virtual void assign_all_overflow_positions()
    {
        job_debug("OVRFLW: Assigning all overflow roles.");
        job_debug(Dm.text("OVRFLW: This shift's overflow role: []", overflow_role));
        datum_job overflow_datum = get_job_type(overflow_role) as datum_job;

        // Due to this prefs quirk, we actually don't want to find JP_HIGH candidates as it may exclude people with abnormal pref states that
        // appear normal from the UI. By passing in JP_ANY, it will return all players that have the overflow job pref (which should be a toggle)
        // set to any level.
        DmList overflow_candidates = find_occupation_candidates(overflow_datum, JP_ANY);
        job_debug(Dm.text("OVRFLW: Attempting to assign the overflow role to [] players.", length(overflow_candidates)));
        foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(overflow_candidates))
        {
            if ((overflow_datum.current_positions >= overflow_datum.spawn_positions) && overflow_datum.spawn_positions != -1)
            {
                job_debug(Dm.text("OVRFLW: Overflow role cap reached, role only assigned to [] players.", overflow_datum.current_positions));
                job_debug(Dm.text("OVRFLW: Overflow Job is now full, Job: [], Positions: [], Limit: []", overflow_datum, overflow_datum.current_positions, overflow_datum.spawn_positions));
                return;
            }

            // Eligibility checks done as part of find_occupation_candidates, so skip them.
            assign_role(player, get_job_type(overflow_role) as datum_job, do_eligibility_checks: FALSE);
            job_debug(Dm.text("OVRFLW: Assigned overflow to player: []", player));
        }
        job_debug("OVRFLW: All overflow roles assigned.");
    }

    // tg: code/controllers/subsystem/job.dm:953
    public virtual object check_job_eligibility(mob_dead_new_player player = null, datum_job possible_job = null, object debug_prefix = null, object add_job_to_log = null)
    {
        debug_prefix ??= "";
        add_job_to_log ??= FALSE;
        string job_tag = Dm.truthy(add_job_to_log) ? Dm.text(", Job: []", possible_job) : "";
        if (player.mind == null)
        {
            job_debug(Dm.text("[]: Player has no mind, Player: [][]", debug_prefix, player, job_tag));
            return JOB_UNAVAILABLE_GENERIC;
        }

        if (Dm.@in(possible_job.title, LAZYACCESS(prevented_occupations, player.mind)))
        {
            job_debug(Dm.text("[] Error: [], Player: [][]", debug_prefix, get_job_unavailable_error_message(JOB_UNAVAILABLE_ANTAG_INCOMPAT, possible_job.title), player, job_tag));
            return JOB_UNAVAILABLE_ANTAG_INCOMPAT;
        }

        if (!Dm.truthy(possible_job.player_old_enough(player.client)))
        {
            job_debug(Dm.text("[] Error: [], Player: [][]", debug_prefix, get_job_unavailable_error_message(JOB_UNAVAILABLE_ACCOUNTAGE, possible_job.title), player, job_tag));
            return JOB_UNAVAILABLE_ACCOUNTAGE;
        }

        object required_playtime_remaining = possible_job.required_playtime_remaining(player.client);
        if (Dm.truthy(required_playtime_remaining))
        {
            job_debug(Dm.text("[] Error: [], Player: [], MissingTime: [][]", debug_prefix, get_job_unavailable_error_message(JOB_UNAVAILABLE_PLAYTIME, possible_job.title), player, required_playtime_remaining, job_tag));
            return JOB_UNAVAILABLE_PLAYTIME;
        }

        // Run the banned check last since it should be the rarest check to fail and can access the database.
        if (Dm.truthy(is_banned_from(player.ckey, possible_job.title)))
        {
            job_debug(Dm.text("[] Error: [], Player: [][]", debug_prefix, get_job_unavailable_error_message(JOB_UNAVAILABLE_BANNED, possible_job.title), player, job_tag));
            return JOB_UNAVAILABLE_BANNED;
        }

        // Check for character age
        client player_client = GET_CLIENT(player) as client;
        if (Dm.isnum(possible_job.required_character_age) && Dm.gt(possible_job.required_character_age, player_client.prefs.read_preference(typeof(datum_preference_numeric_age))))
        {
            job_debug(Dm.text("[] Error: [], Player: [][]", debug_prefix, get_job_unavailable_error_message(JOB_UNAVAILABLE_AGE), player, job_tag));
            return JOB_UNAVAILABLE_AGE;
        }

        // Need to recheck the player exists after is_banned_from since it can query the DB which may sleep.
        if (QDELETED(player))
        {
            job_debug(Dm.text("[]: Player is qdeleted, Player: [][]", debug_prefix, player, job_tag));
            return JOB_UNAVAILABLE_GENERIC;
        }

        return JOB_AVAILABLE;
    }
}

// tg: code/controllers/subsystem/job.dm:735
public partial class atom : datum
{
    // tg: code/controllers/subsystem/job.dm:735
    public virtual async DmTask<object> JoinPlayerHere(object joining_mob = null, object buckle = null)
    {
        // By default, just place the mob on the same turf as the marker or whatever.
        // Set joining_mob as the new mob so subtypes can use it as a proper mob.
        if (ispath(joining_mob))
            joining_mob = await Dm.new_async((Type)joining_mob, get_turf(this));
        else
            await ((atom_movable)joining_mob).forceMove(get_turf(this));
        return joining_mob;
    }
}

// tg: code/controllers/subsystem/job.dm:743
public partial class obj_structure_chair : obj_structure
{
    // tg: code/controllers/subsystem/job.dm:744
    public override async DmTask<object> JoinPlayerHere(object joining_mob = null, object buckle = null)
    {
        mob created_joining_mob = await base.JoinPlayerHere(joining_mob, buckle) as mob;
        // Placing a mob in a chair will attempt to buckle it, or else fall back to default.
        if (Dm.truthy(buckle) && isliving(created_joining_mob))
            await buckle_mob(created_joining_mob as mob_living, FALSE, FALSE);
        return created_joining_mob;
    }
}

// tg: code/controllers/subsystem/job.dm:831
public partial class obj_item_paper_paperslip_corporate_fluff_spare_id_safe_code : obj_item_paper_paperslip_corporate_fluff
{
    // tg: code/controllers/subsystem/job.dm:835  (gaps S3)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object safe_code = SSid_access?.spare_id_safe_code; // a host-built room without the Master has no SSid_access
        default_raw_text = Dm.text("Captain's Spare ID safe code combination: []<br><br>The spare ID can be found in its dedicated safe on the bridge.<br><br>If your job would not ordinarily have Head of Staff access, your ID card has been specially modified to possess it.", Dm.truthy(safe_code) ? safe_code : "[REDACTED]");
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/controllers/subsystem/job.dm:840
public partial class obj_item_paper_paperslip_corporate_fluff_emergency_spare_id_safe_code : obj_item_paper_paperslip_corporate_fluff
{
    // tg: code/controllers/subsystem/job.dm:844  (gaps S3)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object safe_code = SSid_access?.spare_id_safe_code; // a host-built room without the Master has no SSid_access
        default_raw_text = Dm.text("Captain's Spare ID safe code combination: []<br><br>The spare ID can be found in its dedicated safe on the bridge.", Dm.truthy(safe_code) ? safe_code : "[REDACTED]");
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/controllers/subsystem/job.dm:1  (gaps E1b: the random events' job helpers)
public partial class datum_controller_subsystem_job : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/job.dm:111  var/static/list/overflow_jobs (a proc-level static, rules 10.6)
    private static ref DmList get_valid_overflow_jobs__overflow_jobs => ref TgState.Current.datum_controller_subsystem_job__get_valid_overflow_jobs__overflow_jobs;

    /// Returns a list of jobs that we are allowed to fuck with during random events
    // tg: code/controllers/subsystem/job.dm:110
    public virtual object get_valid_overflow_jobs()
    {
        if (get_valid_overflow_jobs__overflow_jobs != null)
            return get_valid_overflow_jobs__overflow_jobs;

        get_valid_overflow_jobs__overflow_jobs = Dm.list();
        foreach (datum_job check_job in Dm.each<datum_job>(joinable_occupations))
        {
            if (!Dm.truthy(check_job.allow_bureaucratic_error))
                continue;
            Dm.append(ref get_valid_overflow_jobs__overflow_jobs, check_job);
        }
        return get_valid_overflow_jobs__overflow_jobs;
    }

    /**
     * Check if the station manifest has at least a certain amount of this staff type.
     * If a matching head of staff is on the manifest, automatically passes (returns TRUE)
     *
     * Arguments:
     * * crew_threshold - amount of crew to meet the requirement
     * * jobs - a list of jobs that qualify the requirement
     * * head_jobs - a list of head jobs that qualify the requirement
     *
    */
    // tg: code/controllers/subsystem/job.dm:999
    public virtual object has_minimum_jobs(object crew_threshold = null, DmList jobs = null, DmList head_jobs = null)
    {
        jobs ??= Dm.list();
        head_jobs ??= Dm.list();
        float employees = 0;
        foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))
        {
            if (Dm.@in(target.trim, head_jobs))
                return TRUE;
            if (Dm.@in(target.trim, jobs))
                employees++;
        }

        if (employees > Dm.num(crew_threshold))
            return TRUE;

        return FALSE;
    }
}

public sealed partial class TgState
{
    internal DmList datum_controller_subsystem_job__get_valid_overflow_jobs__overflow_jobs;
}
