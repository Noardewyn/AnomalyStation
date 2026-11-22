using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Movable.Chain;

namespace Anomaly.Core.Tests.Tg.Wave4.Movable;

/// <summary>
/// Determinism of the Move chain (translation-rules.md 15): the same scenario twice gives the same
/// <c>World.ComputeStateHash()</c>, and the hash sees what the chain keeps: locs, the order of contents, the pulls, the
/// pending <c>active_movement</c> (the scope lists it as hashed state), the channels of important_recursive_contents and the
/// last move.
/// </summary>
public class MoveDeterminismTests
{
    // A scenario that touches every part of the chain: steps, a diagonal, a pull and its follow, forceMove into and out of
    // containers with sensitive things inside, a z-move, a qdel inside Entered, abstract_move and moveToNullspace.
    private static ulong Scenario(Action variation = null, ulong seed = 11)
    {
        var map = MovableWorld.Create(5, 3, 2, floor: typeof(turf_test_movable_floor_zpass), seed: seed);
        using (map.World.Enter())
        {
            MovableWorld.LinkZLevels();
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var crate = Dm.@new<obj_test_movable_witness>(locate(1, 1, 1));
            var box = Dm.@new<obj_test_movable_thing>(locate(3, 3, 1));
            var hearer = Dm.@new<obj_test_movable_thing>(locate(3, 3, 1));
            var doomed = Dm.@new<obj_test_movable_thing>(locate(4, 1, 1));
            var flyer = Dm.@new<obj_test_movable_thing>(locate(5, 3, 1));
            flyer.movement_type = FLYING;
            hearer.become_hearing_sensitive();
            hearer.become_area_sensitive();
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));

            Step(puller, EAST);
            Step(puller, NORTHEAST);
            Force(hearer, box);
            Step(box, EAST);
            On(locate(4, 2, 1), COMSIG_ATOM_ENTERED, "interceptor", act: (source, arrived) => { if (arrived == doomed) qdel(arrived); });
            Chain.Move(doomed, locate(4, 2, 1), NORTH);
            Waiting.NoSleep(flyer.zMove(DOWN));
            Waiting.NoSleep(flyer.zMove(UP));
            Waiting.NoSleep(crate.abstract_move(locate(2, 2, 1)));
            Force(hearer, locate(1, 3, 1));
            Waiting.NoSleep(box.moveToNullspace());
            Step(puller, WEST);
            map.World.Step(3);
            variation?.Invoke();
            Assert.Empty(map.Runtimes);
        }
        return map.World.ComputeStateHash();
    }

    [Fact]
    public void TheSameScenarioTwiceGivesTheSameStateHash()
    {
        Assert.Equal(Scenario(), Scenario());
        Assert.Equal(Scenario(seed: 5), Scenario(seed: 5));
    }

    [Fact]
    public void AnExtraStepChangesTheHash()
    {
        ulong baseline = Scenario();
        // the variation runs in the live world before it is hashed: step whatever stands at (1, 3, 1) once more
        Assert.NotEqual(baseline, Scenario(() => Step(locate(1, 3, 1).contents[1f] as atom_movable, EAST)));
    }

    [Fact]
    public void TheOrderOfTurfContentsIsPartOfTheHash()
    {
        static ulong Order(bool swapped)
        {
            var map = MovableWorld.Create(3, 1, 1, seed: 3);
            using (map.World.Enter())
            {
                var a = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
                var b = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
                var c = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
                // one gets there first: a, then b, then c; or a, then c, then b
                Step(swapped ? c : b, EAST);
                Step(swapped ? b : c, EAST);
            }
            return map.World.ComputeStateHash();
        }
        Assert.Equal(Order(false), Order(false));
        Assert.NotEqual(Order(false), Order(true));
    }

    [Fact]
    public void ThePendingActiveMovementOfARefusedMoveIsHashedState()
    {
        static ulong Pending(bool refused)
        {
            var map = MovableWorld.Create(2, 1, 1, seed: 3);
            using (map.World.Enter())
            {
                var box = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
                var item = Dm.@new<obj_test_movable_thing>(box);
                if (refused)
                    Force(box, item); // refused, `active_movement` stays set (move-chain.md 3e)
                map.Runtimes.Clear(); // the stack_trace of the refusal is a runtime of this world; clear it so only state differs
            }
            return map.World.ComputeStateHash();
        }
        Assert.Equal(Pending(true), Pending(true));
        Assert.NotEqual(Pending(false), Pending(true));
    }

    [Fact]
    public void ThePullsTheChannelsAndTheLastMoveAreHashed()
    {
        static ulong Variant(string what)
        {
            var map = MovableWorld.Create(3, 2, 1, seed: 3);
            using (map.World.Enter())
            {
                var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
                var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
                var box = Dm.@new<obj_test_movable_thing>(locate(3, 1, 1));
                var item = Dm.@new<obj_test_movable_thing>(box);
                switch (what)
                {
                    case "pull": Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE)); break;
                    case "hearing": item.become_hearing_sensitive(); break;
                    case "last_move": Step(crate, NORTH); Step(crate, SOUTH); break; // back where it was, with another last_move and dir
                }
            }
            return map.World.ComputeStateHash();
        }
        ulong none = Variant("none");
        Assert.Equal(none, Variant("none"));
        Assert.NotEqual(none, Variant("pull"));
        Assert.NotEqual(none, Variant("hearing"));
        Assert.NotEqual(none, Variant("last_move"));
    }
}
