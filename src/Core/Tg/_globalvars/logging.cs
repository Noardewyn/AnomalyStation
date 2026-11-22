// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/logging.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The GLOBAL_* macros (__DEFINES/__globals.dm) declare each var on /datum/controller/global_vars with its InitGlobal proc
// (rules 10.6); GLOBAL_PROTECT adds a second InitGlobal definition that calls the first with ..() (rules 7.5: the earlier
// one is InitGlobal<name>__d0). DECLARE_LOG and DECLARE_LOG_NAMED (:10-21, file-local) add one /world/_initialize_log_files
// definition per log, a DmWorld extension method (rules 1.5); the file alternates between the two types (rules 1.9).
// RULE-GAP: rules 2.5 and 7.5 name one earlier definition (__d0); /world/_initialize_log_files is defined four times
// (:26, :33, :34, :44), so the earlier ones are __d0, __d1, __d2 in definition order, each later one calling the one before.

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/logging.dm:1  GLOBAL_LIST_EMPTY(active_turfs_startlist)
    public DmList active_turfs_startlist;

    // tg: code/_globalvars/logging.dm:1  GLOBAL_LIST_EMPTY(active_turfs_startlist): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9)
    public virtual void InitGlobalactive_turfs_startlist()
    {
        active_turfs_startlist = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(active_turfs_startlist));
    }

    // tg: code/_globalvars/logging.dm:3  GLOBAL_VAR(round_id)
    public object round_id;

    // tg: code/_globalvars/logging.dm:3  GLOBAL_VAR(round_id): GLOBAL_UNMANAGED (__DEFINES/__globals.dm:11), the earlier definition
    public void InitGlobalround_id__d0()
    {
        return;
    }

    // tg: code/_globalvars/logging.dm:4  GLOBAL_PROTECT(round_id) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobalround_id()
    {
        InitGlobalround_id__d0();
        gvars_datum_protected_varlist[nameof(round_id)] = TRUE;
    }

    /// The directory in which ALL log files should be stored
    // tg: code/_globalvars/logging.dm:7  GLOBAL_VAR(log_directory)
    public object log_directory;

    // tg: code/_globalvars/logging.dm:7  GLOBAL_VAR(log_directory): GLOBAL_UNMANAGED (__DEFINES/__globals.dm:11), the earlier definition
    public void InitGloballog_directory__d0()
    {
        return;
    }

    // tg: code/_globalvars/logging.dm:8  GLOBAL_PROTECT(log_directory) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGloballog_directory()
    {
        InitGloballog_directory__d0();
        gvars_datum_protected_varlist[nameof(log_directory)] = TRUE;
    }
}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/logging.dm:22  #define START_LOG TRUE (file-local, #undef at :91)
    private const int START_LOG = 1;
    // tg: code/_globalvars/logging.dm:23  #define DONT_START_LOG FALSE (file-local, #undef at :92)
    private const int DONT_START_LOG = 0;

    /// Populated by log declaration macros to set log file names and start messages
    // tg: code/_globalvars/logging.dm:26  (the first definition; DECLARE_LOG_NAMED adds one more per log, rules 7.5)
    [ShouldCallParent]
    public static void _initialize_log_files__d0(this DmWorld world, object temp_log_override = null)
    {
        // Needs to be here to avoid compiler warnings
        return;
    }
}

// All individual log files.
// These should be used where the log category cannot easily be a json log file.
public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/logging.dm:33  DECLARE_LOG(config_error_log, DONT_START_LOG): GLOBAL_VAR(config_error_log)
    public object config_error_log;

    // tg: code/_globalvars/logging.dm:33  GLOBAL_VAR(config_error_log): GLOBAL_UNMANAGED (__DEFINES/__globals.dm:11), the earlier definition
    public void InitGlobalconfig_error_log__d0()
    {
        return;
    }

    // tg: code/_globalvars/logging.dm:33  GLOBAL_PROTECT(config_error_log) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobalconfig_error_log()
    {
        InitGlobalconfig_error_log__d0();
        gvars_datum_protected_varlist[nameof(config_error_log)] = TRUE;
    }
}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/logging.dm:33  DECLARE_LOG(config_error_log, DONT_START_LOG): the /world/_initialize_log_files that DECLARE_LOG_NAMED defines (:13-19), an earlier definition (rules 7.5)
    public static void _initialize_log_files__d1(this DmWorld world, object temp_log_override = null)
    {
        world._initialize_log_files__d0(temp_log_override);
        GLOB.config_error_log = (temp_log_override is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.text("[]/[].log", GLOB.log_directory, Dm.text("[]", copytext("config_error_log", 1, length("config_error_log") - 3))));
        if (!Dm.truthy(temp_log_override) && Dm.truthy(DONT_START_LOG))
            start_log(GLOB.config_error_log);
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/logging.dm:34  DECLARE_LOG(perf_log, DONT_START_LOG): GLOBAL_VAR(perf_log)
    public object perf_log;

    // tg: code/_globalvars/logging.dm:34  GLOBAL_VAR(perf_log): GLOBAL_UNMANAGED (__DEFINES/__globals.dm:11), the earlier definition
    public void InitGlobalperf_log__d0()
    {
        return;
    }

    // tg: code/_globalvars/logging.dm:34  GLOBAL_PROTECT(perf_log) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobalperf_log()
    {
        InitGlobalperf_log__d0();
        gvars_datum_protected_varlist[nameof(perf_log)] = TRUE;
    }
}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/logging.dm:34  DECLARE_LOG(perf_log, DONT_START_LOG): the /world/_initialize_log_files that DECLARE_LOG_NAMED defines (:13-19), an earlier definition (rules 7.5)
    // Declared here but name is set in time_track subsystem
    public static void _initialize_log_files__d2(this DmWorld world, object temp_log_override = null)
    {
        world._initialize_log_files__d1(temp_log_override);
        GLOB.perf_log = (temp_log_override is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.text("[]/[].log", GLOB.log_directory, Dm.text("[]", copytext("perf_log", 1, length("perf_log") - 3))));
        if (!Dm.truthy(temp_log_override) && Dm.truthy(DONT_START_LOG))
            start_log(GLOB.perf_log);
    }
}

// tg: #ifdef REFERENCE_TRACKING_LOG_APART DECLARE_LOG_NAMED(harddel_log, "harddels", START_LOG) #endif not translated (inactive in production)
// tg: #if defined(UNIT_TESTS) || defined(SPACEMAN_DMM) DECLARE_LOG_NAMED(test_log, "tests", START_LOG) #endif not translated (inactive in production)
public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/logging.dm:44  DECLARE_LOG_NAMED(reta_log, "reta", START_LOG): GLOBAL_VAR(reta_log)
    public object reta_log;

    // tg: code/_globalvars/logging.dm:44  GLOBAL_VAR(reta_log): GLOBAL_UNMANAGED (__DEFINES/__globals.dm:11), the earlier definition
    public void InitGlobalreta_log__d0()
    {
        return;
    }

    // tg: code/_globalvars/logging.dm:44  GLOBAL_PROTECT(reta_log) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobalreta_log()
    {
        InitGlobalreta_log__d0();
        gvars_datum_protected_varlist[nameof(reta_log)] = TRUE;
    }
}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/logging.dm:44  DECLARE_LOG_NAMED(reta_log, "reta", START_LOG): the /world/_initialize_log_files that DECLARE_LOG_NAMED defines (:13-19)
    public static void _initialize_log_files(this DmWorld world, object temp_log_override = null)
    {
        world._initialize_log_files__d2(temp_log_override);
        GLOB.reta_log = (temp_log_override is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.text("[]/[].log", GLOB.log_directory, "reta"));
        if (!Dm.truthy(temp_log_override) && Dm.truthy(START_LOG))
            start_log(GLOB.reta_log);
    }
}

public partial class datum_controller_global_vars
{
    /// Picture logging
    // tg: code/_globalvars/logging.dm:48  GLOBAL_VAR(picture_log_directory)
    public object picture_log_directory;

    // tg: code/_globalvars/logging.dm:48  GLOBAL_VAR(picture_log_directory): GLOBAL_UNMANAGED (__DEFINES/__globals.dm:11), the earlier definition
    public void InitGlobalpicture_log_directory__d0()
    {
        return;
    }

    // tg: code/_globalvars/logging.dm:49  GLOBAL_PROTECT(picture_log_directory) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobalpicture_log_directory()
    {
        InitGlobalpicture_log_directory__d0();
        gvars_datum_protected_varlist[nameof(picture_log_directory)] = TRUE;
    }

    // tg: code/_globalvars/logging.dm:51  GLOBAL_VAR_INIT(picture_logging_id, 1)
    public object picture_logging_id;

    // tg: code/_globalvars/logging.dm:51  GLOBAL_VAR_INIT(picture_logging_id, 1): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9), the earlier definition
    public void InitGlobalpicture_logging_id__d0()
    {
        picture_logging_id = 1;
        Dm.append(ref gvars_datum_init_order, nameof(picture_logging_id));
    }

    // tg: code/_globalvars/logging.dm:52  GLOBAL_PROTECT(picture_logging_id) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobalpicture_logging_id()
    {
        InitGlobalpicture_logging_id__d0();
        gvars_datum_protected_varlist[nameof(picture_logging_id)] = TRUE;
    }

    // tg: code/_globalvars/logging.dm:54  GLOBAL_VAR(picture_logging_prefix)
    public object picture_logging_prefix;

    // tg: code/_globalvars/logging.dm:54  GLOBAL_VAR(picture_logging_prefix): GLOBAL_UNMANAGED (__DEFINES/__globals.dm:11), the earlier definition
    public void InitGlobalpicture_logging_prefix__d0()
    {
        return;
    }

    // tg: code/_globalvars/logging.dm:55  GLOBAL_PROTECT(picture_logging_prefix) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobalpicture_logging_prefix()
    {
        InitGlobalpicture_logging_prefix__d0();
        gvars_datum_protected_varlist[nameof(picture_logging_prefix)] = TRUE;
    }

    /// All admin related log lines minus their categories
    // tg: code/_globalvars/logging.dm:58  GLOBAL_LIST_EMPTY(admin_activities)
    public DmList admin_activities;

    // tg: code/_globalvars/logging.dm:58  GLOBAL_LIST_EMPTY(admin_activities): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9), the earlier definition
    public void InitGlobaladmin_activities__d0()
    {
        admin_activities = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(admin_activities));
    }

    // tg: code/_globalvars/logging.dm:59  GLOBAL_PROTECT(admin_activities) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobaladmin_activities()
    {
        InitGlobaladmin_activities__d0();
        gvars_datum_protected_varlist[nameof(admin_activities)] = TRUE;
    }

    /// All bomb related messages
    // tg: code/_globalvars/logging.dm:62  GLOBAL_LIST_EMPTY(bombers)
    public DmList bombers;

    // tg: code/_globalvars/logging.dm:62  GLOBAL_LIST_EMPTY(bombers): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9), the earlier definition
    public void InitGlobalbombers__d0()
    {
        bombers = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(bombers));
    }

    // tg: code/_globalvars/logging.dm:63  GLOBAL_PROTECT(bombers) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobalbombers()
    {
        InitGlobalbombers__d0();
        gvars_datum_protected_varlist[nameof(bombers)] = TRUE;
    }

    /// Investigate log for signaler usage, use the add_to_signaler_investigate_log proc
    // tg: code/_globalvars/logging.dm:66  GLOBAL_LIST_EMPTY(investigate_signaler)
    public DmList investigate_signaler;

    // tg: code/_globalvars/logging.dm:66  GLOBAL_LIST_EMPTY(investigate_signaler): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9), the earlier definition
    public void InitGlobalinvestigate_signaler__d0()
    {
        investigate_signaler = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(investigate_signaler));
    }

    // tg: code/_globalvars/logging.dm:67  GLOBAL_PROTECT(investigate_signaler) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGlobalinvestigate_signaler()
    {
        InitGlobalinvestigate_signaler__d0();
        gvars_datum_protected_varlist[nameof(investigate_signaler)] = TRUE;
    }
}

public static partial class GlobalProcs
{
    /// Used to add a text log to the signaler investigation log.
    /// Do not add to the list directly; if the list is too large it can cause lag when an admin tries to view it.
    // tg: code/_globalvars/logging.dm:71
    public static void add_to_signaler_investigate_log(object text = null)
    {
        float log_length = length(GLOB.investigate_signaler);
        if (log_length >= INVESTIGATE_SIGNALER_LOG_MAX_LENGTH)
            GLOB.investigate_signaler = GLOB.investigate_signaler.Copy((INVESTIGATE_SIGNALER_LOG_MAX_LENGTH - log_length) + 2);
        Dm.append(ref GLOB.investigate_signaler, Dm.list(text));
    }
}

public partial class datum_controller_global_vars
{
    /// Stores who uploaded laws to which silicon-based lifeform, and what the law was
    // tg: code/_globalvars/logging.dm:78  GLOBAL_LIST_EMPTY(lawchanges)
    public DmList lawchanges;

    // tg: code/_globalvars/logging.dm:78  GLOBAL_LIST_EMPTY(lawchanges): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9), the earlier definition
    public void InitGloballawchanges__d0()
    {
        lawchanges = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(lawchanges));
    }

    // tg: code/_globalvars/logging.dm:79  GLOBAL_PROTECT(lawchanges) (__DEFINES/__globals.dm:15-19)
    public virtual void InitGloballawchanges()
    {
        InitGloballawchanges__d0();
        gvars_datum_protected_varlist[nameof(lawchanges)] = TRUE;
    }
}

public static partial class GlobalProcs
{
    /// Adds something to the law change log
    // tg: code/_globalvars/logging.dm:82
    public static void log_law_change(mob_living changer = null, object log_message = null)
    {
        if (changer == null)
            log_message = capitalize(log_message);

        Dm.append(GLOB.lawchanges, Dm.text("[] <b>:</b> [] []", round_timestamp(), changer != null ? key_name(changer) : "(No mob)", log_message));
        log_silicon(Dm.text("Law change: [] []", changer != null ? key_name(changer) : "(No mob)", log_message));
    }
}
