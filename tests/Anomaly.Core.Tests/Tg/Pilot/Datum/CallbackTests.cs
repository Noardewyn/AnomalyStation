using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// tg's /datum/callback (code/datums/callback.dm:43-156) and /world/proc/push_usr (code/__HELPERS/mobs.dm:709-716),
/// written from the DM source.
/// </summary>
public class CallbackTests
{
    private static test_datum_listener Target(List<string> trace, List<object> seenUsr = null, object returns = null)
    {
        test_datum_listener l = Dm.@new<test_datum_listener>();
        l.trace = trace;
        l.label = "target";
        l.seen_usr = seenUsr;
        l.returns = returns ?? NONE;
        return l;
    }

    [Fact]
    public void NewKeepsTheTargetTheProcAndOnlyTheExtraArguments()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            test_datum_listener target = Target(null);
            datum_callback bare = Dm.@new<datum_callback>(target, nameof(test_datum_listener.record_call));
            Assert.Same(target, bare.@object);
            Assert.Equal(nameof(test_datum_listener.record_call), bare.@delegate);
            Assert.Null(bare.arguments); // length(args) > 2 only (callback.dm:66-67)
            Assert.Null(bare.user); // no usr

            datum_callback bound = Dm.@new<datum_callback>(target, nameof(test_datum_listener.record_call), "x", null);
            Assert.Equal(new object[] { "x", null }, Dm.each(bound.arguments).ToArray());

            // A null target leaves the default GLOBAL_PROC (callback.dm:46, :63-64).
            datum_callback global = Dm.@new<datum_callback>(null, "/proc/" + nameof(GlobalProcs.REF), target);
            Assert.Equal(GLOBAL_PROC, global.@object);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InvokePassesTheBoundArgumentsThenTheCallTimeOnes()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            test_datum_listener target = Target(trace, returns: 5);
            datum_callback bound = Dm.@new<datum_callback>(target, nameof(test_datum_listener.record_call), "x");

            Assert.True(Dm.eq(Waiting.NoSleep(bound.Invoke("y", 3)), 5));
            // calling_arguments + args makes a new list: the bound arguments stay clean (callback.dm:110).
            Assert.Equal(new object[] { "x" }, Dm.each(bound.arguments).ToArray());
            Waiting.NoSleep(bound.Invoke());

            datum_callback unbound = Dm.@new<datum_callback>(target, nameof(test_datum_listener.record_call));
            Waiting.NoSleep(unbound.Invoke("only"));
            Waiting.NoSleep(unbound.Invoke());

            Assert.Equal(new[]
            {
                "target called x y 3",
                "target called x null null",
                "target called only null null",
                "target called null null null",
            }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AListArgumentStaysOneArgumentAndADeletedObjectIsStillCalled()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            var trace = new List<string>();
            test_datum_listener target = Target(trace);
            DmList bound = Dm.list(1, 2);
            datum_callback cb = Dm.@new<datum_callback>(target, nameof(test_datum_listener.record_call), bound);

            // arguments = args.Copy(3) and calling_arguments + args (callback.dm:67, :110): list items, not spliced.
            Assert.Same(bound, cb.arguments[1]);
            Waiting.NoSleep(cb.Invoke(Dm.list(3)));
            Assert.Equal(new[] { "target called /list /list null" }, trace);

            // Invoke checks only that object is set (callback.dm:104-105), not whether it was qdeleted.
            qdel(target);
            Waiting.NoSleep(cb.Invoke());
            Assert.Equal(new[] { "target called /list /list null", "target called /list null null" }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AGlobalProcCallbackCallsTheGlobalProc()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum d = Dm.@new<datum>();
            // call(delegate)(arglist(calling_arguments)) (callback.dm:117-118)
            datum_callback cb = Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.REF), d);
            Assert.Equal(REF(d), Waiting.NoSleep(cb.Invoke()));
            Assert.Equal(REF(d), cb.InvokeAsync());

            // A timer cooldown's callback: CALLBACK(GLOBAL_PROC, GLOBAL_PROC_REF(end_cooldown), cd_source, cd_index)
            // (cooldowns.dm:77).
            d.cooldowns = Dm.list(Dm.kv("pilot", 1));
            datum_callback end = Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), d, "pilot");
            end.InvokeAsync();
            Assert.Null(d.cooldowns);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ACallbackWithoutAnObjectDoesNothing()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum_callback cb = Dm.@new<datum_callback>(Target(trace, returns: 1), nameof(test_datum_listener.record_call));
            cb.@object = null;
            Assert.Null(Waiting.NoSleep(cb.Invoke("x")));
            Assert.Null(cb.InvokeAsync("x"));
            Assert.Empty(trace);
        }
    }

    [Fact]
    public void TheUsrOfTheCreatorIsCapturedAndReimposedWhenThereIsNoUsr()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            var seen = new List<object>();
            test_datum_user creator = Dm.@new<test_datum_user>();
            test_datum_user other = Dm.@new<test_datum_user>();
            test_datum_listener target = Target(trace, seen, returns: 9);

            usr = creator;
            datum_callback cb = Dm.@new<datum_callback>(target, nameof(test_datum_listener.record_call), "bound");
            Assert.Same(creator.weak_reference, cb.user); // user = WEAKREF(usr) (callback.dm:68-69)
            usr = null;

            // No usr: world.push_usr(M, src, args...) runs the callback as the creator (callback.dm:95-102).
            Assert.True(Dm.eq(Waiting.NoSleep(cb.Invoke("late")), 9));
            Assert.Null(usr); // push_usr puts usr back (mobs.dm:716)
            Assert.True(Dm.eq(cb.InvokeAsync(), 9));
            Assert.Null(usr);

            // A usr of the caller wins: the stored user is pushed only if(!usr).
            usr = other;
            Waiting.NoSleep(cb.Invoke());
            Assert.Same(other, usr);
            usr = null;

            // The creator is gone: W.resolve() is null and the call runs without usr.
            qdel(creator);
            Waiting.NoSleep(cb.Invoke());

            Assert.Equal(new object[] { creator, creator, other, null }, seen);
            Assert.Equal(new[] { "target called bound late null", "target called bound null null", "target called bound null null", "target called bound null null" }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void PushUsrRunsTheCallbackAsTheGivenMobAndRestoresUsr()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            var seen = new List<object>();
            test_datum_user pushed = Dm.@new<test_datum_user>();
            test_datum_user before = Dm.@new<test_datum_user>();
            datum_callback cb = Dm.@new<datum_callback>(Target(trace, seen, returns: 2), nameof(test_datum_listener.record_call));

            usr = before;
            Assert.True(Dm.eq(Waiting.NoSleep(Dm.world.push_usr(pushed, cb, "a", "b")), 2));
            Assert.Same(before, usr);
            Waiting.NoSleep(Dm.world.push_usr(pushed, cb));
            Assert.Same(before, usr);

            Assert.Equal(new object[] { pushed, pushed }, seen);
            Assert.Equal(new[] { "target called a b null", "target called null null null" }, trace);
        }
    }

    [Fact]
    public void InvokeAsyncReturnsTheValueOnlyWhenTheCalleeDoesNotSleep()
    {
        var t = new DatumTestWorld();
        var trace = new List<string>();
        var seen = new List<object>();
        test_datum_user creator;
        using (t.World.Enter())
        {
            test_datum_listener target = Target(trace, seen);
            datum_callback quick = Dm.@new<datum_callback>(target, nameof(test_datum_listener.sleep_then_return), 0);
            datum_callback slow = Dm.@new<datum_callback>(target, nameof(test_datum_listener.sleep_then_return), 1);

            // sleep(0) still sleeps (it yields), so only a callee that reaches no sleep at all gives its value.
            Assert.Null(quick.InvokeAsync("quick"));
            datum_callback none = Dm.@new<datum_callback>(Target(null, returns: "now"), nameof(test_datum_listener.record_call));
            Assert.Equal("now", none.InvokeAsync());

            // set waitfor = FALSE (callback.dm:130): the caller gets '.' at the callee's first sleep, null here.
            creator = Dm.@new<test_datum_user>();
            usr = creator;
            datum_callback asUser = Dm.@new<datum_callback>(target, nameof(test_datum_listener.sleep_then_return), 1);
            usr = null;
            Assert.Null(slow.InvokeAsync("slow"));
            Assert.Null(asUser.InvokeAsync("as user"));
            Assert.Null(usr);
            Assert.Equal(new[] { "target sleeps", "target sleeps", "target sleeps" }, trace);
        }

        t.World.Step(4);
        // The callees finish on their own; their values are discarded. The one pushed as the creator keeps that usr
        // across its sleep.
        Assert.Equal(new[] { "target sleeps", "target sleeps", "target sleeps", "target woke", "target woke", "target woke" }, trace);
        Assert.Equal(new object[] { null, null, creator, null, null, creator }, seen);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void CallbacksInvokedAsyncOneAfterAnotherEachRunAsTheirOwnUser()
    {
        var t = new DatumTestWorld();
        var seen = new List<object>();
        test_datum_user first;
        test_datum_user second;
        using (t.World.Enter())
        {
            test_datum_listener target = Target(null, seen);
            first = Dm.@new<test_datum_user>();
            second = Dm.@new<test_datum_user>();
            usr = first;
            datum_callback a = Dm.@new<datum_callback>(target, nameof(test_datum_listener.sleep_then_return), 1);
            usr = second;
            datum_callback b = Dm.@new<datum_callback>(target, nameof(test_datum_listener.sleep_then_return), 1);
            usr = null;

            // As SStimer does (timer.dm:125, :172): InvokeAsync pushes the stored user only if(!usr) (callback.dm:132),
            // so the second call works only if the first left usr null when its callee slept.
            Assert.Null(a.InvokeAsync());
            Assert.Null(usr);
            Assert.Null(b.InvokeAsync());
            Assert.Null(usr);
        }
        t.World.Step(3);
        Assert.Equal(new object[] { first, second, first, second }, seen);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void InvokeWaitsForASleepingCalleeAndGivesItsValue()
    {
        var t = new DatumTestWorld();
        var trace = new List<string>();
        object result = "unset";
        using (t.World.Enter())
        {
            datum_callback cb = Dm.@new<datum_callback>(Target(trace), nameof(test_datum_listener.sleep_then_return), 1, "late value");

            async DmTask Caller()
            {
                result = await cb.Invoke();
                trace.Add("caller resumed");
            }
            Waiting.Detach(Caller());
            Assert.Equal("unset", result);
        }
        t.World.Step(1);
        Assert.Equal("unset", result);
        t.World.Step(2);
        Assert.Equal("late value", result);
        Assert.Equal(new[] { "target sleeps", "target woke", "caller resumed" }, trace);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void AnAwaitedInvokeAsTheStoredUserKeepsThatUsrAcrossTheCalleesSleep()
    {
        var t = new DatumTestWorld();
        var trace = new List<string>();
        var seen = new List<object>();
        object result = "unset";
        object callerUsrAfter = "unset";
        test_datum_user creator;
        using (t.World.Enter())
        {
            creator = Dm.@new<test_datum_user>();
            usr = creator;
            datum_callback cb = Dm.@new<datum_callback>(Target(trace, seen), nameof(test_datum_listener.sleep_then_return), 1, "late value");
            usr = null;

            async DmTask Caller()
            {
                // world.push_usr sets usr, waits for Invoke and only then puts usr back (mobs.dm:710-716).
                result = await cb.Invoke();
                callerUsrAfter = usr;
            }
            Waiting.Detach(Caller());
        }
        t.World.Step(3);
        Assert.Equal("late value", result);
        Assert.Equal(new object[] { creator, creator }, seen);
        Assert.Null(callerUsrAfter);
        Assert.Equal(new[] { "target sleeps", "target woke" }, trace);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void InvokeOfASleepingCalleeFromASynchronousContextIsReported()
    {
        var t = new DatumTestWorld();
        var trace = new List<string>();
        using (t.World.Enter())
        {
            datum_callback cb = Dm.@new<datum_callback>(Target(trace), nameof(test_datum_listener.sleep_then_return), 1, "late value");
            // A synchronous proc cannot wait (DV5): NoSleep reports, gives null and lets the callee finish detached.
            Assert.Null(Waiting.NoSleep(cb.Invoke()));
            Assert.IsType<WaitingException>(Assert.Single(t.Runtimes));
        }
        t.World.Step(3);
        Assert.Equal(new[] { "target sleeps", "target woke" }, trace);
        Assert.Single(t.Runtimes);
    }

    [Fact]
    public void AFailingCalleeIsReportedAndTheCallReturnsNull()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum_callback cb = Dm.@new<datum_callback>(Target(trace), nameof(test_datum_listener.fail_call));
            Assert.Null(Waiting.NoSleep(cb.Invoke()));
            Assert.Null(cb.InvokeAsync());
            Assert.Equal(new[] { "target fails", "target fails" }, trace);
            Assert.Equal(new[] { "DmCrashException: pilot callback failure", "DmCrashException: pilot callback failure" }, t.Messages);
        }
    }

    [Fact]
    public void ACallbackCanOnlyBeQdeletedWithForce()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            datum_callback cb = Dm.@new<datum_callback>(Target(null), nameof(test_datum_listener.record_call));
            datum_callback global = Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.REF));

            qdel(cb);
            qdel(global);

            // stack_trace, then QDEL_HINT_LETMELIVE (callback.dm:81-84): both live on.
            Assert.False(QDELETED(cb));
            Assert.False(QDELETED(global));
            Assert.Equal(new[]
            {
                "DmCrashException: " + DatumTestMacros.StackTraceText("Callbacks can not be qdeleted. If they are referenced, they must exist. (/datum/test_datum_listener record_call)", "code/datums/callback.dm", 83),
                "DmCrashException: " + DatumTestMacros.StackTraceText("Callbacks can not be qdeleted. If they are referenced, they must exist. (some_magic_bullshit /proc/REF)", "code/datums/callback.dm", 83),
            }, t.Messages);

            qdel(cb, TRUE);
            Assert.True(QDELETED(cb));
            Assert.Equal(2, t.Runtimes.Count);
        }
    }
}
