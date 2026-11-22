using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Wave3.Engine.EngineTestHelpers;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Engine;

/// <summary>
/// The engine contract of <c>loc</c>, <c>contents</c> and x/y/z (docs/ai/specs/move-chain.md E1 and E5), written from the
/// BYOND reference (loc var, contents list var, x var, locs list var, Entered proc) and from how tg's movement code
/// reads them (atoms_movable.dm doMove :1186-1225, turf.dm Enter :434).
/// </summary>
public class LocContractTests
{
    [Fact]
    public void ALocAssignmentAppendsAtTheEndAndTheOthersKeepTheirOrder()
    {
        var t = new EngineWorld(3, 3, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            turf there = locate(2, 1, 1);
            var a = Dm.@new<obj_test_engine_box>(here);
            var b = Dm.@new<obj_test_engine_box>(here);
            var c = Dm.@new<mob_test_engine_mob>(here);
            Assert.Equal(new object[] { a, b, c }, Items(here));

            b.loc = there;
            Assert.Equal(new object[] { a, c }, Items(here));
            Assert.Equal(new object[] { b }, Items(there));

            b.loc = here; // back at the END, not at its old place (arrival order; /turf/Enter iterates it, turf.dm:445)
            Assert.Equal(new object[] { a, c, b }, Items(here));
            Assert.Empty(Items(there));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AssigningTheCurrentLocAgainChangesNothing()
    {
        var t = new EngineWorld(2, 2, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var a = Dm.@new<obj_test_engine_box>(here);
            var b = Dm.@new<obj_test_engine_box>(here);
            var c = Dm.@new<obj_test_engine_box>(here);

            a.loc = here; // "if X == loc no-op" (move-chain.md E1): a keeps the first place
            Assert.Equal(new object[] { a, b, c }, Items(here));
            Assert.Same(here, a.loc);
        }
    }

    [Fact]
    public void LocAssignmentsFireNothing()
    {
        var t = new EngineWorld(3, 1, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            turf there = locate(3, 1, 1);
            var mover = Dm.@new<obj_test_engine_box>(here);
            var resident = Dm.@new<obj_test_engine_box>(there);
            var bag = Dm.@new<obj_test_engine_box>(there);
            ListenTo(here, there, (area)there.loc, mover);
            t.Trace.Clear();

            // BYOND ref, Entered proc: "Directly setting the object's loc or step_x/y vars does not result in a call to
            // Entered() or any other movement side-effects." tg's doMove relies on it (it calls Exited/Entered itself).
            mover.loc = there;
            mover.loc = bag;
            mover.loc = null;
            mover.loc = here;
            Dm.append(there.contents, mover);
            Dm.remove(there.contents, mover);
            Assert.Null(mover.loc);

            Assert.Empty(t.Trace); // no Enter, Cross, Crossed, Uncross, Uncrossed, no signal of any kind
            Assert.Empty(t.Runtimes);
            Assert.NotNull(resident);
        }
    }

    [Fact]
    public void TgQuirk_ACyclicLocIsRefusedSilentlyAndDoMoveSeesLocEqualOldloc()
    {
        var t = new EngineWorld(2, 2, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var crate = Dm.@new<obj_test_engine_box>(here);
            var box = Dm.@new<obj_test_engine_box>(crate);
            var pouch = Dm.@new<obj_test_engine_box>(box);

            // tg's doMove assigns and then tests `if(loc == oldloc)` to detect the refusal (atoms_movable.dm:1193-1197):
            // the engine must leave loc unchanged and raise nothing.
            atom oldloc = crate.loc;
            crate.loc = pouch; // pouch is inside box inside crate
            Assert.Same(oldloc, crate.loc);
            crate.loc = box;
            Assert.Same(oldloc, crate.loc);
            crate.loc = crate;
            Assert.Same(oldloc, crate.loc);

            Assert.Equal(new object[] { crate }, Items(here));
            Assert.Equal(new object[] { box }, Items(crate));
            Assert.Equal(new object[] { pouch }, Items(box));
            Assert.Empty(Items(pouch));
            Assert.Empty(t.Runtimes);

            pouch.loc = here; // the other way round is fine
            Assert.Equal(new object[] { crate, pouch }, Items(here));
        }
    }

    [Fact]
    public void NullIsNullspaceWithZeroCoordinates()
    {
        var t = new EngineWorld(3, 3, 2);
        using (t.World.Enter())
        {
            turf here = locate(2, 3, 2);
            var box = Dm.@new<obj_test_engine_box>(here);
            Assert.Equal("loc=" + EngineTrace.D(here) + " xyz=2,3,2", EngineTrace.Where(box));

            box.loc = null;
            Assert.Equal("loc=null xyz=0,0,0", EngineTrace.Where(box));
            Assert.Empty(Items(here));
            Assert.False(Dm.@in(box, here));
            Assert.Null(get_turf(box));
            Assert.Null(get_area(box));

            var fresh = Dm.@new<mob_test_engine_mob>(); // created without a location
            Assert.Equal("loc=null xyz=0,0,0", EngineTrace.Where(fresh));
        }
    }

    [Fact]
    public void AMovableInsideAnotherHasZeroCoordinatesAndGetTurfFindsTheOuterTurf()
    {
        var t = new EngineWorld(3, 3, 1);
        using (t.World.Enter())
        {
            turf here = locate(3, 2, 1);
            var mob = Dm.@new<mob_test_engine_mob>(here);
            var backpack = Dm.@new<obj_test_engine_box>(mob);
            var wallet = Dm.@new<obj_test_engine_box>(backpack);

            Assert.Equal(new[] { 3f, 2f, 1f }, new[] { mob.x, mob.y, mob.z });
            // move-chain.md E1: "x/y/z of ... a movable-in-movable = 0 (use get_turf)".
            Assert.Equal(new[] { 0f, 0f, 0f }, new[] { backpack.x, backpack.y, backpack.z });
            Assert.Equal(new[] { 0f, 0f, 0f }, new[] { wallet.x, wallet.y, wallet.z });
            Assert.Same(here, get_turf(wallet));
            Assert.Same(here.loc, get_area(wallet));
            Assert.Equal(new object[] { mob }, Items(here)); // only direct contents
        }
    }

    [Fact]
    public void ATurfIsItsCellAndItsLocIsItsArea()
    {
        var t = new EngineWorld(4, 3, 2);
        using (t.World.Enter())
        {
            turf cell = locate(4, 2, 2);
            Assert.Equal(new[] { 4f, 2f, 2f }, new[] { cell.x, cell.y, cell.z });
            Assert.IsType<area_test_engine_room>(cell.loc);
            Assert.True(Dm.@in(cell, cell.loc));

            // "Turfs cannot be moved" (BYOND ref /turf): a turf never leaves the map through loc, and only an area holds it.
            area room = (area)cell.loc;
            cell.loc = null;
            Assert.Same(room, cell.loc);
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            cell.loc = box;
            Assert.Same(room, cell.loc);
            Assert.Empty(Items(box));
            cell.loc = locate(1, 1, 1);
            Assert.Same(room, cell.loc);
            cell.x = 1; // a turf's coordinates are fixed by the grid
            cell.z = 1;
            Assert.Equal(new[] { 4f, 2f, 2f }, new[] { cell.x, cell.y, cell.z });
            Assert.Same(cell, locate(4, 2, 2));
        }
    }

    [Fact]
    public void AnAreaIsNeverInsideAnything()
    {
        var t = new EngineWorld(2, 2, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            area room = (area)here.loc;
            var box = Dm.@new<obj_test_engine_box>(here);

            // BYOND ref loc var: "For areas, this value is always null, since an area may not be contained by any other object."
            room.loc = here;
            Assert.Null(room.loc);
            room.loc = box;
            Assert.Null(room.loc);
            Dm.append(box.contents, room);
            Assert.Null(room.loc);
            Assert.Empty(Items(box));
            var hall = Dm.@new<area_test_engine_hall>();
            Dm.append(hall.contents, room);
            Assert.Null(room.loc);
            Assert.Empty(Items(hall));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirk_AMovableDirectlyInAnAreaOffTheMapHasNoTurfAndGetAreaIsNull()
    {
        var t = new EngineWorld(2, 2, 1);
        using (t.World.Enter())
        {
            // BYOND ref /area: "Areas off the map serve as rooms that objects may enter and exit." An atom in such a room
            // is not on the map: x = y = z = 0, and get_step(A, 0) is null. tg's get_area macro (turfs.dm:86) is
            // `isarea(A) ? A : get_step(A, 0)?.loc`, so it gives null here rather than the room itself.
            var room = Dm.@new<area_test_engine_hall>();
            var box = Dm.@new<obj_test_engine_box>(room);
            Assert.Same(room, box.loc);
            Assert.Equal(new object[] { box }, Items(room));
            Assert.Equal(new[] { 0f, 0f, 0f }, new[] { box.x, box.y, box.z });
            Assert.Null(get_turf(box));
            Assert.Null(get_area(box));
            Assert.Same(room, get_area(room));
            Assert.Equal(new[] { 0f, 0f, 0f }, new[] { room.x, room.y, room.z }); // an area with no turf of the map
        }
    }

    [Fact]
    public void AnAreasContentsListItsTurfsAndALoopAlsoFindsTheMovablesOnThem()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            area room = (area)locate(1, 1, 1).loc;
            var door = Dm.@new<obj_test_engine_box>(locate(2, 1, 1));
            // BYOND ref contents var: "The turfs contained in the area are in the list along with any objs or mobs directly
            // contained by those turfs." The contents list keeps the turfs only (the engine package's RULE-GAP); a loop over the
            // area walks both (dm_each_contents), so tg's `for(var/obj/machinery/door/door in src)` (areas.dm:444) finds the door.
            Assert.Equal(new object[] { locate(1, 1, 1), locate(2, 1, 1) }, Items(room));
            Assert.Equal(new object[] { door }, Dm.each<obj>(room).Cast<object>().ToArray());
            Assert.False(Dm.@in(door, room));
        }
    }

    [Fact]
    public void ContentsIsNeverNullAndIsTheSameListOnEveryRead()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            DmList first = box.contents;
            Assert.NotNull(first);
            Assert.Equal(0, first.Count);
            Assert.Same(first, box.contents);
            box.contents = null; // clears, the list object stays
            Assert.Same(first, box.contents);
            Assert.NotNull(new obj_test_engine_box().contents); // also for a prototype
        }
    }

    [Fact]
    public void LocsIsTheLocForAnyContainerAndEmptyInNullspace()
    {
        var t = new EngineWorld(2, 2, 1);
        using (t.World.Enter())
        {
            turf here = locate(2, 2, 1);
            var box = Dm.@new<obj_test_engine_box>(here);
            var inner = Dm.@new<obj_test_engine_box>(box);
            var loose = Dm.@new<obj_test_engine_box>();

            // BYOND ref locs var: default list(src.loc); "If loc is not a turf, it will be the only item in the locs list.
            // If loc is null, locs will be empty."
            Assert.Equal(new object[] { here }, Items(box.locs));
            Assert.Equal(new object[] { box }, Items(inner.locs));
            Assert.Empty(Items(loose.locs));
            Assert.NotSame(box.locs, box.locs); // read-only: a fresh list per read, editing it moves nothing
            Dm.append(box.locs, locate(1, 1, 1));
            Assert.Same(here, box.loc);
        }
    }

    [Fact]
    public void AssigningCoordinatesMovesAMovableRawToThatTurf()
    {
        var t = new EngineWorld(3, 3, 2);
        using (t.World.Enter())
        {
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            ListenTo(locate(1, 1, 1), locate(3, 1, 1), (area)locate(1, 1, 1).loc, box);
            t.Trace.Clear();

            // BYOND ref x var: "You may assign the coordinates of movable objects (mobs and objs)". It is a raw move.
            box.x = 3;
            Assert.Same(locate(3, 1, 1), box.loc);
            box.y = 2;
            Assert.Same(locate(3, 2, 1), box.loc);
            box.z = 2;
            Assert.Same(locate(3, 2, 2), box.loc);
            Assert.Equal(new object[] { box }, Items(locate(3, 2, 2)));
            Assert.Empty(Items(locate(1, 1, 1)));
            Assert.Empty(t.Trace);
        }
    }

    [Fact]
    public void AnAreasCoordinatesAreThoseOfItsLowestZYXTurf()
    {
        var t = new EngineWorld(3, 3, 2);
        using (t.World.Enter())
        {
            var hall = Dm.@new<area_test_engine_hall>();
            // BYOND ref x var: "For areas that are on the map, this is the coordinate of the turf with the lowest z, y,
            // and x coordinate (in that order) that is contained by the area."
            Dm.append(hall.contents, locate(1, 3, 2));
            Dm.append(hall.contents, locate(3, 2, 2));
            Dm.append(hall.contents, locate(2, 2, 2));
            Assert.Equal(new[] { 2f, 2f, 2f }, new[] { hall.x, hall.y, hall.z });
            Dm.append(hall.contents, locate(3, 3, 1));
            Assert.Equal(new[] { 3f, 3f, 1f }, new[] { hall.x, hall.y, hall.z });

            area room = (area)locate(1, 1, 1).loc;
            Dm.append(room.contents, locate(3, 3, 1)); // back to the room: the hall's lowest is (2, 2, 2) again
            Assert.Equal(new[] { 2f, 2f, 2f }, new[] { hall.x, hall.y, hall.z });
            Assert.Equal(new[] { 1f, 1f, 1f }, new[] { room.x, room.y, room.z });
            hall.x = 3; // an area's coordinates cannot be assigned
            Assert.Equal(2f, hall.x);
        }
    }

    [Fact]
    public void AForLoopOverATurfIteratesASnapshotOfItsContents()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            turf there = locate(2, 1, 1);
            var a = Dm.@new<obj_test_engine_box>(here);
            var b = Dm.@new<obj_test_engine_box>(here);
            var c = Dm.@new<obj_test_engine_box>(here);

            // BYOND ref for list: "you're actually looping through a copy of that list" (move-chain.md E5). Moving each item
            // away in the body still visits every item once, in order; an atom that arrives during the loop is not visited,
            // and one that leaves before its turn is still visited.
            var seen = new List<object>();
            foreach (obj o in Dm.each<obj>(here))
            {
                seen.Add(o);
                if (o == a)
                {
                    c.loc = there;
                    Dm.@new<obj_test_engine_box>(here);
                }
                o.loc = there;
            }
            Assert.Equal(new object[] { a, b, c }, seen.ToArray());
            Assert.Equal(new object[] { c, a, b }, Items(there));
            Assert.Single(Items(here));
        }
    }

    // Registers a recording listener for every movement signal on the given atoms (none may fire on a raw move).
    internal static void ListenTo(turf here, turf there, area room, atom_movable mover)
    {
        void Listen(datum target, string signal)
        {
            var l = Dm.@new<datum_test_engine_listener>();
            l.label = signal;
            l.RegisterSignal(target, signal, nameof(datum_test_engine_listener.on_signal));
        }
        foreach (turf t in new[] { here, there })
        {
            Listen(t, Defines.COMSIG_ATOM_ENTERED);
            Listen(t, Defines.COMSIG_ATOM_EXITED);
            Listen(t, Defines.COMSIG_ATOM_ABSTRACT_ENTERED);
            Listen(t, Defines.COMSIG_ATOM_EXIT);
        }
        Listen(room, Defines.COMSIG_AREA_ENTERED);
        Listen(room, Defines.COMSIG_AREA_EXITED);
        Listen(mover, Defines.COMSIG_MOVABLE_MOVED);
        Listen(mover, Defines.COMSIG_ATOM_ENTERING);
        Listen(mover, Defines.COMSIG_MOVABLE_CROSS);
    }
}
