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
/// T1: SSinput (code/controllers/subsystem/input.dm) and <c>/atom/movable/keyLoop</c> (code/modules/keybindings/bindings_atom.dm):
/// the ticker that calls <c>keyLoop</c> of every mob of <c>GLOB.keyloop_list</c> with the mob's client, what <c>keyLoop</c> does
/// with <c>client.dir</c>, the movement lock and a null focus, and the running average the subsystem keeps.
/// </summary>
public class KeyLoopAndSSInputTests
{
    private static float N(object v) => Dm.num(v);

    // ---- the subsystem ----

    [Fact]
    public void SSinputIsAnInitializedTickerThatFiresEveryTickWithTgsDefaultMacroSet()
    {
        // input.dm:1-8 (name, init stage, SS_TICKER flag, priority, runlevels) and :24-41 (Initialize and the macro sets)
        var room = new T1Room();
        room.In(() =>
        {
            Assert.Equal("Input", SSinput.name);
            Assert.True(Dm.truthy(SSinput.initialized));
            Assert.Equal(SS_TICKER, SSinput.ss_flags);
            Assert.True(Dm.eq(SSinput.priority, FIRE_PRIORITY_INPUT));
            Assert.True(Dm.eq(SSinput.init_stage, INITSTAGE_EARLY));
            Assert.Equal(Dm.bitor(RUNLEVELS_DEFAULT, RUNLEVEL_LOBBY), SSinput.runlevels);
            Assert.Equal(5f, SSinput.macro_set.len);
            Assert.Equal(new[] { "Any", "Any+UP", "Back", "Tab", "Escape" }, Dm.each(SSinput.macro_set).Select(k => Dm.str(k)).ToArray());
            Assert.False(Dm.truthy(SSinput.use_default_stats));
        });
        long before = room.In(() => (long)SSinput.times_fired);
        room.Step(5);
        Assert.Equal(before + 5, room.In(() => (long)SSinput.times_fired));
    }

    [Fact]
    public void SSverbManagerIsATickerOfItsOwnThatWaitsOneTick()
    {
        // verb_manager.dm: SUBSYSTEM_DEF(verb_manager): wait = 1, SS_TICKER | SS_NO_INIT, RUNLEVEL_LOBBY | RUNLEVELS_DEFAULT
        var room = new T1Room();
        room.In(() =>
        {
            Assert.Equal(1f, SSverb_manager.wait);
            Assert.Equal(1f, SSinput.wait); // inherited
            Assert.Equal(Dm.bitor(SS_TICKER, SS_NO_INIT), SSverb_manager.ss_flags);
            Assert.Equal(Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT), SSverb_manager.runlevels);
            Assert.Equal(0f, SSverb_manager.verb_queue.len);
            Assert.False(Dm.truthy(SSverb_manager.can_queue_admin_verbs));
            Assert.False(Dm.truthy(SSverb_manager.always_queue));
        });
        long before = room.In(() => (long)SSverb_manager.times_fired);
        room.Step(3);
        Assert.Equal(before + 3, room.In(() => (long)SSverb_manager.times_fired));
    }

    // ---- the loop over GLOB.keyloop_list ----

    [Fact]
    public void KeyLoopRunsForEveryClientMobInTheOrderOfTheirLogin()
    {
        // input.dm:70-72  for(var/mob/user in GLOB.keyloop_list) ... user.focus?.keyLoop(user.client); the list grows by `|=`
        // at Login (mob_lists.dm:97-105) and shrinks at Logout (:62-63)
        var room = new T1Room();
        var calls = new List<string>();
        var a = room.NewMob<mob_test_m1t1_loop_recorder>(4, 4);
        var b = room.NewMob<mob_test_m1t1_loop_recorder>(6, 6);
        var c = room.NewMob<mob_test_m1t1_loop_recorder>(8, 8);
        room.In(() => { a.Calls = calls; b.Calls = calls; c.Calls = calls; });
        room.Join(a, "Alpha");
        room.Join(b, "Bravo");
        room.Join(c, "Charlie");
        room.In(() => Assert.Equal(new object[] { a, b, c }, Dm.each(GLOB.keyloop_list).ToArray()));
        calls.Clear();
        room.Step();
        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie" }, calls);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AReconnectingPlayerGoesToTheEndOfTheKeyLoopOrder()
    {
        var room = new T1Room();
        var calls = new List<string>();
        var a = room.NewMob<mob_test_m1t1_loop_recorder>(4, 4);
        var b = room.NewMob<mob_test_m1t1_loop_recorder>(6, 6);
        var c = room.NewMob<mob_test_m1t1_loop_recorder>(8, 8);
        room.In(() => { a.Calls = calls; b.Calls = calls; c.Calls = calls; });
        room.Join(a, "Alpha");
        var bravo = room.Join(b, "Bravo");
        room.Join(c, "Charlie");
        room.Act(() => ClientSeam.Disconnect(bravo.Client));
        room.Step();
        calls.Clear();
        room.Step();
        Assert.Equal(new[] { "Alpha", "Charlie" }, calls); // Logout took Bravo's mob out of the list
        room.Act(() => ClientSeam.Connect("Bravo"));
        room.Step();
        room.In(() => Assert.Equal(new object[] { a, c, b }, Dm.each(GLOB.keyloop_list).ToArray()));
        calls.Clear();
        room.Step();
        Assert.Equal(new[] { "Alpha", "Charlie", "Bravo" }, calls);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMobWithoutAClientIsNotInTheLoop()
    {
        var room = new T1Room();
        var calls = new List<string>();
        var idle = room.NewMob<mob_test_m1t1_loop_recorder>(4, 4);
        room.In(() => idle.Calls = calls);
        room.In(() => Assert.False(Dm.@in(idle, GLOB.keyloop_list)));
        room.Step(3);
        Assert.Empty(calls);
    }

    [Fact]
    public void TwoPlayersHoldingKeysMoveInTheSameTickAndTheAverageCountsBoth()
    {
        // input.dm:70-74: moves_this_run sums the keyLoop results (TRUE for each mob that had input)
        var room = new T1Room();
        var (m1, c1) = room.JoinNew("One", 4, 4);
        var (m2, c2) = room.JoinNew("Two", 10, 10);
        room.Act(() => { room.Down(c1, "D"); room.Down(c2, "A"); });
        room.Step(2);
        room.In(() =>
        {
            Assert.Equal("5,4", T1Room.Where(m1));
            Assert.Equal("9,10", T1Room.Where(m2));
            Assert.Equal(2f, SSinput.movements_per_second, 4);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void MovementsPerSecondIsTgsSlidingAverageOverOneSecondOfTicks()
    {
        // input.dm:74 movements_per_second = MC_AVG_SECONDS(avg, moves_this_run, wait TICKS), MC.dm:23-27:
        // ((total - duration) / total) * average + current with total = 1 SECONDS = 10 and duration = 1 tick = 0.5, factor 0.95
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester", 8, 3);
        room.Step(3);
        room.In(() => Assert.Equal(0f, SSinput.movements_per_second));
        room.Act(() => room.Down(c, "W"));
        room.Step(); // the press: this fire had no input
        room.In(() => Assert.Equal(0f, SSinput.movements_per_second));
        float expected = 0;
        for (int tick = 1; tick <= 6; tick++)
        {
            room.Step(); // a fire with a move
            expected = 0.95f * expected + 1f;
            room.In(() => Assert.Equal(expected, SSinput.movements_per_second, 3));
        }
        room.Act(() => room.Up(c, "W"));
        room.Step(); // this fire still had input (the key goes up in the phase after it)
        expected = 0.95f * expected + 1f;
        for (int tick = 1; tick <= 4; tick++)
        {
            room.Step(); // no input: the average decays
            expected = 0.95f * expected;
            room.In(() => Assert.Equal(expected, SSinput.movements_per_second, 3));
        }
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TgQuirkKeyLoopCountsAMovementEvenWhenTheMobIsStoppedByAWall()
    {
        // bindings_atom.dm:36-37  `return !!movement_dir //true if there was actually any player input`: input, not a move
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester", 8, 15);
        room.Act(() => room.Down(c, "W"));
        room.Step(3);
        room.In(() =>
        {
            Assert.Equal("8,15", T1Room.Where(avatar));
            Assert.Equal(0.95f * 1f + 1f, SSinput.movements_per_second, 3);
        });
    }

    [Fact]
    public void KeyLoopReturnsTrueForInputAndFalseWithoutAnyOrWithoutAClient()
    {
        // bindings_atom.dm:6-9 (no client -> FALSE), :15-21 (no input -> FALSE), :36-37 (input -> TRUE)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            Assert.False(Dm.truthy(avatar.keyLoop(null)));
            Assert.False(Dm.truthy(avatar.keyLoop(c))); // nothing held
            c.intended_direction = East;
            Assert.True(Dm.truthy(avatar.keyLoop(c)));
            Assert.Equal("9,8", T1Room.Where(avatar));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyLoopWithNoInputForgetsAPendingSubtraction()
    {
        // :15-21  if(!movement_dir) { user.next_move_dir_sub = NONE; return FALSE }
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            c.next_move_dir_sub = North;
            Assert.False(Dm.truthy(avatar.keyLoop(c)));
            Assert.Equal(0f, N(c.next_move_dir_sub));
        });
    }

    [Fact]
    public void KeyLoopAppliesTheSubtractionToTheDirectionBeforeMoving()
    {
        // :23-24  if(user.next_move_dir_sub) movement_dir &= ~user.next_move_dir_sub
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            c.intended_direction = North | East;
            c.next_move_dir_sub = North;
            Assert.True(Dm.truthy(avatar.keyLoop(c)));
            Assert.Equal("9,8", T1Room.Where(avatar)); // only east
        });
    }

    [Fact]
    public void KeyLoopOrsInTheTapDirection()
    {
        // :14  var/movement_dir = user.intended_direction | user.next_move_dir_add
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            c.intended_direction = East;
            c.next_move_dir_add = North;
            avatar.keyLoop(c);
            Assert.Equal("9,9", T1Room.Where(avatar));
        });
    }

    // ---- client.dir ----

    [Theory]
    // client.dir, key held, direction of the step. turn(movement_dir, -dir2angle(user.dir)): the map rotated to the client's
    // dir (BYOND ref client/var/dir: with dir = SOUTH the up arrow gives South)
    [InlineData(1, "W", 0, 1)]
    [InlineData(1, "D", 1, 0)]
    [InlineData(1, "S", 0, -1)]
    [InlineData(1, "A", -1, 0)]
    [InlineData(2, "W", 0, -1)]
    [InlineData(2, "D", -1, 0)]
    [InlineData(2, "S", 0, 1)]
    [InlineData(2, "A", 1, 0)]
    [InlineData(4, "W", 1, 0)]
    [InlineData(4, "D", 0, -1)]
    [InlineData(4, "S", -1, 0)]
    [InlineData(4, "A", 0, 1)]
    [InlineData(8, "W", -1, 0)]
    [InlineData(8, "D", 0, 1)]
    [InlineData(8, "S", 1, 0)]
    [InlineData(8, "A", 0, -1)]
    public void ClientDirRotatesTheHeldKeyIntoTheStepDirection(int clientDir, string key, int dx, int dy)
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => c.dir = clientDir);
        room.Act(() => room.Down(c, key));
        room.Step(2);
        room.In(() => Assert.Equal((8 + dx) + "," + (8 + dy), T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(1, 1, 1)]     // north-east stays north-east
    [InlineData(2, -1, -1)]   // dir SOUTH: turned 180 degrees -> south-west
    [InlineData(4, 1, -1)]    // dir EAST: turn(NORTHEAST, -90) -> south-east
    [InlineData(8, -1, 1)]    // dir WEST: turn(NORTHEAST, -270) -> north-west
    public void ClientDirRotatesADiagonalToo(int clientDir, int dx, int dy)
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => c.dir = clientDir);
        room.Act(() => { room.Down(c, "W"); room.Down(c, "D"); });
        room.Step(2);
        room.In(() => Assert.Equal((8 + dx) + "," + (8 + dy), T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheMobsOwnFacingDoesNotRotateTheInput()
    {
        // only user.dir (the client's) is used, never src.dir
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => Waiting.NoSleep(avatar.setDir(West)));
        room.Act(() => room.Down(c, "W"));
        room.Step(2);
        room.In(() => Assert.Equal("8,9", T1Room.Where(avatar)));
    }

    // ---- the movement lock and the focus ----

    [Fact]
    public void WithMovementLockedTheMobTurnsToTheKeyAndDoesNotMove()
    {
        // :30-32  if(user.movement_locked && !(SEND_SIGNAL(src, COMSIG_MOVABLE_KEYBIND_FACE_DIR, movement_dir) & COMSIG_IGNORE_MOVEMENT_LOCK))
        // keybind_face_direction(movement_dir) (atoms_movable.dm:1852: setDir); the else branch is not reached, FALSE returns
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            l.WatchFaceDir(avatar);
            c.movement_locked = TRUE;
            return l;
        });
        room.Act(() => room.Down(c, "D"));
        room.Step(3);
        room.In(() =>
        {
            Assert.Equal("8,8", T1Room.Where(avatar));
            Assert.Equal(East, (int)avatar.dir);
            Assert.Equal(0f, c.move_delay); // client/Move was never called
            Assert.Equal(0f, SSinput.movements_per_second);
        });
        // two fires (t2, t3); setDir sends DIR_CHANGE whether or not the direction changes (_atom.dm:579-584)
        Assert.Equal(new[] { "face_dir 4", "dir_change 2 4", "face_dir 4", "dir_change 4 4" }, listener.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AHandlerThatIgnoresTheMovementLockLetsTheMobMoveAnyway()
    {
        // :30  `!(... & COMSIG_IGNORE_MOVEMENT_LOCK)` is false, so the else branch moves
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchFaceDir(avatar);
            l.FaceDirReturn = (int)COMSIG_IGNORE_MOVEMENT_LOCK;
            c.movement_locked = TRUE;
            return l;
        });
        room.In(() => c.intended_direction = East);
        room.Step(2);
        room.In(() => Assert.Equal("10,8", T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WithMovementLockedTheFaceDirectionIsTheClientRotatedDirection()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            c.movement_locked = TRUE;
            c.dir = East; // W is now east
            c.intended_direction = North;
        });
        room.Step();
        room.In(() => Assert.Equal(East, (int)avatar.dir));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMobWithANullFocusDoesNotMoveBecauseNothingRunsItsKeyLoop()
    {
        // input.dm:72 `user.focus?.keyLoop(user.client)` and bindings_atom.dm:1-2: "If focus is null, we don't want you to move"
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => avatar.focus = null);
        room.Act(() => room.Down(c, "W"));
        room.Step(5);
        room.In(() =>
        {
            Assert.Equal("8,8", T1Room.Where(avatar));
            Assert.Equal(North, (int)N(c.intended_direction));
        });
        room.In(() => avatar.focus = avatar);
        room.Step(2);
        room.In(() => Assert.Equal("8,10", T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADifferentFocusRunsItsOwnKeyLoopWithThePlayersClient()
    {
        // The focus's keyLoop(user) gets the client of the mob that has it as focus; get_step(src, dir) is relative to the focus
        // (bindings_atom.dm:34  user.Move(get_step(src, movement_dir), movement_dir)), and client/Move moves the client's mob
        var room = new T1Room();
        var calls = new List<string>();
        var (avatar, c) = room.JoinNew("Tester", 4, 4);
        var other = room.NewMob<mob_test_m1t1_loop_recorder>(10, 10);
        room.In(() => other.Calls = calls);
        room.In(() => avatar.focus = other);
        room.In(() => c.intended_direction = North);
        room.Step();
        Assert.Equal(new[] { "(null)" }, calls.Select(x => x == "" ? "(null)" : x)); // the other mob has no key: it is the focus, called with the client
        room.In(() => Assert.Equal("10,11", T1Room.Where(avatar))); // moved to the tile north of the focus, not of itself
        room.AssertNoRuntimes();
    }

    // ---- a client that goes away during the loop ----

    [Fact]
    public void AClientQdeletedByAnotherMobsMoveDoesNotMoveItsMobInTheSameLoop()
    {
        // bindings_atom.dm:6-9: "the move of one mob might poke the client of another": the loop is over a copy of the list, so
        // the second mob is still visited, and in BYOND its `client` is already null (qdel runs client/Del and Logout at once),
        // so its keyLoop gets a null client and returns FALSE
        var room = new T1Room();
        var calls = new List<string>();
        var a = room.NewMob<mob_test_m1t1_loop_recorder>(4, 4);
        var b = room.NewMob<mob_test_m1t1_loop_recorder>(8, 8);
        room.In(() => { a.Calls = calls; b.Calls = calls; });
        var ca = room.Join(a, "Alpha").Client;
        var cb = room.Join(b, "Bravo").Client;
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(a);
            l.KillOnPreMove = cb;
            return l;
        });
        room.Act(() => { room.Down(ca, "W"); room.Down(cb, "W"); });
        room.Step(); // the phase
        calls.Clear();
        room.Step(); // the fire: Alpha moves, its move qdels Bravo's client
        room.In(() =>
        {
            Assert.Equal("4,5", T1Room.Where(a));
            Assert.NotNull(cb.gc_destroyed);
            Assert.Equal("8,8", T1Room.Where(b));
        });
        Assert.Equal(new[] { "Alpha", "Bravo (no client)" }, calls);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADeletedClientsMobIsGoneFromTheLoopTheTickAfter()
    {
        var room = new T1Room();
        var calls = new List<string>();
        var a = room.NewMob<mob_test_m1t1_loop_recorder>(4, 4);
        room.In(() => a.Calls = calls);
        var ca = room.Join(a, "Alpha").Client;
        room.Act(() => qdel(ca));
        room.Step();
        calls.Clear();
        room.Step(3);
        Assert.Empty(calls);
        room.AssertNoRuntimes();
    }
}
