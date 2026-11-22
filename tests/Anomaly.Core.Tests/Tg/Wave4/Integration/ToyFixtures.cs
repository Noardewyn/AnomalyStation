using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Integration;

// Fixtures of the W4.M integration tests. Everything the chain runs is tg's real type: /turf/open/indestructible and
// /turf/closed/indestructible with tg's /turf/Enter, /area/misc/testroom with tg's /area/Entered and /area/Exited, /obj, /mob
// and /mob/living. The only test types are signal probes (a datum that records the signals it hears: not a turf, an area or a
// mover, and no override of a proc of the chain) and the ported tg unit tests' own types. Fixture paths are
// /<kind>/test_integration_<name> (translation-rules.md 15).

/// <summary>The per-world recording harness of the integration tests (translation-rules.md 15): lines in call order.</summary>
internal sealed class ToyTrace
{
    public readonly List<string> Lines = new();

    public static ToyTrace Of(World world) => world.GetExtension(static _ => new ToyTrace());

    public static void Add(string line) => Of(World.Current).Lines.Add(line);
}

/// <summary>
/// A datum that hears one signal and records "label source args..." (the source and the first <see cref="arity"/> arguments,
/// written by <see cref="Toy.D"/>); it answers <see cref="result"/> (a signal's return bits) and may run <see cref="act"/>
/// after the record (a handler that moves, deletes or edits something, as tg's own handlers do).
/// </summary>
[DmPath("/datum/test_integration_probe")]
public class datum_test_integration_probe : datum
{
    [NotHashed] public string label;
    [NotHashed] public int arity = 3;
    [NotHashed] public bool stamp_tick;   // prefix the line with "<world tick>:"

    [NotHashed] public object result = NONE;
    [NotHashed] public Action<object[]> act;

    [SignalHandler]
    public virtual object on_signal(datum source = null, object a = null, object b = null, object c = null, object d = null, object e = null)
    {
        var all = new object[] { source, a, b, c, d, e };
        ToyTrace.Add((stamp_tick ? World.Current.Tick + ":" : "") + label + " " + string.Join(" ", all.Take(arity + 1).Select(Toy.D)));
        act?.Invoke(all);
        return result;
    }
}

/// <summary>An extra_checks target of do_after: counts its polls and answers what the test set.</summary>
[DmPath("/datum/test_integration_check")]
public class datum_test_integration_check : datum
{
    [NotHashed] public object answer = TRUE;
    [NotHashed] public float polls;

    public virtual object check()
    {
        polls++;
        return answer;
    }
}

/// <summary>The target of a movement_detector's callback: records what the detector passes (datums/movement_detector.dm:53).</summary>
[DmPath("/datum/test_integration_recorder")]
public class datum_test_integration_recorder : datum
{
    public virtual void on_tracked_moved(object tracked = null, object mover = null, object oldloc = null, object direction = null)
        => ToyTrace.Add("tracked " + Toy.D(tracked) + " " + Toy.D(mover) + " " + Toy.D(oldloc) + " " + Toy.D(direction));
}

/// <summary>Short forms of what the integration tests do a hundred times.</summary>
internal static class Toy
{
    /// <summary>
    /// A value as the traces write it: null as "null", a turf as "(x,y)" ("(x,y,z)" above level 1), an area as "area:name",
    /// another atom by its name, a list as "list(...)", a typepath by its name, a DM number as its text.
    /// </summary>
    public static string D(object value) => value switch
    {
        null => "null",
        turf t => t.z == 1 ? "(" + Dm.str(t.x) + "," + Dm.str(t.y) + ")" : "(" + Dm.str(t.x) + "," + Dm.str(t.y) + "," + Dm.str(t.z) + ")",
        area a => "area:" + a.name,
        atom a => a.name,
        DmList l => "list(" + string.Join(",", Dm.each(l).Cast<object>().Select(D)) + ")",
        Type t => t.Name,
        string s => s,
        float or int or double => Dm.str(value),
        _ => value.ToString(),
    };

    public static List<string> Trace(World world) => ToyTrace.Of(world).Lines;

    /// <summary>A probe on <paramref name="target"/> for <paramref name="signal"/>; <paramref name="arity"/> arguments after the source are written.</summary>
    public static datum_test_integration_probe On(datum target, string signal, string label, int arity = 3, object result = null, Action<object[]> act = null, bool stamp_tick = false)
    {
        var probe = Dm.@new<datum_test_integration_probe>();
        probe.label = label;
        probe.arity = arity;
        probe.stamp_tick = stamp_tick;
        probe.result = result ?? NONE;
        probe.act = act;
        probe.RegisterSignal(target, signal, nameof(datum_test_integration_probe.on_signal));
        return probe;
    }

    /// <summary>A real /obj with a name (the traces name atoms by it), dense when asked.</summary>
    public static obj Thing(string name, atom loc, bool dense = false)
    {
        var o = Dm.@new<obj>(loc);
        o.name = name;
        if (dense)
            o.density = TRUE;
        return o;
    }

    /// <summary>BYOND's <c>step(mover, dir)</c>: what it returns (all of it completes without sleeping).</summary>
    public static object Step(atom_movable mover, float dir) => Waiting.NoSleep(step(mover, dir));

    public static object Move(atom_movable mover, atom newloc, object direct = null) => Waiting.NoSleep(mover.Move(newloc, direct));

    public static object Force(atom_movable mover, atom destination) => Waiting.NoSleep(mover.forceMove(destination));

    /// <summary>The DM value is the number <paramref name="expected"/> (never null, never another kind).</summary>
    public static void AssertNumber(float expected, object actual)
    {
        Assert.NotNull(actual);
        Assert.True(Dm.eq(actual, expected), "expected the DM number " + expected + " but got " + (actual ?? "null"));
    }

    /// <summary>The trace equals <paramref name="expected"/>, compared as text so that a failure shows every line in full.</summary>
    public static void AssertTrace(IEnumerable<string> expected, IEnumerable<string> actual)
        => Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));

    /// <summary>The objects of a DM list, in order.</summary>
    public static object[] Items(object list) => list == null ? Array.Empty<object>() : Dm.each(list).Cast<object>().ToArray();
}
