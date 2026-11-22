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
/// T4 of the move-chain goldens: Exit veto, a failed Enter with its Bump, PHASING, a PRE_MOVE block and a Cross block, and
/// the COMPONENT_* veto flags of PRE_MOVE, CROSS, EXIT and CAN_PASS_THROUGH. Written from atoms_movable.dm (Move, Cross,
/// Bump, CanPassThrough), _atom.dm (CanPass, CanAllowThrough, Exit) and turf.dm:434-466 (/turf/Enter, which the fixture
/// floor <see cref="turf_test_movable_floor_tg_enter"/> copies, as written before package B's merge; the real one is in
/// game/turfs/turf.cs, and W4.M reruns the cases on it in Integration/MoveChainT4Tests.cs and MoveChainT4MoreTests.cs).
/// </summary>
public class MoveEnterBumpTests
{
    private static EngineWorld World() => MovableWorld.Create(floor: typeof(turf_test_movable_floor_tg_enter));

    // ---- a failed Enter and its Bump ----

    [Fact]
    public void ADenseOccupantIsBumpedFromInsideEnterAndNothingPastTheCheckHappens()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var block = Dm.@new<obj_test_movable_block>(there);
            On(mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted");
            On(mover, COMSIG_MOVABLE_BUMP, "bump");
            On(block, COMSIG_ATOM_BUMPED, "bumped");
            On(mover, COMSIG_MOVABLE_PRE_MOVE, "pre_move");
            On(here, COMSIG_ATOM_EXITED, "exited");
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            AssertNumber(FALSE, Step(mover, EAST));

            AssertLines(new[]
            {
                "attempted " + D(mover) + " " + D(there) + " 4 null",
                "Enter " + D(there) + " " + D(mover),
                "bump " + D(mover) + " " + D(block) + " null null",
                "bumped " + D(block) + " " + D(mover) + " null null",
            }, map.Trace);
            Assert.Same(here, mover.loc);
            Assert.Equal(EAST, mover.dir); // setDir comes before Exit and Enter: a bump turns the mover
            AssertNumber(0, mover.last_move);
            Assert.Null(mover.active_movement);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ADenseTurfIsBumpedItselfAndItsOccupantsAreNeverAskedToCross()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            there.density = TRUE;
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var occupant = Dm.@new<obj_test_movable_thing>(there);
            On(occupant, COMSIG_MOVABLE_CROSS, "cross");
            On(mover, COMSIG_MOVABLE_BUMP, "bump");
            On(there, COMSIG_ATOM_BUMPED, "turf_bumped");
            map.Trace.Clear();

            AssertNumber(FALSE, Step(mover, EAST));

            // !can_pass_self: `first_bump = src` (turf.dm:459-460), and the occupants' Cross is inside `if(can_pass_self)`.
            AssertLines(new[]
            {
                "Enter " + D(there) + " " + D(mover),
                "bump " + D(mover) + " " + D(there) + " null null",
                "turf_bumped " + D(there) + " " + D(mover) + " null null",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void OnlyTheFirstBlockerOfTheHighestLayerIsBumpedWhateverTheOrderOfTheContents()
    {
        foreach (bool high_first in new[] { true, false })
        {
            var map = World();
            using (map.World.Enter())
            {
                turf here = locate(1, 1, 1), there = locate(2, 1, 1);
                var mover = Dm.@new<obj_test_movable_thing>(here);
                atom_movable low, high;
                if (high_first)
                {
                    high = Dm.@new<obj_test_movable_block_high>(there);
                    low = Dm.@new<obj_test_movable_block>(there);
                }
                else
                {
                    low = Dm.@new<obj_test_movable_block>(there);
                    high = Dm.@new<obj_test_movable_block_high>(there);
                }
                On(low, COMSIG_MOVABLE_CROSS, "cross_low");
                On(high, COMSIG_MOVABLE_CROSS, "cross_high");
                On(low, COMSIG_ATOM_BUMPED, "bumped_low");
                On(high, COMSIG_ATOM_BUMPED, "bumped_high");
                map.Trace.Clear();

                AssertNumber(FALSE, Step(mover, EAST));

                // Both are asked to cross (the loop does not stop at the first blocker), one is bumped: `thing.layer >
                // first_bump.layer` (turf.dm:452).
                Assert.Equal(1, Count(map.Trace, "cross_low"));
                Assert.Equal(1, Count(map.Trace, "cross_high"));
                Assert.Equal(0, Count(map.Trace, "bumped_low"));
                Assert.Equal(1, Count(map.Trace, "bumped_high"));
                MovableWorld.AssertNoRuntimes(map);
            }
        }
    }

    [Fact]
    public void ABorderObjectIsBumpedBeforeAnyNonBorderBlockerAndTheFirstBorderObjectBeforeALaterOne()
    {
        // contents order -> the expected bumped label (turf.dm:452: `(thing.layer > first_bump.layer || thing.flags_1 &
        // ON_BORDER_1) && !(first_bump.flags_1 & ON_BORDER_1)`)
        foreach ((string[] order, string bumped) in new[]
        {
            (new[] { "high", "border" }, "border"),
            (new[] { "border", "high" }, "border"),
            (new[] { "border", "border2" }, "border"),
            (new[] { "low", "high" }, "high"),
        })
        {
            var map = World();
            using (map.World.Enter())
            {
                turf here = locate(1, 1, 1), there = locate(2, 1, 1);
                var mover = Dm.@new<obj_test_movable_thing>(here);
                foreach (string kind in order)
                {
                    atom_movable thing = kind switch
                    {
                        "high" => Dm.@new<obj_test_movable_block_high>(there),
                        "low" => Dm.@new<obj_test_movable_block>(there),
                        _ => Dm.@new<obj_test_movable_block_border>(there),
                    };
                    On(thing, COMSIG_ATOM_BUMPED, "bumped_" + kind);
                }
                map.Trace.Clear();

                AssertNumber(FALSE, Step(mover, EAST));

                string[] bumps = Kinds(Lines(map.Trace, "bumped_high").Concat(Lines(map.Trace, "bumped_low")).Concat(Lines(map.Trace, "bumped_border")).Concat(Lines(map.Trace, "bumped_border2")));
                Assert.Equal(new[] { "bumped_" + bumped }, bumps);
                MovableWorld.AssertNoRuntimes(map);
            }
        }
    }

    [Fact]
    public void ANonDenseOccupantWhoseCrossSignalVetoesIsBumpedLikeADenseOne()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var rug = Dm.@new<obj_test_movable_thing>(there); // not dense
            On(rug, COMSIG_MOVABLE_CROSS, "cross", COMPONENT_BLOCK_CROSS);
            On(rug, COMSIG_ATOM_BUMPED, "bumped");
            map.Trace.Clear();

            AssertNumber(FALSE, Step(mover, EAST));

            Assert.Equal(new[] { "cross", "bumped" }, Kinds(map.Trace.Where(l => !l.StartsWith("Enter ")))); // CROSS_OVER is not sent after a veto
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- PHASING ----

    [Fact]
    public void APhasingMoverBumpsABlockerThatVetoesCrossAndStillPassesThroughItAfterBumping()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            mover.movement_type = Dm.bitor(GROUND, PHASING);
            var rug = Dm.@new<obj_test_movable_thing>(there);
            On(rug, COMSIG_MOVABLE_CROSS, "cross", COMPONENT_BLOCK_CROSS);
            On(mover, COMSIG_MOVABLE_BUMP, "bump");
            On(rug, COMSIG_ATOM_BUMPED, "bumped");
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            // `if(mover_is_phasing) { mover.Bump(thing) ... continue }` (turf.dm:446-449): the bump is real, the move goes on.
            AssertNumber(TRUE, Step(mover, EAST));

            Assert.Same(there, mover.loc);
            AssertLines(new[]
            {
                "Enter " + D(there) + " " + D(mover),
                "cross " + D(rug) + " " + D(mover) + " null null",
                "bump " + D(mover) + " " + D(rug) + " null null",
                "bumped " + D(rug) + " " + D(mover) + " null null",
                "moved " + D(mover) + " " + D(here) + " 4 0",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void APhasingMoverPassesADenseTurfAndADenseOccupantBecauseCanPassSaysTrueForPhasing()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            there.density = TRUE;
            Dm.@new<obj_test_movable_block>(there);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            mover.movement_type = Dm.bitor(GROUND, PHASING);
            On(mover, COMSIG_MOVABLE_BUMP, "bump");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST)); // _atom.dm:231: `if(mover.movement_type & PHASING) return TRUE`

            Assert.Same(there, mover.loc);
            Assert.Equal(0, Count(map.Trace, "bump"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- the Exit veto ----

    [Fact]
    public void AnExitVetoStopsBeforeEnterAndOnlyTheLowBitCounts()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            On(here, COMSIG_ATOM_EXIT, "exit_other_bits", 2); // not COMPONENT_ATOM_BLOCK_EXIT (1<<0)
            map.Trace.Clear();
            AssertNumber(TRUE, Step(mover, EAST));
            Assert.Same(there, mover.loc);

            // A handler that returns the flag together with other bits vetoes, and the rest of the handlers still run.
            var m2 = Dm.@new<obj_test_movable_thing>(here);
            On(here, COMSIG_ATOM_EXIT, "exit_veto", Dm.bitor(COMPONENT_ATOM_BLOCK_EXIT, 4));
            On(here, COMSIG_ATOM_EXIT, "exit_after");
            map.Trace.Clear();
            AssertNumber(FALSE, Step(m2, EAST));
            Assert.Same(here, m2.loc);
            Assert.Equal(new[] { "exit_other_bits", "exit_veto", "exit_after" }, Kinds(map.Trace)); // no Enter line
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AnExitHandlerThatRelocatesTheMoverAndVetoesLeavesItWhereTheHandlerPutItAndStillRunsMovesTail()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), far = locate(3, 3, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            // Like a closet's Exit, which opens the closet and so moves what is inside (move-chain.md 3a, step 6).
            On(here, COMSIG_ATOM_EXIT, "exit", COMPONENT_ATOM_BLOCK_EXIT, act: (source, leaving) => Force((atom_movable)leaving, far));
            map.Trace.Clear();

            AssertNumber(FALSE, Step(mover, EAST));

            Assert.Same(far, mover.loc);
            // Def2's `loc == oldloc` bail is not taken (the mover is somewhere else): last_move is set and the mover turns
            // although the move failed (:831 versus :864-868).
            AssertNumber(EAST, mover.last_move);
            Assert.Equal(EAST, mover.dir);
            Assert.Equal(0, Count(map.Trace, "Enter"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- PRE_MOVE ----

    [Fact]
    public void APreMoveVetoComesAfterEnterSoABumpStillHappensAndOnlyTheLowBitCounts()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var block = Dm.@new<obj_test_movable_block>(there);
            On(mover, COMSIG_MOVABLE_PRE_MOVE, "pre_move", COMPONENT_MOVABLE_BLOCK_PRE_MOVE);
            On(block, COMSIG_ATOM_BUMPED, "bumped");
            map.Trace.Clear();

            // The Enter fails first: PRE_MOVE is never reached.
            AssertNumber(FALSE, Step(mover, EAST));
            Assert.Equal(new[] { "Enter", "bumped" }, Kinds(map.Trace));

            // With nothing in the way Enter succeeds, and then PRE_MOVE vetoes.
            var free = Dm.@new<obj_test_movable_thing>(locate(1, 3, 1));
            On(free, COMSIG_MOVABLE_PRE_MOVE, "pre_move2", Dm.bitor(COMPONENT_MOVABLE_BLOCK_PRE_MOVE, 8));
            On(free, COMSIG_MOVABLE_PRE_MOVE, "pre_move3");
            map.Trace.Clear();
            AssertNumber(FALSE, Step(free, EAST));
            Assert.Equal(new[] { "Enter", "pre_move2", "pre_move3" }, Kinds(map.Trace)); // every handler runs
            Assert.Same(locate(1, 3, 1), free.loc);

            var other = Dm.@new<obj_test_movable_thing>(locate(1, 2, 1));
            On(other, COMSIG_MOVABLE_PRE_MOVE, "pre_move_other_bits", 2);
            AssertNumber(TRUE, Step(other, EAST)); // 2 is not the veto bit
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- Cross ----

    [Fact]
    public void ACrossOverVetoOnTheMoverSideAlsoMakesCrossFalseWithoutAskingCanPass()
    {
        var map = World();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            On(mover, COMSIG_MOVABLE_CROSS_OVER, "cross_over", COMPONENT_BLOCK_CROSS);
            On(thing, COMSIG_ATOM_TRIED_PASS, "tried_pass");
            map.Trace.Clear();

            AssertNumber(FALSE, Waiting.NoSleep(thing.Cross(mover)));
            Assert.Equal(new[] { "cross_over" }, Kinds(map.Trace)); // CanPass (which would send TRIED_PASS) is not reached

            // A handler that returns another bit does not veto.
            var m2 = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            On(m2, COMSIG_MOVABLE_CROSS_OVER, "cross_over2", 2);
            AssertNumber(TRUE, Waiting.NoSleep(thing.Cross(m2)));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ADenseBlockerThatAllowsPassageBySignalLetsTheMoverThrough()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var block = Dm.@new<obj_test_movable_block>(there);
            On(block, COMSIG_ATOM_TRIED_PASS, "tried_pass", COMSIG_COMPONENT_PERMIT_PASSAGE);
            On(mover, COMSIG_MOVABLE_BUMP, "bump");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST)); // CanPass: COMSIG_ATOM_TRIED_PASS & PERMIT_PASSAGE => TRUE (_atom.dm:229)

            Assert.Same(there, mover.loc);
            Assert.Equal(0, Count(map.Trace, "bump"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AMoverWhoseCanPassThroughPermitsOrRefusesOverrulesTheBlockersOpinion()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var block = Dm.@new<obj_test_movable_block>(there);

            // PERMIT: a dense block lets the mover through.
            var ghost = Dm.@new<obj_test_movable_thing>(here);
            ghost.generic_canpass = FALSE;
            On(ghost, COMSIG_MOVABLE_CAN_PASS_THROUGH, "permit", COMSIG_COMPONENT_PERMIT_PASSAGE);
            AssertNumber(TRUE, Step(ghost, EAST));
            Assert.Same(there, ghost.loc);

            // REFUSE: the (non-dense) turf itself is refused, so the mover bumps the turf.
            var wall = Dm.@new<obj_test_movable_thing>(locate(1, 2, 1));
            wall.generic_canpass = FALSE;
            On(wall, COMSIG_MOVABLE_CAN_PASS_THROUGH, "refuse", COMSIG_COMPONENT_REFUSE_PASSAGE);
            On(locate(2, 2, 1), COMSIG_ATOM_BUMPED, "turf_bumped");
            AssertNumber(FALSE, Step(wall, EAST));
            Assert.Equal(1, Count(map.Trace, "turf_bumped"));

            // Both bits: PERMIT is tested first (:1504-1507).
            var both = Dm.@new<obj_test_movable_thing>(locate(1, 3, 1));
            both.generic_canpass = FALSE;
            On(both, COMSIG_MOVABLE_CAN_PASS_THROUGH, "both", Dm.bitor(COMSIG_COMPONENT_PERMIT_PASSAGE, COMSIG_COMPONENT_REFUSE_PASSAGE));
            AssertNumber(TRUE, Step(both, EAST));

            // No handler answers: the blocker's opinion stands (dense: FALSE).
            var plain = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            plain.generic_canpass = FALSE;
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanPass(plain, WEST)), FALSE));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void PassFlagsAndBuckledMobsMakeCanAllowThroughTrueForADenseBlocker()
    {
        var map = World();
        using (map.World.Enter())
        {
            var block = Dm.@new<obj_test_movable_block>(locate(2, 1, 1));
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanAllowThrough(mover, WEST)), FALSE)); // !density

            // _atom.dm:249: mover.pass_flags & pass_flags_self
            block.pass_flags_self = PASSTABLE;
            mover.pass_flags = PASSTABLE;
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanAllowThrough(mover, WEST)), TRUE));
            mover.pass_flags = PASSGRILLE;
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanAllowThrough(mover, WEST)), FALSE));

            // atoms_movable.dm:1491: a mob buckled to the blocker may pass it.
            var rider = Dm.@new<mob_test_movable_mob>(locate(1, 2, 1));
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanAllowThrough(rider, WEST)), FALSE));
            block.buckled_mobs = Dm.list(rider);
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanAllowThrough(rider, WEST)), TRUE));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void EnterAsksCanPassOfTheTurfAndThenOfEachOccupantWithTheDirectionFromThemToTheMover()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var rug = Dm.@new<obj_test_movable_thing>(there);
            On(there, COMSIG_ATOM_TRIED_PASS, "turf_tried_pass");
            On(rug, COMSIG_ATOM_TRIED_PASS, "rug_tried_pass");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            // CanPass(mover, get_dir(src, mover)) on the turf (turf.dm:437) and, through Cross, on each occupant (:935-938):
            // from them towards the mover that arrives from the west, the border_dir is WEST (8).
            AssertLines(new[]
            {
                "turf_tried_pass " + D(there) + " " + D(mover) + " 8 null",
                "rug_tried_pass " + D(rug) + " " + D(mover) + " 8 null",
            }, map.Trace.Where(l => l.EndsWith(" 8 null")));
            Assert.Equal(2, Count(map.Trace, "turf_tried_pass") + Count(map.Trace, "rug_tried_pass"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- handlers that delete or move the mover from inside Enter (R4) ----

    [Fact]
    public void ACrossHandlerThatDeletesTheMoverEndsEnterWithoutABump()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var rug = Dm.@new<obj_test_movable_thing>(there);
            On(rug, COMSIG_MOVABLE_CROSS, "cross", COMPONENT_BLOCK_CROSS, act: (source, crossing) => qdel(crossing));
            On(rug, COMSIG_ATOM_BUMPED, "bumped");
            map.Trace.Clear();

            AssertNumber(FALSE, Step(mover, EAST)); // `if(QDELETED(mover)) return FALSE` after the failed Cross (turf.dm:443)

            Assert.True(QDELETED(mover));
            Assert.Null(mover.loc);
            Assert.Equal(0, Count(map.Trace, "bumped"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ABumpHandlerThatDeletesTheMoverEndsTheMoveWithFalseAndBumpedIsStillSent()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var block = Dm.@new<obj_test_movable_block>(there);
            On(mover, COMSIG_MOVABLE_BUMP, "bump", act: (source, bumped) => qdel(source));
            On(block, COMSIG_ATOM_BUMPED, "bumped");
            map.Trace.Clear();

            AssertNumber(FALSE, Step(mover, EAST));

            Assert.True(QDELETED(mover));
            Assert.Null(mover.loc);
            // Bump checks only QDELETED(bumped_atom) (:990), so Bumped goes out for a deleted mover.
            Assert.Equal(1, Count(map.Trace, "bumped"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TgQuirk_ABumpHandlerThatMovesTheMoverAwayLeavesItThereAndTheFailedMoveStillSetsLastMoveAndDirection()
    {
        var map = World();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1), far = locate(3, 3, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            var block = Dm.@new<obj_test_movable_block>(there);
            On(mover, COMSIG_MOVABLE_BUMP, "bump", act: (source, bumped) => Force((atom_movable)source, far));
            On(block, COMSIG_ATOM_BUMPED, "bumped");
            map.Trace.Clear();

            AssertNumber(FALSE, Step(mover, EAST));

            Assert.Same(far, mover.loc);
            // The handler's forceMove started and finished a whole movement inside Enter (one Moved), then def1 returned
            // FALSE; def2's bail needs loc == oldloc, which is false now, so its tail ran: last_move = direct.
            AssertNumber(EAST, mover.last_move);
            Assert.Equal(EAST, mover.dir);
            Assert.Equal(1, Count(map.Trace, "witness_moved"));
            Assert.Equal(1, Count(map.Trace, "bumped"));
            Assert.Null(mover.active_movement);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- Bump ----

    [Fact]
    public void BumpInterceptDoesNotSendBumpedAndAnyOtherReturnBitDoesNotIntercept()
    {
        var map = World();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var block = Dm.@new<obj_test_movable_block>(locate(2, 1, 1));
            On(block, COMSIG_ATOM_BUMPED, "bumped");
            On(mover, COMSIG_MOVABLE_BUMP, "bump", 2); // not COMPONENT_INTERCEPT_BUMPED
            map.Trace.Clear();

            Assert.Null(Waiting.NoSleep(mover.Bump(block)));
            Assert.Equal(new[] { "bump", "bumped" }, Kinds(map.Trace));

            var m2 = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            On(m2, COMSIG_MOVABLE_BUMP, "bump2", Dm.bitor(COMPONENT_INTERCEPT_BUMPED, 2));
            map.Trace.Clear();
            AssertNumber(TRUE, Waiting.NoSleep(m2.Bump(block)));
            Assert.Equal(new[] { "bump2" }, Kinds(map.Trace));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void BumpingADeletedAtomReturnsTrueWithoutSendingBumped()
    {
        var map = World();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var block = Dm.@new<obj_test_movable_block>(locate(2, 1, 1));
            On(block, COMSIG_ATOM_BUMPED, "bumped");
            On(mover, COMSIG_MOVABLE_BUMP, "bump", act: (source, bumped) => qdel(bumped));
            map.Trace.Clear();

            // `if(QDELETED(bumped_atom)) return TRUE` (:989-990); the signal listeners of the deleted block are gone anyway.
            AssertNumber(TRUE, Waiting.NoSleep(mover.Bump(block)));
            Assert.Equal(new[] { "bump" }, Kinds(map.Trace));
            MovableWorld.AssertNoRuntimes(map);
        }
    }
}
