using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Anomaly.Core.DmShim;

// Owner: dynamic. The type registry behind DmTypes (docs/ai/specs/object-model.md section 9) and the assembly scan
// that the registry and the binder's global-proc lookup share.

/// <summary>
/// The assemblies that may hold DM types or global procs: every loaded assembly that references the shim (or is
/// it), plus, transitively, the assemblies those reference that do too (so the Tg assembly is found from the host
/// or test assembly even before any Tg type was touched). Scanned once, in a fixed order: the shim first, then by
/// full name. An immutable process-wide cache (object-model.md section 10).
/// </summary>
internal static class DmAssemblyScan
{
    private static readonly Lazy<Assembly[]> s_assemblies = new(Scan);

    private static readonly Lazy<Type[]> s_globalProcHolders = new(FindGlobalProcHolders);

    /// <summary>The scanned assemblies, the shim first, then ordered by full name.</summary>
    internal static IReadOnlyList<Assembly> Assemblies => s_assemblies.Value;

    /// <summary>The classes named by <c>[assembly: DmGlobalProcs]</c>, in assembly order (object-model.md section 11).</summary>
    internal static IReadOnlyList<Type> GlobalProcHolders => s_globalProcHolders.Value;

    private static Assembly[] Scan()
    {
        Assembly shim = typeof(DmTypes).Assembly;
        string shimName = shim.GetName().Name;
        var found = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
        while (queue.Count > 0)
        {
            Assembly assembly = queue.Dequeue();
            if (assembly.IsDynamic || !visited.Add(assembly.FullName)) continue;
            AssemblyName[] references;
            try { references = assembly.GetReferencedAssemblies(); }
            catch (Exception) { continue; }
            if (assembly != shim && !references.Any(r => string.Equals(r.Name, shimName, StringComparison.Ordinal))) continue;
            found[assembly.FullName] = assembly;
            foreach (AssemblyName reference in references)
            {
                if (visited.Contains(reference.FullName)) continue;
                try { queue.Enqueue(Assembly.Load(reference)); }
                catch (Exception) { } // a reference that is not deployed holds no DM type
            }
        }
        return found.Values
            .OrderBy(a => a == shim ? 0 : 1)
            .ThenBy(a => a.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static Type[] FindGlobalProcHolders()
        => Assemblies.Select(a => a.GetCustomAttribute<DmGlobalProcsAttribute>()?.Type).Where(t => t != null).ToArray();

    /// <summary>The loadable types of an assembly (a partly unloadable assembly still gives its loadable types).</summary>
    internal static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
    }
}

/// <summary>
/// One immutable DM type tree (object-model.md section 9): DM path to class and back, each class's DM parent and
/// children, and its ordinal in DM object-tree order. <see cref="DmTypes"/> uses the process-wide one built from the
/// <see cref="DmPathAttribute"/>s of the scanned assemblies (<see cref="Discover"/>); tests build their own.
/// <para>
/// M3 P1 (lead-authorised, perf; docs/ai/specs/types-converter.md 9): the nodes are keyed by path. The classes of an
/// assembly whose order source lists them (the Tg assembly's generated <c>TypeOrder</c>: parents and class names) are
/// registered from that table and loaded only when a caller needs the <c>Type</c>, so building the registry no longer
/// loads the 46k generated classes; every other assembly's classes are found by reflection as before.
/// </para>
/// Thread-safe: the tree never changes after construction; the class of a table node is resolved once, and the
/// class-to-node cache and the subtree caches only grow.
/// </summary>
internal sealed class DmTypeRegistry
{
    // BYOND's root types: every other top-level type without an explicit parent_type is a /datum (BYOND ref
    // /datum/var/parent_type: "If you don't specify the parent_type for an object defined at the top level, it
    // defaults to /datum"; the roots are those OpenDream's DM standard library declares outside /datum).
    private static readonly FrozenSet<string> RootPaths = new[]
    {
        "/datum", "/world", "/list", "/alist", "/savefile", "/callee", "/vector",
    }.ToFrozenSet(StringComparer.Ordinal);

    // BYOND's builtin value types (object-model.md section 3): they and their subtypes get no world id and are never
    // registered, whichever assembly implements them (the shim's /list, /sound, /matrix, /exception; Tg's /image,
    // /mutable_appearance, /icon, /generator; a later /regex).
    private static readonly FrozenSet<string> BuiltinValuePaths = new[]
    {
        "/list", "/regex", "/matrix", "/icon", "/image", "/mutable_appearance", "/sound", "/generator", "/exception",
    }.ToFrozenSet(StringComparer.Ordinal);

    private const int MaxDepth = 256;

    private sealed class Node
    {
        internal string Path;
        internal Node Parent;
        internal int Ordinal;
        internal bool IsBuiltinValue;
        internal readonly List<Node> Children = new();

        // The class: known for a reflected class; for a table class, its full name, resolved on first use.
        private Type _type;
        private readonly string _className;
        private readonly Assembly _assembly;
        private int _resolved;

        internal Node(string path, Type type)
        {
            Path = path;
            _type = type;
            _resolved = 1;
        }

        internal Node(string path, Assembly assembly, string className)
        {
            Path = path;
            _assembly = assembly;
            _className = className;
        }

        internal bool IsLazy => _className != null;

        internal Type Type
        {
            get
            {
                if (Volatile.Read(ref _resolved) == 0)
                {
                    Type found = _assembly.GetType(_className, throwOnError: false);
                    Interlocked.CompareExchange(ref _type, found, null);
                    Volatile.Write(ref _resolved, 1);
                }
                return _type;
            }
        }
    }

    private readonly Dictionary<string, Node> _byPath = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Type, Node> _byType = new();
    private readonly ConcurrentDictionary<Type, Node> _nodeOfClass = new(); // registered or not (null): a cache of NodeOf
    private readonly Dictionary<int, Node> _byOrdinal = new();
    private readonly ConcurrentDictionary<Node, ReadOnlyCollection<Type>> _withRoot = new();
    private readonly ConcurrentDictionary<Node, ReadOnlyCollection<Type>> _withoutRoot = new();
    private readonly List<string> _duplicates = new();
    private readonly List<Node> _reflected = new();

    /// <summary>
    /// Builds the tree from <paramref name="types"/> (class and DM path, in priority order: the first class that
    /// claims a path keeps it, later claims are recorded in <see cref="Duplicates"/>) and the order source; when the
    /// order source has a class table (<see cref="DmTypeOrder.Classes"/>), its classes are registered from it first.
    /// </summary>
    internal DmTypeRegistry(IEnumerable<KeyValuePair<Type, string>> types, IDmTypeOrder order)
    {
        Order = order ?? DmTypeOrder.Empty;
        var table = Order as DmTypeOrder;
        bool hasTable = table?.Classes != null;
        if (hasTable)
        {
            IReadOnlyList<string> paths = table.TypePaths;
            for (int i = 0; i < paths.Count; i++)
            {
                if (table.Classes[i] == null || _byPath.ContainsKey(paths[i])) continue;
                _byPath[paths[i]] = new Node(paths[i], table.ClassAssembly, table.ClassNamespace + "." + table.Classes[i]);
            }
        }
        foreach (KeyValuePair<Type, string> pair in types)
        {
            if (pair.Key == null || string.IsNullOrEmpty(pair.Value) || _byType.ContainsKey(pair.Key)) continue;
            if (hasTable && pair.Key.Assembly == table.ClassAssembly && _byPath.ContainsKey(pair.Value)) continue; // the table registers that assembly, but its port types ([DmPortType])
            if (_byPath.TryGetValue(pair.Value, out Node claimed))
            {
                _duplicates.Add(pair.Value + ": " + (claimed.IsLazy ? "the generated table's class" : claimed.Type.FullName) + " and " + pair.Key.FullName);
                continue;
            }
            var node = new Node(pair.Value, pair.Key);
            _byPath[pair.Value] = node;
            _byType[pair.Key] = node;
            _reflected.Add(node);
        }

        // Parents: a table class's from the table (its preorder; the parent may be a reflected class, by path); a
        // reflected class's from C# inheritance (rules 2.3), else /datum for a top-level type that is not a root.
        if (hasTable)
        {
            IReadOnlyList<string> paths = table.TypePaths;
            for (int i = 0; i < paths.Count; i++)
            {
                if (!_byPath.TryGetValue(paths[i], out Node node) || !node.IsLazy || node.Path != paths[i]) continue;
                for (int p = table.Parents[i]; p >= 0; p = table.Parents[p])
                {
                    if (_byPath.TryGetValue(paths[p], out Node parent)) { node.Parent = parent; break; }
                }
            }
        }
        foreach (Node node in _reflected) node.Parent = FindParent(node);
        foreach (Node node in _byPath.Values)
            if (node.Parent != null) node.Parent.Children.Add(node);

        foreach (Node node in _byPath.Values)
        {
            Node current = node;
            for (int depth = 0; current != null && depth < MaxDepth; depth++)
            {
                if (IsBuiltinValuePath(current.Path))
                {
                    node.IsBuiltinValue = true;
                    break;
                }
                current = current.Parent;
            }
        }

        var listed = new Dictionary<string, int>(Order.TypePaths.Count, StringComparer.Ordinal);
        for (int i = 0; i < Order.TypePaths.Count; i++) listed.TryAdd(Order.TypePaths[i], i);
        int next = Order.TypePaths.Count;
        // Listed types take their place in the generated order; the others follow, in a depth-first preorder of this
        // tree with siblings by ordinal path. That is ordinal path order wherever a path extends its parent's ('/' sorts
        // before every identifier character), and still a preorder for a type whose parent_type is not its path prefix
        // (tg's /client under /datum), so typesof(T) always starts with T and keeps T's subtree together.
        var stack = new Stack<Node>();
        foreach (Node root in _byPath.Values.Where(n => n.Parent == null).OrderByDescending(n => n.Path, StringComparer.Ordinal))
            stack.Push(root);
        while (stack.Count > 0)
        {
            Node node = stack.Pop();
            node.Ordinal = listed.TryGetValue(node.Path, out int at) ? at : next++;
            _byOrdinal[node.Ordinal] = node;
            foreach (Node child in node.Children.OrderByDescending(c => c.Path, StringComparer.Ordinal))
                stack.Push(child);
        }
    }

    /// <summary>The order source the registry was built with.</summary>
    internal IDmTypeOrder Order { get; }

    /// <summary>Paths claimed by more than one class ("path: first and second"); empty when every path is unique.</summary>
    internal IReadOnlyList<string> Duplicates => _duplicates;

    /// <summary>Every registered class (this loads every class of the generated table: for tests).</summary>
    internal IEnumerable<Type> Types => _byPath.Values.Select(n => n.Type).Where(t => t != null);

    /// <summary>The registry of the scanned assemblies with the generated order of the Tg assembly.</summary>
    internal static DmTypeRegistry Discover()
    {
        IReadOnlyList<Assembly> assemblies = DmAssemblyScan.Assemblies;
        DmTypeOrder order = DmTypeOrder.FromGenerated(assemblies);
        var types = new List<KeyValuePair<Type, string>>();
        foreach (Assembly assembly in assemblies)
        {
            if (order.Classes != null && assembly == order.ClassAssembly)
            {
                // registered from the table, unloaded; but the port's own types, which the table does not list
                foreach (DmPortTypeAttribute port in assembly.GetCustomAttributes<DmPortTypeAttribute>())
                {
                    DmPathAttribute path = port.Type?.GetCustomAttribute<DmPathAttribute>(inherit: false);
                    if (path != null) types.Add(new KeyValuePair<Type, string>(port.Type, path.Path));
                }
                continue;
            }
            foreach (Type type in DmAssemblyScan.TypesOf(assembly).OrderBy(t => t.MetadataToken))
            {
                DmPathAttribute attribute = type.GetCustomAttribute<DmPathAttribute>(inherit: false);
                if (attribute != null) types.Add(new KeyValuePair<Type, string>(type, attribute.Path));
            }
        }
        return new DmTypeRegistry(types, order);
    }

    // The node of a class: a reflected class by the map; a table class by its [DmPath] when the table's class at that
    // path is this very class; null for every other class (cached).
    private Node NodeOf(Type type)
    {
        if (type == null) return null;
        if (_byType.TryGetValue(type, out Node node)) return node;
        return _nodeOfClass.GetOrAdd(type, t =>
        {
            string path = t.GetCustomAttribute<DmPathAttribute>(inherit: false)?.Path;
            if (path == null || !_byPath.TryGetValue(path, out Node n) || !n.IsLazy || n.Type != t) return null;
            _byType.TryAdd(t, n);
            return n;
        });
    }

    internal string PathOf(Type type) => NodeOf(type)?.Path;

    internal Type FromPath(string path) => path != null && _byPath.TryGetValue(path, out Node node) ? node.Type : null;

    internal bool IsRegistered(Type type) => NodeOf(type) != null;

    /// <summary>
    /// The DM parent: of a registered class, its nearest C# base class that is registered (rules 2.3: C#
    /// inheritance is DM inheritance), else <c>/datum</c> for a top-level type other than BYOND's roots (the shim's
    /// <c>/sound</c>, <c>/exception</c>, <c>/matrix</c>, which cannot derive from tg's <c>datum</c> in C#); of an
    /// unregistered class, its nearest registered C# base.
    /// </summary>
    internal Type ParentOf(Type type)
    {
        if (type == null) return null;
        Node node = NodeOf(type);
        if (node != null) return node.Parent?.Type;
        return NearestRegisteredBase(type)?.Type;
    }

    /// <summary>The class's own DM type: itself when registered, else its nearest registered C# base, else null.</summary>
    internal Type DmTypeOf(Type type)
    {
        if (type == null) return null;
        return NodeOf(type) != null ? type : NearestRegisteredBase(type)?.Type;
    }

    private Node DmNodeOf(Type type) => type == null ? null : NodeOf(type) ?? NearestRegisteredBase(type);

    internal int TreeOrdinal(Type type) => NodeOf(type)?.Ordinal ?? -1;

    /// <summary>The registered class at <paramref name="ordinal"/> in DM object-tree order, or null (a typepath ref, object-model.md section 3).</summary>
    internal Type TypeAtOrdinal(int ordinal) => _byOrdinal.TryGetValue(ordinal, out Node node) ? node.Type : null;

    /// <summary>
    /// Whether objects of the class are BYOND builtin values (<c>/list</c>, <c>/sound</c>, <c>/image</c>, ... and their
    /// subtypes), which get no world id and are never registered (object-model.md section 3).
    /// </summary>
    internal bool IsBuiltinValue(Type type) => DmNodeOf(type)?.IsBuiltinValue ?? false;

    /// <summary>
    /// Whether the binder may call procs and read vars of instances of the class: a DM object (<see cref="IDmObject"/>)
    /// or a registered DM type such as <c>/list</c>; never a plain .NET object (a boxed number, a <c>Type</c>).
    /// </summary>
    internal bool IsDmClass(Type type) => typeof(IDmObject).IsAssignableFrom(type) || DmNodeOf(type) != null;

    /// <summary>
    /// DM derivation (<c>ispath(A, B)</c>; <c>istype</c> on the value's type): <paramref name="type"/> is
    /// <paramref name="ancestor"/> or one of its DM descendants. C# derivation always counts (rules 2.3); the DM
    /// tree adds the shim's builtin types under <c>/datum</c>.
    /// </summary>
    internal bool IsSubtypeOrSame(Type type, Type ancestor)
    {
        if (type == null || ancestor == null) return false;
        if (type == ancestor || ancestor.IsAssignableFrom(type)) return true;
        Node target = NodeOf(ancestor);
        if (target == null) return false;
        Node current = DmNodeOf(type);
        for (int depth = 0; current != null && depth < MaxDepth; depth++)
        {
            if (current == target) return true;
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// <paramref name="root"/>'s DM subtree in tree order (<c>typesof</c> with the root, tg's <c>subtypesof</c>
    /// without): the registered descendants sorted by <see cref="TreeOrdinal"/>. An unregistered root has no
    /// descendants. Cached per root. This loads the classes of the subtree.
    /// </summary>
    internal IReadOnlyList<Type> Subtypes(Type root, bool includeRoot)
    {
        if (root == null) return Array.Empty<Type>();
        Node node = NodeOf(root);
        if (node == null) return includeRoot ? Array.AsReadOnly(new[] { root }) : Array.AsReadOnly(Array.Empty<Type>());
        ConcurrentDictionary<Node, ReadOnlyCollection<Type>> cache = includeRoot ? _withRoot : _withoutRoot;
        return cache.GetOrAdd(node, n => Array.AsReadOnly(CollectSubtree(n, includeRoot)));
    }

    private Type[] CollectSubtree(Node root, bool includeRoot)
    {
        var result = new List<Node>();
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Node node = stack.Pop();
            if (node != root || includeRoot) result.Add(node);
            foreach (Node child in node.Children) stack.Push(child);
        }
        return result.OrderBy(n => n.Ordinal).Select(n => n.Type).Where(t => t != null).ToArray();
    }

    // A builtin value path or a path below one (tg's /mutable_appearance/emissive_blocker).
    private static bool IsBuiltinValuePath(string path)
    {
        foreach (string builtin in BuiltinValuePaths)
        {
            if (path.StartsWith(builtin, StringComparison.Ordinal) && (path.Length == builtin.Length || path[builtin.Length] == '/'))
                return true;
        }
        return false;
    }

    private Node FindParent(Node node)
    {
        Node baseNode = NearestRegisteredBase(node.Type);
        if (baseNode != null) return baseNode;
        bool topLevel = node.Path.Length > 1 && node.Path[0] == '/' && node.Path.IndexOf('/', 1) < 0;
        if (topLevel && !RootPaths.Contains(node.Path) && _byPath.TryGetValue("/datum", out Node datum) && datum != node)
            return datum;
        return null;
    }

    private Node NearestRegisteredBase(Type type)
    {
        for (Type t = type.BaseType; t != null; t = t.BaseType)
        {
            Node node = NodeOf(t);
            if (node != null) return node;
        }
        return null;
    }
}
