using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Logging;

/// <summary>
/// code/_globalvars/logging.dm: the GLOB initializers of the logging globals and the four definitions of
/// /world/_initialize_log_files that DECLARE_LOG chains through ..(); the rust-g file, log and time functions the logging
/// writes through (Builtin/rust_g.builtin.cs) on the world's file seam and clock (DV3).
/// </summary>
public class LoggingGlobalsTests
{
    private const string Dir = LoggingTestWorld.LogDirectory;

    [Fact]
    public void TheGlobInitializersCreateTheListsAndRecordTheManagedOnes()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            GLOB.InitGlobalactive_turfs_startlist();
            GLOB.InitGlobalround_id();
            GLOB.InitGlobalpicture_logging_id();
            GLOB.InitGloballawchanges();
            Assert.Equal(0f, GLOB.active_turfs_startlist.len);
            Assert.Equal(0f, GLOB.lawchanges.len);
            Assert.Equal(1, Dm.num(GLOB.picture_logging_id));
            // GLOBAL_MANAGED appends the name (__DEFINES/__globals.dm:8); GLOBAL_UNMANAGED (round_id) does not.
            Assert.Equal(new object[] { "active_turfs_startlist", "picture_logging_id", "lawchanges" }, Dm.each(GLOB.gvars_datum_init_order).ToArray());

            // GLOB's Initialize calls them by name in typesof(/datum/controller/global_vars/proc) order (globals.dm:61-63):
            // the binder finds the later definition, which calls the earlier one (..()).
            GLOB.picture_logging_id = 5;
            Waiting.NoSleep(Dm.Call(GLOB, "InitGlobalpicture_logging_id"));
            Assert.Equal(1, Dm.num(GLOB.picture_logging_id));
        }
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void TheSignalerLogKeepsItsLastEntries()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            GLOB.InitGlobalinvestigate_signaler();
            for (int i = 1; i <= INVESTIGATE_SIGNALER_LOG_MAX_LENGTH + 1; i++)
                add_to_signaler_investigate_log(Dm.text("signal []", i));
            // At 500 entries the next call copies from (500 - 500) + 2 = 2 on, then adds (logging.dm:72-75).
            Assert.Equal((float)INVESTIGATE_SIGNALER_LOG_MAX_LENGTH, GLOB.investigate_signaler.len);
            Assert.Equal("signal 2", GLOB.investigate_signaler[1]);
            Assert.Equal("signal 501", GLOB.investigate_signaler[INVESTIGATE_SIGNALER_LOG_MAX_LENGTH]);
        }
    }

    [Fact]
    public void InitializeLogFilesNamesEachLogAndStartsOnlyTheRetaLog()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup(round_id: 9);
            world._initialize_log_files();
            // DECLARE_LOG's name drops the "_log" suffix (logging.dm:21): config_error, perf; DECLARE_LOG_NAMED: reta.
            Assert.Equal(Dir + "/config_error.log", GLOB.config_error_log);
            Assert.Equal(Dir + "/perf.log", GLOB.perf_log);
            Assert.Equal(Dir + "/reta.log", GLOB.reta_log);
            Assert.Equal(new[] { Dir + "/reta.log" }, t.Files.Paths);
            Assert.Equal(t.Stamp() + " Starting up round ID 9.\n - -------------------------\n", t.Read(Dir + "/reta.log"));

            // With an override every log goes to that file and none is started (Genesis, game/world.dm:91).
            world._initialize_log_files("data/logs/config_error.tmp.log");
            Assert.Equal("data/logs/config_error.tmp.log", GLOB.config_error_log);
            Assert.Equal("data/logs/config_error.tmp.log", GLOB.perf_log);
            Assert.Equal("data/logs/config_error.tmp.log", GLOB.reta_log);
            Assert.Equal(new[] { Dir + "/reta.log" }, t.Files.Paths);
        }
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void BeforeGlobExistsTheLogDefinitionsSetTheGlobalVarsAndTheyStayAfterGlobIsCreated()
    {
        // tg's Genesis calls _initialize_log_files (game/world.dm:91) before Master/New creates GLOB (master.dm:128). The
        // GLOBAL_* vars are var/global (__DEFINES/__globals.dm:32), so DM writes them through the null GLOB without a
        // runtime, and GLOBAL_VAR's InitGlobal does not reset them (rules 10.6).
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            Assert.Null(GLOB__var);
            world._initialize_log_files("data/logs/config_error.early.log");
            Assert.Equal("data/logs/config_error.early.log", GLOB.config_error_log);
            var glob = new datum_controller_global_vars();
            GLOB__var = glob;
            Assert.Equal("data/logs/config_error.early.log", glob.config_error_log);
            Assert.Equal("data/logs/config_error.early.log", glob.perf_log);
            Assert.Equal("data/logs/config_error.early.log", glob.reta_log);
        }
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void RustgLogWriteTimestampsTheFirstLineAndIndentsTheOthersUnlessToldNotTo()
    {
        var t = new LoggingTestWorld();
        t.World.Step(25); // 1.25 s after the start
        using (t.World.Enter())
        {
            WRITE_LOG("logs/a.log", "first\nsecond\nthird");
            WRITE_LOG_NO_FORMAT("logs/a.log", "raw\ntext");
            rustg_log_write("logs/a.log", "", "maybe");
            Assert.Equal(
                "[2026-10-06 12:00:01.250] first\n - second\n - third\nraw\ntext[2026-10-06 12:00:01.250] \n",
                t.Read("logs/a.log"));
            Assert.Equal("", rustg_log_close_all());
        }
    }

    [Fact]
    public void RustgFileProcsWriteAndAppendThroughTheFileSeam()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            Assert.Equal("", rustg_file_append("one", "dir/f.txt"));
            rustg_file_append(2, "dir/f.txt");
            Assert.Equal("one2", t.Read("dir/f.txt"));
            rustg_file_write("fresh", "dir/f.txt");
            Assert.Equal("fresh", t.Read("dir/f.txt"));
        }
    }

    [Fact]
    public void RustgClockIsTheWorldClockAndItsTimersMeasureZero()
    {
        var t = new LoggingTestWorld();
        t.World.Step(3);
        using (t.World.Enter())
        {
            // unix_timestamp: seconds since 1970 with six decimals (rust-g time.rs).
            Assert.Equal("1791288000.150000", rustg_unix_timestamp());
            Assert.Equal("2026-10-06 12:00:00.150", rustg_formatted_timestamp("%Y-%m-%d %H:%M:%S%.3f"));
            Assert.Equal("2026-10-06T12:00:00 100%", rustg_formatted_timestamp("%FT%T 100%%"));
            Assert.Equal("2026-10-06 12:00:00.150", logger_timestamp());
            Assert.Throws<DmCrashException>(() => rustg_formatted_timestamp("%A"));

            // DV3: no wall clock, so a subsystem's init time (master.dm:499-504) is 0 ms.
            Assert.Equal("", rustg_time_reset("ss_init"));
            Assert.Equal(0f, rustg_time_milliseconds("ss_init"));
        }

        static object logger_timestamp() => Dm.@new<datum_log_holder>().human_readable_timestamp();
    }
}
