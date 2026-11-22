using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Logging;

/// <summary>
/// A world for the logging and chat tests: records runtimes and the host log (world.log), keeps the in-memory file
/// seam every world starts with, and fixes world.realtime at 2026-10-06 12:00:00 UTC so rust-g's timestamps are known.
/// <see cref="Setup"/> creates what tg's Genesis and SetupLogs would have created before logging is used (GLOB with the
/// vars the logging code reads, the logger, a configuration), without the boot package's procs.
/// </summary>
internal sealed class LoggingTestWorld
{
    public static readonly DateTime Start = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
    public const string LogDirectory = "data/logs/test-round";

    public readonly World World;
    public readonly List<Exception> Runtimes = new();
    public readonly List<string> HostLog = new();
    public readonly ChatRecorder Chat = new();

    public LoggingTestWorld(ulong seed = 7)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        DmWorldState state = DmWorldState.Of(World);
        state.WorldLog = HostLog.Add;
        state.RealtimeStart = (Start - new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds * 10d;
        PresentationSinks.Of(World).Messages = Chat;
    }

    public DmMemoryFiles Files => (DmMemoryFiles)DmWorldState.Of(World).Files;

    /// <summary>The text of a file of the world's file seam, null when it does not exist.</summary>
    public string Read(string path) => Files.ReadText(path);

    /// <summary>The runtime messages so far, "Type: message".</summary>
    public List<string> Messages => Runtimes.Select(e => e.GetType().Name + ": " + e.Message).ToList();

    /// <summary>
    /// Inside the world scope: GLOB (created without its New, globals.dm:11, which the boot package translates; only
    /// the vars the logging and chat code read are set), the logger (game/world.dm:97) and a fixture configuration.
    /// </summary>
    public test_logging_config Setup(object round_id = null)
    {
        var glob = new datum_controller_global_vars();
        glob.gvars_datum_init_order = Dm.list();
        datum_controller_global_vars.gvars_datum_protected_varlist = Dm.list(); // GLOB's Initialize (globals.dm:50); GLOBAL_PROTECT adds to it
        glob.clients = Dm.list();
        glob.admins = Dm.list();
        glob.round_id = round_id;
        glob.log_directory = LogDirectory;
        TgState.Current.GLOB = glob;
        logger = Dm.@new<datum_log_holder>();
        var cfg = Dm.@new<test_logging_config>();
        config = cfg;
        return cfg;
    }

    /// <summary>The timestamp rust-g's log_write puts before a line now: <c>[%F %T%.3f]</c>.</summary>
    public string Stamp() => "[" + Now().ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + "]";

    public DateTime Now() => Start.AddMilliseconds(World.Tick * 50);
}

/// <summary>
/// A configuration that answers <c>Get</c> from a table (every other entry: TRUE), so that the logging tests do not need
/// the boot package's configuration. tg's /datum/controller/configuration has no New of its own.
/// </summary>
[DmPath("/datum/controller/configuration/test_logging_config")]
public class test_logging_config : datum_controller_configuration
{
    [NotHashed]
    public Dictionary<Type, object> values = new();

    [NotHashed]
    public List<Type> asked = new();

    public override object Get(object entry_type = null)
    {
        asked.Add((Type)entry_type);
        return values.TryGetValue((Type)entry_type, out object value) ? value : TRUE;
    }
}

/// <summary>A message sink that records every chat call with the message's keys.</summary>
internal sealed class ChatRecorder : IMessageSink
{
    public readonly List<string> Lines = new();
    public readonly List<(object Target, DmList Message)> Calls = new();

    public void Chat(object target, DmList message)
    {
        Calls.Add((target, message));
        var parts = new List<string>();
        foreach ((object key, object value) in Dm.pairs(message))
            parts.Add(Dm.text("[]=[]", key, value));
        Lines.Add("chat " + RecordingSinks.Describe(target) + " " + string.Join(" ", parts));
    }

    public void Output(object target, string text) => Lines.Add("output " + RecordingSinks.Describe(target) + " " + text);

    public void Balloon(object viewer, object atom, string text) => Lines.Add("balloon " + text);
}

/// <summary>
/// A mob for chat targets whose New does not call ..(): the test does not depend on atom initialization (/atom/New,
/// SSatoms; the atoms package).
/// </summary>
[DmPath("/mob/test_logging_listener")]
public class mob_test_logging_listener : mob
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

// Fixture log categories for the category tree checks of assemble_log_category_tree (log_holder.dm:164-201). They are
// outside the Tg assembly, so a world sees them only when a test sets TgState.LogCategoryTypeFilter.

/// <summary>A master category.</summary>
[DmPath("/datum/log_category/test_logging_master")]
public class datum_log_category_test_logging_master : datum_log_category
{
    public datum_log_category_test_logging_master() { category = "test-master"; }
}

/// <summary>A sub category whose secret status differs from its master's.</summary>
[DmPath("/datum/log_category/test_logging_secret_sub")]
public class datum_log_category_test_logging_secret_sub : datum_log_category
{
    public datum_log_category_test_logging_secret_sub()
    {
        category = "test-secret-sub";
        master_category = typeof(datum_log_category_test_logging_master);
        secret = TRUE;
    }
}

/// <summary>A sub category whose master is itself a sub category.</summary>
[DmPath("/datum/log_category/test_logging_sub_of_sub")]
public class datum_log_category_test_logging_sub_of_sub : datum_log_category
{
    public datum_log_category_test_logging_sub_of_sub()
    {
        category = "test-sub-of-sub";
        master_category = typeof(datum_log_category_test_logging_secret_sub);
    }
}

/// <summary>A second category with the master's category name.</summary>
[DmPath("/datum/log_category/test_logging_duplicate")]
public class datum_log_category_test_logging_duplicate : datum_log_category
{
    public datum_log_category_test_logging_duplicate() { category = "test-master"; }
}

/// <summary>A category without a schema version.</summary>
[DmPath("/datum/log_category/test_logging_no_schema")]
public class datum_log_category_test_logging_no_schema : datum_log_category
{
    public datum_log_category_test_logging_no_schema()
    {
        category = "test-no-schema";
        schema_version = null;
    }
}

/// <summary>A category whose schema version is no semver.</summary>
[DmPath("/datum/log_category/test_logging_bad_schema")]
public class datum_log_category_test_logging_bad_schema : datum_log_category
{
    public datum_log_category_test_logging_bad_schema()
    {
        category = "test-bad-schema";
        schema_version = "one";
    }
}
