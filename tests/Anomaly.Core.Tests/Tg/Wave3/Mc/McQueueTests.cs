using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Mc;

/// <summary>
/// The subsystem queue: enqueue/dequeue (subsystem.dm:203-272), the fire order within a tick, CheckQueue (master.dm:796)
/// and RunQueue (:831) with their '.'-on-runtime results (rules 9.3), SoftReset (:961) and Loop's own 9.3 result.
/// </summary>
public class McQueueTests
{
    private static readonly float LobbyAndDefault = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);

    private static Action<McFixtureSubsystem> Fires(float flags, float wait, float priority)
        => ss =>
        {
            ss.ss_flags = Dm.bitor(flags, SS_NO_INIT);
            ss.wait = wait;
            ss.priority = priority;
            ss.init_stage = INITSTAGE_LAST;
            ss.runlevels = LobbyAndDefault;
        };

    [Fact]
    public void WithinATickTickersFireFirstThenOthersThenBackgroundEachByPriorityWithTheirOwnTieOrder()
    {
        var w = new McWorld();
        // added in an order unrelated to the result: Initialize sorts the subsystems by name (master.dm:433), Loop sorts
        // each list by priority, stably (:636-639)
        w.Add<datum_controller_subsystem_test_mc_a>("Beta BG", Fires(SS_BACKGROUND, 20, 30));
        w.Add<datum_controller_subsystem_test_mc_b>("Gamma", Fires(NONE, 20, 100));
        w.Add<datum_controller_subsystem_test_mc_c>("Beta Ticker", Fires(SS_TICKER, 1, 10));
        w.Add<datum_controller_subsystem_test_mc_d>("Alpha", Fires(NONE, 20, 50));
        w.Add<datum_controller_subsystem_test_mc_e>("Gamma BG", Fires(SS_BACKGROUND, 20, 60));
        w.Add<datum_controller_subsystem_test_mc_f>("Alpha Ticker", Fires(SS_TICKER, 1, 10));
        w.Add<datum_controller_subsystem_test_mc_g>("Beta", Fires(NONE, 20, 50));
        w.Add<datum_controller_subsystem_test_mc_h>("Gamma Ticker", Fires(SS_TICKER, 1, 20));
        w.Add<datum_controller_subsystem_test_mc_i>("Alpha BG", Fires(SS_BACKGROUND, 20, 30));
        w.Boot();
        w.Step(41);

        List<string> FiresAt(long tick) => w.TraceAt(tick).Where(l => l.EndsWith(" fire", StringComparison.Ordinal)).Select(l => l[..^5]).ToList();

        // CheckQueue enqueues in list order: the runlevel list by priority ascending, then the tickers (master.dm:639).
        // enqueue (subsystem.dm:203-258) puts every non-background ticker at the head, so the tickers end up by priority
        // descending with ties in reverse name order; the others go before the first lower priority or the first
        // background node, so ties keep name order; background subsystems go last.
        Assert.Equal(new[]
        {
            "Gamma Ticker", "Beta Ticker", "Alpha Ticker",
            "Gamma", "Alpha", "Beta",
            "Gamma BG", "Alpha BG", "Beta BG",
        }, FiresAt(41));
        Assert.Equal(new[] { "Gamma", "Alpha", "Beta", "Gamma BG", "Alpha BG", "Beta BG" },
            FiresAt(1).Where(n => !n.EndsWith("Ticker", StringComparison.Ordinal)));
        using (w.World.Enter())
            Assert.Equal(new[] { "Alpha", "Alpha BG", "Alpha Ticker", "Beta", "Beta BG", "Beta Ticker", "Gamma", "Gamma BG", "Gamma Ticker" },
                Dm.each(w.Mc.subsystems).Select(s => Dm.str(((datum_controller)s).name)));
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void TgQuirk_EnqueuePrecedence_ANewTickerAlwaysTakesTheHeadAndOutranksNoQueuedTicker()
    {
        var w = new McWorld();
        var ticker10 = w.Add<datum_controller_subsystem_test_mc_a>("Ticker 10", Fires(SS_TICKER, 1, 10));
        var ticker5 = w.Add<datum_controller_subsystem_test_mc_b>("Ticker 5", Fires(SS_TICKER, 1, 5));
        var plain50 = w.Add<datum_controller_subsystem_test_mc_c>("Plain 50", Fires(NONE, 20, 50));
        var plain1 = w.Add<datum_controller_subsystem_test_mc_d>("Plain 1", Fires(NONE, 20, 1));
        var bg100 = w.Add<datum_controller_subsystem_test_mc_e>("BG 100", Fires(SS_BACKGROUND, 20, 100));
        var bg_ticker200 = w.Add<datum_controller_subsystem_test_mc_f>("BG Ticker 200", Fires(Dm.bitor(SS_TICKER, SS_BACKGROUND), 1, 200));
        using (w.World.Enter())
        {
            // subsystem.dm:214 `queue_node_flags & (SS_TICKER|SS_BACKGROUND) == SS_TICKER` parses as
            // flags & ((SS_TICKER|SS_BACKGROUND) == SS_TICKER), that is flags & 0: the ticker branch never runs, and a queued
            // ticker is handled as an ordinary node.
            ticker10.enqueue();
            ticker5.enqueue();
            // the parenthesised test (as at :215) would keep the tickers by priority, Ticker 10 first
            Assert.Equal(new[] { "Ticker 5", "Ticker 10" }, w.QueueNames());

            plain50.enqueue();
            // ...and would skip the tickers; as written, a higher priority than the first queued ticker goes before it
            Assert.Equal(new[] { "Plain 50", "Ticker 5", "Ticker 10" }, w.QueueNames());

            plain1.enqueue();
            bg100.enqueue();
            bg_ticker200.enqueue(); // a background ticker is background first (SS_BACKGROUND overrides SS_TICKER, MC.dm:52)
            Assert.Equal(new[] { "Plain 50", "Ticker 5", "Ticker 10", "Plain 1", "BG Ticker 200", "BG 100" }, w.QueueNames());

            // the links back from the tail agree
            var back = new List<string>();
            for (datum_controller_subsystem node = w.Mc.queue_tail; node != null; node = node.queue_prev)
                back.Add(Dm.str(node.name));
            Assert.Equal(w.QueueNames().AsEnumerable().Reverse(), back);
            Assert.Null(w.Mc.queue_head.queue_prev);

            Assert.Equal(10f + 5 + 50 + 1, w.Mc.queue_priority_count);
            Assert.Equal(100f + 200, w.Mc.queue_priority_count_bg);
            foreach (McFixtureSubsystem ss in new McFixtureSubsystem[] { ticker10, ticker5, plain50, plain1, bg100, bg_ticker200 })
            {
                Assert.True(Dm.eq(ss.state, SS_QUEUED));
                Assert.True(Dm.eq(ss.queued_time, world.time));
                Assert.True(Dm.eq(ss.queued_priority, ss.priority));
            }
        }
    }

    [Fact]
    public void EnqueuePutsANodeAfterTheNodesOfEqualPriorityAndKeepsTheQueuedPriority()
    {
        var w = new McWorld();
        var x = w.Add<datum_controller_subsystem_test_mc_a>("X", Fires(NONE, 20, 50));
        var y = w.Add<datum_controller_subsystem_test_mc_b>("Y", Fires(NONE, 20, 50));
        var z = w.Add<datum_controller_subsystem_test_mc_c>("Z", Fires(NONE, 20, 70));
        var p = w.Add<datum_controller_subsystem_test_mc_d>("P", Fires(SS_BACKGROUND, 20, 5));
        var q = w.Add<datum_controller_subsystem_test_mc_e>("Q", Fires(SS_BACKGROUND, 20, 5));
        using (w.World.Enter())
        {
            p.enqueue();
            x.enqueue();
            q.enqueue();
            y.enqueue();
            x.priority = 99; // an edit after queueing does not move it: enqueue compares queued_priority (:211)
            z.enqueue();
            Assert.Equal(new[] { "Z", "X", "Y", "P", "Q" }, w.QueueNames());
            Assert.True(Dm.eq(x.queued_priority, 50));
        }
    }

    [Fact]
    public void DequeueUnlinksTheNodeAndResetsOnlyAQueuedState()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Fires(NONE, 20, 30));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", Fires(NONE, 20, 20));
        var c = w.Add<datum_controller_subsystem_test_mc_c>("C", Fires(NONE, 20, 10));
        using (w.World.Enter())
        {
            a.enqueue();
            b.enqueue();
            c.enqueue();
            b.dequeue();
            Assert.Equal(new[] { "A", "C" }, w.QueueNames());
            Assert.Same(c, a.queue_next);
            Assert.Same(a, c.queue_prev);
            Assert.True(Dm.eq(b.state, SS_IDLE));
            Assert.True(Dm.eq(b.queued_time, 0));

            a.state = SS_RUNNING;
            a.dequeue();
            Assert.Same(c, w.Mc.queue_head);
            Assert.True(Dm.eq(a.state, SS_RUNNING)); // subsystem.dm:271: only SS_QUEUED becomes SS_IDLE
            c.dequeue();
            Assert.Null(w.Mc.queue_head);
            Assert.Null(w.Mc.queue_tail);
            // dequeue leaves the running totals to its callers (RunQueue subtracts, master.dm:942-945)
            Assert.Equal(60f, w.Mc.queue_priority_count);
        }
    }

    [Fact]
    public void RunQueueGivesEachSubsystemItsShareOfTheTickByPriority()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Fires(NONE, 20, 150));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", Fires(NONE, 20, 50));
        var c = w.Add<datum_controller_subsystem_test_mc_c>("C", Fires(SS_BACKGROUND, 20, 30));
        w.Step(1);
        using (w.World.Enter())
        {
            a.enqueue();
            b.enqueue();
            c.enqueue();
            object result = w.Mc.RunQueue();

            Assert.True(Dm.eq(result, 1));
            // tick_precentage = (TICK_LIMIT_RUNNING - TICK_USAGE) * priority / budget, the budget shrinking by each priority
            // run (master.dm:882-892, :919), the background budget separate (:864-867); current_ticklimit =
            // round(TICK_USAGE + tick_precentage): A 98 * 150/200 = 73.5 -> 73, B 98 * 50/50, C 98 * 30/30
            Assert.Equal(new[] { 73f }, a.seen_ticklimits);
            Assert.Equal(new[] { 98f }, b.seen_ticklimits);
            Assert.Equal(new[] { 98f }, c.seen_ticklimits);
            // ignite: tick_allocation_last = current_ticklimit - TICK_USAGE, averaged (subsystem.dm:144-145)
            Assert.Equal(new[] { 73f }, a.seen_allocations);
            Assert.Equal(MC_AVERAGE(0, 73), a.tick_allocation_avg);
            Assert.Same(c, w.Mc.last_type_processed);
            Assert.Null(w.Mc.queue_head);
            Assert.Equal(0f, w.Mc.queue_priority_count);
            Assert.Equal(0f, w.Mc.queue_priority_count_bg);
            foreach (McFixtureSubsystem ss in new McFixtureSubsystem[] { a, b, c })
            {
                Assert.Equal(1f, ss.times_fired);
                Assert.Equal(0.5f, ss.last_fire);
                Assert.Equal(0.5f + 20, ss.next_fire); // queued_time + wait
                Assert.True(Dm.eq(ss.queued_time, 0));
                Assert.True(Dm.eq(ss.state, SS_IDLE));
                Assert.Equal(MC_AVERAGE(1, 0), ss.ticks); // ticks averages paused_ticks (:930)
                Assert.Equal(0f, ss.tick_overrun);
            }
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void RunQueueReturnsZeroWhenItRuntimesAndLeavesTheRestOfTheQueue()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Fires(NONE, 20, 50));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", Fires(NONE, 20, 10));
        using (w.World.Enter())
        {
            a.nextfire_crashes = TRUE;
            a.enqueue();
            b.enqueue();
            object result = w.Mc.RunQueue();

            // "Returns 0 if runtimed" (master.dm:829-832; rules 9.3): the runtime in update_nextfire ends RunQueue
            Assert.True(Dm.eq(result, 0));
            Assert.Equal(1f, a.times_fired);
            Assert.Empty(b.fires);
            Assert.Equal(new[] { "A", "B" }, w.QueueNames()); // A was never dequeued
        }
        Exception e = Assert.Single(w.DmRuntimes);
        Assert.Equal("A crashed in update_nextfire", e.Message);
    }

    [Fact]
    public void RunQueueReturnsMinusOneForALostPriorityBudgetButStillRunsTheQueue()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Fires(NONE, 20, 50));
        using (w.World.Enter())
        {
            a.enqueue();
            w.Mc.queue_priority_count = 0; // out of sync with the queue
            object result = w.Mc.RunQueue();

            // master.dm:886-892: the error state logs, sets '.' to -1 and gives the subsystem the whole remaining tick
            Assert.True(Dm.eq(result, -1));
            Assert.Equal(new[] { 98f }, a.seen_ticklimits);
            Assert.Equal(1f, a.times_fired);
            Assert.Equal(-50f, w.Mc.queue_priority_count);
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void CheckQueueEnqueuesWhatIsDueAndDropsNoFireSubsystemsFromTheList()
    {
        var w = new McWorld();
        var due = w.Add<datum_controller_subsystem_test_mc_a>("Due", Fires(NONE, 20, 50));
        var later = w.Add<datum_controller_subsystem_test_mc_b>("Later", Fires(NONE, 20, 50));
        var off = w.Add<datum_controller_subsystem_test_mc_c>("Off", Fires(NONE, 20, 50));
        var busy = w.Add<datum_controller_subsystem_test_mc_d>("Busy", Fires(NONE, 20, 50));
        var no_fire = w.Add<datum_controller_subsystem_test_mc_e>("No Fire", Fires(SS_NO_FIRE, 20, 50));
        var keep = w.Add<datum_controller_subsystem_test_mc_f>("Keep", Fires(SS_KEEP_TIMING, 20, 50));
        var postponed = w.Add<datum_controller_subsystem_test_mc_g>("Postponed", Fires(NONE, 20, 50));
        w.Step(20); // world.time 10
        using (w.World.Enter())
        {
            later.next_fire = 10.5f;
            off.can_fire = 0;
            busy.state = SS_SLEEPING;
            keep.last_fire = 0; // 0 + 20 * 0.75 > 10 (master.dm:817)
            postponed.postponed_fires = 1;
            DmList list = Dm.list(due, later, off, busy, no_fire, keep, postponed);

            object result = w.Mc.CheckQueue(list);

            Assert.True(Dm.eq(result, 1));
            Assert.Equal(new[] { "Due" }, w.QueueNames());
            Assert.False(Dm.@in(no_fire, list)); // removed from the list it was checked from (:812-814)
            Assert.Equal(6f, list.len);
            Assert.Equal(0f, postponed.postponed_fires);
            Assert.Equal(0f + 20, postponed.next_fire); // update_nextfire from queued_time 0
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void CheckQueueReturnsZeroWhenItRuntimesOnANullEntryAfterRemovingIt()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Fires(NONE, 20, 50));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", Fires(NONE, 20, 50));
        using (w.World.Enter())
        {
            DmList list = Dm.list(a, null, b);
            object result = w.Mc.CheckQueue(list);

            // `if (!thing) subsystemstocheck -= thing` (master.dm:803-804) does not skip the entry: `SS.state` then reads
            // null and the runtime ends CheckQueue with '. = 0' (:797; rules 9.3)
            Assert.True(Dm.eq(result, 0));
            Assert.Equal(new[] { "A" }, w.QueueNames());
            Assert.Equal(2f, list.len);
            Assert.False(Dm.@in(null, list));
        }
        Assert.Single(w.DmRuntimes);
    }

    [Fact]
    public void SoftResetClearsTheQueueStateAndDropsEntriesThatAreNotSubsystems()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Fires(NONE, 20, 50));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", Fires(SS_BACKGROUND, 20, 50));
        using (w.World.Enter())
        {
            a.enqueue();
            b.enqueue();
            Dm.append(ref w.Mc.subsystems, "junk");
            DmList tickers = Dm.list(a);
            DmList runlevel_list = Dm.list(a, b, "junk");
            DmList runlevels = Dm.list(runlevel_list);

            object result = w.Mc.SoftReset(tickers, runlevels);

            Assert.True(Dm.eq(result, 1));
            Assert.False(Dm.@in("junk", w.Mc.subsystems));
            Assert.False(Dm.@in("junk", runlevel_list)); // the inner list is changed in place (master.dm:979)
            Assert.Equal(2f, runlevel_list.len);
            Assert.Null(w.Mc.queue_head);
            Assert.Null(w.Mc.queue_tail);
            Assert.Equal(0f, w.Mc.queue_priority_count);
            Assert.Equal(0f, w.Mc.queue_priority_count_bg);
            foreach (McFixtureSubsystem ss in new McFixtureSubsystem[] { a, b })
            {
                Assert.Null(ss.queue_next);
                Assert.Null(ss.queue_prev);
                Assert.True(Dm.eq(ss.queued_priority, 0));
                Assert.True(Dm.eq(ss.queued_time, 0));
                Assert.True(Dm.eq(ss.state, SS_IDLE));
            }

            Assert.True(Dm.eq(w.Mc.SoftReset(null, runlevels), 0)); // "Bad list contents" (:965-967)
        }
        // stack_trace("MC: SoftReset called, ...") reports and returns (:963), once per call
        Assert.Equal(2, w.DmRuntimes.Count);
        Assert.All(w.DmRuntimes, e => Assert.Equal("MC: SoftReset called, resetting MC queue state.", e.Message));
    }

    [Fact]
    public void LoopReturnsMinusOneWhenItRuntimesAndNewStagesWhenAnotherStageCompleted()
    {
        var w = new McWorld();
        using (w.World.Enter())
        {
            datum_controller_master.init_stage_completed = INITSTAGE_MAX;
            w.Mc.SetRunLevel(RUNLEVEL_LOBBY);

            // Loop(1) with stage 4 completed returns MC_LOOP_RTN_NEWSTAGES on its first iteration (master.dm:668-669)
            Assert.True(Dm.eq(Waiting.NoSleep(w.Mc.Loop(1)), MC_LOOP_RTN_NEWSTAGES));

            // make_runtime reads a null subsystem's var (:696-698); Loop's '. = -1' (:589) is what StartProcessing sees
            // ("MC crashed or runtimed", :579) since a runtime ends only Loop (rules 9.3)
            w.Mc.make_runtime = TRUE;
            Assert.True(Dm.eq(Waiting.NoSleep(w.Mc.Loop(INITSTAGE_MAX)), -1));
        }
        Assert.IsType<NullReferenceException>(Assert.Single(w.DmRuntimes));
    }
}
