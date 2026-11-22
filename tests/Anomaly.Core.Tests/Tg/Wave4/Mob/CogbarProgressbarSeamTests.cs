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
/// The seam of lead decision D4 (DEVIATION DV13): datums/cogbar.dm and datums/progressbar.dm. The busy cog and the progress
/// bar are drawn by the 3D client from replicated state; the cogbar's New is a deferred leaf, a progress bar's New is
/// NotTranslated until a mob has a client (M1), and what the timed action needs of them (remove, Destroy, update, end_progress,
/// on_user_delete) is translated and tested here from the DM.
/// </summary>
public class CogbarProgressbarSeamTests
{
    private static T NewAt<T>(int x = 3, int y = 3) where T : atom => Dm.@new<T>(MobRoom.T(x, y));

    // ---- cogbar ----

    [Fact]
    public void ACogbarNewIsADeferredLeafThatLeavesEveryFieldNull()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            int before = DmWorldState.Of(room.World).DeferredCount;
            var cog = Dm.@new<datum_cogbar>(m, Dm.resource("icons/effects/progressbar.dmi"), "cog");
            Assert.Equal(before + 1, DmWorldState.Of(room.World).DeferredCount);
            Assert.Contains("code/datums/cogbar.dm:24", DmWorldState.Of(room.World).DeferredSites);
            Assert.Null(cog.user);
            Assert.Null(cog.user_client);
            Assert.Null(cog.cog);
            Assert.Null(cog.blank);
            Assert.Null(cog.cogicon);
            Assert.False(QDELETED(cog)); // the deferred New does not delete it either
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void RemovingACogbarWithNoCogDeletesItAtOnce()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var cog = Dm.@new<datum_cogbar>();
            cog.remove();
            Assert.True(QDELETED(cog)); // `if(isnull(cog)) qdel(src); return` (cogbar.dm:84-86)
            Assert.Equal(0f, SStimer.timer_id_dict.len);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void RemovingACogbarWithACogAnimatesItOutAndDeletesItHalfASecondLater()
    {
        var room = new MobRoom();
        datum_cogbar cogbar = null;
        obj_effect_overlay_vis visual = null;
        room.In(() =>
        {
            cogbar = Dm.@new<datum_cogbar>();
            visual = Dm.@new<obj_effect_overlay_vis>();
            cogbar.cog = visual;
            cogbar.remove();
            Assert.False(QDELETED(cogbar)); // QDEL_IN(src, COGBAR_ANIMATION_TIME), 0.5 SECONDS
        });
        room.Tick(9);
        using (room.World.Enter())
            Assert.False(QDELETED(cogbar));
        room.Tick(1);
        using (room.World.Enter())
        {
            Assert.True(QDELETED(cogbar));
            Assert.Null(cogbar.cog); // Destroy dropped the reference (the visual is the vis overlay system's to delete)
        }
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ADeletedUserDeletesItsCogbar()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var cog = Dm.@new<datum_cogbar>();
            var m = NewAt<TgMob>();
            cog.RegisterSignal(m, COMSIG_QDELETING, nameof(datum_cogbar.on_user_delete));
            qdel(m);
            Assert.True(QDELETED(cog));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void CogbarDestroyClearsItsFieldsAndAsksTheVisOverlaysToDropTheCog()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var cog = Dm.@new<datum_cogbar>();
            cog.user = m;
            cog.cog = Dm.@new<obj_effect_overlay_vis>();
            qdel(cog);
            Assert.True(QDELETED(cog));
            Assert.Null(cog.user);
            Assert.Null(cog.cog);
            // SSvis_overlays.remove_vis_overlay (translated, M7 slice T3) took nothing from a user with no managed overlays
            Assert.DoesNotContain("code/controllers/subsystem/vis_overlays.dm:67", DmWorldState.Of(room.World).DeferredSites);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ACogbarIsDeletedWhenItsActionEndsEvenWhileTheUserKeepsWorking()
    {
        // the whole life of a cogbar in a do_after: made in New, removed by await, gone with no timer left behind
        var room = new MobRoom();
        DmTask<object> task = default;
        datum_cogbar cogbar = null;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            task = do_after(m, 1 * SECONDS);
            cogbar = ((datum_timed_action)SStimed_actions.processing[1]).cogbar;
        });
        room.Tick(20);
        Assert.True(task.IsCompleted);
        using (room.World.Enter())
        {
            Assert.True(QDELETED(cogbar));
            Assert.Equal(0f, SStimer.timer_id_dict.len);
        }
    }

    // ---- progress bar ----

    private static datum_progressbar_test_mob_bare NewBar(float goal, TgMob user = null, atom where = null)
    {
        var bar = Dm.@new<datum_progressbar_test_mob_bare>();
        bar.goal = goal;
        bar.bar = image(Dm.resource("icons/effects/progressbar.dmi"), where, "prog_bar_0");
        bar.user = user;
        bar.bar_loc = where;
        return bar;
    }

    [Fact]
    public void AProgressBarsNewIsTgsAndShowsTheBarOnlyToAClient()
    {
        // datums/progressbar.dm:24 (translated by the M2 integration; it was the NotTranslated seam of DV13)
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var bar = Dm.@new<datum_progressbar>(m, 10, m);
            Assert.Equal("prog_bar_0", bar.bar.icon_state);
            Assert.Same(m, bar.bar.loc);
            Assert.Equal(1f, bar.listindex);
            Assert.Null(bar.user_client);
            var watcher = NewAt<TgMob>(5, 5);
            watcher.client = Dm.@new<client_test_m1_client>();
            var shown = Dm.@new<datum_progressbar>(watcher, 10, m);
            Assert.Same(watcher.client, shown.user_client);
            Assert.True(Dm.@in(shown.bar, watcher.client.images));
            watcher.client = null;
        });
    }

    [Fact]
    public void AProgressBarKeepsTgsFieldDefaults()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var bar = Dm.@new<datum_progressbar_test_mob_bare>();
            Assert.Equal(1f, bar.goal);
            Assert.Equal(0f, bar.last_progress);
            Assert.Equal(0f, bar.listindex);
            Assert.Null(bar.bar);
            Assert.Null(bar.user);
        });
    }

    [Theory]
    [InlineData(10f, 3f, "prog_bar_30")]
    [InlineData(10f, 4.9f, "prog_bar_50")] // round(49, 5)
    [InlineData(10f, 10f, "prog_bar_100")]
    [InlineData(10f, 100f, "prog_bar_100")] // clamp(progress, 0, goal)
    [InlineData(3f, 1f, "prog_bar_35")] // 33.33 rounds to the nearest 5
    [InlineData(3f, 2f, "prog_bar_65")]
    [InlineData(20f, 0.5f, "prog_bar_5")]
    public void UpdateSetsTheIconStateToTheClampedProgressInSteps(float goal, float progress, string expected)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var bar = NewBar(goal);
            bar.update(progress);
            Assert.Equal(expected, bar.bar.icon_state);
            Assert.Equal(Math.Min(progress, goal), bar.last_progress);
        });
    }

    [Fact]
    public void UpdateDoesNothingWhenTheClampedProgressIsTheSameAsBefore()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var bar = NewBar(10);
            bar.update(0); // equal to the starting last_progress: not even the first icon state is set
            Assert.Equal("prog_bar_0", bar.bar.icon_state);
            bar.bar.icon_state = "sentinel";
            bar.update(0);
            Assert.Equal("sentinel", bar.bar.icon_state);
            bar.update(5);
            Assert.Equal("prog_bar_50", bar.bar.icon_state);
            bar.bar.icon_state = "sentinel";
            bar.update(5);
            Assert.Equal("sentinel", bar.bar.icon_state);
            bar.update(50); // clamps to 10: a change
            Assert.Equal("prog_bar_100", bar.bar.icon_state);
            bar.bar.icon_state = "sentinel";
            bar.update(99); // clamps to 10 again: no change
            Assert.Equal("sentinel", bar.bar.icon_state);
        });
    }

    [Fact]
    public void UpdateClampsANegativeProgressToZero()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var bar = NewBar(10);
            bar.update(5);
            bar.update(-3);
            Assert.Equal("prog_bar_0", bar.bar.icon_state);
            Assert.Equal(0f, bar.last_progress);
        });
    }

    [Fact]
    public void EndProgressMarksAnUnfinishedBarFailedAndSchedulesItsDeletion()
    {
        var room = new MobRoom();
        datum_progressbar_test_mob_bare bar = null;
        room.In(() =>
        {
            bar = NewBar(10);
            bar.update(4);
            bar.end_progress();
            Assert.Equal("prog_bar_40_fail", bar.bar.icon_state); // last_progress != goal (:142-143)
            Assert.False(QDELETED(bar));
        });
        room.Tick(9);
        using (room.World.Enter())
            Assert.False(QDELETED(bar));
        room.Tick(1); // QDEL_IN(src, PROGRESSBAR_ANIMATION_TIME): 5 ds
        using (room.World.Enter())
            Assert.True(QDELETED(bar));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void EndProgressOfAFullBarKeepsItsIconState()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var bar = NewBar(10);
            bar.update(10);
            bar.end_progress();
            Assert.Equal("prog_bar_100", bar.bar.icon_state);
        });
    }

    [Fact]
    public void ProgressBarDestroyTakesItOutOfItsUsersListAndClearsItsReferences()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var target = NewAt<obj>();
            var bar = NewBar(10, m, target);
            m.progressbars = Dm.list(Dm.kv(target, Dm.list(bar)));
            qdel(bar);
            Assert.True(QDELETED(bar));
            Assert.Null(m.progressbars); // LAZYREMOVEASSOC emptied it
            Assert.Null(bar.user);
            Assert.Null(bar.bar_loc);
            Assert.Null(bar.bar);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ProgressBarDestroyKeepsTheOtherBarsOfTheSameTarget()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var target = NewAt<obj>();
            var first = NewBar(10, m, target);
            var second = NewBar(10, m, target);
            first.listindex = 1;
            second.listindex = 1; // the same stack position: no restacking is needed (progressbar.dm:70-71)
            m.progressbars = Dm.list(Dm.kv(target, Dm.list(first, second)));
            qdel(first);
            var left = (DmList)m.progressbars[target];
            Assert.Equal(1f, left.len);
            Assert.Same(second, left[1]);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ADeletedUserNukesItsProgressbarListAndDeletesTheBar()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var target = NewAt<obj>();
            var bar = NewBar(10, m, target);
            m.progressbars = Dm.list(Dm.kv(target, Dm.list(bar)));
            bar.on_user_delete(m);
            Assert.Null(m.progressbars); // "We can simply nuke the list" (:93)
            Assert.Null(bar.user);
            Assert.True(QDELETED(bar));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void CleanUserClientOfABarWithoutAClientDoesNothing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var bar = NewBar(10);
            bar.clean_user_client();
            Assert.Null(bar.user_client);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ADumpOfAProgressBarNamesItsOwnersTypeOnceOnly()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var bar = NewBar(10);
            bar.location_type = typeof(obj_item);
            Assert.Equal("Owner's type: /obj/item", Dm.str(bar.dump_harddel_info()));
            Assert.True(Dm.truthy(bar.harddel_deets_dumped));
            Assert.Null(bar.dump_harddel_info()); // `if(harddel_deets_dumped) return`
        });
    }
}

/// <summary>A progress bar whose New is empty: the seam's data procs run on a bar the test fills in (the real New needs a client, M1).</summary>
[DmPath("/datum/progressbar/test_mob_bare")]
public class datum_progressbar_test_mob_bare : datum_progressbar
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}
