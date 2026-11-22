using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.UnitTests;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Boot;

/// <summary>
/// The whole tg boot through <see cref="TgWorld.Boot"/> (scope.md section 4, tg's init order in code/game/world.dm:11-56):
/// Genesis creates the logger, the Master with every subsystem and GLOB; world/New loads the configuration and calls
/// Master.Initialize, which sleeps 10 ds; at tick 20 the subsystems initialize and the MC fires for the first time (SSgarbage,
/// the only lobby subsystem of the first stage), from tick 21 every subsystem is scheduled. The integration tests need every
/// wave-3 package (mc, timers and processing, logging and chat, atoms, engine): they are skipped on the boot package's branch
/// and un-skipped by the lead at the wave-3 merge.
/// </summary>
public class BootIntegrationTests
{

    private static datum_controller_subsystem[] Subsystems() => Dm.each_cast<datum_controller_subsystem>(Master.subsystems).ToArray();

    [Fact]
    public void BootToFirstFire()
    {
        World w = TgTestWorld.Start(out TgTestWorld.Log log);
        using (w.Enter())
        {
            // Tick 0: static initialization (Genesis) and world/New up to the first sleep of Master.Initialize.
            Assert.Equal(0, w.Tick);
            Assert.NotNull(logger);
            Assert.NotNull(Master);
            Assert.NotNull(GLOB__var);
            Assert.True(Dm.truthy(config.loaded));
            Assert.Same(SSgarbage, Subsystems().Single(s => s is datum_controller_subsystem_garbage));
            Assert.Same(SStimer, Subsystems().Single(s => s.GetType() == typeof(datum_controller_subsystem_timer))); // SSsound_loops is a timer subtype (M4b audio slice 5)
            Assert.All(Subsystems(), s => Assert.Equal(0f, s.times_fired));
            Assert.Contains(log.Lines, l => l.Contains("World loaded at", StringComparison.Ordinal));
        }

        var firstFire = new Dictionary<Type, long>();
        for (int i = 0; i < 40; i++)
        {
            w.Step();
            using (w.Enter())
            {
                foreach (datum_controller_subsystem s in Subsystems())
                {
                    if (s.times_fired > 0 && !firstFire.ContainsKey(s.GetType()))
                        firstFire[s.GetType()] = w.Tick;
                }
                if (w.Tick < 20)
                    Assert.False(TgTestWorld.Initialized(w));
            }
        }

        // Master.Initialize slept 10 ds (20 ticks) and initialized every stage in tick 20 (CHECK_TICK never sleeps, DV3).
        Assert.True(TgTestWorld.Initialized(w));
        Assert.Equal(20, firstFire.Values.Min());
        Assert.Equal(20, firstFire[typeof(datum_controller_subsystem_garbage)]);
        using (w.Enter())
        {
            // Loop(1) schedules only the first stage in tick 20 (master.dm:601). SSair counts one times_fired in its Initialize
            // (setup_allturfs, air.dm:509) without firing, so it is not a fire here.
            foreach ((Type type, long tick) in firstFire.Where(f => f.Value == 20 && f.Key != typeof(datum_controller_subsystem_air)))
                Assert.True(Subsystems().Single(s => s.GetType() == type).init_stage <= INITSTAGE_FIRST, type.Name);
        }
        // From tick 21 Loop(4) schedules every subsystem. The tickers' start offsets accumulate: `timer += TICKS2DS(rand(0, 1))`
        // per ticker in subsystem order (master.dm:595, :609-611), and SSchat, SSinput (a ticker since the client layer, M1
        // P1: input.dm:4, which sorts before SStimer as the Master orders its subsystems), SStimed_actions and SSicon_smooth
        // (a ticker since M3 P4 translated it: icon_smooth.dm:8) come before SStimer, so SStimer starts 0 to 5 ticks after
        // tick 21. SSverb_manager, the other ticker of M1, comes after it. M4a adds two tickers that may sort before SStimer:
        // SSmouse_entered (P1: mouse_entered.dm:5) and SSthrowing (P2: throwing.dm:8), and M5x slice 1 SSexplosions (a ticker,
        // explosions.dm:8), so SStimer starts 0 to 8 ticks after; the M7 projectiles slice adds SSprojectiles (a ticker that sorts
        // before SStimer: projectiles.dm:5, SS_NO_INIT | SS_TICKER). The bound is counted from the Master's own list: every ticker that
        // fires (not SS_NO_FIRE, SS_TICKER without SS_BACKGROUND) up to SStimer adds 0 or 1 tick (master.dm:597-611).
        int tickersToTimer;
        using (w.Enter())
        {
            datum_controller_subsystem[] all = Subsystems();
            int timerAt = Array.FindIndex(all, s => s is datum_controller_subsystem_timer);
            tickersToTimer = all.Take(timerAt + 1).Count(s => !Dm.truthy(Dm.bitand(s.ss_flags, SS_NO_FIRE))
                && Dm.eq(Dm.bitand(s.ss_flags, Dm.bitor(SS_TICKER, SS_BACKGROUND)), SS_TICKER));
        }
        Assert.InRange(firstFire[typeof(datum_controller_subsystem_timer)], 21, 21 + tickersToTimer);
        // The lobby runlevel (SetRunLevel(1), master.dm:449) keeps the RUNLEVELS_DEFAULT subsystems from firing.
        Assert.False(firstFire.ContainsKey(typeof(datum_controller_subsystem_processing)));
        Assert.False(firstFire.ContainsKey(typeof(datum_controller_subsystem_processing_obj)));
        Assert.Contains(log.Lines, l => l.Contains("Initializations complete within", StringComparison.Ordinal));

        // The SSticker stand-in moves the round on: the game-runlevel subsystems fire.
        TgTestWorld.RunLevel(w, RUNLEVEL_GAME);
        w.Step(40);
        using (w.Enter())
            Assert.True(SSprocessing.times_fired > 0);
        Assert.Empty(log.Runtimes);
        Assert.Equal(0, w.RuntimeErrorCount);
    }

    [Fact]
    public void TheBootTraceIsTheSameForEverySeed()
    {
        // The tick of each subsystem's first fire, for two seeds: only the ticker offsets (rand(0, 1)) may differ. The window is 70
        // ticks so that every subsystem's first fire falls inside it for both seeds: the tickers' offsets (one more with
        // SSprojectiles, the M7 projectiles slice) push the later first fires (SStgui, SSverb_manager) across a 30-tick edge for
        // one seed and not the other, which would make the sets differ for a reason that is not the trace's; a non-ticker's first
        // fire is up to 2 s (40 ticks) after the init at tick 20 (master.dm's rand(0, min(wait, 2 SECONDS))), SSescape_menu's 2 s
        // wait reaching tick 60.
        static Dictionary<string, long> Trace(ulong seed)
        {
            World w = TgTestWorld.Start(seed);
            var first = new Dictionary<string, long>();
            for (int i = 0; i < 70; i++)
            {
                w.Step();
                using (w.Enter())
                {
                    foreach (datum_controller_subsystem s in Subsystems())
                    {
                        if (s.times_fired > 0)
                            first.TryAdd(DmTypes.PathOf(s.GetType()), w.Tick);
                    }
                }
            }
            return first;
        }

        Dictionary<string, long> a = Trace(1);
        Dictionary<string, long> b = Trace(2);
        Assert.Equal(a.Keys.OrderBy(k => k, StringComparer.Ordinal), b.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(20, a["/datum/controller/subsystem/garbage"]);
        Assert.Equal(20, b["/datum/controller/subsystem/garbage"]);
    }

    // tg: code/modules/unit_tests/subsystem_init.dm, run in a booted world (see datum_unit_test_subsystem_init).
    private sealed class SubsystemInitInABootedWorld : datum_unit_test_subsystem_init
    {
    }

    [Fact]
    public void TgSubsystemInitPassesAfterBoot()
    {
        World w = TgTestWorld.Boot(out TgTestWorld.Log log);
        var test = new SubsystemInitInABootedWorld();
        using (w.Enter())
        {
            test.New();
            DmTask run = test.Run();
            Assert.True(run.IsCompleted);
            Assert.False(run.IsFaulted, run.Exception?.ToString());
        }
        Assert.Null(test.fail_reasons);
        Assert.True(Dm.truthy(test.succeeded));
        Assert.Empty(log.Runtimes);
    }

    [Fact]
    public void TheSameBootTwiceGivesTheSameStateHash()
    {
        World a = TgTestWorld.Start(seed: 5);
        World b = TgTestWorld.Start(seed: 5);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        for (int i = 0; i < 3; i++)
        {
            a.Step(20);
            b.Step(20);
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }
        Assert.NotEqual(TgTestWorld.Start(seed: 6).ComputeStateHash(), TgTestWorld.Start(seed: 5).ComputeStateHash());
    }

    [Fact]
    public void GlobAfterBootRecordsItsGlobalsInTypesofOrderAndDropsItsScratchController()
    {
        World w = TgTestWorld.Start();
        object scratch;
        using (w.Enter())
        {
            string[] order = Dm.each(GLOB.gvars_datum_init_order).Cast<object>().Select(x => (string)x).ToArray();
            List<string> procOrder = ProcOrder.ProcsOf("/datum/controller/global_vars").Select(p => p["InitGlobal".Length..]).ToList();
            Assert.Equal(order.OrderBy(procOrder.IndexOf).ToArray(), order);
            Assert.Equal(order.Length, order.Distinct().Count());
            // exclude_these (controllers/globals.dm:16) has the id after GLOB's and is qdel'd by its QDEL_IN timer.
            Assert.True(w.Objects.TryGet(GLOB.dm_id + 1, out scratch));
            Assert.Equal(typeof(datum_controller), scratch.GetType());
            Assert.False(QDELETED(scratch));
        }
        w.Step(40);
        using (w.Enter())
            Assert.True(QDELETED(scratch));
    }

    [Fact]
    public void TheWorldStateForLoggingNamesTheTick()
    {
        World w = TgTestWorld.Boot();
        using (w.Enter())
        {
            var data = (DmList)Dm.world.get_world_state_for_logging();
            Assert.True(Dm.eq(data["tick_usage"], 0)); // DV3
            Assert.True(Dm.eq(data["tick_lag"], 0.5f));
            Assert.True(Dm.eq(data["time"], Dm.world.time));
            Assert.True(istext(data["timestamp"])); // rust-g "Returns the timestamp as a string" (__DEFINES/rust_g.dm:548)
        }
    }

    [Fact]
    public void TwoBootsOfOneSeedHashAlike()
    {
        // Runs on every branch: until the merge the boot reaches other packages' skeletons, which a running world reports
        // (NotTranslatedThrows = false) in the same order every time, so the two worlds must still agree tick by tick.
        static void Configure(World world) => DmWorldState.Of(world).NotTranslatedThrows = false;
        World a = TgTestWorld.Start(out TgTestWorld.Log logA, seed: 3, configure: Configure);
        World b = TgTestWorld.Start(out TgTestWorld.Log logB, seed: 3, configure: Configure);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        a.Step(30);
        b.Step(30);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.Equal(logA.Lines, logB.Lines);
        Assert.Equal(logA.Runtimes.Select(e => e.Message), logB.Runtimes.Select(e => e.Message));
        Assert.Equal(a.RuntimeErrorCount, b.RuntimeErrorCount);
        // The bootstrap left its traces: the /world defaults, a Master and the genesis call run once.
        using (a.Enter())
        {
            Assert.Equal(typeof(turf_open_space_basic), Dm.world.turf);
            Assert.NotNull(Master);
            Assert.Throws<InvalidOperationException>(() => TgState.Current.InitStatics());
        }
    }
}
