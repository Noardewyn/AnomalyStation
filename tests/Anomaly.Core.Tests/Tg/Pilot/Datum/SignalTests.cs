using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Tg.Pilot.Datum.DatumTestMacros;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// The signal contract of tg's code/datums/signals.dm (RegisterSignal :16, RegisterSignals :56, UnregisterSignal :71,
/// _SendSignal :113) and of _clear_signal_refs (datum.dm:173), written from the DM source.
/// </summary>
public class SignalTests
{
    private const string Ping = "pilot_ping";

    private static test_datum_listener Listener(List<string> trace, string label, object returns = null)
    {
        test_datum_listener l = Dm.@new<test_datum_listener>();
        l.trace = trace;
        l.label = label;
        l.returns = returns ?? NONE;
        return l;
    }

    [Fact]
    public void HandlersRunInRegistrationOrderWithTheSourceFirstAndTheSignalArguments()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(trace, "a");
            test_datum_listener b = Listener(trace, "b");
            test_datum_listener c = Listener(trace, "c");
            // lookup[sig] keeps the registration order (signals.dm:48-53), whatever the creation order.
            c.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_source_only));

            SEND_SIGNAL(source, Ping, "x", 2);

            Assert.Equal(new[] { "c x 2", "a x 2", "b source=" + source.dm_id }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AHandlerRegisteredDuringTheDispatchIsNotCalledUntilTheNextSignal()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(trace, "a");
            test_datum_listener b = Listener(trace, "b");
            test_datum_listener late = Listener(trace, "late");
            a.other = late;
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_register_other));
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            // queued_calls is built before the first call (signals.dm:122-126): late is registered by a, after it.
            SEND_SIGNAL(source, Ping, "1", null);
            Assert.Equal(new[] { "a registers late", "b 1 " }, trace);
            Assert.Equal(3f, ((DmList)source._listen_lookup[Ping]).len);

            a.UnregisterSignal(source, Ping);
            trace.Clear();
            SEND_SIGNAL(source, Ping, "2", null);
            Assert.Equal(new[] { "b 2 ", "late 2 " }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AHandlerUnregisteredDuringTheDispatchStillGetsThisSignal()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(trace, "a");
            test_datum_listener b = Listener(trace, "b");
            a.other = b;
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_unregister_other));
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            // "No you can't cancel the signal reception of another object by doing an unregister in the same signal."
            SEND_SIGNAL(source, Ping, "1", null);
            Assert.Equal(new[] { "a unregisters b", "b 1 " }, trace);
            // b's unregistration collapsed the two-item list back to a (signals.dm:83-84).
            Assert.Same(a, source._listen_lookup[Ping]);

            trace.Clear();
            SEND_SIGNAL(source, Ping, "2", null);
            Assert.Equal(new[] { "a unregisters b" }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AListenerQdeletedDuringTheDispatchStillGetsThisSignal()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(trace, "a", returns: 1);
            test_datum_listener b = Listener(trace, "b", returns: 2);
            a.other = b;
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_qdel_other));
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            // queued_calls holds b and its proc (signals.dm:126); b's Destroy unregisters it (datum.dm:186-187), which
            // cannot cancel this final reception.
            Assert.True(Dm.eq(SEND_SIGNAL(source, Ping, "1", null), 3));
            Assert.Equal(new[] { "a qdels b", "b 1 " }, trace);
            Assert.True(QDELETED(b));
            Assert.Same(a, source._listen_lookup[Ping]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheSingleRegistrantCallsOnlyItselfEvenWhenItRegistersAnother()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(trace, "a");
            test_datum_listener late = Listener(trace, "late");
            a.other = late;
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_register_other));

            // The single-registrant path is one call (signals.dm:115-117): the listener a registers is not called.
            SEND_SIGNAL(source, Ping, "1", null);
            Assert.Equal(new[] { "a registers late" }, trace);
            Assert.Equal(new object[] { a, late }, Dm.each(source._listen_lookup[Ping]).ToArray());

            a.UnregisterSignal(source, Ping);
            trace.Clear();
            SEND_SIGNAL(source, Ping, "2", null);
            Assert.Equal(new[] { "late 2 " }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AGlobalProcCanBeTheHandler()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a");
            // "GLOBAL_PROC_REF for global procs" (signals.dm:6-7): call(listening_datum, /proc/qdel)(arglist(arguments))
            // calls the global proc with the signal's arguments, here qdel(source).
            a.RegisterSignal(source, Ping, "/proc/" + nameof(GlobalProcs.qdel));

            SEND_SIGNAL(source, Ping);

            Assert.True(QDELETED(source));
            Assert.False(QDELETED(a));
            Assert.Null(source._listen_lookup);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheSingleRegistrantPathReturnsNoneOrTheHandlersValue()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(trace, "a", returns: 6);
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            // One registrant: lookup[sig] is the datum itself, not a list (signals.dm:48-49), and _SendSignal returns
            // NONE | call(...) (signals.dm:115-117).
            Assert.Same(a, source._listen_lookup[Ping]);
            Assert.True(Dm.eq(source._SendSignal(Ping, Dm.list(source, "x")), 6));

            // A handler that returns null gives NONE | null, which is 0, not null.
            a.returns = null;
            object none = source._SendSignal(Ping, Dm.list(source, "y"));
            Assert.NotNull(none);
            Assert.True(Dm.eq(none, 0));
            Assert.Equal(new[] { "a x ", "a y " }, trace);
        }
    }

    [Fact]
    public void TheReturnsOfSeveralHandlersAreOredWithNullCountingAsNothing()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a", returns: 1);
            test_datum_listener b = Listener(null, "b", returns: 2);
            test_datum_listener n = Listener(null, "n");
            n.returns = null;
            test_datum_listener d = Listener(null, "d", returns: 4 | 1);
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            n.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            d.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            Assert.True(Dm.eq(SEND_SIGNAL(source, Ping), 7));

            // . = NONE before the loop (signals.dm:118): only null returns still give 0.
            a.returns = b.returns = d.returns = null;
            object none = SEND_SIGNAL(source, Ping);
            Assert.NotNull(none);
            Assert.True(Dm.eq(none, 0));

            // No listener for the signal at all: SEND_SIGNAL itself gives NONE without calling _SendSignal.
            Assert.True(Dm.eq(SEND_SIGNAL(source, "pilot_nobody"), NONE));
        }
    }

    [Fact]
    public void HandlersMayDeclareFewerOrMoreParametersThanTheSignalSends()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener few = Listener(trace, "few");
            test_datum_listener none = Listener(trace, "none");
            test_datum_listener more = Listener(trace, "more");
            few.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_source_only));
            none.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_no_params));
            more.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            // call()(arglist(arguments)) binds what the proc declares: extra arguments are dropped, missing ones null.
            SEND_SIGNAL(source, Ping, "only");
            Assert.Equal(new[] { "few source=" + source.dm_id, "none no params", "more only " }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheLookupCollapsesFromTwoListenersToOneAndIsDroppedWhenEmpty()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a");
            test_datum_listener b = Listener(null, "b");
            test_datum_listener c = Listener(null, "c");

            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            Assert.Same(a, source._listen_lookup[Ping]);
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            var two = (DmList)source._listen_lookup[Ping];
            Assert.Equal(new object[] { a, b }, Dm.each(two).ToArray());
            // "Many other things have registered here": looked_up += src changes the same list (signals.dm:53).
            c.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            Assert.Same(two, source._listen_lookup[Ping]);
            Assert.Equal(new object[] { a, b, c }, Dm.each(two).ToArray());

            // Three or more: lookup[sig] -= src, in place (signals.dm:100).
            b.UnregisterSignal(source, Ping);
            Assert.Same(two, source._listen_lookup[Ping]);
            Assert.Equal(new object[] { a, c }, Dm.each(two).ToArray());

            // Two: the list collapses to the remaining datum (signals.dm:83-84).
            a.UnregisterSignal(source, Ping);
            Assert.Same(c, source._listen_lookup[Ping]);

            // The bookkeeping of the listener side: _signal_procs[target][sig] = proctype, and the target is dropped
            // from _signal_procs once it has no signal left (signals.dm:37, :102-104).
            Assert.Equal(nameof(test_datum_listener.on_signal), ((DmList)c._signal_procs[source])[Ping]);
            Assert.False(Dm.@in(source, a._signal_procs));
            Assert.Equal(0f, a._signal_procs.len);

            // One (the datum itself): lookup -= sig, and the empty lookup becomes null (signals.dm:92-98).
            c.UnregisterSignal(source, Ping);
            Assert.Null(source._listen_lookup);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void UnregisteringOneSignalKeepsTheOtherSignalsOfTheLookup()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a");
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            a.RegisterSignal(source, "pilot_other", nameof(test_datum_listener.on_signal));

            a.UnregisterSignal(source, Ping);
            Assert.NotNull(source._listen_lookup);
            Assert.False(Dm.@in(Ping, source._listen_lookup));
            Assert.Same(a, source._listen_lookup["pilot_other"]);
            Assert.Equal(new object[] { "pilot_other" }, Dm.each(a._signal_procs[source]).ToArray());

            // A list of signals is unregistered at once.
            a.RegisterSignals(source, Dm.list(Ping, "pilot_third"), nameof(test_datum_listener.on_signal));
            a.UnregisterSignal(source, Dm.list(Ping, "pilot_other"));
            Assert.Equal(new object[] { "pilot_third" }, Dm.each(a._signal_procs[source]).ToArray());
            Assert.Equal(new object[] { "pilot_third" }, Dm.each(source._listen_lookup).ToArray());
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void UnregisteringWhatWasNeverRegisteredIsSilentUnlessTheSignalIsNotText()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a");
            test_datum_listener stranger = Listener(null, "stranger");

            // Nothing registered between the two: the early return (signals.dm:73-74).
            stranger.UnregisterSignal(source, Ping);
            Assert.Empty(t.Runtimes);

            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            a.UnregisterSignal(source, "pilot_never");
            Assert.Empty(t.Runtimes);
            // A non-text key (a number would index the list, which is out of bounds in DM as in the port).
            a.UnregisterSignal(source, typeof(test_datum_listener));
            Assert.Equal(
                new[] { "DmCrashException: " + StackTraceText("We're unregistering with something that isn't a valid signal [/datum/test_datum_listener], you fucked up", "code/datums/signals.dm", 80) },
                t.Messages);
            // The real registration is untouched.
            Assert.Same(a, source._listen_lookup[Ping]);
        }
    }

    [Fact]
    public void ASingleLengthListInTheLookupIsReportedAndDroppedOnlyByItsListener()
    {
        string expected = "DmCrashException: " + StackTraceText("/datum (/datum) somehow has single length list inside _listen_lookup", "code/datums/signals.dm", 86);
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a");
            test_datum_listener b = Listener(null, "b");
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            b.RegisterSignal(source, "pilot_other", nameof(test_datum_listener.on_signal));
            // The state tg calls impossible (signals.dm:85-91): a one-item list where a lone datum belongs.
            source._listen_lookup[Ping] = Dm.list(a);

            // A listener that is not in that list: the stack_trace, nothing removed from the lookup.
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal), @override: TRUE);
            Assert.Equal(new object[] { a, b }, Dm.each(source._listen_lookup[Ping]).ToArray()); // looked_up += src
            source._listen_lookup[Ping] = Dm.list(a);
            b.UnregisterSignal(source, Ping);
            Assert.Equal(new[] { expected }, t.Messages);
            Assert.Equal(new object[] { a }, Dm.each(source._listen_lookup[Ping]).ToArray());

            // Its listener: the stack_trace, then lookup -= sig; the lookup keeps the other signal.
            a.UnregisterSignal(source, Ping);
            Assert.Equal(new[] { expected, expected }, t.Messages);
            Assert.False(Dm.@in(Ping, source._listen_lookup));
            Assert.Same(b, source._listen_lookup["pilot_other"]);
            Assert.Equal(0f, a._signal_procs.len);
        }
    }

    [Fact]
    public void RegisterSignalIgnoresADeletedTargetOrADeletedListener()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            datum gone = Dm.@new<datum>();
            qdel(gone);
            test_datum_listener a = Listener(null, "a");

            a.RegisterSignal(gone, Ping, nameof(test_datum_listener.on_signal));
            a.RegisterSignal(null, Ping, nameof(test_datum_listener.on_signal));
            Assert.Null(gone._listen_lookup);
            Assert.Null(a._signal_procs);

            datum source = Dm.@new<datum>();
            test_datum_listener dead = Listener(null, "dead");
            qdel(dead);
            dead.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            Assert.Null(source._listen_lookup);
            Assert.Null(dead._signal_procs);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ReRegisteringWithOverrideReplacesTheProcSilently()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(trace, "a");
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_no_params), @override: TRUE);

            // The proc is replaced (signals.dm:37) and the listener is not added to the lookup a second time (:44).
            Assert.Same(a, source._listen_lookup[Ping]);
            Assert.Equal(nameof(test_datum_listener.on_signal_no_params), ((DmList)a._signal_procs[source])[Ping]);
            SEND_SIGNAL(source, Ping, "x");
            Assert.Equal(new[] { "a no params" }, trace);

            // RegisterSignals hands override on to each RegisterSignal (signals.dm:56-58).
            a.RegisterSignals(source, Dm.list(Ping, "pilot_other"), nameof(test_datum_listener.on_signal), TRUE);
            Assert.Equal(nameof(test_datum_listener.on_signal), ((DmList)a._signal_procs[source])[Ping]);
            Assert.Same(a, source._listen_lookup["pilot_other"]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ReRegisteringWithoutOverrideReplacesTheProcLogsAndStackTraces()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            // tg's Genesis creates the logger before anything logs (game/world.dm:97); log_signal (logging/debug.dm:59)
            // hands the message to it, which queues it until init_logging (log_holder.dm:297-298).
            logger = Dm.@new<datum_log_holder>();
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(trace, "a");
            test_datum_listener b = Listener(trace, "b");
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_no_params));

            // The new proc replaces the old one even without override (signals.dm:36-37); the listener list is unchanged.
            Assert.Equal(nameof(test_datum_listener.on_signal_no_params), ((DmList)a._signal_procs[source])[Ping]);
            Assert.Equal(new object[] { a, b }, Dm.each(source._listen_lookup[Ping]).ToArray());

            // log_signal(override_message), then stack_trace(override_message) (signals.dm:41-43).
            string override_message = "pilot_ping overridden. Use override = TRUE to suppress this warning.\nTarget: /datum (/datum) Existing Proc: on_signal New Proc: on_signal_no_params";
            var queued = Assert.IsType<DmList>(Assert.Single(Dm.each(logger.waiting_log_calls)));
            Assert.Equal(new object[] { LOG_CATEGORY_SIGNAL, override_message, null }, Dm.each(queued).ToArray());
            var crash = Assert.IsType<DmCrashException>(Assert.Single(t.Runtimes));
            Assert.Equal(StackTraceText(override_message, "code/datums/signals.dm", 43), crash.Message);
            Assert.Equal("code/datums/signals.dm", crash.DmFile);
            Assert.Equal(43, crash.DmLine);

            SEND_SIGNAL(source, Ping, "x");
            Assert.Equal(new[] { "a no params", "b x " }, trace);
        }
    }

    [Fact]
    public void RegisteringAListOfSignalsWarnsOncePerMessageAndRegistersEach()
    {
        string expected = StackTraceText(
            "/datum (/datum) is registering pilot_ping, pilot_other as a list, the older method. Change it to RegisterSignals.",
            "code/datums/signals.dm", 27);
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a");
            test_datum_listener b = Listener(null, "b");
            a.RegisterSignal(source, Dm.list(Ping, "pilot_other"), nameof(test_datum_listener.on_signal));
            Assert.Equal(new[] { "DmCrashException: " + expected }, t.Messages);
            Assert.Same(a, source._listen_lookup[Ping]);
            Assert.Same(a, source._listen_lookup["pilot_other"]);

            // var/static/list/known_failures (signals.dm:21): the same message is reported once per world.
            b.RegisterSignal(source, Dm.list(Ping, "pilot_other"), nameof(test_datum_listener.on_signal));
            Assert.Single(t.Runtimes);
            Assert.Equal(new object[] { a, b }, Dm.each(source._listen_lookup["pilot_other"]).ToArray());
        }

        var fresh = new DatumTestWorld();
        using (fresh.World.Enter())
        {
            datum source = Dm.@new<datum>();
            Listener(null, "c").RegisterSignal(source, Dm.list(Ping, "pilot_other"), nameof(test_datum_listener.on_signal));
            Assert.Equal(new[] { "DmCrashException: " + expected }, fresh.Messages);
        }
    }

    [Fact]
    public void AFailingHandlerIsReportedContributesNothingAndTheNextHandlerRuns()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            test_datum_listener first = Listener(trace, "first", returns: 1);
            test_datum_listener failing = Listener(trace, "failing", returns: 2);
            test_datum_listener last = Listener(trace, "last", returns: 4);
            first.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            failing.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_fail));
            last.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            Assert.True(Dm.eq(SEND_SIGNAL(source, Ping, "x"), 5));
            Assert.Equal(new[] { "first x ", "failing fails", "last x " }, trace);
            Assert.Equal(new[] { "DmCrashException: pilot handler failure" }, t.Messages);

            // The single-registrant path is a boundary too: NONE | null.
            datum lone = Dm.@new<datum>();
            failing.RegisterSignal(lone, Ping, nameof(test_datum_listener.on_signal_fail));
            Assert.True(Dm.eq(SEND_SIGNAL(lone, Ping), 0));
            Assert.Equal(2, t.Runtimes.Count);
        }
    }

    [Fact]
    public void AHandlerThatSleepsIsReportedAndFinishesDetached()
    {
        var t = new DatumTestWorld();
        var trace = new List<string>();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener sleeper = Listener(trace, "sleeper", returns: 1);
            test_datum_listener after = Listener(trace, "after", returns: 2);
            sleeper.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal_sleep));
            after.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            // SIGNAL_HANDLER procs never sleep in tg (lint); one that does is a runtime of the port's no-sleep rule
            // (DV5): the dispatch carries on without its value.
            Assert.True(Dm.eq(SEND_SIGNAL(source, Ping, "x"), 2));
            Assert.Equal(new[] { "sleeper sleeps", "after x " }, trace);
            Assert.Single(t.Runtimes);
            Assert.IsType<WaitingException>(t.Runtimes[0]);
        }
        t.World.Step(3);
        Assert.Equal(new[] { "sleeper sleeps", "after x ", "sleeper woke" }, trace);
        Assert.Single(t.Runtimes);
    }

    [Fact]
    public void DeletingTheSourceClearsEveryRegistrationOnBothSides()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a");
            test_datum_listener b = Listener(null, "b");
            test_datum_listener c = Listener(null, "c");
            test_datum_listener d = Listener(null, "d");
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            c.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            d.RegisterSignal(source, "pilot_other", nameof(test_datum_listener.on_signal));
            a.RegisterSignal(source, "pilot_other", nameof(test_datum_listener.on_signal));

            // The source listens to others as well: one where it is alone, one shared with another listener.
            datum alone = Dm.@new<datum>();
            datum shared = Dm.@new<datum>();
            test_datum_listener e = Listener(null, "e");
            source.RegisterSignal(alone, Ping, nameof(datum.selfdelete));
            source.RegisterSignal(shared, Ping, nameof(datum.selfdelete));
            e.RegisterSignal(shared, Ping, nameof(test_datum_listener.on_signal));

            qdel(source);

            // _clear_signal_refs (datum.dm:173-187): every listener unregisters itself from the source ...
            Assert.Null(source._listen_lookup);
            foreach (test_datum_listener l in new[] { a, b, c, d })
            {
                Assert.False(Dm.@in(source, l._signal_procs));
                Assert.Equal(0f, l._signal_procs.len);
            }
            // ... and the source unregisters from everything it listened to.
            Assert.Null(alone._listen_lookup);
            Assert.Same(e, shared._listen_lookup[Ping]);
            Assert.Equal(0f, source._signal_procs.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void DeletingAListenerRemovesItFromWhatItListenedTo()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a");
            test_datum_listener b = Listener(null, "b");
            a.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));
            b.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            qdel(a);
            Assert.Same(b, source._listen_lookup[Ping]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ASourceDeletedByAnEarlierHandlerStillSignalsTheQueuedListeners()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            // The source deletes itself on the signal, before the other listener's turn.
            source.RegisterSignal(source, Ping, nameof(datum.selfdelete));
            test_datum_listener after = Listener(trace, "after", returns: 8);
            after.RegisterSignal(source, Ping, nameof(test_datum_listener.on_signal));

            // The queued calls carry the listener and its proc (signals.dm:126); nothing reads _listen_lookup again.
            Assert.True(Dm.eq(SEND_SIGNAL(source, Ping, "x"), 8));
            Assert.True(QDELETED(source));
            Assert.Null(source._listen_lookup);
            Assert.Equal(new[] { "after x " }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void SelfdeleteAsAHandlerQdelsTheListener()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            test_datum_listener a = Listener(null, "a");
            a.RegisterSignal(source, Ping, nameof(datum.selfdelete));

            SEND_SIGNAL(source, Ping, "x", "y");
            Assert.True(QDELETED(a));
            Assert.Null(source._listen_lookup);
            Assert.Empty(t.Runtimes);
        }
    }
}
