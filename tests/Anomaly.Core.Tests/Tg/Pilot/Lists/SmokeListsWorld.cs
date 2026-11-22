using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// A world for the smoke tests of the list, sort and comparator helpers (code/__HELPERS/_lists.dm, cmp.dm, sorts/).
/// Until the controller wave nothing creates <c>GLOB</c>, so the test builds it with a C# constructor (test code only;
/// translated code uses <c>Dm.@new</c>) and creates its <c>sortInstance</c> with <c>Dm.@new</c>.
/// </summary>
internal sealed class SmokeListsWorld : IDisposable
{
    private readonly World.Scope _scope;

    public SmokeListsWorld(ulong seed = 42)
    {
        Errors = new List<Exception>();
        World = new World(seed) { RuntimeErrorSink = Errors.Add };
        _scope = World.Enter();
        TgState.Current.GLOB = new datum_controller_global_vars();
        GlobalProcs.GLOB.sortInstance = Dm.@new<datum_sort_instance>();
    }

    public World World { get; }

    public List<Exception> Errors { get; }

    public void Dispose() => _scope.Dispose();

    /// <summary>A list of the given items, none flattened (DM <c>list(a, b, ...)</c>).</summary>
    public static DmList NewList(params object[] items) => Dm.list(items);

    /// <summary>The items of a list in order, for assertions.</summary>
    public static object[] Items(DmList list)
    {
        var items = new object[list.Count];
        for (int i = 0; i < list.Count; i++) items[i] = list[i + 1];
        return items;
    }

    /// <summary>The proc reference text of a global proc (rules 7.7, GLOBAL_PROC_REF).</summary>
    public static string ProcRef(string name) => "/proc/" + name;
}
