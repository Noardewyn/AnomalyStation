using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Pilot.Datum;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// Tests of the harness itself. The sample tests that must fail are internal: xunit runs only public test classes, so
// they are run here through RunOnce instead of as tests of their own.

/// <summary>A ported-style test that passes: the signal order contract, with allocate and the TEST_* macros.</summary>
public sealed class datum_signal_order_sample : TgUnitTest
{
    public override async DmTask Run()
    {
        datum source = allocate<datum>();
        var trace = new List<string>();
        test_datum_listener first = allocate<test_datum_listener>();
        test_datum_listener second = allocate<test_datum_listener>();
        first.trace = second.trace = trace;
        first.label = "first";
        second.label = "second";
        first.returns = 1;
        second.returns = 2;
        first.RegisterSignal(source, "pilot_ping", nameof(test_datum_listener.on_signal));
        second.RegisterSignal(source, "pilot_ping", nameof(test_datum_listener.on_signal));

        object result = source._SendSignal("pilot_ping", Dm.list(source, "x"));
        TEST_ASSERT_EQUAL(result, 3, "the returns are ORed");
        TEST_ASSERT_EQUAL(trace.Count, 2);
        TEST_ASSERT_EQUAL(trace[0], "first x ", "registration order");
        TEST_ASSERT_NOTNULL(source._listen_lookup);
        TEST_ASSERT_NULL(source.gc_destroyed);
        TEST_ASSERT(!QDELETED(source), "source is alive");
    }
}

/// <summary>A test whose Run sleeps: the harness steps the world until it returns.</summary>
public sealed class sleeping_run_sample : TgUnitTest
{
    public override async DmTask Run()
    {
        float start = world.time;
        await sleep(2);
        TEST_ASSERT_EQUAL(world.time - start, 2, "slept two deciseconds");
    }
}

internal sealed class failing_assert_sample : TgUnitTest
{
    public bool reached_after;

    public override async DmTask Run()
    {
        TEST_FAIL("soft failure");
        TEST_FAIL(5);
        TEST_ASSERT(1 + 1 == 3, "arithmetic");
        reached_after = true;
    }
}

internal sealed class assert_messages_sample : TgUnitTest
{
    public int which;

    public override async DmTask Run()
    {
        switch (which)
        {
            case 0: TEST_ASSERT_EQUAL(1, 2); break;
            case 1: TEST_ASSERT_EQUAL(null, "b", "with a message"); break;
            case 2: TEST_ASSERT_NOTEQUAL("a", "a"); break;
            case 3: TEST_ASSERT_NULL("x"); break;
            case 4: TEST_ASSERT_NOTNULL(null, "needed"); break;
            case 5: TEST_ASSERT(false, null); break;
        }
    }
}

internal sealed class fails_after_sleep_sample : TgUnitTest
{
    public bool runtime;
    public bool reached_after;

    public override async DmTask Run()
    {
        await sleep(1);
        if (runtime)
            throw Dm.CRASH("late boom", "code/pilot_test.dm", 7);
        TEST_ASSERT_EQUAL(1, 2, "after the sleep");
        reached_after = true;
    }
}

internal sealed class runtime_sample : TgUnitTest
{
    public bool inBoundary;

    public override async DmTask Run()
    {
        if (inBoundary)
        {
            // A runtime that an isolation boundary reports (here a signal handler) fails the current test too.
            datum source = allocate<datum>();
            test_datum_listener l = allocate<test_datum_listener>();
            l.RegisterSignal(source, "pilot_ping", nameof(test_datum_listener.on_signal_fail));
            source._SendSignal("pilot_ping", Dm.list(source));
            return;
        }
        throw Dm.CRASH("boom", "code/pilot_test.dm", 42);
    }
}

internal sealed class not_implemented_sample : TgUnitTest
{
}

internal sealed class notice_sample : TgUnitTest
{
    public bool fail;

    public override async DmTask Run()
    {
        TEST_NOTICE(this, "50% done\nnext");
        if (fail)
            TEST_FAIL("after the notice");
    }
}

internal sealed class allocation_sample : TgUnitTest
{
    public readonly List<datum> made = new();
    public object atom_args_location = "unset";
    public float runs;

    public override async DmTask Run()
    {
        runs++;
        var mine = new List<datum> { allocate<datum>(), (datum)allocate(typeof(test_datum_listener)), allocate<test_datum_user>() };
        made.AddRange(mine);
        TEST_ASSERT_EQUAL(allocated.len, 3);
        TEST_ASSERT_NULL(run_loc_floor_bottom_left, "no test map yet");
        foreach (datum d in mine)
            TEST_ASSERT(!QDELETED(d), "alive during the test");
    }
}

public class TgUnitTestHarnessTests
{
    [Fact]
    public void ASoftFailureContinuesAndAFailedAssertEndsRun()
    {
        var test = new failing_assert_sample();
        string log = test.RunOnce();
        Assert.False(test.reached_after);
        Assert.Equal(FALSE, test.succeeded);
        string[] lines = log.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("\tFAILURE #1: soft failure at tests/Anomaly.Core.Tests/Tg/UnitTests/TgUnitTestHarnessTests.cs:", lines[0]);
        Assert.StartsWith("\tFAILURE #2: FORMATTED: 5 at ", lines[1]);
        Assert.StartsWith("\tFAILURE #3: Assertion failed: arithmetic at ", lines[2]);
    }

    [Theory]
    [InlineData(0, "Expected 1 to be equal to 2.")]
    [InlineData(1, "Expected null to be equal to b. with a message")]
    [InlineData(2, "Expected a to not be equal to a.")]
    [InlineData(3, "Expected null value but received x: No reason")]
    [InlineData(4, "Expected non-null value: needed")]
    [InlineData(5, "Assertion failed: No reason")]
    public void TheAssertMacrosFailWithTgsMessages(int which, string message)
    {
        var test = new assert_messages_sample { which = which };
        string log = test.RunOnce();
        Assert.StartsWith("\tFAILURE #1: " + message + " at tests/", log);
        Assert.DoesNotContain("FAILURE #2", log);
    }

    [Fact]
    public void ARuntimeInRunOrAtABoundaryFailsTheTest()
    {
        string thrown = new runtime_sample().RunOnce();
        Assert.Equal("\tFAILURE #1: Runtime in code/pilot_test.dm,42: DmCrashException: boom at code/pilot_test.dm:42", thrown);

        string reported = new runtime_sample { inBoundary = true }.RunOnce();
        Assert.StartsWith("\tFAILURE #1: Runtime in pilot,1: DmCrashException: pilot handler failure at pilot:1", reported);
    }

    [Fact]
    public void ANoticeDoesNotFailTheTestAndIsLoggedWithAFailure()
    {
        var passing = new notice_sample();
        Assert.Null(passing.RunOnce());
        // log_for_test (unit_test.dm:185-192): % and newlines escaped, GitHub's annotation syntax; no map name yet.
        string notice = Assert.Single(passing.notices);
        Assert.Matches(@"^::notice file=tests/Anomaly.Core.Tests/Tg/UnitTests/TgUnitTestHarnessTests.cs,line=\d+,title=: notice_sample::50%25 done%0Anext$", notice);

        string log = new notice_sample { fail = true }.RunOnce();
        Assert.StartsWith("\tFAILURE #1: after the notice at ", log);
        Assert.EndsWith("\tNOTICE: " + notice, log);
    }

    [Fact]
    public void AFailureAfterRunSleptStillFailsTheTest()
    {
        var asserting = new fails_after_sleep_sample();
        string log = asserting.RunOnce();
        Assert.False(asserting.reached_after);
        Assert.StartsWith("\tFAILURE #1: Expected 1 to be equal to 2. after the sleep at tests/", log);
        Assert.DoesNotContain("FAILURE #2", log);

        Assert.Equal("\tFAILURE #1: Runtime in code/pilot_test.dm,7: DmCrashException: late boom at code/pilot_test.dm:7",
            new fails_after_sleep_sample { runtime = true }.RunOnce());
    }

    [Fact]
    public void ATestWithoutRunFails()
    {
        Assert.StartsWith("\tFAILURE #1: not_implemented_sample/Run() called parent or not implemented at tests/Anomaly.Core.Tests/Tg/UnitTests/TgUnitTest.cs:", new not_implemented_sample().RunOnce());
    }

    [Fact]
    public void AllocatedInstancesAreQdeletedAtTeardownAndEveryRunHasAFreshWorld()
    {
        var test = new allocation_sample { times_to_run = 2 };
        test.RunUnitTest();
        Assert.Equal(2f, test.runs);
        Assert.Equal(6, test.made.Count);
        Assert.All(test.made, d => Assert.NotNull(d.gc_destroyed));
        // Each run starts a fresh world: the ids start over.
        Assert.Equal(test.made[0].dm_id, test.made[3].dm_id);
    }

    [Fact]
    public void FailuresFailTheXunitTest()
    {
        var error = Assert.ThrowsAny<Exception>(() => new failing_assert_sample().RunUnitTest());
        Assert.Contains("failing_assert_sample failed:", error.Message);
        Assert.Contains("Assertion failed: arithmetic", error.Message);
    }
}
