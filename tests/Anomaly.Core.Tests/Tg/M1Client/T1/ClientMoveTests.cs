using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.M1Client.T1.Dirs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: tg's <c>client/Move</c> (code/modules/mob/mob_movement.dm:46-150) for a plain <c>/mob</c>, the branch a non-living avatar
/// takes: the move delay, the early returns and the non-living pre-move signal, then <c>mob.Move</c> and its signals in tg's
/// order (docs/ai/specs/move-chain.md 3a). The room's floor is x and y 2..15, the rim is wall.
/// </summary>
public class ClientMoveTests
{
    private static float N(object v) => Dm.num(v);

    [Fact]
    public void AMoveStepsTheMobOneTileAndReturnsTrue()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            object result = room.MoveCall(c, room.At(8, 9), North);
            Assert.True(Dm.truthy(result));
            Assert.Same(room.At(8, 9), avatar.loc);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMobMovesOnlyOncePerTickBecauseMoveDelayIsOneTickAhead()
    {
        // :47-48  if(world.time < move_delay) return FALSE;  :51  move_delay = world.time + world.tick_lag
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            Assert.True(Dm.truthy(room.MoveCall(c, room.At(8, 9), North)));
            Assert.Equal(world.time + world.tick_lag, c.move_delay);
            Assert.False(Dm.truthy(room.MoveCall(c, room.At(8, 10), North))); // the same tick
            Assert.Same(room.At(8, 9), avatar.loc);
        });
        room.Step();
        room.In(() =>
        {
            Assert.True(Dm.truthy(room.MoveCall(c, room.At(8, 10), North))); // the next tick
            Assert.Same(room.At(8, 10), avatar.loc);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ARefusedMoveBeforeTheDelayLeavesTheDirectionBookkeepingUntouched()
    {
        // :47-48 is the first line: it returns before :49-50 clear next_move_dir_add and _sub
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            c.move_delay = world.time + 100;
            c.next_move_dir_add = East;
            c.next_move_dir_sub = West;
            Assert.False(Dm.truthy(room.MoveCall(c, room.At(8, 9), North)));
            Assert.Equal(East, (int)N(c.next_move_dir_add));
            Assert.Equal(West, (int)N(c.next_move_dir_sub));
            Assert.Equal(world.time + 100, c.move_delay);
        });
    }

    [Fact]
    public void AMoveThatGetsPastTheDelayClearsTheDirectionBookkeepingAndSetsTheDelay()
    {
        // :49-51
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            c.next_move_dir_add = East;
            c.next_move_dir_sub = West;
            room.MoveCall(c, room.At(8, 9), North);
            Assert.Equal(0f, N(c.next_move_dir_add));
            Assert.Equal(0f, N(c.next_move_dir_sub));
        });
    }

    [Fact]
    public void TgQuirkAMoveWithoutADirectionReturnsFalseButStillSpendsTheTickAndClearsTheBookkeeping()
    {
        // :49-53: add, sub and move_delay are written before `if(!direct || !new_loc) return FALSE`
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            c.next_move_dir_add = East;
            c.next_move_dir_sub = West;
            Assert.False(Dm.truthy(room.MoveCall(c, room.At(8, 9), 0)));
            Assert.Equal(0f, N(c.next_move_dir_add));
            Assert.Equal(0f, N(c.next_move_dir_sub));
            Assert.Equal(world.time + world.tick_lag, c.move_delay);
            Assert.Same(room.At(8, 8), avatar.loc);
            // and a real move in the same tick is refused now
            Assert.False(Dm.truthy(room.MoveCall(c, room.At(8, 9), North)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMoveWithoutATargetLocReturnsFalseAndStillSpendsTheTick()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            Assert.False(Dm.truthy(room.MoveCall(c, null, North)));
            Assert.Equal(world.time + world.tick_lag, c.move_delay);
            Assert.Same(room.At(8, 8), avatar.loc);
        });
    }

    [Fact]
    public void AMobWithoutALocDoesNotMove()
    {
        // :54-55  if(!mob?.loc) return FALSE
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            Waiting.NoSleep(avatar.moveToNullspace());
            l.Trace.Clear(); // moveToNullspace's own Moved signal
            return l;
        });
        room.Step(); // let SSinput run: a client without a loc and without keys is no error
        room.In(() =>
        {
            Assert.Null(avatar.loc);
            Assert.False(Dm.truthy(room.MoveCall(c, room.At(8, 9), North)));
            Assert.Equal(world.time + world.tick_lag, c.move_delay);
            Assert.Null(avatar.loc);
        });
        Assert.Empty(listener.Trace); // the non-living signal is not reached
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheNoTransformTraitStopsTheMobAndTheNonLivingSignalIsNotSent()
    {
        // :56-57  if(HAS_TRAIT(mob, TRAIT_NO_TRANSFORM)) return FALSE
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            ADD_TRAIT(avatar, TRAIT_NO_TRANSFORM, "t1");
            return l;
        });
        room.In(() => Assert.False(Dm.truthy(room.MoveCall(c, room.At(8, 9), North))));
        room.Step();
        room.In(() =>
        {
            Assert.Same(room.At(8, 8), avatar.loc);
            REMOVE_TRAIT(avatar, TRAIT_NO_TRANSFORM, "t1");
        });
        Assert.Empty(listener.Trace);
        room.Step();
        room.In(() => Assert.True(Dm.truthy(room.MoveCall(c, room.At(8, 9), North))));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void HeldKeysAgainstTheNoTransformTraitDoNotMoveTheMobEvenAfterManyTicks()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => ADD_TRAIT(avatar, TRAIT_NO_TRANSFORM, "t1"));
        room.Act(() => room.Down(c, "W"));
        room.Step(6);
        room.In(() => Assert.Same(room.At(8, 8), avatar.loc));
        room.In(() => REMOVE_TRAIT(avatar, TRAIT_NO_TRANSFORM, "t1"));
        room.Step(); // the very next fire: the key is still held
        room.In(() => Assert.Same(room.At(8, 9), avatar.loc));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheNonLivingPreMoveSignalCarriesTheNewLocAndTheDirection()
    {
        // :58-60  SEND_SIGNAL(mob, COMSIG_MOB_CLIENT_PRE_NON_LIVING_MOVE, new_loc, direct)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            return l;
        });
        room.In(() => room.MoveCall(c, room.At(9, 8), East));
        Assert.Equal("client_pre_move 9,8 4", listener.Trace[0]);
    }

    [Fact]
    public void ABlockingPreMoveHandlerRefusesTheMoveBeforeTheMobIsAskedToMove()
    {
        // :58-59  if(SEND_SIGNAL(...) & COMSIG_MOB_CLIENT_BLOCK_PRE_NON_LIVING_MOVE) return FALSE
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            l.PreNonLivingReturn = (int)COMSIG_MOB_CLIENT_BLOCK_PRE_NON_LIVING_MOVE;
            return l;
        });
        room.In(() =>
        {
            Assert.False(Dm.truthy(room.MoveCall(c, room.At(8, 9), North)));
            Assert.Same(room.At(8, 8), avatar.loc);
            Assert.Equal(world.time + world.tick_lag, c.move_delay);
        });
        Assert.Equal(new[] { "client_pre_move 8,9 1" }, listener.Trace); // no attempted_move: mob.Move was not called
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AHandlerThatReturnsNothingBlocksNothing()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            return l;
        });
        room.In(() => Assert.True(Dm.truthy(room.MoveCall(c, room.At(8, 9), North))));
    }

    [Fact]
    public void ACardinalMoveSendsTheMovementSignalsInTgsOrder()
    {
        // client/Move sends PRE_NON_LIVING_MOVE first (:58); /atom/movable/Move (atoms_movable.dm:687-760) then sends
        // ATTEMPTED_MOVE, turns the mob (setDir: DIR_CHANGE), asks the old turf to Exit, the new one to Enter, sends PRE_MOVE,
        // does `loc = newloc`, then oldloc.Exited, newloc.Entered and Moved (MOVED)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            l.WatchTurf(room.At(8, 8));
            l.WatchTurf(room.At(8, 9));
            return l;
        });
        room.In(() =>
        {
            Assert.Equal(2f, avatar.dir); // a new mob faces SOUTH
            room.MoveCall(c, room.At(8, 9), North);
        });
        Assert.Equal(new[]
        {
            "client_pre_move 8,9 1",
            "attempted_move 8,9 1",
            "dir_change 2 1",
            "exit 8,8",
            "pre_move 8,9",
            "exited 8,8",
            "entered 8,9",
            "moved 8,8 1",
        }, listener.Trace);
        room.In(() => Assert.Equal(1f, avatar.dir));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMoveInTheDirectionTheMobAlreadyFacesSendsNoDirChange()
    {
        // atoms_movable.dm:697  if(set_dir_on_move && dir != direction && update_dir) setDir(direction)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => room.MoveCall(c, room.At(8, 9), North));
        room.Step();
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            return l;
        });
        room.In(() => room.MoveCall(c, room.At(8, 10), North));
        Assert.Equal(new[]
        {
            "client_pre_move 8,10 1",
            "attempted_move 8,10 1",
            "pre_move 8,10",
            "moved 8,9 1",
        }, listener.Trace);
    }

    [Fact]
    public void ADiagonalMoveIsTwoCardinalStepsInTgsOrderNorthFirstForANorthEastMove()
    {
        // atoms_movable.dm:773-820: NORTH then EAST for NORTHEAST (step(src, NORTH) && moving_diagonally, then step(src, EAST)),
        // each a full Move of the chain; client/Move sends its own signal once for the diagonal
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            l.WatchTurf(room.At(8, 8));
            l.WatchTurf(room.At(8, 9));
            l.WatchTurf(room.At(9, 9));
            return l;
        });
        room.In(() => Assert.True(Dm.truthy(room.MoveCall(c, room.At(9, 9), North | East))));
        Assert.Equal(new[]
        {
            "client_pre_move 9,9 5",
            "attempted_move 8,9 1",
            "dir_change 2 1",
            "exit 8,8",
            "pre_move 8,9",
            "exited 8,8",
            "entered 8,9",
            "moved 8,8 1",
            "attempted_move 9,9 4",
            "dir_change 1 4",
            "exit 8,9",
            "pre_move 9,9",
            "exited 8,9",
            "entered 9,9",
            "moved 8,9 4",
        }, listener.Trace);
        room.In(() => Assert.Equal(East, (int)avatar.dir));
        room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(South | West, "8,7", "7,7")] // SOUTH first, then WEST
    [InlineData(South | East, "8,7", "9,7")]
    [InlineData(North | West, "8,9", "7,9")]
    public void TheOrderOfTheTwoStepsOfADiagonalFollowsTgsBranches(int diagonal, string first, string second)
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            return l;
        });
        int dx = (diagonal & East) != 0 ? 1 : -1;
        int dy = (diagonal & North) != 0 ? 1 : -1;
        room.In(() => room.MoveCall(c, room.At(8 + dx, 8 + dy), diagonal));
        Assert.Equal(new[] { "pre_move " + first, "pre_move " + second }, listener.Trace.Where(t => t.StartsWith("pre_move", StringComparison.Ordinal)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADiagonalWithOneBlockedAxisSlidesAlongTheOtherAndReturnsFalse()
    {
        // At (8,15) the wall is at y = 16. NORTHEAST: step(NORTH) fails, so `else if (moving_diagonally && step(src, EAST))`
        // takes (9,15); then `. = step(src, NORTH)` fails again: `.` is FALSE although the mob moved, and since `!.`
        // the mob is turned to the first successful step's direction (:812-813).
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester", 8, 15);
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            return l;
        });
        room.In(() => Assert.False(Dm.truthy(room.MoveCall(c, room.At(9, 16), North | East))));
        Assert.Equal(new[]
        {
            "client_pre_move 9,16 5",
            "attempted_move 8,16 1",
            "dir_change 2 1",
            "attempted_move 9,15 4",
            "dir_change 1 4",
            "pre_move 9,15",
            "moved 8,15 4",
            "attempted_move 9,16 1",
            "dir_change 4 1",
            "dir_change 1 4",
        }, listener.Trace);
        room.In(() =>
        {
            Assert.Same(room.At(9, 15), avatar.loc);
            Assert.Equal(East, (int)avatar.dir);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AWallStopsTheMobButTheMobTurnsToFaceIt()
    {
        // atoms_movable.dm:696-698 turns the mob before Exit and Enter, so a refused move still changes dir; client/Move returns
        // mob.Move's FALSE and has already spent the tick
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester", 8, 15);
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            return l;
        });
        room.In(() =>
        {
            Assert.False(Dm.truthy(room.MoveCall(c, room.At(8, 16), North)));
            Assert.Same(room.At(8, 15), avatar.loc);
            Assert.Equal(North, (int)avatar.dir);
            Assert.Equal(world.time + world.tick_lag, c.move_delay);
        });
        Assert.Equal(new[] { "client_pre_move 8,16 1", "attempted_move 8,16 1", "dir_change 2 1" }, listener.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AHeldKeyIntoAWallKeepsCallingMoveEveryTickAndTheMobStaysPut()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester", 8, 15);
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            return l;
        });
        room.Act(() => room.Down(c, "W"));
        room.Step(5); // fires at ticks 2 to 5: four attempts
        room.In(() => Assert.Same(room.At(8, 15), avatar.loc));
        Assert.Equal(4, listener.Trace.Count(t => t.StartsWith("client_pre_move", StringComparison.Ordinal)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void MoveDelayOfAPlainMobIsExactlyOneTickAndNoSlowdownIsAdded()
    {
        // The non-living branch returns at :60 before the movement-delay arithmetic (:98-113)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => room.Down(c, "W"));
        room.Step(2);
        room.In(() =>
        {
            Assert.Equal(world.time + world.tick_lag, c.move_delay); // set by the fire of this very tick
        });
        float before = room.In(() => c.move_delay);
        room.Step(3);
        room.In(() => Assert.Equal(before + 3 * world.tick_lag, c.move_delay));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AHeldKeyMovesOneTilePerTickForTenTicks()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester", 8, 3);
        room.Act(() => room.Down(c, "W"));
        room.Step(1 + 10); // the press and ten fires
        room.In(() => Assert.Equal("8,13", T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }
}
