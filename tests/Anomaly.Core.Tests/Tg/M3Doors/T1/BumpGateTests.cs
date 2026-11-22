using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: the bump gate of /obj/machinery/door (door.dm:254 Bumped -> :303 bumpopen -> :331 try_to_activate_door -> :327 allowed),
// the airlock's bumpopen (airlock.dm:329) and the deny path, from tg's code.

public class BumpGateTests
{
    private static obj_machinery_door_airlock_public Airlock(TgToyRoom room, int x = 4, int y = 4, DmList req_access = null, DmList req_one_access = null)
    {
        var door = T1.At<obj_machinery_door_airlock_public>(room, x, y);
        room.In(() =>
        {
            if (req_access != null) door.req_access = req_access;
            if (req_one_access != null) door.req_one_access = req_one_access;
        });
        return door;
    }

    [Fact]
    public void ABumpWhileTheDoorIsOperatingIsIgnoredAndLeavesNoMark()
    {
        // door.dm:256: `if(operating || ...) return` comes before `M.last_bumped = world.time` (:268)
        var room = T1.Room();
        var door = Airlock(room);
        var opener = T1.Human(room, 4, 3);
        var other = T1.Human(room, 3, 4);
        T1.AllAccess(room, opener);
        T1.AllAccess(room, other);
        T1.Bump(room, opener, door);
        room.In(() => Assert.True(Dm.truthy(door.operating)));
        room.Step(1);
        T1.Bump(room, other, door);
        room.In(() => Assert.Equal(0f, Dm.num(other.last_bumped)));
        room.Step(60);
        room.In(() => Assert.False(Dm.truthy(door.density)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TheGateRefusesAtExactlyPointThreeSecondsAndPassesJustAfter()
    {
        // door.dm:266: `if(world.time - M.last_bumped <= 0.3 SECONDS) return`: 3 ds is still refused, the next tick (3.5 ds) passes
        var room = T1.Room();
        var door = Airlock(room, req_access: T1.L(ACCESS_ENGINEERING));
        var human = T1.Human(room, 4, 3);
        T1.Bump(room, human, door);
        float first = room.In(() => Dm.num(human.last_bumped));
        Assert.Equal(room.In(() => Dm.world.time), first);
        room.Step(6); // 3.0 ds
        T1.Bump(room, human, door);
        room.In(() => Assert.Equal(first, Dm.num(human.last_bumped)));
        room.Step(1); // 3.5 ds
        T1.Bump(room, human, door);
        room.In(() => Assert.Equal(room.World.Time, Dm.num(human.last_bumped)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ABumpIntoAPodDoorThatCannotOpenWithHandsDoesNothingEvenWithAccess()
    {
        // door.dm:256: `(!can_open_with_hands && density)` returns first; poddoor.dm:16 can_open_with_hands = FALSE
        var room = T1.Room();
        var blast = T1.At<obj_machinery_door_poddoor>(room, 4, 4);
        var human = T1.Human(room, 4, 3);
        T1.AllAccess(room, human);
        T1.Bump(room, human, blast);
        room.Step(40);
        room.In(() =>
        {
            Assert.Equal(0f, Dm.num(human.last_bumped)); // returned before the mark
            Assert.True(T1.Dense(blast));
            Assert.Equal("closed", blast.icon_state); // no deny animation either
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void HandsBlockedMobsOnlyOpenAccessFreeAndEmergencyDoors()
    {
        // door.dm:269: HAS_TRAIT(M, TRAIT_HANDS_BLOCKED) && !check_access(null) && !emergency -> return, after last_bumped is set
        var room = T1.Room();
        var locked = Airlock(room, 3, 4, req_access: T1.L(ACCESS_ENGINEERING));
        var free = Airlock(room, 5, 4);
        var human = T1.Human(room, 4, 3);
        T1.AllAccess(room, human);
        room.In(() => ADD_TRAIT(human, TRAIT_HANDS_BLOCKED, "t1"));

        T1.Bump(room, human, locked);
        room.Step(30);
        room.In(() =>
        {
            Assert.True(Dm.num(human.last_bumped) > 0f); // the mark was made
            Assert.True(T1.Dense(locked));
            Assert.Equal(AIRLOCK_CLOSED, locked.airlock_state); // not even a deny animation
            Assert.Equal("closed", locked.icon_state);
        });

        room.Step(10);
        T1.Bump(room, human, free);
        room.Step(14);
        room.In(() => Assert.False(T1.Dense(free)));

        room.In(() => locked.emergency = TRUE);
        room.Step(10);
        T1.Bump(room, human, locked);
        room.Step(14);
        room.In(() => Assert.False(T1.Dense(locked)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ABumpOnAnOpenDoorClosesIt()
    {
        // try_to_activate_door (door.dm:343): `if(density) open(opener = user) else close()`
        var room = T1.Room();
        var door = Airlock(room);
        var human = T1.Human(room, 4, 3);
        room.In(() => Waiting.Detach(door.open()));
        room.Step(14);
        room.In(() => Assert.False(T1.Dense(door)));
        room.Step(30); // the open() above autocloses at 8 s; this is well before it
        T1.Bump(room, human, door);
        room.In(() =>
        {
            Assert.True(Dm.truthy(door.operating));
            Assert.Equal(AIRLOCK_CLOSING, door.airlock_state);
        });
        room.Step(14);
        room.In(() => Assert.True(T1.Dense(door)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMobWithAccessAtABoltedOrWeldedDoorGetsNoDenyAndNoOpening()
    {
        // try_to_activate_door returns TRUE after calling open() (door.dm:340-346), whatever open() answered; the deny
        // animation is only for the no-access branch (:349). airlock.dm:1332: open() refuses `locked`, `welded`, `seal`.
        var room = T1.Room();
        var bolted = Airlock(room, 3, 4);
        var welded = Airlock(room, 5, 4);
        var human = T1.Human(room, 4, 3);
        var stranger = T1.Human(room, 4, 5);
        T1.AllAccess(room, human);
        room.In(() =>
        {
            bolted.req_access = T1.L(ACCESS_ENGINEERING);
            welded.req_access = T1.L(ACCESS_ENGINEERING);
            bolted.bolt();
            welded.welded = TRUE;
            Assert.True(Dm.truthy(bolted.locked));
        });
        T1.Bump(room, human, bolted);
        room.In(() =>
        {
            Assert.Equal(AIRLOCK_CLOSED, bolted.airlock_state); // no deny
            Assert.False(Dm.truthy(bolted.operating));
        });
        room.Step(30);
        T1.Bump(room, human, welded);
        room.In(() => Assert.Equal(AIRLOCK_CLOSED, welded.airlock_state));
        room.Step(30);
        room.In(() =>
        {
            Assert.True(T1.Dense(bolted));
            Assert.True(T1.Dense(welded));
        });
        // without access the bolted door denies
        T1.Bump(room, stranger, bolted);
        room.In(() => Assert.Equal(AIRLOCK_DENY, bolted.airlock_state));
        room.Step(30);
        room.In(() =>
        {
            Assert.Equal(AIRLOCK_CLOSED, bolted.airlock_state);
            Assert.True(T1.Dense(bolted));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ABoltedOpenDoorDoesNotAutocloseAndNotCloseOnBump()
    {
        // door.dm:688 autoclose(): `!locked`; airlock.dm:1411 close(): `operating || welded || locked || seal` -> FALSE
        var room = T1.Room();
        var door = Airlock(room);
        var human = T1.Human(room, 4, 3);
        room.In(() => Waiting.Detach(door.open()));
        room.Step(14);
        room.In(() =>
        {
            Assert.False(T1.Dense(door));
            door.bolt();
        });
        room.Step(170); // past the 8 s autoclose timer
        room.In(() =>
        {
            Assert.False(T1.Dense(door));
            Assert.Equal(AIRLOCK_OPEN, door.airlock_state);
            Assert.True(Dm.eq(Waiting.NoSleep(door.close()), FALSE));
        });
        T1.Bump(room, human, door);
        room.Step(20);
        room.In(() => Assert.False(T1.Dense(door)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ADeniedBumpPlaysTheDenyStateForPointSixSeconds()
    {
        // airlock.dm:647 run_animation(DENY): airlock_state = AIRLOCK_DENY, animation "deny"; handle_deny_end (:672) after
        // AIRLOCK_DENY_ANIMATION_TIME 0.6 s puts the state back to closed, only if it is still the deny state
        var room = T1.Room();
        var door = Airlock(room, req_access: T1.L(ACCESS_ENGINEERING));
        var human = T1.Human(room, 4, 3);
        T1.Bump(room, human, door);
        room.In(() =>
        {
            Assert.Equal(AIRLOCK_DENY, door.airlock_state);
            Assert.Equal("deny", door.icon_state);
            Assert.False(Dm.truthy(door.operating)); // set_airlock_state(AIRLOCK_DENY, animated = FALSE)
        });
        room.Step(11); // 0.55 s
        room.In(() => Assert.Equal(AIRLOCK_DENY, door.airlock_state));
        room.Step(1); // 0.6 s
        room.In(() =>
        {
            Assert.Equal(AIRLOCK_CLOSED, door.airlock_state);
            Assert.Equal("closed", door.icon_state);
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AnItemBumpOpensTheDoorOnlyWithAnAccessCardOrAnUnrestrictedSide()
    {
        // door.dm:278-286: `!density || (w_class < NORMAL && !LAZYLEN(GetAccess()))` -> nothing; a card with access opens
        // the door through open() (not through the access gate); anything else denies
        var room = T1.Room();
        var door = Airlock(room, req_access: T1.L(ACCESS_ENGINEERING));
        var tiny = T1.At<obj_item>(room, 4, 3);
        var heavy = T1.At<obj_item>(room, 3, 3);
        var card = T1.At<obj_item_t1_keycard>(room, 5, 3);
        var wrong = T1.At<obj_item_t1_keycard>(room, 3, 5);
        room.In(() =>
        {
            tiny.w_class = WEIGHT_CLASS_SMALL;
            heavy.w_class = WEIGHT_CLASS_NORMAL;
            card.w_class = WEIGHT_CLASS_TINY;
            card.t1_access = T1.L(ACCESS_ENGINEERING, ACCESS_MAINT_TUNNELS);
            wrong.w_class = WEIGHT_CLASS_NORMAL;
            wrong.t1_access = T1.L(ACCESS_MAINT_TUNNELS);
        });
        T1.Bump(room, tiny, door); // small and no access: returns before any animation
        room.In(() => Assert.Equal(AIRLOCK_CLOSED, door.airlock_state));
        T1.Bump(room, heavy, door); // normal size, no access: the deny animation
        room.In(() => Assert.Equal(AIRLOCK_DENY, door.airlock_state));
        room.Step(14);
        T1.Bump(room, wrong, door); // a card with another access: deny
        room.In(() => Assert.Equal(AIRLOCK_DENY, door.airlock_state));
        room.Step(14);
        T1.Bump(room, card, door); // a small card with the access: opens (check_access(I))
        room.In(() => Assert.Equal(AIRLOCK_OPENING, door.airlock_state));
        room.Step(14);
        room.In(() => Assert.False(T1.Dense(door)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AnUnrestrictedSideLetsAMobInFromThatSideOnly()
    {
        // door.dm:367 unrestricted_side: `get_dir(src, opener) & unres_sides`; user_can_activate_door (:355) accepts it
        var room = T1.Room();
        var door = Airlock(room, req_access: T1.L(ACCESS_ENGINEERING));
        var inside = T1.Human(room, 4, 3); // south of the door
        var outside = T1.Human(room, 4, 5); // north
        room.In(() => door.unres_sides = SOUTH);
        T1.Bump(room, outside, door);
        room.In(() => Assert.Equal(AIRLOCK_DENY, door.airlock_state));
        room.Step(14);
        T1.Bump(room, inside, door);
        room.In(() => Assert.True(Dm.truthy(door.operating)));
        room.Step(14);
        room.In(() => Assert.False(T1.Dense(door)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMobWalkingIntoAnAirlockBumpsItOpenAndThenWalksThrough()
    {
        // the whole path: Move into a dense door -> Bump (the move's blocking atom) -> Bumped -> bumpopen -> open; with access
        var room = T1.Room();
        var door = Airlock(room, req_access: T1.L(ACCESS_ENGINEERING));
        var human = T1.Human(room, 4, 3);
        T1.Access(room, human, ACCESS_ENGINEERING);
        Assert.False(room.In(() => Dm.truthy(Waiting.NoSleep(human.Move(locate(4, 4, 1), NORTH)))));
        room.In(() => Assert.Equal(locate(4, 3, 1), human.loc));
        room.In(() => Assert.True(Dm.truthy(door.operating)));
        room.Step(T1.Sleep(5) + 1);
        Assert.True(room.In(() => Dm.truthy(Waiting.NoSleep(human.Move(locate(4, 4, 1), NORTH)))));
        room.In(() => Assert.Equal(locate(4, 4, 1), human.loc));
        T1.NoRuntimes(room);
    }
}
