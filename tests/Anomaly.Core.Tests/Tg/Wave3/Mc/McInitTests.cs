using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Mc;

/// <summary>
/// The Master's Initialize (master.dm:323-477): the dependency graph and its topological sort (:337-412), the init stages
/// (:414-451), init_subsystem and its results (:485-552), a sleeping subsystem Initialize (scope R1) and MC_RUNNING
/// (MC.dm:36). The fixtures are appended to Master.subsystems in the order each test gives, the order tg's Master/New
/// leaves them in (subtypesof sorted by init stage, master.dm:110-125).
/// </summary>
public class McInitTests
{
    private static Action<McFixtureSubsystem> Init(float init_stage, object result = null)
        => ss =>
        {
            ss.ss_flags = SS_NO_FIRE;
            ss.init_stage = init_stage;
            ss.init_result = result ?? SS_INIT_SUCCESS;
        };

    private static List<string> InitOrder(McWorld w)
        => w.Trace.Where(l => l.EndsWith(" init", StringComparison.Ordinal)).Select(l => l[(l.IndexOf(' ') + 1)..^5]).ToList();

    [Fact]
    public void StagesInitializeInOrderAndASameStageWithoutDependenciesInReverseListOrder()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("delta", Init(INITSTAGE_MAIN));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("Alpha", Init(INITSTAGE_EARLY));
        var c = w.Add<datum_controller_subsystem_test_mc_c>("charlie", Init(INITSTAGE_MAIN));
        var d = w.Add<datum_controller_subsystem_test_mc_d>("Bravo", Init(INITSTAGE_LAST));
        var e = w.Add<datum_controller_subsystem_test_mc_e>("echo", Init(INITSTAGE_EARLY));
        w.Boot();

        // Kahn's algorithm pops from the end of unsorted_subsystems (master.dm:387-389), which starts as the list order:
        // e, d, c, b, a; then stage by stage (:414-418, :441)
        Assert.Equal(new[] { "echo", "Alpha", "charlie", "delta", "Bravo" }, InitOrder(w));
        using (w.World.Enter())
        {
            Assert.Equal(new[] { 1f, 2f, 3f, 4f, 5f }, new McFixtureSubsystem[] { e, b, c, a, d }.Select(s => s.init_order));
            Assert.Equal(new[] { 1f, 2f, 3f, 4f, 5f }, new McFixtureSubsystem[] { a, b, c, d, e }.Select(s => s.ordering_id)); // list positions (:381)
            Assert.True(Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX));
            Assert.Null(w.Mc.current_initializing_subsystem);
            // the subsystems are sorted by name for display (cmp_subsystem_display, sorttext ignores case; :433)
            Assert.Equal(new[] { "Alpha", "Bravo", "charlie", "delta", "echo" }, Dm.each(w.Mc.subsystems).Select(s => Dm.str(((datum_controller)s).name)));
            Assert.All(new McFixtureSubsystem[] { a, b, c, d, e }, s => Assert.Equal(TRUE, s.initialized));
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void DependenciesInitializeBeforeTheirDependents()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Init(INITSTAGE_MAIN));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", Init(INITSTAGE_MAIN));
        var c = w.Add<datum_controller_subsystem_test_mc_c>("C", Init(INITSTAGE_MAIN));
        var d = w.Add<datum_controller_subsystem_test_mc_d>("D", Init(INITSTAGE_MAIN));
        using (w.World.Enter())
        {
            b.dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_a));
            d.dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_c));
        }
        w.Boot();

        // unsorted [A, C]: C, then D freed by C, then A, then B freed by A
        Assert.Equal(new[] { "C", "D", "A", "B" }, InitOrder(w));
        using (w.World.Enter())
        {
            // the reverse graph holds instances (:368)
            Assert.Equal(new object[] { b }, Dm.each(a.dependents));
            Assert.Equal(new object[] { d }, Dm.each(c.dependents));
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void DeclaredDependentsBecomeDependenciesOfTheirTargets()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Init(INITSTAGE_MAIN));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", Init(INITSTAGE_MAIN));
        var c = w.Add<datum_controller_subsystem_test_mc_c>("C", Init(INITSTAGE_MAIN));
        using (w.World.Enter())
        {
            a.dependents = Dm.list(typeof(datum_controller_subsystem_test_mc_c), "junk");
            c.dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_a)); // already there: |= keeps one (:353)
        }
        w.Boot();

        // without A's dependent, C would initialize first (reverse list order)
        Assert.Equal(new[] { "B", "A", "C" }, InitOrder(w));
        using (w.World.Enter())
        {
            Assert.Equal(new object[] { typeof(datum_controller_subsystem_test_mc_a) }, Dm.each(c.dependencies));
            Assert.Equal(new object[] { c }, Dm.each(a.dependents)); // reset to list() (:354), then the reverse graph
        }
        // an entry that is not a subsystem path is skipped with a stack trace (:348-350)
        Exception e = Assert.Single(w.DmRuntimes);
        Assert.Equal("ERROR: MC: subsystem `/datum/controller/subsystem/test_mc_a` has an invalid dependent: `junk`. Skipping", e.Message);
    }

    [Fact]
    public void ASubsystemWithAnEarlierStageThanItsDependencyIsMovedToThatStage()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Init(INITSTAGE_MAIN));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", Init(INITSTAGE_EARLY));
        var c = w.Add<datum_controller_subsystem_test_mc_c>("C", Init(INITSTAGE_EARLY));
        using (w.World.Enter())
            b.dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_a));
        w.Boot();

        Assert.Equal(new[] { "C", "A", "B" }, InitOrder(w));
        using (w.World.Enter())
            Assert.Equal(INITSTAGE_MAIN, b.init_stage);
        Exception e = Assert.Single(w.DmRuntimes);
        Assert.Equal("ERROR: MC: subsystem `/datum/controller/subsystem/test_mc_b` has an init_stage before one of its dependencies "
            + "(Dependency: `/datum/controller/subsystem/test_mc_a`, 2 < 3)! Setting init_stage to 3", e.Message);
    }

    [Fact]
    public void AnInvalidInitStageIsReportedAndBecomesTheLastStage()
    {
        var w = new McWorld();
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", Init(2.5f));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", Init(INITSTAGE_LAST));
        var c = w.Add<datum_controller_subsystem_test_mc_c>("C", Init(0));
        var d = w.Add<datum_controller_subsystem_test_mc_d>("D", Init(INITSTAGE_MAIN));
        w.Boot();

        // sorted [D, C, B, A]; C and A are not whole stages in 1..4 (:425-428) and join stage 4 in that order
        Assert.Equal(new[] { "D", "C", "B", "A" }, InitOrder(w));
        using (w.World.Enter())
        {
            Assert.Equal(INITSTAGE_MAX, a.init_stage);
            Assert.Equal(INITSTAGE_MAX, c.init_stage);
        }
        Assert.Equal(new[]
        {
            "ERROR: MC: subsystem `/datum/controller/subsystem/test_mc_c` has invalid init_stage: `0`. Setting to `4`",
            "ERROR: MC: subsystem `/datum/controller/subsystem/test_mc_a` has invalid init_stage: `2.5`. Setting to `4`",
        }, w.DmRuntimes.Select(e => e.Message));
    }

    [Fact]
    public void CircularDependenciesAreReportedFromTheLoopAndThoseSubsystemsNeverInitializeButStillFire()
    {
        var w = new McWorld();
        var x = w.Add<datum_controller_subsystem_test_mc_a>("X", Init(INITSTAGE_LAST));
        var y = w.Add<datum_controller_subsystem_test_mc_b>("Y", Init(INITSTAGE_LAST));
        var z = w.Add<datum_controller_subsystem_test_mc_c>("Z", Init(INITSTAGE_LAST));
        using (w.World.Enter())
        {
            x.dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_b));
            y.dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_a));
            // X fires: Loop does not look at initialized (master.dm:596-642)
            x.ss_flags = NONE;
            x.wait = 10;
            x.runlevels = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);
        }
        w.Boot();
        w.Step(30);

        Assert.Equal(new[] { "Z" }, InitOrder(w));
        using (w.World.Enter())
        {
            Assert.Equal(FALSE, x.initialized);
            Assert.Equal(FALSE, y.initialized);
            Assert.Equal(0f, x.init_order);
        }
        Assert.Equal(new long[] { 1, 21 }, x.fires);
        Exception e = Assert.Single(w.DmRuntimes);
        Assert.Equal("ERROR: CRITICAL: MC: The following subsystems have circular dependencies: X -> Y -> X", e.Message);
    }

    [Fact]
    public void TheCircularDependencyMessageStartsAtTheFirstRepeatedNode()
    {
        var w = new McWorld();
        var p = w.Add<datum_controller_subsystem_test_mc_a>("P", Init(INITSTAGE_MAIN));
        var q = w.Add<datum_controller_subsystem_test_mc_b>("Q", Init(INITSTAGE_MAIN));
        var r = w.Add<datum_controller_subsystem_test_mc_c>("R", Init(INITSTAGE_MAIN));
        using (w.World.Enter())
        {
            p.dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_b));
            q.dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_c));
            r.dependencies = Dm.list(typeof(datum_controller_subsystem_test_mc_b));
        }
        w.Boot();

        // the walk from P finds P, Q, R, Q; the part before the first Q is cut (master.dm:405-412)
        Assert.Empty(InitOrder(w));
        Exception e = Assert.Single(w.DmRuntimes);
        Assert.Equal("ERROR: CRITICAL: MC: The following subsystems have circular dependencies: Q -> R -> Q", e.Message);
    }

    [Fact]
    public void InitSubsystemHandlesEachResultAndAFailureCannotFire()
    {
        var w = new McWorld();
        var success = w.Add<datum_controller_subsystem_test_mc_a>("Success", Init(INITSTAGE_MAIN, SS_INIT_SUCCESS));
        var failure = w.Add<datum_controller_subsystem_test_mc_b>("Failure", Init(INITSTAGE_LAST, SS_INIT_FAILURE));
        var none = w.Add<datum_controller_subsystem_test_mc_c>("None", Init(INITSTAGE_MAIN, SS_INIT_NONE));
        var no_need = w.Add<datum_controller_subsystem_test_mc_d>("No Need", Init(INITSTAGE_MAIN, SS_INIT_NO_NEED));
        var no_message = w.Add<datum_controller_subsystem_test_mc_e>("No Message", Init(INITSTAGE_MAIN, SS_INIT_NO_MESSAGE));
        var invalid = w.Add<datum_controller_subsystem_test_mc_f>("Invalid", Init(INITSTAGE_MAIN, 7));
        var crasher = w.Add<datum_controller_subsystem_test_mc_g>("Crasher", Init(INITSTAGE_MAIN));
        var no_init = w.Add<datum_controller_subsystem_test_mc_h>("No Init", Init(INITSTAGE_MAIN));
        var already = w.Add<datum_controller_subsystem_test_mc_i>("Already", Init(INITSTAGE_MAIN));
        var control = w.Add<datum_controller_subsystem_test_mc_j>("Control", Init(INITSTAGE_LAST));
        var all = new McFixtureSubsystem[] { success, failure, none, no_need, no_message, invalid, crasher, no_init, already, control };
        datum_test_mc_listener listener;
        using (w.World.Enter())
        {
            crasher.init_crashes = TRUE;
            no_init.ss_flags = Dm.bitor(SS_NO_INIT, SS_NO_FIRE);
            already.initialized = TRUE;
            foreach (McFixtureSubsystem ss in new McFixtureSubsystem[] { failure, control })
            {
                ss.ss_flags = NONE;
                ss.wait = 10;
                ss.runlevels = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);
            }
            listener = Dm.@new<datum_test_mc_listener>();
            foreach (McFixtureSubsystem ss in all)
                listener.RegisterSignal(ss, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(datum_test_mc_listener.on_post_initialize));
        }
        w.Boot();
        w.Step(50);

        using (w.World.Enter())
        {
            // SS_NO_INIT or already initialized: no Initialize call, initialized = TRUE (master.dm:494-496)
            Assert.Equal(-1, no_init.initialized_at);
            Assert.Equal(-1, already.initialized_at);
            Assert.All(all.Where(s => s != no_init && s != already), s => Assert.Equal(0, s.initialized_at));
            // anything but SS_INIT_FAILURE is initialized and sends COMSIG_SUBSYSTEM_POST_INITIALIZE (:516-521); a
            // runtime in Initialize returns null (rules 9.2 boundary here, a translator's rule gap), "with errors"
            Assert.All(all.Where(s => s != failure), s => Assert.Equal(TRUE, s.initialized));
            Assert.Equal(new[] { "Control", "Crasher", "Invalid", "No Message", "No Need", "None", "Success" }, listener.heard.OrderBy(n => n, StringComparer.Ordinal));
            // SS_INIT_FAILURE: not initialized and can_fire FALSE (:524-526)
            Assert.Equal(FALSE, failure.initialized);
            Assert.Equal(FALSE, failure.can_fire);
            Assert.All(all.Where(s => s != failure), s => Assert.Equal(TRUE, s.can_fire));
        }
        Assert.Empty(failure.fires);
        Assert.Equal(new long[] { 1, 21, 41 }, control.fires);
        Exception e = Assert.Single(w.DmRuntimes);
        Assert.Equal("Crasher crashed in Initialize", Assert.IsType<DmCrashException>(e).Message);
    }

    [Fact]
    public void ASleepingSubsystemInitializeHoldsTheLaterStagesWhileTheEarlierOnesFire()
    {
        var w = new McWorld();
        var early = w.Add<datum_controller_subsystem_test_mc_a>("Early", ss =>
        {
            ss.ss_flags = SS_NO_INIT;
            ss.init_stage = INITSTAGE_EARLY;
            ss.wait = 10;
            ss.runlevels = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);
        });
        var slow = w.Add<datum_controller_subsystem_test_mc_b>("Slow", Init(INITSTAGE_MAIN));
        var last = w.Add<datum_controller_subsystem_test_mc_c>("Last", ss =>
        {
            ss.ss_flags = SS_NO_INIT;
            ss.init_stage = INITSTAGE_LAST;
            ss.wait = 10;
            ss.runlevels = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);
        });
        using (w.World.Enter())
            slow.init_sleep_ds = 5;

        w.Boot(); // Initialize's wrapper returns at Slow's sleep, after stage 2
        using (w.World.Enter())
        {
            Assert.True(Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_EARLY));
            Assert.Same(slow, w.Mc.current_initializing_subsystem);
            Assert.False(MC_RUNNING());
            Assert.True(MC_RUNNING(INITSTAGE_EARLY));
        }
        w.Step(9);
        using (w.World.Enter())
        {
            Assert.True(Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_EARLY));
            Assert.Equal(FALSE, slow.initialized);
            Assert.Equal(FALSE, last.initialized);
        }
        w.Step(1); // tick 10: Slow wakes before the MC (it slept first), stages 3 and 4 complete, then Loop(2) -> Loop(4)
        Assert.Equal(new[] { "Slow init woke", "Last fire", "Last end" }, w.TraceAt(10));
        using (w.World.Enter())
        {
            Assert.True(Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX));
            Assert.Equal(TRUE, slow.initialized);
            Assert.True(MC_RUNNING());
        }
        w.Step(30);
        // Loop(1) at tick 0 has no stage-1 subsystem; Loop(2) from tick 1 fires Early at once (next_fire 0)
        Assert.Equal(new long[] { 1, 21 }, early.fires);
        Assert.Equal(new long[] { 10, 30 }, last.fires);
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void McRunningNeedsAMasterThatProcessesARunlevelAndTheStage()
    {
        var w = new McWorld();
        using (w.World.Enter())
        {
            datum_controller_master.init_stage_completed = INITSTAGE_MAX;
            Assert.False(MC_RUNNING()); // no runlevel yet
            w.Mc.SetRunLevel(RUNLEVEL_LOBBY);
            Assert.True(MC_RUNNING());
            datum_controller_master.init_stage_completed = INITSTAGE_MAIN;
            Assert.False(MC_RUNNING()); // the stage defaults to INITSTAGE_MAX
            Assert.True(MC_RUNNING(INITSTAGE_MAIN));
            Assert.False(MC_RUNNING(9)); // max(min(INITSTAGE_MAX, 9), 1) == 4
            Assert.True(MC_RUNNING(0)); // max(min(INITSTAGE_MAX, 0), 1) == 1
            w.Mc.processing = 0;
            Assert.False(MC_RUNNING(0));
            Master = null;
            Assert.False(MC_RUNNING(0));
        }
    }
}
