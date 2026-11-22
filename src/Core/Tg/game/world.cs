// Translated from tgstation (snapshot 1d99bd7f): code/game/world.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The /world procs are DmWorld extension methods (rules 1.5). Translated: Genesis (without byond-tracy, BYOND's profiler and
// the debugger, rules 13), New, InitTgs (a no-op seam: TGS is not ported, rules 13), ConfigLoaded and SetupLogs (with deferred
// leaves for the admin, poll, RETA, changelog and revision parts, M9-M10), RunUnattendedFunctions, get_world_state_for_logging,
// Tick, increase_max_x, increase_max_y, incrementMaxZ, change_fps, change_tick_lag (both under DV6) and on_tickrate_change.
// Not translated (M10 round loop, admin and hub tooling, or test-run only): HandleTestRun, queue_performance_tests,
// run_performance_tests, Topic, AnnouncePR, FinishTestRun, check_hard_reboot, Reboot, Del, update_status,
// update_hub_visibility and Profile (profiling, rules 13).

public partial class datum_controller_global_vars
{
    // tg: code/game/world.dm:9  GLOBAL_VAR(restart_counter)
    public object restart_counter;

    // tg: code/game/world.dm:9  (GLOBAL_UNMANAGED(restart_counter), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalrestart_counter()
    {
        return;
    }

    /// The world.time we last ran maptick, used for stupid reasons
    // tg: code/game/world.dm:273  GLOBAL_VAR_INIT(last_maptick_time, 0)
    public float last_maptick_time;

    // tg: code/game/world.dm:273  (GLOBAL_MANAGED(last_maptick_time, 0), __DEFINES/__globals.dm:5)
    public virtual void InitGloballast_maptick_time()
    {
        last_maptick_time = 0;
        Dm.append(ref gvars_datum_init_order, "last_maptick_time");
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/world.dm:1  #define RESTART_COUNTER_PATH "data/round_counter.txt" (file-local, #undef at :529)
    private const string RESTART_COUNTER_PATH = "data/round_counter.txt";
    // tg: code/game/world.dm:3  #define USE_TRACY_PARAMETER "tracy" (file-local, #undef at :528): only read by the byond-tracy
    // block of Genesis, which is not ported.
    /// Force the log directory to be something specific in the data/logs folder
    // tg: code/game/world.dm:5  #define OVERRIDE_LOG_DIRECTORY_PARAMETER "log-directory" (file-local, #undef at :527)
    private const string OVERRIDE_LOG_DIRECTORY_PARAMETER = "log-directory";
    /// Prevent the master controller from starting automatically
    // tg: code/game/world.dm:7  #define NO_INIT_PARAMETER "no-init" (file-local, #undef at :526)
    private const string NO_INIT_PARAMETER = "no-init";

    /**
     * WORLD INITIALIZATION
     * THIS IS THE INIT ORDER:
     *
     * BYOND =>
     * - (secret init native) =>
     *   - world.Genesis() =>
     *     - world.init_byond_tracy()
     *     - (Start native profiling)
     *     - world.init_debugger()
     *     - Master =>
     *       - config *unloaded
     *       - (all subsystems) PreInit()
     *       - GLOB =>
     *         - make_datum_reference_lists()
     *   - (/static variable inits, reverse declaration order)
     * - (all pre-mapped atoms) /atom/New()
     * - world.New() =>
     *   - config.Load()
     *   - world.InitTgs() =>
     *     - TgsNew() *may sleep
     *     - GLOB.rev_data.load_tgs_info() *may sleep
     *   - world.ConfigLoaded() =>
     *     - SSdbcore.InitializeRound()
     *     - world.SetupLogs()
     *     - load_admins()
     *     - ...
     *   - Master.Initialize() =>
     *     - (all subsystems) Initialize()
     *     - Master.StartProcessing() =>
     *       - Master.Loop() =>
     *         - Failsafe
     *   - world.RunUnattendedFunctions()
     *
     * Now listen up because I want to make something clear:
     * If something is not in this list it should almost definitely be handled by a subsystem Initialize()ing
     * If whatever it is that needs doing doesn't fit in a subsystem you probably aren't trying hard enough tbhfam
     *
     * GOT IT MEMORIZED?
     * - Dominion/Cyberboss
     *
     * Where to put init shit quick guide:
     * If you need it to happen before the mc is created: world/Genesis.
     * If you need it to happen last: world/New(),
     * Otherwise, in a subsystem preinit or init. Subsystems can set an init priority.
     */
    // The port runs this order in TgWorld.Boot (src/Core/Tg/Infrastructure/TgWorld.cs).

    /**
     * THIS !!!SINGLE!!! PROC IS WHERE ANY FORM OF INIITIALIZATION THAT CAN'T BE PERFORMED IN SUBSYSTEMS OR WORLD/NEW IS DONE
     * NOWHERE THE FUCK ELSE
     * I DON'T CARE HOW MANY LAYERS OF DEBUG/PROFILE/TRACE WE HAVE, YOU JUST HAVE TO DEAL WITH THIS PROC EXISTING
     * I'M NOT EVEN GOING TO TELL YOU WHERE IT'S CALLED FROM BECAUSE I'M DECLARING THAT FORBIDDEN KNOWLEDGE
     * SO HELP ME GOD IF I FIND ABSTRACTION LAYERS OVER THIS!
     */
    // tg: code/game/world.dm:65
    public static async DmTask Genesis(this DmWorld world, float? tracy_initialized = null)
    {
        tracy_initialized ??= FALSE;

        // tg: code/game/world.dm:68-85  if(!tracy_initialized) Tracy = new ... Genesis(tracy_initialized = TRUE): PORT: byond-tracy, a
        // native profiler, is not ported (translation-rules.md 13), so Genesis never restarts itself with it.

        // tg: code/game/world.dm:87-88  Profile(PROFILE_RESTART) and Profile(PROFILE_RESTART, type = "sendmaps"): PORT: BYOND's
        // profiler is not ported (translation-rules.md 13).

        // Write everything to this log file until we get to SetupLogs() later
        world._initialize_log_files(Dm.text("data/logs/config_error.[].log", GUID()));

        // Init the debugger first so we can debug Master
        // tg: code/game/world.dm:94  Debugger = new: PORT: the auxtools debugger is not ported (translation-rules.md 13).

        // Create the logger
        logger = Dm.@new<datum_log_holder>();

        // THAT'S IT, WE'RE DONE, THE. FUCKING. END.
        // RULE-GAP: C# cannot hold the ref of an accessor (Master, rules 10.6) across an await (CS8178), so the awaited new
        // goes to a temp first; DM also creates the object before it assigns the global.
        datum_controller_master t__1 = await Dm.new_async<datum_controller_master>();
        Master = t__1;
    }

    /**
     * World creation
     *
     * Here is where a round itself is actually begun and setup.
     * * db connection setup
     * * config loaded from files
     * * loads admins
     * * Sets up the dynamic menu system
     * * and most importantly, calls initialize on the master subsystem, starting the game loop that causes the rest of the game to begin processing and setting up
     *
     *
     * Nothing happens until something moves. ~Albert Einstein
     *
     * For clarity, this proc gets triggered later in the initialization pipeline, it is not the first thing to happen, as it might seem.
     *
     * Initialization Pipeline:
     * Global vars are new()'ed, (including config, glob, and the master controller will also new and preinit all subsystems when it gets new()ed)
     * Compiled in maps are loaded (mainly centcom). all areas/turfs/objs/mobs(ATOMs) in these maps will be new()ed
     * world/New() (You are here)
     * Once world/New() returns, client's can connect.
     * 1 second sleep
     * Master Controller initialization.
     * Subsystem initialization.
     * Non-compiled-in maps are maploaded, all atoms are new()ed
     * All atoms in both compiled and uncompiled maps are initialized()
     */
    // tg: code/game/world.dm:128
    public static async DmTask New(this DmWorld world)
    {
        log_world(Dm.text("World loaded at []!", server_timestamp()));

        // First possible sleep()
        await world.InitTgs();

        await config.Load(world.@params[OVERRIDE_CONFIG_DIRECTORY_PARAMETER]);

        await world.ConfigLoaded();

        if (Dm.@in(NO_INIT_PARAMETER, world.@params))
            return;

        await Master.Initialize(10, FALSE, TRUE);

        world.RunUnattendedFunctions();
    }

    /// Initializes TGS and loads the returned revising info into GLOB.revdata
    // tg: code/game/world.dm:146
    // PORT: a no-op seam. TgsNew(new /datum/tgs_event_handler/impl, TGS_SECURITY_TRUSTED) starts the tgstation-server API and
    // GLOB.revdata.load_tgs_info() reads the revision TGS deployed; TGS is not ported (translation-rules.md 13) and without it
    // TgsNew does nothing and load_tgs_info has no info to load.
    public static async DmTask InitTgs(this DmWorld world)
    {
    }

    /// Runs after config is loaded but before Master is initialized
    // tg: code/game/world.dm:151
    public static async DmTask ConfigLoaded(this DmWorld world)
    {
        // Everything in here is prioritized in a very specific way.
        // If you need to add to it, ask yourself hard if what your adding is in the right spot
        // (i.e. basically nothing should be added before load_admins() in here)

        // Try to set round ID
        await SSdbcore.InitializeRound();

        world.SetupLogs();

        load_admins(initial: TRUE);

        Dm.Deferred("code/game/world.dm:163", "ConfigLoaded: load_poll_data(), the player polls (database, M10+)");

        // Initialize RETA system - code/modules/reta/reta_system.dm
        reta_init_config();

        if (Dm.truthy(fexists(RESTART_COUNTER_PATH)))
        {
            GLOB.restart_counter = text2num(trim(file2text(RESTART_COUNTER_PATH)));
            fdel(RESTART_COUNTER_PATH);
        }
    }

    /// Runs after the call to Master.Initialize, but before the delay kicks in. Used to turn the world execution into some single function then exit
    // tg: code/game/world.dm:173
    public static void RunUnattendedFunctions(this DmWorld world)
    {
        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)

        // tg: #ifdef AUTOWIKI ... #endif not translated (inactive in production)

        // tg: #ifdef PERFORMANCE_TESTS ... #endif not translated (inactive in production)
    }

    // tg: code/game/world.dm:186
    public static async DmTask HandleTestRun(this DmWorld world)
    {
        Dm.NotTranslated("code/game/world.dm:186", "HandleTestRun: the unattended test run (UNIT_TESTS builds only)");
    }

    // tg: code/game/world.dm:199
    public static void queue_performance_tests(this DmWorld world)
    {
        Dm.NotTranslated("code/game/world.dm:199", "queue_performance_tests: the unattended performance run (PERFORMANCE_TESTS builds only)");
    }

    /// Stub proc intended to be filled with code that does some test, profiles it, and logs that test.
    /// Intended to be used with line by line macros, but you should live your truth
    // tg: code/game/world.dm:208
    public static void run_performance_tests(this DmWorld world)
    {
        Dm.NotTranslated("code/game/world.dm:208", "run_performance_tests: the unattended performance run (PERFORMANCE_TESTS builds only)");
    }

    /// Returns a list of data about the world state, don't clutter
    // tg: code/game/world.dm:219
    public static object get_world_state_for_logging(this DmWorld world)
    {
        object data = Dm.list();
        ((DmList)data)["tick_usage"] = world.tick_usage;
        ((DmList)data)["tick_lag"] = world.tick_lag;
        ((DmList)data)["time"] = world.time;
        ((DmList)data)["timestamp"] = rustg_unix_timestamp();
        return data;
    }

    // tg: code/game/world.dm:227
    public static void SetupLogs(this DmWorld world)
    {
        object override_dir = world.@params[OVERRIDE_LOG_DIRECTORY_PARAMETER];
        if (!Dm.truthy(override_dir))
        {
            float realtime = world.realtime;
            string texttime = time2text(realtime, "YYYY/MM/DD", TIMEZONE_UTC);
            GLOB.log_directory = Dm.text("data/logs/[]/round-", texttime);
            GLOB.picture_logging_prefix = Dm.text("L_[]_", time2text(realtime, "YYYYMMDD", TIMEZONE_UTC));
            GLOB.picture_log_directory = Dm.text("data/picture_logs/[]/round-", texttime);
            if (Dm.truthy(GLOB.round_id))
            {
                Dm.append(ref GLOB.log_directory, Dm.text("[]", GLOB.round_id));
                Dm.append(ref GLOB.picture_logging_prefix, Dm.text("R_[]_", GLOB.round_id));
                Dm.append(ref GLOB.picture_log_directory, Dm.text("[]", GLOB.round_id));
            }
            else
            {
                string timestamp = replacetext(server_timestamp(), ":", ".");
                Dm.append(ref GLOB.log_directory, Dm.text("[]", timestamp));
                Dm.append(ref GLOB.picture_log_directory, Dm.text("[]", timestamp));
                Dm.append(ref GLOB.picture_logging_prefix, Dm.text("T_[]_", timestamp));
            }
        }
        else
        {
            GLOB.log_directory = Dm.text("data/logs/[]", override_dir);
            GLOB.picture_logging_prefix = Dm.text("O_[]_", override_dir);
            GLOB.picture_log_directory = Dm.text("data/picture_logs/[]", override_dir);
        }

        logger.init_logging();

        // tg: code/game/world.dm:251-252  if(Tracy.trace_path) rustg_file_write(...): PORT: byond-tracy is not ported
        // (translation-rules.md 13).

        // DEVIATION(DV200): the port's changelog is CHANGELOG.md, not this month's html/changelogs/archive/YYYY-MM.yml (datums/changelog)
        string latest_changelog = file2text(datum_changelog.ChangelogFile);
        GLOB.changelog_hash = latest_changelog != null ? md5(latest_changelog) : 0; //for telling if the changelog has changed recently

        if (Dm.truthy(GLOB.round_id))
            log_game(Dm.text("Round ID: []", GLOB.round_id));

        // This was printed early in startup to the world log and config_error.log,
        // but those are both private, so let's put the commit info in the runtime
        // log which is ultimately public.
        Dm.Deferred("code/game/world.dm:263", "SetupLogs: log_runtime(GLOB.revdata.get_log_message()), the commit info for the runtime log (/datum/getrev, M10+)");

        // tg: #ifndef USE_CUSTOM_ERROR_HANDLER ... #else: USE_CUSTOM_ERROR_HANDLER is defined (_compile_options.dm:8), so the #else
        // branch is the active one.
        if (Dm.truthy(world.TgsAvailable())) // why
        {
            Dm.NotTranslated("code/game/world.dm:269", "SetupLogs: world.log = file(\"[GLOB.log_directory]/dd.log\") (the facade's world.log cannot be reassigned; TGS is not ported, so TgsAvailable() is never true)");
        }
    }

    // tg: code/game/world.dm:274
    public static void Tick(this DmWorld world)
    {
        // We need a hook for if maptick has happen yet
        GLOB.last_maptick_time = world.time;
    }

    // tg: code/game/world.dm:278
    public static async DmTask<object> Topic(this DmWorld world, object T = null, object addr = null, object master = null, object key = null, params object[] __extra)
    {
        Dm.NotTranslated("code/game/world.dm:278", "Topic: world topic calls (server-to-server and TGS commands, M10+)");
        return null;
    }

    // tg: code/game/world.dm:299
    public static void AnnouncePR(this DmWorld world, object announcement = null, DmList payload = null)
    {
        Dm.NotTranslated("code/game/world.dm:299", "AnnouncePR: pull request announcements from TGS (not ported)");
    }

    // tg: code/game/world.dm:313  (set waitfor = FALSE)
    public static void FinishTestRun(this DmWorld world)
    {
        Dm.NotTranslated("code/game/world.dm:313", "FinishTestRun: the end of an unattended test run (UNIT_TESTS builds only)");
    }

    /// Returns TRUE if the world should do a TGS hard reboot.
    // tg: code/game/world.dm:335
    public static object check_hard_reboot(this DmWorld world)
    {
        Dm.NotTranslated("code/game/world.dm:335", "check_hard_reboot: the TGS hard reboot (round loop, M10+)");
        return null;
    }

    // tg: code/game/world.dm:354
    public static async DmTask Reboot(this DmWorld world, float? reason = null, float? fast_track = null)
    {
        reason ??= 0;
        fast_track ??= FALSE;
        if (Dm.truthy(reason) || Dm.truthy(fast_track)) //special reboot, do none of the normal stuff
        {
            // tg: :356-358  the admins' log and message of a client side debugging restart (M10+)
            to_chat(world, span_boldannounce(I18n.T("Rebooting World immediately due to host request.")));
        }
        else
        {
            to_chat(world, span_boldannounce(I18n.T("Rebooting world...")));
            await Master.Shutdown(); //run SS shutdowns
        }

        // tg: :366-373  check_hard_reboot (the TGS process restart after a number of rounds): the port's host always restarts the
        // world in-process (D15), so there is no hard reboot
        log_world(Dm.text("World rebooted at []", server_timestamp()));
        // tg: :377-381  shutdown_logging, Tracy, the debugger and TgsReboot: the logs (M10+), byond-tracy, auxtools and TGS (not
        // ported, rules 13)

        // ..(): BYOND's world/Reboot, the host's restart (D15)
        DmWorldState.Of(World.Current).RebootRequested = true;
    }

    // tg: code/game/world.dm:387
    public static void Del(this DmWorld world)
    {
        // tg: :388-389  QDEL_NULL(Tracy); QDEL_NULL(Debugger): byond-tracy and the auxtools debugger are not ported
        // (translation-rules.md 13; Genesis makes neither, :68-94), so both vars are null and there is nothing to delete.
        // . = ..(): BYOND's own world/Del, "Shutdown the world" (BYOND ref /world/proc/Del): once world/Del returns, the
        // host stops stepping the world (M1 P3); "any other procedures which may still be executing are immediately
        // aborted and all objects are silently destroyed", so no client/Del or Logout runs.
    }

    // tg: code/game/world.dm:392
    public static void update_status(this DmWorld world)
    {
        // tg: :393-438  the status line BYOND shows in the hub (features, the server name, the round state, the map) and
        // game_state (the popcap), written to world.status
        Dm.Deferred("code/game/world.dm:393", "update_status: the BYOND hub status line (world.status, game_state; the BYOND hub is not ported, rules 13)");
    }

    // tg: code/game/world.dm:444
    public static void update_hub_visibility(this DmWorld world, object new_visibility = null)
    {
        Dm.NotTranslated("code/game/world.dm:444", "update_hub_visibility: the BYOND hub listing (not ported)");
    }

    /**
     * Handles increasing the world's maxx var and initializing the new turfs and assigning them to the global area.
     * If map_load_z_cutoff is passed in, it will only load turfs up to that z level, inclusive.
     * This is because maploading will handle the turfs it loads itself.
     */
    // tg: code/game/world.dm:458
    public static void increase_max_x(this DmWorld world, object new_maxx = null, object map_load_z_cutoff = null)
    {
        map_load_z_cutoff ??= world.maxz;
        if (Dm.le(new_maxx, world.maxx))
            return;
        float old_max = world.maxx;
        world.maxx = Dm.num(new_maxx);
        if (!Dm.truthy(map_load_z_cutoff))
            return;
        area global_area = GLOB.areas_by_type[world.area] as area; // We're guaranteed to be touching the global area, so we'll just do this
        // LISTASSERTLEN(global_area.turfs_by_zlevel, map_load_z_cutoff, list()) (__HELPERS/_lists.dm:114), expanded inline (rules 11.1 (e))
        if (Dm.lt(length(global_area.turfs_by_zlevel), map_load_z_cutoff))
        {
            float _OLD_LENGTH = length(global_area.turfs_by_zlevel);
            global_area.turfs_by_zlevel.len = Dm.num(map_load_z_cutoff);
            /* Convert the optional argument to a if check */
            foreach (object _USELESS_VAR in Dm.each(Dm.list(Dm.list())))
            {
                for (float _INDEX_TO_ASSIGN_TO = _OLD_LENGTH + 1, _INDEX_TO_ASSIGN_TO__end = Dm.num(map_load_z_cutoff); _INDEX_TO_ASSIGN_TO <= _INDEX_TO_ASSIGN_TO__end; _INDEX_TO_ASSIGN_TO++)
                    global_area.turfs_by_zlevel[_INDEX_TO_ASSIGN_TO] = Dm.list();
            }
        }
        for (float zlevel = 1, zlevel__end = Dm.num(map_load_z_cutoff); zlevel <= zlevel__end; zlevel++)
        {
            DmList to_add = block(
                old_max + 1, 1, zlevel,
                world.maxx, world.maxy, zlevel
            );

            Dm.append_at(global_area.turfs_by_zlevel, zlevel, to_add);
        }
    }

    // tg: code/game/world.dm:475
    public static void increase_max_y(this DmWorld world, object new_maxy = null, object map_load_z_cutoff = null)
    {
        map_load_z_cutoff ??= world.maxz;
        if (Dm.le(new_maxy, world.maxy))
            return;
        float old_maxy = world.maxy;
        world.maxy = Dm.num(new_maxy);
        if (!Dm.truthy(map_load_z_cutoff))
            return;
        area global_area = GLOB.areas_by_type[world.area] as area; // We're guaranteed to be touching the global area, so we'll just do this
        // LISTASSERTLEN(global_area.turfs_by_zlevel, map_load_z_cutoff, list()) (__HELPERS/_lists.dm:114), expanded inline (rules 11.1 (e))
        if (Dm.lt(length(global_area.turfs_by_zlevel), map_load_z_cutoff))
        {
            float _OLD_LENGTH = length(global_area.turfs_by_zlevel);
            global_area.turfs_by_zlevel.len = Dm.num(map_load_z_cutoff);
            /* Convert the optional argument to a if check */
            foreach (object _USELESS_VAR in Dm.each(Dm.list(Dm.list())))
            {
                for (float _INDEX_TO_ASSIGN_TO = _OLD_LENGTH + 1, _INDEX_TO_ASSIGN_TO__end = Dm.num(map_load_z_cutoff); _INDEX_TO_ASSIGN_TO <= _INDEX_TO_ASSIGN_TO__end; _INDEX_TO_ASSIGN_TO++)
                    global_area.turfs_by_zlevel[_INDEX_TO_ASSIGN_TO] = Dm.list();
            }
        }
        for (float zlevel = 1, zlevel__end = Dm.num(map_load_z_cutoff); zlevel <= zlevel__end; zlevel++)
        {
            DmList to_add = block(
                1, old_maxy + 1, zlevel,
                world.maxx, world.maxy, zlevel
            );
            Dm.append_at(global_area.turfs_by_zlevel, zlevel, to_add);
        }
    }

    // tg: code/game/world.dm:491
    public static void incrementMaxZ(this DmWorld world)
    {
        world.maxz++;
        SSmobs.MaxZChanged();
        SSai_controllers?.on_max_z_changed(); // null only in the unbooted toy worlds, which make the subsystems they need
    }

    // tg: code/game/world.dm:496
    public static void change_fps(this DmWorld world, float? new_value = null)
    {
        new_value ??= 20;
        if (Dm.num(new_value) <= 0)
            throw Dm.CRASH(Dm.text("change_fps() called with [] new_value.", new_value), "code/game/world.dm", 498);
        if (Dm.eq(world.fps, new_value))
            return; //No change required.

        // DEVIATION(DV6): world.fps is the constant 20 (World.Fps): fps = new_value and on_tickrate_change() (:502-503) cannot
        // run, so any other value is a runtime error. The Master's Initialize calls this with the configured fps (default 20).
        throw Dm.CRASH(Dm.text("change_fps() called with [] new_value: world.fps is a constant [] in the port (DV6).", new_value, world.fps), "code/game/world.dm", 502);
    }

    // tg: code/game/world.dm:506
    public static void change_tick_lag(this DmWorld world, float? new_value = null)
    {
        new_value ??= 0.5f;
        if (Dm.num(new_value) <= 0)
            throw Dm.CRASH(Dm.text("change_tick_lag() called with [] new_value.", new_value), "code/game/world.dm", 508);
        if (Dm.eq(world.tick_lag, new_value))
            return; //No change required.

        // DEVIATION(DV6): world.tick_lag is the constant 0.5 (World.TickLag): tick_lag = new_value and on_tickrate_change()
        // (:512-513) cannot run, so any other value is a runtime error.
        throw Dm.CRASH(Dm.text("change_tick_lag() called with [] new_value: world.tick_lag is a constant [] in the port (DV6).", new_value, world.tick_lag), "code/game/world.dm", 512);
    }

    // tg: code/game/world.dm:516
    public static void on_tickrate_change(this DmWorld world)
    {
        SStimer?.reset_buckets();
        // tg: #ifndef DISABLE_DREAMLUAU ... #endif not translated (inactive in production)
    }

    // tg: code/game/world.dm:522
    public static object Profile(this DmWorld world, object command = null, object type = null, object format = null)
    {
        Dm.NotTranslated("code/game/world.dm:522", "Profile: BYOND's profiler is not ported (translation-rules.md 13)");
        return null;
    }
}
