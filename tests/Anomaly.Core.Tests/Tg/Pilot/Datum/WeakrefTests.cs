using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// tg's weak references (code/datums/weakrefs.dm, code/__HELPERS/weakref.dm), written from the DM source.
/// </summary>
public class WeakrefTests
{
    [Fact]
    public void WeakrefMakesOneWeakrefPerDatumAndRefusesWhatIsNotALiveDatum()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum d = Dm.@new<datum>();
            var weak = WEAKREF(d) as datum_weakref;
            Assert.NotNull(weak);
            Assert.Same(weak, d.weak_reference);
            Assert.Same(weak, WEAKREF(d));
            Assert.Same(weak, d.create_weakref());
            // reference = REF(thing) (weakrefs.dm:59)
            Assert.Equal(REF(d), weak.reference);

            // A weakref of a weakref is itself (weakrefs.dm:5-6), and it gets no weakref of its own.
            Assert.Same(weak, WEAKREF(weak));
            Assert.Null(weak.weak_reference);

            Assert.Null(WEAKREF(null));
            t.World.Step(2);
            qdel(d);
            Assert.Null(WEAKREF(d));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ResolveGivesTheDatumUntilItIsDeleted()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            datum d = Dm.@new<datum>();
            var weak = (datum_weakref)WEAKREF(d);
            Assert.Same(d, weak.resolve());
            Assert.Same(d, weak.hard_resolve());

            qdel(d);
            Assert.Null(weak.resolve());
            Assert.Null(weak.hard_resolve());
        }
    }

    [Fact]
    public void HardResolveStillSeesADatumBeingDeletedInsideItsQdelingSignal()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum d = Dm.@new<datum>();
            WEAKREF(d);
            test_datum_listener l = Dm.@new<test_datum_listener>();
            l.trace = trace;
            l.RegisterSignal(d, COMSIG_QDELETING, nameof(test_datum_listener.on_qdeleting_resolve));

            qdel(d);

            // "Unless you're resolving a weakref to a datum in a COMSIG_QDELETING signal handler registered on that
            // very same datum, just use resolve instead." (weakrefs.dm:90-91): QDELETED is already true there.
            Assert.Equal(new[] { "resolve=null hard_resolve=source" }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheRefReuseGuardRejectsADatumThatDoesNotOwnTheWeakref()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum first = Dm.@new<datum>();
            datum second = Dm.@new<datum>();
            var weak = (datum_weakref)WEAKREF(first);
            var other = (datum_weakref)WEAKREF(second);

            // BYOND reuses the ref of a deleted datum for a new one; the weakref then locates a datum whose
            // weak_reference is not src (weakrefs.dm:77, :95). The port never reuses ids, so the reuse is staged by
            // pointing the reference at another datum.
            weak.reference = REF(second);
            Assert.Null(weak.resolve());
            Assert.Null(weak.hard_resolve());
            Assert.Same(second, other.resolve());

            // A datum that dropped its weakref (Destroy does) is not resolved by it either.
            weak.reference = REF(first);
            Assert.Same(first, weak.resolve());
            first.weak_reference = null;
            Assert.Null(weak.resolve());
            Assert.Null(weak.hard_resolve());

            // A reference that names nothing resolves to null.
            weak.reference = "[0x7fffffff]";
            Assert.Null(weak.resolve());
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void QdelOfAWeakrefDeletesItsTargetAndLetsTheWeakrefLiveUnlessForced()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            datum d = Dm.@new<datum>();
            var weak = (datum_weakref)WEAKREF(d);

            // /datum/weakref/Destroy: qdel(resolve()), then QDEL_HINT_LETMELIVE without force (weakrefs.dm:61-66).
            qdel(weak);
            Assert.True(QDELETED(d));
            Assert.False(QDELETED(weak));
            Assert.Null(weak.gc_destroyed);
            Assert.True(t.World.Objects.TryGet(weak.dm_id, out _));

            // With force: target?.weak_reference = null; return ..() (weakrefs.dm:67-68): the weakref is queued.
            datum e = Dm.@new<datum>();
            var forced = (datum_weakref)WEAKREF(e);
            qdel(forced, TRUE);
            Assert.True(QDELETED(e));
            Assert.Null(e.weak_reference);
            Assert.True(QDELETED(forced));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ADatumOnlyItsWeakrefReachesStillResolvesAfterAGarbageCollection()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            datum_weakref weak = OrphanWeakref();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // DV90: BYOND frees the datum at once (refcount 0); the port keeps it until it is deleted, whenever the GC runs,
            // so QDEL_IN's weakref (qdel.dm:60) deletes it the same way in every run.
            var d = weak.resolve() as datum;
            Assert.NotNull(d);
            qdel(weak);
            Assert.True(QDELETED(d));
            Assert.Null(weak.resolve());
            Assert.Empty(t.Runtimes);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static datum_weakref OrphanWeakref() => (datum_weakref)WEAKREF(Dm.@new<datum>());

    [Fact]
    public void IsWeakrefOfComparesTheDatumsOwnWeakref()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum d = Dm.@new<datum>();
            datum e = Dm.@new<datum>();
            object weak = WEAKREF(d);
            Assert.True(IS_WEAKREF_OF(d, weak));
            Assert.False(IS_WEAKREF_OF(e, weak));
            Assert.False(IS_WEAKREF_OF(e, null)); // e has no weakref: null == null, but !isnull(potential_weakref) fails
            Assert.False(IS_WEAKREF_OF("text", weak));
        }
    }

    [Fact]
    public void WeakrefFilterListKeepsTheThingsWhoseWeakrefIsAKeyOfRefs()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            datum a = Dm.@new<datum>();
            datum b = Dm.@new<datum>();
            datum c = Dm.@new<datum>();
            datum gone = Dm.@new<datum>();
            object wa = WEAKREF(a);
            object wc = WEAKREF(c);
            object wgone = WEAKREF(gone);
            qdel(gone);

            // More things than refs: resolve the refs and intersect (weakref.dm:10-17); the deleted one resolves to null.
            DmList things = Dm.list(c, b, a);
            DmList refs = Dm.list(Dm.kv(wa, TRUE), Dm.kv(wgone, TRUE));
            Assert.Equal(new object[] { a }, Dm.each(weakref_filter_list(things, refs)).ToArray());
            Assert.Equal(new object[] { c, b }, Dm.each(weakref_filter_list_reverse(things, refs)).ToArray());

            // At most as many things as refs: look each thing's WEAKREF up in refs (weakref.dm:19-24, :40-45).
            DmList few = Dm.list(a, b);
            DmList many = Dm.list(Dm.kv(wa, TRUE), Dm.kv(wc, TRUE), Dm.kv(wgone, TRUE));
            Assert.Equal(new object[] { a }, Dm.each(weakref_filter_list(few, many)).ToArray());
            Assert.Equal(new object[] { b }, Dm.each(weakref_filter_list_reverse(few, many)).ToArray());
            Assert.NotNull(b.weak_reference); // WEAKREF(i) makes one as a side effect

            // No refs: things as they are; not lists: null.
            DmList none = Dm.list();
            Assert.Same(things, weakref_filter_list(things, none));
            Assert.Same(things, weakref_filter_list_reverse(things, none));
            Assert.Null(weakref_filter_list(null, refs));
            Assert.Null(weakref_filter_list_reverse(things, null));
            Assert.Empty(t.Runtimes);
        }
    }
}
