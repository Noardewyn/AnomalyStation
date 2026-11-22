using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: the density/opacity/state timelines of the doors, one tick at a time, against the sleeps and timers of tg's procs
// (door.dm open/close, airlock.dm:1327 open and :1410 close, windowdoor.dm:272 open_and_close, poddoor.dm, shutters.dm).
// Tick 0 is the call; a BYOND sleep(n ds) lasts floor(n ds / tick_lag) ticks, one at least (T1.Sleep), and each sleep
// starts where the previous one woke.

public class DoorTimingTests
{
    private static void Start(TgToyRoom room, obj_machinery_door door) => room.In(() => Waiting.Detach(door.open()));

    [Fact]
    public void AnAirlockOpensAndAutocloses_TickByTick()
    {
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        var human = T1.Human(room, 4, 3);
        T1.AllAccess(room, human);
        room.In(() => door.req_access = T1.L(ACCESS_ENGINEERING));
        room.In(() =>
        {
            Assert.True(Dm.eq(door.layer, CLOSED_DOOR_LAYER)); // set_init_door_layer, door.dm:173
            Assert.True(Dm.truthy(Dm.bitand(door.flags_1, PREVENT_CLICK_UNDER_1)));
        });
        T1.Bump(room, human, door);
        var s = T1.Record(room, door, 175);

        // airlock.dm:1366-1383: set_airlock_state(OPENING) now; sleep 0.1 s, opacity; sleep 0.4 s, density; sleep 0.1 s, open
        int transparent = T1.Sleep(1);
        int passable = transparent + T1.Sleep(5 - 1);
        int finished = passable + T1.Sleep(6 - 5);
        Assert.Equal((2, 10, 12), (transparent, passable, finished));
        Assert.Equal(new DoorSnap(0, true, true, true, "opening", AIRLOCK_OPENING, CLOSED_DOOR_LAYER), s[0] with { Layer = CLOSED_DOOR_LAYER });
        Assert.Equal(transparent, T1.FirstTick(s, x => !x.Opaque));
        Assert.Equal(passable, T1.FirstTick(s, x => !x.Dense));
        Assert.Equal(finished, T1.FirstTick(s, x => !x.Operating));
        Assert.Equal("open", s[finished].Icon);
        Assert.Equal(AIRLOCK_OPEN, s[finished].State);
        Assert.Equal(OPEN_DOOR_LAYER, s[finished].Layer);
        Assert.Equal("opening", s[finished - 1].Icon); // airlock.dm:1385 layer and state change only at the end
        Assert.True(s[finished - 1].Operating);

        // autoclose_in(8 SECONDS) at airlock.dm:1343 (before the sleeps): the timer fires 160 ticks after the call
        int closing = T1.FirstTick(s, x => x.State == AIRLOCK_CLOSING, finished);
        Assert.Equal(160, closing);
        Assert.Equal(CLOSED_DOOR_LAYER, s[closing].Layer);
        Assert.True(s[closing].Operating);
        Assert.False(s[closing].Dense); // air_tight is FALSE: density only after the 0.2 s sleep (:1449)
        int dense = closing + T1.Sleep(2);
        int opaque = dense + T1.Sleep(5 - 2);
        int closed = opaque + T1.Sleep(6 - 2 - 3);
        Assert.Equal((164, 170, 172), (dense, opaque, closed));
        Assert.Equal(dense, T1.FirstTick(s, x => x.Dense, closing));
        Assert.Equal(opaque, T1.FirstTick(s, x => x.Opaque, closing));
        Assert.Equal(closed, T1.FirstTick(s, x => !x.Operating, closing));
        Assert.Equal(AIRLOCK_CLOSED, s[closed].State);
        Assert.Equal("closed", s[closed].Icon);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AnAirTightAirlockTurnsDenseAtTheStartOfClosing()
    {
        // airlock.dm:1441: `if(air_tight) set_density(TRUE)` before the unpassable sleep
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        room.In(() => door.air_tight = TRUE);
        Start(room, door);
        room.Step(14);
        room.In(() => Assert.False(T1.Dense(door)));
        room.In(() => Waiting.Detach(door.close()));
        room.In(() =>
        {
            Assert.True(T1.Dense(door));
            Assert.Equal(AIRLOCK_CLOSING, door.airlock_state);
            Assert.False(Dm.truthy(door.opacity)); // opacity still waits for the 0.5 s mark
        });
        room.Step(T1.Sleep(5) - 1);
        room.In(() => Assert.False(Dm.truthy(door.opacity)));
        room.Step(1);
        room.In(() => Assert.True(Dm.truthy(door.opacity)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ASlowAirlockClosesAfterOnePointFiveSeconds()
    {
        // airlock.dm:1343: autoclose_in(normalspeed ? 8 SECONDS : 1.5 SECONDS)
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        room.In(() => door.normalspeed = FALSE);
        Start(room, door);
        var s = T1.Record(room, door, 40);
        Assert.Equal(30, T1.FirstTick(s, x => x.State == AIRLOCK_CLOSING, 12));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ABlockedAirlockRetriesEverySixSecondsAndClosesOnceTheDoorwayIsFree()
    {
        // airlock.dm:1423-1430: a dense mover in the doorway -> autoclose_in(DOOR_CLOSE_WAIT = 60 ds), return FALSE
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        var blocker = T1.Human(room, 4, 4);
        room.In(() => Assert.True(Dm.truthy(blocker.density)));
        Start(room, door);
        room.Step(160 + 1); // the first autoclose attempt has run
        room.In(() =>
        {
            Assert.False(T1.Dense(door));
            Assert.Equal(AIRLOCK_OPEN, door.airlock_state);
        });
        room.In(() => Waiting.NoSleep(blocker.forceMove(locate(3, 3, 1))));
        room.Step(100); // tick 261: still before 160 + 120
        room.In(() => Assert.False(T1.Dense(door)));
        room.Step(19); // tick 280
        room.In(() => Assert.Equal(AIRLOCK_CLOSING, door.airlock_state));
        room.Step(14);
        room.In(() => Assert.True(T1.Dense(door)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AWeldedOpenAirlockNeitherAutoclosesNorCloses()
    {
        // door.dm:688 autoclose(): `!welded`; airlock.dm:1411 close(): `welded` -> FALSE
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        Start(room, door);
        room.Step(14);
        room.In(() =>
        {
            door.welded = TRUE;
            Assert.True(Dm.eq(Waiting.NoSleep(door.close()), FALSE));
        });
        room.Step(200);
        room.In(() =>
        {
            Assert.False(T1.Dense(door));
            Assert.Equal(AIRLOCK_OPEN, door.airlock_state);
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void OpenOnAnOpenDoorAndCloseOnAClosedDoorReturnTrueAtOnce()
    {
        // airlock.dm:1332-1338 / 1411-1414 and door.dm:586-587 / :621: `if(!density) return TRUE` / `if(density) return TRUE`
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        room.In(() =>
        {
            Assert.True(Dm.eq(Waiting.NoSleep(door.close()), TRUE));
            Assert.False(Dm.truthy(door.operating));
        });
        Start(room, door);
        room.Step(14);
        room.In(() => Assert.True(Dm.eq(Waiting.NoSleep(door.open()), TRUE)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ABaseDoorFollowsDoorDmsTimes()
    {
        // door.dm:584-612 open: opacity off at once, density after 0.5 s, done at 0.6 s; :619-659 close: density after
        // 0.2 s, opaque and done at 0.6 s. A plain door has no autoclose.
        var room = T1.Room();
        var door = T1.At<obj_machinery_door>(room, 4, 4);
        Start(room, door);
        var s = T1.Record(room, door, 12);
        Assert.False(s[0].Opaque);
        Assert.True(s[0].Operating);
        Assert.True(s[0].Dense);
        Assert.Equal("door_opening", s[0].Icon);
        Assert.Equal(10, T1.FirstTick(s, x => !x.Dense));
        Assert.Equal(12, T1.FirstTick(s, x => !x.Operating));
        Assert.Equal("door_open", s[12].Icon);
        Assert.Equal(OPEN_DOOR_LAYER, s[12].Layer);
        room.Step(300);
        room.In(() => Assert.False(T1.Dense(door))); // no autoclose
        room.In(() => Waiting.Detach(door.close()));
        var c = T1.Record(room, door, 12);
        Assert.True(c[0].Operating);
        Assert.Equal("door_closing", c[0].Icon);
        Assert.Equal(CLOSED_DOOR_LAYER, c[0].Layer);
        Assert.Equal(T1.Sleep(2), T1.FirstTick(c, x => x.Dense));
        Assert.Equal(T1.Sleep(2) + T1.Sleep(4), T1.FirstTick(c, x => !x.Operating));
        Assert.True(c[12].Opaque); // visible && !glass
        Assert.Equal("door_closed", c[12].Icon);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AWindoorOpensInPointSevenSecondsClosesItselfAndCloses_TickByTick()
    {
        // windowdoor.dm:272 open_and_close: open() (0.7 s to density off, 0.9 s to done), then, for a door with no access
        // requirement, sleep(8 SECONDS) and close() (0.2 s to density on, 0.9 s to done)
        var room = T1.Room();
        var windoor = T1.At<obj_machinery_door_window_left_directional_north>(room, 4, 4);
        var human = T1.Human(room, 4, 5);
        T1.Bump(room, human, windoor);
        var s = T1.Record(room, windoor, 200);
        int passable = T1.Sleep(7);
        int done = passable + T1.Sleep(9 - 7);
        Assert.Equal((14, 18), (passable, done));
        Assert.True(s[0].Operating);
        Assert.Equal("leftopening", s[0].Icon);
        Assert.Equal(passable, T1.FirstTick(s, x => !x.Dense));
        Assert.Equal(done, T1.FirstTick(s, x => !x.Operating));
        Assert.Equal("leftopening", s[done - 1].Icon);
        Assert.Equal("leftopen", s[done].Icon); // the animation timer (0.9 s) ends the opening state
        int closeStart = done + T1.Sleep(80);
        Assert.Equal(178, closeStart);
        Assert.False(s[closeStart - 1].Operating);
        Assert.True(s[closeStart].Operating);
        Assert.Equal("leftclosing", s[closeStart].Icon);
        Assert.Equal(closeStart + T1.Sleep(2), T1.FirstTick(s, x => x.Dense, closeStart));
        Assert.Equal(closeStart + T1.Sleep(2) + T1.Sleep(7), T1.FirstTick(s, x => !x.Operating, closeStart));
        Assert.Equal("left", s[closeStart + 18].Icon);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ASecureWindoorClosesAfterFiveSeconds()
    {
        // windowdoor.dm:276-279: `if(check_access(null)) sleep(8 SECONDS) else sleep(5 SECONDS)` ("secure doors close faster")
        var room = T1.Room();
        var windoor = T1.At<obj_machinery_door_window_left_directional_north>(room, 4, 4);
        var human = T1.Human(room, 4, 5);
        T1.Access(room, human, ACCESS_ENGINEERING);
        room.In(() => windoor.req_access = T1.L(ACCESS_ENGINEERING));
        T1.Bump(room, human, windoor);
        var s = T1.Record(room, windoor, 130);
        Assert.Equal(18 + 100, T1.FirstTick(s, x => x.Operating, 18));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ADeniedWindoorBumpPlaysDenyAndStaysShut()
    {
        // windowdoor.dm:128-135 bumpopen: `else run_animation(DOOR_DENY_ANIMATION)`; the base update_icon_state sets "[base_state]deny"
        var room = T1.Room();
        var windoor = T1.At<obj_machinery_door_window_left_directional_north>(room, 4, 4);
        var human = T1.Human(room, 4, 5);
        room.In(() => windoor.req_access = T1.L(ACCESS_ENGINEERING));
        T1.Bump(room, human, windoor);
        room.In(() =>
        {
            Assert.True(T1.Dense(windoor));
            Assert.Equal("leftdeny", windoor.icon_state);
            Assert.False(Dm.truthy(windoor.operating));
        });
        room.Step(7); // a window door has no animation_length for deny: the timer takes its minimum, one tick
        room.In(() => Assert.True(T1.Dense(windoor)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ABlastDoorOpensInHalfASecondAndFinishesAtOnePointOne()
    {
        // poddoor.dm:234-249: passable 0.5 s, finished 1.1 s, closing unpassable 0.2 s, finished 1.1 s; safe = FALSE
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_poddoor>(room, 4, 4);
        Start(room, door);
        var s = T1.Record(room, door, 24);
        int passable = T1.Sleep(5);
        int done = passable + T1.Sleep(11 - 5);
        Assert.Equal((10, 22), (passable, done));
        Assert.Equal("opening", s[0].Icon);
        Assert.False(s[0].Opaque); // door.dm:595 set_opacity(FALSE) at the start
        Assert.Equal(passable, T1.FirstTick(s, x => !x.Dense));
        Assert.Equal(done, T1.FirstTick(s, x => !x.Operating));
        Assert.Equal("open", s[done].Icon);
        room.Step(300);
        room.In(() => Assert.False(T1.Dense(door))); // no autoclose
        room.In(() => Waiting.Detach(door.close()));
        var c = T1.Record(room, door, 24);
        Assert.Equal("closing", c[0].Icon);
        Assert.Equal(T1.Sleep(2), T1.FirstTick(c, x => x.Dense));
        Assert.Equal(T1.Sleep(2) + T1.Sleep(11 - 2), T1.FirstTick(c, x => !x.Operating));
        Assert.True(c[24].Opaque);
        Assert.Equal("closed", c[24].Icon);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ShuttersUseTheirOwnFractionalDelays()
    {
        // shutters.dm:16-33: open 0.76 s then 1.388 - 0.76 s; close 0.152 s then 1.388 - 0.152 s. Each sleep rounds down to whole
        // ticks and the next one starts there.
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_poddoor_shutters>(room, 4, 4);
        Start(room, door);
        var s = T1.Record(room, door, 40);
        int passable = T1.Sleep(7.6);
        int done = passable + T1.Sleep(13.88 - 7.6);
        Assert.Equal((15, 27), (passable, done));
        Assert.Equal(passable, T1.FirstTick(s, x => !x.Dense));
        Assert.Equal(done, T1.FirstTick(s, x => !x.Operating));
        room.Step(100);
        room.In(() => Waiting.Detach(door.close()));
        var c = T1.Record(room, door, 40);
        int dense = T1.Sleep(1.52);
        int closed = dense + T1.Sleep(13.88 - 1.52);
        Assert.Equal((3, 27), (dense, closed));
        Assert.Equal(dense, T1.FirstTick(c, x => x.Dense));
        Assert.Equal(closed, T1.FirstTick(c, x => !x.Operating));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ASafeAirlockClosingOnAMobInItsDoorwayOpensAgain()
    {
        // airlock.dm:1466-1468 / door.dm:661-664: a safe door that closed on a non-dense mob runs CheckForMobs: after 0.1 s it
        // opens again
        var room = T1.Room();
        var airlock = T1.At<obj_machinery_door_airlock_public>(room, 3, 3);
        var human = T1.Human(room, 3, 3);
        room.In(() => Assert.True(Dm.truthy(airlock.safe)));
        Start(room, airlock);
        room.Step(14);
        // lying down makes the human non-dense, so the safe check in close() lets the door close on it
        room.In(() => human.density = FALSE);
        room.In(() => Waiting.Detach(airlock.close()));
        room.Step(14); // closed at 12; CheckForMobs sleeps 1 ds and opens: the door is opening again by tick 14
        room.In(() =>
        {
            Assert.Equal(AIRLOCK_OPENING, airlock.airlock_state);
        });
        room.Step(14);
        room.In(() => Assert.False(T1.Dense(airlock)));
        T1.NoRuntimes(room);
    }
}
