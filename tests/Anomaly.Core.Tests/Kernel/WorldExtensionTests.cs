using System.Threading;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>
/// The per-world extension slot <see cref="World.GetExtension{T}"/> (docs/ai/specs/object-model.md section 10):
/// one instance per world and type, created on first use.
/// </summary>
public class WorldExtensionTests
{
    private sealed class StateA
    {
        public readonly World Owner;
        public int Counter;
        public StateA(World owner) => Owner = owner;
    }

    private sealed class StateB
    {
        public readonly StateA Inner;
        public StateB(World w) => Inner = w.GetExtension(static x => new StateA(x));
    }

    private sealed class SelfAsking
    {
        public SelfAsking(World w) => w.GetExtension(static x => new SelfAsking(x));
    }

    private sealed class Many<T>
    {
    }

    [Fact]
    public void OneInstancePerWorldAndTypeCreatedOnFirstUse()
    {
        var w1 = new World();
        var w2 = new World();
        int created = 0;
        StateA a1 = w1.GetExtension(w => { created++; return new StateA(w); });
        StateA again = w1.GetExtension(w => { created++; return new StateA(w); });
        StateA a2 = w2.GetExtension(w => { created++; return new StateA(w); });
        Assert.Same(a1, again);
        Assert.NotSame(a1, a2);
        Assert.Same(w1, a1.Owner);
        Assert.Same(w2, a2.Owner);
        Assert.Equal(2, created);
        a1.Counter = 5;
        Assert.Equal(5, w1.GetExtension(static w => new StateA(w)).Counter);
        Assert.Equal(0, w2.GetExtension(static w => new StateA(w)).Counter);
    }

    [Fact]
    public void AFactoryMayCreateOtherExtensions()
    {
        var w = new World();
        StateB b = w.GetExtension(static x => new StateB(x));
        Assert.Same(b.Inner, w.GetExtension(static x => new StateA(x)));
    }

    [Fact]
    public void AFactoryThatAsksForItsOwnExtensionFails()
    {
        var w = new World();
        Assert.Throws<InvalidOperationException>(() => w.GetExtension(static x => new SelfAsking(x)));
    }

    [Fact]
    public void ANullFactoryResultFailsAndALaterCallRetries()
    {
        var w = new World();
        Assert.Throws<InvalidOperationException>(() => w.GetExtension<StateA>(static _ => null));
        Assert.NotNull(w.GetExtension(static x => new StateA(x)));
    }

    [Fact]
    public void AThrowingFactoryLeavesTheSlotEmpty()
    {
        var w = new World();
        Assert.Throws<FormatException>(() => w.GetExtension<StateA>(static _ => throw new FormatException()));
        Assert.NotNull(w.GetExtension(static x => new StateA(x)));
    }

    [Fact]
    public void ManyExtensionTypesGetSeparateSlots()
    {
        var w = new World();
        var all = new List<object>
        {
            w.GetExtension(static _ => new Many<int>()),
            w.GetExtension(static _ => new Many<long>()),
            w.GetExtension(static _ => new Many<string>()),
            w.GetExtension(static _ => new Many<float>()),
            w.GetExtension(static _ => new Many<double>()),
            w.GetExtension(static _ => new Many<byte>()),
            w.GetExtension(static _ => new Many<char>()),
            w.GetExtension(static _ => new Many<bool>()),
            w.GetExtension(static _ => new Many<decimal>()),
        };
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Same(all[0], w.GetExtension(static _ => new Many<int>()));
        Assert.Same(all[8], w.GetExtension(static _ => new Many<decimal>()));
    }

    [Fact]
    public void ExtensionsAreNotHashedByTheKernel()
    {
        var w1 = new World(3);
        var w2 = new World(3);
        w1.GetExtension(static x => new StateA(x)).Counter = 42;
        Assert.Equal(w1.ComputeStateHash(), w2.ComputeStateHash());
    }

    // An extension with a contributor that hashes its counter and adds nothing while the counter is 0.
    private sealed class Counted
    {
        public int Counter;

        public Counted(World w, string name) => w.AddStateHasher(name, (World _, ref StateHasher h) =>
        {
            if (Counter != 0) h.Add(Counter);
        });
    }

    private sealed class CountedA
    {
        public readonly Counted State;
        public CountedA(World w) => State = new Counted(w, "a");
    }

    private sealed class CountedB
    {
        public readonly Counted State;
        public CountedB(World w) => State = new Counted(w, "b");
    }

    [Fact]
    public void CreatingAnExtensionInItsDefaultStateDoesNotChangeTheHash()
    {
        var created = new World(3);
        var untouched = new World(3);
        Counted a = created.GetExtension(static x => new CountedA(x)).State;
        Assert.Equal(untouched.ComputeStateHash(), created.ComputeStateHash());
        a.Counter = 7;
        Assert.NotEqual(untouched.ComputeStateHash(), created.ComputeStateHash());
        a.Counter = 0;
        Assert.Equal(untouched.ComputeStateHash(), created.ComputeStateHash());
    }

    private sealed class FirstUsedOnTwoThreads
    {
        public readonly World Owner;
        public int Counter;
        public FirstUsedOnTwoThreads(World owner) => Owner = owner;
    }

    [Fact]
    public void WorldsOnParallelThreadsKeepTheirOwnAmbientWorldExtensionsAndRegistries()
    {
        // ROADMAP principle 6 / object-model.md section 10: parallel tests run separate worlds. The ambient world is
        // thread-local, extension slots and registries are per world, and the slot index of a type first used on two
        // threads at once is allocated once.
        const int Rounds = 2000;
        using var start = new Barrier(2);
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();

        void Run(ulong seed)
        {
            try
            {
                var w = new World(seed);
                var keep = new List<object>();
                start.SignalAndWait();
                for (int i = 0; i < Rounds; i++)
                {
                    using (w.Enter())
                    {
                        if (!ReferenceEquals(World.Current, w)) errors.Enqueue("the ambient world leaked between threads");
                        var state = World.Current.GetExtension(static x => new FirstUsedOnTwoThreads(x));
                        if (!ReferenceEquals(state.Owner, w)) errors.Enqueue("an extension of another world");
                        state.Counter++;
                        var obj = new object();
                        keep.Add(obj);
                        w.Objects.Register(w.AllocateId(), obj);
                    }
                }
                if (w.GetExtension(static x => new FirstUsedOnTwoThreads(x)).Counter != Rounds) errors.Enqueue("lost increments");
                if (w.Objects.Count != Rounds) errors.Enqueue("registry count " + w.Objects.Count);
                GC.KeepAlive(keep);
            }
            catch (Exception e)
            {
                errors.Enqueue(e.ToString());
            }
        }

        var t1 = new Thread(() => Run(1));
        var t2 = new Thread(() => Run(2));
        t1.Start();
        t2.Start();
        t1.Join();
        t2.Join();
        Assert.Empty(errors);
    }

    [Fact]
    public void ContributorsHashTheSameWhateverOrderTheirExtensionsWereCreatedIn()
    {
        var abFirst = new World(3);
        var baFirst = new World(3);
        abFirst.GetExtension(static x => new CountedA(x)).State.Counter = 1;
        abFirst.GetExtension(static x => new CountedB(x)).State.Counter = 2;
        baFirst.GetExtension(static x => new CountedB(x)).State.Counter = 2;
        baFirst.GetExtension(static x => new CountedA(x)).State.Counter = 1;
        Assert.Equal(abFirst.ComputeStateHash(), baFirst.ComputeStateHash());
        baFirst.GetExtension(static x => new CountedA(x)).State.Counter = 2;
        abFirst.GetExtension(static x => new CountedB(x)).State.Counter = 1;
        Assert.NotEqual(abFirst.ComputeStateHash(), baFirst.ComputeStateHash()); // the values still count per name
    }
}
