using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: what the doors' Initialize and Destroy set up (door.dm:109-143, :179, airlock.dm:166-201, windowdoor.dm:50-78) and the
// morgue door's own times (door.dm:721-740).

public class DoorInitTests
{
    [Fact]
    public void AnAirlockInitializesLikeDoorDmAndAirlockDm()
    {
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        var glass = T1.At<obj_machinery_door_airlock_public_glass>(room, 3, 3);
        room.In(() =>
        {
            Assert.True(Dm.truthy(Dm.bitand(door.flags_1, PREVENT_CLICK_UNDER_1))); // door.dm:127 `if(density) flags_1 |= ...`
            Assert.True(Dm.eq(door.layer, CLOSED_DOOR_LAYER)); // set_init_door_layer
            Assert.True(Dm.eq(door.atom_integrity, 300)); // airlock.dm:174 atom_integrity = normal_integrity
            Assert.True(Dm.eq(door.max_integrity, 300));
            Assert.NotNull(door.wires); // set_wires(get_wires())
            Assert.True(Dm.eq(door.real_explosion_block, door.explosion_block)); // door.dm:134
            Assert.True(Dm.eq(door.explosion_block, 1));
            Assert.True(Dm.truthy(door.hasPower()));
            Assert.True(Dm.truthy(door.requiresID()));
            Assert.True(Dm.eq(door.power_channel, AREA_USAGE_ENVIRON));
            // glass: airlock_material "glass" (airlock.dm:167) and PASSWINDOW (door.dm:131)
            Assert.Equal("glass", glass.airlock_material);
            Assert.True(Dm.truthy(Dm.bitand(glass.pass_flags, PASSWINDOW)));
            Assert.False(Dm.truthy(glass.opacity));
            Assert.False(Dm.truthy(Dm.bitand(door.pass_flags, PASSWINDOW))); // a plain airlock does not pass windows
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AWindoorKeepsOnlyTheUnrestrictedSidesItsDirectionAllows()
    {
        // windowdoor.dm:50-65: Initialize(mapload, set_dir, unres_sides): a door facing NORTH or SOUTH drops EAST and WEST,
        // one facing EAST or WEST drops NORTH and SOUTH; the click-under flag is cleared (:52)
        var room = T1.Room();
        var ns = room.In(() => Dm.@new<obj_machinery_door_window>(locate(3, 3, 1), NORTH, Dm.bitor(Dm.bitor(NORTH, EAST), Dm.bitor(SOUTH, WEST))));
        var ew = room.In(() => Dm.@new<obj_machinery_door_window>(locate(5, 3, 1), EAST, Dm.bitor(Dm.bitor(NORTH, EAST), Dm.bitor(SOUTH, WEST))));
        var none = room.In(() => Dm.@new<obj_machinery_door_window>(locate(4, 5, 1), SOUTH, 0));
        room.In(() =>
        {
            Assert.True(Dm.eq(ns.dir, NORTH));
            Assert.True(Dm.eq(ns.unres_sides, Dm.bitor(NORTH, SOUTH)));
            Assert.True(Dm.eq(ew.dir, EAST));
            Assert.True(Dm.eq(ew.unres_sides, Dm.bitor(EAST, WEST)));
            Assert.True(Dm.eq(none.unres_sides, 0));
            Assert.False(Dm.truthy(Dm.bitand(ns.flags_1, PREVENT_CLICK_UNDER_1)));
            Assert.True(Dm.truthy(Dm.bitand(ns.flags_1, ON_BORDER_1)));
            Assert.False(Dm.truthy(ns.opacity));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ASecureWindoorUsesItsSecureIconAndDestroyingAWindoorMakesItNonDense()
    {
        // windowdoor.dm:56-58 `if(LAZYLEN(req_access)) icon_state = "[icon_state]"; base_state = icon_state`; Destroy (:71) set_density(FALSE)
        var room = T1.Room();
        var cell = T1.At<obj_machinery_door_window_brigdoor_security_cell>(room, 4, 4);
        room.In(() =>
        {
            Assert.Equal("leftsecure", cell.icon_state);
            Assert.Equal("leftsecure", cell.base_state);
            Assert.True(Dm.@in(ACCESS_BRIG, cell.req_access));
            Assert.True(T1.Dense(cell));
            qdel(cell);
            Assert.False(T1.Dense(cell));
            Assert.False(Dm.@in(cell, (DmList)SSmachines.get_all_machines()));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMorgueDoorOpensInOnePointFourSecondsAndFinishesAtOnePointFive()
    {
        // door.dm:721-740: morgue animation_length 1.5 s (deny 0.1 s), segment delays 1.4 / 1.5 s opening, 0.2 / 1.5 s closing
        var room = T1.Room();
        var morgue = T1.At<obj_machinery_door_morgue>(room, 4, 4);
        room.In(() => Waiting.Detach(morgue.open()));
        var s = T1.Record(room, morgue, 32);
        int passable = T1.Sleep(14), done = passable + T1.Sleep(1);
        Assert.Equal((28, 30), (passable, done));
        Assert.Equal(passable, T1.FirstTick(s, x => !x.Dense));
        Assert.Equal(done, T1.FirstTick(s, x => !x.Operating));
        Assert.Equal("door_opening", s[done - 1].Icon);
        Assert.Equal("door_open", s[done].Icon); // the 1.5 s animation timer fires at tick 30
        room.In(() => Waiting.Detach(morgue.close()));
        var c = T1.Record(room, morgue, 34);
        Assert.Equal(4, T1.FirstTick(c, x => x.Dense));
        Assert.Equal(4 + T1.Sleep(13), T1.FirstTick(c, x => !x.Operating));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMorgueDoorsDenyAnimationLastsOneDecisecond()
    {
        // door.dm:518-532 deny: icon "<base_icon_state>_deny"; the animation timer is animation_length(DENY) = 0.1 s (1 ds = 2 ticks)
        var room = T1.Room();
        var morgue = T1.At<obj_machinery_door_morgue>(room, 4, 4);
        var human = T1.Human(room, 4, 3);
        room.In(() => morgue.req_access = T1.L(ACCESS_MORGUE));
        T1.Bump(room, human, morgue);
        room.In(() => Assert.Equal("door_deny", morgue.icon_state));
        room.Step(1);
        room.In(() => Assert.Equal("door_deny", morgue.icon_state));
        room.Step(1);
        room.In(() => Assert.Equal("door_closed", morgue.icon_state));
        T1.NoRuntimes(room);
    }
}
