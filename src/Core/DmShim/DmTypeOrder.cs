using System.Collections;
using System.Linq;
using System.Reflection;

namespace Anomaly.Core.DmShim;

// Owner: dynamic. The source of DM object-tree order for the type registry (docs/ai/specs/object-model.md section 9).

/// <summary>
/// Where <see cref="DmTypes"/> takes DM object-tree order from: every DM type path in depth-first preorder with
/// children in compile order, and the procs that <c>typesof(/t/proc)</c> enumerates (object-model.md section 9). The
/// generated <c>Anomaly.Core.Tg.TypeOrder</c> and <c>Anomaly.Core.Tg.ProcOrder</c> classes, which the Tg assembly names with
/// <c>[assembly: DmTypeOrder]</c>, are the real source (<see cref="DmTypeOrder.FromGenerated"/>); tests pass their own.
/// </summary>
internal interface IDmTypeOrder
{
    /// <summary>The DM type paths in object-tree order (may be empty: every type then sorts in a path-ordered preorder).</summary>
    IReadOnlyList<string> TypePaths { get; }

    /// <summary>
    /// The names of the procs (or verbs, <paramref name="verbs"/>) declared on <paramref name="typePath"/> itself, in
    /// compile order; false when the source does not list that type.
    /// </summary>
    bool TryGetProcs(string typePath, bool verbs, out IReadOnlyList<string> procNames);
}

/// <summary>An immutable <see cref="IDmTypeOrder"/>.</summary>
internal sealed class DmTypeOrder : IDmTypeOrder
{
    /// <summary>No order information: types sort in a path-ordered preorder, proc lists come from reflection.</summary>
    internal static readonly DmTypeOrder Empty = new(Array.Empty<string>(), null);

    private readonly string[] _paths;
    private readonly Dictionary<string, string[]> _procs;

    /// <summary>
    /// M3 P1 (lead-authorised, perf; types-converter.md 9): the generated table's parent index per path (-1 for a root)
    /// and class name per path (null: no class in <see cref="ClassAssembly"/>), or null when the source has none. With
    /// them the registry registers the classes of <see cref="ClassAssembly"/> without loading them.
    /// </summary>
    internal IReadOnlyList<int> Parents { get; private init; }

    internal IReadOnlyList<string> Classes { get; private init; }

    internal Assembly ClassAssembly { get; private init; }

    internal string ClassNamespace { get; private init; }

    internal DmTypeOrder(IEnumerable<string> paths, IEnumerable<KeyValuePair<string, IEnumerable<string>>> procs)
    {
        _paths = paths?.ToArray() ?? Array.Empty<string>();
        _procs = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (procs != null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> pair in procs)
                _procs[pair.Key] = pair.Value?.ToArray() ?? Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> TypePaths => _paths;

    // Verbs are stored under "<path>/verb" (FromClasses reads ProcOrder.Verbs that way; tg enumerates no verb path at
    // the snapshot, so the generated table is empty).
    public bool TryGetProcs(string typePath, bool verbs, out IReadOnlyList<string> procNames)
    {
        if (typePath != null && _procs.TryGetValue(verbs ? typePath + "/verb" : typePath, out string[] names))
        {
            procNames = names;
            return true;
        }
        procNames = null;
        return false;
    }

    /// <summary>
    /// The generated order named by <c>[assembly: DmTypeOrder(typeof(TypeOrder), typeof(ProcOrder))]</c> (the Tg
    /// assembly, <c>src/Core/Tg/Generated/*.g.cs</c>, produced by <c>tools/coloring</c>). The shim cannot reference the Tg
    /// assembly, so it reads the named classes by reflection, once. The fallback is explicit: when no scanned assembly
    /// carries the attribute, <see cref="Empty"/> (every type sorts in a path-ordered preorder, <c>typesof(/t/proc)</c>
    /// reads the class's methods). Two assemblies with the attribute, or a named class without <c>Paths</c>, are a
    /// configuration error and throw.
    /// </summary>
    internal static DmTypeOrder FromGenerated(IEnumerable<Assembly> assemblies)
    {
        DmTypeOrderAttribute found = null;
        Assembly source = null;
        foreach (Assembly assembly in assemblies)
        {
            DmTypeOrderAttribute attribute = assembly.GetCustomAttribute<DmTypeOrderAttribute>();
            if (attribute == null) continue;
            if (found != null)
                throw new InvalidOperationException("[assembly: DmTypeOrder] is on both " + source.GetName().Name + " and " + assembly.GetName().Name + "; one order source per process");
            found = attribute;
            source = assembly;
        }
        return found == null ? Empty : FromClasses(found.TypeOrder, found.ProcOrder);
    }

    /// <summary>The order held by a <c>TypeOrder</c> class (<c>Paths</c>) and a <c>ProcOrder</c> class (<c>Procs</c>, <c>Verbs</c>; may be null).</summary>
    internal static DmTypeOrder FromClasses(Type typeOrder, Type procOrder)
    {
        if (typeOrder == null || ReadStatic(typeOrder, "Paths") is not IEnumerable<string> paths)
            throw new InvalidOperationException("[assembly: DmTypeOrder]: " + (typeOrder?.FullName ?? "null") + " has no public static Paths of type path text");
        var procs = new List<KeyValuePair<string, IEnumerable<string>>>();
        if (procOrder != null)
        {
            AddProcTable(procOrder, "Procs", "", procs);
            AddProcTable(procOrder, "Verbs", "/verb", procs);
        }
        var order = new DmTypeOrder(paths, procs);
        if (ReadStatic(typeOrder, "Parents") is IEnumerable<int> parents && ReadStatic(typeOrder, "Classes") is IEnumerable<string> classes)
        {
            int[] p = parents.ToArray();
            string[] c = classes.ToArray();
            if (p.Length == order._paths.Length && c.Length == order._paths.Length)
                return new DmTypeOrder(order._paths, procs) { Parents = p, Classes = c, ClassAssembly = typeOrder.Assembly, ClassNamespace = typeOrder.Namespace };
        }
        return order;
    }

    // A ProcOrder table: type path -> names; verbs are kept under "<path>/verb" (TryGetProcs).
    private static void AddProcTable(Type procOrder, string member, string suffix, List<KeyValuePair<string, IEnumerable<string>>> procs)
    {
        if (ReadStatic(procOrder, member) is not IDictionary table) return;
        foreach (DictionaryEntry entry in table)
        {
            if (entry.Key is string key && entry.Value is IEnumerable<string> names)
                procs.Add(new KeyValuePair<string, IEnumerable<string>>(key + suffix, names));
        }
    }

    private static object ReadStatic(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        FieldInfo field = type.GetField(name, flags);
        if (field != null) return field.GetValue(null);
        PropertyInfo property = type.GetProperty(name, flags);
        return property != null && property.GetIndexParameters().Length == 0 ? property.GetValue(null) : null;
    }
}
