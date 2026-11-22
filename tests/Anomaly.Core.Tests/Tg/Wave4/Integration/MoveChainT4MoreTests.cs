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
/// The cases package A's tester ran against a literal copy of <c>/turf/Enter</c> on fixture floors (MoveEnterBumpTests,
/// MovedOnceTests, MoveMiscTests), rerun on the toy room against the real turf types, the real Enter, Exit, Cross, CanPass and
/// Bump. Written from the DM of turf.dm:434-466, _atom.dm:225-258 and :629-640, and atoms_movable.dm:935-993 and :1486-1509.
/// </summary>
public class MoveChainT4MoreTests
{
    // ---- Exit and PRE_MOVE return bits ----

    [Fact]
    public void AnExitHandlerReturningAnotherBitThanTheLowOneDoesNotVeto()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            On(a, COMSIG_ATOM_EXIT, "exit", 2, result: 2);   // COMPONENT_ATOM_BLOCK_EXIT is (1<<0)

            AssertNumber(TRUE, Step(m, EAST));

            Assert.Same(b, m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void AnExitHandlerThatRelocatesTheMoverAndVetoesLeavesItWhereTheHandlerPutItAndMovesTailStillRuns()
    {
        // "Handlers/overrides may relocate src (closet Exit -> open())" (move-chain.md 3a step 6): def1 returns FALSE, but def2's
        // bail needs loc == oldloc, which is false now, so last_move is set and the mover is turned (:841-846)
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), far = locate(6, 6, 1);
            var m = Thing("mover", a);
            m.dir = SOUTH;
            On(a, COMSIG_ATOM_EXIT, "exit", 2, result: COMPONENT_ATOM_BLOCK_EXIT, act: _ => Force(m, far));
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            AssertTrace(new[] { "exit (3,3) mover 4", "moved mover (3,3) 0 1" }, Trace(room.World));   // the forceMove's own Moved only
            Assert.Same(far, m.loc);
            AssertNumber(EAST, m.last_move);
            Assert.Equal(EAST, m.dir);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void APreMoveHandlerReturningAnotherBitThanTheLowOneDoesNotBlock()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var m = Thing("mover", locate(3, 3, 1));
            On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1, result: 2);   // COMPONENT_MOVABLE_BLOCK_PRE_MOVE is (1<<0)

            AssertNumber(TRUE, Step(m, EAST));

            Assert.Same(locate(4, 3, 1), m.loc);
        }
    }

    // ---- which blocker is bumped ----

    [Fact]
    public void OfTwoBorderObjectsTheFirstOneInTheContentsIsBumped()
    {
        // turf.dm:454: a later border object does not replace a border first_bump (the `!(first_bump.flags_1 & ON_BORDER_1)` half)
        foreach (bool first_has_lower_layer in new[] { true, false })
        {
            var room = TgToyRoom.Create();
            using (room.World.Enter())
            {
                turf a = locate(3, 3, 1), b = locate(4, 3, 1);
                var m = Thing("mover", a);
                var one = Thing("one", b, dense: true);
                var two = Thing("two", b, dense: true);
                one.layer = first_has_lower_layer ? 2 : 5;
                two.layer = first_has_lower_layer ? 5 : 2;
                Dm.combine(ref one.flags_1, ON_BORDER_1);
                Dm.combine(ref two.flags_1, ON_BORDER_1);
                On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
                Trace(room.World).Clear();

                Step(m, EAST);

                AssertTrace(new[] { "bump mover one" }, Trace(room.World));
            }
        }
    }

    [Fact]
    public void APhasingMoverPassesADenseOccupantToo()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            Dm.combine(ref m.movement_type, PHASING);
            Thing("blocker", b, dense: true);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, EAST));   // CanPass answers TRUE for a phasing mover before density is asked (_atom.dm:231)

            Assert.Empty(Trace(room.World));
            Assert.Same(b, m.loc);
        }
    }

    [Fact]
    public void OfTwoBlockersOfTheSameLayerTheFirstInTheContentsIsBumpedBecauseTheComparisonIsStrict()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var first = Thing("first", b, dense: true);
            var second = Thing("second", b, dense: true);
            first.layer = 4;
            second.layer = 4;
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
            Trace(room.World).Clear();

            Step(m, EAST);

            AssertTrace(new[] { "bump mover first" }, Trace(room.World));   // `thing.layer > first_bump.layer` (turf.dm:454)
        }
    }

    [Fact]
    public void AMoverLeavingADenseContainerOntoTheContainersOwnTurfIsNotBlockedByTheContainer()
    {
        // turf.dm:441-442: `if(thing == mover || thing == mover_loc) continue` ("moving out of other objects")
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var closet = Thing("closet", a, dense: true);
            var item = Thing("item", closet);
            On(closet, COMSIG_MOVABLE_CROSS, "cross_closet", 1);
            On(item, COMSIG_MOVABLE_BUMP, "bump", 1);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Move(item, a));

            Assert.Same(a, item.loc);
            Assert.Empty(Trace(room.World));   // the closet is not even asked to Cross, and nothing is bumped
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void APhasingMoversCrossHandlerThatDeletesItEndsEnterBeforeTheBumpOfTheBlockingCross()
    {
        // turf.dm:443-444: the QDELETED check right after a failed Cross, before the phasing branch's Bump
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            Dm.combine(ref m.movement_type, PHASING);
            var blocker = Thing("blocker", b, dense: true);
            On(blocker, COMSIG_MOVABLE_CROSS, "cross", 1, result: COMPONENT_BLOCK_CROSS, act: all => qdel(all[1]));
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
            On(blocker, COMSIG_ATOM_BUMPED, "bumped", 1);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            AssertTrace(new[] { "cross blocker mover" }, Trace(room.World));
            Assert.True(QDELETED(m));
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- CanPassThrough and CanAllowThrough ----

    [Fact]
    public void AMoverWhoseCanPassThroughSignalPermitsOrRefusesOverrulesTheBlockersOpinion()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var block = Thing("block", locate(4, 3, 1), dense: true);

            // PERMIT: a dense block lets the mover through
            var ghost = Thing("ghost", locate(3, 3, 1));
            ghost.generic_canpass = FALSE;
            On(ghost, COMSIG_MOVABLE_CAN_PASS_THROUGH, "permit", 2, result: COMSIG_COMPONENT_PERMIT_PASSAGE);
            AssertNumber(TRUE, Step(ghost, EAST));
            Assert.Same(locate(4, 3, 1), ghost.loc);

            // REFUSE: a plain floor refuses, so the mover bumps the turf itself (the turf answers can_pass_self FALSE)
            var wall = Thing("wall_walker", locate(3, 4, 1));
            wall.generic_canpass = FALSE;
            On(wall, COMSIG_MOVABLE_CAN_PASS_THROUGH, "refuse", 2, result: COMSIG_COMPONENT_REFUSE_PASSAGE);
            On(locate(4, 4, 1), COMSIG_ATOM_BUMPED, "turf_bumped", 1);
            Trace(room.World).Clear();
            AssertNumber(FALSE, Step(wall, EAST));
            Assert.Contains("turf_bumped (4,4) wall_walker", Trace(room.World));
            Assert.Same(locate(3, 4, 1), wall.loc);

            // both bits: PERMIT is tested first (:1505-1508)
            var both = Thing("both", locate(3, 5, 1));
            both.generic_canpass = FALSE;
            On(both, COMSIG_MOVABLE_CAN_PASS_THROUGH, "both", 2, result: Dm.bitor(COMSIG_COMPONENT_PERMIT_PASSAGE, COMSIG_COMPONENT_REFUSE_PASSAGE));
            AssertNumber(TRUE, Step(both, EAST));

            // no handler answers: the blocker's opinion stands (dense: no)
            var plain = Thing("plain", locate(3, 3, 1));
            plain.generic_canpass = FALSE;
            AssertNumber(FALSE, Waiting.NoSleep(block.CanPass(plain, WEST)));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void PassFlagsAndBuckledMobsMakeCanAllowThroughTrueForADenseBlocker()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var block = Thing("block", locate(4, 3, 1), dense: true);
            var mover = Thing("mover", locate(3, 3, 1));
            AssertNumber(FALSE, Waiting.NoSleep(block.CanAllowThrough(mover, WEST)));   // !density

            // _atom.dm:243-249: mover.pass_flags & pass_flags_self
            block.pass_flags_self = PASSTABLE;
            mover.pass_flags = PASSTABLE;
            AssertNumber(TRUE, Waiting.NoSleep(block.CanAllowThrough(mover, WEST)));
            AssertNumber(TRUE, Step(mover, EAST));   // and a real step passes it
            mover.pass_flags = PASSGRILLE;
            AssertNumber(FALSE, Waiting.NoSleep(block.CanAllowThrough(mover, WEST)));

            // atoms_movable.dm:1491-1494: a mob buckled to the blocker may pass it
            var rider = Dm.@new<mob>(locate(5, 3, 1));
            rider.name = "rider";
            AssertNumber(FALSE, Waiting.NoSleep(block.CanAllowThrough(rider, WEST)));
            block.buckled_mobs = Dm.list(rider);
            AssertNumber(TRUE, Waiting.NoSleep(block.CanAllowThrough(rider, WEST)));
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- handlers that delete or move the mover from inside Enter (R4) ----

    [Fact]
    public void DV2_ACrossHandlerThatDeletesTheMoverEndsEnterWithoutABumpEvenAtWorldTimeZero()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            Assert.Equal(0, room.World.Tick);   // DV2: a datum qdel'd at world time 0 counts as deleted
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var rug = Thing("rug", b);
            On(rug, COMSIG_MOVABLE_CROSS, "cross", 1, result: COMPONENT_BLOCK_CROSS, act: all => qdel(all[1]));
            On(rug, COMSIG_ATOM_BUMPED, "bumped", 1);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));   // `if(QDELETED(mover)) return FALSE` after the failed Cross (turf.dm:443)

            Assert.True(QDELETED(m));
            Assert.Null(m.loc);
            AssertTrace(new[] { "cross rug mover" }, Trace(room.World));   // no Bump, no Bumped
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void ABumpHandlerThatDeletesTheMoverEndsTheMoveWithFalseAndBumpedIsStillSent()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var block = Thing("block", b, dense: true);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1, act: all => qdel(all[0]));
            On(block, COMSIG_ATOM_BUMPED, "bumped", 1);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            Assert.True(QDELETED(m));
            Assert.Null(m.loc);
            // Bump checks only QDELETED(bumped_atom) (atoms_movable.dm:989), so Bumped goes out for a deleted mover
            AssertTrace(new[] { "bump mover block", "bumped block mover" }, Trace(room.World));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void TgQuirk_ABumpHandlerThatMovesTheMoverAwayLeavesItThereAndTheFailedMoveStillSetsLastMoveAndDirection()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1), far = locate(6, 6, 1);
            var m = Thing("mover", a);
            m.dir = SOUTH;
            var block = Thing("block", b, dense: true);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1, act: _ => Force(m, far));
            On(block, COMSIG_ATOM_BUMPED, "bumped", 1);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3);
            Trace(room.World).Clear();

            AssertNumber(FALSE, Step(m, EAST));

            Assert.Same(far, m.loc);
            // the handler's forceMove ran a whole movement inside Enter (one Moved), def1 returned FALSE; def2's bail needs
            // loc == oldloc, which is false now, so its tail ran: last_move = direct, setDir
            AssertNumber(EAST, m.last_move);
            Assert.Equal(EAST, m.dir);
            AssertTrace(new[] { "bump mover block", "moved mover (3,3) 0 1", "bumped block mover" }, Trace(room.World));
            Assert.Null(m.active_movement);
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- Bump ----

    [Fact]
    public void BumpInterceptDoesNotSendBumpedAndAnyOtherReturnBitDoesNotIntercept()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var m = Thing("mover", locate(3, 3, 1));
            var block = Thing("block", locate(4, 3, 1), dense: true);
            On(block, COMSIG_ATOM_BUMPED, "bumped", 1);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1, result: 2);   // not COMPONENT_INTERCEPT_BUMPED
            Trace(room.World).Clear();

            Assert.Null(Waiting.NoSleep(m.Bump(block)));   // `. = ..()` of the native proc: null
            AssertTrace(new[] { "bump mover block", "bumped block mover" }, Trace(room.World));

            var m2 = Thing("mover2", locate(3, 3, 1));
            On(m2, COMSIG_MOVABLE_BUMP, "bump2", 1, result: Dm.bitor(COMPONENT_INTERCEPT_BUMPED, 2));
            Trace(room.World).Clear();
            AssertNumber(TRUE, Waiting.NoSleep(m2.Bump(block)));
            AssertTrace(new[] { "bump2 mover2 block" }, Trace(room.World));   // intercepted: Bumped is not sent
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void BumpingADeletedAtomReturnsTrueWithoutSendingBumped()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var m = Thing("mover", locate(3, 3, 1));
            var block = Thing("block", locate(4, 3, 1), dense: true);
            On(block, COMSIG_ATOM_BUMPED, "bumped", 1);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1, act: all => qdel(all[1]));
            Trace(room.World).Clear();

            AssertNumber(TRUE, Waiting.NoSleep(m.Bump(block)));   // `if(QDELETED(bumped_atom)) return TRUE` (:989)

            AssertTrace(new[] { "bump mover block" }, Trace(room.World));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void BumpWithNoArgumentIsACrashAndABumpOfAWallTurfBumpsTheTurf()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var m = Thing("mover", locate(2, 3, 1));
            Assert.Throws<DmCrashException>(() => Waiting.NoSleep(m.Bump(null)));   // CRASH("Bump was called with no argument.")
            On(locate(1, 3, 1), COMSIG_ATOM_BUMPED, "wall_bumped", 1);
            Trace(room.World).Clear();

            Waiting.NoSleep(m.Bump(locate(1, 3, 1)));

            AssertTrace(new[] { "wall_bumped (1,3) mover" }, Trace(room.World));
        }
    }
}
