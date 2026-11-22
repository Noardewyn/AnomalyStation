namespace Anomaly.Core.Kernel;

/// <summary>
/// An object whose content takes part in <see cref="World.ComputeStateHash"/>. The registry hashes every live
/// registered object in id order as its id followed by <see cref="HashState"/> (or by its C# type name when the
/// object does not implement this interface). The Tg layer implements it on <c>datum</c> through the shim's
/// field hasher (docs/ai/specs/object-model.md section 8); the Kernel only defines the hook.
/// </summary>
public interface IStateHashable
{
    /// <summary>Adds the object's simulation state to <paramref name="hasher"/>, deterministically.</summary>
    void HashState(ref StateHasher hasher);
}

/// <summary>
/// The world registry of objects created by <c>Dm.@new</c> (docs/ai/specs/object-model.md section 6), reached
/// through <see cref="World.Objects"/>: id to object, iterated in id order (creation order), with weak entries
/// so that an object nothing references any more drops out when the C# GC collects it, as a DM datum is
/// deleted when its reference count drops to zero. A tagged object is held strongly (BYOND: a tag is an
/// implicit reference). Ids are the <see cref="World.AllocateId"/> values: positive and never reused.
/// <para>
/// Determinism: an unreachable object stays visible until the GC collects it, so iteration and lookups may
/// still see it; <see cref="HashInto"/> forces a full collection first so two runs hash the same set. Atoms
/// stay reachable through their location's contents, so atom iteration does not depend on GC timing.
/// </para>
/// <para>Single-threaded like the rest of a world. Registration and removal during an iteration are allowed.</para>
/// </summary>
public sealed class ObjectRegistry
{
    private struct Entry
    {
        public WeakReference<object> Weak;
        public object Strong; // the object itself while it is held strongly (tagged), else null
    }

    private readonly Dictionary<long, Entry> _entries = new();

    // Every registered id in increasing order, plus ids that were unregistered or collected since the last
    // compaction ("stale" ids, skipped because _entries no longer has them).
    private readonly List<long> _order = new();
    private int _stale;

    // Bumped whenever _order changes in a way other than an append (an insert or a compaction), so that a
    // running iteration re-finds its position by id.
    private int _version;

    // The highest id ever registered (the watermark of HighestId).
    private long _highestId;

    internal ObjectRegistry()
    {
    }

    /// <summary>
    /// Registers <paramref name="obj"/> under <paramref name="id"/> (<c>Dm.@new</c>, object-model.md section 4,
    /// step 3). The entry is weak until <see cref="HoldStrongly"/> pins it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The id is not positive.</exception>
    /// <exception cref="InvalidOperationException">The id is registered already (ids are never reused).</exception>
    public void Register(long id, object obj)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id), id, "Object ids are positive (World.AllocateId).");
        ArgumentNullException.ThrowIfNull(obj);
        if (_entries.ContainsKey(id)) throw new InvalidOperationException($"Object id {id} is registered already.");
        _entries.Add(id, new Entry { Weak = new WeakReference<object>(obj) });
        if (id > _highestId) _highestId = id;

        int count = _order.Count;
        if (count == 0 || id > _order[count - 1])
        {
            _order.Add(id);
            return;
        }
        int at = _order.BinarySearch(id);
        if (at >= 0)
        {
            _stale--; // the id is still listed from an earlier registration that was removed: reuse its place
            return;
        }
        _order.Insert(~at, id);
        _version++;
    }

    /// <summary>
    /// Removes the entry (tg's translated <c>qdel</c>, object-model.md section 7) and then raises
    /// <see cref="Unregistered"/>. Returns false, and raises nothing, when the id is not registered.
    /// </summary>
    public bool Unregister(long id)
    {
        if (!_entries.Remove(id)) return false;
        if (_tagOf != null && _tagOf.Remove(id, out string tag)) Untag(id, tag);
        MarkStale();
        Unregistered?.Invoke(id);
        return true;
    }

    // The tag index (M3 I1b): the ids under each tag, so that locate(tag) does not scan every live object (a weakref of a mob,
    // whose REF is its tag, resolved by a scan of Meta's objects took seconds the first time and tens of milliseconds
    // after). Derived from the objects' tags, which are hashed with the objects, so it is not hashed itself.
    private Dictionary<string, SortedSet<long>> _byTag;
    private Dictionary<long, string> _tagOf;

    /// <summary>
    /// Records that the object <paramref name="id"/> now has the tag <paramref name="tag"/> (null or empty: none), for
    /// <see cref="FindTagged"/>. The datum's tag setter calls it, and an object's id assignment for a tag it was
    /// constructed with; the id need not be registered yet.
    /// </summary>
    public void SetTag(long id, string tag)
    {
        if (_tagOf != null && _tagOf.Remove(id, out string old)) Untag(id, old);
        if (string.IsNullOrEmpty(tag)) return;
        _tagOf ??= new Dictionary<long, string>();
        _byTag ??= new Dictionary<string, SortedSet<long>>(StringComparer.Ordinal);
        _tagOf[id] = tag;
        if (!_byTag.TryGetValue(tag, out SortedSet<long> ids)) _byTag[tag] = ids = new SortedSet<long>();
        ids.Add(id);
    }

    /// <summary>
    /// DM <c>locate(tag)</c>: the live registered object with the lowest id whose tag is <paramref name="tag"/>, or null:
    /// what a scan of the live objects in id order finds, in O(log n).
    /// </summary>
    public object FindTagged(string tag)
    {
        if (_byTag == null || string.IsNullOrEmpty(tag) || !_byTag.TryGetValue(tag, out SortedSet<long> ids)) return null;
        foreach (long id in ids)
        {
            if (TryGet(id, out object obj)) return obj;
        }
        return null;
    }

    /// <summary>
    /// True once an object that does not keep the tag index (the shim's <c>IDmTagIndexed</c>) was registered: then
    /// <c>locate(tag)</c> scans the live objects as before the index (the shim's own fixtures; never tg's objects).
    /// </summary>
    public bool TagIndexIncomplete { get; set; }

    private void Untag(long id, string tag)
    {
        if (_byTag != null && _byTag.TryGetValue(tag, out SortedSet<long> ids) && ids.Remove(id) && ids.Count == 0)
            _byTag.Remove(tag);
    }

    /// <summary>
    /// The highest id ever registered in this world, 0 before the first registration; it never decreases. Ids are
    /// allocated in increasing order and registered at once (<c>Dm.@new</c>), so the objects created since a host last
    /// read this value are exactly the live ones with a higher id (<see cref="TryGetNextLive"/> from the old value): the
    /// replication watermark for created objects (object-model.md section 6). Not hashed: it follows from the registered
    /// ids, and <see cref="World.ComputeStateHash"/> already hashes the id counter.
    /// </summary>
    public long HighestId => _highestId;

    /// <summary>
    /// Raised by <see cref="Unregister"/> after it removed an entry, with the removed id: the replication hint for a
    /// deleted object (object-model.md section 6; the shim's <c>PresentationSinks.Replication</c> wires it to its sink).
    /// Only explicit removals raise it (qdel's paths, a replaced turf), never an entry the GC collected, whose timing is
    /// not deterministic. A handler must only record the id, or queue a scheduler entry for later in the tick: it runs
    /// inside the world, in the middle of qdel (object-model.md section 6.1). No handler costs one null check.
    /// </summary>
    public event Action<long> Unregistered;

    /// <summary>
    /// Looks an object up by id (<c>locate(ref)</c>). False when the id was never registered, was unregistered,
    /// or its object has been collected.
    /// </summary>
    public bool TryGet(long id, out object obj)
    {
        if (_entries.TryGetValue(id, out Entry entry))
        {
            if (entry.Strong != null)
            {
                obj = entry.Strong;
                return true;
            }
            if (entry.Weak.TryGetTarget(out obj)) return true;
            _entries.Remove(id); // collected
            MarkStale();
        }
        obj = null;
        return false;
    }

    /// <summary>
    /// Holds the object strongly (<paramref name="hold"/> true: the <c>tag</c> setter gave it a tag) or lets the
    /// entry become weak again (false: the tag was cleared). Returns false when the id is not registered or its
    /// object has been collected (whichever <paramref name="hold"/> is).
    /// </summary>
    public bool HoldStrongly(long id, bool hold)
    {
        if (!_entries.TryGetValue(id, out Entry entry)) return false;
        if (entry.Strong == null)
        {
            if (!entry.Weak.TryGetTarget(out object target))
            {
                _entries.Remove(id); // collected
                MarkStale();
                return false;
            }
            if (!hold) return true; // weak already
            entry.Strong = target;
        }
        else if (!hold)
        {
            entry.Strong = null;
        }
        else
        {
            return true; // strong already
        }
        _entries[id] = entry;
        return true;
    }

    /// <summary>Whether the entry is currently held strongly (diagnostics and tests).</summary>
    public bool IsHeldStrongly(long id) => _entries.TryGetValue(id, out Entry entry) && entry.Strong != null;

    /// <summary>
    /// Calls <paramref name="action"/> for every live registered object in increasing id order (state hashing,
    /// <c>for (... in world)</c>, <c>locate(T)</c> in the world). Objects registered during the walk with a
    /// higher id than the current one are visited too; objects removed before they are reached are skipped.
    /// </summary>
    public void ForEachLiveInIdOrder(Action<long, object> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        long cursor = 0;
        while (TryGetNextLive(cursor, out long id, out object obj))
        {
            action(id, obj);
            cursor = id;
        }
    }

    /// <summary>
    /// The live registered object with the smallest id greater than <paramref name="afterId"/>: the cursor form
    /// of <see cref="ForEachLiveInIdOrder"/> for enumerators (<c>Dm.each(world)</c>). Start with 0.
    /// </summary>
    public bool TryGetNextLive(long afterId, out long id, out object obj)
    {
        int i = UpperBound(afterId);
        while (i < _order.Count)
        {
            long candidate = _order[i];
            int version = _version;
            if (TryGet(candidate, out obj))
            {
                id = candidate;
                return true;
            }
            i = version == _version ? i + 1 : UpperBound(candidate);
        }
        id = 0;
        obj = null;
        return false;
    }

    /// <summary>The number of live registered objects (scans and purges collected entries: O(n)).</summary>
    public int Count
    {
        get
        {
            List<long> dead = null;
            foreach (KeyValuePair<long, Entry> pair in _entries)
            {
                if (pair.Value.Strong == null && !pair.Value.Weak.TryGetTarget(out _)) (dead ??= new List<long>()).Add(pair.Key);
            }
            if (dead != null)
            {
                foreach (long id in dead)
                {
                    _entries.Remove(id);
                    MarkStale();
                }
            }
            return _entries.Count;
        }
    }

    /// <summary>
    /// Adds the registry to a state hash (called by <see cref="World.ComputeStateHash"/>). A registry without live
    /// objects adds nothing, whether or not it still lists entries whose objects were collected (so worlds without
    /// objects hash as before the registry existed, and the result never depends on when the GC ran or whether a
    /// lookup purged a dead entry). Otherwise: a full blocking garbage collection first (so weak entries reflect
    /// reachability and two runs hash the same set), then for each live object in id order its id and its content
    /// (<see cref="IStateHashable.HashState"/>, else its C# type name), then the number of objects hashed.
    /// </summary>
    public void HashInto(ref StateHasher hasher)
    {
        if (_entries.Count == 0) return;
        // One full blocking collection settles every weak entry: a short weak reference is cleared in the collection that finds its
        // target unreachable, before any finalizer runs (a second collection after the finalizers only freed memory, at a stop of every
        // thread of the process).
        GC.Collect();
        long hashed = 0;
        long cursor = 0;
        while (TryGetNextLive(cursor, out long id, out object obj))
        {
            hasher.Add(id);
            if (obj is IStateHashable hashable) hashable.HashState(ref hasher);
            else hasher.Add(obj.GetType().FullName);
            hashed++;
            cursor = id;
        }
        if (hashed != 0) hasher.Add(hashed); // only dead entries: nothing added, as for an empty registry
    }

    private void MarkStale()
    {
        _stale++;
        if (_stale > 64 && _stale > _order.Count / 2) Compact();
    }

    private void Compact()
    {
        int write = 0;
        for (int read = 0; read < _order.Count; read++)
        {
            long id = _order[read];
            if (_entries.ContainsKey(id)) _order[write++] = id;
        }
        _order.RemoveRange(write, _order.Count - write);
        _stale = 0;
        _version++;
    }

    // Index of the first listed id greater than id.
    private int UpperBound(long id)
    {
        int lo = 0, hi = _order.Count;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (_order[mid] <= id) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
