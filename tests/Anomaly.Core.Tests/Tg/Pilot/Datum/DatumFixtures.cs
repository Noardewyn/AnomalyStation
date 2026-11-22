using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

// Test datums for the behaviour tests of the datum package (wave 2 pilot). They are shaped like translated tg types
// (snake_case DM names, a fixture [DmPath] /datum/<kind>/test_datum_<name> of translation-rules.md 15, which no tg path
// starts with, public fields for vars, the slot signatures of docs/ai/specs/generated/slots.tsv for the overrides) and
// record what happens to them in a shared trace, the recording harness of translation-rules.md 15. Traces and other
// host-side helpers are [NotHashed]: they are not simulation state.

/// <summary>A world for one test, with SSgarbage created the way the MC would create it (NEW_SS_GLOBAL).</summary>
internal sealed class DatumTestWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();

    public DatumTestWorld(ulong seed = 7, bool withGarbage = true)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        AtomsStandIn.Install(World); // GLOB and SSatoms, which /atom/New reads (wave 3)
        using (World.Enter())
        {
            // Wave 4: /mob/Destroy takes the mob out of tg's mob lists (mob.dm:34-37), which GLOB always has (their
            // GLOBAL_LIST_EMPTY initializers, _globalvars/lists/mobs.dm); the mobs these tests use as usr are destroyed.
            GLOB.mob_list = Dm.list();
            GLOB.alive_mob_list = Dm.list();
            GLOB.suicided_mob_list = Dm.list();
            GLOB.dead_mob_list = Dm.list();
        }
        if (withGarbage)
        {
            using (World.Enter())
                Dm.@new<datum_controller_subsystem_garbage>();
        }
    }

    /// <summary>The runtime messages reported so far, "Type: message".</summary>
    public List<string> Messages => Runtimes.Select(e => e.GetType().Name + ": " + e.Message).ToList();
}

/// <summary>A listener: signal handlers that record their calls and return <see cref="returns"/>.</summary>
[DmPath("/datum/test_datum_listener")]
public class test_datum_listener : datum
{
    [NotHashed]
    public List<string> trace;

    /// <summary>The name in the trace.</summary>
    public string label = "listener";

    /// <summary>What the handlers return (a bitfield, signals.dm:8).</summary>
    public object returns = NONE;

    /// <summary>A datum the during-dispatch handlers act on.</summary>
    public datum other;

    public datum other_target;

    // A plain var for state-hash tests.
    public float counter = 0;

    [NotHashed]
    public List<object> seen_usr;

    [SignalHandler]
    public virtual object on_signal(datum source = null, object a = null, object b = null)
    {
        trace?.Add(label + " " + Dm.str(a) + " " + Dm.str(b));
        return returns;
    }

    /// <summary>Declares fewer parameters than the signal sends (the binder drops the rest, rules 7.7).</summary>
    [SignalHandler]
    public virtual object on_signal_source_only(datum source = null)
    {
        trace?.Add(label + " source=" + (source == null ? "null" : source.dm_id.ToString()));
        return returns;
    }

    /// <summary>Declares no parameter at all.</summary>
    [SignalHandler]
    public virtual object on_signal_no_params()
    {
        trace?.Add(label + " no params");
        return returns;
    }

    /// <summary>Registers <see cref="other"/> on the source for the same signal, during the dispatch.</summary>
    [SignalHandler]
    public virtual object on_signal_register_other(datum source = null, object a = null, object b = null)
    {
        trace?.Add(label + " registers " + ((test_datum_listener)other).label);
        other.RegisterSignal(source, "pilot_ping", nameof(on_signal));
        return returns;
    }

    /// <summary>Unregisters <see cref="other"/> from the source, during the dispatch.</summary>
    [SignalHandler]
    public virtual object on_signal_unregister_other(datum source = null, object a = null, object b = null)
    {
        trace?.Add(label + " unregisters " + ((test_datum_listener)other).label);
        other.UnregisterSignal(source, "pilot_ping");
        return returns;
    }

    /// <summary>Qdels <see cref="other"/>, a listener queued after this one, during the dispatch.</summary>
    [SignalHandler]
    public virtual object on_signal_qdel_other(datum source = null, object a = null, object b = null)
    {
        trace?.Add(label + " qdels " + ((test_datum_listener)other).label);
        qdel(other);
        return returns;
    }

    [SignalHandler]
    public virtual object on_signal_fail(datum source = null, object a = null, object b = null)
    {
        trace?.Add(label + " fails");
        throw Dm.CRASH("pilot handler failure", "pilot", 1);
    }

    /// <summary>A handler that sleeps (tg forbids it by lint; the port reports it, DV5).</summary>
    public virtual async DmTask<object> on_signal_sleep(datum source = null, object a = null, object b = null)
    {
        trace?.Add(label + " sleeps");
        await sleep(1);
        trace?.Add(label + " woke");
        return returns;
    }

    /// <summary>For COMSIG_PREQDELETED and COMSIG_QDELETING: records the source's state and the force argument.</summary>
    [SignalHandler]
    public virtual object on_qdel_signal(datum source = null, object force = null)
    {
        trace?.Add(label + " gc_destroyed=" + Describe(source.gc_destroyed) + " force=" + Describe(force)
            + " registered=" + (World.Current.Objects.TryGet(source.dm_id, out _) ? "yes" : "no"));
        return returns;
    }

    /// <summary>Resolves a weakref of the source inside its COMSIG_QDELETING (weakrefs.dm:90-91).</summary>
    [SignalHandler]
    public virtual object on_qdeleting_resolve(datum source = null, object force = null)
    {
        var weak = (datum_weakref)source.weak_reference;
        trace?.Add("resolve=" + Describe(weak.resolve()) + " hard_resolve=" + (weak.hard_resolve() == source ? "source" : Describe(weak.hard_resolve())));
        return returns;
    }

    /// <summary>For COMSIG_CD_STOP and COMSIG_CD_RESET: records the arguments and whether the cooldown still runs.</summary>
    [SignalHandler]
    public virtual object on_cooldown_signal(datum source = null, object timeleft = null, object extra = null)
    {
        trace?.Add(label + " cooldown signal timeleft=" + Describe(timeleft) + " extra=" + Describe(extra)
            + " running=" + Describe(TIMER_COOLDOWN_RUNNING(source, "pilot")));
        return returns;
    }

    /// <summary>A target for callbacks: records its arguments and usr, returns <see cref="returns"/>.</summary>
    public virtual object record_call(object a = null, object b = null, object c = null)
    {
        trace?.Add(label + " called " + Describe(a) + " " + Describe(b) + " " + Describe(c));
        seen_usr?.Add(usr);
        return returns;
    }

    /// <summary>A target for callbacks that sleeps <paramref name="ds"/> deciseconds before it returns.</summary>
    public virtual async DmTask<object> sleep_then_return(object ds = null, object value = null)
    {
        trace?.Add(label + " sleeps");
        seen_usr?.Add(usr);
        await sleep(Dm.num(ds));
        trace?.Add(label + " woke");
        seen_usr?.Add(usr);
        return value;
    }

    public virtual object fail_call()
    {
        trace?.Add(label + " fails");
        throw Dm.CRASH("pilot callback failure", "pilot", 2);
    }

    public static string Describe(object value) => value switch
    {
        null => "null",
        datum d => "#" + d.dm_id,
        _ => Dm.str(value),
    };
}

/// <summary>A datum whose Destroy records the state qdel gives it and returns a chosen hint.</summary>
[DmPath("/datum/test_datum_destroyable")]
public class test_datum_destroyable : datum
{
    /// <summary>Text standing for "return ..()" in <see cref="hint"/>.</summary>
    public const string ParentHint = "parent";

    [NotHashed]
    public List<string> trace;

    public string label = "destroyable";

    /// <summary>What Destroy returns after calling the parent: <see cref="ParentHint"/>, a QDEL_HINT_*, null, ...</summary>
    public object hint = ParentHint;

    /// <summary>When set, Destroy qdels this datum again (a qdel loop, garbage.dm:366-367).</summary>
    public float qdel_self_in_destroy = 0;

    /// <summary>What dump_harddel_info returns (garbage.dm:287).</summary>
    public object harddel_info;

    public float destroy_calls = 0;

    public override object Destroy(float? force = null)
    {
        destroy_calls++;
        trace?.Add(label + " Destroy gc_destroyed=" + test_datum_listener.Describe(gc_destroyed) + " force=" + test_datum_listener.Describe(force)
            + " QDESTROYING=" + (QDESTROYING(this) ? "yes" : "no"));
        if (qdel_self_in_destroy != 0)
            qdel(this);
        object parent = base.Destroy(force);
        return Dm.eq(hint, ParentHint) ? parent : hint;
    }

    public override object dump_harddel_info()
    {
        trace?.Add("dump_harddel_info");
        return harddel_info;
    }
}

/// <summary>A component that records the force qdel passes to it (datum.dm:154, :157 pass FALSE).</summary>
[DmPath("/datum/component/test_datum_component")]
public class test_datum_component : datum_component
{
    [NotHashed]
    public List<string> trace;

    public string label = "component";

    // The datum tests build components without a parent and put them into _datum_components by hand. tg's
    // /datum/component/New(list/raw_args) (datums/components/_component.dm:43, translated by the dcs package) reads
    // raw_args[1] and joins the parent, so this fixture's New does nothing instead.
    public override async DmTask New(params object[] __extra)
    {
    }

    public override object Destroy(float? force = null)
    {
        trace?.Add(label + " Destroy force=" + test_datum_listener.Describe(force));
        return base.Destroy(force);
    }
}

/// <summary>A timer that records its deletion (datum.dm:133-139).</summary>
[DmPath("/datum/timedevent/test_datum_timer")]
public class test_datum_timer : datum_timedevent
{
    [NotHashed]
    public List<string> trace;

    public string label = "timer";

    // ADAPTED: a stand-in that is never scheduled. The translated /datum/timedevent/New (timer.dm:391) needs a callback
    // and joins a bucket; this one only takes the world's SStimer, which the timer's Destroy (timer.dm:425) ejects it from.
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        timer_subsystem = SStimer;
    }

    public override object Destroy(float? force = null)
    {
        trace?.Add(label + " Destroy");
        return base.Destroy(force);
    }
}

/// <summary>A mob to stand for usr (tg's /datum/callback captures WEAKREF(usr)).</summary>
[DmPath("/mob/test_datum_user")]
public class test_datum_user : mob
{
    public string label = "user";
}

/// <summary>tg macros the tests use as tg code would.</summary>
internal static class DatumTestMacros
{
    /// <summary>
    /// tg <c>SEND_SIGNAL(target, sigtype, arguments...)</c> (__DEFINES/dcs/helpers.dm:5), expanded as rules 11.1 does:
    /// <c>!target._listen_lookup?[sigtype] ? NONE : target._SendSignal(sigtype, list(target, ##arguments))</c>.
    /// </summary>
    public static object SEND_SIGNAL(datum target, object sigtype, params object[] arguments)
    {
        if (!Dm.truthy(target._listen_lookup?[sigtype]))
            return NONE;
        var items = new object[arguments.Length + 1];
        items[0] = target;
        arguments.CopyTo(items, 1);
        return target._SendSignal(sigtype, Dm.list(items));
    }

    /// <summary>
    /// The message of a stack_trace runtime reported at <paramref name="file"/>:<paramref name="line"/>: the message
    /// itself. tg's _stack_trace hides the use site in the message (stack_trace.dm:4) and world/Error moves it back to the
    /// runtime's file and line (error_handler.dm:62-66); the port's stack_trace member does that rewrite, so the use site
    /// is the runtime's DmFile and DmLine (rules 9.5, v1.2), which DatumHelperTests checks, and not part of the message.
    /// </summary>
    public static string StackTraceText(string message, string file, int line) => message;
}
