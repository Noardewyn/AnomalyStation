using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tests.Tg.M4aAccess.P5;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aTools.P4;

/// <summary>
/// M4a P4b: tg's door buttons (buttons.dm, doorcontrol.dm) through the real click chain. A mapped button gets its
/// controller at init (button/door setup_device), a press with access pulses it (attempt_press), the blast door controller
/// opens every pod door with its id and the next press closes them (sync_doors), the controller's cooldown (1 s) refuses
/// a press until it ends, a press without access is refused, and the airlock controller bolts its airlocks.
/// </summary>
public class P4bButtonTests
{
    private const int Second = 20;

    private static void Click(CapsuleRoom room, mob who, atom target, int ticks = 2 * Second)
    {
        room.Command(() => ClientSeam.Click(who.client, target, "left=1;button=left"));
        room.Step(ticks);
    }

    /// <summary>A button as the map loader makes it: the preloader's var edits, then Initialize with mapload.</summary>
    private static T Mapped<T>(CapsuleRoom room, int x, int y, DmList edits) where T : obj_machinery_button
    {
        return room.In(() =>
        {
            float previous = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            world.preloader_setup(edits, typeof(T));
            T button = Dm.@new<T>(locate(x, y, 1));
            SSatoms.initialized = previous;
            return button;
        });
    }

    private static obj_machinery_door_poddoor PodDoor(CapsuleRoom room, int x, int y, string id)
    {
        return room.In(() =>
        {
            var door = Dm.@new<obj_machinery_door_poddoor>(locate(x, y, 1));
            door.id = id;
            return door;
        });
    }

    /// <summary>The card in the hand that does not click, so the click is an empty hand's (attack_hand, interact).</summary>
    private static void HoldCardInOtherHand(CapsuleRoom room, mob_living who, string access)
    {
        room.In(() =>
        {
            IdCards.Hold(who, access);
            who.swap_hand();
            Assert.Null(who.get_active_held_item());
        });
    }

    [Fact]
    public void APressWithAccessOpensThePodDoorsAndTheNextClosesThem()
    {
        var room = new CapsuleRoom(4401);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        var button = Mapped<obj_machinery_button_door>(room, 6, 5, Dm.list(Dm.kv("id", "p4b_test"), Dm.kv("req_access", Dm.list(ACCESS_MEDICAL))));
        var a = PodDoor(room, 3, 8, "p4b_test");
        var b = PodDoor(room, 4, 8, "p4b_test");
        var other = PodDoor(room, 5, 8, "elsewhere");
        HoldCardInOtherHand(room, alice, ACCESS_MEDICAL);
        room.Step(Second);

        room.In(() =>
        {
            Assert.IsType<obj_item_assembly_control_blast_door>(button.device_instance);
            Assert.Equal("p4b_test", ((obj_item_assembly_control)button.device_instance).id);
            Assert.NotNull(button.board_instance); // req_access gives the button its airlock electronics (buttons.dm:53)
            Assert.False(Dm.truthy(button.panel_open));
            Assert.True(Dm.truthy(a.density) && Dm.truthy(b.density) && Dm.truthy(other.density));
        });

        Click(room, alice, button);
        room.In(() =>
        {
            Assert.False(Dm.truthy(a.density), "the press opens the first pod door");
            Assert.False(Dm.truthy(b.density), "and every pod door with the controller's id");
            Assert.True(Dm.truthy(other.density), "a pod door with another id stays shut");
        });

        Click(room, alice, button);
        room.In(() =>
        {
            Assert.True(Dm.truthy(a.density), "the next press closes them");
            Assert.True(Dm.truthy(b.density));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ThePressIsRefusedUntilTheControllersCooldownEnds()
    {
        var room = new CapsuleRoom(4402);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        var button = Mapped<obj_machinery_button_door>(room, 6, 5, Dm.list(Dm.kv("id", "p4b_cd")));
        var door = PodDoor(room, 3, 8, "p4b_cd");
        room.Step(Second);

        // the press opens the door and starts the controller's cooldown (doorcontrol.dm:107, 113: 1 SECONDS)
        Click(room, alice, button, ticks: 2);
        var control = room.In(() => (obj_item_assembly_control)button.device_instance);
        Assert.True(room.In(() => Dm.truthy(control.cooldown)), "the cooldown runs after a press");
        room.Step(Second - 6);
        Assert.True(room.In(() => Dm.truthy(control.cooldown)), "still running just before 1 s");
        Click(room, alice, button, ticks: 2);
        room.Step(2 * Second);
        Assert.False(room.In(() => Dm.truthy(door.density)), "a press during the cooldown does not close the door");
        Assert.False(room.In(() => Dm.truthy(control.cooldown)), "the cooldown ends after tg's 1 s");

        Click(room, alice, button);
        Assert.True(room.In(() => Dm.truthy(door.density)), "after the cooldown the press closes it");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void APressWithoutAccessIsRefused()
    {
        var room = new CapsuleRoom(4403);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        var button = Mapped<obj_machinery_button_door>(room, 6, 5, Dm.list(Dm.kv("id", "p4b_locked"), Dm.kv("req_access", Dm.list(ACCESS_CAPTAIN))));
        var door = PodDoor(room, 3, 8, "p4b_locked");
        HoldCardInOtherHand(room, alice, ACCESS_MEDICAL);
        room.Step(Second);

        Click(room, alice, button);
        Assert.True(room.In(() => Dm.truthy(door.density)), "no access: the pod door stays shut");
        Assert.False(room.In(() => Dm.truthy(((obj_item_assembly_control)button.device_instance).cooldown)), "the controller was not pulsed");
        Assert.Equal(FALSE, room.In(() => Dm.num(Waiting.NoSleep(button.attempt_press(alice)))));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnAirlockControllerBoltsItsAirlocksAndTheNextPressUnboltsThem()
    {
        var room = new CapsuleRoom(4404);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        var button = Mapped<obj_machinery_button_door>(room, 6, 5, Dm.list(Dm.kv("id", "p4b_cabin"), Dm.kv("normaldoorcontrol", 1), Dm.kv("specialfunctions", BOLTS)));
        var airlock = room.In(() =>
        {
            var d = Dm.@new<obj_machinery_door_airlock>(locate(3, 8, 1));
            d.id_tag = "p4b_cabin";
            return d;
        });
        var stranger = room.In(() => Dm.@new<obj_machinery_door_airlock>(locate(4, 8, 1)));
        room.Step(Second);
        room.In(() =>
        {
            var control = Assert.IsType<obj_item_assembly_control_airlock>(button.device_instance);
            Assert.Equal((float)BOLTS, control.specialfunctions);
            Assert.False(Dm.truthy(airlock.locked));
        });

        Click(room, alice, button);
        Assert.True(room.In(() => Dm.truthy(airlock.locked)), "the press bolts the airlock with the controller's id");
        Assert.False(room.In(() => Dm.truthy(stranger.locked)), "another airlock is not bolted");
        Assert.True(room.In(() => Dm.truthy(airlock.density)), "bolts only: the airlock is not opened");

        Click(room, alice, button);
        Assert.False(room.In(() => Dm.truthy(airlock.locked)), "the next press unbolts it");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACrowbarPriesAnUnpoweredPodDoorOpen()
    {
        var room = new CapsuleRoom(4405);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        var door = PodDoor(room, 6, 5, "p4b_pry");
        room.In(() =>
        {
            var crowbar = Dm.@new<obj_item_crowbar>(locate(5, 5, 1));
            Assert.True(Dm.truthy(Waiting.NoSleep(alice.put_in_hands(crowbar))));
        });
        room.Step(Second);

        // powered: the crowbar is refused while the door is shut (poddoor.dm:159)
        Click(room, alice, door);
        Assert.True(room.In(() => Dm.truthy(door.density)), "a powered pod door is not pried");

        room.In(() => Dm.combine(ref door.machine_stat, NOPOWER));
        Click(room, alice, door);
        Assert.False(room.In(() => Dm.truthy(door.density)), "an unpowered pod door is pried open (poddoor.dm:156)");
        room.AssertNoRuntimes();
    }
}
