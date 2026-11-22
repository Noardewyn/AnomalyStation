using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Timers;

/// <summary>
/// Translator's smoke tests of code/controllers/subsystem/processing/{processing,fastprocess,obj,timed_actions}.dm and
/// the SSOBJ_DT and DELTA_WORLD_TIME members of __DEFINES/subsystems.dm. No datum here returns PROCESS_KILL, so these
/// fires keep every datum processing; the behaviour tests (Tg/Wave3/Timers) are the independent test-writer's.
/// </summary>
public class ProcessingSmokeTests
{
    public static TheoryData<Type, string, string, float, float, float, float> Subsystems() => new()
    {
        { typeof(datum_controller_subsystem_processing), "Processing", "P", 10, FIRE_PRIORITY_PROCESS, SS_BACKGROUND | SS_POST_FIRE_TIMING | SS_NO_INIT, RUNLEVELS_DEFAULT },
        { typeof(datum_controller_subsystem_processing_fastprocess), "Fast Processing", "FP", 2, FIRE_PRIORITY_PROCESS, SS_BACKGROUND | SS_POST_FIRE_TIMING | SS_NO_INIT, RUNLEVELS_DEFAULT },
        { typeof(datum_controller_subsystem_processing_obj), "Objects", "P", 20, FIRE_PRIORITY_OBJ, SS_NO_INIT, RUNLEVELS_DEFAULT },
        { typeof(datum_controller_subsystem_processing_timed_actions), "Timed Actions", "P", 1, FIRE_PRIORITY_TIMED_ACTIONS, SS_TICKER | SS_NO_INIT, RUNLEVEL_GAME | RUNLEVEL_POSTGAME },
    };

    [Theory]
    [MemberData(nameof(Subsystems))]
    public void TheProcessingSubsystemsKeepTgsSettingsAndRegisterTheirGlobals(Type type, string name, string stat_tag, float wait, float priority, float ss_flags, float runlevels)
    {
        var t = new TimersTestWorld();
        using (t.World.Enter())
        {
            var ss = (datum_controller_subsystem_processing)Dm.@new(type);
            Assert.Equal(name, ss.name);
            Assert.Equal(stat_tag, ss.stat_tag);
            Assert.Equal(wait, ss.wait);
            Assert.Equal(priority, ss.priority);
            Assert.Equal(ss_flags, ss.ss_flags);
            Assert.Equal(runlevels, ss.runlevels);
            Assert.Equal(0, ss.processing.Count);
            Assert.True(Dm.@in(ss, global_vars__subsystems())); // the SUBSYSTEM_DEF global (NEW_SS_GLOBAL, __DEFINES/MC.dm:29)
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheRootProcessReturnsProcessKill()
    {
        var t = new TimersTestWorld();
        using (t.World.Enter())
            Assert.True(Dm.eq(Dm.@new<datum>().process(1f), PROCESS_KILL)); // processing.dm:48-50
    }

    [Fact]
    public void AFireProcessesFromTheEndWithWaitTimesATenthAndDropsDeletedDatums()
    {
        var t = new TimersTestWorld();
        var trace = new List<string>();
        using (t.World.Enter())
        {
            var ss = Dm.@new<datum_controller_subsystem_processing_obj>();
            test_timers_processed Processed(string label)
            {
                test_timers_processed d = Dm.@new<test_timers_processed>();
                d.trace = trace;
                d.label = label;
                Dm.append(ref ss.processing, d);
                return d;
            }
            Processed("first");
            test_timers_processed deleted = Processed("deleted");
            Processed("last");
            qdel(deleted);

            TimersTestWorld.Fire(ss);

            // current_run[current_run.len] first (processing.dm:23-25); seconds_per_tick = wait * 0.1 (:28)
            Assert.Equal(new[] { "last 2", "first 2" }, trace);
            Assert.Equal(2, ss.processing.Count); // the deleted datum left the list (:26-27)
            Assert.Equal(0, ss.currentrun.Count);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ASleepingProcessDoesNotHoldUpTheFireAndARuntimeEndsOnlyItsItem()
    {
        var t = new TimersTestWorld();
        var trace = new List<string>();
        using (t.World.Enter())
        {
            var ss = Dm.@new<datum_controller_subsystem_processing>();
            test_timers_processed after = Dm.@new<test_timers_processed>();
            after.trace = trace;
            after.label = "after";
            test_timers_processed broken = Dm.@new<test_timers_processed>();
            broken.trace = null; // process() raises a runtime (a null trace)
            test_timers_sleeper sleeper = Dm.@new<test_timers_sleeper>();
            sleeper.trace = trace;
            ss.processing = Dm.list(after, broken, sleeper);

            TimersTestWorld.Fire(ss);

            // the sleeper's wrapper hands back null at its sleep (waiting-model.md 5.4), the broken datum's runtime is
            // reported at the per-item boundary (rules 9.2) and the fire goes on to the next datum
            Assert.Equal(new[] { "sleeper start", "after 1" }, trace);
            Assert.Equal(3, ss.processing.Count);
            Assert.IsType<NullReferenceException>(Assert.Single(t.Runtimes));
        }
        t.World.Step(2);
        Assert.Equal(new[] { "sleeper start", "after 1", "sleeper end" }, trace);
        Assert.Single(t.Runtimes);
    }

    [Fact]
    public void SsobjDtAndDeltaWorldTimeReadTheSubsystem()
    {
        var t = new TimersTestWorld();
        t.World.Step(30);
        using (t.World.Enter())
        {
            var ss = Dm.@new<datum_controller_subsystem_processing_obj>();
            Assert.Equal(2f, SSOBJ_DT()); // (SSobj.wait/10), subsystems.dm:278
            ss.last_fire = 5;
            Assert.Equal(1f, DELTA_WORLD_TIME(ss)); // ((world.time - ss.last_fire) * 0.1), subsystems.dm:281
        }
    }
}
