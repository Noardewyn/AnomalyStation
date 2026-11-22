using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// Compile-and-run smoke tests of the datum package (wave 2 pilot): the translated datum core runs end to end in a
/// world. The behaviour tests written from the DM code belong to the test-writer.
/// </summary>
public class SmokeTests
{
    private static World NewWorld(out List<Exception> runtimes)
    {
        var errors = new List<Exception>();
        runtimes = errors;
        return new World(7) { RuntimeErrorSink = errors.Add };
    }

    [Fact]
    public void QdelRunsDestroyQueuesAndUnregistersTheDatum()
    {
        World w = NewWorld(out _);
        using (w.Enter())
        {
            datum_controller_subsystem_garbage garbage = Dm.@new<datum_controller_subsystem_garbage>();
            Assert.Same(garbage, SSgarbage);
            Assert.Equal(3f, garbage.queues.len);

            test_datum_smoke_listener d = Dm.@new<test_datum_smoke_listener>();
            long id = d.dm_id;
            Assert.False(QDELETED(d));
            qdel(d);
            Assert.True(QDELETED(d));
            Assert.Equal(1f, d.destroyed);
            Assert.False(w.Objects.TryGet(id, out _));
            var trash = (datum_qdel_item)garbage.items[typeof(test_datum_smoke_listener)];
            Assert.Equal(1f, trash.qdels);
            Assert.Equal("/datum/test_datum_smoke_listener", trash.name);
        }
    }

    [Fact]
    public void SignalHandlersRunInRegistrationOrderAndTheirReturnsAreOred()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            Dm.@new<datum_controller_subsystem_garbage>();
            datum source = Dm.@new<datum>();
            var calls = new List<string>();
            test_datum_smoke_listener a = Dm.@new<test_datum_smoke_listener>();
            test_datum_smoke_listener b = Dm.@new<test_datum_smoke_listener>();
            a.calls = calls;
            b.calls = calls;
            a.returns = 1;
            b.returns = 4;
            a.RegisterSignal(source, "smoke_ping", nameof(test_datum_smoke_listener.on_ping));
            b.RegisterSignal(source, "smoke_ping", nameof(test_datum_smoke_listener.on_ping));

            object result = source._SendSignal("smoke_ping", Dm.list(source, "x"));
            Assert.Equal(5f, Dm.num(result));
            Assert.Equal(new[] { a.dm_id + " x", b.dm_id + " x" }, calls);

            // One registrant left: the single-registrant path; the lookup collapses back to the datum itself.
            a.UnregisterSignal(source, "smoke_ping");
            Assert.Same(b, source._listen_lookup["smoke_ping"]);
            calls.Clear();
            Assert.Equal(4f, Dm.num(source._SendSignal("smoke_ping", Dm.list(source, "y"))));
            Assert.Single(calls);

            // qdel of the source clears every registration (_clear_signal_refs).
            qdel(source);
            Assert.Null(source._listen_lookup);
            Assert.Equal(0f, b._signal_procs.len);
            Assert.Empty(runtimes);
        }
    }

    [Fact]
    public void AFailingHandlerIsReportedAndTheNextHandlerStillRuns()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            datum source = Dm.@new<datum>();
            var calls = new List<string>();
            test_datum_smoke_listener failing = Dm.@new<test_datum_smoke_listener>();
            test_datum_smoke_listener ok = Dm.@new<test_datum_smoke_listener>();
            ok.calls = calls;
            ok.returns = 2;
            failing.RegisterSignal(source, "smoke_ping", nameof(test_datum_smoke_listener.on_fail));
            ok.RegisterSignal(source, "smoke_ping", nameof(test_datum_smoke_listener.on_ping));

            object result = source._SendSignal("smoke_ping", Dm.list(source, "z"));
            Assert.Equal(2f, Dm.num(result));
            Assert.Single(calls);
            Assert.Single(runtimes);
        }
    }

    [Fact]
    public void ACallbackInvokesItsTargetAndAWeakrefResolvesUntilQdel()
    {
        World w = NewWorld(out _);
        using (w.Enter())
        {
            Dm.@new<datum_controller_subsystem_garbage>();
            var calls = new List<string>();
            test_datum_smoke_listener target = Dm.@new<test_datum_smoke_listener>();
            target.calls = calls;
            target.returns = 3;
            datum_callback cb = Dm.@new<datum_callback>(target, nameof(test_datum_smoke_listener.on_ping), target);
            Assert.Equal(3f, Dm.num(Waiting.NoSleep(cb.Invoke("w"))));
            Assert.Equal(3f, Dm.num(cb.InvokeAsync("v")));
            Assert.Equal(new[] { target.dm_id + " w", target.dm_id + " v" }, calls);

            var weak = (datum_weakref)WEAKREF(target);
            Assert.Same(weak, WEAKREF(target));
            Assert.Same(target, weak.resolve());
            Assert.Equal(REF(target), weak.reference);
            qdel(target);
            Assert.Null(weak.resolve());
            Assert.Null(WEAKREF(target));
        }
    }

    [Fact]
    public void TheSameScenarioTwiceGivesTheSameStateHash()
    {
        static ulong Run()
        {
            World w = new World(11);
            using (w.Enter())
            {
                Dm.@new<datum_controller_subsystem_garbage>();
                datum source = Dm.@new<datum>();
                test_datum_smoke_listener a = Dm.@new<test_datum_smoke_listener>();
                a.RegisterSignal(source, "smoke_ping", nameof(test_datum_smoke_listener.on_ping));
                WEAKREF(a);
                test_datum_smoke_listener gone = Dm.@new<test_datum_smoke_listener>();
                a.target = gone;
                qdel(gone);
            }
            return w.ComputeStateHash();
        }

        ulong first = Run();
        Assert.Equal(first, Run());
        Assert.NotEqual(new World(11).ComputeStateHash(), first);
    }
}

/// <summary>A listener with signal handlers, for the smoke tests.</summary>
[DmPath("/datum/test_datum_smoke_listener")]
public class test_datum_smoke_listener : datum
{
    [NotHashed]
    public List<string> calls;

    public object returns = NONE;

    public object target;

    public float destroyed;

    public override object Destroy(float? force = null)
    {
        destroyed++;
        return base.Destroy(force);
    }

    [SignalHandler]
    public virtual object on_ping(datum source = null, object a = null)
    {
        calls?.Add(dm_id + " " + Dm.str(a));
        return returns;
    }

    [SignalHandler]
    public virtual object on_fail(datum source = null, object a = null)
    {
        throw Dm.CRASH("smoke handler failure", "smoke", 1);
    }
}
