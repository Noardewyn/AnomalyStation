using System.Collections;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: values. Contracts wrote the enumeration core (DmEach<T> for Dm.each, Dm.each<T>, Dm.each_cast<T> over
// lists, atoms and the world); values completed each_as and pairs.

/// <summary>
/// The loop source of <c>for (x in L)</c> (rules 6.5; BYOND ref <c>/proc/for/list</c>): <c>foreach (object x in
/// Dm.each(L))</c>. A struct with a struct enumerator, so a loop allocates nothing beyond the list's copy-on-write
/// snapshot (a list is copied only when it is changed while a loop over it runs). The source may be anything:
/// null and non-lists iterate nothing, an atom iterates its <c>contents</c>, a list iterates a snapshot of its items
/// as they were when the loop started, and <c>world</c> iterates the live registered atoms in the world's order (not a
/// copy, BYOND's one exception; <c>world.contents</c> holds atoms only, whatever the loop type): mobs, then the other
/// movables, then areas, then turfs, each group in id order (OpenDream <c>AtomManager.EnumerateAtoms</c>; IDmAtom.dm_world_order).
/// An atom created during the loop is met if its group is the current or a later one and its id is past the cursor. <c>Dm.each&lt;T&gt;()</c>, the
/// loop without <c>in</c>, walks the world registry instead (rules 6.5): the atoms of an atom loop type, the non-atom
/// datums of <c>/datum</c> or of a non-atom loop type. The enumerator stays valid across <c>await</c> and exceptions
/// (nothing is pooled).
/// Implements <see cref="IEnumerable{T}"/> for host and test code (boxed); translated code only uses <c>foreach</c>.
/// </summary>
public readonly struct DmEach<T> : IEnumerable<T>
{
    private readonly object _source;
    private readonly DmEachMode _mode;
    private readonly DmAs _as;

    internal DmEach(object source, DmEachMode mode, DmAs kinds = DmAs.anything)
    {
        _source = source;
        _mode = mode;
        _as = kinds;
    }

    public Enumerator GetEnumerator() => new Enumerator(_source, _mode, _as);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>The enumerator of <see cref="DmEach{T}"/>; <c>foreach</c> disposes it, which ends the list snapshot.</summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly DmEachMode _mode;
        private readonly DmAs _as;
        private DmList _list;
        private object[] _items;
        private int _count;
        private int _index;
        private ObjectRegistry _world;
        private bool _registry;
        private long _cursor;
        private int _group;
        private T _current;

        internal Enumerator(object source, DmEachMode mode, DmAs kinds)
        {
            _mode = mode;
            _as = kinds;
            _list = null;
            _items = null;
            _count = 0;
            _index = 0;
            _world = null;
            _registry = false;
            _cursor = 0;
            _group = 0;
            _current = default;
            DmList list = source switch
            {
                DmList l => l,
                IDmAtom atom => atom.dm_each_contents,
                _ => null,
            };
            if (list != null)
            {
                _list = list;
                _items = list.AcquireSnapshot(out _count);
            }
            else if (source is DmWorld)
            {
                _world = World.Current.Objects;
            }
            else if (source == DmWorldLoop.Registry)
            {
                _world = World.Current.Objects;
                _registry = true;
            }
        }

        public T Current => _current;

        object IEnumerator.Current => _current;

        public bool MoveNext()
        {
            if (_world != null)
            {
                // for(x in world) means world.contents, atoms only (BYOND ref /world/var/contents), also with a
                // /datum loop var (tg reference_tracking.dm:101, debug.dm:1033: "atoms (don't believe its
                // lies)"). Without 'in' (Dm.each<T>()), a loop var of /datum or of a non-atom type finds the
                // registered non-atom datums instead (tg debug.dm:1039, reference_tracking.dm:113,
                // SDQL_2.dm:664: "to have it not return atoms"); an atom loop type finds atoms either way.
                // Atoms come in the world's order: one pass over the registry per group (mobs, other movables,
                // areas, turfs); datums in one pass in id order.
                bool wantDatums = _registry && DmWorldLoop<T>.NonAtomDatums;
                while (true)
                {
                    while (_world.TryGetNextLive(_cursor, out long id, out object obj))
                    {
                        _cursor = id;
                        if (wantDatums)
                        {
                            if (obj is IDmAtom) continue;
                        }
                        else if (obj is not IDmAtom atom || atom.dm_world_order != _group)
                            continue;
                        if (Accept(obj)) return true;
                    }
                    if (wantDatums || _group >= DmWorldOrder.Last)
                        return false;
                    _group++;
                    _cursor = 0;
                }
            }
            while (_index < _count)
            {
                if (Accept(_items[_index++])) return true;
            }
            _current = default;
            return false;
        }

        private bool Accept(object item)
        {
            switch (_mode)
            {
                case DmEachMode.All:
                    _current = (T)item;
                    return true;
                case DmEachMode.Filter:
                    if (item is T typed)
                    {
                        _current = typed;
                        return true;
                    }
                    return false;
                case DmEachMode.As:
                    if (!DmAsFilter.Matches(item, _as)) return false;
                    _current = (T)item;
                    return true;
                default: // Cast: as anything, every item including null; a wrongly typed item is a runtime error here
                    if (item == null)
                    {
                        _current = default;
                        return true;
                    }
                    if (item is T cast)
                    {
                        _current = cast;
                        return true;
                    }
                    throw new DmRuntimeException("for (... as anything in L): " + Dm.KindOf(item) + " is not a " + typeof(T).Name);
            }
        }

        public void Dispose()
        {
            if (_list != null)
            {
                _list.ReleaseSnapshot(_items);
                _list = null;
            }
            _items = null;
            _count = 0;
            _world = null;
            _registry = false;
        }

        public void Reset() => throw new NotSupportedException();
    }
}

/// <summary>The world's order of atoms (<see cref="IDmAtom.dm_world_order"/>): mobs, other movables, areas, turfs.</summary>
internal static class DmWorldOrder
{
    /// <summary>The last group (turfs).</summary>
    internal const int Last = 3;
}

/// <summary>The loop without <c>in</c> (<c>Dm.each&lt;T&gt;()</c>, rules 6.5).</summary>
internal static class DmWorldLoop
{
    /// <summary>The source marker of <c>Dm.each&lt;T&gt;()</c>: the world registry rather than a list.</summary>
    internal static readonly object Registry = new();
}

/// <summary>
/// Whether a loop without <c>in</c> (<c>Dm.each&lt;T&gt;()</c>, rules 6.5) walks the registered non-atom datums rather
/// than the atoms: when <typeparamref name="T"/> is a DM class that is not an atom type (<c>/datum</c> itself, a datum
/// subtype, tg's <c>/client</c>). <c>object</c>, the interfaces and the atom types mean atoms. An immutable per-type
/// cache.
/// </summary>
internal static class DmWorldLoop<T>
{
    internal static readonly bool NonAtomDatums = Compute();

    private static bool Compute()
    {
        Type t = typeof(T);
        return t.IsClass && typeof(IDmObject).IsAssignableFrom(t) && !typeof(IDmAtom).IsAssignableFrom(t);
    }
}

internal enum DmEachMode
{
    All,     // Dm.each: every item
    Filter,  // Dm.each<T>: items that are T (nulls and other types skipped)
    Cast,    // Dm.each_cast<T>: every item, checked as T
    As,      // Dm.each_as: items of the DmAs kinds
}

/// <summary>
/// The kind test of <c>for (x as mob|obj in L)</c> (BYOND ref <c>/proc/for/list</c>): an object's kind is the first
/// <c>[DmPath]</c> on its class chain (<c>/mob/...</c> is a mob, <c>/obj/...</c> an obj, <c>/turf/...</c> a turf,
/// <c>/area/...</c> an area); text, numbers, null, sounds, icons and files are the input types of the same names
/// (the text-like input types <c>message</c>, <c>key</c>, <c>password</c>, <c>color</c> and <c>command_text</c>
/// accept text). <c>anything</c> (no flag) accepts every item.
/// </summary>
internal static class DmAsFilter
{
    internal static bool Matches(object item, DmAs kinds)
    {
        if (kinds == DmAs.anything) return true;
        if (item == null) return (kinds & DmAs.@null) != 0;
        if (item is string) return (kinds & (DmAs.text | DmAs.message | DmAs.key | DmAs.password | DmAs.color | DmAs.command_text)) != 0;
        if (Dm.isnum(item)) return (kinds & DmAs.num) != 0;
        if (item is sound) return (kinds & DmAs.sound) != 0;
        if (Dm.isicon(item) && (kinds & DmAs.icon) != 0) return true;
        if (Dm.isfile(item) && (kinds & DmAs.file) != 0) return true;
        string path = DmTextRender.DmPathOf(item.GetType());
        if (path == null) return false;
        return ((kinds & DmAs.mob) != 0 && IsUnder(path, "/mob"))
            || ((kinds & DmAs.obj) != 0 && IsUnder(path, "/obj"))
            || ((kinds & DmAs.turf) != 0 && IsUnder(path, "/turf"))
            || ((kinds & DmAs.area) != 0 && IsUnder(path, "/area"));
    }

    private static bool IsUnder(string path, string root)
        => path.StartsWith(root, StringComparison.Ordinal) && (path.Length == root.Length || path[root.Length] == '/');
}

/// <summary>
/// The loop source of <c>for (k, v in L)</c> (BYOND 516; rules 6.5): <c>foreach ((object k, object v) in
/// Dm.pairs(L))</c>; the keys with the values associated at the start of the loop (both copied then, so the loop body
/// may change the list). A list iterates its items, an atom its contents; null and other values iterate nothing. A
/// typed source (<c>Dm.pairs&lt;T&gt;</c>) skips keys that are not a <typeparamref name="T"/>, nulls included.
/// </summary>
public readonly struct DmPairs<T> : IEnumerable<(T key, object value)>
{
    internal readonly object _source;
    internal readonly bool _typed;

    internal DmPairs(object source, bool typed)
    {
        _source = source;
        _typed = typed;
    }

    public IEnumerator<(T key, object value)> GetEnumerator()
    {
        DmList list = _source switch
        {
            DmList l => l,
            IDmAtom atom => atom.dm_each_contents,
            _ => null,
        };
        return Walk(list?.CopyPairs() ?? Array.Empty<(object, object)>(), _typed);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static IEnumerator<(T key, object value)> Walk((object Key, object Value)[] pairs, bool typed)
    {
        foreach ((object key, object value) in pairs)
        {
            if (key is T typedKey) yield return (typedKey, value);
            else if (!typed && key == null) yield return (default, value);
        }
    }
}

/// <summary>An entry <c>key = value</c> of <c>list(...)</c> / <c>alist(...)</c>: <c>Dm.kv(key, value)</c> (rules 6.1).</summary>
public sealed class DmKeyValue
{
    public DmKeyValue(object key, object value)
    {
        Key = key;
        Value = value;
    }

    public object Key { get; }

    public object Value { get; }
}

/// <summary>
/// The type filter of <c>for (x as mob|obj in L)</c>: <c>Dm.each_as(L, DmAs.mob | DmAs.obj)</c> (rules 6.5). The
/// names are BYOND's input types (BYOND ref <c>/proc/input</c>, <c>/proc/for/list</c>); <c>anything</c> is no filter.
/// </summary>
[Flags]
public enum DmAs
{
    anything = 0,
    @null = 1 << 0,
    text = 1 << 1,
    obj = 1 << 2,
    mob = 1 << 3,
    turf = 1 << 4,
    num = 1 << 5,
    message = 1 << 6,
    area = 1 << 7,
    color = 1 << 8,
    file = 1 << 9,
    command_text = 1 << 10,
    sound = 1 << 11,
    icon = 1 << 12,
    key = 1 << 13,
    password = 1 << 14,
}
