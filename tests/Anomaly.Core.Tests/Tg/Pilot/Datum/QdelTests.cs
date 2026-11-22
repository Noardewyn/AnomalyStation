using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// tg's /proc/qdel (controllers/subsystem/garbage.dm:346-428), the SSgarbage parts it calls (Queue :266, HardDelete
/// :281, /datum/qdel_item :325) and the qdel macros (__DEFINES/qdel.dm:53-67), written from the DM source. The port's
/// SSgarbage keeps no queues and never hard deletes (DV7): it removes the datum from the world registry instead.
/// </summary>
public class QdelTests
{
    private static test_datum_destroyable Destroyable(List<string> trace, string label = "d")
    {
        test_datum_destroyable d = Dm.@new<test_datum_destroyable>();
        d.trace = trace;
        d.label = label;
        return d;
    }

    private static test_datum_listener Watch(datum target, string signal, List<string> trace, string label, object returns = null)
    {
        test_datum_listener l = Dm.@new<test_datum_listener>();
        l.trace = trace;
        l.label = label;
        l.returns = returns ?? NONE;
        l.RegisterSignal(target, signal, nameof(test_datum_listener.on_qdel_signal));
        return l;
    }

    private static datum_qdel_item Trash(Type type) => SSgarbage.items[type] as datum_qdel_item;

    private static bool Registered(datum d) => World.Current.Objects.TryGet(d.dm_id, out _);

    [Fact]
    public void QdelSignalsPreqdeletedThenMarksThenSignalsQdelingThenDestroysThenQueues()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(4); // world.time 2
            var trace = new List<string>();
            test_datum_destroyable d = Destroyable(trace);
            Watch(d, COMSIG_PREQDELETED, trace, "preqdeleted");
            Watch(d, COMSIG_QDELETING, trace, "qdeleting");

            qdel(d);

            Assert.Equal(new[]
            {
                "preqdeleted gc_destroyed=null force=0 registered=yes",
                "qdeleting gc_destroyed=-2 force=0 registered=yes",
                "d Destroy gc_destroyed=-2 force=0 QDESTROYING=yes",
            }, trace);
            // QDEL_HINT_QUEUE: SSgarbage.Queue sets gc_destroyed = world.time (garbage.dm:274-275), then (DV7) the
            // datum leaves the world registry.
            Assert.True(Dm.eq(d.gc_destroyed, 2));
            Assert.False(Registered(d));
            Assert.True(QDELETED(d));
            Assert.True(QDELING(d));
            Assert.False(QDESTROYING(d));
            datum_qdel_item trash = Trash(typeof(test_datum_destroyable));
            Assert.Equal("/datum/test_datum_destroyable", trash.name);
            Assert.Equal(1f, trash.qdels);
            Assert.Equal(0f, trash.no_hint);
            Assert.Equal(0f, trash.slept_destroy);
            Assert.Equal(0f, trash.destroy_time);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnImageIsQdeledAsTheDatumItIsInByond()
    {
        // syndicate N1c: an operative's antag HUD qdels its images (antag_hud.dm:54); BYOND's /image is a /datum, so no stack trace and
        // no hard delete, and nothing else changes
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var picture = image("icons/mob/huds/antag_hud.dmi", null, "synd");
            var held = Dm.list(picture);
            QDEL_LIST(held);
            Assert.Equal(0f, held.len);
            Assert.Equal("synd", picture.icon_state);
        }
    }

    [Fact]
    public void ForceReachesBothSignalsAndDestroy()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            test_datum_destroyable d = Destroyable(trace);
            Watch(d, COMSIG_PREQDELETED, trace, "preqdeleted");
            Watch(d, COMSIG_QDELETING, trace, "qdeleting");

            qdel(d, TRUE);

            Assert.Equal(new[]
            {
                "preqdeleted gc_destroyed=null force=1 registered=yes",
                "qdeleting gc_destroyed=-2 force=1 registered=yes",
                "d Destroy gc_destroyed=-2 force=1 QDESTROYING=yes",
            }, trace);
        }
    }

    [Fact]
    public void ATruthyPreqdeletedReturnCancelsTheDeletion()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(4);
            var trace = new List<string>();
            test_datum_destroyable d = Destroyable(trace);
            test_datum_listener guard = Watch(d, COMSIG_PREQDELETED, trace, "guard", returns: TRUE);
            Watch(d, COMSIG_QDELETING, trace, "qdeleting");

            qdel(d);

            // "Give the components a chance to prevent their parent from being deleted" (garbage.dm:370-371): the
            // statistics already counted the call, nothing else happened.
            Assert.Equal(new[] { "guard gc_destroyed=null force=0 registered=yes" }, trace);
            Assert.Null(d.gc_destroyed);
            Assert.True(Registered(d));
            Assert.False(QDELETED(d));
            Assert.Equal(0f, d.destroy_calls);
            Assert.Equal(1f, Trash(typeof(test_datum_destroyable)).qdels);

            // A falsy return lets it through.
            guard.returns = 0;
            trace.Clear();
            qdel(d);
            Assert.Equal(1f, d.destroy_calls);
            Assert.True(QDELETED(d));
            Assert.Equal(2f, Trash(typeof(test_datum_destroyable)).qdels);
        }
    }

    [Fact]
    public void ASecondQdelOfADeletedDatumOnlyCountsIt()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(4);
            test_datum_destroyable d = Destroyable(null);
            qdel(d);
            qdel(d);
            qdel(d, TRUE);

            // !isnull(gc_destroyed) and not GC_CURRENTLY_BEING_QDELETED: return (garbage.dm:365-368).
            Assert.Equal(1f, d.destroy_calls);
            Assert.True(Dm.eq(d.gc_destroyed, 2));
            Assert.Equal(3f, Trash(typeof(test_datum_destroyable)).qdels);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AQdelLoopInDestroyCrashes()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            test_datum_destroyable d = Destroyable(null);
            d.qdel_self_in_destroy = 1;

            // tg: the inner qdel CRASHes (garbage.dm:366-367) and, a DM runtime ending only the failing proc, Destroy
            // and the outer qdel carry on. The port (DV1) unwinds the CRASH through Destroy to the isolation boundary
            // at qdel's Destroy call (translation-rules.md 9.2), which reports it; the outer qdel carries on with a null
            // hint, as in tg once Destroy has ended.
            qdel(d);
            Assert.Equal(new[] { "DmCrashException: /datum/test_datum_destroyable destroy proc was called multiple times, likely due to a qdel loop in the Destroy logic" }, t.Messages);
            Assert.Equal(2f, Trash(typeof(test_datum_destroyable)).qdels);
            Assert.Equal(1f, Trash(typeof(test_datum_destroyable)).no_hint);
            Assert.True(QDELETED(d));
            Assert.False(Dm.eq(d.gc_destroyed, GC_CURRENTLY_BEING_QDELETED));
            Assert.False(Registered(d));
        }
    }

    [Fact]
    public void AQdelInsideQdelingCrashesInTheHandlerOnlyAndTheDeletionGoesOn()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(4);
            var trace = new List<string>();
            test_datum_destroyable d = Destroyable(trace);
            test_datum_listener l = Dm.@new<test_datum_listener>();
            l.RegisterSignal(d, COMSIG_QDELETING, "/proc/" + nameof(GlobalProcs.qdel));

            qdel(d);

            // The handler's qdel(source, force) meets GC_CURRENTLY_BEING_QDELETED and CRASHes (garbage.dm:366-367);
            // the handler call is an isolation boundary (signals.dm:113, translation-rules.md 9.2), so qdel goes on
            // with Destroy and the hint as in tg, where the CRASH ends only the inner qdel.
            Assert.Equal(new[] { "DmCrashException: /datum/test_datum_destroyable destroy proc was called multiple times, likely due to a qdel loop in the Destroy logic" }, t.Messages);
            Assert.Equal(new[] { "d Destroy gc_destroyed=-2 force=0 QDESTROYING=yes" }, trace);
            Assert.True(Dm.eq(d.gc_destroyed, 2));
            Assert.False(Registered(d));
            Assert.Equal(2f, Trash(typeof(test_datum_destroyable)).qdels);
        }
    }

    [Fact]
    public void ARuntimeInDestroyEndsDestroyAndQdelCarriesOnWithANullHint()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            test_datum_failing_destroyable d = Dm.@new<test_datum_failing_destroyable>();

            // tg: the runtime ends Destroy, whose '.' is still null, so qdel carries on with a null hint: no_hint++ and
            // SSgarbage.Queue (garbage.dm:422-428), and the datum is deleted. The port's isolation boundary at qdel's
            // Destroy call (translation-rules.md 9.2) reports the runtime and gives the same result.
            qdel(d);
            Assert.Single(t.Messages);
            Assert.True(Dm.eq(d.gc_destroyed, 1));
            Assert.False(Registered(d));
            Assert.Equal(1f, Trash(typeof(test_datum_failing_destroyable)).no_hint);
        }
    }

    [Fact]
    public void QueueHintQueuesTheDatum()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(6); // world.time 3
            test_datum_destroyable d = Destroyable(null);
            d.hint = QDEL_HINT_QUEUE;
            qdel(d);
            Assert.True(Dm.eq(d.gc_destroyed, 3));
            Assert.False(Registered(d));
            datum_qdel_item trash = Trash(typeof(test_datum_destroyable));
            Assert.Equal(0f, trash.no_hint);
            Assert.Equal(0f, trash.no_respect_force);
            Assert.Equal(0f, trash.hard_deletes);
            Assert.Equal(0f, SSgarbage.totaldels);
        }
    }

    [Fact]
    public void IWillGcHintMarksTheTimeAndLeavesTheRegistry()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(6);
            test_datum_destroyable d = Destroyable(null);
            d.hint = QDEL_HINT_IWILLGC;
            qdel(d);
            // to_delete.gc_destroyed = world.time; return (garbage.dm:390-392); the port also unregisters (DV7).
            Assert.True(Dm.eq(d.gc_destroyed, 3));
            Assert.False(Registered(d));
            Assert.Equal(0f, Trash(typeof(test_datum_destroyable)).no_hint);
        }
    }

    [Fact]
    public void LetMeLiveHintWithoutForceKeepsTheDatumAlive()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(6);
            var trace = new List<string>();
            test_datum_destroyable d = Destroyable(trace);
            d.hint = QDEL_HINT_LETMELIVE;
            qdel(d);

            // gc_destroyed = null //clear the gc variable (important!) (garbage.dm:394-396)
            Assert.Null(d.gc_destroyed);
            Assert.True(Registered(d));
            Assert.False(QDELETED(d));
            Assert.Equal(0f, Trash(typeof(test_datum_destroyable)).no_respect_force);

            // It can be qdel'd again, and Destroy runs again.
            qdel(d);
            Assert.Equal(2f, d.destroy_calls);
            Assert.Null(d.gc_destroyed);
        }
    }

    [Fact]
    public void LetMeLiveHintWithForceIsCountedAndQueued()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(6);
            test_datum_destroyable d = Destroyable(null);
            d.hint = QDEL_HINT_LETMELIVE;
            qdel(d, TRUE);

            // "indicates the objects Destroy() does not respect force" (garbage.dm:397-409).
            Assert.Equal(1f, Trash(typeof(test_datum_destroyable)).no_respect_force);
            Assert.True(Dm.eq(d.gc_destroyed, 3));
            Assert.False(Registered(d));
        }
    }

    [Fact]
    public void HardDelHintQueuesInTheHardDeleteQueueWithoutHardDeletingNow()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(6);
            var trace = new List<string>();
            test_datum_destroyable d = Destroyable(trace);
            d.hint = QDEL_HINT_HARDDEL;
            qdel(d);

            // SSgarbage.Queue(to_delete, GC_QUEUE_HARDDELETE): level 3 is not above GC_QUEUE_COUNT, so it is queued
            // (garbage.dm:269, :410-411), with gc_destroyed set; the hard delete would come from the queue (DV7).
            Assert.True(Dm.eq(d.gc_destroyed, 3));
            Assert.False(Registered(d));
            Assert.Equal(0f, Trash(typeof(test_datum_destroyable)).hard_deletes);
            Assert.Equal(0f, SSgarbage.totaldels);
            Assert.DoesNotContain("dump_harddel_info", trace);
        }
    }

    [Fact]
    public void HardDelNowHintHardDeletesWithItsStatistics()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(6);
            var trace = new List<string>();
            test_datum_destroyable d = Destroyable(trace);
            d.hint = QDEL_HINT_HARDDEL_NOW;
            d.harddel_info = "pilot detail";
            qdel(d);

            // SSgarbage.HardDelete (garbage.dm:281-306): the counters, the details of dump_harddel_info, the timing
            // (0 ms: tick usage is virtual, DV3); del(D) is the registry removal plus gc_destroyed = world.time (DV7),
            // so a stale reference reads QDELETED and a second qdel returns quietly (BYOND's del nulls the reference).
            Assert.Equal(1f, SSgarbage.delslasttick);
            Assert.Equal(1f, SSgarbage.totaldels);
            datum_qdel_item trash = Trash(typeof(test_datum_destroyable));
            Assert.Equal(1f, trash.hard_deletes);
            Assert.Equal(0f, trash.hard_delete_time);
            Assert.Equal(0f, trash.hard_delete_max);
            Assert.Equal(new object[] { "pilot detail" }, Dm.each(trash.extra_details).ToArray());
            Assert.Equal(0f, SSgarbage.highest_del_ms);
            Assert.Equal("", SSgarbage.highest_del_type_string);
            Assert.Contains("dump_harddel_info", trace);
            Assert.False(Registered(d));
            Assert.True(Dm.eq(d.gc_destroyed, 3));
            Assert.True(QDELETED(d));
            qdel(d);
            Assert.Empty(t.Messages);
            Assert.Empty(t.Runtimes);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(99)]
    [InlineData("not a hint")]
    public void AnyOtherHintCountsAsNoHintAndQueues(object hint)
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(6);
            test_datum_destroyable d = Destroyable(null);
            d.hint = hint;
            qdel(d);

            // switch(hint) ... else: trash.no_hint++; SSgarbage.Queue(to_delete) (garbage.dm:422-428). A null hint is
            // not QDEL_HINT_QUEUE (null == 0 is false in DM).
            Assert.Equal(1f, Trash(typeof(test_datum_destroyable)).no_hint);
            Assert.True(Dm.eq(d.gc_destroyed, 3));
            Assert.False(Registered(d));
        }
    }

    [Fact]
    public void QueueKeepsAnEarlierDeletionTimeAndAboveTheLastQueueHardDeletes()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(8); // world.time 4
            test_datum_destroyable early = Destroyable(null);
            early.gc_destroyed = 1;
            SSgarbage.Queue(early);
            // if (D.gc_destroyed <= 0) D.gc_destroyed = queue_time (garbage.dm:274-275)
            Assert.True(Dm.eq(early.gc_destroyed, 1));
            Assert.False(Registered(early));

            SSgarbage.Queue(null);

            // A level above GC_QUEUE_COUNT is a hard delete (garbage.dm:269-271); HardDelete reads the qdel_item that
            // qdel made for the type.
            test_datum_destroyable first = Destroyable(null);
            qdel(first);
            test_datum_destroyable beyond = Destroyable(null);
            SSgarbage.Queue(beyond, GC_QUEUE_COUNT + 1);
            Assert.Equal(1f, Trash(typeof(test_datum_destroyable)).hard_deletes);
            // HardDelete marks the datum deleted at world.time (DV7: in place of BYOND's del nulling its references).
            Assert.True(Dm.eq(beyond.gc_destroyed, 4));
            Assert.False(Registered(beyond));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void QdelKeepsOneStatisticsItemPerType()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            qdel(Destroyable(null));
            qdel(Destroyable(null));
            qdel(Dm.@new<test_datum_listener>());

            Assert.Equal(2f, Trash(typeof(test_datum_destroyable)).qdels);
            Assert.Equal(1f, Trash(typeof(test_datum_listener)).qdels);
            Assert.Equal("/datum/test_datum_listener", Trash(typeof(test_datum_listener)).name);
        }
    }

    [Fact]
    public void QdelOfNullDoesNothingAndOfANonDatumStackTracesBeforeTheHardDelete()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            qdel(null);
            Assert.Empty(t.Runtimes);
            Assert.Equal(0f, SSgarbage.items.len);

            // del(to_delete) is not translated (object-model.md 7): report it instead of throwing.
            DmWorldState.Current.NotTranslatedThrows = false;
            qdel(Dm.list(1));
            qdel("text");
            qdel(Dm.world);
            Assert.Equal(new[]
            {
                "DmCrashException: " + DatumTestMacros.StackTraceText("Lists should not be directly passed to qdel! You likely want either list.Cut(), QDEL_LIST(list), QDEL_LIST_ASSOC(list), or QDEL_LIST_ASSOC_VAL(list)", "code/controllers/subsystem/garbage.dm", 351),
                "DmNotTranslatedException: " + new DmNotTranslatedException("code/controllers/subsystem/garbage.dm:357", "del(to_delete): hard delete is not translated (object-model.md section 7, DV7)").Message,
                "DmCrashException: " + DatumTestMacros.StackTraceText("Tried to qdel possibly invalid value: text", "code/controllers/subsystem/garbage.dm", 353),
            }, t.Messages);
            Assert.Equal(0f, SSgarbage.items.len);
        }
    }

    [Fact]
    public void AQdelAtWorldTimeZeroCountsAsDeleted()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            Assert.Equal(0f, Dm.world.time);
            test_datum_destroyable d = Destroyable(null);
            qdel(d);

            // gc_destroyed is world.time = 0. tg's QDELING tests its truthiness and would call the datum alive; the
            // port tests it against null (DEVIATION DV2), so the datum is deleted as at any later time.
            Assert.NotNull(d.gc_destroyed);
            Assert.True(Dm.eq(d.gc_destroyed, 0));
            Assert.True(QDELING(d));
            Assert.True(QDELETED(d));

            // So everything that tests QDELETED treats it as gone: RegisterSignal, WEAKREF, a second qdel.
            test_datum_listener l = Dm.@new<test_datum_listener>();
            l.RegisterSignal(d, "pilot_ping", nameof(test_datum_listener.on_signal));
            Assert.Null(d._listen_lookup);
            Assert.Null(WEAKREF(d));
            qdel(d);
            Assert.Equal(1f, d.destroy_calls);
            Assert.Equal(0f, Dm.num(is_valid_src(d)));
        }
    }

    [Fact]
    public void QdestroyingIsTrueForNullAndDuringDestroyOnly()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            var trace = new List<string>();
            test_datum_destroyable d = Destroyable(trace);
            Assert.True(QDESTROYING(null));
            Assert.False(QDESTROYING(d));
            qdel(d);
            Assert.Equal(new[] { "d Destroy gc_destroyed=-2 force=0 QDESTROYING=yes" }, trace);
            Assert.False(QDESTROYING(d));
        }
    }

    [Fact]
    public void QdelNullDeletesAndClearsTheVar()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            test_datum_destroyable d = Destroyable(null);
            test_datum_destroyable held = d;
            QDEL_NULL(ref held);
            Assert.Null(held);
            Assert.True(QDELETED(d));
        }
    }

    [Fact]
    public void TheQdelListMacrosDeleteWhatTheyNameAndCutTheList()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            test_datum_destroyable a = Destroyable(trace, "a");
            test_datum_destroyable b = Destroyable(trace, "b");
            DmList plain = Dm.list(a, b);
            QDEL_LIST(plain);
            Assert.Equal(0f, plain.len);
            Assert.True(QDELETED(a) && QDELETED(b));

            // QDEL_LIST_ASSOC: the value, then the key (qdel.dm:66).
            trace.Clear();
            test_datum_destroyable key = Destroyable(trace, "key");
            test_datum_destroyable value = Destroyable(trace, "value");
            DmList assoc = Dm.list(Dm.kv(key, value));
            QDEL_LIST_ASSOC(assoc);
            Assert.Equal(new[] { "value", "key" }, trace.Select(s => s.Split(' ')[0]));
            Assert.Equal(0f, assoc.len);

            // QDEL_LIST_ASSOC_VAL: only the values.
            test_datum_destroyable key2 = Destroyable(null, "key2");
            test_datum_destroyable value2 = Destroyable(null, "value2");
            DmList vals = Dm.list(Dm.kv(key2, value2));
            QDEL_LIST_ASSOC_VAL(vals);
            Assert.True(QDELETED(value2));
            Assert.False(QDELETED(key2));
            Assert.Equal(0f, vals.len);

            // ______qdel_list_wrapper (__HELPERS/qdel.dm:2) is QDEL_LIST; a null list is left alone.
            test_datum_destroyable wrapped = Destroyable(null, "wrapped");
            DmList viaWrapper = Dm.list(wrapped);
            ______qdel_list_wrapper(viaWrapper);
            Assert.True(QDELETED(wrapped));
            Assert.Equal(0f, viaWrapper.len);
            QDEL_LIST(null);
            Assert.Empty(t.Runtimes);
        }
    }
}

/// <summary>A datum whose Destroy runtimes before it returns a hint.</summary>
[DmPath("/datum/test_datum_failing_destroyable")]
public class test_datum_failing_destroyable : datum
{
    public override object Destroy(float? force = null)
    {
        throw Dm.CRASH("pilot Destroy failure", "pilot", 3);
    }
}
