using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>
/// Smoke tests of the timed actions (datums/timed_actions.dm) and their seams (cogbar.dm, progressbar.dm; lead decision
/// D4, DV13) in a world whose Master does not run: the await loop sleeps a tick at a time and makes the last poll
/// itself (timed_actions.dm:110). The scenarios with the Master polling every tick and a step cancelling the action are
/// the independent test-writer's (scope.md section 3 C).
/// </summary>
public class TimedActionSmokeTests
{
    private static object Result(DmTask<object> task)
    {
        Assert.True(task.IsCompleted, "the do_after has not finished");
        Assert.True(task.TryGetResult(out object value));
        return value;
    }

    [Fact]
    public void ANullUserFailsANonPositiveDelaySucceedsAndANonNumberDelayIsARuntime()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var o = Dm.@new<obj>(locate(1, 1, 1));
            Assert.True(Dm.eq(Result(do_after(null, 10)), FALSE));
            Assert.True(Dm.eq(Result(do_after(o, 0)), TRUE));
            Assert.True(Dm.eq(Result(do_after(o, -5)), TRUE));
            DmTask<object> bad = do_after(o, "ten");
            Assert.True(bad.IsFaulted);
            Assert.Contains("do_after was passed a non-number delay: ten.", bad.Exception.Message);
            DmTask<object> swapped = do_after(o, o);
            Assert.True(swapped.IsFaulted);
        }
    }

    [Fact]
    public void AnObjUsersDoAfterEndsAtItsEndTimeWithOnePollByTheAwaitItself()
    {
        var t = new MobTestWorld();
        DmTask<object> task;
        datum_test_mob_check check;
        using (t.World.Enter())
        {
            var o = Dm.@new<obj>(locate(1, 1, 1));
            check = Dm.@new<datum_test_mob_check>();
            datum_callback cb = Dm.@new<datum_callback>(check, nameof(datum_test_mob_check.check));
            task = do_after(o, 5, extra_checks: cb); // 5 ds: no cogbar (timed_actions.dm:40)
            Assert.False(task.IsCompleted);
            Assert.Equal(1f, SStimed_actions.processing.len);
        }
        for (int i = 0; i < 9; i++)
            t.World.Step();
        Assert.False(task.IsCompleted);
        t.World.Step();
        Assert.True(Dm.eq(Result(task), TRUE));
        using (t.World.Enter())
        {
            Assert.Equal(1f, check.polls);
            Assert.Equal(0f, SStimed_actions.processing.len);
        }
        Assert.Empty(t.Runtimes.Select(e => e.ToString()));
    }

    [Fact]
    public void AFailedExtraCheckCancels()
    {
        var t = new MobTestWorld();
        DmTask<object> task;
        using (t.World.Enter())
        {
            var o = Dm.@new<obj>(locate(1, 1, 1));
            var check = Dm.@new<datum_test_mob_check>();
            check.answer = FALSE;
            task = do_after(o, 2, extra_checks: Dm.@new<datum_callback>(check, nameof(datum_test_mob_check.check)));
        }
        for (int i = 0; i < 4; i++)
            t.World.Step();
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Empty(t.Runtimes.Select(e => e.ToString()));
    }

    [Fact]
    public void AMovedSignalFromAnotherLocCancelsButOneFromTheSameLocDoesNot()
    {
        var t = new MobTestWorld();
        DmTask<object> task;
        obj o;
        turf elsewhere;
        using (t.World.Enter())
        {
            o = Dm.@new<obj>(locate(1, 1, 1));
            elsewhere = locate(2, 1, 1);
            task = do_after(o, 5);
        }
        t.World.Step();
        using (t.World.Enter())
        {
            // forceMove to the same loc: Moved fires with old_loc == loc (timed_actions.dm:172)
            o._SendSignal(COMSIG_MOVABLE_MOVED, Dm.list(o, o.loc, NONE, TRUE, null, TRUE));
        }
        t.World.Step();
        Assert.False(task.IsCompleted);
        using (t.World.Enter())
        {
            // a real move: the action is cancelled at once, the awaiting proc notices at its next wake (latency 1 tick)
            o._SendSignal(COMSIG_MOVABLE_MOVED, Dm.list(o, elsewhere, EAST, FALSE, null, TRUE));
            Assert.Equal(0f, SStimed_actions.processing.len);
        }
        Assert.False(task.IsCompleted);
        t.World.Step();
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Empty(t.Runtimes.Select(e => e.ToString()));
    }

    [Fact]
    public void AMobsInteractionKeyCapsConcurrentDoAftersAndIsClearedAfterwards()
    {
        var t = new MobTestWorld();
        DmTask<object> first;
        TgMob m;
        obj target;
        datum_test_mob_listener listener;
        using (t.World.Enter())
        {
            m = Dm.@new<TgMob>(locate(1, 1, 1));
            m.cached_multiplicative_actions_slowdown = 1; // what initialize_actionspeed sets (package D)
            target = Dm.@new<obj>(locate(1, 1, 1));
            listener = Dm.@new<datum_test_mob_listener>();
            listener.RegisterSignal(m, COMSIG_DO_AFTER_BEGAN, nameof(datum_test_mob_listener.on_began));
            listener.RegisterSignal(m, COMSIG_DO_AFTER_ENDED, nameof(datum_test_mob_listener.on_ended));

            first = do_after(m, 5, target);
            Assert.True(Dm.eq(m.do_afters[target], 1));
            Assert.True(Dm.truthy(DOING_INTERACTION_WITH_TARGET(m, target)));
            Assert.True(DOING_INTERACTION_LIMIT(m, target, 1));

            DmTask<object> capped = do_after(m, 5, target); // max_interact_count = 1: a bare return (timed_actions.dm:236)
            Assert.True(capped.IsCompleted);
            Assert.Null(Result(capped));
            Assert.Equal(new[] { "BEGAN" }, listener.heard);
        }
        for (int i = 0; i < 10; i++)
            t.World.Step();
        Assert.True(Dm.eq(Result(first), TRUE));
        Assert.Null(m.do_afters);
        Assert.False(DOING_INTERACTION_LIMIT(m, target, 1));
        Assert.Equal(new[] { "BEGAN", "ENDED" }, listener.heard);
        Assert.Empty(t.OwnRuntimes.Select(e => e.ToString()));
    }

    [Fact]
    public void AMobWhoseActionspeedWasNeverInitializedSucceedsAtOnceAndItsCogbarDeletesItself()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var m = Dm.@new<TgMob>(locate(1, 1, 1));
            m.cached_multiplicative_actions_slowdown = null; // delay *= null is 0 (timed_actions.dm:50)
            DmTask<object> task = do_after(m, 1 * SECONDS);
            Assert.True(Dm.eq(Result(task), TRUE));
            Assert.Null(m.do_afters);
            // 1 second or more shows a cog (timed_actions.dm:40): its New is the DV13 seam, remove() deletes it
            Assert.Contains("code/datums/cogbar.dm:24", DmWorldState.Of(t.World).DeferredSites);
        }
        Assert.Empty(t.OwnRuntimes.Select(e => e.ToString()));
    }

    [Fact]
    public void TheCogbarSeamKeepsNoUserAndRemoveDeletesIt()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var m = Dm.@new<TgMob>(locate(1, 1, 1));
            var cog = Dm.@new<datum_cogbar>(m, Dm.resource("icons/effects/progressbar.dmi"), "cog");
            Assert.Null(cog.user);
            Assert.Null(cog.cog);
            cog.remove();
            Assert.True(QDELETED(cog));
        }
        Assert.Empty(t.OwnRuntimes.Select(e => e.ToString()));
    }

    [Fact]
    public void AProgressBarIsTranslatedSinceTheM2Integration()
    {
        // datums/progressbar.dm:24 (was the NotTranslated seam of DV13 until the M2 integration translated it)
        var t = new MobTestWorld(notTranslatedThrows: true);
        using (t.World.Enter())
        {
            var m = Dm.@new<mob_test_mob_incap>(locate(1, 1, 1));
            var bar = Dm.@new<datum_progressbar>(m, 10, m);
            Assert.Same(m, bar.user);
            Assert.Same(m, bar.bar_loc);
            Assert.Equal(10f, bar.goal);
            Assert.Null(bar.user_client); // no client: the image goes nowhere
        }
        Assert.Empty(t.OwnRuntimes.Select(e => e.ToString()));
    }
}
