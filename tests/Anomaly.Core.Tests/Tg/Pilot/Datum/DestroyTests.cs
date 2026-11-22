using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// The cleanup of the base /datum/proc/Destroy (code/datums/datum.dm:125-169), written from the DM source: tag, the
/// DF_USE_TAG flag, the weakref, the active timers, the components, the signals (in that order), then QDEL_HINT_QUEUE.
/// </summary>
public class DestroyTests
{
    [Fact]
    public void BaseDestroyReturnsQueueAndClearsTagFlagAndWeakref()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum d = Dm.@new<datum>();
            // The weakref first, so that it is keyed by the plain ref (with DF_USE_TAG and a tag, REF() is the
            // url-encoded tag, ref.dm:13).
            var weak = (datum_weakref)WEAKREF(d);
            Assert.Same(weak, d.weak_reference);
            d.tag = "pilot_tag";
            Assert.True(t.World.Objects.IsHeldStrongly(d.dm_id)); // a tag is an implicit reference (object-model.md 2)
            d.datum_flags = DF_USE_TAG | DF_ISPROCESSING;

            object hint = d.Destroy();

            Assert.True(Dm.eq(hint, QDEL_HINT_QUEUE));
            Assert.Null(d.tag);
            Assert.False(t.World.Objects.IsHeldStrongly(d.dm_id));
            // datum_flags &= ~DF_USE_TAG //In case something tries to REF us
            Assert.Equal((float)DF_ISPROCESSING, d.datum_flags);
            // weak_reference = null //ensure prompt GCing of weakref.
            Assert.Null(d.weak_reference);
            Assert.Null(weak.hard_resolve());
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void DestroyQdelsTheActiveTimersExceptTheSpentOnesNotMarkedDeleteMe()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>(); // the timers' Destroy ejects them from SStimer (timer.dm:445-447)
            var trace = new List<string>();
            test_datum_timer Timer(string label, float spent, object flags)
            {
                test_datum_timer timer = Dm.@new<test_datum_timer>();
                timer.trace = trace;
                timer.label = label;
                timer.spent = spent;
                timer.flags = flags;
                return timer;
            }
            test_datum_timer pending = Timer("pending", 0, 0);
            test_datum_timer spent = Timer("spent", 5, null); // flags null: null & TIMER_DELETE_ME is 0
            test_datum_timer spentDeleteMe = Timer("spent_delete_me", 5, TIMER_DELETE_ME);
            test_datum_timer pendingDeleteMe = Timer("pending_delete_me", 0, TIMER_DELETE_ME | 1);
            datum d = Dm.@new<datum>();
            DmList timers = Dm.list(pending, spent, spentDeleteMe, pendingDeleteMe);
            d._active_timers = timers;

            qdel(d);

            // if (timer.spent && !(timer.flags & TIMER_DELETE_ME)) continue; qdel(timer) (datum.dm:136-139)
            Assert.Equal(new[] { "pending Destroy", "spent_delete_me Destroy", "pending_delete_me Destroy" }, trace);
            Assert.False(QDELETED(spent));
            Assert.Null(d._active_timers);
            Assert.Equal(4f, timers.len); // the local copy is iterated, not cut
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void DestroyQdelsEveryComponentWithoutForceAndCutsTheList()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            test_datum_component Component(string label)
            {
                test_datum_component c = Dm.@new<test_datum_component>();
                c.trace = trace;
                c.label = label;
                return c;
            }
            test_datum_component single = Component("single");
            test_datum_component first = Component("first");
            test_datum_component second = Component("second");
            datum d = Dm.@new<datum>();
            // type -> component, or type -> list of components (datum.dm:29-34)
            DmList dc = Dm.list(Dm.kv(typeof(test_datum_component), single), Dm.kv(typeof(datum_component), Dm.list(first, second)));
            d._datum_components = dc;

            qdel(d, TRUE);

            // qdel(component, FALSE), whatever force the parent got (datum.dm:150-157), then dc.Cut()
            Assert.Equal(new[] { "single Destroy force=0", "first Destroy force=0", "second Destroy force=0" }, trace);
            Assert.Same(dc, d._datum_components);
            Assert.Equal(0f, dc.len);
            Assert.True(QDELETED(single) && QDELETED(first) && QDELETED(second));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void DestroyCleansUpInTgOrderTimersThenComponentsThenSignals()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>(); // the timer's Destroy ejects it from SStimer (timer.dm:445-447)
            var trace = new List<string>();
            datum d = Dm.@new<datum>();
            d.tag = "pilot_order";
            WEAKREF(d);
            test_datum_order_probe_timer timer = Dm.@new<test_datum_order_probe_timer>();
            timer.trace = trace;
            timer.watched = d;
            d._active_timers = Dm.list(timer);
            test_datum_order_probe_component component = Dm.@new<test_datum_order_probe_component>();
            component.trace = trace;
            component.watched = d;
            d._datum_components = Dm.list(Dm.kv(typeof(test_datum_order_probe_component), component));
            test_datum_listener listener = Dm.@new<test_datum_listener>();
            listener.RegisterSignal(d, "pilot_ping", nameof(test_datum_listener.on_signal));

            qdel(d);

            Assert.Equal(new[]
            {
                // tag, flags and weakref first (datum.dm:129-131), then the timers (:133), while components and
                // signals are still there ...
                "timer: tag=null weakref=null components=1 listeners=yes",
                // ... then the components (:148-158), while the signals are still there ...
                "component: tag=null weakref=null timers=null listeners=yes",
            }, trace);
            // ... then _clear_signal_refs (:160).
            Assert.Null(d._listen_lookup);
            Assert.Empty(t.Runtimes);
        }
    }
}

/// <summary>A timer whose Destroy records what of its owner's Destroy has run.</summary>
[DmPath("/datum/timedevent/test_datum_order_probe_timer")]
public class test_datum_order_probe_timer : datum_timedevent
{
    [NotHashed]
    public List<string> trace;

    public datum watched;

    // ADAPTED: a stand-in that is never scheduled, as test_datum_timer (DatumFixtures.cs).
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        timer_subsystem = SStimer;
    }

    public override object Destroy(float? force = null)
    {
        trace.Add("timer: tag=" + (watched.tag ?? "null") + " weakref=" + (watched.weak_reference == null ? "null" : "set")
            + " components=" + Dm.str(watched._datum_components?.len) + " listeners=" + (watched._listen_lookup != null ? "yes" : "no"));
        return base.Destroy(force);
    }
}

/// <summary>A component whose Destroy records what of its parent's Destroy has run.</summary>
[DmPath("/datum/component/test_datum_order_probe_component")]
public class test_datum_order_probe_component : datum_component
{
    [NotHashed]
    public List<string> trace;

    public datum watched;

    // Built without a parent and put into _datum_components by hand: tg's /datum/component/New(list/raw_args)
    // (datums/components/_component.dm:43, translated by the dcs package) would read raw_args[1] and join the parent.
    public override async DmTask New(params object[] __extra)
    {
    }

    public override object Destroy(float? force = null)
    {
        trace.Add("component: tag=" + (watched.tag ?? "null") + " weakref=" + (watched.weak_reference == null ? "null" : "set")
            + " timers=" + (watched._active_timers == null ? "null" : "set") + " listeners=" + (watched._listen_lookup != null ? "yes" : "no"));
        return base.Destroy(force);
    }
}
