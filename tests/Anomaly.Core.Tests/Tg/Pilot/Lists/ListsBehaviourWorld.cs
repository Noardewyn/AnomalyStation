using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// The world of the behaviour tests of the list, sort and comparator helpers (code/__HELPERS/_lists.dm, cmp.dm,
/// sorts/), written from the tg source independently of the translation's own smoke tests. <c>GLOB</c> is built with a
/// C# constructor because nothing runs <c>GLOB.Initialize</c> yet (the controller wave); its <c>sortInstance</c> is
/// created with <c>Dm.@new</c> (its <c>dm_init_vars</c> makes the run lists), and the factory can replace it, for
/// example by a recording subclass.
/// </summary>
internal sealed class ListsBehaviourWorld : IDisposable
{
    private readonly World.Scope _scope;

    public ListsBehaviourWorld(ulong seed = 1234, Func<datum_sort_instance> sorter = null)
    {
        Errors = new List<Exception>();
        World = new World(seed) { RuntimeErrorSink = Errors.Add };
        _scope = World.Enter();
        TgState.Current.GLOB = new datum_controller_global_vars();
        GlobalProcs.GLOB.sortInstance = sorter != null ? sorter() : Dm.@new<datum_sort_instance>();
    }

    public World World { get; }

    /// <summary>The runtimes reported through <c>World.ReportRuntime</c> (stack_trace and the isolation boundaries).</summary>
    public List<Exception> Errors { get; }

    public void Dispose() => _scope.Dispose();

    /// <summary>DM <c>list(a, b, ...)</c>: a list argument becomes one nested item.</summary>
    public static DmList DL(params object[] items) => Dm.list(items);

    /// <summary>The items (keys) of a list in order.</summary>
    public static object[] Items(object list)
    {
        if (list is not DmList l) throw new Xunit.Sdk.XunitException("expected a list, got " + (list?.GetType().Name ?? "null"));
        var items = new object[l.Count];
        for (int i = 0; i < l.Count; i++) items[i] = l[(float)(i + 1)];
        return items;
    }

    /// <summary>The items as DM numbers (a boxed int and a float of the same value are one DM number).</summary>
    public static float[] Nums(object list) => Items(list).Select(x => Dm.num(x)).ToArray();

    /// <summary>The text form of a proc reference, GLOBAL_PROC_REF(name) (rules 7.7).</summary>
    public static string ProcRef(string name) => "/proc/" + name;

    /// <summary>Asserts DM equality (rules 4.2: numbers by value whatever their box).</summary>
    public static void AssertDmEqual(object expected, object actual)
        => Xunit.Assert.True(Dm.eq(expected, actual), "expected " + Describe(expected) + ", got " + Describe(actual));

    /// <summary>Asserts that a list holds exactly these items, compared with DM equality.</summary>
    public static void AssertItems(object list, params object[] expected)
    {
        object[] actual = Items(list);
        Xunit.Assert.True(actual.Length == expected.Length && actual.Zip(expected).All(p => Dm.eq(p.First, p.Second)),
            "expected (" + string.Join(", ", expected.Select(Describe)) + "), got (" + string.Join(", ", actual.Select(Describe)) + ")");
    }

    private static string Describe(object x) => x switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        DmList l => "list(" + string.Join(", ", Items(l).Select(Describe)) + ")",
        Type t => DmTypes.PathOf(t) ?? t.Name,
        _ => Dm.str(x),
    };
}
