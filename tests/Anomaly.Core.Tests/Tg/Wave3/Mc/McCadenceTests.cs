using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Mc;

/// <summary>
/// When the Master Controller fires a subsystem, by its flags and wait (controllers.md section 11, golden test a), written
/// from subsystem.dm:179-197 (update_nextfire), master.dm:796-826 (CheckQueue) and master.dm:588-793 (Loop). Every fixture
/// initializes at INITSTAGE_LAST, so Loop(4) leaves its next_fire at 0 (master.dm:616: no random offset when init_stage is
/// not init_stage - 1) and it fires on Loop(4)'s first iteration, tick 1 (McWorld.Boot). World time is 0.5 ds per tick.
/// </summary>
public class McCadenceTests
{
    private static readonly float LobbyAndDefault = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);

    private static Action<McFixtureSubsystem> Fires(float flags, float wait, float priority = FIRE_PRIORITY_DEFAULT)
        => ss =>
        {
            ss.ss_flags = Dm.bitor(flags, SS_NO_INIT);
            ss.wait = wait;
            ss.priority = priority;
            ss.init_stage = INITSTAGE_LAST;
            ss.runlevels = LobbyAndDefault;
        };

    /// <summary>The ticks first, first + period, ... up to last.</summary>
    internal static List<long> Every(long first, long period, long last)
    {
        var ticks = new List<long>();
        for (long t = first; t <= last; t += period)
            ticks.Add(t);
        return ticks;
    }

    [Fact]
    public void GoldenA_EachTimingFlagFiresOnItsOwnCadenceOver400Ticks()
    {
        var w = new McWorld();
        var ticker1 = w.Add<datum_controller_subsystem_test_mc_a>("Ticker One", Fires(SS_TICKER, 1));
        var ticker2 = w.Add<datum_controller_subsystem_test_mc_b>("Ticker Two", Fires(SS_TICKER, 2));
        var bg_ticker = w.Add<datum_controller_subsystem_test_mc_c>("Background Ticker", Fires(Dm.bitor(SS_TICKER, SS_BACKGROUND), 2));
        var plain = w.Add<datum_controller_subsystem_test_mc_d>("Plain", Fires(NONE, 20));
        var plain10 = w.Add<datum_controller_subsystem_test_mc_e>("Plain Ten", Fires(NONE, 10));
        var keep = w.Add<datum_controller_subsystem_test_mc_f>("Keep Timing", Fires(SS_KEEP_TIMING, 20));
        var post = w.Add<datum_controller_subsystem_test_mc_g>("Post Fire", Fires(Dm.bitor(SS_POST_FIRE_TIMING, SS_BACKGROUND), 10)); // SSprocessing's flags
        var fast = w.Add<datum_controller_subsystem_test_mc_h>("Fast", Fires(Dm.bitor(SS_POST_FIRE_TIMING, SS_BACKGROUND), 2)); // SSfastprocess
        var bg = w.Add<datum_controller_subsystem_test_mc_i>("Background", Fires(SS_BACKGROUND, 20));
        var no_fire = w.Add<datum_controller_subsystem_test_mc_j>("No Fire", Fires(SS_NO_FIRE, 20));
        var cannot = w.Add<datum_controller_subsystem_test_mc_k>("Cannot Fire", Fires(NONE, 20));
        using (w.World.Enter())
            cannot.can_fire = FALSE;

        w.Boot();
        w.Step(400); // ticks 1..400

        // queued_time + wait (subsystem.dm:197): every wait ds from the first fire
        Assert.Equal(Every(1, 40, 400), plain.fires);
        Assert.Equal(Every(1, 20, 400), plain10.fires);
        Assert.Equal(Every(1, 40, 400), bg.fires);
        // world.time + wait (:193): SSprocessing every 20 ticks, SSfastprocess every 4 (controllers.md 3.7)
        Assert.Equal(Every(1, 20, 400), post.fires);
        Assert.Equal(Every(1, 4, 400), fast.fires);
        // next_fire += wait (:195) from next_fire 0, and CheckQueue holds a KEEP_TIMING subsystem until last_fire + 0.75 wait
        // (master.dm:817), last_fire 0 included: the first fire waits for 15 ds, then it fires every 15 ds until the
        // absolute schedule 60, 80, ... ds is ahead of last_fire + 15 ds
        Assert.Equal(new long[] { 30, 60, 90 }.Concat(Every(120, 40, 400)), keep.fires);
        // a ticker's wait counts ticks (:191); a background ticker goes through the runlevel lists (master.dm:607) and keeps
        // next_fire 0, so it fires at once
        Assert.Equal(Every(1, 2, 400), bg_ticker.fires);
        // Loop staggers the non-background tickers by TICKS2DS(rand(0, 1)) each, cumulatively, in the subsystems' display
        // order (master.dm:610-611): Ticker One draws first.
        Assert.InRange(ticker1.fires[0], 1, 2);
        Assert.InRange(ticker2.fires[0], ticker1.fires[0], ticker1.fires[0] + 1);
        Assert.Equal(Every(ticker1.fires[0], 1, 400), ticker1.fires);
        Assert.Equal(Every(ticker2.fires[0], 2, 400), ticker2.fires);
        Assert.Empty(no_fire.fires);
        Assert.Empty(cannot.fires);

        using (w.World.Enter())
        {
            foreach (McFixtureSubsystem ss in new McFixtureSubsystem[] { ticker1, ticker2, bg_ticker, plain, plain10, keep, post, fast, bg })
            {
                Assert.Equal(ss.fires.Count, ss.times_fired);
                Assert.Equal(ss.fires[^1] * World.TickLag, ss.last_fire);
                Assert.True(Dm.eq(ss.state, SS_IDLE));
                Assert.Equal(0f, ss.tick_overrun); // the tick usage is 0 (DV3)
            }
            Assert.Equal(0f, no_fire.times_fired);
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void KeepTimingCatchesUpAfterAPauseFiringAtMostAQuarterFaster()
    {
        var w = new McWorld();
        var keep = w.Add<datum_controller_subsystem_test_mc_a>("Keep", Fires(SS_KEEP_TIMING, 10));
        var plain = w.Add<datum_controller_subsystem_test_mc_b>("Plain", Fires(NONE, 10));
        w.Boot();
        w.Step(50);
        using (w.World.Enter())
        {
            keep.can_fire = FALSE;
            plain.can_fire = FALSE;
        }
        w.Step(100);
        using (w.World.Enter())
        {
            keep.can_fire = TRUE;
            plain.can_fire = TRUE;
        }
        w.Step(320); // to tick 470

        // keep: next_fire += 10 ds per fire (subsystem.dm:195) while CheckQueue holds it until last_fire + 7.5 ds
        // (master.dm:817): one fire every 15 ticks until next_fire is ahead again, then every 20 ticks on the old schedule
        Assert.Equal(new long[]
        {
            15, 30, 45, // the first fire waits for last_fire (0) + 7.5 ds
            151, 166, 181, 196, 211, 226, 241, 256, 271, 286, 301, 316, 331, 346, 361, 376, 391, 406, 421,
            440, 460,
        }, keep.fires);
        // plain: queued_time + 10 ds, a new schedule from the first fire after the pause
        Assert.Equal(new long[] { 1, 21, 41 }.Concat(Every(151, 20, 470)), plain.fires);
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void TgQuirk_PostponeSkipsWholeCyclesExceptForDefaultTimingWhereEachSkipCostsOneTick()
    {
        var w = new McWorld();
        var plain = w.Add<datum_controller_subsystem_test_mc_a>("Plain", Fires(NONE, 10));
        var keep = w.Add<datum_controller_subsystem_test_mc_b>("Keep", Fires(SS_KEEP_TIMING, 10));
        var post = w.Add<datum_controller_subsystem_test_mc_c>("Post Fire", Fires(Dm.bitor(SS_POST_FIRE_TIMING, SS_BACKGROUND), 10));
        var ticker = w.Add<datum_controller_subsystem_test_mc_d>("Ticker", Fires(SS_TICKER, 1));
        w.Boot();
        w.Step(41);
        using (w.World.Enter())
        {
            foreach (McFixtureSubsystem ss in new McFixtureSubsystem[] { plain, keep, post, ticker })
                ss.postpone(2);
            Assert.Equal(2f, plain.postponed_fires);
        }
        w.Step(100); // to tick 141

        // A postponed fire calls update_nextfire() (master.dm:820-822). For the default timing that is queued_time + wait,
        // and queued_time is 0 between runs (RunQueue zeroes it, master.dm:950, and dequeue does, subsystem.dm:270), so
        // next_fire falls into the past and each postponed fire costs one tick instead of one cycle.
        Assert.Equal(new long[] { 1, 21, 41, 63, 83, 103, 123 }, plain.fires);
        Assert.Equal(new long[] { 15, 30, 80, 100, 120, 140 }, keep.fires); // next_fire += wait per skip: 22.5 and 30 ds skipped
        Assert.Equal(new long[] { 1, 21, 41, 101, 121, 141 }, post.fires); // world.time + wait per skip
        Assert.Contains(41L, ticker.fires);
        Assert.DoesNotContain(42L, ticker.fires);
        Assert.DoesNotContain(43L, ticker.fires);
        Assert.Equal(Every(44, 1, 141), ticker.fires.Where(t => t >= 42).ToList());
        using (w.World.Enter())
            Assert.Equal(0f, plain.postponed_fires);
    }

    [Fact]
    public void WhileProcessingIsZeroTheLoopChecksOncePerSecondAndFiresNothing()
    {
        var w = new McWorld();
        var plain = w.Add<datum_controller_subsystem_test_mc_a>("Plain", Fires(NONE, 10));
        var ticker = w.Add<datum_controller_subsystem_test_mc_b>("Ticker", Fires(SS_TICKER, 1));
        w.Boot();
        w.Step(30);
        using (w.World.Enter())
            w.Mc.processing = 0;
        w.Step(70); // to tick 100: the Loop runs at 31 and sleeps 1 SECONDS per check (master.dm:670-673): 51, 71, 91, 111
        using (w.World.Enter())
        {
            Assert.DoesNotContain(ticker.fires, t => t > 30);
            w.Mc.processing = 1;
        }
        w.Step(40); // to tick 140

        Assert.Equal(new long[] { 1, 21, 111, 131 }, plain.fires);
        Assert.DoesNotContain(ticker.fires, t => t > 30 && t < 111);
        Assert.Equal(Every(111, 1, 140), ticker.fires.Where(t => t > 30).ToList());
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void AProcessingValueAboveOneMultipliesTheMastersSleep()
    {
        var w = new McWorld();
        var plain = w.Add<datum_controller_subsystem_test_mc_a>("Plain", Fires(NONE, 10));
        var ticker = w.Add<datum_controller_subsystem_test_mc_b>("Ticker", Fires(SS_TICKER, 1));
        w.Boot();
        w.Step(10);
        using (w.World.Enter())
            w.Mc.processing = 2; // "higher values increase the processing delay by n ticks" (master.dm:15)
        w.Step(50); // to tick 60

        // sleep(world.tick_lag * (processing * sleep_delta)) (master.dm:793): from the iteration in tick 11 on, every
        // second tick; Plain's 20-tick schedule falls on those ticks
        Assert.Equal(Every(11, 2, 60), ticker.fires.Where(t => t >= 11).ToList());
        Assert.Equal(new long[] { 1, 21, 41 }, plain.fires);
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void ALaggyMapUpdateRunsOnlyTheNonBackgroundTickersForOneIteration()
    {
        var w = new McWorld();
        var plain = w.Add<datum_controller_subsystem_test_mc_a>("Plain", Fires(NONE, 10));
        var ticker = w.Add<datum_controller_subsystem_test_mc_b>("Ticker", Fires(SS_TICKER, 1));
        var bg_ticker = w.Add<datum_controller_subsystem_test_mc_c>("Background Ticker", Fires(Dm.bitor(SS_TICKER, SS_BACKGROUND), 1));
        w.Boot();
        w.Step(40);
        using (w.World.Enter())
        {
            w.Mc.laggy_byond_map_update_incoming();
            w.Mc.laggy_byond_map_update_incoming(); // only sets skip_ticks when it is 0 (master.dm:1004)
            Assert.Equal(1f, w.Mc.skip_ticks);
        }
        w.Step(30); // to tick 70

        // tick 41 checks only tickersubsystems (master.dm:725), the non-background tickers (:607)
        Assert.Equal(new long[] { 1, 21, 42, 62 }, plain.fires);
        Assert.Contains(41L, ticker.fires);
        Assert.Equal(Every(1, 1, 70).Where(t => t != 41), bg_ticker.fires);
        using (w.World.Enter())
            Assert.Equal(0f, w.Mc.skip_ticks);
    }

    [Fact]
    public void AFireRuntimeEndsOnlyThatFireAndTheSubsystemKeepsItsSchedule()
    {
        var w = new McWorld();
        var crasher = w.Add<datum_controller_subsystem_test_mc_a>("Crasher", Fires(NONE, 10));
        var plain = w.Add<datum_controller_subsystem_test_mc_b>("Plain", Fires(NONE, 10));
        using (w.World.Enter())
            crasher.fire_crashes = TRUE;
        w.Boot();
        w.Step(1);
        float iteration_before;
        using (w.World.Enter())
            iteration_before = w.Mc.iteration;
        w.Step(99);

        // ignite's fire() call is an isolation boundary (rules 9.2); ignite goes on with '. = state' (subsystem.dm:149),
        // which RunQueue maps from SS_RUNNING to SS_IDLE (master.dm:917-918)
        Assert.Equal(Every(1, 20, 100), crasher.fires);
        Assert.Equal(Every(1, 20, 100), plain.fires);
        Assert.Equal(5, w.DmRuntimes.Count);
        Assert.All(w.DmRuntimes, e => Assert.Equal("Crasher crashed in fire", Assert.IsType<DmCrashException>(e).Message));
        using (w.World.Enter())
        {
            Assert.Equal(5f, crasher.times_fired);
            Assert.True(Dm.eq(crasher.state, SS_IDLE));
            Assert.Same(w.Mc, Master);
            Assert.Equal(iteration_before + 99, w.Mc.iteration); // the same Loop(4) ran every tick
        }
    }

    [Fact]
    public void TheBaseFireCrashesOnceAndSetsSsNoFireSoCheckQueueDropsTheSubsystem()
    {
        var w = new McWorld();
        var lazy = w.Add<datum_controller_subsystem_test_mc_a>("Lazy", Fires(NONE, 10));
        using (w.World.Enter())
            lazy.uses_base_fire = TRUE;
        w.Boot();
        w.Step(100);

        Assert.Equal(new long[] { 1 }, lazy.fires);
        Exception e = Assert.Single(w.DmRuntimes);
        Assert.Contains("does not fire() but did not set the SS_NO_FIRE flag", Assert.IsType<DmCrashException>(e).Message);
        using (w.World.Enter())
        {
            Assert.True(Dm.truthy(Dm.bitand(lazy.ss_flags, SS_NO_FIRE))); // subsystem.dm:164
            Assert.Equal(1f, lazy.times_fired);
        }
    }

    [Fact]
    public void SsGarbageIsTheFirstSubsystemTheMcFiresFromLoopOneAtTickZero()
    {
        var w = new McWorld();
        datum_controller_subsystem_garbage gc;
        using (w.World.Enter())
        {
            gc = Dm.@new<datum_controller_subsystem_garbage>();
            Dm.append(ref w.Mc.subsystems, gc);
        }
        var plain = w.Add<datum_controller_subsystem_test_mc_a>("Plain", Fires(NONE, 10));

        w.Boot(); // tick 0: stage 1 (SSgarbage, SS_NO_INIT) then Loop(1), which runs only init_stage <= 1 (master.dm:601)
        using (w.World.Enter())
        {
            Assert.Equal(1f, gc.times_fired);
            Assert.Equal(0f, gc.last_fire);
            Assert.Equal(20f, gc.next_fire); // SS_POST_FIRE_TIMING: world.time + 2 SECONDS
            Assert.Empty(plain.fires);
        }
        w.Step(100);
        using (w.World.Enter())
        {
            // Loop(4) keeps its next_fire (init_stage 1 is not 4 - 1): it fires again at 20 ds and 40 ds
            Assert.Equal(3f, gc.times_fired);
            Assert.Equal(40f, gc.last_fire);
            Assert.True(Dm.eq(gc.state, SS_IDLE));
        }
        Assert.Equal(Every(1, 20, 100), plain.fires);
        Assert.Empty(w.DmRuntimes);
    }
}
