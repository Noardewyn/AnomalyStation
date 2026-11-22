using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.Wave4.Integration;

/// <summary>
/// The area and z-level cases package A's tester ran against recording areas (<c>/area/Entered</c> and <c>Exited</c> stand-ins)
/// and fixture floors that answered <c>zPassIn</c> and <c>zPassOut</c>, rerun on the toy room against the real
/// <c>/area/Entered</c> and <c>Exited</c> (areas.dm:581-606) and the real <c>/turf/open/zPassIn</c> and <c>zPassOut</c>
/// (_open.dm:149-165), <c>/turf</c>'s and the wall's own.
/// </summary>
public class MoveChainAreasAndZTests
{
    private static area Carve(string name, int fromX, int toX, int fromY, int toY)
    {
        var made = Dm.@new<area_misc_testroom>();
        made.name = name;
        foreach (turf t in Dm.each<turf>(block(fromX, fromY, 1, toX, toY, 1)))
            t.change_area((area)t.loc, made);
        return made;
    }

    // ---- areas ----

    [Fact]
    public void T2_AMoverTakenOutOfAContainerOntoAnotherAreaTellsTheAreaOfTheContainersTurf()
    {
        // doMove and Move read `get_area(oldloc)`, which for a container is the area of the container's turf (turfs.dm:86)
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            room.Room.name = "room";
            area annex = Carve("annex", 4, 6, 2, 6);
            turf here = locate(3, 3, 1), there = locate(4, 3, 1);
            var box = Thing("box", here);
            var item = Thing("item", box);
            On(box, COMSIG_ATOM_EXITED, "exited_box", 2);
            On(room.Room, COMSIG_AREA_EXITED, "area_exited", 2);
            On(there, COMSIG_ATOM_ENTERED, "entered", 3);
            On(annex, COMSIG_AREA_ENTERED, "area_entered", 2);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(item, EAST));

            AssertTrace(new[]
            {
                "exited_box box item 4",
                "area_exited area:room item 4",
                "entered (4,3) item box null",
                "area_entered area:annex item area:room",
            }, Trace(room.World));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T3_AForceMoveInsideTheOldAreasExitedFiresTheOuterMovedFirstThenTheStaleExitedAreaAndEnteredOfTheTurfAndTheArea()
    {
        // move-chain.md 3g, with the real areas and the real /turf/open/Entered: the area/Exited handler relocates the mover
        // while Move is between oldarea.Exited and newloc.Entered
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            room.Room.name = "room";
            area annex = Carve("annex", 4, 5, 2, 6);
            area third = Carve("third", 6, 6, 2, 6);
            turf a = locate(3, 3, 1), b = locate(4, 3, 1), far = locate(6, 5, 1);
            var m = Thing("mover", a);
            bool done = false;
            On(a, COMSIG_ATOM_EXITED, "exited_a", 2);
            On(room.Room, COMSIG_AREA_EXITED, "area_exited_room", 2, act: _ =>
            {
                if (done)
                    return;
                done = true;
                Force(m, far);
            });
            On(annex, COMSIG_AREA_EXITED, "area_exited_annex", 2);
            On(m, COMSIG_MOVABLE_EXITED_AREA, "exited_area", 2);
            On(b, COMSIG_ATOM_EXITED, "exited_b", 2);
            On(b, COMSIG_ATOM_ENTERED, "entered_b", 3);
            On(far, COMSIG_ATOM_ENTERED, "entered_far", 3);
            On(annex, COMSIG_AREA_ENTERED, "area_entered_annex", 2);
            On(third, COMSIG_AREA_ENTERED, "area_entered_third", 2);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, EAST));

            AssertTrace(new[]
            {
                "exited_a (3,3) mover 4",
                "area_exited_room area:room mover 4",
                // the nested doMove: RESOLVE_ACTIVE_MOVEMENT flushes the outer Moved first (mover is at b)
                "moved mover (3,3) 4 0 null 1",
                "exited_b (4,3) mover 5",                       // b never saw an Entered; get_dir(b, far) = NORTHEAST
                "area_exited_annex area:annex mover 5",
                "exited_area mover area:annex 5",
                "entered_far (6,5) mover (4,3) null",
                "area_entered_third area:third mover area:annex",
                "moved mover (4,3) 0 1 null 1",
                // back in the outer area/Exited: the rest of it, for the area the mover left long ago
                "exited_area mover area:room 4",
                // back in def1: the stale newloc.Entered and newarea.Entered
                "entered_b (4,3) mover (3,3) null",
                "area_entered_annex area:annex mover area:room",
            }, Trace(room.World));
            Assert.Same(far, m.loc);
            Assert.Null(m.active_movement);
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- z-levels ----

    private static TgToyRoom ZRoom(int levels = 3)
    {
        var room = TgToyRoom.Create(levels: levels);
        using (room.World.Enter())
        {
            // generate_z_level_linkages' result (mapping.dm:230-239): every level has a level above and below it but the ends
            SSmapping.multiz_levels.len = levels;
            for (int z = 1; z <= levels; z++)
            {
                var links = Dm.list_n(LARGEST_Z_LEVEL_INDEX);
                links[(float)Z_LEVEL_UP] = z < levels ? TRUE : FALSE;
                links[(float)Z_LEVEL_DOWN] = z > 1 ? TRUE : FALSE;
                SSmapping.multiz_levels[(float)z] = links;
            }
        }
        return room;
    }

    [Fact]
    public void TheRealFloorsAndWallsAnswerTheZPassProcsAsTheirDMDoes()
    {
        var room = ZRoom();
        using (room.World.Enter())
        {
            turf floor = locate(3, 3, 2), wall = locate(1, 1, 2);
            // /turf/open (_open.dm:149-165): in only when travelling DOWN, out only when travelling UP
            AssertNumber(TRUE, floor.zPassIn(DOWN));
            AssertNumber(FALSE, floor.zPassIn(UP));
            AssertNumber(FALSE, floor.zPassIn(NORTH));
            AssertNumber(TRUE, floor.zPassOut(UP));
            AssertNumber(FALSE, floor.zPassOut(DOWN));
            AssertNumber(FALSE, floor.zPassOut(EAST));
            // /turf (turf.dm:333-338) answers FALSE for everything: a wall
            foreach (float dir in new float[] { UP, DOWN, NORTH })
            {
                AssertNumber(FALSE, wall.zPassIn(dir));
                AssertNumber(FALSE, wall.zPassOut(dir));
            }
            // an object on the floor can block each way
            var grate = Thing("grate", floor);
            Dm.combine(ref grate.obj_flags, BLOCK_Z_IN_DOWN);
            AssertNumber(FALSE, floor.zPassIn(DOWN));
            AssertNumber(TRUE, floor.zPassOut(UP));
            Dm.combine(ref grate.obj_flags, BLOCK_Z_OUT_UP);
            AssertNumber(FALSE, floor.zPassOut(UP));
            var harmless = Thing("harmless", locate(4, 3, 2));
            AssertNumber(TRUE, locate(4, 3, 2).zPassIn(DOWN));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void CanZMoveAsksStartZPassOutAndDestinationZPassInSoRealFloorsRefuseAFlyerBothWaysUnlessObstaclesAreIgnored()
    {
        var room = ZRoom();
        using (room.World.Enter())
        {
            turf start = locate(3, 3, 2), above = locate(3, 3, 3), below = locate(3, 3, 1);
            var flyer = Thing("flyer", start);
            flyer.movement_type = FLYING;

            AssertNumber(FALSE, flyer.can_z_move(UP));     // start.zPassOut(UP) is TRUE, above.zPassIn(UP) is FALSE: both must hold
            AssertNumber(FALSE, flyer.can_z_move(DOWN));   // start.zPassOut(DOWN) is FALSE
            Assert.Same(above, flyer.can_z_move(UP, null, null, ZMOVE_FLIGHT_FLAGS | ZMOVE_IGNORE_OBSTACLES));
            Assert.Same(below, flyer.can_z_move(DOWN, null, null, ZMOVE_FLIGHT_FLAGS | ZMOVE_IGNORE_OBSTACLES));
            Assert.Same(above, flyer.can_z_move(null, start, above, ZMOVE_IGNORE_OBSTACLES));   // the direction from get_dir_multiz
            AssertNumber(FALSE, flyer.can_z_move(null, start, null));   // no direction and no destination
            AssertNumber(FALSE, flyer.can_z_move(EAST));                // not UP or DOWN

            // a ground mover with gravity is not Superman under the default flight flags, wherever the floors let it
            var walker = Thing("walker", start);
            AssertNumber(FALSE, walker.can_z_move(UP, null, null, ZMOVE_IGNORE_OBSTACLES | ZMOVE_CAN_FLY_CHECKS));
            Assert.Same(above, walker.can_z_move(UP, null, null, ZMOVE_IGNORE_OBSTACLES));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void ZFallFromARealFloorIsRefusedAndLeavesTheThingWhereItIs()
    {
        // turf.dm:349-375 (D325: the whole fall): a floor over a linked level keeps what stands on it (can_z_move fails: the
        // floor's zPassOut lets nothing out downwards, or the toy room has no gravity), and the refusal clears currently_z_moving
        var room = ZRoom();
        using (room.World.Enter())
        {
            turf floor = locate(3, 3, 2);
            var thing = Thing("thing", floor);

            AssertNumber(FALSE, Waiting.NoSleep(floor.zFall(thing)));

            Assert.Same(floor, thing.loc);
            AssertNumber(FALSE, thing.currently_z_moving);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void ZMoveUpThroughRealFloorsWithObstaclesIgnoredForceMovesTheMoverAndSendsZChangedAfterMoved()
    {
        var room = ZRoom();
        using (room.World.Enter())
        {
            turf start = locate(3, 3, 1), above = locate(3, 3, 2);
            var m = Thing("mover", start);
            var item = Thing("item", m);
            var seen = new List<object>();
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3, act: _ => seen.Add(m.currently_z_moving));
            On(m, COMSIG_MOVABLE_Z_CHANGED, "z_changed", 3);
            On(item, COMSIG_MOVABLE_Z_CHANGED, "item_z_changed", 3);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Waiting.NoSleep(m.zMove(UP, null, ZMOVE_IGNORE_OBSTACLES)));

            AssertTrace(new[]
            {
                "moved mover (3,3) 0 1",
                "z_changed mover (3,3) (3,3,2) 1",       // Moved step 4; same_z_layer: both levels have plane offset 0
                "item_z_changed item (3,3) (3,3,2) 1",    // notify_contents: every movable inside is told too
            }, Trace(room.World));
            Assert.Same(above, m.loc);
            AssertNumber(CURRENTLY_Z_MOVING_GENERIC, Assert.Single(seen));   // set for the forceMove...
            AssertNumber(FALSE, m.currently_z_moving);                          // ...and cleared after it
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void ZMoveWithNoPassableTargetClearsTheZMovingFlagAndReturnsFalse()
    {
        var room = ZRoom();
        using (room.World.Enter())
        {
            var m = Thing("mover", locate(3, 3, 1));
            m.set_currently_z_moving(CURRENTLY_Z_FALLING);

            AssertNumber(FALSE, Waiting.NoSleep(m.zMove(UP, null, 0)));   // no flight checks: can_z_move says no because the real floor above refuses zPassIn(UP)

            AssertNumber(FALSE, m.currently_z_moving);
            Assert.Same(locate(3, 3, 1), m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void TryStepMultizStepsInAPlaneDirectionAndZMovesUpAndDown()
    {
        var room = ZRoom();
        using (room.World.Enter())
        {
            var m = Thing("mover", locate(3, 3, 1));

            AssertNumber(TRUE, Waiting.NoSleep(m.try_step_multiz(EAST)));
            Assert.Same(locate(4, 3, 1), m.loc);
            AssertNumber(TRUE, Waiting.NoSleep(m.try_step_multiz(UP, ZMOVE_IGNORE_OBSTACLES)));
            Assert.Same(locate(4, 3, 2), m.loc);
            AssertNumber(FALSE, Waiting.NoSleep(m.try_step_multiz(DOWN)));   // the floor under it refuses (zPassOut(DOWN) FALSE)
            Assert.Same(locate(4, 3, 2), m.loc);
            Assert.Empty(room.Runtimes);
        }
    }
}
