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
/// The goldens T1 to T3 of docs/ai/specs/move-chain.md section 11 on the toy room, with nothing but tg's real types: the real
/// <c>/turf/Enter</c> (CanPass, the Cross of every occupant), <c>/atom/Exit</c>, the real <c>/turf/open/Entered</c> and
/// <c>Exited</c>, the real <c>/area/Entered</c> and <c>Exited</c>, the real <c>step</c>, <c>Move</c>, <c>doMove</c> and
/// <c>forceMove</c>. The expected order is written from move-chain.md section 3 and from the DM of atoms_movable.dm,
/// turf.dm, _atom.dm and areas.dm, not from the C#.
/// </summary>
public class MoveChainT1ToT3Tests
{
    // ---- T1: step() in one area ----

    [Fact]
    public void T1_ACardinalStepInOneAreaRunsTheRealEnterAndSendsEverySignalInTgsOrder()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var rug = Thing("rug", b);   // not dense: it is crossed, it does not block
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            On(m, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir", 2);
            On(m, COMSIG_ATOM_DIR_CHANGE, "dir", 2);
            On(m, COMSIG_ATOM_POST_DIR_CHANGE, "post_dir", 2);
            On(a, COMSIG_ATOM_EXIT, "exit", 2);
            On(b, COMSIG_ATOM_TRIED_PASS, "tried_pass_turf", 2);
            On(rug, COMSIG_MOVABLE_CROSS, "cross", 1);
            On(m, COMSIG_MOVABLE_CROSS_OVER, "cross_over", 1);
            On(rug, COMSIG_ATOM_TRIED_PASS, "tried_pass_rug", 2);
            On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
            On(a, COMSIG_ATOM_EXITED, "exited", 2);
            On(m, COMSIG_ATOM_EXITING, "exiting", 2);
            On(b, COMSIG_ATOM_ENTERED, "entered", 3);
            On(m, COMSIG_ATOM_ENTERING, "entering", 3);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            On(a, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited", 2);
            On(b, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered", 3);
            var area_here = (area)a.loc;
            On(area_here, COMSIG_AREA_EXITED, "area_exited", 2);
            On(area_here, COMSIG_AREA_ENTERED, "area_entered", 2);
            On(m, COMSIG_MOVABLE_EXITED_AREA, "exited_area", 2);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, EAST));

            AssertTrace(new[]
            {
                "attempted mover (4,3) 4",           // before anything, with the direction asked for
                "pre_dir mover 2 4",                 // the dir changes BEFORE Exit and Enter: a failed move still turns the mover
                "dir mover 2 4",
                "post_dir mover 2 4",
                "exit (3,3) mover 4",                // loc.Exit(src, direction)
                "tried_pass_turf (4,3) mover 8",     // newloc.Enter: the turf's CanPass first, border_dir = get_dir(turf, mover)
                "cross rug mover",                   // then the Cross of each occupant: its own signal,
                "cross_over mover rug",              // the mover's,
                "tried_pass_rug rug mover 8",        // and the occupant's CanPass
                "pre_move mover (4,3)",              // after Enter
                "exited (3,3) mover 4",              // point of no return passed: oldloc.Exited
                "exiting mover (3,3) 4",
                "entered (4,3) mover (3,3) null",    // newloc.Entered(src, oldloc, old_locs)
                "entering mover (4,3) (3,3) null",
                "moved mover (3,3) 4 0 null 1",      // Moved(oldloc, direction, FALSE, old_locs, momentum_change = TRUE)
                "abstract_exited (3,3) mover 4",
                "abstract_entered (4,3) mover (3,3) null",
            }, Trace(room.World));
            Assert.Same(b, m.loc);
            Assert.Equal(EAST, m.dir);
            AssertNumber(EAST, m.last_move);
            Assert.Null(m.active_movement);
            Assert.Equal(0f, m.moving_diagonally);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T1_ALeavingMoverEndsAsTheLastItemOfTheTurfsContentsAndTheOthersKeepTheirOrder()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf here = locate(3, 3, 1), there = locate(4, 3, 1);
            var a = Thing("a", there);
            var b = Thing("b", there);
            var mover = Thing("mover", here);
            var c = Thing("c", here);

            Step(mover, EAST);
            Assert.Equal(new object[] { a, b, mover }, Items(there.contents));   // arrival order (move-chain.md E1)
            Assert.Equal(new object[] { c }, Items(here.contents));

            // leaving and coming back puts a thing last again
            Step(a, WEST);
            Step(a, EAST);
            Assert.Equal(new object[] { b, mover, a }, Items(there.contents));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T1_AStepOutOfAContainerLeavesThroughTheContainersExitAndExitedAndTheRealTurfGetsTheEntered()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf here = locate(3, 3, 1), there = locate(4, 3, 1);
            var box = Thing("box", here);
            var item = Thing("item", box);
            On(box, COMSIG_ATOM_EXIT, "exit", 2);
            On(box, COMSIG_ATOM_EXITED, "exited", 2);
            On(there, COMSIG_ATOM_ENTERED, "entered", 3);
            On(item, COMSIG_MOVABLE_MOVED, "moved", 5);
            On(box, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited", 2);
            On(there, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered", 3);
            Trace(room.World).Clear();

            // get_step(item, EAST) resolves the item to the turf its box stands on (move-chain.md E3): a move out of the box
            AssertNumber(TRUE, Step(item, EAST));

            Assert.Same(there, item.loc);
            AssertTrace(new[]
            {
                "exit box item 4",
                "exited box item 4",
                "entered (4,3) item box null",
                "moved item box 4 0 null 1",
                "abstract_exited box item 4",
                "abstract_entered (4,3) item box null",
            }, Trace(room.World));
            Assert.DoesNotContain(item, Items(box.contents));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T1_AStepSetsTheDirectionAndLastMoveAndTheMoverTurnsEvenWhenNothingElseChanges()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var mover = Thing("mover", locate(3, 3, 1));
            mover.dir = SOUTH;

            AssertNumber(TRUE, Step(mover, NORTH));

            Assert.Equal(NORTH, mover.dir);
            AssertNumber(NORTH, mover.last_move);
            Assert.Same(locate(3, 4, 1), mover.loc);
            Assert.Equal(new float[] { 3, 4, 1 }, new[] { mover.x, mover.y, mover.z });
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- T2: areas ----

    // Two real /area/misc/testroom areas with gravity: the columns x >= 4 move into a second one with tg's change_area.
    private static area Annex(TgToyRoom room, int fromX = 4)
    {
        var annex = Dm.@new<area_misc_testroom>();
        annex.name = "annex";
        room.Room.name = "room";
        foreach (turf t in Dm.each<turf>(block(fromX, 1, 1, room.Width, room.Height, 1)))
            t.change_area((area)t.loc, annex);
        return annex;
    }

    [Fact]
    public void T2_AStepAcrossAnAreaBorderCallsTheOldAreasExitedAfterTheTurfsAndTheNewAreasEnteredAfterTheTurfs()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            area annex = Annex(room);
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            Assert.Same(room.Room, a.loc);
            Assert.Same(annex, b.loc);
            var m = Thing("mover", a);
            On(a, COMSIG_ATOM_EXITED, "exited", 2);
            On(room.Room, COMSIG_AREA_EXITED, "area_exited", 2);       // area/Exited sends this first,
            On(m, COMSIG_MOVABLE_EXITED_AREA, "exited_area", 2);       // then this one to the mover
            On(b, COMSIG_ATOM_ENTERED, "entered", 3);
            On(annex, COMSIG_AREA_ENTERED, "area_entered", 2);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            On(room.Room, COMSIG_ATOM_EXITED, "area_atom_exited", 2);  // /area/Exited has no ..(): this never fires
            On(annex, COMSIG_ATOM_ENTERED, "area_atom_entered", 3);    // nor this one
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, EAST));

            AssertTrace(new[]
            {
                "exited (3,3) mover 4",
                "area_exited area:room mover 4",
                "exited_area mover area:room 4",
                "entered (4,3) mover (3,3) null",
                "area_entered area:annex mover area:room",
                "moved mover (3,3) 4 0 null 1",
            }, Trace(room.World));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T2_AStepWithinOneAreaCallsNeitherAreaProc()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            Annex(room, 5);
            var m = Thing("mover", locate(2, 2, 1));
            On(room.Room, COMSIG_AREA_EXITED, "area_exited", 2);
            On(room.Room, COMSIG_AREA_ENTERED, "area_entered", 2);
            On(m, COMSIG_MOVABLE_EXITED_AREA, "exited_area", 2);
            Trace(room.World).Clear();

            Step(m, EAST);
            Step(m, NORTH);

            Assert.Empty(Trace(room.World));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T2_AnAreaSensitiveMoverGetsEnterAreaAndExitAreaAndARealMobUpdatesItsAmbience()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            area annex = Annex(room);
            turf a = locate(3, 3, 1);
            var thing = Thing("thing", a);
            thing.become_area_sensitive("integration");   // the channel of area-sensitive recipients: itself
            var m = Dm.@new<mob>(locate(3, 4, 1));
            m.name = "mob";
            m.become_area_sensitive("integration");
            On(thing, COMSIG_EXIT_AREA, "exit_area", 1);
            On(thing, COMSIG_ENTER_AREA, "enter_area", 1);
            On(m, COMSIG_ENTER_AREA, "mob_enter_area", 1);
            Trace(room.World).Clear();

            Step(thing, EAST);
            Step(m, EAST);

            AssertTrace(new[]
            {
                "exit_area thing area:room",       // area/Exited: the recipients of the mover's area-sensitive channel
                "enter_area thing area:annex",     // area/Entered returns early without that channel: with it, the same fan-out
                "mob_enter_area mob area:annex",
            }, Trace(room.World));
            Assert.Empty(room.Runtimes);   // update_ambience_area of a mob with no client runs (ambience.dm:106-110) and does not throw
        }
    }

    [Fact]
    public void T2_AMoverWithoutTheAreaChannelGetsNoEnterAreaSignalEvenThoughTheAreaEnteredSignalFires()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            area annex = Annex(room);
            var thing = Thing("thing", locate(3, 3, 1));
            On(thing, COMSIG_ENTER_AREA, "enter_area", 1);
            On(annex, COMSIG_AREA_ENTERED, "area_entered", 2);
            Trace(room.World).Clear();

            Step(thing, EAST);

            AssertTrace(new[] { "area_entered area:annex thing area:room" }, Trace(room.World));   // areas.dm:585-586
        }
    }

    [Fact]
    public void T2_ForceMoveAcrossAnAreaBorderCallsTheTurfExitedThenTheAreaExitedThenTheDestinationEnteredWithTwoArgumentsThenTheAreaEntered()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            area annex = Annex(room);
            turf a = locate(3, 3, 1), b = locate(5, 4, 1);
            var m = Thing("mover", a);
            On(a, COMSIG_ATOM_EXITED, "exited", 2);
            On(room.Room, COMSIG_AREA_EXITED, "area_exited", 2);
            On(b, COMSIG_ATOM_ENTERED, "entered", 3);
            On(annex, COMSIG_AREA_ENTERED, "area_entered", 2);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Force(m, b));

            // doMove (atoms_movable.dm:1218-1224): movement_dir = get_dir(src, destination) = NORTHEAST (5) for the Exited calls,
            // destination.Entered(src, oldloc) has no old_locs, Moved(oldloc, NONE, TRUE, null)
            AssertTrace(new[]
            {
                "exited (3,3) mover 5",
                "area_exited area:room mover 5",
                "entered (5,4) mover (3,3) null",
                "area_entered area:annex mover area:room",
                "moved mover (3,3) 0 1 null 1",
            }, Trace(room.World));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T2_MoveToNullspaceCallsTheTurfExitedThenTheAreaExitedBothWithNoDirectionAndMovedHasNoNewLoc()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var m = Thing("mover", a);
            On(a, COMSIG_ATOM_EXITED, "exited", 2);
            On(room.Room, COMSIG_AREA_EXITED, "area_exited", 2);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            On(a, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited", 2);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Waiting.NoSleep(m.moveToNullspace()));

            Assert.Null(m.loc);
            Assert.Equal(new float[] { 0, 0, 0 }, new[] { m.x, m.y, m.z });
            AssertTrace(new[]
            {
                "exited (3,3) mover 0",
                "area_exited area:" + room.Room.name + " mover 0",
                "moved mover (3,3) 0 1 null 1",
                "abstract_exited (3,3) mover 0",
            }, Trace(room.World));
            Assert.DoesNotContain(m, Items(a.contents));
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- T2b: step() that never calls Move ----

    [Fact]
    public void T2b_AStepWithDirectionZeroOrOffTheMapOrFromNullspaceNeverCallsMoveAndReturnsZero()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            // a mover can be put on a wall tile by forceMove (no Enter): (1, 3) is on the map's west edge
            var m = Thing("mover", locate(3, 3, 1));
            Force(m, locate(1, 3, 1));
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            Trace(room.World).Clear();

            AssertNumber(0, Step(m, WEST));         // get_step is null at the edge
            AssertNumber(0, Step(m, SOUTHWEST));
            AssertNumber(0, Step(m, 0));            // dir 0: get_step(src, 0) is the turf itself: t == ref.loc
            var nowhere = Thing("nowhere", null);
            AssertNumber(0, Step(nowhere, EAST));   // from nullspace

            Assert.Empty(Trace(room.World));        // ATTEMPTED_MOVE is the first thing Move sends: Move was never called
            Assert.Same(locate(1, 3, 1), m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- T3: forceMove ----

    [Fact]
    public void T3_ForceMoveFromNullspaceSendsEnteredAndMovedButNoExitedAndNoAbstractExit()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var m = Thing("mover", null);
            turf b = locate(4, 4, 1);
            On(b, COMSIG_ATOM_ENTERED, "entered", 3);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            On(b, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered", 3);
            On(room.Room, COMSIG_AREA_ENTERED, "area_entered", 2);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Force(m, b));

            AssertTrace(new[]
            {
                "entered (4,4) mover null null",           // oldloc is null
                "area_entered area:" + room.Room.name + " mover null",   // old_area null != destarea (doMove :1223)
                "moved mover null 0 1 null 1",
                "abstract_entered (4,4) mover null null",
            }, Trace(room.World));
            Assert.Same(b, m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T3_ForceMoveToTheSameLocFiresNoExitedNoEnteredButMovedStillFires()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var m = Thing("mover", a);
            On(a, COMSIG_ATOM_EXITED, "exited", 2);
            On(a, COMSIG_ATOM_ENTERED, "entered", 3);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Force(m, a));

            AssertTrace(new[] { "moved mover (3,3) 0 1 null 1" }, Trace(room.World));   // (do_after.on_user_moved guards user.loc == old_loc)
        }
    }

    [Fact]
    public void T3_ANestedForceMoveInsideAMovedHandlerRunsItsOwnWholeChainBeforeTheOuterMovedFinishes()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1), c = locate(5, 3, 1);
            var m = Thing("mover", a);
            bool moved_on = false;
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3, act: _ =>
            {
                if (moved_on)
                    return;
                moved_on = true;
                Force(m, c);   // the first Moved's handler: forceMove from inside the chain
            });
            On(b, COMSIG_ATOM_EXITED, "exited_b", 2);
            On(c, COMSIG_ATOM_ENTERED, "entered_c", 3);
            On(a, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited_a", 2);
            On(b, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited_b", 2);
            On(b, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered_b", 3);
            On(c, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered_c", 3);
            Trace(room.World).Clear();

            Force(m, b);

            // Moved(a -> b) sends MOVED, whose handler forceMoves to c: Exited b, Entered c, and a nested Moved(b -> c) with
            // its own MOVED and abstract signals; then the outer Moved goes on with its steps 3 on: ABSTRACT_EXITED of a, and
            // ABSTRACT_ENTERED of whatever loc is NOW (c), old_loc a (move-chain.md 3d; TgQuirk: the outer step reads loc late)
            AssertTrace(new[]
            {
                "moved mover (3,3) 0 1",
                "exited_b (4,3) mover 4",
                "entered_c (5,3) mover (4,3) null",
                "moved mover (4,3) 0 1",
                "abstract_exited_b (4,3) mover 0",
                "abstract_entered_c (5,3) mover (4,3) null",
                "abstract_exited_a (3,3) mover 0",
                "abstract_entered_c (5,3) mover (3,3) null",
            }, Trace(room.World));
            Assert.Same(c, m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T3_ForceMoveInsideAnExitedHandlerRunsTheNestedChainThenAStaleEnteredForAMoverThatAlreadyLeft()
    {
        // move-chain.md 3g: def1 has no `loc == newloc` check between oldloc.Exited (:747) and newloc.Entered (:755)
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1), c = locate(3, 4, 1);
            var m = Thing("mover", a);
            bool done = false;
            On(a, COMSIG_ATOM_EXITED, "exited_a", 2, act: _ =>
            {
                if (done)
                    return;
                done = true;
                Force(m, c);   // from inside a's Exited: m is at b already (loc was set before Exited)
            });
            On(b, COMSIG_ATOM_EXITED, "exited_b", 2);
            On(b, COMSIG_ATOM_ENTERED, "entered_b", 3);
            On(c, COMSIG_ATOM_ENTERED, "entered_c", 3);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, EAST));

            AssertTrace(new[]
            {
                "exited_a (3,3) mover 4",
                // the nested doMove: RESOLVE_ACTIVE_MOVEMENT flushes the OUTER Moved first (m is at b), then b.Exited (b never saw an
                // Entered), c.Entered, the nested Moved
                "moved mover (3,3) 4 0 null 1",
                "exited_b (4,3) mover 9",   // get_dir(mover at b, c) = NORTHWEST
                "entered_c (3,4) mover (4,3) null",
                "moved mover (4,3) 0 1 null 1",
                // back in the outer def1: a STALE newloc.Entered(src, oldloc, old_locs) for a mover that is at c now; the outer
                // RESOLVE_ACTIVE_MOVEMENT finds nothing pending (TgQuirk, pinned so nobody "fixes" it)
                "entered_b (4,3) mover (3,3) null",
            }, Trace(room.World));
            Assert.Same(c, m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T3_TheStaleEnteredOfARealOpenTurfSendsItsSignalsButTheOccupantIsNotTransferred()
    {
        // /turf/open/Entered (_open.dm:226-234) calls ..() first, so the signals go out for the stale mover; its own work
        // (footprints, transfer_occupant) is skipped by `src != arrived.loc`. b is a transit turf: a transfer would zMove the
        // mover to z 2.
        var room = TgToyRoom.Create(levels: 2);
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = (turf)locate(4, 3, 1), c = locate(3, 4, 1);
            var open_b = Assert.IsAssignableFrom<turf_open>(b);
            open_b.destination_x = 3;
            open_b.destination_y = 3;
            open_b.destination_z = 2;
            var m = Thing("mover", a);
            bool done = false;
            On(a, COMSIG_ATOM_EXITED, "exited_a", 0, act: _ =>
            {
                if (done)
                    return;
                done = true;
                Force(m, c);
            });
            On(b, COMSIG_ATOM_ENTERED, "entered_b", 1);
            Trace(room.World).Clear();

            Step(m, EAST);

            AssertTrace(new[] { "exited_a (3,3)", "entered_b (4,3) mover" }, Trace(room.World));
            Assert.Same(c, m.loc);
            Assert.Equal(1f, m.z);   // not transferred to level 2
            Assert.Empty(room.Runtimes);
        }
    }
}
