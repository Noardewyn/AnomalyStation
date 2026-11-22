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
/// T4 of docs/ai/specs/move-chain.md section 11 on the toy room: the Exit veto, a failed Enter with its Bump, PHASING, the PRE_MOVE
/// block and the Cross block, against tg's real <c>/turf/Enter</c> (turf.dm:434-466), the real <c>/atom/Exit</c> and the real
/// <c>/atom/movable/Bump</c> and <c>Bumped</c>. A wall is a real <c>/turf/closed/indestructible</c>; a blocker is a real dense
/// <c>/obj</c>. The order is written from move-chain.md 3a, 3b, 3c and 3g.
/// </summary>
public class MoveChainT4Tests
{
    // The probes every T4 test wants: what Move sends from ATTEMPTED_MOVE to MOVED.
    private static void ChainProbes(atom_movable m, turf a, turf b)
    {
        On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
        On(a, COMSIG_ATOM_EXIT, "exit", 2);
        On(b, COMSIG_ATOM_TRIED_PASS, "tried_pass_turf", 2);
        On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
        On(a, COMSIG_ATOM_EXITED, "exited", 2);
        On(b, COMSIG_ATOM_ENTERED, "entered", 3);
        On(m, COMSIG_MOVABLE_MOVED, "moved", 3);
    }

    private static void BumpProbes(atom_movable m, atom bumped)
    {
        On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
        On(bumped, COMSIG_ATOM_BUMPED, "bumped", 1);
    }

    // ---- the Exit veto ----

    [Fact]
    public void T4_AnExitVetoStopsTheMoveAfterTheExitSignalReturnsFalseNotNullAndTheMoverHasTurned()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            m.dir = SOUTH;
            ChainProbes(m, a, b);
            On(a, COMSIG_ATOM_EXIT, "exit_veto", 2, result: COMPONENT_ATOM_BLOCK_EXIT);
            On(a, COMSIG_ATOM_EXIT, "exit_second", 2);   // a second listener still hears it: every handler runs
            Trace(room.World).Clear();

            object result = Step(m, EAST);

            AssertNumber(FALSE, result);   // def1 returns FALSE, def2 returns it: FALSE, not null
            AssertTrace(new[]
            {
                "attempted mover (4,3) 4",
                "exit (3,3) mover 4",
                "exit_veto (3,3) mover 4",
                "exit_second (3,3) mover 4",
            }, Trace(room.World));
            Assert.Same(a, m.loc);
            Assert.Equal(EAST, m.dir);               // the dir changed BEFORE Exit: a failed move still turns the mover
            AssertNumber(0, m.last_move);            // def2's :831 bail
            Assert.Null(m.active_movement);
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- a failed Enter and its Bump ----

    [Fact]
    public void T4_ADenseOccupantFailsTheEnterAndTheMoverBumpsItBeforeAnythingIsCommitted()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var blocker = Thing("blocker", b, dense: true);
            ChainProbes(m, a, b);
            On(blocker, COMSIG_MOVABLE_CROSS, "cross", 1);
            On(m, COMSIG_MOVABLE_CROSS_OVER, "cross_over", 1);
            On(blocker, COMSIG_ATOM_TRIED_PASS, "tried_pass_blocker", 2);
            BumpProbes(m, blocker);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            AssertTrace(new[]
            {
                "attempted mover (4,3) 4",
                "exit (3,3) mover 4",
                "tried_pass_turf (4,3) mover 8",
                "cross blocker mover",
                "cross_over mover blocker",
                "tried_pass_blocker blocker mover 8",   // the blocker's CanPass says no: density
                "bump mover blocker",                    // mover.Bump(first_bump), still inside Enter
                "bumped blocker mover",                  // bumped_atom.Bumped(src) (waitfor = FALSE: its signal goes out at once)
            }, Trace(room.World));                       // no pre_move, exited, entered or moved
            Assert.Same(a, m.loc);
            Assert.Equal(new object[] { blocker }, Items(b.contents));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T4_AStepIntoARealWallBumpsTheWallTurfItselfAndNoOccupantIsCrossed()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(2, 3, 1);
            turf wall = locate(1, 3, 1);
            Assert.IsType<turf_closed_indestructible>(wall);
            var m = Thing("mover", a);
            var inside = Thing("inside", wall);   // an occupant of the wall tile: with can_pass_self FALSE the loop is skipped
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            On(a, COMSIG_ATOM_EXIT, "exit", 2);
            On(wall, COMSIG_ATOM_TRIED_PASS, "tried_pass_turf", 2);
            On(inside, COMSIG_MOVABLE_CROSS, "cross", 1);
            On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
            BumpProbes(m, wall);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, WEST));

            AssertTrace(new[]
            {
                "attempted mover (1,3) 8",
                "exit (2,3) mover 8",
                "tried_pass_turf (1,3) mover 4",   // border_dir = get_dir(wall, mover) = EAST
                "bump mover (1,3)",                // first_bump = src (turf.dm:459)
                "bumped (1,3) mover",
            }, Trace(room.World));
            Assert.Same(a, m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T4_OfSeveralBlockersTheOneWithTheHigherLayerIsBumped()
    {
        foreach (bool high_first in new[] { true, false })
        {
            var room = TgToyRoom.Create();
            using (room.World.Enter())
            {
                turf a = locate(3, 3, 1), b = locate(4, 3, 1);
                var m = Thing("mover", a);
                obj low, high;
                if (high_first)
                {
                    high = Thing("high", b, dense: true);
                    low = Thing("low", b, dense: true);
                }
                else
                {
                    low = Thing("low", b, dense: true);
                    high = Thing("high", b, dense: true);
                }
                low.layer = 2;
                high.layer = 5;
                On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
                Trace(room.World).Clear();

                AssertNumber(FALSE, Step(m, EAST));

                AssertTrace(new[] { "bump mover high" }, Trace(room.World));   // one Bump, for the higher layer, in either contents order
                Assert.Empty(room.Runtimes);
            }
        }
    }

    [Fact]
    public void T4_ABorderObjectIsBumpedBeforeAnObjectOfAHigherLayerWhateverTheContentsOrder()
    {
        // turf.dm:454: first_bump is replaced when (thing.layer > first_bump.layer || thing.flags_1 & ON_BORDER_1)
        // && !(first_bump.flags_1 & ON_BORDER_1)
        foreach (bool border_first in new[] { true, false })
        {
            var room = TgToyRoom.Create();
            using (room.World.Enter())
            {
                turf a = locate(3, 3, 1), b = locate(4, 3, 1);
                var m = Thing("mover", a);
                obj border, high;
                if (border_first)
                {
                    border = Thing("border", b, dense: true);
                    high = Thing("high", b, dense: true);
                }
                else
                {
                    high = Thing("high", b, dense: true);
                    border = Thing("border", b, dense: true);
                }
                border.layer = 2;
                high.layer = 5;
                Dm.combine(ref border.flags_1, ON_BORDER_1);
                On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
                Trace(room.World).Clear();

                Step(m, EAST);

                AssertTrace(new[] { "bump mover border" }, Trace(room.World));
                Assert.Empty(room.Runtimes);
            }
        }
    }

    // ---- the Cross block ----

    [Fact]
    public void T4_AnOccupantsCrossSignalReturningTheBlockBitMakesItTheBumpedThingEvenWhenItIsNotDense()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var rug = Thing("rug", b);   // not dense
            ChainProbes(m, a, b);
            On(rug, COMSIG_MOVABLE_CROSS, "cross", 1, result: COMPONENT_BLOCK_CROSS);
            On(m, COMSIG_MOVABLE_CROSS_OVER, "cross_over", 1);
            On(rug, COMSIG_ATOM_TRIED_PASS, "tried_pass_rug", 2);
            BumpProbes(m, rug);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            AssertTrace(new[]
            {
                "attempted mover (4,3) 4",
                "exit (3,3) mover 4",
                "tried_pass_turf (4,3) mover 8",
                "cross rug mover",       // COMPONENT_BLOCK_CROSS: Cross returns FALSE at once: no cross_over, no CanPass of the rug
                "bump mover rug",
                "bumped rug mover",
            }, Trace(room.World));
            Assert.Same(a, m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T4_TheMoversCrossOverSignalReturningTheBlockBitBlocksToo()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var rug = Thing("rug", b);
            On(rug, COMSIG_MOVABLE_CROSS, "cross", 1);
            On(m, COMSIG_MOVABLE_CROSS_OVER, "cross_over", 1, result: COMPONENT_BLOCK_CROSS);
            On(rug, COMSIG_ATOM_TRIED_PASS, "tried_pass_rug", 2);
            BumpProbes(m, rug);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            AssertTrace(new[] { "cross rug mover", "cross_over mover rug", "bump mover rug", "bumped rug mover" }, Trace(room.World));
            Assert.Same(a, m.loc);
        }
    }

    [Fact]
    public void T4_ACanPassSignalPermittingPassageLetsTheMoverThroughADenseBlocker()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var blocker = Thing("blocker", b, dense: true);
            On(blocker, COMSIG_ATOM_TRIED_PASS, "permit", 2, result: COMSIG_COMPONENT_PERMIT_PASSAGE);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, EAST));

            AssertTrace(new[] { "permit blocker mover 8" }, Trace(room.World));   // no Bump
            Assert.Same(b, m.loc);
        }
    }

    // ---- PHASING ----

    [Fact]
    public void T4_APhasingMoverBumpsABlockingCrossAndStillMovesBecauseEnterReturnsThePhasingBit()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            Dm.combine(ref m.movement_type, PHASING);
            var blocker = Thing("blocker", b, dense: true);
            ChainProbes(m, a, b);
            On(blocker, COMSIG_MOVABLE_CROSS, "cross", 1, result: COMPONENT_BLOCK_CROSS);
            BumpProbes(m, blocker);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, EAST));

            AssertTrace(new[]
            {
                "attempted mover (4,3) 4",
                "exit (3,3) mover 4",
                "tried_pass_turf (4,3) mover 8",
                "cross blocker mover",
                "bump mover blocker",            // mover_is_phasing: Bump, and carry on (turf.dm:446-449)
                "bumped blocker mover",
                "pre_move mover (4,3)",          // Enter returned TRUE: the move goes on, the bump already happened
                "exited (3,3) mover 4",
                "entered (4,3) mover (3,3) null",
                "moved mover (3,3) 4 0",
            }, Trace(room.World));
            Assert.Same(b, m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T4_APhasingMoverPassesARealWallWithNoBumpBecauseCanPassAnswersYesForAPhasingMover()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(2, 3, 1);
            turf wall = locate(1, 3, 1);
            var m = Thing("mover", a);
            Dm.combine(ref m.movement_type, PHASING);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
            On(wall, COMSIG_ATOM_ENTERED, "entered", 2);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, WEST));

            AssertTrace(new[] { "entered (1,3) mover (2,3)" }, Trace(room.World));   // _atom.dm:231 returns TRUE before density is asked
            Assert.Same(wall, m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T4_ABumpCanHappenEvenWhenPreMoveBlocksTheMoveAfterwards()
    {
        // move-chain.md 3a step 8: PRE_MOVE is checked AFTER Enter, so a phasing mover bumps and is still stopped
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            Dm.combine(ref m.movement_type, PHASING);
            var blocker = Thing("blocker", b, dense: true);
            ChainProbes(m, a, b);
            On(blocker, COMSIG_MOVABLE_CROSS, "cross", 1, result: COMPONENT_BLOCK_CROSS);
            BumpProbes(m, blocker);
            On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move_veto", 1, result: COMPONENT_MOVABLE_BLOCK_PRE_MOVE);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            AssertTrace(new[]
            {
                "attempted mover (4,3) 4",
                "exit (3,3) mover 4",
                "tried_pass_turf (4,3) mover 8",
                "cross blocker mover",
                "bump mover blocker",
                "bumped blocker mover",
                "pre_move mover (4,3)",
                "pre_move_veto mover (4,3)",
            }, Trace(room.World));
            Assert.Same(a, m.loc);
            AssertNumber(0, m.last_move);
        }
    }

    // ---- the PRE_MOVE block ----

    [Fact]
    public void T4_APreMoveBlockStopsTheMoveAfterTheWholeEnterAndNothingIsCommitted()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var rug = Thing("rug", b);
            ChainProbes(m, a, b);
            On(rug, COMSIG_MOVABLE_CROSS, "cross", 1);
            On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move_veto", 1, result: COMPONENT_MOVABLE_BLOCK_PRE_MOVE);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            AssertTrace(new[]
            {
                "attempted mover (4,3) 4",
                "exit (3,3) mover 4",
                "tried_pass_turf (4,3) mover 8",
                "cross rug mover",
                "pre_move mover (4,3)",
                "pre_move_veto mover (4,3)",
            }, Trace(room.World));
            Assert.Same(a, m.loc);
            Assert.Equal(new object[] { m }, Items(a.contents));
            Assert.Null(m.active_movement);   // never set: SET_ACTIVE_MOVEMENT comes after the point of no return
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T4_EveryHandlerOfASignalRunsAndTheBitsAreOredNotShortCircuited()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var m = Thing("mover", a);
            On(m, COMSIG_MOVABLE_PRE_MOVE, "first", 1, result: NONE);
            On(m, COMSIG_MOVABLE_PRE_MOVE, "second", 1, result: COMPONENT_MOVABLE_BLOCK_PRE_MOVE);
            On(m, COMSIG_MOVABLE_PRE_MOVE, "third", 1, result: NONE);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            AssertTrace(new[] { "first mover (4,3)", "second mover (4,3)", "third mover (4,3)" }, Trace(room.World));
        }
    }
}
