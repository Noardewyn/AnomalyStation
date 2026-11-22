using System.Runtime.CompilerServices;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: dynamic. The shim's per-world state for list and resource refs (docs/ai/specs/object-model.md section 3; DmRefs).

/// <summary>The per-world numbering of list refs (object-model.md section 3).</summary>
public sealed partial class DmWorldState
{
    // The ref number of each list that was ref()'d, without keeping the list alive.
    private ConditionalWeakTable<DmList, StrongBox<long>> _listRefs;

    // Ref number to list, weakly; dead entries are dropped on lookup and swept when the table doubles.
    private Dictionary<long, WeakReference<DmList>> _listsByRef;
    private int _listsSweepAt = 1024;

    // The next number of each list kind; hashed (they decide the text of every later ref).
    private long _nextListNumber = 1;
    private long _nextAlistNumber = 1;

    /// <summary>The ref number of <paramref name="list"/> in this world, allocated on its first ref.</summary>
    internal long ListRef(DmList list)
    {
        _listRefs ??= new ConditionalWeakTable<DmList, StrongBox<long>>();
        if (_listRefs.TryGetValue(list, out StrongBox<long> known)) return known.Value;
        long kind = DmRefs.ListKindOf(list);
        ref long next = ref kind == DmRefs.AlistKind ? ref _nextAlistNumber : ref _nextListNumber;
        if (next > DmRefs.NumberMask) throw new DmRuntimeException("ref(): this world has given out every list ref number");
        long number = kind | next++;
        _listRefs.Add(list, new StrongBox<long>(number));
        _listsByRef ??= new Dictionary<long, WeakReference<DmList>>();
        if (_listsByRef.Count >= _listsSweepAt) SweepListRefs();
        _listsByRef.Add(number, new WeakReference<DmList>(list));
        return number;
    }

    /// <summary>The list whose ref number is <paramref name="number"/>, or null when there is none or it was collected.</summary>
    internal DmList ListByRef(long number)
    {
        if (_listsByRef == null || !_listsByRef.TryGetValue(number, out WeakReference<DmList> weak)) return null;
        if (weak.TryGetTarget(out DmList list)) return list;
        _listsByRef.Remove(number);
        return null;
    }

    private void SweepListRefs()
    {
        var dead = new List<long>();
        foreach (KeyValuePair<long, WeakReference<DmList>> entry in _listsByRef)
        {
            if (!entry.Value.TryGetTarget(out _)) dead.Add(entry.Key);
        }
        foreach (long number in dead) _listsByRef.Remove(number);
        _listsSweepAt = Math.Max(1024, _listsByRef.Count * 2);
    }

    // The ref number of each resource path that was ref()'d, and the resource of each number (resources are values and
    // live as long as the world, so nothing is weak here).
    private Dictionary<string, long> _resourceRefs;
    private List<DmResource> _resourcesByNumber;

    /// <summary>The ref number of <paramref name="resource"/>'s path in this world, allocated on its first ref.</summary>
    internal long ResourceRef(DmResource resource)
    {
        _resourceRefs ??= new Dictionary<string, long>(StringComparer.Ordinal);
        if (_resourceRefs.TryGetValue(resource.Path, out long known)) return known;
        _resourcesByNumber ??= new List<DmResource>();
        long next = _resourcesByNumber.Count + 1;
        if (next > DmRefs.NumberMask) throw new DmRuntimeException("ref(): this world has given out every resource ref number");
        long number = DmRefs.ResourceKind | next;
        _resourceRefs.Add(resource.Path, number);
        _resourcesByNumber.Add(resource);
        return number;
    }

    /// <summary>The resource whose ref number is <paramref name="number"/>, or null when there is none.</summary>
    internal DmResource ResourceByRef(long number)
    {
        long index = (number & DmRefs.NumberMask) - 1;
        return _resourcesByNumber != null && index >= 0 && index < _resourcesByNumber.Count ? _resourcesByNumber[(int)index] : null;
    }

    // The list ref counters, for HashState: nothing while no list was ever ref()'d.
    private bool ListRefsAtDefault => _nextListNumber == 1 && _nextAlistNumber == 1 && ResourceRefCount == 0;

    private int ResourceRefCount => _resourcesByNumber?.Count ?? 0;

    private void HashListRefs(ref StateHasher h)
    {
        h.Add(_nextListNumber);
        h.Add(_nextAlistNumber);
        // The resource ref counter decides the text of every later resource ref; hashed only once one was given, so a
        // world that never ref()'d a resource hashes as before.
        if (ResourceRefCount > 0)
        {
            h.Add(ResourceRefCount);
            foreach (DmResource resource in _resourcesByNumber) h.Add(resource.Path);
        }
    }
}
