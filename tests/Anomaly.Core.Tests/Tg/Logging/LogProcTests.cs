using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Logging;

/// <summary>
/// The log procs of code/__HELPERS/logging/debug.dm, _logging.dm and game.dm, written from the DM source: each hands its
/// text to logger.Log with its category; log_world, log_config and log_mapping also write it to world.log (the host log,
/// DmWorldState.WorldLog). Before init_logging the logger queues every call (log_holder.dm:297-298).
/// </summary>
public class LogProcTests
{
    private static object[] Queued(int index) => Dm.each(logger.waiting_log_calls[index]).ToArray();

    [Fact]
    public void LogWorldReachesTheHostLogAndTheRuntimeCategory()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            log_world("the world says hi");
            // debug.dm:71-75: USE_CUSTOM_ERROR_HANDLER is defined (DEBUG), so logger.Log(LOG_CATEGORY_RUNTIME) first.
            Assert.Equal(new object[] { LOG_CATEGORY_RUNTIME, "the world says hi", null }, Queued(1));
            Assert.Equal(1f, logger.waiting_log_calls.len);
        }
        Assert.Equal(new[] { "the world says hi" }, t.HostLog);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void WarningAndNoticePrefixTheirMessageAndGoThroughLogWorld()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            warning("disk full");
            notice("all good");
            Assert.Equal(new object[] { LOG_CATEGORY_RUNTIME, "## WARNING: disk full", null }, Queued(1));
            Assert.Equal(new object[] { LOG_CATEGORY_RUNTIME, "## NOTICE: all good", null }, Queued(2));
        }
        Assert.Equal(new[] { "## WARNING: disk full", "## NOTICE: all good" }, t.HostLog);
    }

    [Fact]
    public void EachLogProcUsesItsCategoryAndOnlyTheWorldLogProcsWriteTheHostLog()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            DmList data = Dm.list(Dm.kv("k", 1));
            var calls = new (Action call, string category)[]
            {
                (() => log_asset("a", data), LOG_CATEGORY_DEBUG_ASSET),
                (() => log_config("b", data), LOG_CATEGORY_CONFIG),
                (() => log_filter_raw("c", data), LOG_CATEGORY_FILTER),
                (() => log_job_debug("d", data), LOG_CATEGORY_DEBUG_JOB),
                (() => log_tts("e", data), LOG_CATEGORY_DEBUG_TTS),
                (() => log_lua("f", data), LOG_CATEGORY_DEBUG_LUA),
                (() => log_qdel("g", data), LOG_CATEGORY_QDEL),
                (() => log_runtime("h", data), LOG_CATEGORY_RUNTIME),
                (() => log_signal("i", data), LOG_CATEGORY_SIGNAL),
                (() => log_sql("j", data), LOG_CATEGORY_DEBUG_SQL),
                (() => log_topic("k", data), LOG_CATEGORY_GAME_TOPIC),
                (() => log_game("l", data), LOG_CATEGORY_GAME),
                (() => log_emote("m", data), LOG_CATEGORY_GAME_EMOTE),
                (() => log_radio_emote("n", data), LOG_CATEGORY_GAME_RADIO_EMOTE),
                (() => log_ooc("o", data), LOG_CATEGORY_GAME_OOC),
                (() => log_prayer("p", data), LOG_CATEGORY_GAME_PRAYER),
                (() => log_internet_request("q", data), LOG_CATEGORY_GAME_INTERNET_REQUEST),
                (() => log_access("r", data), LOG_CATEGORY_GAME_ACCESS),
                (() => log_vote("s", data), LOG_CATEGORY_GAME_VOTE),
                (() => log_ghost_poll("t", data), LOG_CATEGORY_GAME_GHOST_POLLS),
                (() => log_minimap_drawing("u", data), LOG_CATEGORY_GAME_MINIMAP_DRAWING),
            };
            foreach ((Action call, _) in calls) call();
            for (int i = 0; i < calls.Length; i++)
            {
                object[] queued = Queued(i + 1);
                Assert.Equal(calls[i].category, queued[0]);
                Assert.Same(data, queued[2]);
            }

            // log_mapping(text, skip_world_log) logs without data (debug.dm:41) and skips world.log when asked (:42-43).
            log_mapping("mapped");
            log_mapping("quiet", TRUE);
            Assert.Equal(new object[] { LOG_CATEGORY_DEBUG_MAPPING, "mapped", null }, Queued(calls.Length + 1));
            Assert.Equal(new object[] { LOG_CATEGORY_DEBUG_MAPPING, "quiet", null }, Queued(calls.Length + 2));
        }
        Assert.Equal(new[] { "b", "mapped" }, t.HostLog);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void StartLogWritesTheRoundHeaderAndLogPerfWritesItsLineUnformatted()
    {
        var t = new LoggingTestWorld();
        t.World.Step(3);
        using (t.World.Enter())
        {
            t.Setup(round_id: 42);
            start_log("data/logs/test-round/reta.log");
            Assert.Equal(t.Stamp() + " Starting up round ID 42.\n - -------------------------\n", t.Read("data/logs/test-round/reta.log"));

            GLOB.perf_log = "data/logs/test-round/perf.log";
            object line = log_perf(Dm.list(1, "two", 3.5f));
            Assert.Equal("1,two,3.5\n", line);
            Assert.Equal("1,two,3.5\n", t.Read("data/logs/test-round/perf.log"));
        }
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ShutdownLoggingClosesTheLogsAndShutsTheLoggerDownOnce()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            shutdown_logging();
            Assert.Equal(1f, logger.shutdown);
            // log_holder.dm:159-160: a second shutdown CRASHes.
            var crash = Assert.Throws<DmCrashException>(() => shutdown_logging());
            Assert.Equal("Attempted to call shutdown_logging twice!", crash.Message);
            Assert.Equal("code/modules/logging/log_holder.dm", crash.DmFile);
            Assert.Equal(160, crash.DmLine);
        }
    }

    [Fact]
    public void ARegisterSignalOverrideWarningGoesThroughLogSignalWithoutThrowing()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            DmWorldState.Current.NotTranslatedThrows = false; // the entry's world state (log_entry.dm:90), boot package
            t.Setup();
            datum source = Dm.@new<datum>();
            datum listener = Dm.@new<datum>();
            listener.RegisterSignal(source, "logging_test_signal", nameof(datum.Destroy));
            listener.RegisterSignal(source, "logging_test_signal", nameof(datum.Topic));

            // signals.dm:41-43: log_signal(override_message), then stack_trace(override_message).
            string message = "logging_test_signal overridden. Use override = TRUE to suppress this warning.\nTarget: /datum (/datum) Existing Proc: Destroy New Proc: Topic";
            Assert.Equal(new object[] { LOG_CATEGORY_SIGNAL, message, null }, Queued(1));
            var crash = Assert.IsType<DmCrashException>(Assert.Single(t.Runtimes));
            Assert.Equal("code/datums/signals.dm", crash.DmFile);

            // Once logging is up the message lands in the signal category's files.
            logger.init_logging();
            Assert.EndsWith("] SIGNAL: logging_test_signal overridden. Use override = TRUE to suppress this warning.\n - Target: /datum (/datum) Existing Proc: Destroy New Proc: Topic\n",
                t.Read(LoggingTestWorld.LogDirectory + "/signal.log"));
        }
    }
}
