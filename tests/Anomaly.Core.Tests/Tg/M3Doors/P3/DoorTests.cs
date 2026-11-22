using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.P3;

// M3 P3's smoke tests of the doors, the access, the access helpers and the light fixture on the booted toy room (the real
// Master: SStimer, SSmachines and SSatoms run). The 7 x 7 room has walls on its rim and floor at x, y 2..6. T1 tests P3
// independently later (door_access, the timings, the helpers' payloads).

public class DoorTests
{
    // 20 ticks per second: one decisecond is two ticks.
    private const int TicksPerDs = 2;

    private static TgToyRoom Room(Action<World> mapExtras = null, ulong seed = 41) => TgToyRoom.Boot(seed: seed, mapExtras: mapExtras);

    private static mob_living_carbon_human Human(TgToyRoom room, int x, int y)
        => room.In(() => Dm.@new<mob_living_carbon_human_consistent>(locate(x, y, 1)));

    private static T At<T>(TgToyRoom room, int x, int y) where T : atom => room.In(() => Dm.@new<T>(locate(x, y, 1)));

    private static void Bump(TgToyRoom room, atom_movable who, atom what) => room.In(() => what.Bumped(who));

    [Fact]
    public void AMobWithoutAccessIsDeniedAtAnAccessLockedAirlock()
    {
        var room = Room();
        var door = At<obj_machinery_door_airlock_public>(room, 4, 4);
        var human = Human(room, 4, 3);
        room.In(() => door.req_access = Dm.list(ACCESS_ENGINEERING));
        Bump(room, human, door);
        room.In(() =>
        {
            Assert.True(Dm.truthy(door.density));
            Assert.Equal(AIRLOCK_DENY, door.airlock_state); // airlock.dm:651, run_animation(DOOR_DENY_ANIMATION)
            Assert.Equal("deny", door.icon_state); // update_icon_state: "[base_icon_state][animation]", base_icon_state null
        });
        room.Step(8 * TicksPerDs); // AIRLOCK_DENY_ANIMATION_TIME 0.6 s, then handle_deny_end (airlock.dm:672)
        room.In(() =>
        {
            Assert.True(Dm.truthy(door.density));
            Assert.Equal(AIRLOCK_CLOSED, door.airlock_state);
            Assert.Equal("closed", door.icon_state);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void WithAllAccessTheAirlockOpensAtTgsDelaysAndClosesItself()
    {
        var room = Room();
        var door = At<obj_machinery_door_airlock_public>(room, 4, 4);
        var human = Human(room, 4, 3);
        room.In(() =>
        {
            door.req_access = Dm.list(ACCESS_ENGINEERING);
            Anomaly.Core.Tests.Tg.M4aAccess.P5.IdCards.HoldAll(human); // all access (M4a P5: an ID card; DV23 retired)
            Assert.True(Dm.truthy(door.opacity));
        });
        Bump(room, human, door);
        room.In(() =>
        {
            Assert.True(Dm.truthy(door.operating));
            Assert.Equal("opening", door.icon_state);
            Assert.True(Dm.truthy(door.density));
        });
        room.Step(1 * TicksPerDs); // AIRLOCK_OPENING_TRANSPARENT 0.1 s
        room.In(() => { Assert.False(Dm.truthy(door.opacity)); Assert.True(Dm.truthy(door.density)); });
        room.Step(4 * TicksPerDs - 1); // just before 0.5 s: still dense
        room.In(() => Assert.True(Dm.truthy(door.density)));
        room.Step(1); // AIRLOCK_OPENING_PASSABLE 0.5 s
        room.In(() => Assert.False(Dm.truthy(door.density)));
        room.Step(1 * TicksPerDs); // AIRLOCK_OPENING_FINISHED 0.6 s
        room.In(() =>
        {
            Assert.False(Dm.truthy(door.operating));
            Assert.Equal(AIRLOCK_OPEN, door.airlock_state);
            Assert.Equal("open", door.icon_state);
        });
        // autoclose_in(8 SECONDS) at the start of open (airlock.dm:1343): closing at 8 s, dense 0.2 s later, opaque at 0.5 s
        room.Step(80 * TicksPerDs - 6 * TicksPerDs - 1);
        room.In(() => Assert.False(Dm.truthy(door.density)));
        room.Step(1);
        room.In(() => { Assert.True(Dm.truthy(door.operating)); Assert.Equal("closing", door.icon_state); });
        room.Step(2 * TicksPerDs);
        room.In(() => Assert.True(Dm.truthy(door.density)));
        room.Step(4 * TicksPerDs);
        room.In(() =>
        {
            Assert.True(Dm.truthy(door.opacity));
            Assert.False(Dm.truthy(door.operating));
            Assert.Equal("closed", door.icon_state);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TheBumpGateLetsOneBumpThroughPerPointThreeSeconds()
    {
        var room = Room();
        var door = At<obj_machinery_door_airlock_public>(room, 4, 4);
        var human = Human(room, 4, 3);
        room.In(() => door.req_access = Dm.list(ACCESS_ENGINEERING));
        Bump(room, human, door); // denied, last_bumped set (door.dm:266-268)
        float first = room.In(() => human.last_bumped);
        room.Step(2 * TicksPerDs); // 0.2 s later
        Bump(room, human, door);
        room.In(() => Assert.True(Dm.eq(human.last_bumped, first))); // gated: world.time - last_bumped <= 0.3 s
        room.Step(2 * TicksPerDs);
        Bump(room, human, door);
        room.In(() => Assert.True(Dm.gt(human.last_bumped, first)));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void BoltsKeepTheAirlockShutForEveryone()
    {
        var room = Room();
        var door = At<obj_machinery_door_airlock_public>(room, 4, 4);
        var human = Human(room, 4, 3);
        room.In(() =>
        {
            Anomaly.Core.Tests.Tg.M4aAccess.P5.IdCards.HoldAll(human);
            door.bolt();
            Assert.True(Dm.truthy(door.locked));
        });
        Bump(room, human, door);
        room.Step(20 * TicksPerDs);
        room.In(() =>
        {
            Assert.True(Dm.truthy(door.density));
            Assert.True(Dm.eq(Waiting.NoSleep(door.open()), FALSE)); // open() refuses a bolted airlock (airlock.dm:1332)
            door.unbolt();
        });
        room.Step(4 * TicksPerDs); // the bump gate
        Bump(room, human, door);
        room.Step(6 * TicksPerDs);
        room.In(() => Assert.False(Dm.truthy(door.density)));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AWindoorBlocksOnlyItsFace()
    {
        var room = Room();
        var windoor = At<obj_machinery_door_window_left_directional_north>(room, 4, 4);
        var thing = At<obj_item>(room, 4, 3);
        room.In(() =>
        {
            Assert.True(Dm.eq(windoor.dir, NORTH));
            Assert.True(Dm.truthy(windoor.density));
            // CanAllowThrough by dir (windowdoor.dm:196): a mover crossing its north face is refused, any other passes
            Assert.False(Dm.truthy(Waiting.NoSleep(windoor.CanAllowThrough(thing, NORTH))));
            Assert.True(Dm.truthy(Waiting.NoSleep(windoor.CanAllowThrough(thing, SOUTH))));
            Assert.True(Dm.truthy(Waiting.NoSleep(windoor.CanAllowThrough(thing, EAST))));
            // on_exit (windowdoor.dm:223, through connect_loc): leaving north is blocked, leaving south is not
            Assert.True(Dm.eq(windoor.on_exit(null, thing, NORTH), COMPONENT_ATOM_BLOCK_EXIT));
            Assert.Null(windoor.on_exit(null, thing, SOUTH));
            // the motion faces see the border object's face (LocomotionSeam.FacesOf), not a solid tile
            Assert.False(Anomaly.Core.Tg.LocomotionSeam.IsSolid(room.World, 4, 4, 1));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AnOpenWindoorClosesAfterOpenAndClose()
    {
        var room = Room();
        var windoor = At<obj_machinery_door_window_left_directional_north>(room, 4, 4);
        var human = Human(room, 4, 5);
        room.In(() => Assert.Equal("left", windoor.icon_state));
        Bump(room, human, windoor); // no req_access: allowed (access.dm:8), open_and_close
        room.Step(7 * TicksPerDs); // DOOR_OPENING_PASSABLE of the windoor, 0.7 s
        room.In(() => Assert.False(Dm.truthy(windoor.density)));
        room.Step(2 * TicksPerDs);
        room.In(() => Assert.Equal("leftopen", windoor.icon_state));
        room.Step(80 * TicksPerDs); // check_access(null) passes: 8 s, then close (windowdoor.dm:147-152)
        room.Step(2 * TicksPerDs + 1);
        room.In(() => Assert.True(Dm.truthy(windoor.density)));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void PodDoorsAndShuttersKeepTheirMappedStateAndFirelocksStayOpen()
    {
        obj_machinery_door_poddoor blast = null, open_blast = null;
        obj_machinery_door_poddoor_shutters open_shutters = null;
        obj_machinery_door_firedoor firelock = null;
        var room = Room(mapExtras: w =>
        {
            blast = Dm.@new<obj_machinery_door_poddoor>(locate(3, 3, 1));
            open_blast = Dm.@new<obj_machinery_door_poddoor_preopen>(locate(4, 3, 1));
            open_shutters = Dm.@new<obj_machinery_door_poddoor_shutters_preopen>(locate(5, 3, 1));
            firelock = Dm.@new<obj_machinery_door_firedoor>(locate(3, 5, 1));
        });
        var human = Human(room, 3, 4);
        room.In(() => Anomaly.Core.Tests.Tg.M4aAccess.P5.IdCards.HoldAll(human));
        Bump(room, human, blast); // can_open_with_hands FALSE (poddoor.dm:16): Bumped returns at once (door.dm:256); its buttons are M4a
        room.Step(20 * TicksPerDs);
        room.In(() =>
        {
            Assert.True(Dm.truthy(blast.density));
            Assert.True(Dm.truthy(blast.opacity));
            Assert.Equal("closed", blast.icon_state);
            Assert.False(Dm.truthy(open_blast.density)); // poddoor.dm:279, preopen
            Assert.False(Dm.truthy(open_blast.opacity));
            Assert.Equal("open", open_blast.icon_state);
            Assert.False(Dm.truthy(open_shutters.density)); // shutters.dm:35
            Assert.False(Dm.truthy(firelock.density)); // D117: firelocks stay open (since M4b slice 2 they are live and close on a hot or cold neighbour or a pulled fire alarm)
            Assert.False(Dm.truthy(firelock.opacity));
        });
        room.In(() =>
        {
            // their custom_materials (poddoor.dm:17, shutters.dm:41) pass the deferred initialize_materials (M4a): the whole
            // Initialize runs, the machine registers and the door's own Initialize sets its click flag (door.dm:126-129)
            foreach (obj_machinery_door door in new obj_machinery_door[] { blast, open_blast, open_shutters })
            {
                Assert.True(Dm.truthy(Dm.bitand(door.flags_1, INITIALIZED_1)));
                Assert.True(Dm.@in(door, (DmList)SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor))));
                Assert.Equal(Dm.truthy(door.density), Dm.truthy(Dm.bitand(door.flags_1, PREVENT_CLICK_UNDER_1)));
            }
            Assert.True(Dm.@in(firelock, (DmList)SSmachines.get_all_machines())); // live since M4b slice 2: its Initialize registers it
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ALampHasTgsLightVars()
    {
        obj_machinery_light lamp = null;
        // loaded with the map: only a mapload lamp mounts itself (light.dm:118-119)
        var room = Room(mapExtras: w => lamp = Dm.@new<obj_machinery_light_directional_west>(locate(2, 4, 1))); // against the west wall (x 1)
        room.In(() =>
        {
            Assert.True(Dm.eq(lamp.status, LIGHT_OK), "the 2% break on init (light.dm:134) hit this seed: pick another");
            Assert.True(Dm.truthy(lamp.on)); // post_machine_initialize -> power_change -> set_on (light.dm:722)
            Assert.True(Dm.eq(lamp.light_range, 8)); // brightness
            Assert.True(Dm.eq(lamp.light_power, 1)); // bulb_power
            Assert.Equal("#f3fffa", lamp.light_color); // bulb_colour, LIGHT_COLOR_DEFAULT
            Assert.True(Dm.eq(lamp.light_angle, 170));
            Assert.True(Dm.eq(lamp.light_dir, EAST)); // REVERSE_DIR(dir), the light projects into the room
            Assert.True(Dm.truthy(lamp.light_on));
            Assert.Equal("tube", lamp.icon_state);
            Assert.NotNull(lamp.GetComponent(typeof(datum_component_atom_mounted))); // mounted on the wall it faces
            var late = Dm.@new<obj_machinery_light_directional_west>(locate(2, 5, 1));
            Assert.Null(late.GetComponent(typeof(datum_component_atom_mounted))); // built after the load: not mounted
        });
        room.In(() => Waiting.NoSleep(lamp.break_light_tube(TRUE)));
        room.In(() =>
        {
            Assert.True(Dm.eq(lamp.status, LIGHT_BROKEN));
            Assert.False(Dm.truthy(lamp.on));
            Assert.True(Dm.eq(lamp.light_range, 0));
            Assert.Equal("tube-broken", lamp.icon_state);
            Waiting.NoSleep(lamp.fix());
            Assert.True(Dm.eq(lamp.light_range, 8));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TheAccessHelpersAndTheBoltHelperApplyTheirPayloadsAtMapLoad()
    {
        obj_machinery_door_airlock door = null;
        var room = Room(mapExtras: w =>
        {
            door = Dm.@new<obj_machinery_door_airlock_public>(locate(4, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_access_all_engineering_general>(locate(4, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_locked>(locate(4, 4, 1));
        });
        room.In(() =>
        {
            Assert.True(Dm.@in(ACCESS_ENGINEERING, door.req_access)); // access_helpers.dm, all/engineering/general
            Assert.True(Dm.truthy(door.locked)); // mapping_helpers.dm:209
            Assert.Null(Dm.locate<obj_effect_mapping_helpers>(locate(4, 4, 1))); // the helpers qdel themselves
        });
        Assert.Empty(room.RuntimeText);
    }

    // The abandoned helper with one outcome left: tg's weights list (mapping_helpers.dm:243) holds only that key, so
    // Initialize sets ABANDON_NONE to 0 (:255) and pick_weight can only return it.
    private static obj_effect_mapping_helpers_airlock_abandoned Abandoned(int x, int y, string outcome)
    {
        var helper = Dm.@new<obj_effect_mapping_helpers_airlock_abandoned>(locate(x, y, 1));
        helper.weights = Dm.list(Dm.kv(outcome, 100));
        return helper;
    }

    [Fact]
    public void AnAbandonedAirlockIsWalledUpWithTheNearestWallsType()
    {
        obj_machinery_door_airlock door = null;
        turf old_floor = null;
        Type floor_type = null;
        var room = Room(mapExtras: w =>
        {
            Dm.@new<turf_closed_wall>(locate(3, 4, 1)); // nearer than the room's indestructible rim
            door = Dm.@new<obj_machinery_door_airlock_public>(locate(4, 4, 1));
            Abandoned(4, 4, "wall");
            old_floor = locate(4, 4, 1);
            floor_type = old_floor.type;
        });
        room.In(() =>
        {
            turf here = locate(4, 4, 1);
            // get_wall_type: the first /turf/closed of range(2, airlock), ring 1 before the rim (mapping_helpers.dm:295)
            Assert.Equal(typeof(turf_closed_wall), here.type);
            // place_on_top (baseturfs.dm:31): the old stack, then the open turf's own type, under the wall
            Assert.True(Dm.@in(floor_type, here.baseturfs));
            // TURF-IDENTITY (object-model.md 7.1): the area's per-z list holds the new turf where it held the old one
            DmList zturfs = (DmList)((area)here.loc).turfs_by_zlevel[1f];
            Assert.True(Dm.@in(here, zturfs));
            Assert.False(Dm.@in(old_floor, zturfs));
            Assert.True(QDELETED(door));
            Assert.Null(Dm.locate<obj_effect_mapping_helpers>(here)); // the helper qdels itself after the payload
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AnAbandonedAirlockBecomesTheFalseWallOfTheNearestWall()
    {
        obj_machinery_door_airlock door = null;
        Type floor_type = null;
        var room = Room(mapExtras: w =>
        {
            Dm.@new<turf_closed_wall_r_wall>(locate(5, 4, 1));
            door = Dm.@new<obj_machinery_door_airlock_public>(locate(4, 4, 1));
            Abandoned(4, 4, "falsewall");
            floor_type = locate(4, 4, 1).type;
        });
        room.In(() =>
        {
            turf here = locate(4, 4, 1);
            Assert.Equal(floor_type, here.type);
            // the first falsewall type whose walltype is the wall's (false_walls.dm: /obj/structure/falsewall/reinforced)
            obj_structure_falsewall false_wall = Dm.locate<obj_structure_falsewall>(here);
            Assert.NotNull(false_wall);
            Assert.Equal(typeof(obj_structure_falsewall_reinforced), false_wall.GetType());
            Assert.True(QDELETED(door));
        });
        Assert.Empty(room.RuntimeText);
    }
}

// Meta's init (seed 1, one shared boot): P3's mapping helpers leave no runtime (the abandoned helper's walls since M3 P3,
// the lead's assignment).
public class MetaDoorHelperTests : IClassFixture<Anomaly.Core.Tests.Tg.M3Maps.P2.MetaWorldFixture>
{
    private readonly Anomaly.Core.Tests.Tg.M3Maps.P2.MetaWorldFixture _meta;

    public MetaDoorHelperTests(Anomaly.Core.Tests.Tg.M3Maps.P2.MetaWorldFixture meta) => _meta = meta;

    [Anomaly.Core.Tests.Tg.M3Maps.P2.MetaFact]
    public void TheAbandonedAirlockHelpersRunWithoutRuntimesAndLeave()
    {
        Assert.DoesNotContain(_meta.Log.Runtimes, e => (e.StackTrace ?? "").Contains("obj_effect_mapping_helpers_airlock", StringComparison.Ordinal)
            || e.Message.Contains("mapping_helpers.dm", StringComparison.Ordinal));
        using (_meta.World.Enter())
        {
            int left = 0;
            foreach (obj_effect_mapping_helpers_airlock_abandoned helper in Dm.each<obj_effect_mapping_helpers_airlock_abandoned>())
                left++;
            Assert.Equal(0, left);
        }
    }
}
