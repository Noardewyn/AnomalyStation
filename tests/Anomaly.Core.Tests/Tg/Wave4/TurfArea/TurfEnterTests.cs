using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// /turf/Enter (code/game/turfs/turf.dm:434-466), the golden T4 of move-chain.md section 11 (Exit veto, Enter failing plus
/// Bump, PHASING, PRE_MOVE block, Cross block), from the DM and move-chain.md 3b. Written before package A's Move, Bump and
/// Cross were merged (they are in game/atoms_movable.cs now), so these tests do not use them: the movers are fixtures that
/// record Bump (obj_test_turfarea_probe), the occupants carry an ADAPTED copy of tg's /atom/movable/Cross
/// (obj_test_turfarea_occupant; the copy lets a test script the exceptions), and Enter is called directly. The Move-based
/// goldens (the order of Exit, Enter, PRE_MOVE, loc, Exited, Entered, Moved around these calls) are W4.M's, in Integration/MoveChainT1ToT3Tests.cs and MoveChainT4Tests.cs.
/// </summary>
public class TurfEnterTests
{
    // A 3x1 strip of floor: the mover starts at (1,1,1) and tries to enter (2,1,1).
    private sealed class Strip
    {
        public readonly TurfAreaWorld T = new(3, 1, 1);
        public turf From => locate(1, 1, 1);
        public turf To => locate(2, 1, 1);
        public obj_test_turfarea_probe Mover;
        public List<string> Lines => TurfAreaTrace.Of(T.World).Lines;
    }

    private static Strip NewStrip(Action<Strip> arrange = null)
    {
        var s = new Strip();
        using (s.T.World.Enter())
        {
            s.Mover = Dm.@new<obj_test_turfarea_probe>(s.From);
            s.Mover.name = "mover";
            arrange?.Invoke(s);
        }
        return s;
    }

    private static obj_test_turfarea_occupant Occupant(string name, turf at, float cross_result = -1, float layer = 4)
    {
        var o = Dm.@new<obj_test_turfarea_occupant>(at);
        o.name = name;
        o.cross_result = cross_result;
        o.layer = layer;
        return o;
    }

    private static object Enter(Strip s) => Waiting.NoSleep(s.To.Enter(s.Mover, s.From));

    // ---- the call forms the Move chain will use ----

    [Fact]
    public void EnterAndTheAreaProcsAreReachableByNameThroughTheBinderWithTgsArguments()
    {
        // `newloc.Enter(src)` has one argument in tg (turf.dm:434) and `area/Entered(arrived, old_area)` two (areas.dm:581):
        // the dynamic call by name (call(), CALLBACK, a typed call through an untyped var) must find them
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var blocker = Occupant("blocker", s.To, cross_result: 0);
            Assert.False(Dm.truthy(Waiting.NoSleep(Dm.Call(s.To, "Enter", s.Mover))));
            Assert.Equal(new atom[] { blocker }, s.Mover.bumped);

            var room = (area)s.To.loc;
            var from = Dm.@new<area_test_turfarea_common>();
            var watch = Dm.@new<datum_test_turfarea_listener>();
            watch.label = "area_entered";
            watch.RegisterSignal(room, COMSIG_AREA_ENTERED, nameof(datum_test_turfarea_listener.on_any));
            Waiting.NoSleep(Dm.Call(room, "Entered", s.Mover, from));
            Assert.Equal("area_entered area:area_misc_testroom(mover,area:area_test_turfarea_common,null,null)", s.Lines.Single(l => l.StartsWith("area_entered")));
            Assert.Empty(s.T.Runtimes);
        }
    }

    // ---- no obstacle ----

    [Fact]
    public void AnEmptyOpenTurfLetsTheMoverInWithoutBumping()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            Assert.True(Dm.eq(Enter(s), TRUE));
            Assert.Empty(s.Mover.bumped);
            Assert.Empty(s.T.Runtimes);
        }
    }

    [Fact]
    public void EnterSendsNeitherEnteredNorPreMoveNorAnyOtherMovementSignal()
    {
        // Enter only asks; Entered, Exited, PRE_MOVE and Moved belong to Move (move-chain.md 3a)
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var watch = Dm.@new<datum_test_turfarea_listener>();
            watch.label = "sig";
            watch.RegisterSignal(s.To, COMSIG_ATOM_ENTERED, nameof(datum_test_turfarea_listener.on_any));
            watch.RegisterSignal(s.Mover, COMSIG_ATOM_ENTERING, nameof(datum_test_turfarea_listener.on_any));
            watch.RegisterSignal(s.Mover, COMSIG_MOVABLE_MOVED, nameof(datum_test_turfarea_listener.on_any));
            watch.returns = COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
            watch.RegisterSignal(s.Mover, COMSIG_MOVABLE_PRE_MOVE, nameof(datum_test_turfarea_listener.on_any));

            // a PRE_MOVE block does not stop Enter: the signal comes after it, in Move (:734 vs :741)
            Assert.True(Dm.eq(Enter(s), TRUE));
            Assert.DoesNotContain(s.Lines, l => l.StartsWith("sig"));
            Assert.Same(s.From, s.Mover.loc);
            Assert.Empty(s.T.Runtimes);
        }
    }

    // ---- the turf itself refuses ----

    [Fact]
    public void ADenseTurfBumpsTheMoverAndAnswersFalse()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            Assert.False(Dm.truthy(Waiting.NoSleep(wall.Enter(s.Mover, s.From))));
            Assert.Equal(new atom[] { wall }, s.Mover.bumped);
            Assert.Empty(s.T.Runtimes);
        }
    }

    [Fact]
    public void ADenseTurfDoesNotAskItsOccupantsToCross()
    {
        // turf.dm:441: the occupants are only asked when the turf itself lets the mover pass
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            var inside = Occupant("inside", wall, cross_result: 0, layer: 99);
            Waiting.NoSleep(wall.Enter(s.Mover, s.From));
            Assert.Empty(inside.crossed);
            // "Even if mover is unstoppable they need to bump us": the turf is the bump, not the high-layer occupant
            Assert.Equal(new atom[] { wall }, s.Mover.bumped);
        }
    }

    [Fact]
    public void AMoverWithTheTurfsPassFlagGoesThroughADenseTurf()
    {
        // _atom.dm:243-246 via CanPass: mover.pass_flags & pass_flags_self, and /turf/closed has PASSCLOSEDTURF (_closed.dm:9)
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            s.Mover.pass_flags = PASSCLOSEDTURF;
            Assert.True(Dm.eq(Waiting.NoSleep(wall.Enter(s.Mover, s.From)), TRUE));
            Assert.Empty(s.Mover.bumped);
        }
    }

    [Fact]
    public void ASignalHandlerCanPermitPassageThroughADenseTurf()
    {
        // _atom.dm:229: COMSIG_ATOM_TRIED_PASS with COMSIG_COMPONENT_PERMIT_PASSAGE
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            var permit = Dm.@new<datum_test_turfarea_listener>();
            permit.label = "tried_pass";
            permit.returns = COMSIG_COMPONENT_PERMIT_PASSAGE;
            permit.RegisterSignal(wall, COMSIG_ATOM_TRIED_PASS, nameof(datum_test_turfarea_listener.on_any));
            Assert.True(Dm.eq(Waiting.NoSleep(wall.Enter(s.Mover, s.From)), TRUE));
            Assert.Empty(s.Mover.bumped);
            Assert.Single(s.Lines, l => l.StartsWith("tried_pass"));
        }
    }

    // ---- Exit veto ----

    [Fact]
    public void ATurfExitIsRefusedByACOMSIG_ATOM_EXITHandlerThatBlocks()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            // no handler: the turf lets the mover leave (_atom.dm:635-638); the first half of the chain's Exit then Enter
            Assert.True(Dm.eq(Waiting.NoSleep(s.From.Exit(s.Mover, EAST)), TRUE));

            var veto = Dm.@new<datum_test_turfarea_listener>();
            veto.label = "exit";
            veto.returns = COMPONENT_ATOM_BLOCK_EXIT;
            veto.RegisterSignal(s.From, COMSIG_ATOM_EXIT, nameof(datum_test_turfarea_listener.on_any));
            Assert.True(Dm.eq(Waiting.NoSleep(s.From.Exit(s.Mover, EAST)), FALSE));
            Assert.Equal("exit turf(1,1,1)(mover," + Dm.str(EAST) + ",null,null)", s.Lines.Single(l => l.StartsWith("exit")));

            // a handler that does not block does not veto
            veto.returns = NONE;
            Assert.True(Dm.eq(Waiting.NoSleep(s.From.Exit(s.Mover, EAST)), TRUE));
            Assert.Empty(s.T.Runtimes);
        }
    }

    // ---- an occupant refuses ----

    [Fact]
    public void ARefusingOccupantIsBumpedAndEnterAnswersFalse()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var blocker = Occupant("blocker", s.To, cross_result: 0);
            Assert.False(Dm.truthy(Enter(s)));
            Assert.Equal(new atom[] { blocker }, s.Mover.bumped);
            Assert.Equal(new[] { (atom_movable)s.Mover }, blocker.crossed);
            Assert.Empty(s.T.Runtimes);
        }
    }

    [Fact]
    public void EveryOccupantIsAskedToCrossInContentsOrderEvenAfterOneRefused()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            Occupant("a", s.To, cross_result: 1);
            Occupant("b", s.To, cross_result: 0);
            Occupant("c", s.To, cross_result: 1);
            Enter(s);
            Assert.Equal(new[] { "Cross a <- mover", "Cross b <- mover", "Cross c <- mover" }, s.Lines.Where(l => l.StartsWith("Cross")));
        }
    }

    [Fact]
    public void TheRefusingOccupantWithTheHighestLayerIsTheOneBumped()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            Occupant("low", s.To, cross_result: 0, layer: 3);
            var high = Occupant("high", s.To, cross_result: 0, layer: 5);
            Occupant("middle", s.To, cross_result: 0, layer: 4);
            Enter(s);
            Assert.Equal(new atom[] { high }, s.Mover.bumped);
        }
    }

    [Fact]
    public void OnEqualLayersTheEarlierOccupantStaysTheBump()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var first = Occupant("first", s.To, cross_result: 0, layer: 4);
            Occupant("second", s.To, cross_result: 0, layer: 4);
            Enter(s);
            Assert.Equal(new atom[] { first }, s.Mover.bumped);
        }
    }

    [Fact]
    public void AnOccupantThatAllowsTheMoverIsNeverTheBumpEvenWithTheHighestLayer()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var refuses = Occupant("refuses", s.To, cross_result: 0, layer: 2);
            Occupant("allows", s.To, cross_result: 1, layer: 9);
            Enter(s);
            Assert.Equal(new atom[] { refuses }, s.Mover.bumped);
        }
    }

    [Fact]
    public void ABorderOccupantBeatsAnyLayerAndTheFirstBorderOccupantStays()
    {
        // turf.dm:458: (thing.layer > first.layer || thing.flags_1 & ON_BORDER_1) && !(first.flags_1 & ON_BORDER_1)
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var tall = Occupant("tall", s.To, cross_result: 0, layer: 9);
            var border1 = Occupant("border1", s.To, cross_result: 0, layer: 1);
            border1.flags_1 = ON_BORDER_1;
            var border2 = Occupant("border2", s.To, cross_result: 0, layer: 8);
            border2.flags_1 = ON_BORDER_1;
            var taller = Occupant("taller", s.To, cross_result: 0, layer: 20);
            Enter(s);
            // the border object replaced the tall one, and neither the later border object nor the taller one replaced it
            Assert.Equal(new atom[] { border1 }, s.Mover.bumped);
            Assert.NotSame(tall, border1);
            Assert.NotSame(taller, border2);
        }
    }

    [Fact]
    public void ALaterBorderOccupantReplacesAnEarlierNonBorderOne()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            Occupant("tall", s.To, cross_result: 0, layer: 9);
            var border = Occupant("border", s.To, cross_result: 0, layer: 1);
            border.flags_1 = ON_BORDER_1;
            Enter(s);
            Assert.Equal(new atom[] { border }, s.Mover.bumped);
        }
    }

    [Fact]
    public void AnOccupantsOwnCrossRulesDecideThroughCanPass()
    {
        // tg's Cross ends in CanPass(mover, dir): a dense occupant refuses, a flat one lets the mover by (through the occupant fixture's copy of Cross)
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var flat = Occupant("flat", s.To);
            Assert.True(Dm.eq(Enter(s), TRUE));
            Assert.Empty(s.Mover.bumped);
            flat.density = TRUE;
            Assert.False(Dm.truthy(Enter(s)));
            Assert.Equal(new atom[] { flat }, s.Mover.bumped);
        }
    }

    [Fact]
    public void ACrossBlockedByACOMSIG_MOVABLE_CROSSHandlerRefusesTheMover()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var flat = Occupant("flat", s.To);   // not dense: CanPass would allow it
            var block = Dm.@new<datum_test_turfarea_listener>();
            block.label = "cross";
            block.returns = COMPONENT_BLOCK_CROSS;
            block.RegisterSignal(flat, COMSIG_MOVABLE_CROSS, nameof(datum_test_turfarea_listener.on_any));
            Assert.False(Dm.truthy(Enter(s)));
            Assert.Equal(new atom[] { flat }, s.Mover.bumped);
            Assert.Equal("cross flat(mover,null,null,null)", s.Lines.Single(l => l.StartsWith("cross")));
        }
    }

    [Fact]
    public void ACrossBlockedFromTheMoversSideByCOMSIG_MOVABLE_CROSS_OVERRefusesToo()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var flat = Occupant("flat", s.To);
            var block = Dm.@new<datum_test_turfarea_listener>();
            block.label = "cross_over";
            block.returns = COMPONENT_BLOCK_CROSS;
            block.RegisterSignal(s.Mover, COMSIG_MOVABLE_CROSS_OVER, nameof(datum_test_turfarea_listener.on_any));
            Assert.False(Dm.truthy(Enter(s)));
            Assert.Equal(new atom[] { flat }, s.Mover.bumped);
            Assert.Equal("cross_over mover(flat,null,null,null)", s.Lines.Single(l => l.StartsWith("cross_over")));
        }
    }

    [Fact]
    public void TheMoverAndTheContainerItLeavesAreNeverAskedToCross()
    {
        // turf.dm:444-445: "Multi tile objects and moving out of other objects"
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            // a mover that is already on the target turf (a multi tile object stepping within its own turf)
            var self = Occupant("self", s.To, cross_result: 0);
            Assert.True(Dm.eq(Waiting.NoSleep(s.To.Enter(self, s.From)), TRUE));
            Assert.Empty(self.crossed);
            self.cross_result = 1;   // the second case is about the locker only

            // a mover inside a container that stands on the target turf (moving out of a locker onto its own tile)
            var locker = Occupant("locker", s.To, cross_result: 0);
            var inside = Dm.@new<obj_test_turfarea_probe>(locker);
            inside.name = "inside";
            Assert.True(Dm.eq(Waiting.NoSleep(s.To.Enter(inside, locker)), TRUE));
            Assert.Empty(locker.crossed);
            Assert.Empty(inside.bumped);
            Assert.Empty(s.T.Runtimes);
        }
    }

    // ---- PHASING ----

    [Fact]
    public void APhasingMoverBumpsEveryRefusingOccupantInOrderAndStillGetsIn()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var a = Occupant("a", s.To, cross_result: 0, layer: 3);
            Occupant("b", s.To, cross_result: 1, layer: 9);
            var c = Occupant("c", s.To, cross_result: 0, layer: 7);
            s.Mover.movement_type = PHASING;

            // turf.dm:446-452: no first_bump is kept for a phasing mover, each refusing thing is bumped as it is met
            Assert.True(Dm.eq(Enter(s), TRUE));
            Assert.Equal(new atom[] { a, c }, s.Mover.bumped);
            Assert.Empty(s.T.Runtimes);
        }
    }

    [Fact]
    public void APhasingMoverDoesNotBumpADenseTurf()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            s.Mover.movement_type = Dm.bitor(GROUND, PHASING);
            Assert.True(Dm.eq(Waiting.NoSleep(wall.Enter(s.Mover, s.From)), TRUE));
            Assert.Empty(s.Mover.bumped);
        }
    }

    [Fact]
    public void APhasingMoverThatBumpingDeletesStopsTheLoopAndEnterAnswersFalse()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var a = Occupant("a", s.To, cross_result: 0);
            var b = Occupant("b", s.To, cross_result: 0);
            s.Mover.movement_type = PHASING;
            s.Mover.qdel_on_bump = TRUE;
            Assert.False(Dm.truthy(Enter(s)));
            Assert.Equal(new atom[] { a }, s.Mover.bumped);   // turf.dm:450-451: deleted from Bump(), return at once
            Assert.Empty(b.crossed);
            Assert.True(QDELETED(s.Mover));
        }
    }

    [Fact]
    public void ThePhasingBitIsReadOnceBeforeTheLoop()
    {
        // turf.dm:443 caches `mover.movement_type & PHASING`: a Cross that strips PHASING half way does not change how the
        // refusing occupants after it are treated
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var first = Occupant("first", s.To);   // tg's Cross, and not dense: it allows the mover
            var refuses = Occupant("refuses", s.To, cross_result: 0);
            s.Mover.movement_type = PHASING;
            var hook = Dm.@new<datum_test_turfarea_set_movement_type>();
            hook.mover = s.Mover;
            hook.to = GROUND;
            hook.RegisterSignal(first, COMSIG_MOVABLE_CROSS, nameof(datum_test_turfarea_set_movement_type.set_it));

            Assert.True(Dm.eq(Enter(s), TRUE));
            Assert.True(Dm.eq(s.Mover.movement_type, GROUND));
            Assert.Equal(new atom[] { refuses }, s.Mover.bumped);   // bumped as a phasing mover's obstacle, no first_bump
            Assert.Empty(s.T.Runtimes);
        }
    }

    [Fact]
    public void TgQuirkAMoverThatGainsPhasingInTheLoopIsBumpedOnceAndThenLetThrough()
    {
        // not phasing when the loop starts: the refusing occupant becomes first_bump; the answer (:464) reads
        // movement_type again, finds PHASING and Enter answers truthy "after bumping" (move-chain.md 3b)
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var first = Occupant("first", s.To);
            var refuses = Occupant("refuses", s.To, cross_result: 0);
            var hook = Dm.@new<datum_test_turfarea_set_movement_type>();
            hook.mover = s.Mover;
            hook.to = PHASING;
            hook.RegisterSignal(first, COMSIG_MOVABLE_CROSS, nameof(datum_test_turfarea_set_movement_type.set_it));

            object answer = Enter(s);

            Assert.True(Dm.truthy(answer));
            Assert.True(Dm.eq(answer, PHASING));
            Assert.Equal(new atom[] { refuses }, s.Mover.bumped);
            Assert.Empty(s.T.Runtimes);
        }
    }

    // ---- the mover is deleted ----

    [Fact]
    public void AMoverDeletedByACrossThatRefusesIsNotBumpedAndEnterAnswersFalse()
    {
        // DV2: QDELETED is gc_destroyed != null, so a qdel at world time 0 counts (tg reads gc_destroyed's truthiness)
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var executioner = Occupant("executioner", s.To, cross_result: 0);
            executioner.qdel_crossing = TRUE;
            var after = Occupant("after", s.To, cross_result: 0);
            Assert.Equal(0, s.T.World.Time);
            Assert.False(Dm.truthy(Enter(s)));
            Assert.Empty(s.Mover.bumped);    // turf.dm:447-448
            Assert.Empty(after.crossed);     // and the loop ended
            Assert.True(QDELETED(s.Mover));
        }
    }

    [Fact]
    public void TgQuirkAMoverDeletedByACrossThatAllowsIsOnlyNoticedAfterTheLoop()
    {
        // turf.dm:446-457 checks QDELETED only inside the refusal branch: a Cross that deletes and returns TRUE lets the
        // loop go on asking the other occupants, and the check at :459 then answers FALSE without a Bump
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var executioner = Occupant("executioner", s.To, cross_result: 1);
            executioner.qdel_crossing = TRUE;
            var after = Occupant("after", s.To, cross_result: 0);
            Assert.False(Dm.truthy(Enter(s)));
            Assert.Equal(new[] { (atom_movable)s.Mover }, after.crossed);
            Assert.Empty(s.Mover.bumped);
            Assert.True(QDELETED(s.Mover));
        }
    }

    [Fact]
    public void ADenseTurfDoesNotBumpAMoverThatIsAlreadyDeleted()
    {
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            qdel(s.Mover);
            Assert.False(Dm.truthy(Waiting.NoSleep(wall.Enter(s.Mover, s.From))));
            Assert.Empty(s.Mover.bumped);    // turf.dm:459-460 comes before first_bump = src
        }
    }

    [Fact]
    public void EnterAnswersWithThePhasingBitWhenSomeOccupantWasBumped()
    {
        // turf.dm:462-464: `return (mover.movement_type & PHASING)`: numeric 0 for a non-phasing mover that was bumped
        var s = NewStrip();
        using (s.T.World.Enter())
        {
            Occupant("blocker", s.To, cross_result: 0);
            object answer = Enter(s);
            Assert.True(Dm.eq(answer, 0));
            Assert.False(Dm.truthy(answer));
        }
    }
}

/// <summary>A handler that sets a mover's movement_type (a Cross handler that changes how the mover is treated).</summary>
[DmPath("/datum/test_turfarea_set_movement_type")]
public class datum_test_turfarea_set_movement_type : datum
{
    public atom_movable mover;
    public float to;

    [SignalHandler]
    public virtual object set_it(datum source = null, object crossed = null)
    {
        mover.movement_type = to;
        return NONE;
    }
}
