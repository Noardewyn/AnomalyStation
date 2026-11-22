using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Pilot.Datum;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// Calls by name of a <c>/world</c> proc (rules 1.5; object-model.md section 9; v1.2 lead decisions 26 and C3): tg's
/// procs on <c>/world</c> are <c>GlobalProcs</c> extension methods of <c>DmWorld</c>, and <c>Dm.Call(world, "x", ...)</c>
/// (<c>call(world, "x")(...)</c>, <c>world.x(arglist(L))</c>) binds them like any proc with <c>world</c> passed first.
/// The proc used is tg's <c>/world/proc/push_usr(mob/user_mob, datum/callback/invoked_callback, ...)</c>
/// (<c>__HELPERS/mobs.dm:709</c>).
/// </summary>
public class WorldProcCallTests
{
    private static (DatumTestWorld World, test_datum_user Pushed, datum_callback Callback, List<string> Trace, List<object> Seen) Setup()
    {
        var t = new DatumTestWorld();
        var trace = new List<string>();
        var seen = new List<object>();
        using (t.World.Enter())
        {
            test_datum_user pushed = Dm.@new<test_datum_user>();
            test_datum_listener target = Dm.@new<test_datum_listener>();
            target.label = "target";
            target.trace = trace;
            target.seen_usr = seen;
            target.returns = 4;
            datum_callback cb = Dm.@new<datum_callback>(target, nameof(test_datum_listener.record_call));
            return (t, pushed, cb, trace, seen);
        }
    }

    [Fact]
    public void ACallByNameOnWorldRunsTheWorldProcWithWorldFirst()
    {
        var (t, pushed, cb, trace, seen) = Setup();
        using (t.World.Enter())
        {
            Assert.True(Dm.eq(4, Waiting.NoSleep(Dm.Call(world, "push_usr", pushed, cb, "a"))));
            Assert.True(Dm.eq(4, Waiting.NoSleep(Dm.Call(world, nameof(GlobalProcs.push_usr), Dm.named("invoked_callback", cb), Dm.named("user_mob", pushed)))));
            // world.push_usr(arglist(list(M, src) + args)), callback.dm:101: the extras land in push_usr's ... (args.Copy(3)).
            Assert.True(Dm.eq(4, Waiting.NoSleep(Dm.Call(world, "push_usr", Dm.arglist(Dm.list(pushed, cb, "x", "y"))))));
            Assert.Null(usr);
            Assert.Equal(new object[] { pushed, pushed, pushed }, seen);
            Assert.Equal(new[] { "target called a null null", "target called null null null", "target called x y null" }, trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void HascallSeesAWorldProcAndAGlobalCallDoesNot()
    {
        var (t, pushed, cb, _, _) = Setup();
        using (t.World.Enter())
        {
            Assert.True(hascall(world, "push_usr"));
            Assert.False(hascall(world, "no_such_world_proc"));
            // A /world proc is not a global proc: call(GLOBAL_PROC, "push_usr") finds nothing.
            DmTask<object> global = Dm.Call(null, "push_usr", pushed, cb);
            Assert.True(global.IsFaulted);
            Assert.Contains("undefined proc", global.Exception.Message);
            DmTask<object> missing = Dm.Call(world, "no_such_world_proc");
            Assert.True(missing.IsFaulted);
            Assert.Contains("undefined proc", missing.Exception.Message);
            // An unknown argument name is the binder's runtime error, naming the /world proc.
            DmTask<object> badName = Dm.Call(world, "push_usr", Dm.named("nobody", 1));
            Assert.True(badName.IsFaulted);
            Assert.Contains("/world/proc/push_usr", badName.Exception.Message);
        }
    }

    [Fact]
    public void ACallbackWithArgumentsReachesPushUsrByName()
    {
        // callback.dm:100-101: Invoke with no usr and arguments calls world.push_usr(arglist(list(M, src) + args)).
        var (t, pushed, _, trace, seen) = Setup();
        using (t.World.Enter())
        {
            test_datum_listener target = Dm.@new<test_datum_listener>();
            target.label = "inner";
            target.trace = trace;
            target.seen_usr = seen;
            usr = pushed;
            datum_callback cb = Dm.@new<datum_callback>(target, nameof(test_datum_listener.record_call), "bound");
            usr = null;
            Waiting.NoSleep(cb.Invoke("one", "two"));
            Assert.Null(usr);
            Assert.Equal(new object[] { pushed }, seen);
            Assert.Equal(new[] { "inner called bound one two" }, trace);
            Assert.Empty(t.Runtimes);
        }
    }
}
