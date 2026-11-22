using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>
/// The call forms besides await (docs/ai/specs/waiting-model.md, "Call forms"): Waiting.Detach, Waiting.NoSleep,
/// the set waitfor = FALSE wrapper, and spawn.
/// </summary>
public class CallFormTests
{
    private static async DmTask<int> Value(int v, bool sleep)
    {
        if (sleep) await Sleep(0.5f);
        return v;
    }

    // ---- Waiting.Detach ----

    [Fact]
    public void DetachReturnsTheCalleesValueOnlyWhenItDidNotSleep()
    {
        var w = KernelTestWorld.New(out var errors);
        using (w.Enter())
        {
            Assert.Equal(5, Waiting.Detach(Value(5, sleep: false)));
            Assert.Equal(0, Waiting.Detach(Value(5, sleep: true)));
            Assert.Equal(0, Waiting.Detach(DmTask<int>.FromException(new InvalidOperationException("x"))));
        }
        Assert.Equal(new[] { "InvalidOperationException: x" }, errors);
        w.Step(2);
        Assert.Single(errors);
    }

    [Fact]
    public void DetachSyncRunsASynchronousProcInsideAnIsolationBoundary()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter())
        {
            Waiting.DetachSync(() => { log.Add("sync callee"); throw new InvalidOperationException("sync callee runtime"); });
            log.Add("caller continues");
        }
        Assert.Equal(new[] { "sync callee", "caller continues" }, log);
        Assert.Equal(new[] { "InvalidOperationException: sync callee runtime" }, errors);
    }

    // ---- Waiting.NoSleep ----

    [Fact]
    public void NoSleepIsAPlainCallWhenTheCalleeCompletes()
    {
        var w = KernelTestWorld.New(out var errors);
        using (w.Enter())
        {
            Assert.Equal(3, Waiting.NoSleep(Value(3, sleep: false)));
            Waiting.NoSleep(DmTask.CompletedTask);
            var e = Assert.Throws<InvalidOperationException>(() => Waiting.NoSleep(Fails()));
            Assert.Equal("captured", e.Message);
        }
        Assert.Empty(errors);

        static async DmTask<int> Fails() { throw new InvalidOperationException("captured"); }
    }

    [Fact]
    public void NoSleepOnASleepingCalleeReportsReturnsDefaultAndLetsTheCalleeFinishDetached()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        w.AddPhase("signal handler", x =>
        {
            if (x.Tick != 1) return;
            int got = Waiting.NoSleep(update_icon(7));
            log.Add($"handler got {got}");
        });
        w.Step(1);
        Assert.Equal(new[] { "update_icon start", "handler got 0" }, log);
        Assert.Single(errors);
        Assert.Contains("slept inside a no-sleep context", errors[0]);
        Assert.Contains("update_icon(7)", errors[0]); // the call expression, for the translator
        w.Step(2);
        Assert.Equal("update_icon end", log[^1]);
        Assert.Equal(2, errors.Count); // its later runtime is reported too (it runs detached)
        Assert.Equal("InvalidOperationException: late runtime", errors[1]);

        async DmTask<int> update_icon(int updates)
        {
            log.Add("update_icon start");
            await Sleep(0.5f);
            log.Add("update_icon end");
            throw new InvalidOperationException("late runtime");
        }
    }

    // ---- set waitfor = FALSE: ignite()/fire() (code/controllers/subsystem.dm:139-157) ----

    private sealed class Subsystem
    {
        public const int SS_IDLE = 0, SS_QUEUED = 1, SS_RUNNING = 2, SS_PAUSED = 3, SS_SLEEPING = 4, SS_PAUSING = 5;

        public string name;
        public int state = SS_IDLE;
        public int slept_count;
        public int times_fired;
        public Func<Subsystem, DmTask> fire_impl;

        // tg: code/controllers/subsystem.dm:162 (coloured: overrides of fire may sleep)
        public DmTask fire(bool resumed = false) => fire_impl(this);

        // tg: code/controllers/subsystem.dm:139  set waitfor = FALSE, and the MC uses the value
        public int ignite(bool resumed = false)
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(ignite__body(resumed, dot), dot);
        }

        private async DmTask<int> ignite__body(bool resumed, DotBox<int> dot)
        {
            dot.Value = SS_IDLE;
            // tick_allocation_last / tick_allocation_avg: tick usage is virtual 0 in the port

            dot.Value = SS_SLEEPING;
            // Isolation boundary of the port (waiting-model.md "Runtime errors"): a fire() runtime ends fire()
            // only, which is what DM does here too.
            try { await fire(resumed); }
            catch (Exception e) { World.Current.ReportRuntime(e); }
            dot.Value = state;
            if (state == SS_SLEEPING)
            {
                slept_count++;
                state = SS_IDLE;
            }
            if (state == SS_PAUSING)
            {
                slept_count++;
                // enqueue() keeping queued_time: not modelled here
                state = SS_PAUSED;
            }
            return dot.Value;
        }
    }

    [Fact]
    public void IgniteReturnsSsSleepingWhenFireSleepsAndTheMcSurvivesAFireRuntime()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        var sleeper = new Subsystem
        {
            name = "sleeper",
            fire_impl = async ss => { if (++ss.times_fired == 1) await Sleep(1); },
        };
        var plain = new Subsystem { name = "plain", fire_impl = ss => { ss.times_fired++; return DmTask.CompletedTask; } };
        var bad = new Subsystem
        {
            name = "bad",
            fire_impl = async ss => { if (++ss.times_fired == 2) throw new InvalidOperationException("runtime in fire"); },
        };
        var queue = new[] { sleeper, plain, bad };
        int iterations = 0;

        // Stand-in for the Master Controller loop: a scheduler coroutine started at world start.
        using (w.Enter()) Waiting.Detach(Loop());
        w.Step(4);

        Assert.Equal(new[]
        {
            "0: ignite(sleeper)=4", "0: ignite(plain)=2", "0: ignite(bad)=2",
            "1: skip sleeper (state 4)", "1: ignite(plain)=2", "1: ignite(bad)=2",
            "2: ignite(sleeper)=2", "2: ignite(plain)=2", "2: ignite(bad)=2",
            "3: ignite(sleeper)=2", "3: ignite(plain)=2", "3: ignite(bad)=2",
            "4: ignite(sleeper)=2", "4: ignite(plain)=2", "4: ignite(bad)=2",
        }, log);
        Assert.Equal(1, sleeper.slept_count);
        Assert.Equal(5, iterations);
        Assert.Equal(5, plain.times_fired);
        Assert.Equal(new[] { "InvalidOperationException: runtime in fire" }, errors);

        async DmTask Loop()
        {
            while (true)
            {
                iterations++;
                RunQueue();
                await Sleep(World.TickLag);
            }
        }

        // tg: code/controllers/master.dm:894-918 (the ignite part of RunQueue)
        void RunQueue()
        {
            foreach (var ss in queue)
            {
                if (ss.state != Subsystem.SS_IDLE) { log.Add($"{w.Tick}: skip {ss.name} (state {ss.state})"); continue; }
                ss.state = Subsystem.SS_RUNNING;
                int state = ss.ignite(false);
                log.Add($"{w.Tick}: ignite({ss.name})={state}");
                if (state == Subsystem.SS_RUNNING) state = Subsystem.SS_IDLE;
                ss.state = state;
            }
        }
    }

    // ---- set waitfor = FALSE: process() and PROCESS_KILL (processing.dm:17-33, :48-50) ----

    private const int PROCESS_KILL = 26;

    private class datum
    {
        public List<string> log;

        // tg: code/controllers/subsystem/processing/processing.dm:48  set waitfor = FALSE; return PROCESS_KILL
        // This body never sleeps, so it is a plain method; overrides that may sleep get the wrapper.
        public virtual int process(float seconds_per_tick) => PROCESS_KILL;
    }

    private sealed class quiet : datum
    {
        public override int process(float seconds_per_tick) { log.Add("quiet"); return 0; }
    }

    // pod.dm:25-30 shape: "alarm() sleeps, so we ... can't rely on return PROCESS_KILL"
    private sealed class alarmed : datum
    {
        // Inherits set waitfor = FALSE from /datum/proc/process: wrapper plus body.
        public override int process(float seconds_per_tick)
        {
            return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // '.' is never written before the sleep
        }

        private async DmTask<int> process__body(float seconds_per_tick)
        {
            log.Add("alarm start");
            await Sleep(1); // alarm()
            log.Add("alarm end");
            return PROCESS_KILL; // discarded: the caller already got '.' (null)
        }
    }

    [Fact]
    public void ProcessKillIsSeenWhenReturnedSynchronouslyAndLostWhenReturnedAfterASleep()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        var killer = new datum { log = log };
        var keeper = new quiet { log = log };
        var sleeper = new alarmed { log = log };
        var processing = new List<datum> { killer, keeper, sleeper };

        // tg: SSprocessing.fire, processing.dm:17-33 (iterates from the end; MC_TICK_CHECK never trips)
        void fire()
        {
            var current_run = processing.ToList();
            while (current_run.Count > 0)
            {
                var thing = current_run[^1];
                current_run.RemoveAt(current_run.Count - 1);
                if (thing.process(1f) == PROCESS_KILL)
                    processing.Remove(thing); // STOP_PROCESSING
            }
        }

        using (w.Enter()) fire();
        Assert.Equal(new[] { "alarm start", "quiet" }, log);
        Assert.DoesNotContain(killer, processing);   // synchronous PROCESS_KILL stops processing
        Assert.Contains(sleeper, processing);        // the caller got '.' = null at the sleep
        w.Step(2);
        Assert.Equal("alarm end", log[^1]);
        Assert.Contains(sleeper, processing);        // its later PROCESS_KILL went nowhere
        Assert.Empty(errors);
    }

    // ---- set waitfor = FALSE: '.' at the first suspension, and ..() calls the parent's wrapper ----

    private class parent_proc
    {
        public List<string> log;

        // set waitfor = FALSE
        public virtual int act()
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(act__body(dot), dot);
        }

        private async DmTask<int> act__body(DotBox<int> dot)
        {
            dot.Value = 5;
            await Sleep(1);
            log.Add("parent resumed");
            dot.Value = 7;
            return dot.Value;
        }
    }

    private sealed class child_proc : parent_proc
    {
        // Inherits waitfor = FALSE. Its body does not sleep itself, but ..() may: the parent's wrapper
        // returns at the parent's sleep, so this body never suspends and needs no wrapper of its own.
        public override int act()
        {
            int r = base.act(); // ..()
            log.Add($"child got {r}");
            return r + 1;
        }
    }

    [Fact]
    public void WaitforFalseHandsTheCallerTheDotValueAtTheFirstSleepAndParentCallsKeepIt()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        int direct, viaChild;
        using (w.Enter())
        {
            direct = new parent_proc { log = log }.act();
            viaChild = new child_proc { log = log }.act();
        }
        Assert.Equal(5, direct);
        Assert.Equal(6, viaChild);
        Assert.Equal(new[] { "child got 5" }, log);
        w.Step(2);
        Assert.Equal(new[] { "child got 5", "parent resumed", "parent resumed" }, log);
        Assert.Empty(errors);
    }

    // ---- set waitfor = FALSE in a coloured slot (areas.dm:581 /area/Entered vs a sleeping /turf Entered) ----

    private class atom
    {
        public List<string> log;
        public virtual async DmTask Entered(string arrived) { log.Add($"{GetType().Name} entered"); }
    }

    private sealed class slow_turf : atom
    {
        public override async DmTask Entered(string arrived) { await Sleep(1); log.Add("turf entered"); }
    }

    private sealed class area : atom
    {
        // tg: code/game/area/areas.dm:581  set waitfor = FALSE in a slot that other overrides make coloured
        public override async DmTask Entered(string arrived) { Waiting.WaitforFalse(Entered__body(arrived)); }

        private async DmTask Entered__body(string arrived)
        {
            log.Add("area start");
            await Sleep(1);
            log.Add("area end");
            throw new InvalidOperationException("area runtime after the sleep");
        }
    }

    [Fact]
    public void AWaitforFalseOverrideInAColouredSlotNeverBlocksItsAwaitingCaller()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        var turf = new slow_turf { log = log };
        var a = new area { log = log };
        using (w.Enter()) Waiting.Detach(Move());
        Assert.Empty(log);
        w.Step(2);
        Assert.Equal(new[] { "turf entered", "area start", "after area" }, log); // the turf blocked Move, the area did not
        w.Step(2);
        Assert.Equal("area end", log[^1]);
        Assert.Equal(new[] { "InvalidOperationException: area runtime after the sleep" }, errors);

        async DmTask Move()
        {
            await turf.Entered("mob");
            var t = a.Entered("mob");
            Assert.True(t.IsCompleted);
            await t;
            log.Add("after area");
        }
    }

    // ---- calls by name: the untyped view of a returned task ----

    [Fact]
    public void ACallByNameUnwrapsABoxedTaskWithoutReflection()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter())
        {
            // What the dynamic call machinery does with the callee's return value, which it sees as object.
            static DmTask<object> Call(object returned) =>
                returned is IDmTask t ? t.AsObjectTask() : DmTask<object>.FromResult(returned);

            static object Result(DmTask<object> t) { Assert.True(t.TryGetResult(out var v)); return v; }

            Assert.Equal(5, Result(Call(Value(5, sleep: false))));
            Assert.Equal("plain", Result(Call("plain")));
            Assert.Null(Result(Call(DmTask.CompletedTask)));
            Assert.Equal("x", Call(DmTask<int>.FromException(new InvalidOperationException("x"))).Exception.Message);

            var pending = Call(Value(9, sleep: true));
            Assert.False(pending.IsCompleted);
            Waiting.Detach(Awaits(pending));
            Waiting.Detach(Call(FailsLater()));
        }
        w.Step(2);
        Assert.Equal(new[] { "got 9" }, log);
        Assert.Equal(new[] { "InvalidOperationException: late" }, errors); // once, through the adapter

        async DmTask Awaits(DmTask<object> t) => log.Add("got " + await t);
        static async DmTask FailsLater() { await Sleep(0.5f); throw new InvalidOperationException("late"); }
    }

    // ---- spawn ----

    [Fact]
    public void SpawnRunsNegativeDelaysNowZeroLaterThisTickAndPositiveDelaysRoundedLikeSleep()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        w.Scheduler.At(1, () =>
        {
            Waiting.Spawn(-1, async () => { log.Add($"spawn(-1) start@{w.Tick}"); await Sleep(0.5f); log.Add($"spawn(-1) end@{w.Tick}"); });
            log.Add($"caller after spawn(-1)@{w.Tick}");
            Waiting.Spawn(0, async () => log.Add($"spawn(0)@{w.Tick}"));
            Waiting.Spawn(1, async () => log.Add($"spawn(1)@{w.Tick}"));
            Waiting.Spawn(0.75f, async () => log.Add($"spawn(0.75)@{w.Tick}"));
            log.Add($"caller done@{w.Tick}");
        });
        w.Scheduler.At(1, () => log.Add($"queued before@{w.Tick}"));
        w.Step(4);
        Assert.Equal(new[]
        {
            "spawn(-1) start@1", "caller after spawn(-1)@1", "caller done@1", "queued before@1", "spawn(0)@1",
            "spawn(-1) end@2", "spawn(0.75)@2", "spawn(1)@3",
        }, log);
        Assert.Empty(errors);
    }

    [Fact]
    public void SpawnedBodiesSeeCopiedLocalsOnlyWhenTheTranslatorCopiesThem()
    {
        // BYOND copies the proc's locals into the spawned block; a C# lambda shares them. The translation rule
        // is to copy every local the body reads into a fresh local first.
        var w = KernelTestWorld.New(out _);
        var shared = new List<int>();
        var copied = new List<int>();
        using (w.Enter())
        {
            int i = 0;
            for (i = 1; i <= 3; i++)
            {
                Waiting.Spawn(1, async () => shared.Add(i));
                int i_copy = i;
                Waiting.Spawn(1, async () => copied.Add(i_copy));
            }
        }
        w.Step(2);
        Assert.Equal(new[] { 4, 4, 4 }, shared);
        Assert.Equal(new[] { 1, 2, 3 }, copied);
    }
}
