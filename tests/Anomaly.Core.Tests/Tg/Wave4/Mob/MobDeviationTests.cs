using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>The deviations of docs/ai/DEVIATIONS.md that a mob, an obj or a timed action meets (DV1, DV2, DV12, DV13).</summary>
public class MobDeviationTests
{
    private static T NewAt<T>(int x = 3, int y = 3) where T : atom => Dm.@new<T>(MobRoom.T(x, y));

    private static object Result(DmTask<object> task)
    {
        Assert.True(task.IsCompleted, "the do_after has not finished");
        Assert.True(task.TryGetResult(out object value));
        return value;
    }

    [Fact]
    public void Dv2_AMobOrObjDeletedAtWorldTimeZeroCountsAsDeleted()
    {
        // in tg gc_destroyed becomes world.time = 0 there, which reads as alive; QDELETED tests `!= null` here
        var room = new MobRoom();
        room.In(() =>
        {
            Assert.Equal(0f, world.time);
            var m = NewAt<TgMob>();
            var o = NewAt<obj>();
            Assert.False(QDELETED(m));
            qdel(m);
            qdel(o);
            Assert.True(QDELETED(m));
            Assert.True(QDELETED(o));
            Assert.NotNull(m.gc_destroyed);
            qdel(m); // a second qdel of a deleted datum is quiet
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void Dv12_AMobAndAnObjAreCreatedWithoutTheEmissiveBlockerOverlay()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var o = NewAt<obj>();
            Assert.Contains("code/game/atoms_movable.dm:183", DmWorldState.Of(room.World).DeferredSites);
            Assert.Equal(0f, m.overlays.len);
            Assert.Equal(0f, o.overlays.len);
            Assert.Null(m.managed_overlays);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void Dv13_ADoAfterOfOneSecondOrMoreDefersTheCogbarOncePerAction()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            int before = DmWorldState.Of(room.World).DeferredCount;
#pragma warning disable ANOM0001 // the test watches the cogbars of do_afters it never awaits
            do_after(m, 1 * SECONDS, NewAt<obj>());
            do_after(m, 3 * SECONDS, NewAt<obj>());
#pragma warning restore ANOM0001
            Assert.Equal(before + 2, DmWorldState.Of(room.World).DeferredCount);
            Assert.Equal(1, DmWorldState.Of(room.World).DeferredSites.Count(s => s == "code/datums/cogbar.dm:24"));
        });
    }

    [Fact]
    public void Dv1_ARuntimeInAListenerOfTheBeganSignalIsReportedAndTheDoAfterCarriesOn()
    {
        // a signal handler call is an isolation boundary (translation-rules.md 9.2): the runtime does not unwind into do_after
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var listener = Dm.@new<datum_test_mob_throwing_listener>();
            listener.RegisterSignal(m, COMSIG_DO_AFTER_BEGAN, nameof(datum_test_mob_throwing_listener.on_began));
            task = do_after(m, 5);
            Assert.False(task.IsCompleted);
        });
        room.Tick(10);
        Assert.True(task.TryGetResult(out object result));
        Assert.True(Dm.eq(result, TRUE));
        Assert.Contains("listener boom", Assert.Single(room.Runtimes).Message);
    }

    [Fact]
    public void Dv5_AnAdjacentCheckThatSleepsInsideTheMovedHandlerIsReportedAndFailsTheCheck()
    {
        // on_user_moved is a SIGNAL_HANDLER and calls target.Adjacent(user) through Waiting.NoSleep (timed_actions.cs); an override
        // that sleeps there is the DV5 case: the caller goes on with a default (null: not adjacent -> cancel) and a runtime is reported.
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var sleepy = NewAt<obj_test_mob_sleepy_adjacent>();
            m.drift_handler = Dm.@new<Anomaly.Core.Tests.Tg.Wave4.Movable.datum_drift_handler_test_movable>();
            task = do_after(m, 1 * SECONDS, sleepy);
            Waiting.NoSleep(step(m, EAST));
            Assert.Equal(0f, SStimed_actions.processing.len); // cancelled with the default answer
            m.drift_handler = null;
        });
        room.Tick(1);
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Contains(room.Runtimes, e => e.Message.Contains("slept inside a no-sleep context"));
    }

    [Fact]
    public void Dv1_ARuntimeInACallbackOfAnEndedSignalDoesNotChangeTheResult()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var listener = Dm.@new<datum_test_mob_throwing_listener>();
            listener.RegisterSignal(m, COMSIG_DO_AFTER_ENDED, nameof(datum_test_mob_throwing_listener.on_began));
            task = do_after(m, 5, NewAt<obj>());
        });
        room.Tick(10);
        Assert.True(task.TryGetResult(out object result));
        Assert.True(Dm.eq(result, TRUE));
        using (room.World.Enter())
            Assert.Null(((TgMob)Dm.each<TgMob>(MobRoom.T(3, 3).contents).First()).do_afters); // the count came down before ENDED
        Assert.Single(room.Runtimes);
    }
}

/// <summary>An obj whose Adjacent sleeps before it answers.</summary>
[DmPath("/obj/test_mob_sleepy_adjacent")]
public class obj_test_mob_sleepy_adjacent : obj
{
    public override async DmTask<object> Adjacent(atom neighbor = null, atom target = null, atom_movable mover = null, params object[] __extra)
    {
        await sleep(1);
        return TRUE;
    }
}

/// <summary>A signal listener whose handler runtimes.</summary>
[DmPath("/datum/test_mob_throwing_listener")]
public class datum_test_mob_throwing_listener : datum
{
    [SignalHandler]
    public virtual void on_began(datum source = null)
    {
        throw Dm.CRASH("listener boom", "tests/Mob", 1);
    }
}
