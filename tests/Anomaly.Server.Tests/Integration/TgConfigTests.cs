using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The M2 integration's hand-over of tg's game options to tg's own configuration: the host writes RUN_DELAY and WALK_DELAY
/// into the world's <c>config/config.txt</c>, which <c>world/New</c> loads (configuration.dm:88), so a player's walk and run
/// speeds are tg's (the <c>config_walk_run</c> movespeed modifiers, mobs.dm:58-64).
/// </summary>
public class TgConfigTests
{
    [Fact]
    public void TgsConfigurationReadsTheHostsRunAndWalkDelays()
    {
        using var server = TestServer.Start();
        server.Join("Alice");
        using (server.World.Enter())
        {
            Assert.Equal(1.5f, Dm.num(GlobalProcs.config.Get(typeof(datum_config_entry_number_movedelay_run_delay))));
            Assert.Equal(4f, Dm.num(GlobalProcs.config.Get(typeof(datum_config_entry_number_movedelay_walk_delay))));
            var alice = (mob_living)server.MobOf("Alice");
            Assert.Equal(1.5f, Dm.num(alice.cached_multiplicative_slowdown)); // a fresh player runs (tg's default move intent)
        }
    }

    // gaps R6 (D332): with the real date the world's clock starts at the host's wall clock and tg's holidays are on; the tests' default
    // config keeps the fixed 2000-01-01 clock and no holidays
    [Fact]
    public void TheRealDateStartsTheClockAtTheWallClockWithTheHolidays()
    {
        double before = ServerHost.RealDateNow();
        using var server = TestServer.Start(new ServerConfig { Seed = 7, RoomWidth = TestServer.RoomSize, RoomHeight = TestServer.RoomSize, RoomSpace = 0, Console = false, StatusEveryTicks = 1000, RealDate = true });
        double after = ServerHost.RealDateNow();
        Assert.InRange(server.Host.Log.RealtimeStart, before, after);
        using (server.World.Enter())
        {
            Assert.InRange(DmWorldState.Current.RealtimeStart, before, after);
            Assert.True(Dm.truthy(GlobalProcs.config.Get(typeof(datum_config_entry_flag_allow_holidays))));
            Assert.Equal(DateTime.UtcNow.Year.ToString(System.Globalization.CultureInfo.InvariantCulture), Dm.time2text(Dm.world.realtime, "YYYY", 0));
            Assert.NotNull(GlobalProcs.GLOB.holidays); // SSevents filled the day's holidays, if any
        }
        using var fixedClock = TestServer.Start();
        using (fixedClock.World.Enter())
        {
            Assert.Equal(0d, DmWorldState.Current.RealtimeStart);
            Assert.False(Dm.truthy(GlobalProcs.config.Get(typeof(datum_config_entry_flag_allow_holidays))));
        }
    }

    // M5x slice 1: the host writes tg's shipped REACTIONARY_EXPLOSIONS and BOMBCAP 20 (game_options.txt:436, :447); bombcap's
    // ValidateAndSet (game_options.dm:373) sets the explosion caps to (5, 10, 20), flash and flame 20.
    [Fact]
    public void TheHostSetsTgsShippedReactionaryExplosionsAndBombCap()
    {
        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Assert.True(Dm.truthy(GlobalProcs.config.Get(typeof(datum_config_entry_flag_reactionary_explosions))));
            Assert.Equal(20f, Dm.num(GlobalProcs.config.Get(typeof(datum_config_entry_number_bombcap))));
            Assert.Equal(5f, GlobalProcs.GLOB.MAX_EX_DEVESTATION_RANGE);
            Assert.Equal(10f, GlobalProcs.GLOB.MAX_EX_HEAVY_RANGE);
            Assert.Equal(20f, GlobalProcs.GLOB.MAX_EX_LIGHT_RANGE);
            Assert.Equal(20f, GlobalProcs.GLOB.MAX_EX_FLASH_RANGE);
            Assert.Equal(20f, GlobalProcs.GLOB.MAX_EX_FLAME_RANGE);
        }
    }

    // The owner (2026-11-08): tg's crew-death shuttle call at its suggested 20% (game_options.txt:480) and players' restart votes (config.txt:117)
    [Fact]
    public void TheHostTurnsOnTheCrewDeathShuttleCallAndRestartVotes()
    {
        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Assert.Equal(0.2f, Dm.num(GlobalProcs.config.Get(typeof(datum_config_entry_number_emergency_shuttle_autocall_threshold))));
            Assert.True(Dm.truthy(GlobalProcs.config.Get(typeof(datum_config_entry_flag_allow_vote_restart))));
        }
    }

    // D270, D271: the lobby's jobs.txt closes every job but the station engineer; with --open-jobs none (the heads opened with D274, the
    // silicons with D326)
    [Fact]
    public void TheLobbysJobsTextClosesEveryJobButTheEngineerOrWithOpenJobsNone()
    {
        ServerConfig config = ServerConfig.Parse(new[] { "--ticks", "10", "--lobby", "--open-jobs" }, _ => null, out string error);
        Assert.Null(error);
        Assert.True(config.Lobby && config.OpenJobs);
        string[] closed = ServerHost.TgJobsText().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("Medical Doctor=0,0", closed);
        Assert.DoesNotContain("Station Engineer=0,0", closed);
        string[] open = ServerHost.TgJobsText(openJobs: true).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(ServerHost.ClosedOpenJobs.Select(t => t + "=0,0").Append("Cyborg=3,3").OrderBy(l => l, StringComparer.Ordinal), open.OrderBy(l => l, StringComparer.Ordinal)); // DV426
    }

    [Fact]
    public void TheDelaysAreOptionsOfTheServerAndOfItsReplayLog()
    {
        ServerConfig config = ServerConfig.Parse(new[] { "--ticks", "10", "--run-delay", "2", "--walk-delay=5.5" }, _ => null, out string error);
        Assert.Null(error);
        Assert.Equal((2f, 5.5f), (config.RunDelay, config.WalkDelay));
        Assert.Equal("RUN_DELAY 2\nWALK_DELAY 5.5\nREACTIONARY_EXPLOSIONS\nBOMBCAP 20\nOOC_DURING_ROUND\nPROTECT_ROLES_FROM_ANTAGONIST\nALLOW_LATEJOIN_ANTAGONISTS\nDYNAMIC_CONFIG_ENABLED\nALLOW_AI\nALLOW_AI_MULTICAM\nALLOW_RANDOM_EVENTS\nEMERGENCY_SHUTTLE_AUTOCALL_THRESHOLD 0.2\nALLOW_VOTE_RESTART\nCOMMENDATION_PERCENT_POLL 0.05\n" + ServerHost.RoundstartRaces(config) + "FORBID_STATION_TRAITS\n", ServerHost.TgConfigText(config));
        Assert.StartsWith("ROUNDSTART_RACES human\nROUNDSTART_RACES felinid\nROUNDSTART_RACES lizard\n", ServerHost.RoundstartRaces(config)); // tg's list order (D349)
        CommandLog log = CommandLog.Parse(CommandLog.For(config).ToText(), out error);
        Assert.Null(error);
        Assert.Equal((2f, 5.5f), (log.ReplayConfig().RunDelay, log.ReplayConfig().WalkDelay));
        Assert.Null(ServerConfig.Parse(new[] { "--ticks", "10", "--run-delay", "0" }, _ => null, out error));
        Assert.Contains("--run-delay expects", error);
        ServerConfig defaults = ServerConfig.Parse(new[] { "--ticks", "10" }, _ => null, out _);
        Assert.Equal((ServerConfig.DefaultRunDelay, ServerConfig.DefaultWalkDelay), (defaults.RunDelay, defaults.WalkDelay));
    }
}
