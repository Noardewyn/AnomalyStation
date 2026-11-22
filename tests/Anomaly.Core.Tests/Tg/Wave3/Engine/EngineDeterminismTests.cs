using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Tg.Wave3.Engine;

/// <summary>
/// Determinism of the engine state (translation-rules.md 15: the same scenario twice gives the same
/// World.ComputeStateHash()), and that the hash covers what the engine keeps: where each atom is, the order of contents,
/// a turf's area and cell.
/// </summary>
public class EngineDeterminismTests
{
    // A build that touches every engine path: growth on three axes, placement, nesting, loc moves, contents edits
    // (each read afterwards), coordinate assignment, turf replacement, a new area taking a turf.
    private static ulong Build(Action<World> variation = null, ulong seed = 31)
    {
        var t = new EngineWorld(3, 2, 1, seed: seed);
        using (t.World.Enter())
        {
            Dm.world.maxz = 2;
            Dm.world.maxx = 4;
            var mob = Dm.@new<mob_test_engine_mob>(locate(1, 1, 1));
            var bag = Dm.@new<obj_test_engine_box>(mob);
            var coin = Dm.@new<obj_test_engine_box>(bag);
            var crate = Dm.@new<obj_test_engine_crate>(locate(2, 2, 2));
            Dm.append(crate.contents, coin);
            Assert.Same(crate, coin.loc);
            mob.x = 4;
            Dm.@new<turf_test_engine_wall>(locate(2, 2, 2));
            Dm.@new<area_test_engine_hall>(locate(3, 1, 1));
            bag.loc = locate(4, 1, 2);
            t.World.Step(2);
            variation?.Invoke(t.World);
            Assert.Empty(t.Runtimes);
        }
        return t.World.ComputeStateHash();
    }

    [Fact]
    public void TwoIdenticalBuildsHashAlike()
    {
        Assert.Equal(Build(), Build());
    }

    [Fact]
    public void TheHashSeesWhereAnAtomIs()
    {
        ulong baseline = Build();
        Assert.NotEqual(baseline, Build(w => Dm.@new<obj_test_engine_box>(locate(1, 1, 1))));
        ulong east = Build(w => Dm.@new<obj_test_engine_box>(locate(2, 1, 1)));
        ulong west = Build(w => Dm.@new<obj_test_engine_box>(locate(1, 1, 1)));
        Assert.NotEqual(east, west);
        Assert.NotEqual(baseline, Build(w =>
        {
            var box = Dm.@new<obj_test_engine_box>();
            locate(1, 2, 1).contents.Add(box);
            Assert.Same(locate(1, 2, 1), box.loc); // read after the edit (see ContentsEditTests' hash gap)
        }));
        Assert.NotEqual(baseline, Build(w =>
        {
            Dm.append(((area)locate(3, 1, 1).loc).contents, locate(1, 1, 2));
            Assert.IsType<area_test_engine_hall>(locate(1, 1, 2).loc);
        }));
    }

    [Fact]
    public void TheHashSeesTheOrderOfContents()
    {
        static ulong Order(bool swapped)
        {
            var t = new EngineWorld(1, 1, 1, seed: 5);
            using (t.World.Enter())
            {
                turf cell = locate(1, 1, 1);
                var a = Dm.@new<obj_test_engine_box>();
                var b = Dm.@new<obj_test_engine_box>();
                if (swapped)
                {
                    b.loc = cell;
                    a.loc = cell;
                }
                else
                {
                    a.loc = cell;
                    b.loc = cell;
                }
            }
            return t.World.ComputeStateHash();
        }
        Assert.Equal(Order(false), Order(false));
        Assert.NotEqual(Order(false), Order(true));
    }
}
