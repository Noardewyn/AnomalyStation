using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.M1Seams;

/// <summary>
/// The registry's replication seams (M1 contracts step; docs/ai/specs/object-model.md section 6): the id watermark that tells
/// a host which objects were created since it last looked, and the event that tells it which were removed.
/// </summary>
public class RegistryWatermarkTests
{
    private sealed class Thing
    {
    }

    [Fact]
    public void TheWatermarkIsTheHighestRegisteredIdAndNeverDecreases()
    {
        var world = new World();
        Assert.Equal(0, world.Objects.HighestId);
        long a = world.AllocateId();
        long b = world.AllocateId();
        world.Objects.Register(a, new Thing());
        Assert.Equal(a, world.Objects.HighestId);
        var kept = new Thing();
        world.Objects.Register(b, kept);
        Assert.Equal(b, world.Objects.HighestId);
        world.Objects.Unregister(b);
        Assert.Equal(b, world.Objects.HighestId);
        GC.KeepAlive(kept);
    }

    [Fact]
    public void TheObjectsAboveAWatermarkAreTheOnesCreatedSince()
    {
        var world = new World();
        var early = new Thing();
        world.Objects.Register(world.AllocateId(), early);
        long mark = world.Objects.HighestId;
        var created = new List<object>();
        for (int i = 0; i < 3; i++)
        {
            var thing = new Thing();
            created.Add(thing);
            world.Objects.Register(world.AllocateId(), thing);
        }
        var found = new List<object>();
        for (long cursor = mark; world.Objects.TryGetNextLive(cursor, out long id, out object obj); cursor = id)
            found.Add(obj);
        Assert.Equal(created, found);
        GC.KeepAlive(early);
    }

    [Fact]
    public void UnregisterRaisesTheEventOnceWithTheIdAfterTheEntryIsGone()
    {
        var world = new World();
        var thing = new Thing();
        long id = world.AllocateId();
        world.Objects.Register(id, thing);
        var seen = new List<(long Id, bool StillThere)>();
        world.Objects.Unregistered += removed => seen.Add((removed, world.Objects.TryGet(removed, out _)));

        Assert.True(world.Objects.Unregister(id));
        Assert.False(world.Objects.Unregister(id)); // not registered any more: no event
        Assert.False(world.Objects.Unregister(12345)); // never registered: no event

        Assert.Equal(new[] { (id, false) }, seen);
        GC.KeepAlive(thing);
    }

    [Fact]
    public void AnEntryTheGcCollectedRaisesNothing()
    {
        var world = new World();
        long id = world.AllocateId();
        RegisterGarbage(world, id);
        int raised = 0;
        world.Objects.Unregistered += _ => raised++;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        world.Objects.TryGet(id, out _); // a lookup purges the entry once its object is collected
        _ = world.Objects.Count;          // so does counting
        Assert.Equal(0, raised);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RegisterGarbage(World world, long id) => world.Objects.Register(id, new Thing());

    [Fact]
    public void NeitherSeamChangesTheStateHash()
    {
        ulong Hash(bool hooked)
        {
            var world = new World(7);
            var things = new List<Thing>();
            if (hooked) world.Objects.Unregistered += _ => { };
            for (int i = 0; i < 4; i++)
            {
                var thing = new Thing();
                things.Add(thing);
                world.Objects.Register(world.AllocateId(), thing);
            }
            _ = world.Objects.HighestId;
            world.Objects.Unregister(2);
            ulong hash = world.ComputeStateHash();
            GC.KeepAlive(things);
            return hash;
        }
        Assert.Equal(Hash(false), Hash(true));
    }
}
