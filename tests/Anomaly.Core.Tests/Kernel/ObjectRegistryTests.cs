using System.Runtime.CompilerServices;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>
/// The world object registry (docs/ai/specs/object-model.md section 6): id lookup, id-order iteration, weak
/// entries, strong holding for tagged objects, and its part of the state hash.
/// </summary>
public class ObjectRegistryTests
{
    private sealed class Thing
    {
        public readonly string Name;
        public Thing(string name) => Name = name;
    }

    private sealed class Hashed : IStateHashable
    {
        public float Value;
        public void HashState(ref StateHasher hasher) => hasher.Add(Value);
    }

    private static List<long> Ids(ObjectRegistry r)
    {
        var ids = new List<long>();
        r.ForEachLiveInIdOrder((id, _) => ids.Add(id));
        return ids;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterGarbage(ObjectRegistry r, long id) => r.Register(id, new Thing("garbage " + id));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string NameOf(ObjectRegistry r, long id) => r.TryGet(id, out object o) ? ((Thing)o).Name : null;

    private static void CollectAll()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Fact]
    public void RegisteredObjectsAreFoundByIdUntilUnregistered()
    {
        var w = new World();
        var a = new Thing("a");
        w.Objects.Register(5, a);
        Assert.True(w.Objects.TryGet(5, out object found));
        Assert.Same(a, found);
        Assert.False(w.Objects.TryGet(6, out found));
        Assert.Null(found);
        Assert.True(w.Objects.Unregister(5));
        Assert.False(w.Objects.TryGet(5, out _));
        Assert.False(w.Objects.Unregister(5));
        GC.KeepAlive(a);
    }

    [Fact]
    public void IterationIsInIncreasingIdOrderWhateverTheRegistrationOrder()
    {
        var w = new World();
        var keep = new List<Thing>();
        foreach (long id in new long[] { 3, 1, 7, 2, 10, 5 })
        {
            var t = new Thing("t" + id);
            keep.Add(t);
            w.Objects.Register(id, t);
        }
        Assert.Equal(new long[] { 1, 2, 3, 5, 7, 10 }, Ids(w.Objects));
        Assert.Equal(6, w.Objects.Count);
        GC.KeepAlive(keep);
    }

    [Fact]
    public void InvalidRegistrationsThrow()
    {
        var w = new World();
        var a = new Thing("a");
        Assert.Throws<ArgumentOutOfRangeException>(() => w.Objects.Register(0, a));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.Objects.Register(-3, a));
        Assert.Throws<ArgumentNullException>(() => w.Objects.Register(1, null));
        w.Objects.Register(1, a);
        Assert.Throws<InvalidOperationException>(() => w.Objects.Register(1, new Thing("b")));
    }

    [Fact]
    public void AnUnreferencedObjectDropsOutAfterACollection()
    {
        var w = new World();
        var kept = new Thing("kept");
        w.Objects.Register(1, kept);
        RegisterGarbage(w.Objects, 2);
        CollectAll();
        Assert.False(w.Objects.TryGet(2, out _));
        Assert.Equal(new long[] { 1 }, Ids(w.Objects));
        Assert.Equal(1, w.Objects.Count);
        GC.KeepAlive(kept);
    }

    [Fact]
    public void AStronglyHeldObjectSurvivesCollectionsUntilReleased()
    {
        var w = new World();
        RegisterGarbage(w.Objects, 1);
        Assert.True(w.Objects.HoldStrongly(1, true));
        Assert.True(w.Objects.IsHeldStrongly(1));
        CollectAll();
        // Read through a non-inlined helper: a local in this frame could keep the object alive (JIT liveness
        // is not guaranteed to end at the last use, so the later collection would be flaky).
        Assert.Equal("garbage 1", NameOf(w.Objects, 1));

        Assert.True(w.Objects.HoldStrongly(1, false));
        Assert.False(w.Objects.IsHeldStrongly(1));
        CollectAll();
        Assert.False(w.Objects.TryGet(1, out _));
        Assert.False(w.Objects.HoldStrongly(1, true));
        Assert.False(w.Objects.HoldStrongly(99, true));
    }

    [Fact]
    public void ReleasingACollectedEntryReportsItAsGone()
    {
        var w = new World();
        RegisterGarbage(w.Objects, 1);
        CollectAll();
        Assert.False(w.Objects.HoldStrongly(1, false)); // collected: false for either hold value
        Assert.False(w.Objects.TryGet(1, out _));
        Assert.Equal(0, w.Objects.Count);
    }

    [Fact]
    public void ObjectsRegisteredDuringAWalkWithHigherIdsAreVisitedAndRemovedOnesAreSkipped()
    {
        var w = new World();
        var keep = new List<Thing>();
        for (long id = 1; id <= 4; id++)
        {
            var t = new Thing("t" + id);
            keep.Add(t);
            w.Objects.Register(id, t);
        }
        var seen = new List<long>();
        w.Objects.ForEachLiveInIdOrder((id, _) =>
        {
            seen.Add(id);
            if (id == 1)
            {
                w.Objects.Unregister(3);
                var late = new Thing("t9");
                keep.Add(late);
                w.Objects.Register(9, late);
            }
        });
        Assert.Equal(new long[] { 1, 2, 4, 9 }, seen);
        GC.KeepAlive(keep);
    }

    [Fact]
    public void AnInsertBelowTheCursorDuringAWalkNeitherRepeatsNorSkips()
    {
        var w = new World();
        var keep = new List<Thing>();
        foreach (long id in new long[] { 10, 20, 30 })
        {
            var t = new Thing("t" + id);
            keep.Add(t);
            w.Objects.Register(id, t);
        }
        var seen = new List<long>();
        w.Objects.ForEachLiveInIdOrder((id, _) =>
        {
            seen.Add(id);
            if (id == 20)
            {
                var low = new Thing("t5");
                keep.Add(low);
                w.Objects.Register(5, low); // lands before the cursor: not visited in this walk
            }
        });
        Assert.Equal(new long[] { 10, 20, 30 }, seen);
        Assert.Equal(new long[] { 5, 10, 20, 30 }, Ids(w.Objects));
        GC.KeepAlive(keep);
    }

    [Fact]
    public void CompactionDuringAWalkKeepsTheOrder()
    {
        var w = new World();
        var keep = new List<Thing>();
        for (long id = 1; id <= 400; id++)
        {
            var t = new Thing("t" + id);
            keep.Add(t);
            w.Objects.Register(id, t);
        }
        var seen = new List<long>();
        w.Objects.ForEachLiveInIdOrder((id, _) =>
        {
            seen.Add(id);
            if (id == 100)
            {
                for (long gone = 101; gone <= 350; gone++) w.Objects.Unregister(gone); // forces a compaction
            }
        });
        var expected = Enumerable.Range(1, 100).Select(i => (long)i).Concat(Enumerable.Range(351, 50).Select(i => (long)i)).ToList();
        Assert.Equal(expected, seen);
        Assert.Equal(150, w.Objects.Count);
        GC.KeepAlive(keep);
    }

    [Fact]
    public void TheCursorFormWalksLiveObjectsInIdOrder()
    {
        var w = new World();
        var a = new Thing("a");
        var b = new Thing("b");
        w.Objects.Register(4, b);
        w.Objects.Register(2, a);
        RegisterGarbage(w.Objects, 3);
        CollectAll();
        Assert.True(w.Objects.TryGetNextLive(0, out long id, out object obj));
        Assert.Equal(2, id);
        Assert.Same(a, obj);
        Assert.True(w.Objects.TryGetNextLive(id, out id, out obj));
        Assert.Equal(4, id);
        Assert.Same(b, obj);
        Assert.False(w.Objects.TryGetNextLive(id, out id, out obj));
        Assert.Equal(0, id);
        Assert.Null(obj);
    }

    [Fact]
    public void ReRegisteringAnUnregisteredIdKeepsTheOrder()
    {
        var w = new World();
        var keep = new[] { new Thing("1"), new Thing("2"), new Thing("3") };
        w.Objects.Register(1, keep[0]);
        w.Objects.Register(2, keep[1]);
        w.Objects.Register(3, keep[2]);
        w.Objects.Unregister(2);
        w.Objects.Register(2, keep[1]);
        Assert.Equal(new long[] { 1, 2, 3 }, Ids(w.Objects));
        GC.KeepAlive(keep);
    }

    [Fact]
    public void TheStateHashCoversRegisteredObjectsTheirContentAndOnlyLiveOnes()
    {
        static (World, Hashed) Build(float value, bool withGarbage)
        {
            var w = new World(7);
            var h = new Hashed { Value = value };
            w.Objects.Register(w.AllocateId(), h);
            long garbageId = w.AllocateId(); // both worlds allocate the same ids
            if (withGarbage) RegisterGarbage(w.Objects, garbageId);
            return (w, h);
        }

        var (w1, h1) = Build(1.5f, withGarbage: true);
        var (w2, h2) = Build(1.5f, withGarbage: false);
        var (w3, h3) = Build(2.5f, withGarbage: false);
        var empty = new World(7);
        empty.AllocateId();
        empty.AllocateId();

        Assert.Equal(w1.ComputeStateHash(), w2.ComputeStateHash()); // the collected object is not hashed
        Assert.NotEqual(w2.ComputeStateHash(), w3.ComputeStateHash()); // content goes through IStateHashable
        Assert.NotEqual(w2.ComputeStateHash(), empty.ComputeStateHash()); // registration itself is hashed
        GC.KeepAlive(h1);
        GC.KeepAlive(h2);
        GC.KeepAlive(h3);
    }

    [Fact]
    public void ARegistryWhoseObjectsWereAllCollectedHashesLikeAnEmptyOne()
    {
        // Whether a lookup purged a dead entry before the hash depends on when the GC ran; the hash must not.
        var listed = new World(9);
        var purged = new World(9);
        var empty = new World(9);
        foreach (World w in new[] { listed, purged, empty }) w.AllocateId();
        RegisterGarbage(listed.Objects, 1);
        RegisterGarbage(purged.Objects, 1);
        CollectAll();
        Assert.False(purged.Objects.TryGet(1, out _)); // purges the dead entry; listed still lists it
        Assert.Equal(empty.ComputeStateHash(), listed.ComputeStateHash());
        Assert.Equal(empty.ComputeStateHash(), purged.ComputeStateHash());
    }

    [Fact]
    public void ObjectsWithoutAHashHookAreHashedByTypeName()
    {
        var w1 = new World();
        var w2 = new World();
        var a = new Thing("a");
        var b = new Hashed();
        w1.Objects.Register(1, a);
        w2.Objects.Register(1, b);
        Assert.NotEqual(w1.ComputeStateHash(), w2.ComputeStateHash());
        GC.KeepAlive(a);
        GC.KeepAlive(b);
    }
}
