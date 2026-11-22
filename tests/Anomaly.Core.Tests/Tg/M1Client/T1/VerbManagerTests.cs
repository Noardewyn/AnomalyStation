using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: SSverb_manager (code/controllers/subsystem/verb_manager.dm), the verb queue SSinput inherits and overrides
/// (input.dm:50-94), and <c>/datum/callback/verb_callback</c> (code/datums/verb_callbacks.dm). What a click or a verb that the
/// server delays does; M1 queues none (only <c>keyDown</c> and <c>keyUp</c> run), so these pin the translation directly.
/// </summary>
public class VerbManagerTests
{
    private static float N(object v) => Dm.num(v);

    /// <summary>Makes a verb callback as <paramref name="creator"/> would (the callback records the usr that makes it).</summary>
    private static datum_callback_verb_callback MakeCallback(datum target, string proc, object creator)
    {
        object saved = World.Current.usr;
        World.Current.usr = creator;
        try
        {
            return Dm.@new<datum_callback_verb_callback>(target, proc);
        }
        finally
        {
            World.Current.usr = saved;
        }
    }

    private static List<string> RuntimeTexts(T1Room room) => room.Room.Runtimes.Select(e => e.Message).ToList();

    // ---- queue_verb and the fire ----

    [Fact]
    public void QueueVerbAddsTheCallbackToTheQueueAndReturnsTrue()
    {
        // verb_manager.dm:136-141
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            var target = Dm.@new<datum_test_m1t1_listener>();
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            Assert.True(Dm.truthy(SSverb_manager.queue_verb(cb)));
            Assert.Equal(new object[] { cb }, Dm.each(SSverb_manager.verb_queue).ToArray());
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheNextFireRunsEveryQueuedCallbackOfAPlayerWithAClientAndCutsTheQueue()
    {
        // verb_manager.dm:143-165: fire -> run_verb_queue: InvokeAsync each, executed_verbs++, verb_queue.Cut(), the average; with
        // `wait SECONDS` (= 10) as the duration, MC_AVG_SECONDS keeps nothing of the old average (factor (10 - 10) / 10 = 0)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            for (int i = 0; i < 3; i++)
                SSverb_manager.queue_verb(MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar));
        });
        room.Step();
        Assert.Equal(new[] { "CALLBACK post_login", "CALLBACK post_login", "CALLBACK post_login" }, target.Trace);
        room.In(() =>
        {
            Assert.Equal(0f, SSverb_manager.verb_queue.len);
            Assert.Equal(3f, SSverb_manager.verbs_executed_per_second);
        });
        room.Step();
        room.In(() => Assert.Equal(0f, SSverb_manager.verbs_executed_per_second));
        Assert.Equal(3, target.Trace.Count);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACallbackMadeWithoutAPlayerAsUsrIsCountedButNeverRuns()
    {
        // verb_callbacks.dm:12-14  var/mob/our_user = user?.resolve(); if(QDELETED(our_user) || isnull(our_user.client)) return
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var idle = room.NewMob(4, 4); // a mob with no client
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            SSverb_manager.queue_verb(MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), null));
            SSverb_manager.queue_verb(MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), idle));
        });
        room.Step();
        Assert.Empty(target.Trace);
        room.In(() => Assert.Equal(2f, SSverb_manager.verbs_executed_per_second)); // run_verb_queue counts what it invoked
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACallbackWhoseCreatorLeftBeforeTheFireDoesNotRun()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() => SSverb_manager.queue_verb(MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar)));
        room.In(() => ClientSeam.Disconnect(c));
        room.Step();
        Assert.Empty(target.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACallbackInvokedDirectlyRunsForAPlayerAndKeepsTheCallersUsr()
    {
        // verb_callbacks.dm:11-26  Invoke and InvokeAsync: run `..()` and put the caller's usr back
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var other = room.NewMob(4, 4);
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            World.Current.usr = other;
            Waiting.NoSleep(cb.Invoke());
            Assert.Same(other, World.Current.usr);
            cb.InvokeAsync();
            Assert.Same(other, World.Current.usr);
            World.Current.usr = null;
        });
        Assert.Equal(2, target.Trace.Count);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACallbackInvokedDirectlyDoesNotRunForACreatorWithoutAClientOrACreatorThatIsGone()
    {
        // verb_callbacks.dm:12-14 (Invoke) and :20-22 (InvokeAsync): `if(QDELETED(our_user) || isnull(our_user.client)) return`
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var idle = room.NewMob(4, 4);
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            var noClient = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), idle);
            Waiting.NoSleep(noClient.Invoke());
            noClient.InvokeAsync();
            var noUsr = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), null);
            Waiting.NoSleep(noUsr.Invoke());
            noUsr.InvokeAsync();
            var gone = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), idle);
            qdel(idle);
            Waiting.NoSleep(gone.Invoke());
            gone.InvokeAsync();
            // with a usr set, /datum/callback/InvokeAsync does not go through push_usr (callback.dm:131-139), so the verb
            // callback's own check is the only guard
            var other = Dm.@new<mob>(room.At(5, 5));
            var stranger = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), other);
            World.Current.usr = avatar;
            stranger.InvokeAsync();
            Waiting.NoSleep(stranger.Invoke());
            World.Current.usr = null;
        });
        Assert.Empty(target.Trace);
        room.In(() =>
        {
            var ok = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            Waiting.NoSleep(ok.Invoke());
            ok.InvokeAsync();
        });
        Assert.Equal(2, target.Trace.Count);
    }

    [Fact]
    public void ACallbackRecordsTheTickOfItsCreation()
    {
        // verb_callbacks.dm:7-9  creation_time = DS2TICKS(world.time)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            Assert.Equal(world.time / world.tick_lag, cb.creation_time);
        });
    }

    [Fact]
    public void ANonCallbackInTheQueueIsAStackTraceAndTheOthersStillRun()
    {
        // verb_manager.dm:153-156  if(!istype(verb_callback)) { stack_trace(...); continue }
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            SSverb_manager.verb_queue.Add("not a callback");
            SSverb_manager.queue_verb(MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar));
        });
        room.Step();
        Assert.Equal(new[] { "CALLBACK post_login" }, target.Trace);
        Assert.Single(room.Room.Runtimes);
        Assert.Contains("non /datum/callback/verb_callback inside Verb Manager's verb_queue!", RuntimeTexts(room)[0], StringComparison.Ordinal);
        room.In(() => Assert.Equal(0f, SSverb_manager.verb_queue.len));
    }

    // ---- _queue_verb ----

    [Fact]
    public void QueueVerbRefusesANullCallbackWithAStackTrace()
    {
        // verb_manager.dm:59-70
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            World.Current.usr = avatar;
            Assert.False(Dm.truthy(_queue_verb(null, 0)));
            World.Current.usr = null;
        });
        Assert.Single(room.Room.Runtimes);
        Assert.Contains("_queue_verb() returned false because it was given a deleted callback! callback is null.", RuntimeTexts(room)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void QueueVerbRefusesACallbackThatActsOnADeletedObject()
    {
        // :72-83
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            var target = Dm.@new<datum_test_m1t1_listener>();
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            qdel(target);
            World.Current.usr = avatar;
            Assert.False(Dm.truthy(_queue_verb(cb, 0)));
            World.Current.usr = null;
        });
        Assert.Single(room.Room.Runtimes);
        Assert.Contains("_queue_verb() returned false because it was given a callback acting on a qdeleted object!", RuntimeTexts(room)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void QueueVerbRefusesACallerThatIsNotAPlayersInput()
    {
        // :90-93 (the build without UNIT_TESTS)  if(QDELETED(usr) || isnull(usr.client)) { stack_trace(...); return FALSE }
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var idle = room.NewMob(4, 4);
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            Assert.False(Dm.truthy(_queue_verb(cb, 0))); // no usr
            World.Current.usr = idle;
            Assert.False(Dm.truthy(_queue_verb(cb, 0))); // usr without a client
            World.Current.usr = null;
            Assert.Equal(0f, SSverb_manager.verb_queue.len);
        });
        Assert.Equal(2, room.Room.Runtimes.Count);
        Assert.All(RuntimeTexts(room), t => Assert.Contains("_queue_verb() returned false because it wasn't called from player input!", t, StringComparison.Ordinal));
    }

    [Fact]
    public void QueueVerbQueuesForAPlayerWhenTheTickCheckIsBelowTheTickUsage()
    {
        // :99-100  if((TICK_USAGE < tick_check) && !always_queue) return FALSE. Tick usage is virtual and 0 (DV3), so only a check of
        // 0 queues: a real threshold such as VERB_DEFAULT_QUEUE_THRESHOLD (85) always means "not busy, run it now"
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            World.Current.usr = avatar;
            Assert.False(Dm.truthy(_queue_verb(cb, VERB_DEFAULT_QUEUE_THRESHOLD)));
            Assert.Equal(0f, SSverb_manager.verb_queue.len);
            Assert.True(Dm.truthy(_queue_verb(cb, 0)));
            Assert.Equal(1f, SSverb_manager.verb_queue.len);
            World.Current.usr = null;
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnAlwaysQueueSubsystemQueuesWhateverTheThreshold()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            SSverb_manager.always_queue = TRUE;
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            World.Current.usr = avatar;
            Assert.True(Dm.truthy(_queue_verb(cb, VERB_OVERTIME_QUEUE_THRESHOLD)));
            Assert.Equal(1f, SSverb_manager.verb_queue.len);
            World.Current.usr = null;
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void CanQueueVerbSaysNoForAdminsWhenTheSubsystemQueuesEverythingByForceAndOutsideItsRunlevels()
    {
        // verb_manager.dm:118-131
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            World.Current.usr = avatar;
            Assert.True(Dm.truthy(SSverb_manager.can_queue_verb(cb)));
            SSverb_manager.FOR_ADMINS_IF_VERBS_FUCKED_immediately_execute_all_verbs = TRUE;
            Assert.False(Dm.truthy(SSverb_manager.can_queue_verb(cb)));
            SSverb_manager.always_queue = TRUE;
            Assert.False(Dm.truthy(SSverb_manager.can_queue_verb(cb))); // always_queue does not override the emergency switch (:119)
            SSverb_manager.FOR_ADMINS_IF_VERBS_FUCKED_immediately_execute_all_verbs = FALSE;
            Assert.True(Dm.truthy(SSverb_manager.can_queue_verb(cb)));
            SSverb_manager.always_queue = FALSE;
            object level = Master.current_runlevel;
            Master.current_runlevel = 0;
            Assert.False(Dm.truthy(SSverb_manager.can_queue_verb(cb))); // !(runlevels & Master.current_runlevel)
            Master.current_runlevel = level;
            World.Current.usr = null;
        });
        room.AssertNoRuntimes();
    }

    // ---- SSinput's own queue ----

    [Fact]
    public void SSinputOnlyQueuesWhatComesFromTheMapWindow()
    {
        // input.dm:50-62  if(control != SKIN_MAPWINDOW_MAP) return FALSE
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            World.Current.usr = avatar;
            Assert.False(Dm.truthy(SSinput.can_queue_verb(cb, "somewhere.else")));
            Assert.Equal(0f, SSinput.current_clicks); // refused before it counts anything
            Assert.True(Dm.truthy(SSinput.can_queue_verb(cb, SKIN_MAPWINDOW_MAP)));
            Assert.Equal(0f, SSinput.current_clicks);
            World.Current.usr = null;
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ASlowClickAverageMakesSSinputRefuseAndCountTheClick()
    {
        // input.dm:56-60  if(average_click_delay > MAXIMUM_CLICK_LATENCY || !..()) { current_clicks++; average_click_delay =
        // MC_AVG_FAST_UP_SLOW_DOWN(average_click_delay, 0); return FALSE }; MC.dm:17: average > current -> MC_AVERAGE_SLOW: 0.9 * 2
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            var cb = MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar);
            World.Current.usr = avatar;
            SSinput.average_click_delay = 2; // above MAXIMUM_CLICK_LATENCY (0.5)
            Assert.False(Dm.truthy(SSinput.can_queue_verb(cb, SKIN_MAPWINDOW_MAP)));
            Assert.Equal(1f, SSinput.current_clicks);
            Assert.Equal(1.8f, SSinput.average_click_delay, 4);
            World.Current.usr = null;
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SSinputRunsItsQueuedClicksAndCountsThemInTheAverages()
    {
        // input.dm:76-94: for each callback: average_click_delay = MC_AVG_FAST_UP_SLOW_DOWN(avg, TICKS2DS(DS2TICKS(world.time) -
        // creation_time)), InvokeAsync, current_clicks++, deferred_clicks_this_run++; Cut; clicks_per_second = MC_AVG_SECONDS(.., wait
        // SECONDS) which is the plain count (wait SECONDS = 10 = the whole second)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var target = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        room.In(() =>
        {
            SSinput.verb_queue.Add(MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar));
            SSinput.verb_queue.Add(MakeCallback(target, nameof(datum_test_m1t1_listener.ping_login), avatar));
        });
        room.Step();
        Assert.Equal(2, target.Trace.Count);
        room.In(() =>
        {
            Assert.Equal(0f, SSinput.verb_queue.len);
            Assert.Equal(2f, SSinput.clicks_per_second);
            Assert.Equal(2f, SSinput.delayed_clicks_per_second);
            Assert.Equal(0f, SSinput.current_clicks);
            // each click waited one tick (0.5 ds): average 0 -> 0.7 * 0 + 0.3 * 0.5 = 0.15, then 0.15 > 0.5 is false: 0.7 * 0.15 + 0.3 * 0.5
            Assert.Equal(0.255f, SSinput.average_click_delay, 4);
        });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(0f, SSinput.clicks_per_second);
            Assert.Equal(0f, SSinput.delayed_clicks_per_second);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ANonCallbackInSSinputsQueueIsAStackTraceToo()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => SSinput.verb_queue.Add("not a callback"));
        room.Step();
        Assert.Single(room.Room.Runtimes);
        Assert.Contains("non /datum/callback/verb_callback instance inside SSinput's verb_queue!", RuntimeTexts(room)[0], StringComparison.Ordinal);
        room.In(() =>
        {
            Assert.Equal(0f, SSinput.verb_queue.len);
            Assert.Equal(0f, SSinput.clicks_per_second);
        });
    }

    [Fact]
    public void SSinputAndSSverbManagerKeepTheirOwnQueues()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => Assert.NotSame(SSinput.verb_queue, SSverb_manager.verb_queue));
    }
}
