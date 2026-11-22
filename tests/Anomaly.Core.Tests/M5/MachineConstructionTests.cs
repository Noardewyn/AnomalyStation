using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.M5.FurnitureTests;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 7: pipes and machines taken apart and built again by the player's tools in a booted toy room. A wrenched pipe
/// becomes its fitting and lets its gas out; the fitting wrenched down joins the pipenet again (pipe/construction.dm,
/// atmosmachinery.dm). A suit storage unit's panel is screwed open and crowbarred into a machine frame, its circuit board and
/// its parts (_machinery.dm handle_deconstruct, spawn_frame), and the frame takes the board and the parts back and is screwed
/// into a working unit (machine_frame.dm). A fire alarm frame is mounted on a wall and finished with its electronics and
/// cable, then pulled (wall_mounted.dm, firealarm.dm), and taken back down to its frame.
/// </summary>
public class MachineConstructionTests
{
    private const int Second = 20;

    private static datum_pipeline Net(AudioRoom r, obj_machinery_atmospherics_pipe p) => r.In(() => p.parent);

    [Fact]
    public void AWrenchedPipeBecomesItsFittingAndTheFittingWrenchedDownJoinsThePipenet()
    {
        var (r, bob) = Room(5711);
        var pipes = r.In(() =>
        {
            var list = new List<obj_machinery_atmospherics_pipe_smart>();
            for (int x = 3; x <= 5; x++)
                list.Add(Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(x, 5, 1), TRUE, EAST));
            SSair.setup_template_machinery(Dm.list(list.Cast<object>().ToArray()));
            return list;
        });
        r.Step(Second);
        Assert.Same(Net(r, pipes[0]), Net(r, pipes[2]));
        // some gas in the line: unwrenching takes 2 s (atmosmachinery.dm:458, empty_pipe FALSE)
        r.In(() => Net(r, pipes[0]).air.merge(SSair.parse_gas_string("n2=10;TEMP=293.15") as datum_gas_mixture));

        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, pipes[1], Left, 1);
        Assert.False(r.In(() => QDELETED(pipes[1])));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(pipes[1])));
        // on_deconstruction (atmosmachinery.dm:513): the fitting of its construction_type, made from the pipe
        var fitting = Assert.Single(On<obj_item_pipe>(r, 4, 5));
        Assert.Equal(typeof(obj_item_pipe_quaternary), fitting.GetType());
        Assert.Equal(typeof(obj_machinery_atmospherics_pipe_smart), r.In(() => fitting.pipe_type));
        Assert.Equal("pipe fitting", r.In(() => fitting.name));
        r.Step(Second);
        Assert.NotSame(Net(r, pipes[0]), Net(r, pipes[2]));

        // wrenched down (construction.dm:268): a smart pipe again, on_construction joins it and SSair rebuilds one pipeline
        Click(r, bob, fitting, Left, 1);
        Assert.True(r.In(() => QDELETED(fitting)));
        var rebuilt = Assert.Single(On<obj_machinery_atmospherics_pipe_smart>(r, 4, 5));
        r.Step(2 * Second);
        Assert.NotNull(Net(r, rebuilt));
        Assert.Same(Net(r, pipes[0]), Net(r, rebuilt));
        Assert.Same(Net(r, pipes[2]), Net(r, rebuilt));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/tools/ratchet.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASuitStorageUnitIsTakenApartIntoAFrameABoardAndPartsAndBuiltAgain()
    {
        var (r, bob) = Room(5712);
        var unit = r.In(() => Dm.@new<obj_machinery_suit_storage_unit>(locate(5, 4, 1)));
        r.Step(Second);
        // Initialize (_machinery.dm:179): the board and its default parts (circuitboard.dm:141, flatten_component_list)
        var board = r.In(() => unit.circuit_value) as obj_item_circuitboard_machine_suit_storage_unit;
        Assert.NotNull(board);
        Assert.Contains(r.In(() => Dm.each(unit.component_parts).ToList()), p => p is datum_stock_part_capacitor);
        Assert.Contains(r.In(() => Dm.each(unit.component_parts).ToList()), p => p is obj_item_electronics_airlock);
        if (r.In(() => Dm.truthy(unit.state_open)))
            r.In(() => Waiting.NoSleep(unit.close_machine()));

        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, unit, Left, 1);
        Assert.True(r.In(() => Dm.truthy(unit.panel_open)));
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, unit, Left, 1);
        Assert.True(r.In(() => QDELETED(unit)));
        // handle_deconstruct (_machinery.dm:925): a wired, anchored frame (spawn_frame :970), the board, the capacitor made
        // from its stock part datum, the electronics, and the board's stacks (2 glass, 5 cable)
        var frame = Assert.Single(On<obj_structure_frame_machine>(r));
        Assert.Equal(FRAME_STATE_WIRED, r.In(() => Dm.num(frame.state)));
        Assert.True(r.In(() => Dm.truthy(frame.anchored)));
        Assert.Equal("box_1", r.In(() => frame.icon_state));
        Assert.Single(On<obj_item_circuitboard_machine_suit_storage_unit>(r));
        Assert.Single(On<obj_item_stock_parts_capacitor>(r));
        Assert.Single(On<obj_item_electronics_airlock>(r));
        Assert.Equal(2f, Amount<obj_item_stack_sheet_glass>(r));
        Assert.Equal(5f, Amount<obj_item_stack_cable_coil>(r));

        // the board back in (machine_frame.dm:140), then every component by hand (:324 add_part), then the screwdriver (:439)
        foreach (Type part in new[] { typeof(obj_item_circuitboard_machine_suit_storage_unit), typeof(obj_item_stack_sheet_glass),
            typeof(obj_item_stack_cable_coil), typeof(obj_item_stock_parts_capacitor), typeof(obj_item_electronics_airlock) })
        {
            r.In(() =>
            {
                foreach (object held in Dm.each(bob.held_items.Copy()))
                    if (held is obj_item i)
                        qdel(i);
                Waiting.NoSleep(bob.put_in_active_hand(On<obj_item>(r).First(i => i.GetType() == part)));
            });
            Click(r, bob, frame, Left, 1);
        }
        Assert.Equal(FRAME_STATE_BOARD_INSTALLED, r.In(() => Dm.num(frame.state)));
        Assert.Equal("box_2", r.In(() => frame.icon_state));
        Assert.All(r.In(() => Dm.each(frame.req_components).Select(k => Dm.num(frame.req_components[k])).ToList()), n => Assert.Equal(0f, n));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.True(r.In(() => QDELETED(frame)));
        var built = Assert.Single(On<obj_machinery_suit_storage_unit>(r));
        Assert.True(r.In(() => Dm.truthy(built.anchored)));
        Assert.IsType<obj_item_circuitboard_machine_suit_storage_unit>(r.In(() => built.circuit_value));
        Assert.Contains(r.In(() => Dm.each(built.component_parts).ToList()), p => p is datum_stock_part_capacitor);
        Assert.Empty(On<obj_item_stack>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFireAlarmFrameIsMountedFinishedPulledAndTakenDownAgain()
    {
        var (r, bob) = Room(5713);
        // tg's try_build mounts only from a floor (wall_mounted.dm:98, isfloorturf); the toy room is indestructible floor
        r.In(() => Waiting.NoSleep(locate(4, 4, 1).ChangeTurf(typeof(turf_open_floor_iron))));
        turf wall = r.In(() => (turf)Waiting.NoSleep(locate(4, 5, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.In(() => Waiting.NoSleep(bob.setDir(NORTH)));
        var frame = Hold<obj_item_wallframe_firealarm>(r, bob);
        // wall_mounted.dm:48: a click on the wall mounts its result on the user's tile facing the wall, shifted 26 px
        Click(r, bob, wall, Left, 1);
        Assert.True(r.In(() => QDELETED(frame)));
        var alarm = Assert.Single(On<obj_machinery_firealarm>(r, 4, 4));
        Assert.Equal((float)NORTH, r.In(() => Dm.num(alarm.dir)));
        Assert.Equal(26f, r.In(() => alarm.pixel_y));
        Assert.Equal(FIRE_ALARM_BUILD_NO_CIRCUIT, r.In(() => Dm.num(alarm.buildstage)));
        Assert.True(r.In(() => Dm.truthy(alarm.panel_open)));
        Assert.Equal("fire_b0", r.In(() => alarm.icon_state));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/click.ogg");

        Hold<obj_item_electronics_firealarm>(r, bob);
        Click(r, bob, alarm, Left, 1);
        Assert.Equal(FIRE_ALARM_BUILD_NO_WIRES, r.In(() => Dm.num(alarm.buildstage)));
        var coil = Hold<obj_item_stack_cable_coil>(r, bob, 10);
        Click(r, bob, alarm, Left, 1);
        Assert.Equal(FIRE_ALARM_BUILD_SECURED, r.In(() => Dm.num(alarm.buildstage)));
        Assert.Equal(5f, r.In(() => Dm.num(coil.amount)));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, alarm, Left, 1);
        Assert.False(r.In(() => Dm.truthy(alarm.panel_open)));

        // it works: an empty hand pulls it (firealarm.dm:288 -> alarm)
        r.In(() => { foreach (object held in Dm.each(bob.held_items.Copy())) if (held is obj_item i) qdel(i); });
        Click(r, bob, alarm, Left, 2);
        Assert.Equal(1f, r.In(() => Dm.num(alarm.my_area.active_alarms[ALARM_FIRE])));

        // and back down: panel, wires (5 cable), circuit (electronics), the wrench (the frame)
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, alarm, Left, 1);
        Hold<obj_item_wirecutters>(r, bob);
        Click(r, bob, alarm, Right, 1);
        Assert.Equal(FIRE_ALARM_BUILD_NO_WIRES, r.In(() => Dm.num(alarm.buildstage)));
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, alarm, Left, 3);
        Assert.Equal(FIRE_ALARM_BUILD_NO_CIRCUIT, r.In(() => Dm.num(alarm.buildstage)));
        Assert.Single(On<obj_item_electronics_firealarm>(r, 4, 4));
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, alarm, Left, 1);
        Assert.True(r.In(() => QDELETED(alarm)));
        Assert.Single(On<obj_item_wallframe_firealarm>(r, 4, 4));
        Assert.Equal(5f, Amount<obj_item_stack_cable_coil>(r, 4, 4));
        r.AssertNoRuntimes();
    }
}
