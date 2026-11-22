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
/// Adjacency (code/_onclick/adjacent.dm, package A: the Move chain's pull code calls it): /turf/Adjacent, /turf/ClickCross,
/// /atom/movable/Adjacent and /obj/item/Adjacent, written from the DM and its header comment.
/// </summary>
public class AdjacentTests
{
    private static bool Adjacent(atom subject, atom neighbor) => Dm.truthy(Waiting.NoSleep(subject.Adjacent(neighbor)));

    private static bool ClickCross(turf t, float dir, bool border_only, atom target = null, atom_movable mover = null)
        => Dm.truthy(Waiting.NoSleep(t.ClickCross(dir, border_only ? TRUE : FALSE, target, mover)));

    private static obj_test_movable_block_border Window(turf at, float dir)
    {
        var window = Dm.@new<obj_test_movable_block_border>(at);
        window.dir = dir;
        return window;
    }

    // ---- /atom/movable/Adjacent ----

    [Fact]
    public void AMovableIsAdjacentToItsContainerToWhatItHoldsAndToWhatIsOnAnAdjacentTurf()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var box = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_movable_thing>(box);
            var inside_item = Dm.@new<obj_test_movable_thing>(item);
            var neighbor = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var far = Dm.@new<mob_test_movable_mob>(locate(3, 3, 1));

            Assert.True(Adjacent(item, box));          // neighbor == loc (:70)
            Assert.True(Adjacent(box, item));          // neighbor.loc == src (:72)
            Assert.True(Adjacent(item, inside_item));
            Assert.False(Adjacent(item, neighbor));    // in a box, the turf test is not made: !istype(loc, /turf) (:74-75)
            Assert.True(Adjacent(box, neighbor));      // on a turf: its turf is adjacent to the neighbor's
            Assert.False(Adjacent(box, far));
            Assert.False(Adjacent(box, null));         // nothing: no turf, too far
            Assert.True(Adjacent(box, locate(1, 1, 1))); // its own turf is its loc
            Assert.True(Adjacent(box, locate(2, 2, 1)));
            Assert.False(Adjacent(box, locate(3, 1, 1)));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AThingInNullspaceIsAdjacentToNothingButWhatItHolds()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var lost = Dm.@new<obj_test_movable_thing>();
            var held = Dm.@new<obj_test_movable_thing>(lost);
            var other = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));

            Assert.True(Adjacent(lost, held));
            Assert.False(Adjacent(lost, other)); // `loc` is null, not a turf
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- /turf/Adjacent: the non-diagonal case ----

    [Fact]
    public void SameTurfIsAlwaysAdjacentEvenInsideAContainerAndFarOrOtherLevelTurfsAreNot()
    {
        var map = MovableWorld.Create(3, 3, 2);
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var in_box = Dm.@new<obj_test_movable_thing>(Dm.@new<obj_test_movable_thing>(locate(1, 1, 1)));
            var above = Dm.@new<obj_test_movable_thing>(locate(1, 1, 2));
            Window(locate(1, 1, 1), NORTH); // a window changes nothing for the same turf
            Window(locate(1, 1, 1), SOUTH);

            Assert.True(Adjacent(locate(1, 1, 1), in_box)); // T0 = get_turf(neighbor) == src (:33-34)
            Assert.True(Adjacent(locate(1, 1, 1), thing));
            Assert.False(Adjacent(locate(1, 1, 1), above)); // one z up: `z != T0.z`
            Assert.False(Adjacent(locate(1, 1, 1), locate(3, 1, 1)));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ADenseObjectThatIsNotOnTheBorderDoesNotBreakCardinalAdjacencyButABorderObjectFacingTheOtherTileDoes()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var neighbor = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            Assert.True(Adjacent(thing, neighbor));

            // border_only TRUE: a dense full-tile object in either turf is ignored (the click can reach what is in the turf)
            Dm.@new<obj_test_movable_block>(locate(1, 1, 1));
            Dm.@new<obj_test_movable_block>(locate(2, 1, 1));
            Assert.True(Adjacent(thing, neighbor));

            // a window of the neighbor's turf that faces the thing blocks (T0.ClickCross(get_dir(T0, src)) = WEST, :41)
            var window_there = Window(locate(2, 1, 1), WEST);
            Assert.False(Adjacent(thing, neighbor));
            window_there.dir = EAST; // facing away: no longer in the way
            Assert.True(Adjacent(thing, neighbor));

            // a window of the thing's own turf that faces the neighbor blocks (ClickCross(get_dir(src, T0)) = EAST)
            var window_here = Window(locate(1, 1, 1), EAST);
            Assert.False(Adjacent(thing, neighbor));
            window_here.dir = WEST;
            Assert.True(Adjacent(thing, neighbor));

            // the same window does not stop a neighbor on another side
            window_here.dir = EAST;
            var north_neighbor = Dm.@new<mob_test_movable_mob>(locate(1, 2, 1));
            Assert.True(Adjacent(thing, north_neighbor));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AFullTileWindowWithADiagonalDirectionBlocksEveryDirection()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(2, 2, 1));
            Window(locate(2, 2, 1), NORTHEAST); // `O.dir & (O.dir - 1)` is true for a diagonal dir (:119)
            foreach ((int x, int y) in new[] { (1, 2), (3, 2), (2, 1), (2, 3) })
                Assert.False(Adjacent(thing, Dm.@new<mob_test_movable_mob>(locate(x, y, 1))));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ThePassFlagsLetPassThrowAndLetPassClicksAndTheTargetItselfAreIgnoredByClickCross()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var neighbor = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var window = Window(locate(2, 1, 1), WEST);
            Assert.False(Adjacent(thing, neighbor));

            window.pass_flags_self = LETPASSTHROW; // a windowed table
            Assert.True(Adjacent(thing, neighbor));
            window.pass_flags_self = LETPASSCLICKS;
            Assert.True(Adjacent(thing, neighbor));
            window.pass_flags_self = NONE;
            Assert.False(Adjacent(thing, neighbor));

            // `O == target`: the window itself is the target of the click, so it does not block reaching it
            Assert.True(Adjacent(thing, window));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MobsOnATurfNeverBlockClickCrossBecauseOnlyObjectsAreLookedAt()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mob_here = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            mob_here.density = TRUE;
            Assert.True(ClickCross(locate(2, 2, 1), NORTH, border_only: false)); // `for(var/obj/O in src)`
            Assert.True(ClickCross(locate(2, 2, 1), NORTH, border_only: false, target: null, mover: Dm.@new<obj_test_movable_thing>(locate(1, 1, 1))));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- /turf/ClickCross ----

    [Fact]
    public void ClickCrossBlocksOnABorderObjectOnlyInTheDirectionItsDirContains()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf t = locate(2, 2, 1);
            var window = Window(t, NORTH);

            Assert.False(ClickCross(t, NORTH, true));
            Assert.True(ClickCross(t, SOUTH, true));
            Assert.True(ClickCross(t, EAST, true));
            Assert.False(ClickCross(t, NORTHEAST, true)); // `O.dir & target_dir`: the diagonal contains NORTH

            window.dir = NORTHWEST; // diagonal dir: blocks always
            Assert.False(ClickCross(t, SOUTH, true));
            Assert.False(ClickCross(t, EAST, false));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ClickCrossBlocksOnADenseNonBorderObjectOnlyWhenBorderOnlyIsFalse()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf t = locate(2, 2, 1);
            var crate = Dm.@new<obj_test_movable_block>(t);
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));

            Assert.True(ClickCross(t, NORTH, true));
            Assert.False(ClickCross(t, NORTH, false));

            // without a mover the test is the object's density: a non-dense object is skipped (:99)
            var rug = Dm.@new<obj_test_movable_thing>(locate(3, 3, 1));
            Assert.True(ClickCross(locate(3, 3, 1), NORTH, false));

            // with a mover the test is CanPass: a mover that can pass (pass_flags) is not blocked by the crate
            crate.pass_flags_self = PASSTABLE;
            Assert.False(ClickCross(t, NORTH, false, null, thing));
            thing.pass_flags = PASSTABLE;
            Assert.True(ClickCross(t, NORTH, false, null, thing));

            // O == target and O == mover are skipped
            thing.pass_flags = NONE;
            Assert.True(ClickCross(t, NORTH, false, crate));
            Assert.True(ClickCross(t, NORTH, false, null, crate));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- /turf/Adjacent: the diagonal case ----

    [Fact]
    public void ADiagonalNeighborIsAdjacentThroughAnyOneOpenCornerAndNotWhenBothAreBlocked()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var neighbor = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            Assert.True(Adjacent(thing, neighbor));

            // one corner holds a dense full-tile object (border_only FALSE at the corner T1): the other still works
            Dm.@new<obj_test_movable_block>(locate(2, 1, 1));
            Assert.True(Adjacent(thing, neighbor));

            // a dense turf is skipped as a corner (`T1.density`, :55)
            locate(2, 1, 1).density = FALSE;
            locate(1, 2, 1).density = TRUE;
            Assert.False(Adjacent(thing, neighbor)); // the open corner (2,1) still holds the block, the other is a wall
            locate(1, 2, 1).density = FALSE;

            // both corners blocked by dense objects
            Dm.@new<obj_test_movable_block>(locate(1, 2, 1));
            Assert.False(Adjacent(thing, neighbor));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ADiagonalCornerObjectThatLetsThingsPassOverItDoesNotBlockTheCorner()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var neighbor = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            Dm.@new<obj_test_movable_table>(locate(2, 1, 1)); // dense, LETPASSTHROW
            Dm.@new<obj_test_movable_block>(locate(1, 2, 1));

            Assert.True(Adjacent(thing, neighbor)); // the table corner is passable "such as tables, racks" (header comment)
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ADiagonalReachNeedsToLeaveTheNeighborsTurfAndEnterOwnTurfThroughTheCornerSides()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var neighbor = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            Dm.@new<obj_test_movable_block>(locate(2, 1, 1)); // corner (2,1) is closed: only the (1,2) corner is open

            // in_dir = get_dir(T0, src) = SOUTHWEST: d1 = SOUTH, d2 = WEST. Going WEST out of T0 = (2,2) reaches T1 = (1,2).
            var leave = Window(locate(2, 2, 1), WEST);  // a window of T0 facing the way out
            Assert.False(Adjacent(thing, neighbor));
            leave.dir = EAST;
            Assert.True(Adjacent(thing, neighbor));

            var enter = Window(locate(1, 1, 1), NORTH); // a window of src facing T1 = (1,2): `ClickCross(get_dir(src, T1))`
            Assert.False(Adjacent(thing, neighbor));
            enter.dir = SOUTH;
            Assert.True(Adjacent(thing, neighbor));

            var corner_window = Window(locate(1, 2, 1), SOUTH); // a window on the corner turf facing src: T1.ClickCross(get_dir(T1, src))
            Assert.False(Adjacent(thing, neighbor));
            corner_window.dir = NORTH;
            Assert.True(Adjacent(thing, neighbor)); // facing away from both src and T0 (EAST of T1 is T0): free
            corner_window.dir = EAST; // facing T0: T1.ClickCross(get_dir(T1, T0))
            Assert.False(Adjacent(thing, neighbor));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- /obj/item/Adjacent ----

    [Fact]
    public void AnItemInABagOnTheFloorIsAdjacentToWhatTheBagIsAdjacentToButNotTwoBagsDeep()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var bag = Dm.@new<obj_item_test_movable_item>(locate(1, 1, 1));
            var item = Dm.@new<obj_item_test_movable_item>(bag);
            var neighbor = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var far = Dm.@new<mob_test_movable_mob>(locate(3, 1, 1));

            Assert.True(Adjacent(item, bag));      // neighbor == loc
            Assert.True(Adjacent(bag, item));      // neighbor.loc == src
            Assert.True(Adjacent(item, neighbor)); // `isitem(loc)`: the bag is asked with recurse - 1 (:85-88)
            Assert.False(Adjacent(item, far));
            Assert.True(Adjacent(bag, neighbor));  // a plain item on a turf: the movable's own test

            var inner_bag = Dm.@new<obj_item_test_movable_item>(bag);
            var deep = Dm.@new<obj_item_test_movable_item>(inner_bag);
            Assert.False(Adjacent(deep, neighbor)); // two bags deep: recurse is used up (`return FALSE`)
            Assert.True(Adjacent(inner_bag, neighbor));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AnItemInANonItemContainerIsNotAdjacentToTheOutsideWorld()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var crate = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var item = Dm.@new<obj_item_test_movable_item>(crate);
            var neighbor = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));

            Assert.False(Adjacent(item, neighbor)); // not `isitem(loc)`: the movable's rule: loc is not a turf
            Assert.True(Adjacent(item, crate));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AnAreaCannotBeAskedForAdjacency()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var room = (area)locate(1, 1, 1).loc;
            var e = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(room.Adjacent(locate(1, 1, 1))));
            Assert.Contains("Call to /area/Adjacent(), unimplemented proc", e.Message);
            MovableWorld.AssertNoRuntimes(map);
        }
    }
}
