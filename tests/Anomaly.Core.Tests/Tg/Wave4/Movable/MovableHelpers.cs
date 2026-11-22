using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;

namespace Anomaly.Core.Tests.Tg.Wave4.Movable;

/// <summary>Short forms of the chain calls the movable tests make (all of them complete without sleeping).</summary>
internal static class Chain
{
    public static string D(object value) => EngineTrace.D(value);

    /// <summary>BYOND's <c>step(mover, dir)</c> (the builtin), what it returns.</summary>
    public static object Step(atom_movable mover, float dir) => Waiting.NoSleep(Builtins.step(mover, dir));

    /// <summary>The outermost <c>Move</c> of the mover.</summary>
    public static object Move(atom_movable mover, atom newloc, object direct = null, float? glide_size_override = null, float? update_dir = null)
        => Waiting.NoSleep(mover.Move(newloc, direct, glide_size_override, update_dir));

    public static object Force(atom_movable mover, atom destination) => Waiting.NoSleep(mover.forceMove(destination));

    /// <summary>A signal listener on <paramref name="target"/> (see <see cref="datum_test_movable_listener"/>).</summary>
    public static datum_test_movable_listener On(datum target, string signal, string label, object result = null, Action<object, object> act = null)
        => datum_test_movable_listener.On(target, signal, label, result, act);

    /// <summary>The first word of each trace line ("moved", "exited", ...).</summary>
    public static string[] Kinds(IEnumerable<string> trace) => trace.Select(l => l.Split(' ')[0]).ToArray();

    /// <summary>How many lines of the trace start with <paramref name="kind"/> followed by a space.</summary>
    public static int Count(IEnumerable<string> trace, string kind) => trace.Count(l => l.StartsWith(kind + " "));

    /// <summary>The lines of the trace that start with <paramref name="kind"/>.</summary>
    public static string[] Lines(IEnumerable<string> trace, string kind) => trace.Where(l => l.StartsWith(kind + " ")).ToArray();

    /// <summary>The trace equals <paramref name="expected"/>, compared as text so that a failure shows every line in full.</summary>
    public static void AssertLines(IEnumerable<string> expected, IEnumerable<string> actual)
        => Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));

    /// <summary>The DM value is the number <paramref name="expected"/> (never null, never another kind).</summary>
    public static void AssertNumber(float expected, object actual)
    {
        Assert.NotNull(actual);
        Assert.True(Dm.eq(actual, expected), "expected the DM number " + expected + " but got " + (actual ?? "null"));
    }
}
