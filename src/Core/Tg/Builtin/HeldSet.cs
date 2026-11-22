namespace Anomaly.Core.Tg;

// Owner: the contract (this API and its complexity) is the M3 contracts step's and frozen (docs/ai/specs/m3-ownership.md);
// M3 P2 implements it as its first commit (the boot fix, M3 scope 1.6 and 2c) and switches atom's held list to it.

/// <summary>
/// The engine's ordered set of the atoms an atom holds (its <c>contents</c> as the engine keeps them: an area's turfs, a
/// turf's movables). BYOND keeps contents in insertion order; tg's <c>change_area</c> and the map reader's
/// <c>area.contents.Add(turf)</c> add and remove one turf at a time from areas of up to 65,025 turfs, so every operation
/// here is O(1) amortized (an index map beside the order, with removed slots compacted lazily), where M2's list made a
/// 128 x 128 boot take 9 s. Iteration and indexing give the insertion order; <see cref="Replace"/> keeps the position.
/// Not thread-safe, world state (hashed as the list it replaces is: the same order and the same atoms).
/// </summary>
/// <remarks>
/// The layout (M3 P2): the atoms sit in slots in insertion order; a removal empties its slot (a hole) and the slots are
/// compacted when the holes outnumber the atoms, so a removal is O(1) amortized. While there are holes, a position is the
/// number of atoms in the slots before it, read from a Fenwick tree over the slots' occupancy (O(log n), at most 17 steps
/// for a whole Meta level); without holes, a position is its slot. The tree is built only when the first hole appears and
/// dropped at the compaction, so a set that only grows (a turf's movables, a loading area) pays nothing for it. A small set
/// (at most <see cref="SmallLimit"/> slots, most turfs' movables) finds an atom by a scan instead of the index map.
/// </remarks>
internal sealed class HeldSet : IReadOnlyList<atom>
{
    // Compaction runs when the holes are at least this many and at least the atoms held.
    private const int CompactMinimum = 16;

    // Up to this many slots in use, atoms are found by a scan and the index map is not created.
    internal const int SmallLimit = 8;

    private atom[] _slots = Array.Empty<atom>();
    private int _used;   // slots in use, holes included
    private int _holes;  // emptied slots below _used
    private Dictionary<atom, int> _slotOf; // atom to slot; null while the set is small

    // Fenwick tree over the occupancy of _slots (1-based, length _slots.Length + 1); null while there are no holes.
    private int[] _tree;

    // Changes on every structural write, so an enumeration in progress detects them (as List<T>'s enumerator does).
    private int _version;

    /// <summary>The number of atoms held.</summary>
    public int Count => _used - _holes;

    /// <summary>The atom at <paramref name="index"/> in insertion order (0 to <see cref="Count"/> - 1).</summary>
    public atom this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _holes == 0 ? _slots[index] : _slots[SlotOfPosition(index)];
        }
    }

    /// <summary>Appends <paramref name="thing"/> (false when it is already held: a set, as an atom has one loc).</summary>
    public bool Add(atom thing)
    {
        ArgumentNullException.ThrowIfNull(thing);
        if (SlotOf(thing) >= 0) return false;
        if (_used == _slots.Length) Grow();
        int slot = _used++;
        _slots[slot] = thing;
        if (_slotOf != null) _slotOf.Add(thing, slot);
        else if (_used > SmallLimit) BuildIndex();
        if (_tree != null) TreeAdd(slot, 1);
        _version++;
        return true;
    }

    /// <summary>Removes <paramref name="thing"/>, keeping the order of the others (false when it is not held).</summary>
    public bool Remove(atom thing)
    {
        int slot = SlotOf(thing);
        if (slot < 0) return false;
        _slotOf?.Remove(thing);
        _version++;
        if (slot == _used - 1)
        {
            // The last slot: no hole, and the holes just below it are trimmed with it.
            _slots[slot] = null;
            _used--;
            if (_tree != null) TreeAdd(slot, -1);
            while (_used > 0 && _slots[_used - 1] == null)
            {
                _used--;
                _holes--;
            }
            if (_holes == 0) _tree = null;
            return true;
        }
        if (_tree == null) BuildTree(); // over the occupancy before this removal
        _slots[slot] = null;
        TreeAdd(slot, -1);
        _holes++;
        if (_holes >= CompactMinimum && _holes >= Count) Compact();
        return true;
    }

    /// <summary>Whether <paramref name="thing"/> is held, O(1).</summary>
    public bool Contains(atom thing) => SlotOf(thing) >= 0;

    /// <summary>The position of <paramref name="thing"/> in insertion order, or -1, O(1) amortized.</summary>
    public int IndexOf(atom thing)
    {
        int slot = SlotOf(thing);
        if (slot < 0) return -1;
        return _holes == 0 ? slot : TreePrefix(slot);
    }

    /// <summary>Puts <paramref name="newThing"/> where <paramref name="oldThing"/> was (BYOND's turf replacement in an area keeps the slot).</summary>
    public void Replace(atom oldThing, atom newThing)
    {
        ArgumentNullException.ThrowIfNull(newThing);
        if (ReferenceEquals(oldThing, newThing)) return;
        int slot = SlotOf(oldThing);
        if (slot < 0) throw new InvalidOperationException("the replaced atom is not held");
        if (SlotOf(newThing) >= 0) throw new InvalidOperationException("the replacement is already held");
        _slots[slot] = newThing;
        if (_slotOf != null)
        {
            _slotOf.Remove(oldThing);
            _slotOf.Add(newThing, slot);
        }
        _version++;
    }

    /// <summary>Drops every atom.</summary>
    public void Clear()
    {
        Array.Clear(_slots, 0, _used);
        _used = 0;
        _holes = 0;
        _tree = null;
        _slotOf = null;
        _version++;
    }

    public IEnumerator<atom> GetEnumerator()
    {
        int version = _version;
        for (int slot = 0; slot < _used; slot++)
        {
            if (version != _version) throw new InvalidOperationException("the held set changed during its enumeration");
            atom thing = _slots[slot];
            if (thing != null) yield return thing;
        }
        if (version != _version) throw new InvalidOperationException("the held set changed during its enumeration");
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- the layout ----

    // The slot of `thing`, or -1.
    private int SlotOf(atom thing)
    {
        if (thing == null) return -1;
        if (_slotOf != null) return _slotOf.TryGetValue(thing, out int slot) ? slot : -1;
        atom[] slots = _slots;
        for (int i = 0; i < _used; i++)
        {
            if (ReferenceEquals(slots[i], thing)) return i;
        }
        return -1;
    }

    private void BuildIndex()
    {
        var index = new Dictionary<atom, int>(_used * 2, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < _used; i++)
        {
            if (_slots[i] != null) index.Add(_slots[i], i);
        }
        _slotOf = index;
    }

    private void Grow()
    {
        int size = _slots.Length == 0 ? 4 : _slots.Length * 2;
        Array.Resize(ref _slots, size);
        if (_tree != null) BuildTree();
    }

    // Moves the atoms down over the holes, in order, and drops the tree.
    private void Compact()
    {
        int write = 0;
        for (int read = 0; read < _used; read++)
        {
            atom thing = _slots[read];
            if (thing == null) continue;
            if (write != read)
            {
                _slots[write] = thing;
                if (_slotOf != null) _slotOf[thing] = write;
            }
            write++;
        }
        Array.Clear(_slots, write, _used - write);
        _used = write;
        _holes = 0;
        _tree = null;
    }

    // Builds the Fenwick tree of the occupancy of every slot (O(n)).
    private void BuildTree()
    {
        int n = _slots.Length;
        var tree = new int[n + 1];
        for (int i = 1; i <= n; i++)
        {
            if (i <= _used && _slots[i - 1] != null) tree[i] += 1;
            int parent = i + (i & -i);
            if (parent <= n) tree[parent] += tree[i];
        }
        _tree = tree;
    }

    private void TreeAdd(int slot, int delta)
    {
        int[] tree = _tree;
        for (int i = slot + 1; i < tree.Length; i += i & -i) tree[i] += delta;
    }

    // The number of atoms in the slots below `slot`.
    private int TreePrefix(int slot)
    {
        int[] tree = _tree;
        int sum = 0;
        for (int i = slot; i > 0; i -= i & -i) sum += tree[i];
        return sum;
    }

    // The slot of the atom at `position` (0-based) while there are holes: the Fenwick tree's descent.
    private int SlotOfPosition(int position)
    {
        int[] tree = _tree;
        int remaining = position + 1;
        int at = 0;
        int step = 1;
        while (step * 2 < tree.Length) step *= 2;
        for (; step > 0; step >>= 1)
        {
            int next = at + step;
            if (next < tree.Length && tree[next] < remaining)
            {
                at = next;
                remaining -= tree[next];
            }
        }
        return at; // the 1-based index at + 1 is the slot at, 0-based
    }
}
