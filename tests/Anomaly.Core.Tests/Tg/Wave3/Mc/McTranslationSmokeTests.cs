using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Mc;

// Fixture subsystems of the mc package's smoke tests (rules 15: port fixtures' paths start with test_<package>_). Master/New
// creates them only in a world whose subsystem-type filter keeps them (lead decision W3 G2).

/// <summary>A ticker (wait counts ticks) that records the tick of every fire.</summary>
[DmPath("/datum/controller/subsystem/test_mc_smoke_ticker")]
public class datum_controller_subsystem_test_mc_smoke_ticker : datum_controller_subsystem
{
    public readonly List<long> fires = new();

    public datum_controller_subsystem_test_mc_smoke_ticker()
    {
        name = "Smoke Ticker";
        wait = 1;
        ss_flags = Dm.bitor(SS_TICKER, SS_NO_INIT);
    }

    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra) => fires.Add(World.Current.Tick);
}

/// <summary>A first-stage subsystem that fires in the lobby, every second.</summary>
[DmPath("/datum/controller/subsystem/test_mc_smoke_lobby")]
public class datum_controller_subsystem_test_mc_smoke_lobby : datum_controller_subsystem
{
    public readonly List<long> fires = new();

    public datum_controller_subsystem_test_mc_smoke_lobby()
    {
        name = "Smoke Lobby";
        wait = 1 * SECONDS;
        ss_flags = SS_NO_INIT;
        init_stage = INITSTAGE_FIRST;
        runlevels = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);
    }

    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra) => fires.Add(World.Current.Tick);
}

/// <summary>A main-stage subsystem that initializes after the lobby one and fires only in the game runlevels.</summary>
[DmPath("/datum/controller/subsystem/test_mc_smoke_game")]
public class datum_controller_subsystem_test_mc_smoke_game : datum_controller_subsystem
{
    public readonly List<long> fires = new();
    public long initialized_at = -1;

    public datum_controller_subsystem_test_mc_smoke_game()
    {
        name = "Smoke Game";
        wait = 2 * SECONDS;
    }

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_smoke_lobby));
    }

    public override async DmTask<object> Initialize(params object[] __extra)
    {
        initialized_at = World.Current.Tick;
        return SS_INIT_SUCCESS;
    }

    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra) => fires.Add(World.Current.Tick);
}

/// <summary>
/// Smoke tests of the mc package's translation (master.dm, subsystem.dm, controller.dm, MC.dm, _tick.dm, stoplag.dm, the
/// SSgarbage fire): the Master Controller boots a world of fixture subsystems and runs them. The behaviour tests of the
/// package (cadence, fire order, the enqueue quirk, sleeping fires, ...) are the independent test-writer's.
/// </summary>
public class McTranslationSmokeTests
{
    private static readonly Type[] SmokeTypes =
    {
        typeof(datum_controller_subsystem_test_mc_smoke_ticker),
        typeof(datum_controller_subsystem_test_mc_smoke_lobby),
        typeof(datum_controller_subsystem_test_mc_smoke_game),
        // Master.Initialize writes SSticker.timeLeft (master.dm:460) and init_subsystem tallies SSblackbox (:508).
        typeof(datum_controller_subsystem_ticker),
        typeof(datum_controller_subsystem_blackbox),
    };

    /// <summary>
    /// A world whose Master/New creates only <see cref="SmokeTypes"/>, with the GLOB vars the MC reads. The config, the
    /// logging and chat procs and world.change_fps are other packages' skeletons on this branch, so the world reports
    /// Dm.NotTranslated instead of throwing (docs/ai/specs/wave3-ownership.md); the tests assert only on the MC's own state.
    /// </summary>
    private static World NewSmokeWorld(ulong seed = 1)
    {
        var w = new World(seed);
        using (w.Enter())
        {
            DmWorldState.Current.NotTranslatedThrows = false;
            TgState.Current.SubsystemTypeFilter = t => SmokeTypes.Contains(t);
            datum_controller_global_vars glob = new datum_controller_global_vars() /* the C# constructor: GLOB's New queues a timer (globals.dm:24) */;
            TgState.Current.GLOB = glob; // tg's New sets GLOB (globals.dm:14): the boot package's
            glob.gvars_datum_init_order = Dm.list();
            glob.InitGlobalsortInstance();
            glob.InitGlobalbitflags();
            glob.InitGlobalmidnight_rollovers();
            glob.InitGlobalrollovercheck_last_timeofday();
            TgTestGenesis.Install(); // the logger, SSchat and the default configuration (Genesis, Master/New)
        }
        return w;
    }

    private static T Ss<T>() where T : datum_controller_subsystem
        => Dm.each(Master.subsystems).OfType<T>().Single();

    [Fact]
    public void MasterNewCreatesEveryFilteredSubsystemOnceStableSortedByInitStage()
    {
        World w = NewSmokeWorld();
        using (w.Enter())
        {
            datum_controller_global_vars glob = GLOB;
            datum_controller_master mc = Dm.@new<datum_controller_master>();

            Assert.Same(mc, Master);
            Assert.Same(glob, GLOB); // GLOB existed: Master/New does not create another
            Assert.NotNull(config);
            Assert.NotNull(datum_controller_master.random_seed);
            Assert.Equal(SmokeTypes.Length, mc.subsystems.len);
            Assert.Equal(SmokeTypes.OrderBy(t => t.Name), Dm.each(mc.subsystems).Select(s => s.GetType()).OrderBy(t => t.Name));
            // cmp_subsystem_init_stage, stable: the one INITSTAGE_FIRST subsystem first.
            Assert.IsType<datum_controller_subsystem_test_mc_smoke_lobby>(mc.subsystems[1]);
            // SUBSYSTEM_DEF's New registered the stubs' globals.
            Assert.Same(Ss<datum_controller_subsystem_ticker>(), SSticker);
            Assert.Same(Ss<datum_controller_subsystem_blackbox>(), SSblackbox);
        }
        Assert.Equal(0, w.RuntimeErrorCount);
    }

    [Fact]
    public void TheMasterBootsInitializesInDependencyOrderAndFiresBySchedule()
    {
        World w = NewSmokeWorld();
        var runtimes = new List<Exception>();
        w.RuntimeErrorSink = runtimes.Add;
        datum_controller_master mc;
        using (w.Enter())
        {
            mc = Dm.@new<datum_controller_master>();
            // M10 slice F translated the ticker; here it is only what Master.Initialize writes, so it neither initializes nor fires
            SSticker.ss_flags = Dm.bitor(SSticker.ss_flags, SS_NO_INIT | SS_NO_FIRE);
            // world/New's call (game/world.dm:141): the set waitfor = 0 wrapper returns at the body's sleep(10).
            Assert.Null(Waiting.NoSleep(mc.Initialize(10, FALSE, TRUE)));
            Assert.Null(datum_controller_master.init_stage_completed);
        }

        w.Step(19);
        using (w.Enter())
            Assert.Null(datum_controller_master.init_stage_completed); // sleep(10) is 20 ticks

        w.Step(1); // tick 20: every stage initializes in one burst (CHECK_TICK never sleeps, DV3)
        using (w.Enter())
        {
            var lobby = Ss<datum_controller_subsystem_test_mc_smoke_lobby>();
            var game = Ss<datum_controller_subsystem_test_mc_smoke_game>();
            var ticker = Ss<datum_controller_subsystem_test_mc_smoke_ticker>();
            Assert.True(Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX));
            Assert.True(Dm.eq(mc.current_runlevel, 1)); // SetRunLevel(1) after the first stage (master.dm:449)
            Assert.Equal(20, game.initialized_at);
            Assert.Equal(TRUE, game.initialized);
            Assert.Equal(TRUE, lobby.initialized); // SS_NO_INIT: marked initialized without Initialize
            Assert.True(lobby.init_order < game.init_order); // the dependency initializes first
            Assert.Equal(new long[] { 20 }, lobby.fires); // Loop(1) runs the first stage at once (next_fire 0)
            Assert.Empty(ticker.fires); // a main-stage ticker waits for Loop(4)
            Assert.NotNull(Failsafe); // Loop created the Failsafe skeleton (master.dm:701-702)
            Assert.True(MC_RUNNING()); // processing, a runlevel and every stage initialized
        }

        w.Step(80); // to tick 100
        using (w.Enter())
        {
            var lobby = Ss<datum_controller_subsystem_test_mc_smoke_lobby>();
            var game = Ss<datum_controller_subsystem_test_mc_smoke_game>();
            var ticker = Ss<datum_controller_subsystem_test_mc_smoke_ticker>();
            // a ticker fires every tick from its first fire on (wait 1 tick)
            Assert.NotEmpty(ticker.fires);
            Assert.InRange(ticker.fires[0], 21, 22); // Loop(4) staggers tickers by TICKS2DS(rand(0, 1)) (master.dm:610)
            Assert.Equal(Enumerable.Range((int)ticker.fires[0], 100 - (int)ticker.fires[0] + 1).Select(t => (long)t), ticker.fires);
            // a 1 s subsystem fires every 20 ticks from its queued time (subsystem.dm:197)
            Assert.Equal(new long[] { 20, 40, 60, 80, 100 }, lobby.fires);
            Assert.Empty(game.fires); // not in the lobby runlevel
            mc.SetRunLevel(RUNLEVEL_GAME); // the stand-in for SSticker (scope R6)
        }

        w.Step(200);
        using (w.Enter())
        {
            var game = Ss<datum_controller_subsystem_test_mc_smoke_game>();
            Assert.NotEmpty(game.fires);
            Assert.InRange(game.fires[0], 101, 141); // a random offset of up to 2 s when it joins (master.dm:722)
            for (int i = 1; i < game.fires.Count; i++)
                Assert.Equal(40, game.fires[i] - game.fires[i - 1]); // 2 s
        }

        // The only runtimes are the reports of other packages' skeletons on this branch (each reported once per site).
        Assert.All(runtimes, e => Assert.IsType<DmNotTranslatedException>(e));
        var sites = runtimes.Select(e => ((DmNotTranslatedException)e).Where).ToList();
        Assert.Equal(sites.Distinct().Count(), sites.Count);
        Assert.All(sites, where => Assert.True(
            where.StartsWith("code/controllers/configuration/", StringComparison.Ordinal) // config.Get (boot)
            || where.StartsWith("code/__HELPERS/logging/", StringComparison.Ordinal)     // log_world, warning (logging)
            || where.StartsWith("code/modules/tgchat/", StringComparison.Ordinal)        // to_chat (logging)
            || where.StartsWith("code/game/world.dm", StringComparison.Ordinal)          // world.change_fps (boot)
            || where == "code/controllers/master.dm:1045",                               // AttemptProfileDump: config.Get reads null here
            where));
    }

    [Fact]
    public void TheSameBootTwiceGivesTheSameWorldState()
    {
        ulong Run()
        {
            World w = NewSmokeWorld(7);
            using (w.Enter())
            {
                datum_controller_master mc = Dm.@new<datum_controller_master>();
                Waiting.NoSleep(mc.Initialize(10, FALSE, TRUE));
            }
            w.Step(120);
            using (w.Enter())
                Master.SetRunLevel(RUNLEVEL_GAME);
            w.Step(120);
            return w.ComputeStateHash();
        }
        Assert.Equal(Run(), Run());
    }

    [Theory]
    [InlineData(RUNLEVEL_LOBBY, 1)]
    [InlineData(RUNLEVEL_SETUP, 2)]
    [InlineData(RUNLEVEL_GAME, 3)]
    [InlineData(RUNLEVEL_POSTGAME, 4)]
    public void SetRunLevelTurnsTheFlagIntoAListIndex(int flag, int index)
    {
        var w = new World();
        using (w.Enter())
        {
            var mc = new datum_controller_master();
            mc.SetRunLevel(flag);
            Assert.True(Dm.eq(mc.current_runlevel, index));
        }
    }

    [Fact]
    public void SetRunLevelCrashesOnAFlagBelowOneAndKeepsTheOldRunlevel()
    {
        var w = new World();
        using (w.Enter())
        {
            var mc = new datum_controller_master();
            mc.SetRunLevel(RUNLEVEL_GAME);
            // log(2, 0.25) + 1 == -1 < 1 (master.dm:559-561)
            DmCrashException e = Assert.Throws<DmCrashException>(() => mc.SetRunLevel(0.25f));
            Assert.Contains("Attempted to set invalid runlevel: 0.25", e.Message);
            Assert.True(Dm.eq(mc.current_runlevel, 3));
        }
    }

    [Fact]
    public void StoplagSleepsOneTickUntilTheMcIsUpThenTheRequestedDelay()
    {
        var w = new World();
        DmTask<object> before, after;
        using (w.Enter())
            before = stoplag(5);
        w.Step(1);
        Assert.True(Dm.eq(before.TryGetResult(out object r1) ? r1 : null, 1)); // no Master: one tick, returns 1 (stoplag.dm:8-10)

        using (w.Enter())
        {
            Master = new datum_controller_master();
            datum_controller_master.init_stage_completed = INITSTAGE_MAX;
            after = stoplag(2); // DS2TICKS(2) = 4 ticks, DELTA_CALC 1 with tick usage 0 (DV3): one pass of the do-while
        }
        w.Step(3);
        Assert.False(after.IsCompleted);
        w.Step(1);
        Assert.True(Dm.eq(after.TryGetResult(out object r2) ? r2 : null, 4));
    }

    [Fact]
    public void TheTickChecksAreInertWithTheVirtualTickUsage()
    {
        var w = new World();
        using (w.Enter())
        {
            Assert.False(TICK_CHECK()); // 0 > 98
            CHECK_TICK(); // never reaches stoplag
            Assert.False(TICK_CHECK_HIGH_PRIORITY());
            Assert.Equal(98f, TICK_LIMIT_RUNNING());
            Assert.Equal(0f, TICK_USAGE());
            Assert.Equal(2f, TICKS2DS(4));
            Assert.Equal(4f, DS2TICKS(2));
            Assert.Equal(1.5f, CEILING(1.2f, 0.5f));
            Assert.Equal(2f, ROUND_UP(1.2f));
            Assert.Equal(1f, WRAP_UP(SHORT_REAL_LIMIT - 1, SHORT_REAL_LIMIT - 1));
            Assert.Equal(180f, SIMPLIFY_DEGREES(540)); // %% keeps the dividend's sign (dm-conformance.md): -180 stays -180
        }
    }

    [Fact]
    public void RealtimeofdayCountsMidnightRollovers()
    {
        World w = NewSmokeWorld();
        using (w.Enter())
        {
            Assert.Equal(Dm.world.timeofday, REALTIMEOFDAY()); // the first check records timeofday, no rollover
            Assert.Equal(Dm.world.timeofday, GLOB.rollovercheck_last_timeofday);
            GLOB.rollovercheck_last_timeofday = Dm.world.timeofday + 1; // time went backwards: a midnight passed
            Assert.Equal(Dm.world.timeofday + MIDNIGHT_ROLLOVER, REALTIMEOFDAY());
            Assert.Equal(1f, GLOB.midnight_rollovers);
        }
    }

    [Fact]
    public void GarbageFiresOverItsEmptyQueuesWithoutReachingAnUntranslatedBranch()
    {
        var w = new World(); // NotTranslated throws (the default)
        using (w.Enter())
        {
            var gc = Dm.@new<datum_controller_subsystem_garbage>();
            gc.state = SS_RUNNING; // as RunQueue leaves it (master.dm:896)
            gc.delslasttick = 3;
            Waiting.NoSleep(gc.fire());
            Assert.True(Dm.eq(gc.state, SS_RUNNING));
            Assert.Equal(0f, gc.delslasttick); // HandleQueue(GC_QUEUE_FILTER) resets the per-tick counters
            Assert.All(Dm.each(gc.queues), q => Assert.Equal(0f, Dm.length(q)));
        }
        Assert.Equal(0, w.RuntimeErrorCount);
    }

    [Fact]
    public void ServerTimestampFormatsTheWorldTimeOfDay()
    {
        var w = new World();
        using (w.Enter())
        {
            Assert.Equal(Dm.time2text(Dm.world.timeofday, "hh:mm:ss", 0), server_timestamp());
            Assert.Equal(Dm.time2text(Dm.world.timeofday, "YYYY", 0), server_timestamp("YYYY"));
        }
    }
}
