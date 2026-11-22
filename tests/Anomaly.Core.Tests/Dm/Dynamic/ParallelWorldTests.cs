using System.Threading.Tasks;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// The shim's process-wide caches (the type registry, invokers, factories, prototypes, var accessors) are built
/// lazily and are immutable once built, so worlds in parallel tests share them without seeing each other
/// (object-model.md section 10; ROADMAP principle 6). The threads here stand in for xunit's parallel test classes.
/// </summary>
public class ParallelWorldTests
{
    [Fact]
    public void WorldsOnParallelThreadsBuildTheSameObjectsAndHashes()
    {
        const int Worlds = 8;
        var hashes = new ulong[Worlds];
        var firstIds = new long[Worlds];
        var orders = new Type[Worlds][];
        Parallel.For(0, Worlds, i =>
        {
            orders[i] = DmTypeRegistry.Discover().Subtypes(typeof(fx_atom), includeRoot: true).ToArray();
            var w = new World(11);
            var keep = new List<object>();
            using (w.Enter())
            {
                fx_atom room = Dm.@new<fx_atom>();
                keep.Add(room);
                keep.Add(Dm.@new<fx_stack>(room));
                fx_point point = Dm.@new<fx_point>(named("_z", 4));
                keep.Add(point);
                Waiting.NoSleep(Dm.Call(point, "initialize_location", 1, 2));
                Assert.Equal("stack", initial(typeof(fx_sheet), (fx_item p) => p.icon_state));
                firstIds[i] = room.dm_id;
            }
            hashes[i] = w.ComputeStateHash();
            GC.KeepAlive(keep);
        });
        Assert.All(firstIds, id => Assert.Equal(1, id));
        Assert.All(hashes, h => Assert.Equal(hashes[0], h));
        Assert.All(orders, order => Assert.Equal(DmTypes.Subtypes(typeof(fx_atom), includeRoot: true), order));
    }

    [Fact]
    public void ThreadsThatMeetTheShimsCachesEmptyAtOnceAllGetTheSameAnswers()
    {
        // object-model.md sections 5, 10 and 12: the factory, prototype, invoker and accessor caches are built on first
        // use; threads that all ask for the same new keys at the same moment (a barrier releases them together) must
        // each get a working, equivalent entry and see nothing of the other threads' worlds.
        const int Threads = 12;
        using var start = new Barrier(Threads);
        var answers = new string[Threads];
        var failures = new Exception[Threads];
        Thread[] threads = Enumerable.Range(0, Threads).Select(i => new Thread(() =>
        {
            try
            {
                start.SignalAndWait();
                var w = new World(5);
                using (w.Enter())
                {
                    fx_parallel made = Dm.@new<fx_parallel>(named("seed", i));
                    object twice = Waiting.NoSleep(Dm.Call(made, "twice_seed", named("plus", 1)));
                    answers[i] = string.Join(" ", made.dm_id, twice, get_var(made, "seed"), initial(made, "desc"),
                        initial(typeof(fx_parallel), (fx_datum p) => p.desc), hascall(made, "twice_seed"), typesof(typeof(fx_parallel)).Count);
                }
            }
            catch (Exception e)
            {
                failures[i] = e;
            }
        })).ToArray();
        foreach (Thread thread in threads) thread.Start();
        foreach (Thread thread in threads) thread.Join();
        Assert.All(failures, Assert.Null);
        for (int i = 0; i < Threads; i++)
            Assert.Equal(string.Join(" ", 1, 2f * i + 1, i, "parallel", "parallel", true, 1), answers[i]);
    }
}
