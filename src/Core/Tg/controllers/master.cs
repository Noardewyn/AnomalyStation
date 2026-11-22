// Translated from tgstation (snapshot 1d99bd7f): code/controllers/master.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * StonedMC
 *
 * Designed to properly split up a given tick among subsystems
 * Note: if you read parts of this code and think "why is it doing it that way"
 * Odds are, there is a reason
 *
 **/

// See initialization order in /code/game/world.dm
// tg: code/controllers/master.dm:11  GLOBAL_REAL(Master, /datum/controller/master): TgState.Master, GlobalProcs.Master
// (src/Core/Tg/Infrastructure).
// tg: code/controllers/master.dm:12
[DmPath("/datum/controller/master")]
public partial class datum_controller_master : datum_controller
{
    // tg: code/controllers/master.dm:13
    public datum_controller_master()
    {
        name = "Master";
    }

    /// Are we processing (higher values increase the processing delay by n ticks)
    // tg: code/controllers/master.dm:16
    public float processing = TRUE;
    /// How many times have we ran
    // tg: code/controllers/master.dm:18
    public float iteration = 0;
    /// Stack end detector to detect stack overflows that kill the mc's main loop
    // tg: code/controllers/master.dm:20
    public datum_stack_end_detector stack_end_detector;

    /// world.time of last fire, for tracking lag outside of the mc
    // tg: code/controllers/master.dm:23
    public object last_run;

    /// List of subsystems to process().
    // tg: code/controllers/master.dm:26
    public DmList subsystems;

    ///Most recent init stage to complete init.
    // tg: code/controllers/master.dm:29  var/static/init_stage_completed (rules 10.6: a TgState field)
    public static ref object init_stage_completed => ref TgState.Current.datum_controller_master__init_stage_completed;

    // Vars for keeping track of tick drift.
    // tg: code/controllers/master.dm:32
    public object init_timeofday;
    // tg: code/controllers/master.dm:33
    public object init_time;
    // tg: code/controllers/master.dm:34
    public float tickdrift = 0;
    /// Tickdrift as of last tick, w no averaging going on
    // tg: code/controllers/master.dm:36
    public float olddrift = 0;

    /// How long is the MC sleeping between runs, read only (set by Loop() based off of anti-tick-contention heuristics)
    // tg: code/controllers/master.dm:39
    public float sleep_delta = 1;

    /// Only run ticker subsystems for the next n ticks.
    // tg: code/controllers/master.dm:42
    public float skip_ticks = 0;

    /// makes the mc main loop runtime
    // tg: code/controllers/master.dm:45
    public float make_runtime = FALSE;

    /// The type of the last subsystem to be fire()'d.
    // tg: code/controllers/master.dm:48
    public object last_type_processed;

    // tg: code/controllers/master.dm:50
    public datum_controller_subsystem queue_head; //!Start of queue linked list
    // tg: code/controllers/master.dm:51
    public datum_controller_subsystem queue_tail; //!End of queue linked list (used for appending to the list)
    // tg: code/controllers/master.dm:52
    public float queue_priority_count = 0; //Running total so that we don't have to loop thru the queue each run to split up the tick
    // tg: code/controllers/master.dm:53
    public float queue_priority_count_bg = 0; //Same, but for background subsystems
    // tg: code/controllers/master.dm:54
    public float map_loading = FALSE; //!Are we loading in a new map?

    // tg: code/controllers/master.dm:56
    public object current_runlevel; //!for scheduling different subsystems for different stages of the round
    // tg: code/controllers/master.dm:57
    public float sleep_offline_after_initializations = TRUE;

    /// During initialization, will be the instanced subsytem that is currently initializing.
    /// Outside of initialization, returns null.
    // tg: code/controllers/master.dm:61
    public object current_initializing_subsystem = null;

    /// The last decisecond we force dumped profiling information
    /// Used to avoid spamming profile reads since they can be expensive (string memes)
    // tg: code/controllers/master.dm:65
    public float last_profiled = 0;

    // tg: code/controllers/master.dm:67  var/static/restart_clear = 0
    public static ref float restart_clear => ref TgState.Current.datum_controller_master__restart_clear;
    // tg: code/controllers/master.dm:68  var/static/restart_timeout = 0
    public static ref float restart_timeout => ref TgState.Current.datum_controller_master__restart_timeout;
    // tg: code/controllers/master.dm:69  var/static/restart_count = 0
    public static ref float restart_count => ref TgState.Current.datum_controller_master__restart_count;

    // tg: code/controllers/master.dm:71  var/static/random_seed
    public static ref object random_seed => ref TgState.Current.datum_controller_master__random_seed;

    ///current tick limit, assigned before running a subsystem.
    ///used by CHECK_TICK as well so that the procs subsystems call can obey that SS's tick limits
    // tg: code/controllers/master.dm:75  var/static/current_ticklimit = TICK_LIMIT_RUNNING
    public static ref float current_ticklimit => ref TgState.Current.datum_controller_master__current_ticklimit;

    /// Whether the Overview UI will update as fast as possible for viewers.
    // tg: code/controllers/master.dm:78
    public float overview_fast_update = FALSE;
    /// Enables rolling usage averaging
    // tg: code/controllers/master.dm:80
    public float use_rolling_usage = FALSE;
    /// How long to run our rolling usage averaging
    // tg: code/controllers/master.dm:82
    public float rolling_usage_length = 5 * SECONDS;

    // tg: code/controllers/master.dm:84
    public override async DmTask New(params object[] __extra)
    {
        // Ensure usr is null, to prevent any potential weirdness resulting from the MC having a usr if it's manually restarted.
        usr = null;

        if (!Dm.truthy(config))
            config = Dm.@new<datum_controller_configuration>();
        // Highlander-style: there can only be one! Kill off the old and replace it with the new.

        if (!Dm.truthy(random_seed))
        {
            // tg: #ifdef UNIT_TESTS ... #else not translated (inactive in production)
            random_seed = rand(1, 1e9f);
            // DEVIATION(DV11): rand_seed is a no-op; the world RNG is seeded by the host and reseeded every tick.
            rand_seed(random_seed);
        }

        DmList _subsystems = Dm.list();
        subsystems = _subsystems;
        if (Master != this)
        {
            if (Master is datum_controller_master) //If there is an existing MC take over his stuff and delete it
            {
                Recover();
                qdel(Master);
                Master = this;
            }
            else
            {
                //Code used for first master on game boot or if existing master got deleted
                Master = this;
                // PORT(test seam): the subsystem-type filter of this world (lead decision W3 G2,
                // Infrastructure/TgState.Subsystems.cs); by default it keeps every type of the Tg assembly, which is
                // every subsystem type of the production build.
                DmList subsystem_types = TgState.Current.FilterSubsystemTypes(subtypesof(typeof(datum_controller_subsystem)));
                // sortTim reads GLOB.sortInstance (__HELPERS/sorts/helpers.dm:27) before GLOB exists (:128): a var/global,
                // null until its InitGlobal runs (the pre-GLOB storage, rules 10.6), so sortTim makes its own sorter.
                sortTim(subsystem_types, "/proc/" + nameof(GlobalProcs.cmp_subsystem_init_stage));

                //Find any abandoned subsystem from the previous master (if there was any)
                DmList existing_subsystems = Dm.list();
                // tg: for(var/global_var in global.vars) / if (istype(global.vars[global_var], ...)) existing_subsystems +=
                // global.vars[global_var]. global.vars is not translated (rules 10.7, 13): the loop runs over the values
                // of the global vars that can hold a subsystem, the SUBSYSTEM_DEF fields of TgState (lead decision W3 G3).
                foreach (object global_var in Dm.each(global_vars__subsystems()))
                {
                    if (global_var is datum_controller_subsystem)
                        Dm.append(ref existing_subsystems, global_var);
                }

                //Either init a new SS or if an existing one was found use that
                foreach (object I in Dm.each(subsystem_types))
                {
                    float ss_idx = existing_subsystems.Find(I);
                    if (Dm.truthy(ss_idx))
                        Dm.append(ref _subsystems, existing_subsystems[ss_idx]);
                    else
                        Dm.append(ref _subsystems, await Dm.new_async(I as Type));
                }
            }
        }

        if (!Dm.truthy(GLOB__var))
            await Dm.new_async<datum_controller_global_vars>();
    }

    // tg: code/controllers/master.dm:130
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        base.Destroy(force);
        // Tell qdel() to Del() this object.
        return QDEL_HINT_HARDDEL_NOW;
    }

    // tg: code/controllers/master.dm:135
    public override async DmTask Shutdown()
    {
        processing = FALSE;
        sortTim(subsystems, "/proc/" + nameof(GlobalProcs.cmp_subsystem_init));
        reverse_range(subsystems);
        foreach (datum_controller_subsystem ss in Dm.each<datum_controller_subsystem>(subsystems))
        {
            log_world(Dm.text("Shutting down [] subsystem...", ss.name));
            if (ss.slept_count > 0)
                log_world(Dm.text("Warning: Subsystem `[]` slept [] times.", ss.name, ss.slept_count));
            await ss.Shutdown();
        }
        log_world("Shutdown complete");
    }

    // tg: code/controllers/master.dm:146  ADMIN_VERB(cmd_controller_view_ui, ...): not declared, verbs are translated with
    // the player command layer (rules 7.10).
    // tg: code/controllers/master.dm:149-237  ui_status, ui_interact, ui_close, ui_data, ui_act: not declared, the tgui
    // procs of /datum are not translated (rules 13).

    // tg: code/controllers/master.dm:239  (set waitfor = FALSE; a plain method: with the tgui update deferred its body
    // cannot sleep, waiting-model.md 5.4)
    [DmPrivate]
    public virtual void check_and_perform_fast_update()
    {
        if (!Dm.truthy(overview_fast_update))
            return;

        if (Dm.truthy(check_and_perform_fast_update__already_updating))
            return;
        check_and_perform_fast_update__already_updating = TRUE;
        Dm.Deferred("code/controllers/master.dm:251", "SStgui.update_uis(src): the MC overview UI (tgui, M10+); nothing in M0 sets overview_fast_update");
        check_and_perform_fast_update__already_updating = FALSE;
    }

    // tg: code/controllers/master.dm:247  var/static/already_updating = FALSE (in check_and_perform_fast_update; rules 10.6)
    private static ref float check_and_perform_fast_update__already_updating => ref TgState.Current.datum_controller_master__check_and_perform_fast_update__already_updating;

    // tg: code/controllers/master.dm:275
    public override void Recover()
    {
        Dm.NotTranslated("code/controllers/master.dm:275", "Recover: not translated (translation-rules.md 13)");
    }

    // Please don't stuff random bullshit here,
    // Make a subsystem, give it the SS_NO_FIRE flag, and do your work in its Initialize()
    // tg: code/controllers/master.dm:323  (set waitfor = 0: the wrapper of waiting-model.md 5.4, an async method in this
    // DmTask slot; it never writes '.', so it has no box)
    [DmParams("delay", "init_sss", "tgs_prime")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        return Waiting.WaitforFalse(Initialize__body(__extra, null), null);
    }

    // The body of the set waitfor = 0 wrapper above (waiting-model.md 5.4).
    private async DmTask<object> Initialize__body(object[] __extra, DotBox<object> dot)
    {
        object delay = Dm.arg(__extra, 0);
        object init_sss = Dm.arg(__extra, 1);
        object tgs_prime = Dm.arg(__extra, 2);

        if (Dm.truthy(delay))
            await sleep(Dm.num(delay));

        if (Dm.truthy(init_sss))
        {
            // init_subtypes (__HELPERS/global_lists.dm:138) is translated with Recover, the only caller that passes init_sss
            // (master.dm:319, MC recovery, rules 13); world/New passes FALSE.
            Dm.NotTranslated("code/controllers/master.dm:330", "Initialize: init_subtypes (__HELPERS/global_lists.dm:138) is not translated yet");
        }

        init_stage_completed = 0;
        float mc_started = FALSE;

        to_chat(world, span_boldannounce(I18n.T("Initializing subsystems...")), MESSAGE_TYPE_DEBUG);

        DmList stage_sorted_subsystems = Dm.list_n(INITSTAGE_MAX);
        for (float i = 1, i__end = INITSTAGE_MAX; i <= i__end; i++)
            stage_sorted_subsystems[i] = Dm.list();

        DmList type_to_subsystem = Dm.list();
        foreach (datum_controller_subsystem subsystem in Dm.each_cast<datum_controller_subsystem>(subsystems))
            type_to_subsystem[subsystem.type] = subsystem;

        // Allows subsystems to declare other subsystems that must initialize after them.
        foreach (datum_controller_subsystem subsystem in Dm.each_cast<datum_controller_subsystem>(subsystems))
        {
            foreach (object dependent_type in Dm.each(subsystem.dependents))
            {
                if (!ispath(dependent_type, typeof(datum_controller_subsystem)))
                {
                    stack_trace(Dm.text("ERROR: MC: subsystem `[]` has an invalid dependent: `[]`. Skipping", subsystem.type, dependent_type), "code/controllers/master.dm", 349);
                    continue;
                }
                datum_controller_subsystem dependent = type_to_subsystem[dependent_type] as datum_controller_subsystem;
                Dm.combine(ref dependent.dependencies, subsystem.type);
            }
            subsystem.dependents = Dm.list();
        }

        // Constructs a reverse-dependency graph.
        foreach (datum_controller_subsystem subsystem in Dm.each_cast<datum_controller_subsystem>(subsystems))
        {
            foreach (object dependency_type in Dm.each(subsystem.dependencies))
            {
                if (!ispath(dependency_type, typeof(datum_controller_subsystem)))
                {
                    stack_trace(Dm.text("ERROR: MC: subsystem `[]` has an invalid dependency: `[]`. Skipping", subsystem.type, dependency_type), "code/controllers/master.dm", 359);
                    continue;
                }
                datum_controller_subsystem dependency = type_to_subsystem[dependency_type] as datum_controller_subsystem;
                // Not a foolproof failsafe, likely to only prevent any immediate issues if this is only triggered once.
                if (subsystem.init_stage < dependency.init_stage)
                {
                    stack_trace(Dm.text("ERROR: MC: subsystem `[]` has an init_stage before one of its dependencies (Dependency: `[]`, [] < [])! Setting init_stage to []", subsystem.type, dependency.type, subsystem.init_stage, dependency.init_stage, dependency.init_stage), "code/controllers/master.dm", 364);
                    subsystem.init_stage = dependency.init_stage;
                }
                Dm.append(ref dependency.dependents, subsystem);
            }
        }

        // Topological sorting algorithm
        DmList counts = Dm.list_n(subsystems.len);
        DmList unsorted_subsystems = Dm.list();
        float index = 1;
        foreach (datum_controller_subsystem subsystem in Dm.each_cast<datum_controller_subsystem>(subsystems))
        {
            counts[index] = length(subsystem.dependencies);
            subsystem.ordering_id = index;
            if (Dm.eq(counts[index], 0))
                Dm.append(ref unsorted_subsystems, subsystem);
            index += 1;
        }

        DmList sorted_subsystems = Dm.list();
        while (length(unsorted_subsystems) > 0)
        {
            datum_controller_subsystem sub = unsorted_subsystems[unsorted_subsystems.len] as datum_controller_subsystem;
            unsorted_subsystems.len--;
            Dm.append(ref sorted_subsystems, sub);
            foreach (datum_controller_subsystem dependent in Dm.each_cast<datum_controller_subsystem>(sub.dependents))
            {
                Dm.remove_at(counts, dependent.ordering_id, 1);
                if (Dm.eq(counts[dependent.ordering_id], 0))
                    Dm.append(ref unsorted_subsystems, dependent);
            }
        }
        // Topological sorting algorithm end

        if (length(subsystems) != length(sorted_subsystems))
        {
            DmList circular_dependency = Dm.sub(subsystems, sorted_subsystems) as DmList;
            DmList debug_msg = Dm.list();
            DmList usr_msg = Dm.list();
            foreach (datum_controller_subsystem subsystem in Dm.each_cast<datum_controller_subsystem>(circular_dependency))
                Dm.append(ref usr_msg, subsystem.name);

            DmList nodes = Dm.list(circular_dependency[1]);
            DmList loop = Dm.list();
            while (length(nodes) > 0)
            {
                datum_controller_subsystem node = nodes[nodes.len] as datum_controller_subsystem;
                nodes.len--;
                if (Dm.@in(node, loop))
                {
                    Dm.append(ref loop, node);
                    break;
                }
                Dm.append(ref loop, node);
                // dependencies holds typepaths (rules 3.2, typepaths read from a list).
                foreach (Type connected in Dm.each_cast<Type>(node.dependencies))
                    Dm.append(ref nodes, type_to_subsystem[connected]);
            }

            float loop_position = 0;
            foreach (datum_controller_subsystem node in Dm.each<datum_controller_subsystem>(loop))
            {
                if (Dm.eq(node, loop[loop.len]))
                    break;
                loop_position++;
            }
            if (loop_position != 0)
                loop.Cut(1, loop_position + 1);

            foreach (datum_controller_subsystem subsystem in Dm.each_cast<datum_controller_subsystem>(loop))
                Dm.append(ref debug_msg, Dm.text("[]", subsystem.name));

            // Can't initialize them if they have circular dependencies, there's no real failsafe here.
            stack_trace(Dm.text("ERROR: CRITICAL: MC: The following subsystems have circular dependencies: []", jointext(debug_msg, " -> ")), "code/controllers/master.dm", 421);
            to_chat(world, span_bolddanger(I18n.T("CRITICAL: Failed to initialize []", jointext(usr_msg, ", "))), MESSAGE_TYPE_DEBUG);
        }

        foreach (datum_controller_subsystem subsystem in Dm.each_cast<datum_controller_subsystem>(sorted_subsystems))
        {
            float subsystem_init_stage = subsystem.init_stage;
            if (!isnum(subsystem_init_stage) || subsystem_init_stage < 1 || subsystem_init_stage > INITSTAGE_MAX || round(subsystem_init_stage) != subsystem_init_stage)
            {
                stack_trace(Dm.text("ERROR: MC: subsystem `[]` has invalid init_stage: `[]`. Setting to `[]`", subsystem.type, subsystem_init_stage, INITSTAGE_MAX), "code/controllers/master.dm", 427);
                subsystem_init_stage = subsystem.init_stage = INITSTAGE_MAX;
            }
            Dm.append_at(stage_sorted_subsystems, subsystem_init_stage, subsystem);
        }

        // Sort subsystems by display setting for easy access.
        float evaluated_order = 1;
        sortTim(subsystems, "/proc/" + nameof(GlobalProcs.cmp_subsystem_display));
        float start_timeofday = REALTIMEOFDAY();
        for (float current_init_stage = 1, current_init_stage__end = INITSTAGE_MAX; current_init_stage <= current_init_stage__end; current_init_stage++)
        {

            // Initialize subsystems.
            foreach (datum_controller_subsystem subsystem in Dm.each<datum_controller_subsystem>(stage_sorted_subsystems[current_init_stage]))
            {
                subsystem.init_order = evaluated_order;
                evaluated_order++;
                await init_subsystem(subsystem);

                CHECK_TICK();
            }
            current_initializing_subsystem = null;
            init_stage_completed = current_init_stage;
            if (!Dm.truthy(mc_started))
            {
                mc_started = TRUE;
                if (!Dm.truthy(current_runlevel))
                    SetRunLevel(1); // Intentionally not using the defines here because the MC doesn't care about them
                // Loop.
                Master.StartProcessing(0);
            }
        }

        float time = (REALTIMEOFDAY() - start_timeofday) / 10;



        string msg = I18n.T("Initializations complete within [] second[]!", time, time == 1 ? "" : I18n.T("s"));
        to_chat(world, span_boldannounce(I18n.T("[]", msg)), MESSAGE_TYPE_DEBUG);
        log_world(msg);
        SSticker.timeLeft = SSticker.start_at;


        if (world.system_type == MS_WINDOWS && Dm.truthy(config.Get(typeof(datum_config_entry_flag_toast_notification_on_init))) && !Dm.truthy(length(GLOB.clients)))
            Dm.NotTranslated("code/controllers/master.dm:464", "Initialize: world.shelleo, the Windows init toast notification (shell, rules 13; never on the port's UNIX system_type)");

        // Set world options.
        world.change_fps(Dm.num_or_null(config.Get(typeof(datum_config_entry_number_fps))));

        if (Dm.truthy(tgs_prime))
            await world.TgsInitializationComplete();

        if (Dm.truthy(sleep_offline_after_initializations))
            world.sleep_offline = TRUE;
        await sleep(1 * world.tick_lag);

        if (Dm.truthy(sleep_offline_after_initializations) && Dm.truthy(config.Get(typeof(datum_config_entry_flag_resume_after_initializations))))
            world.sleep_offline = FALSE;
        return null;
    }

    /**
     * Initialize a given subsystem and handle the results.
     *
     * Arguments:
     * * subsystem - the subsystem to initialize.
     */
    // tg: code/controllers/master.dm:485
    public virtual async DmTask init_subsystem(datum_controller_subsystem subsystem = null)
    {
        if (Dm.truthy(Dm.bitand(subsystem.ss_flags, SS_NO_INIT)) || Dm.truthy(subsystem.initialized)) //Don't init SSs with the corresponding flag or if they already are initialized
        {
            subsystem.initialized = TRUE; // set initialized to TRUE, because the value of initialized may still be checked on SS_NO_INIT subsystems as an "is this ready" check
            return;
        }

        current_initializing_subsystem = subsystem;
        rustg_time_reset(SS_INIT_TIMER_KEY);

        // In DM a runtime in the subsystem's Initialize ends only Initialize, whose '.' (null) init_subsystem reports
        // as "initialized with errors", and the boot goes on with the next subsystem.
        object result = null;
        // PORT(measurement, M3 I1b): the host's profiler times each subsystem's init (World.Profiler, outside the world).
        IStepProfiler profiler = World.CurrentOrNull?.Profiler;
        string profileName = Dm.text("init []", subsystem.name);
        profiler?.Begin(profileName);
        try { result = await subsystem.Initialize(); }
        catch (Exception e) { World.Current.ReportRuntime(e); result = null; } // isolation boundary (translation-rules.md 9.2)
        profiler?.End(profileName);

        // Capture end time
        float time = Dm.num(rustg_time_milliseconds(SS_INIT_TIMER_KEY));
        float seconds = round(time / 1000, 0.01f);

        // Always update the blackbox tally regardless.
        SSblackbox.record_feedback("tally", "subsystem_initialize", time, subsystem.name);

        // Gave invalid return value.
        if (Dm.truthy(result) && !Dm.@in(result, init_subsystem__valid_results))
            warning(Dm.text("[] subsystem initialized, returning invalid result []. This is a bug.", subsystem.name, result));

        // just returned ..() or didn't implement Initialize() at all
        if (Dm.eq(result, SS_INIT_NONE))
            warning(Dm.text("[] subsystem does not implement Initialize() or it returns ..(). If the former is true, the SS_NO_INIT flag should be set for this subsystem.", subsystem.name));

        if (!Dm.eq(result, SS_INIT_FAILURE))
        {
            // Some form of success, implicit failure, or the SS in unused.
            subsystem.initialized = TRUE;
            if (Dm.truthy(subsystem._listen_lookup?[COMSIG_SUBSYSTEM_POST_INITIALIZE]))
                subsystem._SendSignal(COMSIG_SUBSYSTEM_POST_INITIALIZE, Dm.list(subsystem));
        }
        else
        {
            // The subsystem officially reports that it failed to init and wishes to be treated as such.
            subsystem.initialized = FALSE;
            subsystem.can_fire = FALSE;
        }

        // The rest of this proc is printing the world log and chat message.
        string message_prefix = null;

        // If true, print the chat message with boldwarning text.
        float chat_warning = FALSE;

        object sw__1 = result;
        if (Dm.eq(sw__1, SS_INIT_FAILURE))
        {
            message_prefix = I18n.T("Failed to initialize [] subsystem after", subsystem.name);
            chat_warning = TRUE;
        }
        else if (Dm.eq(sw__1, SS_INIT_SUCCESS) || Dm.eq(sw__1, SS_INIT_NO_MESSAGE))
            message_prefix = I18n.T("Initialized [] subsystem within", subsystem.name);
        else if (Dm.eq(sw__1, SS_INIT_NO_NEED))
        {
            // This SS is disabled or is otherwise shy.
            return;
        }
        else
        {
            // SS_INIT_NONE or an invalid value.
            message_prefix = I18n.T("Initialized [] subsystem with errors within", subsystem.name);
            chat_warning = TRUE;
        }

        string message = I18n.T("[] [] second[]!", message_prefix, seconds, seconds == 1 ? "" : I18n.T("s"));
        string chat_message = (Dm.truthy(chat_warning) ? span_boldwarning(message) : span_boldannounce(message));

        if (!Dm.eq(result, SS_INIT_NO_MESSAGE))
            to_chat(world, chat_message, MESSAGE_TYPE_DEBUG);
        log_world(message);
    }

    // tg: code/controllers/master.dm:486  var/static/list/valid_results (in init_subsystem; rules 10.6)
    private static ref DmList init_subsystem__valid_results => ref TgState.Current.datum_controller_master__init_subsystem__valid_results;

    // tg: code/controllers/master.dm:554
    public virtual void SetRunLevel(object new_runlevel = null)
    {
        object old_runlevel = current_runlevel;

        // tg: testing("MC: Runlevel changed from ...") expands to nothing in production (__HELPERS/logging/_logging.dm:49).
        current_runlevel = log(2, Dm.num(new_runlevel)) + 1;
        if (Dm.lt(current_runlevel, 1))
        {
            current_runlevel = old_runlevel;
            throw Dm.CRASH(Dm.text("Attempted to set invalid runlevel: []", new_runlevel), "code/controllers/master.dm", 561);
        }
    }

    // Starts the mc, and sticks around to restart it if the loop ever ends.
    // tg: code/controllers/master.dm:564  (set waitfor = 0: the wrapper of waiting-model.md 5.4)
    public virtual void StartProcessing(object delay = null)
    {
        Waiting.WaitforFalse(StartProcessing__body(delay));
    }

    // The body of the set waitfor = 0 wrapper above (waiting-model.md 5.4).
    private async DmTask StartProcessing__body(object delay)
    {
        if (Dm.truthy(delay))
            await sleep(Dm.num(delay));
        // tg: testing("Master starting processing") expands to nothing in production (__HELPERS/logging/_logging.dm:49).
        object started_stage = null;
        object rtn = -2;
        do
        {
            started_stage = init_stage_completed;
            rtn = await Loop(started_stage);
        }
        while (Dm.eq(rtn, MC_LOOP_RTN_NEWSTAGES) && processing > 0 && Dm.lt(started_stage, init_stage_completed));

        if (Dm.ge(rtn, MC_LOOP_RTN_GRACEFUL_EXIT) || processing < 0)
            return; //this was suppose to happen.
        //loop ended, restart the mc
        log_game("MC crashed or runtimed, restarting");
        message_admins("MC crashed or runtimed, restarting");
        object rtn2 = await Recreate_MC();
        if (Dm.le(rtn2, 0))
        {
            log_game(Dm.text("Failed to recreate MC (Error code: []), it's up to the failsafe now", rtn2));
            message_admins(Dm.text("Failed to recreate MC (Error code: []), it's up to the failsafe now", rtn2));
            Failsafe.defcon = 2;
        }
    }

    // Main loop.
    // tg: code/controllers/master.dm:588
    public virtual async DmTask<object> Loop(object init_stage = null)
    {
        object dot = null;
        try
        {
            dot = -1;
            //Prep the loop (most of this is because we want MC restarts to reset as much state as we can, and because
            // local vars rock

            //all this shit is here so that flag edits can be refreshed by restarting the MC. (and for speed)
            DmList tickersubsystems = Dm.list();
            DmList runlevel_sorted_subsystems = Dm.list(Dm.list()); //ensure we always have at least one runlevel
            float timer = world.time;
            foreach (object thing in Dm.each(subsystems))
            {
                datum_controller_subsystem SS = thing as datum_controller_subsystem;
                if (Dm.truthy(Dm.bitand(SS.ss_flags, SS_NO_FIRE)))
                    continue;
                if (Dm.gt(SS.init_stage, init_stage))
                    continue;
                SS.queued_time = 0;
                SS.queue_next = null;
                SS.queue_prev = null;
                SS.state = SS_IDLE;
                if (Dm.bitand(SS.ss_flags, Dm.bitor(SS_TICKER, SS_BACKGROUND)) == SS_TICKER)
                {
                    Dm.append(ref tickersubsystems, SS);
                    // Timer subsystems aren't allowed to bunch up, so we offset them a bit
                    timer += TICKS2DS(rand(0, 1));
                    SS.next_fire = timer;
                    continue;
                }

                // Now, we have to set starting next_fires for all our new non ticker kids
                if (Dm.eq(SS.init_stage, Dm.sub(init_stage, 1)) && Dm.truthy(Dm.bitand(SS.runlevels, current_runlevel)))
                {
                    // Give em a random offset so things don't clump up too bad
                    float delay = SS.wait;
                    if (Dm.truthy(Dm.bitand(SS.ss_flags, SS_TICKER)))
                        delay = TICKS2DS(delay);
                    // Gotta convert to ticks cause rand needs integers
                    SS.next_fire = world.time + TICKS2DS(rand(0, DS2TICKS(min(delay, 2 * SECONDS))));
                }

                float ss_runlevels = SS.runlevels;
                float added_to_any = FALSE;
                for (float I = 1, I__end = GLOB.bitflags.len; I <= I__end; I++)
                {
                    if (Dm.truthy(Dm.bitand(ss_runlevels, GLOB.bitflags[I])))
                    {
                        while (runlevel_sorted_subsystems.len < I)
                            Dm.append(ref runlevel_sorted_subsystems, Dm.list(Dm.list()));
                        Dm.append_at(runlevel_sorted_subsystems, I, SS);
                        added_to_any = TRUE;
                    }
                }
                if (!Dm.truthy(added_to_any))
                    warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("[] subsystem is not SS_NO_FIRE but also does not have any runlevels set!", SS.name), "code/controllers/master.dm", 632, this, usr));
            }

            queue_head = null;
            queue_tail = null;
            //these sort by lower priorities first to reduce the number of loops needed to add subsequent SS's to the queue
            //(higher subsystems will be sooner in the queue, adding them later in the loop means we don't have to loop thru them next queue add)
            sortTim(tickersubsystems, "/proc/" + nameof(GlobalProcs.cmp_subsystem_priority));
            foreach (object I__it in Dm.each(runlevel_sorted_subsystems))
            {
                object I = I__it;
                sortTim(I as DmList, "/proc/" + nameof(GlobalProcs.cmp_subsystem_priority));
                Dm.append(ref I, tickersubsystems);
            }

            object cached_runlevel = current_runlevel;
            DmList current_runlevel_subsystems = runlevel_sorted_subsystems[cached_runlevel] as DmList;

            init_timeofday = REALTIMEOFDAY();
            init_time = world.time;

            iteration = 1;
            float error_level = 0;
            float sleep_delta = 1;
            DmList subsystems_to_check = null;

            //setup the stack overflow detector
            stack_end_detector = Dm.@new<datum_stack_end_detector>();
            datum_stack_canary canary = stack_end_detector.prime_canary() as datum_stack_canary;
            canary.use_variable();
            //the actual loop.
            while (true)
            {
                // PORT: BYOND keeps a proc's vars alive until the proc returns, which is what the canary relies on
                // (stack_end_detector.dm). A C# async method keeps a local across an await only if it is read after it,
                // so without this read the canary would be collected while Loop still runs (in optimized builds), and
                // the world state hash would depend on the build (docs/ai/specs/object-model.md section 8).
                GC.KeepAlive(canary);
                object newdrift = Dm.div(Dm.sub(Dm.sub(REALTIMEOFDAY(), init_timeofday), Dm.sub(world.time, init_time)), world.tick_lag);
                tickdrift = max(0, MC_AVERAGE_FAST(tickdrift, Dm.num(newdrift)));
                float starting_tick_usage = TICK_USAGE();

                if (Dm.ge(Dm.sub(newdrift, olddrift), config.Get(typeof(datum_config_entry_number_drift_dump_threshold))))
                    AttemptProfileDump(config.Get(typeof(datum_config_entry_number_drift_profile_delay)));
                olddrift = Dm.num(newdrift);

                if (!Dm.eq(init_stage, init_stage_completed))
                    return MC_LOOP_RTN_NEWSTAGES;
                if (processing <= 0)
                {
                    current_ticklimit = TICK_LIMIT_RUNNING();
                    await sleep(1 * SECONDS);
                    continue;
                }

                //Anti-tick-contention heuristics:
                if (Dm.eq(init_stage, INITSTAGE_MAX))
                {
                    //if there are multiple sleeping procs running before us hogging the cpu, we have to run later.
                    // (because sleeps are processed in the order received, longer sleeps are more likely to run first)
                    if (starting_tick_usage > TICK_LIMIT_MC) //if there isn't enough time to bother doing anything this tick, sleep a bit.
                    {
                        sleep_delta *= 2;
                        current_ticklimit = TICK_LIMIT_RUNNING() * 0.5f;
                        await sleep(world.tick_lag * (processing * sleep_delta));
                        continue;
                    }

                    //Byond resumed us late. assume it might have to do the same next tick
                    if (Dm.lt(Dm.add(last_run, CEILING(world.tick_lag * (processing * sleep_delta), world.tick_lag)), world.time))
                        sleep_delta += 1;

                    sleep_delta = MC_AVERAGE_FAST(sleep_delta, 1); //decay sleep_delta

                    if (starting_tick_usage > (TICK_LIMIT_MC * 0.75f)) //we ran 3/4 of the way into the tick
                        sleep_delta += 1;
                }
                else
                    sleep_delta = 1;

                //debug
                if (Dm.truthy(make_runtime))
                {
                    datum_controller_subsystem SS = null;
                    SS.can_fire = 0;
                }

                if (!Dm.truthy(Failsafe) || (Failsafe.processing_interval > 0 && (Failsafe.lasttick + (Failsafe.processing_interval * 5)) < world.time))
                    Dm.@new<datum_controller_failsafe>(); // (re)Start the failsafe.

                //now do the actual stuff
                if (!Dm.truthy(skip_ticks))
                {
                    object checking_runlevel = current_runlevel;
                    if (!Dm.eq(cached_runlevel, checking_runlevel))
                    {
                        //resechedule subsystems
                        DmList old_subsystems = current_runlevel_subsystems;
                        cached_runlevel = checking_runlevel;
                        current_runlevel_subsystems = runlevel_sorted_subsystems[cached_runlevel] as DmList;

                        //now we'll go through all the subsystems we want to offset and give them a next_fire
                        foreach (datum_controller_subsystem SS in Dm.each_cast<datum_controller_subsystem>(current_runlevel_subsystems))
                        {
                            //we only want to offset it if it's new and also behind
                            if (SS.next_fire > world.time || Dm.@in(SS, old_subsystems))
                                continue;
                            // If they're new, give em a random offset so things don't clump up too bad
                            float delay = SS.wait;
                            if (Dm.truthy(Dm.bitand(SS.ss_flags, SS_TICKER)))
                                delay = TICKS2DS(delay);
                            SS.next_fire = world.time + TICKS2DS(rand(0, DS2TICKS(min(delay, 2 * SECONDS))));
                        }
                    }
                    subsystems_to_check = current_runlevel_subsystems;
                }
                else
                    subsystems_to_check = tickersubsystems;

                if (Dm.le(CheckQueue(subsystems_to_check), 0)) //error processing queue
                {
                    stack_trace(Dm.text("MC: CheckQueue failed. Current error_level is []", round(error_level, 0.25f)), "code/controllers/master.dm", 729);
                    if (!Dm.truthy(SoftReset(tickersubsystems, runlevel_sorted_subsystems)))
                    {
                        error_level++;
                        throw Dm.CRASH("MC: SoftReset() failed, exiting loop()", "code/controllers/master.dm", 732);
                    }

                    if (error_level < 2) //except for the first strike, stop incrmenting our iteration so failsafe enters defcon
                        iteration++;
                    else
                        cached_runlevel = null; //3 strikes, Lets reset the runlevel lists
                    current_ticklimit = TICK_LIMIT_RUNNING();
                    await sleep((1 * SECONDS) * error_level);
                    error_level++;
                    continue;
                }

                if (Dm.truthy(queue_head))
                {
                    if (Dm.le(RunQueue(), 0)) //error running queue
                    {
                        stack_trace(Dm.text("MC: RunQueue failed. Current error_level is []", round(error_level, 0.25f)), "code/controllers/master.dm", 745);
                        if (error_level > 1) //skip the first error,
                        {
                            if (!Dm.truthy(SoftReset(tickersubsystems, runlevel_sorted_subsystems)))
                            {
                                error_level++;
                                throw Dm.CRASH("MC: SoftReset() failed, exiting loop()", "code/controllers/master.dm", 749);
                            }

                            if (error_level <= 2) //after 3 strikes stop incrmenting our iteration so failsafe enters defcon
                                iteration++;
                            else
                                cached_runlevel = null; //3 strikes, Lets also reset the runlevel lists
                            current_ticklimit = TICK_LIMIT_RUNNING();
                            await sleep((1 * SECONDS) * error_level);
                            error_level++;
                            continue;
                        }
                        error_level++;
                    }
                }
                if (error_level > 0)
                    error_level = max(MC_AVERAGE_SLOW(error_level - 1, error_level), 0);
                if (!Dm.truthy(queue_head)) //reset the counts if the queue is empty, in the off chance they get out of sync
                {
                    queue_priority_count = 0;
                    queue_priority_count_bg = 0;
                }

                iteration++;
                last_run = world.time;
                if (Dm.truthy(skip_ticks))
                    skip_ticks--;
                this.sleep_delta = MC_AVERAGE_FAST(this.sleep_delta, sleep_delta);

                // Force any verbs into overtime, to test how they perfrom under load
                // For local ONLY
                // tg: #ifdef VERB_STRESS_TEST ... #endif not translated (inactive in production)

                if (!Dm.eq(init_stage, INITSTAGE_MAX))
                    current_ticklimit = TICK_LIMIT_RUNNING() * 2;
                else
                {
                    current_ticklimit = TICK_LIMIT_RUNNING();
                    if (processing * sleep_delta <= world.tick_lag)
                        current_ticklimit -= (TICK_LIMIT_RUNNING() * 0.25f); //reserve the tail 1/4 of the next tick for the mc if we plan on running next tick
                }

                check_and_perform_fast_update();
                await sleep(world.tick_lag * (processing * sleep_delta));
            }
        }
        // DM: Loop sets '.' to -1 first, and a runtime ends only Loop, so StartProcessing sees -1 ("MC crashed or runtimed,
        // restarting", master.dm:579) instead of an unwound MC (rules 9.3; site reported to the lead).
        catch (Exception e) { World.Current.ReportRuntime(e); return dot; } // DM: a runtime ends only this proc and returns '.'
    }

    // This is what decides if something should run.
    // tg: code/controllers/master.dm:796
    public virtual object CheckQueue(DmList subsystemstocheck = null)
    {
        object dot = null;
        try
        {
            dot = 0; //so the mc knows if we runtimed

            //we create our variables outside of the loops to save on overhead
            datum_controller_subsystem SS = null;
            float SS_flags = 0;

            foreach (object thing in Dm.each(subsystemstocheck))
            {
                if (!Dm.truthy(thing))
                    Dm.remove(ref subsystemstocheck, thing);
                SS = thing as datum_controller_subsystem;
                if (!Dm.eq(SS.state, SS_IDLE))
                    continue;
                if (SS.can_fire <= 0)
                    continue;
                if (SS.next_fire > world.time)
                    continue;
                SS_flags = SS.ss_flags;
                if (Dm.truthy(Dm.bitand(SS_flags, SS_NO_FIRE)))
                {
                    Dm.remove(ref subsystemstocheck, SS);
                    continue;
                }
                // If we're keeping timing and running behind,
                // fire at most 25% faster then normal to try and make up the gap without spamming
                if (Dm.bitand(SS_flags, Dm.bitor(SS_TICKER, SS_KEEP_TIMING)) == SS_KEEP_TIMING && SS.last_fire + (SS.wait * 0.75f) > world.time)
                    continue;
                if (SS.postponed_fires >= 1)
                {
                    SS.postponed_fires--;
                    SS.update_nextfire();
                    continue;
                }
                SS.enqueue();
            }
            dot = 1;
            return dot;
        }
        catch (Exception e) { World.Current.ReportRuntime(e); return dot; } // DM: a runtime ends only this proc and returns '.'
    }


    /// RunQueue - Run thru the queue of subsystems to run, running them while balancing out their allocated tick precentage
    /// Returns 0 if runtimed, a negitive number for logic errors, and a positive number if the operation completed without errors
    // tg: code/controllers/master.dm:831
    public virtual object RunQueue()
    {
        object dot = null;
        try
        {
            dot = 0;
            datum_controller_subsystem queue_node = null;
            float queue_node_flags = 0;
            object queue_node_priority = null;
            float queue_node_paused = 0;

            float current_tick_budget = 0;
            object tick_precentage = null;
            float tick_remaining = 0;
            float ran = TRUE; //this is right
            float bg_calc = 0; //have we swtiched current_tick_budget to background mode yet?
            float tick_usage = 0;

            //keep running while we have stuff to run and we haven't gone over a tick
            // this is so subsystems paused eariler can use tick time that later subsystems never used
            while (Dm.truthy(ran) && Dm.truthy(queue_head) && TICK_USAGE() < TICK_LIMIT_MC)
            {
                ran = FALSE;
                bg_calc = FALSE;
                current_tick_budget = queue_priority_count;
                queue_node = queue_head;
                while (Dm.truthy(queue_node))
                {
                    if (Dm.truthy(ran) && TICK_USAGE() > TICK_LIMIT_RUNNING())
                        break;
                    queue_node_flags = queue_node.ss_flags;
                    queue_node_priority = queue_node.queued_priority;

                    if (!Dm.truthy(Dm.bitand(queue_node_flags, SS_TICKER)) && Dm.truthy(skip_ticks))
                    {
                        queue_node = queue_node.queue_next;
                        continue;
                    }

                    if (Dm.truthy(Dm.bitand(queue_node_flags, SS_BACKGROUND)))
                    {
                        if (!Dm.truthy(bg_calc))
                        {
                            current_tick_budget = queue_priority_count_bg;
                            bg_calc = TRUE;
                        }
                    }
                    else if (Dm.truthy(bg_calc))
                    {
                        //error state, do sane fallback behavior
                        if (Dm.eq(dot, 0))
                            log_world(Dm.text("MC: Queue logic failure, non-background subsystem queued to run after a background subsystem: [] queue_prev:[]", queue_node, queue_node.queue_prev));
                        dot = -1;
                        current_tick_budget = queue_priority_count; //this won't even be right, but is the best we have.
                        bg_calc = FALSE;
                    }


                    tick_remaining = TICK_LIMIT_RUNNING() - TICK_USAGE();

                    if (Dm.ge(queue_node_priority, 0) && current_tick_budget > 0 && Dm.ge(current_tick_budget, queue_node_priority))
                    {
                        //Give the subsystem a precentage of the remaining tick based on the remaining priority
                        tick_precentage = Dm.mul(tick_remaining, Dm.div(queue_node_priority, current_tick_budget));
                    }
                    else
                    {
                        //error state
                        if (Dm.eq(dot, 0))
                            log_world(Dm.text("MC: tick_budget sync error. []", json_encode(Dm.list(current_tick_budget, queue_priority_count, queue_priority_count_bg, bg_calc, queue_node, queue_node_priority))));
                        dot = -1;
                        tick_precentage = tick_remaining; //just because we lost track of priority calculations doesn't mean we can't try to finish off the run, if the error state persists, we don't want to stop ticks from happening
                    }

                    tick_precentage = max(Dm.mul(tick_precentage, 0.5f), Dm.sub(tick_precentage, queue_node.tick_overrun));

                    current_ticklimit = round(Dm.num(Dm.add(TICK_USAGE(), tick_precentage)));

                    ran = TRUE;

                    queue_node_paused = Dm.num(Dm.eq(queue_node.state, SS_PAUSED) || Dm.eq(queue_node.state, SS_PAUSING));
                    last_type_processed = queue_node;

                    queue_node.state = SS_RUNNING;

                    if (Dm.truthy(queue_node.profiler_focused))
                        Dm.NotTranslated("code/controllers/master.dm:899", "RunQueue: world.Profile(PROFILE_START), profiling is not translated (translation-rules.md 13)");

                    tick_usage = TICK_USAGE();
                    // PORT(measurement, M3 I1b): tg's cost needs world.tick_usage, which is 0 in the port (DV3); the host's
                    // profiler times the fire instead (World.Profiler, outside the world).
                    IStepProfiler profiler = World.CurrentOrNull?.Profiler;
                    string profileName = profiler != null ? queue_node.name as string ?? queue_node.GetType().Name : null;
                    profiler?.Begin(profileName);
                    object state;
                    try { state = queue_node.ignite(queue_node_paused); }
                    finally { profiler?.End(profileName); }
                    tick_usage = TICK_USAGE() - tick_usage;

                    if (Dm.truthy(use_rolling_usage))
                    {
                        queue_node.prune_rolling_usage();
                        // Rolling usage is an unrolled list that we know the order off
                        // OPTIMIZATION POSTING
                        Dm.append(ref queue_node.rolling_usage, Dm.list(DS2TICKS(world.time), tick_usage));
                    }

                    if (Dm.truthy(queue_node.profiler_focused))
                        Dm.NotTranslated("code/controllers/master.dm:912", "RunQueue: world.Profile(PROFILE_STOP), profiling is not translated (translation-rules.md 13)");

                    if (Dm.eq(state, SS_RUNNING))
                        state = SS_IDLE;
                    current_tick_budget -= Dm.num(queue_node_priority);


                    if (tick_usage < 0)
                        tick_usage = 0;
                    queue_node.tick_overrun = max(0, MC_AVG_FAST_UP_SLOW_DOWN(queue_node.tick_overrun, Dm.num(Dm.sub(tick_usage, tick_precentage))));
                    queue_node.state = Dm.num_or_null(state);

                    if (Dm.eq(state, SS_PAUSED))
                    {
                        queue_node.paused_ticks++;
                        Dm.append(ref queue_node.paused_tick_usage, tick_usage);
                        queue_node = queue_node.queue_next;
                        continue;
                    }

                    queue_node.ticks = MC_AVERAGE(queue_node.ticks, queue_node.paused_ticks);
                    tick_usage += Dm.num(queue_node.paused_tick_usage);

                    queue_node.tick_usage = MC_AVERAGE_FAST(queue_node.tick_usage, tick_usage);

                    queue_node.cost = MC_AVERAGE_FAST(queue_node.cost, TICK_DELTA_TO_MS(tick_usage));
                    queue_node.paused_ticks = 0;
                    queue_node.paused_tick_usage = 0;

                    if (Dm.truthy(bg_calc)) //update our running total
                        queue_priority_count_bg -= Dm.num(queue_node_priority);
                    else
                        queue_priority_count -= Dm.num(queue_node_priority);

                    queue_node.last_fire = world.time;
                    queue_node.times_fired++;

                    queue_node.update_nextfire();

                    queue_node.queued_time = 0;

                    //remove from queue
                    queue_node.dequeue();

                    queue_node = queue_node.queue_next;
                }
            }

            if (Dm.eq(dot, 0))
                dot = 1;
            return dot;
        }
        catch (Exception e) { World.Current.ReportRuntime(e); return dot; } // DM: a runtime ends only this proc and returns '.'
    }

    //resets the queue, and all subsystems, while filtering out the subsystem lists
    // called if any mc's queue procs runtime or exit improperly.
    // tg: code/controllers/master.dm:961
    public virtual object SoftReset(DmList ticker_SS = null, DmList runlevel_SS = null)
    {
        object dot = null;
        dot = 0;
        stack_trace("MC: SoftReset called, resetting MC queue state.", "code/controllers/master.dm", 963);

        if (!(subsystems is DmList) || !(ticker_SS is DmList) || !(runlevel_SS is DmList))
        {
            log_world(Dm.text("MC: SoftReset: Bad list contents: '[]' '[]' '[]'", subsystems, ticker_SS, runlevel_SS));
            return dot;
        }
        object subsystemstocheck = Dm.bitor(subsystems, ticker_SS);
        foreach (object I in Dm.each(runlevel_SS))
            Dm.combine(ref subsystemstocheck, I);

        foreach (object thing in Dm.each(subsystemstocheck))
        {
            // object, not the declared subsystem type: the bad-entry branch below reads a value that is not a subsystem
            // (rules 3.4).
            object SS = thing;
            if (!Dm.truthy(SS) || !(SS is datum_controller_subsystem))
            {
                //list(SS) is so if a list makes it in the subsystem list, we remove the list, not the contents
                Dm.remove(ref subsystems, Dm.list(SS));
                Dm.remove(ref ticker_SS, Dm.list(SS));
                foreach (object I__it in Dm.each(runlevel_SS))
                {
                    object I = I__it;
                    Dm.remove(ref I, Dm.list(SS));
                }
                log_world(Dm.text("MC: SoftReset: Found bad entry in subsystem list, '[]'", SS));
                continue;
            }
            if (Dm.truthy(((datum_controller_subsystem)SS).queue_next) && !(((datum_controller_subsystem)SS).queue_next is datum_controller_subsystem))
                log_world(Dm.text("MC: SoftReset: Found bad data in subsystem queue, queue_next = '[]'", ((datum_controller_subsystem)SS).queue_next));
            ((datum_controller_subsystem)SS).queue_next = null;
            if (Dm.truthy(((datum_controller_subsystem)SS).queue_prev) && !(((datum_controller_subsystem)SS).queue_prev is datum_controller_subsystem))
                log_world(Dm.text("MC: SoftReset: Found bad data in subsystem queue, queue_prev = '[]'", ((datum_controller_subsystem)SS).queue_prev));
            ((datum_controller_subsystem)SS).queue_prev = null;
            ((datum_controller_subsystem)SS).queued_priority = 0;
            ((datum_controller_subsystem)SS).queued_time = 0;
            ((datum_controller_subsystem)SS).state = SS_IDLE;
        }
        if (Dm.truthy(queue_head) && !(queue_head is datum_controller_subsystem))
            log_world(Dm.text("MC: SoftReset: Found bad data in subsystem queue, queue_head = '[]'", queue_head));
        queue_head = null;
        if (Dm.truthy(queue_tail) && !(queue_tail is datum_controller_subsystem))
            log_world(Dm.text("MC: SoftReset: Found bad data in subsystem queue, queue_tail = '[]'", queue_tail));
        queue_tail = null;
        queue_priority_count = 0;
        queue_priority_count_bg = 0;
        log_world("MC: SoftReset: Finished.");
        dot = 1;
        return dot;
    }

    /// Warns us that the end of tick byond map_update will be laggier then normal, so that we can just skip running subsystems this tick.
    // tg: code/controllers/master.dm:1003
    public virtual void laggy_byond_map_update_incoming()
    {
        if (!Dm.truthy(skip_ticks))
            skip_ticks = 1;
    }


    // tg: code/controllers/master.dm:1008
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/master.dm:1008", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }


    // tg: code/controllers/master.dm:1013
    public override async DmTask StartLoadingMap()
    {
        //disallow more than one map to load at once, multithreading it will just cause race conditions
        while (Dm.truthy(map_loading))
            await stoplag();
        foreach (object S in Dm.each(subsystems))
        {
            datum_controller_subsystem SS = S as datum_controller_subsystem;
            await SS.StartLoadingMap();
        }
        map_loading = TRUE;
    }

    // tg: code/controllers/master.dm:1022
    [DmParams("bounds")]
    public override void StopLoadingMap(params object[] __extra)
    {
        object bounds = Dm.arg(__extra, 0);
        map_loading = FALSE;
        foreach (object S in Dm.each(subsystems))
        {
            datum_controller_subsystem SS = S as datum_controller_subsystem;
            SS.StopLoadingMap();
        }
    }


    // tg: code/controllers/master.dm:1029
    public virtual void UpdateTickRate()
    {
        if (!Dm.truthy(processing))
            return;
        float client_count = length(GLOB.clients);
        if (Dm.lt(client_count, config.Get(typeof(datum_config_entry_number_mc_tick_rate_disable_high_pop_mc_mode_amount))))
            processing = Dm.num(config.Get(typeof(datum_config_entry_number_mc_tick_rate_base_mc_tick_rate)));
        else if (Dm.gt(client_count, config.Get(typeof(datum_config_entry_number_mc_tick_rate_high_pop_mc_mode_amount))))
            processing = Dm.num(config.Get(typeof(datum_config_entry_number_mc_tick_rate_high_pop_mc_tick_rate)));
    }

    // tg: code/controllers/master.dm:1038
    public virtual async DmTask OnConfigLoad()
    {
        foreach (object thing in Dm.each(subsystems))
        {
            datum_controller_subsystem SS = thing as datum_controller_subsystem;
            await SS.OnConfigLoad();
        }
    }

    /// Attempts to dump our current profile info into a file, triggered if the MC thinks shit is going down
    /// Accepts a delay in deciseconds of how long ago our last dump can be, this saves causing performance problems ourselves
    // tg: code/controllers/master.dm:1045
    public virtual object AttemptProfileDump(object delay = null)
    {
        Dm.NotTranslated("code/controllers/master.dm:1045", "AttemptProfileDump: profiling is not translated (translation-rules.md 13); unreachable while the drift is 0 (DV3)");
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/master.dm:29  var/static/init_stage_completed
    internal object datum_controller_master__init_stage_completed;
    // tg: code/controllers/master.dm:67-69  var/static/restart_clear, restart_timeout, restart_count
    internal float datum_controller_master__restart_clear = 0;
    internal float datum_controller_master__restart_timeout = 0;
    internal float datum_controller_master__restart_count = 0;
    // tg: code/controllers/master.dm:71  var/static/random_seed
    internal object datum_controller_master__random_seed;
    // tg: code/controllers/master.dm:75  var/static/current_ticklimit = TICK_LIMIT_RUNNING (__DEFINES/_tick.dm:7, 98 with
    // the virtual map_cpu of 0, DV3)
    internal float datum_controller_master__current_ticklimit = TICK_LIMIT_RUNNING();
    // tg: code/controllers/master.dm:247  var/static/already_updating = FALSE (in check_and_perform_fast_update)
    internal float datum_controller_master__check_and_perform_fast_update__already_updating = FALSE;
    // tg: code/controllers/master.dm:486-492  var/static/list/valid_results (in init_subsystem)
    internal DmList datum_controller_master__init_subsystem__valid_results = Dm.list(
        SS_INIT_FAILURE,
        SS_INIT_NONE,
        SS_INIT_SUCCESS,
        SS_INIT_NO_NEED,
        SS_INIT_NO_MESSAGE
    );
}

public static partial class GlobalProcs
{
    // Returns 1 if we created a new mc, 0 if we couldn't due to a recent restart,
    // -1 if we encountered a runtime trying to recreate it
    // tg: code/controllers/master.dm:256
    public static async DmTask<object> Recreate_MC()
    {
        object dot = null;
        try
        {
            dot = -1; //so if we runtime, things know we failed
            if (world.time < datum_controller_master.restart_timeout)
                return 0;
            if (world.time < datum_controller_master.restart_clear)
                datum_controller_master.restart_count *= 0.5f;

            float delay = 50 * ++datum_controller_master.restart_count;
            datum_controller_master.restart_timeout = world.time + delay;
            datum_controller_master.restart_clear = world.time + (delay * 2);
            if (Dm.truthy(Master)) //Can only do this if master hasn't been deleted
                Master.processing = FALSE; //stop ticking this one
            try
            {
                await Dm.new_async<datum_controller_master>();
            }
            catch (Exception)
            {
                return -1;
            }
            return 1;
        }
        catch (Exception e) { World.Current.ReportRuntime(e); return dot; } // DM: a runtime ends only this proc and returns '.'
    }
}
