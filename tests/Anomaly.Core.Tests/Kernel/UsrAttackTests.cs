using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>
/// DM <c>usr</c> is an implicit argument of every proc call: "Each procedure inherits the value from its caller"
/// (BYOND reference, usr var (proc)). A callee that assigns it never changes its caller's usr, also when the
/// callee sleeps and the caller continues (detached, NoSleep, waitfor = FALSE) and when the caller resumes after
/// awaiting it. tg relies on this: world.push_usr (__HELPERS/mobs.dm:709) restores usr only after the invoked
/// callback returns, and SSverbs.invoke_verb (subsystem/verbs.dm:33, :58) assigns it and never restores it.
/// </summary>
public class UsrAttackTests
{
    private const string Player = "player", Admin = "admin", Other = "other";

    // tg world.push_usr: var/temp = usr; usr = user_mob; . = invoked_callback.Invoke(); usr = temp
    private static async DmTask<string> PushUsr(string user, Func<DmTask<string>> invoke)
    {
        var w = World.Current;
        var temp = w.usr;
        w.usr = user;
        string dot = await invoke();
        w.usr = temp;
        return dot;
    }

    private static async DmTask<string> SleepThenReportUsr()
    {
        await Sleep(0.5f);
        return (string)World.Current.usr;
    }

    [Fact]
    public void ACalleeThatAssignsUsrAndSleepsLeavesTheNoSleepCallersUsrAlone()
    {
        var w = KernelTestWorld.New(out var errors);
        object after = "unset";
        string calleeSaw = null;
        w.AddPhase("signal", x =>
        {
            if (x.Tick != 1) return;
            x.usr = Player;
            Waiting.NoSleep(PushUsr(Admin, async () => calleeSaw = await SleepThenReportUsr()));
            after = x.usr;
        });
        w.Step(3);
        Assert.Equal(Player, after);
        Assert.Equal(Admin, calleeSaw); // the callee keeps its own usr across its sleep
        Assert.Single(errors);          // the NoSleep report
    }

    [Fact]
    public void ACalleeThatAssignsUsrAndSleepsLeavesTheDetachingCallersUsrAlone()
    {
        var w = KernelTestWorld.New(out _);
        object after = "unset";
        using (w.Enter())
        {
            w.usr = Player;
            Waiting.Detach(PushUsr(Admin, SleepThenReportUsr));
            after = w.usr;
            w.usr = null;
        }
        Assert.Equal(Player, after);
    }

    // tg datum/callback/InvokeAsync (callback.dm:129): set waitfor = FALSE; if (!usr) push the stored user.
    private sealed class callback
    {
        public string user;
        public Func<DmTask<string>> target;

        public string InvokeAsync()
        {
            var dot = new DotBox<string>();
            return Waiting.WaitforFalse(InvokeAsync__body(dot), dot);
        }

        private async DmTask<string> InvokeAsync__body(DotBox<string> dot)
        {
            if (World.Current.usr == null && user != null)
                return dot.Value = await PushUsr(user, target);
            return dot.Value = await target();
        }
    }

    [Fact]
    public void TimerCallbacksEachRunWithTheirOwnUserWhenAnEarlierOneSlept()
    {
        // SStimer.fire (timer.dm:125) calls InvokeAsync on each due timer with usr = null. The first target sleeps
        // inside push_usr; the second callback must still see usr == null and push its own user.
        var w = KernelTestWorld.New(out var errors);
        var seen = new List<string>();
        var first = new callback { user = Player, target = async () => { seen.Add("first:" + World.Current.usr); return await SleepThenReportUsr(); } };
        var second = new callback { user = Other, target = async () => { seen.Add("second:" + World.Current.usr); return "x"; } };
        object usrAfterFire = "unset";
        w.AddPhase("timers", x =>
        {
            if (x.Tick != 1) return;
            first.InvokeAsync();
            second.InvokeAsync();
            usrAfterFire = x.usr;
        });
        w.Step(3);
        Assert.Equal(new[] { "first:player", "second:other" }, seen);
        Assert.Null(usrAfterFire);
        Assert.Empty(errors);
    }

    // tg SSverbs.invoke_verb (verbs.dm:58): `if(invoker) usr = invoker` and no restore.
    private static async DmTask InvokeVerb(string invoker, Func<DmTask> body)
    {
        if (invoker != null) World.Current.usr = invoker;
        await body();
    }

    [Fact]
    public void AnAwaitingCallerKeepsItsUsrWhenTheCalleeAssignsItAndReturnsAtOnce()
    {
        var w = KernelTestWorld.New(out _);
        object after = "unset";
        using (w.Enter())
        {
            w.usr = Player;
            Waiting.Detach(Caller());
            w.usr = null;
        }
        Assert.Equal(Player, after);

        async DmTask Caller()
        {
            await InvokeVerb(Admin, () => DmTask.CompletedTask);
            after = World.Current.usr;
        }
    }

    [Fact]
    public void AnAwaitingCallerKeepsItsUsrWhenTheCalleeAssignsItAfterASleep()
    {
        var w = KernelTestWorld.New(out _);
        var seen = new List<object>();
        using (w.Enter())
        {
            w.usr = Player;
            Waiting.Detach(Caller());
            w.usr = null;
        }
        w.Step(3);
        Assert.Equal(new object[] { Admin, Player }, seen);

        async DmTask Caller()
        {
            await Callee();
            seen.Add(World.Current.usr);
        }

        async DmTask Callee()
        {
            await Sleep(0.5f);
            World.Current.usr = Admin;
            seen.Add(World.Current.usr);
        }
    }

    [Fact]
    public void ADeepChainResumedThroughTheTrampolineGivesEachLevelItsOwnUsr()
    {
        // Each level assigns its own usr before awaiting the next; 200 levels exceed the inline depth.
        var w = KernelTestWorld.New(out var errors);
        var wrong = new List<int>();
        using (w.Enter()) Waiting.Detach(Level(200));
        w.AddPhase("noise", x => x.usr = Other);
        w.Step(2);
        Assert.Empty(wrong);
        Assert.Empty(errors);

        async DmTask Level(int n)
        {
            World.Current.usr = "level" + n;
            if (n == 0) await Sleep(0.5f);
            else await Level(n - 1);
            if (!Equals(World.Current.usr, "level" + n)) wrong.Add(n);
        }
    }

    [Fact]
    public void SpawnMinusOneAndDetachSyncKeepTheCallersUsr()
    {
        var w = KernelTestWorld.New(out var errors);
        var after = new List<object>();
        using (w.Enter())
        {
            w.usr = Player;
            Waiting.Spawn(-1, () => { World.Current.usr = Admin; return DmTask.CompletedTask; });
            after.Add(w.usr);
            Waiting.Spawn(-1, async () => { World.Current.usr = Admin; await Sleep(0.5f); });
            after.Add(w.usr);
            Waiting.DetachSync(() => World.Current.usr = Admin);
            after.Add(w.usr);
            w.usr = null;
        }
        Assert.Equal(new object[] { Player, Player, Player }, after);
        Assert.Empty(errors);
    }

    [Fact]
    public void UsrIsRestoredWhenAProcFails()
    {
        var w = KernelTestWorld.New(out var errors);
        object after = "unset";
        using (w.Enter())
        {
            w.usr = Player;
            Waiting.Detach(Fails());
            after = w.usr;
            w.usr = null;
        }
        Assert.Equal(Player, after);
        Assert.Single(errors);

        static async DmTask Fails()
        {
            World.Current.usr = Admin;
            throw new InvalidOperationException("x");
        }
    }
}
