using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Anomaly.Core.DmShim;

/// <summary>
/// The DM <c>/list</c> (docs/ai/translation-rules.md section 6; BYOND ref <c>/list</c>): 1-based items with optional
/// associated values per key. Created by <c>Dm.list(...)</c>, <c>Dm.alist(...)</c>, <c>Dm.list_n(n)</c>; never by a
/// C# collection initializer (it is deliberately not <c>IEnumerable</c>, and it has no public enumerator: loops
/// go through <c>Dm.each</c>, which is null-safe and takes a copy-on-write snapshot). It defines no C# operators
/// (rules 6.3: <c>Dm.append(ref L, x)</c> and friends).
/// <para>
/// Owner: values. Contracts wrote the first core storage; values completed the list procs and hardened the core
/// (docs/ai/specs/shim-ownership.md).
/// </para>
/// <para>
/// Storage: <c>_items</c> holds the items (the keys of an associative list) in order; <c>_assoc</c> maps a key to its
/// associated value with DM equality (<see cref="Dm.eq(object, object)"/>, so a number is one key whatever its box:
/// <c>1</c>, <c>1.0</c> and <c>1f</c>); <c>_index</c> counts the occurrences of every item, which makes key and
/// membership lookups O(1). A number that becomes a key by association (<c>L[k] = v</c>) or an alist key is stored as
/// a boxed <c>float</c>, as the contracts core did; other items and values keep the box they arrive in (every shim
/// member reads numbers through <c>Dm.TryGetNumber</c>, so the box is never observable in DM terms). Invariants, kept by every
/// mutation path (all of them go through the private primitives <c>AppendItem</c>, <c>InsertItems</c>,
/// <c>RemoveRange</c>, <c>ReplaceAt</c>, <c>RemoveUnkept</c>, <c>Clear</c>):
/// </para>
/// <list type="bullet">
/// <item>every key in <c>_assoc</c> occurs in the items: an association lives exactly as long as its key occurs
/// (BYOND ref "list associations": an index item that is not in the list reads null);</item>
/// <item><c>_index</c> is null or holds the exact count of every item (NaN, equal to nothing, is not counted);
/// it exists whenever <c>_assoc</c> does, in alist mode, and after the first membership query on a list of
/// <see cref="IndexThreshold"/> or more items;</item>
/// <item>a live snapshot (an enumerator of <c>Dm.each</c>) never sees a write: a write with a live snapshot first
/// replaces <c>_items</c> with a copy (copy-on-write, rules 6.5).</item>
/// </list>
/// <para>
/// The orders of <c>_assoc</c> and <c>_index</c> (which depend on per-process string hashing) are never observed.
/// In alist mode (<c>Dm.alist</c>, BYOND 516) a number is a key, never an index, keys are unique, and the
/// positional procs that would break that (<c>Insert</c>, <c>Splice</c>, <c>Swap</c>, a partial <c>Copy</c>, a
/// <c>len</c> other than 0) are runtime errors, as OpenDream's alist tests pin them.
/// </para>
/// </summary>
[DmPath("/list")]
public sealed partial class DmList
{
    internal object[] _items = Array.Empty<object>();
    internal int _count;
    internal Dictionary<object, object> _assoc;
    internal readonly bool _isAlist;

    // Occurrence counts of the items (see the class remarks); null until first needed.
    internal Dictionary<object, int> _index;

    // Live snapshots (enumerators) of the current _items array: a write with any live snapshot first replaces
    // _items with a copy, so every snapshot keeps the items it started with (copy-on-write, rules 6.5).
    private int _snapshots;

    /// <summary>The item count from which a membership query builds the occurrence index instead of scanning.</summary>
    internal const int IndexThreshold = 8;

    // The engine object whose builtin list this is (an atom's contents, overlays, ...), told after every write.
    private IDmListOwner _owner;

    /// <summary>
    /// The engine owner of a builtin list (an atom's <c>contents</c>, <c>overlays</c>, <c>underlays</c>,
    /// <c>vis_contents</c>), told after every write to the list, by any path: a list operator, a list proc, an
    /// index or <c>len</c> assignment, through any reference. Null for an ordinary list. Not a DM var (the binder
    /// never reaches a <c>dm_</c> name, rules 2.5).
    /// </summary>
    public IDmListOwner dm_owner
    {
        get => _owner;
        set => _owner = value;
    }

    // M2 P3's overlay snapshot hook: the engine's conversion of an item entering or looked up in this list.
    private Func<object, object> _convert;

    /// <summary>
    /// The engine's item conversion of a builtin list (M2 P3, the overlay snapshot: an atom's <c>overlays</c> and
    /// <c>underlays</c> hold an immutable snapshot of each image, mutable appearance, atom or type added, BYOND ref
    /// <c>/atom/var/overlays</c>): applied to every item that enters the list (an append, an insertion, an index
    /// assignment) and to the operand of every lookup (<c>-=</c>, <c>Remove</c>, <c>Find</c>, <c>in</c>, <c>&amp;=</c>,
    /// <c>^=</c>), so <c>overlays -= x</c> finds the snapshot equal to <c>x</c>'s appearance. It must be idempotent.
    /// Null for an ordinary list. Not a DM var (the binder never reaches a <c>dm_</c> name, rules 2.5).
    /// </summary>
    public Func<object, object> dm_convert
    {
        get => _convert;
        set => _convert = value;
    }

    // M2 integration: an engine list that holds an object once.
    private bool _unique;

    /// <summary>
    /// An engine list that holds an item once (BYOND's <c>client.screen</c>: adding an object that is already shown does
    /// nothing): an append of an item already in the list is skipped. False for an ordinary list. Not a DM var (the binder
    /// never reaches a <c>dm_</c> name, rules 2.5).
    /// </summary>
    public bool dm_unique
    {
        get => _unique;
        set => _unique = value;
    }

    // The engine conversion of an item (identity for an ordinary list).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object Conv(object item) => _convert == null ? item : _convert(item);

    // After every write (the primitives below and the len and association writes): tell the owner.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Changed() => _owner?.ListChanged(this);

    /// <summary>An empty list (<c>list()</c>).</summary>
    public DmList()
    {
    }

    internal DmList(int capacity, bool alist)
    {
        if (capacity > 0) _items = new object[capacity];
        _isAlist = alist;
        if (alist) _index = NewIndex();
    }

    /// <summary>The number of items, for host and test code (translated code reads <see cref="len"/>).</summary>
    public int Count => _count;

    /// <summary>
    /// DM <c>L.len</c> (BYOND ref <c>/list/var/len</c>): the number of items. Assigning a smaller value drops the
    /// last items (and the associations of keys that no longer occur), a larger one pads with nulls; a negative or
    /// NaN value is a runtime error. A fractional value is truncated. An alist only accepts 0 (it clears) or its
    /// own length.
    /// </summary>
    public float len
    {
        get => _count;
        set => SetLength(value);
    }

    /// <summary>
    /// DM <c>L[i]</c> with a number (rules 6.2): the item at 1-based position <paramref name="index"/> (truncated);
    /// out of range is DM's "list index out of bounds" runtime error. In alist mode the number is a key.
    /// </summary>
    public object this[float index]
    {
        get => _isAlist ? GetAssociated(index) : _items[CheckIndex(index) - 1];
        set
        {
            if (_isAlist)
            {
                SetAssociated(index, value);
                return;
            }
            int i = CheckIndex(index);
            ReplaceAt(i - 1, value);
        }
    }

    /// <summary>
    /// DM <c>L[x]</c> (rules 6.2): a number is a position (as <see cref="this[float]"/>); any other value is a key:
    /// reading a missing key gives null, assigning a missing key appends it with its associated value.
    /// </summary>
    public object this[object index]
    {
        get => _isFilters && index is string name ? FilterNamed(name) : !_isAlist && Dm.TryGetNumber(index, out float i) ? this[i] : GetAssociated(index);
        set
        {
            if (!_isAlist && Dm.TryGetNumber(index, out float i)) this[i] = value;
            else SetAssociated(index, value);
        }
    }

    // ---- list procs (BYOND ref /list/proc) ----

    /// <summary>
    /// DM <c>L.Add(Item1, Item2, ...)</c> (BYOND ref <c>/list/proc/Add</c>): appends the items; a list argument adds
    /// each of its items, with their associated values (as <c>L += L2</c> does). An alist adds only keys it does not
    /// have yet.
    /// </summary>
    public void Add(params object[] Items)
    {
        Items ??= Dm.OneNull();
        foreach (object item in Items)
        {
            if (item is DmList source) AddAllFrom(source);
            else AddItem(item);
        }
    }

    /// <summary>
    /// DM <c>L.Remove(Item1, Item2, ...)</c> (BYOND ref <c>/list/proc/Remove</c>): removes the last occurrence of each
    /// item ("Removal starts at the end of the list (highest index) so that this operation is an exact reversal of
    /// Add()"; a list argument: each of its items, read before anything is removed); 1 if anything was removed,
    /// else 0.
    /// </summary>
    public float Remove(params object[] Items)
    {
        Items ??= Dm.OneNull();
        bool removed = false;
        foreach (object item in Items)
        {
            if (item is DmList source)
            {
                foreach (object x in source.CopyItems())
                    removed |= RemoveLast(x);
            }
            else
            {
                removed |= RemoveLast(item);
            }
        }
        return removed ? 1f : 0f;
    }

    /// <summary>
    /// DM <c>L.RemoveAll(Item1, Item2, ...)</c> (BYOND ref <c>/list/proc/RemoveAll</c>): removes every copy of each
    /// item (a list argument: each of its items); the number of items removed.
    /// </summary>
    public float RemoveAll(params object[] Items)
    {
        Items ??= Dm.OneNull();
        int total = 0;
        foreach (object item in Items)
        {
            if (item is DmList source)
            {
                foreach (object x in source.CopyItems())
                    total += RemoveEvery(x);
            }
            else
            {
                total += RemoveEvery(item);
            }
        }
        return total;
    }

    /// <summary>
    /// DM <c>L.Cut(Start=1, End=0)</c> (BYOND ref <c>/list/proc/Cut</c>): removes the items from Start to End-1;
    /// End 0 (or past the end) means past the last item, Start 0 means 1 (OpenDream <c>DreamList.Cut</c>); a negative
    /// Start is "list index out of bounds". Associations of keys that no longer occur go with them.
    /// </summary>
    public void Cut(float Start = 1, float End = 0)
    {
        int start = Position(Start);
        int end = Position(End);
        if (start < 0) throw OutOfBounds(Start);
        if (start == 0) start = 1;
        if (end == 0 || end > _count + 1) end = _count + 1;
        if (end > start && start <= _count) RemoveRange(start - 1, end - start);
    }

    /// <summary>
    /// DM <c>L.Copy(Start=1, End=0)</c> (BYOND ref <c>/list/proc/Copy</c>): a new list with the items from Start to
    /// End-1 and their associated values (End 0 is past the last item, Start 0 is 1). A position outside
    /// 0..len+1 is "list index out of bounds" (OpenDream <c>DreamList.CreateCopy</c>); an alist copies only whole.
    /// </summary>
    public DmList Copy(float Start = 1, float End = 0)
    {
        int start = Position(Start);
        int end = Position(End);
        if (start == 0) start = 1;
        if (start < 0 || start > _count + 1) throw OutOfBounds(Start);
        if (end < 0 || end > _count + 1) throw OutOfBounds(End);
        if (end == 0) end = _count + 1;
        if (_isAlist && (start != 1 || end != _count + 1))
            throw new DmRuntimeException("an alist can only be copied whole");
        return CopyRange(start - 1, Math.Max(0, end - start));
    }

    /// <summary>
    /// DM <c>L.Find(Elem, Start=1, End=0)</c> (BYOND ref <c>/list/proc/Find</c>): the first position of Elem between
    /// Start and End-1 (End 0, or past the end, is past the last item; a Start below 1 is 1), or 0.
    /// </summary>
    public float Find(object Elem, float Start = 1, float End = 0)
    {
        Elem = Conv(Elem);
        int start = Position(Start);
        int end = Position(End);
        if (start < 1) start = 1;
        if (end == 0 || end > _count) end = _count + 1;
        if (start >= end || !Contains(Elem)) return 0f;
        object[] items = _items;
        for (int i = start; i < end; i++)
        {
            if (Dm.eq(items[i - 1], Elem)) return i;
        }
        return 0f;
    }

    /// <summary>
    /// DM <c>L.Insert(Index, Item1, Item2, ...)</c> (BYOND ref <c>/list/proc/Insert</c>): inserts the items before
    /// Index (0, a negative Index or len+1 appends; a list argument inserts its items with their associated values);
    /// returns the index after the inserted items. An Index past len+1 is "list index out of bounds"; inserting
    /// nothing, or into an alist, is a runtime error.
    /// </summary>
    public float Insert(float Index, params object[] Items)
    {
        if (_isAlist) throw new DmRuntimeException("cannot Insert() into an alist");
        Items ??= Dm.OneNull();
        if (Items.Length == 0) throw new DmRuntimeException("Insert(): no value given to insert");
        int index = Position(Index);
        if (index <= 0) index = _count + 1;
        if (index > _count + 1) throw OutOfBounds(Index);
        int inserted = InsertFlattened(index - 1, Items);
        return index + inserted;
    }

    /// <summary>
    /// DM <c>L.Join(Glue, Start=1, End=0)</c> (BYOND ref <c>/list/proc/Join</c>, "exactly the same as calling
    /// jointext(List,Glue,Start,End)"): the items from Start to End-1 as text, joined by Glue (null Glue joins with
    /// nothing). A negative Start or End counts from the end; End 0 is past the last item; Start 0 gives "".
    /// </summary>
    public string Join(string Glue, float Start = 1, float End = 0)
    {
        int start = Position(Start);
        int end = Position(End);
        if (end <= 0) end += _count + 1;
        if (start < 0) start += _count + 1;
        if (end > _count + 1) end = _count + 1;
        if (start <= 0 || start >= end) return "";
        var sb = new System.Text.StringBuilder();
        object[] items = _items;
        for (int i = start; i < end; i++)
        {
            if (i != start) sb.Append(Glue);
            sb.Append(DmTextRender.Plain(items[i - 1]));
        }
        return sb.ToString();
    }

    /// <summary>
    /// DM <c>L.Swap(Index1, Index2)</c> (BYOND ref <c>/list/proc/Swap</c>): swaps two items; associations stay with
    /// their keys ("If the list has associated values, they will be preserved"). Both indexes must be in 1..len; an
    /// alist cannot be swapped.
    /// </summary>
    public void Swap(float Index1, float Index2)
    {
        if (_isAlist) throw new DmRuntimeException("cannot Swap() items of an alist");
        int i = CheckIndex(Index1) - 1;
        int j = CheckIndex(Index2) - 1;
        if (i == j) return;
        PrepareWrite();
        (_items[i], _items[j]) = (_items[j], _items[i]);
        Changed();
    }

    /// <summary>
    /// DM <c>L.Splice(Start=1, End=0, Item1, Item2, ...)</c> (BYOND ref <c>/list/proc/Splice</c>): cuts Start to End-1
    /// and inserts the items there (list arguments insert their items with their associated values). Negative
    /// positions count from the end, End 0 is the end, out-of-range positions are clamped, and an End before Start
    /// swaps the two. An alist cannot be spliced.
    /// </summary>
    public void Splice(float Start = 1, float End = 0, params object[] Items)
    {
        if (_isAlist) throw new DmRuntimeException("cannot Splice() an alist");
        Items ??= Dm.OneNull();
        int len = _count;
        int start = Position(Start);
        int end = Position(End);
        if (start < 0) start += len + 1;
        if (end <= 0) end += len + 1;
        start = Math.Clamp(start, 1, len + 1);
        end = Math.Clamp(end, 1, len + 1);
        if (end < start) (start, end) = (end, start);
        if (end > start) RemoveRange(start - 1, end - start);
        InsertFlattened(start - 1, Items);
    }

    // ---- shared internals (the dynamic package and the operators use these) ----

    /// <summary>Appends one item as is (no flattening of a list argument): the primitive under Add, list(), args.</summary>
    internal void AppendItem(object item)
    {
        item = Conv(item);
        if (_isAlist) item = Normalize(item);
        if (_unique && IndexOfItem(item) >= 0) return; // already there: an engine list that holds an item once
        if (_count == _items.Length) Grow(_count + 1);
        PrepareWrite();
        _items[_count++] = item;
        IndexAdd(item);
        Changed();
    }

    /// <summary>Whether the key has an associated value, and that value.</summary>
    internal bool TryGetAssociated(object key, out object value)
    {
        if (_assoc != null && _assoc.TryGetValue(KeyOf(key), out value)) return true;
        value = null;
        return false;
    }

    /// <summary>The 0-based position of the first item equal to <paramref name="item"/> (DM equality), or -1.</summary>
    internal int IndexOfItem(object item)
    {
        item = Conv(item);
        if (_index != null && !IndexContains(item)) return -1;
        object[] items = _items;
        for (int i = 0; i < _count; i++)
        {
            if (Dm.eq(items[i], item)) return i;
        }
        return -1;
    }

    /// <summary>
    /// DM <c>A in L</c> for this list (rules 4.7): whether an item (a key) equals <paramref name="item"/>. O(1) once
    /// the occurrence index exists; a list of <see cref="IndexThreshold"/> or more items builds it on first use.
    /// </summary>
    internal bool Contains(object item)
    {
        item = Conv(item);
        if (_index == null)
        {
            if (_count < IndexThreshold) return LinearIndexOf(item) >= 0;
            BuildIndex();
        }
        return IndexContains(item);
    }

    /// <summary>Starts a snapshot of the current items for an enumerator (copy-on-write; release it once).</summary>
    internal object[] AcquireSnapshot(out int count)
    {
        _snapshots++;
        count = _count;
        return _items;
    }

    /// <summary>Ends a snapshot started by <see cref="AcquireSnapshot"/>.</summary>
    internal void ReleaseSnapshot(object[] items)
    {
        if (ReferenceEquals(items, _items) && _snapshots > 0) _snapshots--;
    }

    /// <summary>A copy of the items, for a caller that changes this list while it walks them.</summary>
    internal object[] CopyItems()
    {
        if (_count == 0) return Array.Empty<object>();
        var copy = new object[_count];
        Array.Copy(_items, copy, _count);
        return copy;
    }

    /// <summary>The items with their associated values (null when none), copied: the source of <c>Dm.pairs</c>.</summary>
    internal (object Key, object Value)[] CopyPairs()
    {
        var pairs = new (object, object)[_count];
        for (int i = 0; i < _count; i++)
        {
            object key = _items[i];
            TryGetAssociated(key, out object value);
            pairs[i] = (key, value);
        }
        return pairs;
    }

    /// <summary>Whether any item has an associated value (json_encode writes such a list as an object).</summary>
    internal bool HasAssociations => _assoc != null && _assoc.Count > 0;

    /// <summary>Whether this list is in alist mode (<c>Dm.alist</c>): DM's <c>istype(L, /alist)</c>.</summary>
    public bool IsAlist => _isAlist;

    // ---- operator support (Dm.Operators; rules 6.3, BYOND ref "list operators") ----

    /// <summary>A copy of the whole list with its associations and mode (<c>L.Copy()</c> without bounds checks).</summary>
    internal DmList CopyAll() => CopyRange(0, _count);

    /// <summary>
    /// <c>L += B</c> in place (OpenDream <c>DreamList.OperatorAppend</c>): a list B adds every one of its items, read
    /// first (so <c>L += L</c> doubles L), and its associated values; anything else (null included) is one item. An
    /// alist adds only keys it does not have yet, with B's value (OpenDream alist tests <c>AListAppend</c>).
    /// </summary>
    internal void AppendOp(object b)
    {
        if (b is DmList source) AddAllFrom(source);
        else AddItem(b);
    }

    /// <summary>
    /// <c>L -= B</c> in place: removes the last occurrence of B, or of each item of a list B (read first, so
    /// <c>L -= L</c> empties L): BYOND ref <c>/list/proc/Remove</c>; OpenDream <c>DreamList.RemoveValue</c>
    /// (<c>LastIndexOf</c>).
    /// </summary>
    internal void RemoveOp(object b)
    {
        if (b is DmList source)
        {
            foreach (object x in source.CopyItems())
                RemoveLast(x);
        }
        else
        {
            RemoveLast(b);
        }
    }

    /// <summary>
    /// <c>L |= B</c> in place: adds the items of B (or B itself) that L does not have yet, each with B's associated
    /// value; an existing key keeps its own value (BYOND ref <c>|=</c>; OpenDream <c>DreamList.OperatorCombine</c>
    /// and its test <c>AssocListCombine</c>).
    /// </summary>
    internal void CombineOp(object b)
    {
        if (b is DmList source)
        {
            foreach ((object key, bool has, object value) in source.CopyEntries())
            {
                if (Contains(key)) continue;
                AppendItem(key);
                if (has) SetAssociatedValueOfPresentKey(key, value);
            }
        }
        else if (!Contains(b))
        {
            AppendItem(b);
        }
    }

    /// <summary>
    /// <c>L &amp;= B</c> in place: keeps, in L's order, as many copies of each item as B has (the minimum of the two
    /// counts), with L's associated values; a non-list B keeps its first occurrence only (OpenDream
    /// <c>DreamList.OperatorMask</c> and its test <c>list_set_pair_preservation</c>).
    /// </summary>
    internal void MaskOp(object b)
    {
        var keep = new bool[_count];
        if (b is DmList source)
        {
            var remaining = new Dictionary<object, int>(DmKeyComparer.Instance);
            for (int i = 0; i < source._count; i++)
            {
                object key = KeyOf(Conv(source._items[i]));
                ref int c = ref CollectionsMarshal.GetValueRefOrAddDefault(remaining, key, out _);
                c++;
            }
            for (int i = 0; i < _count; i++)
            {
                if (remaining.TryGetValue(KeyOf(_items[i]), out int c) && c > 0)
                {
                    keep[i] = true;
                    remaining[KeyOf(_items[i])] = c - 1;
                }
            }
        }
        else
        {
            b = Conv(b);
            for (int i = 0; i < _count; i++)
            {
                if (Dm.eq(_items[i], b))
                {
                    keep[i] = true;
                    break;
                }
            }
        }
        RemoveUnkept(keep);
    }

    /// <summary>
    /// <c>L ^= B</c> in place. A list B: L becomes the items of L not in B (with L's values), then the items of B not
    /// in L (with B's values), keeping the same list object (BYOND ref <c>^=</c>; OpenDream <c>BitXorReference</c>).
    /// Anything else toggles one occurrence: the first copy of B is removed, or B is appended when L has none
    /// (OpenDream <c>BitXorAssignmentValues</c>, "BYOND removes the first match and leaves later duplicates").
    /// </summary>
    internal void XorAssignOp(object b)
    {
        if (b is DmList source)
        {
            DmList result = XorOp(this, source);
            Clear();
            AddAllFrom(result);
            return;
        }
        b = Conv(b);
        int first = LinearIndexOf(b);
        if (first >= 0) RemoveRange(first, 1);
        else AppendItem(b);
    }

    /// <summary>
    /// <c>A ^ B</c>: a new list of the items of A not in B (with A's values), then the items of B not in A (with B's
    /// values); a non-list B: A without any copy of B, plus B when A had none (BYOND ref <c>^</c>; OpenDream
    /// <c>BitXorValues</c>).
    /// </summary>
    internal static DmList XorOp(DmList a, object b)
    {
        var result = new DmList(0, a._isAlist);
        if (b is DmList source)
        {
            foreach ((object key, bool has, object value) in a.CopyEntries())
            {
                if (source.Contains(key)) continue;
                result.AppendItem(key);
                if (has) result.SetAssociatedValueOfPresentKey(key, value);
            }
            foreach ((object key, bool has, object value) in source.CopyEntries())
            {
                if (a.Contains(key)) continue;
                if (result._isAlist && result.Contains(key)) continue;
                result.AppendItem(key);
                if (has) result.SetAssociatedValueOfPresentKey(key, value);
            }
            return result;
        }
        bool had = a.Contains(b);
        foreach ((object key, bool has, object value) in a.CopyEntries())
        {
            if (Dm.eq(key, b)) continue;
            result.AppendItem(key);
            if (has) result.SetAssociatedValueOfPresentKey(key, value);
        }
        if (!had) result.AppendItem(b);
        return result;
    }

    /// <summary>
    /// DM <c>A ~= B</c> for lists (BYOND ref <c>~=</c>, 516 rules): the same length, equal items in order, and
    /// equal associated values (a missing value is null), shallowly.
    /// </summary>
    internal bool EquivalentTo(DmList other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other == null || _count != other._count) return false;
        for (int i = 0; i < _count; i++)
        {
            object item = _items[i];
            if (!Dm.eq(item, other._items[i])) return false;
            TryGetAssociated(item, out object mine);
            other.TryGetAssociated(item, out object theirs);
            if (!Dm.eq(mine, theirs)) return false;
        }
        return true;
    }

    // ---- private: list semantics over the primitives ----

    private object GetAssociated(object key) => TryGetAssociated(key, out object value) ? value : null;

    private void SetAssociated(object key, object value)
    {
        key = Normalize(Conv(key));
        if (!Contains(key)) AppendItem(key);
        SetAssociatedValueOfPresentKey(key, value);
    }

    // Sets the value of a key that occurs in the items (the caller made sure of that).
    private void SetAssociatedValueOfPresentKey(object key, object value)
    {
        if (_assoc == null)
        {
            if (_index == null) BuildIndex();
            _assoc = new Dictionary<object, object>(DmKeyComparer.Instance);
        }
        _assoc[KeyOf(key)] = value;
        Changed();
    }

    // One item of Add / += : an alist adds only a key it does not have yet.
    private void AddItem(object item)
    {
        if (_isAlist && Contains(item)) return;
        AppendItem(item);
    }

    // The items of a list with their associated values (Add, +=, +, Insert's list arguments carry them).
    private void AddAllFrom(DmList source)
    {
        foreach ((object key, bool has, object value) in source.CopyEntries())
        {
            if (_isAlist && Contains(key)) continue;
            AppendItem(key);
            if (has) SetAssociatedValueOfPresentKey(key, value);
        }
    }

    // Items, a "has an associated value" flag and the value, copied (safe when the source is this list).
    private (object Key, bool Has, object Value)[] CopyEntries()
    {
        var entries = new (object, bool, object)[_count];
        for (int i = 0; i < _count; i++)
        {
            object key = _items[i];
            bool has = TryGetAssociated(key, out object value);
            entries[i] = (key, has, value);
        }
        return entries;
    }

    // Insert's and Splice's items at a 0-based position, list arguments flattened with their associations;
    // returns the number of items inserted.
    private int InsertFlattened(int position, object[] items)
    {
        var flat = new List<object>(items.Length);
        List<(object Key, object Value)> carried = null;
        foreach (object item in items)
        {
            if (item is DmList source)
            {
                foreach ((object key, bool has, object value) in source.CopyEntries())
                {
                    flat.Add(key);
                    if (has) (carried ??= new()).Add((key, value));
                }
            }
            else
            {
                flat.Add(item);
            }
        }
        InsertItems(position, flat);
        if (carried != null)
        {
            foreach ((object key, object value) in carried) SetAssociatedValueOfPresentKey(key, value);
        }
        return flat.Count;
    }

    private DmList CopyRange(int start, int count)
    {
        var copy = new DmList(count, _isAlist);
        for (int i = 0; i < count; i++) copy.AppendItem(_items[start + i]);
        if (_assoc != null)
        {
            for (int i = 0; i < count; i++)
            {
                object key = _items[start + i];
                if (TryGetAssociated(key, out object value)) copy.SetAssociatedValueOfPresentKey(key, value);
            }
        }
        return copy;
    }

    // Removes the last occurrence of item; whether anything was removed. An item the occurrence index counts once is found
    // from both ends at once: its only occurrence is the last one (M3 I1d: SSarea_contents cuts an area's turfs oldest first,
    // area_contents.dm:68-71, and a backward scan of every removal made a whole-map area change O(n^2): 7.2 s in one tick on
    // MetaStation). Duplicates keep the backward scan.
    private bool RemoveLast(object item)
    {
        if (_isFilters && item is string filterName) return RemoveFiltersNamed(filterName);
        item = Conv(item);
        if (!Contains(item)) return false;
        object[] items = _items;
        if (_index != null && _index.TryGetValue(KeyOf(item), out int occurrences) && occurrences == 1)
        {
            for (int lo = 0, hi = _count - 1; lo <= hi; lo++, hi--)
            {
                if (Dm.eq(items[hi], item))
                {
                    RemoveRange(hi, 1);
                    return true;
                }
                if (Dm.eq(items[lo], item))
                {
                    RemoveRange(lo, 1);
                    return true;
                }
            }
        }
        for (int i = _count - 1; i >= 0; i--)
        {
            if (Dm.eq(items[i], item))
            {
                RemoveRange(i, 1);
                return true;
            }
        }
        return false;
    }

    // Removes every occurrence of item; the number removed.
    private int RemoveEvery(object item)
    {
        item = Conv(item);
        if (!Contains(item)) return 0;
        var keep = new bool[_count];
        int removed = 0;
        for (int i = 0; i < _count; i++)
        {
            keep[i] = !Dm.eq(_items[i], item);
            if (!keep[i]) removed++;
        }
        if (removed > 0) RemoveUnkept(keep);
        return removed;
    }

    private int LinearIndexOf(object item)
    {
        item = Conv(item);
        object[] items = _items;
        for (int i = 0; i < _count; i++)
        {
            if (Dm.eq(items[i], item)) return i;
        }
        return -1;
    }

    private void SetLength(float value)
    {
        if (float.IsNaN(value) || value < 0f) throw new DmRuntimeException("bad list length: " + value.ToString(CultureInfo.InvariantCulture));
        if (value >= int.MaxValue) throw new DmRuntimeException("list too long");
        int n = (int)value;
        if (_isAlist)
        {
            // OpenDream tests AListLen2/AListLen3: an alist's length can only be cleared.
            if (n == 0) Clear();
            else if (n != _count) throw new DmRuntimeException("an alist's len can only be set to 0");
            return;
        }
        if (n < _count)
        {
            RemoveRange(n, _count - n);
        }
        else if (n > _count)
        {
            if (n > _items.Length) Grow(n);
            PrepareWrite();
            Array.Clear(_items, _count, n - _count);
            int added = n - _count;
            _count = n;
            if (_index != null) _index[KeyOf(null)] = (_index.TryGetValue(KeyOf(null), out int c) ? c : 0) + added;
            Changed();
        }
    }

    private int CheckIndex(float index)
    {
        if (float.IsNaN(index) || index < 1f || index >= _count + 1f) throw OutOfBounds(index);
        return (int)index;
    }

    // A DM list position argument, truncated (a NaN or huge value lands outside every list).
    private static int Position(float value)
    {
        if (float.IsNaN(value)) return int.MinValue;
        if (value >= int.MaxValue) return int.MaxValue;
        if (value <= int.MinValue) return int.MinValue;
        return (int)value;
    }

    internal static DmRuntimeException OutOfBounds(float index)
        => new DmRuntimeException("list index out of bounds: " + index.ToString(CultureInfo.InvariantCulture));

    // ---- private: the primitives (every write goes through these) ----

    // Inserts items at a 0-based position (0..count).
    private void InsertItems(int position, List<object> items)
    {
        int n = items.Count;
        if (n == 0) return;
        int newCount = _count + n;
        if (newCount > _items.Length) Grow(newCount);
        PrepareWrite();
        Array.Copy(_items, position, _items, position + n, _count - position);
        for (int i = 0; i < n; i++)
        {
            object item = Conv(items[i]);
            _items[position + i] = item;
            IndexAdd(item);
        }
        _count = newCount;
        Changed();
    }

    // Removes count items from a 0-based position; the associations of keys that no longer occur go with them.
    private void RemoveRange(int position, int count)
    {
        if (count <= 0) return;
        PrepareWrite();
        for (int i = position; i < position + count; i++) ItemLeft(_items[i]);
        Array.Copy(_items, position + count, _items, position, _count - position - count);
        _count -= count;
        Array.Clear(_items, _count, count);
        Changed();
    }

    // Overwrites the item at a 0-based position.
    private void ReplaceAt(int position, object item)
    {
        item = Conv(item);
        PrepareWrite();
        object old = _items[position];
        _items[position] = item;
        IndexAdd(item);   // first, so replacing a key with itself keeps its association
        ItemLeft(old);
        Changed();
    }

    // Removes the items whose keep flag is false, keeping order.
    private void RemoveUnkept(bool[] keep)
    {
        PrepareWrite();
        int write = 0;
        for (int read = 0; read < _count; read++)
        {
            object item = _items[read];
            if (keep[read]) _items[write++] = item;
            else ItemLeft(item);
        }
        Array.Clear(_items, write, _count - write);
        _count = write;
        Changed();
    }

    private void Clear()
    {
        if (_count == 0) return;
        PrepareWrite();
        Array.Clear(_items, 0, _count);
        _count = 0;
        _index?.Clear();
        _assoc?.Clear();
        Changed();
    }

    private void Grow(int min)
    {
        int size = Math.Max(min, _items.Length == 0 ? 4 : _items.Length * 2);
        var bigger = new object[size];
        Array.Copy(_items, bigger, _count);
        _items = bigger;
        _snapshots = 0; // snapshots keep the old array; the new one is not shared
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareWrite()
    {
        if (_snapshots != 0)
        {
            _items = (object[])_items.Clone();
            _snapshots = 0;
        }
    }

    // ---- private: the occurrence index ----

    private static Dictionary<object, int> NewIndex() => new Dictionary<object, int>(DmKeyComparer.Instance);

    private void BuildIndex()
    {
        _index = NewIndex();
        for (int i = 0; i < _count; i++) IndexAdd(_items[i]);
    }

    private bool IndexContains(object item) => !IsNaN(item) && _index.ContainsKey(KeyOf(item));

    private void IndexAdd(object item)
    {
        if (_index == null || IsNaN(item)) return;
        ref int c = ref CollectionsMarshal.GetValueRefOrAddDefault(_index, KeyOf(item), out _);
        c++;
    }

    // An item left the items: count it out; when its last copy is gone, its association goes too.
    private void ItemLeft(object item)
    {
        if (_index == null || IsNaN(item)) return;
        object key = KeyOf(item);
        ref int c = ref CollectionsMarshal.GetValueRefOrNullRef(_index, key);
        if (Unsafe.IsNullRef(ref c)) return;
        if (--c > 0) return;
        _index.Remove(key);
        _assoc?.Remove(key);
    }

    // NaN in any number box (a boxed double NaN too): equal to nothing, so never counted.
    private static bool IsNaN(object item) => item is float f ? float.IsNaN(f) : item is double d && double.IsNaN(d);

    // A DM number in its canonical box (float); other values as they are. Used for keys.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object Normalize(object value)
    {
        if (value is null or float or string) return value;
        return Dm.TryGetNumber(value, out float f) ? f : value;
    }

    // The dictionary key for a DM key: null gets a sentinel (Dictionary refuses null keys).
    private static object KeyOf(object key) => key ?? NullKey;

    private static readonly object NullKey = new();

    /// <summary>
    /// DM equality as a dictionary comparer for keys: numbers by value (any box, -0 equals +0), text ordinal, null
    /// (as its sentinel) and objects by reference. Hash codes are only used for lookups, never for ordering.
    /// </summary>
    internal sealed class DmKeyComparer : IEqualityComparer<object>
    {
        public static readonly DmKeyComparer Instance = new();

        public new bool Equals(object a, object b) => Dm.eq(a, b);

        public int GetHashCode(object obj)
        {
            if (obj is string s) return string.GetHashCode(s, StringComparison.Ordinal);
            if (obj is DmResource r) return r.GetHashCode();
            if (Dm.TryGetNumber(obj, out float f)) return f == 0f ? 0 : f.GetHashCode();
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
