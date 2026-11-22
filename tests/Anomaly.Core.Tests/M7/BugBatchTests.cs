using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

public class BugBatchTests
{
    [Fact]
    public void AContinuousDriftRetainsTgsFinalGlideAndInputLockWhenHalting()
    {
        var r = new AudioRoom(9, 9, seed: 8190);
        LocomotionSeam.Install(r.World);
        var bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            for (int x = 3; x <= 5; x++)
                for (int y = 3; y <= 5; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
            LocomotionSeam.Register(bob);
            Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST)));
            var drift = Assert.IsType<datum_drift_handler>(bob.drift_handler);
            drift.glide_to_halt(3);
            Assert.False(QDELETED(drift));
            Assert.Same(drift, bob.drift_handler);
            Assert.Equal(world.time + 4, Dm.num(drift.block_inputs_until));
            Assert.True(QDELETED(drift.drifting_loop));
            Assert.Equal(COMSIG_MOB_CLIENT_BLOCK_PRE_MOVE, drift.allow_final_movement());
        });
        r.Step(12);
        Assert.Null(r.In(() => bob.drift_handler));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OpeningAnAirlocksPanelAndUsingAMultitoolOpensWorkingWireControls()
    {
        var r = new AudioRoom(9, 9, seed: 8191);
        var bob = r.Join(4, 4, "Bob");
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(5, 4, 1)));
        r.In(() =>
        {
            door.panel_open = TRUE;
            Waiting.NoSleep(bob.put_in_hands(Dm.@new<obj_item_multitool>(get_turf(bob))));
        });
        r.Command(() => ClientSeam.Click(bob.client, door, "left=1;button=left"));
        r.Step(4);
        r.In(() =>
        {
            var ui = Assert.Single(Dm.each<datum_tgui>(bob.tgui_open_uis));
            Assert.Same(door.wires, ui.src_object);
            string color = Dm.str(door.wires.get_color_of_wire(WIRE_BOLTS));
            Assert.False(Dm.truthy(door.locked));
            Assert.True(Dm.truthy(Waiting.NoSleep(door.wires.ui_act("pulse", Dm.list(Dm.kv("wire", color)), ui))));
            Assert.True(Dm.truthy(door.locked));
            Waiting.NoSleep(bob.dropItemToGround(bob.get_active_held_item() as obj_item));
            Assert.False(Dm.truthy(Waiting.NoSleep(door.wires.ui_act("pulse", Dm.list(Dm.kv("wire", color)), ui))));
            Assert.True(Dm.truthy(door.locked));
            Waiting.NoSleep(bob.put_in_hands(Dm.@new<obj_item_wirecutters>(get_turf(bob))));
            Assert.True(Dm.truthy(Waiting.NoSleep(door.wires.ui_act("cut", Dm.list(Dm.kv("wire", color)), ui))));
            Assert.True(Dm.truthy(door.wires.is_cut(WIRE_BOLTS)));
            Assert.True(Dm.truthy(Waiting.NoSleep(door.wires.ui_act("cut", Dm.list(Dm.kv("wire", color)), ui))));
            Assert.False(Dm.truthy(door.wires.is_cut(WIRE_BOLTS)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EngineeringStorageHoldsALiveModsuit()
    {
        // gaps MO1: the MOD control units deploy their parts, so the unit's EVA stopgap is gone and it holds tg's engineering MODsuit
        var r = new AudioRoom(9, 9, seed: 8192);
        r.In(() =>
        {
            var unit = Dm.@new<obj_machinery_suit_storage_unit_engine>(locate(5, 4, 1));
            Assert.Null(unit.suit_value);
            Assert.Null(unit.helmet_value);
            Assert.IsType<obj_item_mod_control_pre_equipped_engineering>(unit.mod_value);
            Assert.NotNull(unit.mask_value);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AtmosCleanupAcceptsMissingConnectionLists()
    {
        var r = new AudioRoom(9, 9, seed: 8193);
        r.In(() =>
        {
            var vent = Dm.@new<obj_machinery_atmospherics_components_unary_vent_pump>(locate(5, 4, 1));
            vent.parents = null;
            vent.airs = null;
            vent.nodes = null;
            vent.nullify_node(1f);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACameraDisappearsWhenItsSupportingWallIsDestroyed()
    {
        var r = new AudioRoom(9, 9, seed: 8194);
        obj_machinery_camera camera = null;
        r.In(() =>
        {
            Waiting.NoSleep(locate(5, 3, 1).ChangeTurf(typeof(turf_closed_wall)));
            camera = Dm.@new<obj_machinery_camera>(locate(5, 4, 1));
            Waiting.NoSleep(camera.find_and_mount_on_atom());
            Assert.True(HAS_TRAIT(camera, TRAIT_WALLMOUNTED));
            Waiting.NoSleep(locate(5, 3, 1).ChangeTurf(typeof(turf_open_floor_plating)));
        });
        r.Step(2);
        Assert.True(r.In(() => QDELETED(camera)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SuffocationCanPlayTheReferenceGaspSounds()
    {
        var r = new AudioRoom(9, 9, seed: 8195);
        var bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            var lungs = bob.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
            for (int i = 0; i < 30; i++)
                Waiting.NoSleep(lungs.handle_suffocation(bob, 0, 16, 0));
        });
        Assert.Contains(r.Sounds.Heard, sound => sound.File != null && sound.File.Contains("/gasp/gasp_", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }
}
