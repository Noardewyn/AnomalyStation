using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: tg's own unit tests of doors and wall mounts, ported from code/modules/unit_tests/ (door_access.dm, interaction_door.dm,
// wallmount.dm) onto the booted toy room. Each test names its tg source and lists what was adapted or cut and why.
//
//   door_access_check              ported (the instant airlock rebuilt as tg defines it; dev access stands in for the ID card, D117/DV23)
//   door_access_handcuffs          ported without the UnarmedAttack step (clicks, M4a); dev access for the card
//   door_require_id_wire_cut/item_only  ported with aiDisabledIdScanner standing in for the cut wire (wires are M5)
//   door_click                     first half ported (open, then not operating); the floor click is M4a
//   wallmount                      ported (the button is an inert type; find_and_mount_on_atom is the atom's own)
//   cut: door_access_ai, windoor_access_ai (/mob/living/silicon/ai), door_access_telekinesis (the telekinesis mutation),
//        door_access_mecha (mechas), door_require_id_wire_cut (wires pulse/cut: wires are M5), keyring_on_door and door_remote
//        (/obj/item/access_key, /obj/item/door_remote and clicks: M4a), firedoor_regions (a map test of firealarms, M4b)
//   P3's own ports: none of these (P3 wrote smoke tests, not ports): no duplicates.

/// <summary>tg's <c>/obj/machinery/door/airlock/instant</c> (airlock.dm:2618-2637), "excises everything that would delay the
/// process", which the generated type leaves as plain airlock behaviour.</summary>
[DmPath("/obj/machinery/door/airlock/instant/t1")]
public class obj_machinery_door_airlock_instant_t1 : obj_machinery_door_airlock_instant
{
    // tg: code/game/machinery/doors/airlock.dm:2622
    public override async DmTask<object> open(float? forced = null, mob_living opener = null)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        // SEND_SIGNAL(src, COMSIG_AIRLOCK_OPEN, forced); the opener's COMSIG_MOB_OPENED_AIRLOCK: no listener in M3
        operating = TRUE;
        set_density(FALSE);
        await set_airlock_state(AIRLOCK_OPEN, animated: FALSE);
        return TRUE;
    }

    // tg: code/game/machinery/doors/airlock.dm:2631
    public override async DmTask<object> close(float? forced = null, params object[] __extra)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        operating = TRUE;
        set_density(TRUE);
        await set_airlock_state(AIRLOCK_CLOSED, animated: FALSE);
        return TRUE;
    }
}

public class PortedTgTests
{
    private static void Bump(TgToyRoom room, mob_living_carbon_human subject, atom door) => room.In(() => Waiting.NoSleep(subject.Bump(door)));

    private static void Close(TgToyRoom room, obj_machinery_door door) => room.In(() => Waiting.NoSleep(door.close()));

    [Fact]
    public void DoorAccessCheck()
    {
        // tg: code/modules/unit_tests/door_access.dm:1 door_access_check
        var room = T1.Room();
        var subject = T1.Human(room, 2, 2); // run_loc_floor_bottom_left
        var door = T1.At<obj_machinery_door_airlock_instant_t1>(room, 2, 2);
        room.In(() => door.interaction_flags_machine = Dm.bitor(door.interaction_flags_machine, INTERACT_MACHINE_OFFLINE));

        // First, test that someone without any access can open a door that doesn't have any access requirements.
        Bump(room, subject, door);
        room.In(() => Assert.False(Dm.truthy(door.density), "Subject failed to open access-free airlock!"));
        Close(room, door);
        room.In(() => subject.last_bumped = 0);

        // someone with access can open a door that requires access when only req_access is set (keycard.access -> dev access)
        T1.Access(room, subject, ACCESS_ENGINEERING, ACCESS_MAINT_TUNNELS);
        room.In(() => door.req_access = T1.L(ACCESS_ENGINEERING, ACCESS_MAINT_TUNNELS));
        Bump(room, subject, door);
        room.In(() => Assert.False(Dm.truthy(door.density), "Subject with valid access failed to open airlock access-locked behind req_access!"));
        Close(room, door);
        room.In(() => subject.last_bumped = 0);

        // must have all accesses required on the keycard
        room.In(() => door.req_access = T1.L(ACCESS_ENGINEERING, ACCESS_MAINT_TUNNELS, ACCESS_CARGO));
        Bump(room, subject, door);
        room.In(() => Assert.True(Dm.truthy(door.density), "Subject with invalid access succeeded in opening airlock access-locked behind req_access!"));
        Close(room, door);
        room.In(() => subject.last_bumped = 0);

        // req_one_access
        room.In(() =>
        {
            door.req_access = null;
            door.req_one_access = T1.L(ACCESS_ENGINEERING, ACCESS_MAINT_TUNNELS);
        });
        T1.Access(room, subject, ACCESS_MAINT_TUNNELS);
        Bump(room, subject, door);
        room.In(() => Assert.False(Dm.truthy(door.density), "Subject with valid access failed to open airlock access-locked behind req_one_access!"));
        Close(room, door);
        room.In(() => subject.last_bumped = 0);

        room.In(() => door.req_one_access = T1.L(ACCESS_ENGINEERING, ACCESS_CARGO));
        Bump(room, subject, door);
        room.In(() => Assert.True(Dm.truthy(door.density), "Subject with invalid access succeeded in opening airlock access-locked behind req_one_access!"));
        Close(room, door);
        room.In(() => subject.last_bumped = 0);

        // emergency access
        room.In(() => door.emergency = TRUE);
        Bump(room, subject, door);
        room.In(() => Assert.False(Dm.truthy(door.density), "Subject failed to open airlock set to emergency access!"));
        Close(room, door);
        room.In(() => subject.last_bumped = 0);

        // CUT: tg's last step sets door.red_alert_access and SSsecurity_level.set_level(SEC_LEVEL_RED); SSsecurity_level is
        // M10 (the door's check_access_list reads SEC_LEVEL_GREEN until then, not-translated.md)
        T1.NoRuntimes(room);
    }

    [Fact]
    public void DoorAccessHandcuffs()
    {
        // tg: code/modules/unit_tests/door_access.dm door_access_handcuffs
        var room = T1.Room();
        var subject = T1.Human(room, 2, 2);
        var door = T1.At<obj_machinery_door_airlock_instant_t1>(room, 3, 2);
        room.In(() => door.interaction_flags_machine = Dm.bitor(door.interaction_flags_machine, INTERACT_MACHINE_OFFLINE));
        T1.Access(room, subject, ACCESS_ENGINEERING, ACCESS_MAINT_TUNNELS);
        room.In(() => ADD_TRAIT(subject, TRAIT_HANDS_BLOCKED, INNATE_TRAIT));

        Bump(room, subject, door);
        room.In(() => Assert.False(Dm.truthy(door.density), "Subject failed to bump open access-free airlock while hands blocked!"));
        Close(room, door);
        room.In(() => subject.last_bumped = 0);

        // CUT: `subject.UnarmedAttack(door)` must not open it (hands blocked): unarmed attacks and clicks are M4a

        room.In(() => door.req_access = T1.L(ACCESS_ENGINEERING, ACCESS_MAINT_TUNNELS));
        Bump(room, subject, door);
        room.In(() => Assert.True(Dm.truthy(door.density), "Subject opened access-locked airlock while hands blocked!"));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void DoorRequireIdItemOnly()
    {
        // tg: code/modules/unit_tests/door_access.dm door_require_id_wire_cut/item_only. Adapted: the keycard is the test
        // stand-in item (ID cards are M4a) and the cut ID wire is aiDisabledIdScanner, the other half of requiresID()
        // (airlock.dm:355); wires.cut is M5.
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_instant_t1>(room, 3, 2);
        var keycard = T1.At<obj_item_t1_keycard>(room, 2, 2);
        room.In(() =>
        {
            door.interaction_flags_machine = Dm.bitor(door.interaction_flags_machine, INTERACT_MACHINE_OFFLINE);
            door.req_access = T1.L(ACCESS_ENGINEERING);
            keycard.w_class = WEIGHT_CLASS_TINY;
            keycard.t1_access = T1.L(ACCESS_ENGINEERING, ACCESS_MAINT_TUNNELS);
            Waiting.NoSleep(keycard.Bump(door));
            Assert.False(Dm.truthy(door.density), "Throwing an ID at an access-locked airlock failed to open it before id wire cut!");
        });
        Close(room, door);
        room.In(() =>
        {
            door.aiDisabledIdScanner = TRUE; // wires.cut(WIRE_IDSCAN)
            Waiting.NoSleep(keycard.Bump(door));
            Assert.True(Dm.truthy(door.density), "Throwing an ID at an access-locked airlock succeeded in opening it after id wire cut!");
        });
        Close(room, door);
        room.In(() =>
        {
            door.aiDisabledIdScanner = FALSE; // mend
            Waiting.NoSleep(keycard.Bump(door));
            Assert.False(Dm.truthy(door.density), "Throwing an ID at an access-locked airlock failed to open it after mending the id wire!");
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void DoorClickFirstHalf()
    {
        // tg: code/modules/unit_tests/interaction_door.dm door_click, up to the click: `door.open()` ("this sleeps we just have to
        // cope"), then not operating and not dense. CUT: click_wrapper(tider, get_turf(door)) and the check that the airlock is
        // closing (datum/component/redirect_attack_hand_from_turf, clicks: M4a; the component is a deferred leaf in door Initialize).
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public_glass>(room, 4, 4);
        var tider = T1.Human(room, 5, 4);
        room.In(() => Waiting.Detach(door.open()));
        room.Step(14); // the open() sleeps 0.6 s
        room.In(() =>
        {
            Assert.False(Dm.truthy(door.operating), "Airlock was operating after being opened.");
            Assert.False(Dm.truthy(door.density), "Airlock was not open after being opened.");
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void Wallmount()
    {
        // tg: code/modules/unit_tests/wallmount.dm. EASY_ALLOCATE() without a location puts each atom on run_loc_floor_bottom_left
        // (2,2 here); run_loc_floor_top_right is (6,6). The first light must prioritize the wall to the south and not the table.
        var room = T1.Room();
        var table = T1.At<obj_structure_table>(room, 2, 2);
        var light = T1.At<obj_machinery_light_directional_south>(room, 2, 2);
        room.In(() =>
        {
            Waiting.NoSleep(light.find_and_mount_on_atom());
            var wallmount = light.GetComponent(typeof(datum_component_atom_mounted)) as datum_component_atom_mounted;
            Assert.NotNull(wallmount); // "failed to mount"
            Assert.NotSame(table, wallmount.hanging_support_atom); // "failed to mount on wall!"
        });

        // Test 2: a button mounts on the table, not the wall (0 offsets)
        var button = T1.At<obj_machinery_button>(room, 2, 2);
        room.In(() =>
        {
            Waiting.NoSleep(button.find_and_mount_on_atom());
            var wallmount = button.GetComponent(typeof(datum_component_atom_mounted)) as datum_component_atom_mounted;
            Assert.NotNull(wallmount);
            Assert.Same(table, wallmount.hanging_support_atom); // "0 offsets failed to mount on table!"
        });

        // Test 3: with pixel_y = 24 it mounts on the wall, not the table
        var button2 = T1.At<obj_machinery_button>(room, 6, 6);
        room.In(() =>
        {
            button2.pixel_y = 24;
            Waiting.NoSleep(button2.find_and_mount_on_atom());
            var wallmount = button2.GetComponent(typeof(datum_component_atom_mounted)) as datum_component_atom_mounted;
            Assert.NotNull(wallmount);
            Assert.True(wallmount.hanging_support_atom is turf_closed_indestructible); // isindestructiblewall
        });
        T1.NoRuntimes(room);
    }
}
