using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aTools.P4;

/// <summary>
/// M4a P4b: the reinforced table's welder (tables_racks.dm:784 welder_act_secondary: a lit welder's right click weakens
/// it after use_tool's 5 second do_after, a second one strengthens it), its wrench right click (:814, through
/// attempt_electrocution's early returns) and the operating table's item_interaction (:1134: a breath mask is attached, an
/// ordinary item falls through to the base table's placing, :1032 table_place_act).
/// </summary>
public class P4bReinforcedTableTests
{
    private const int Second = 20; // ticks per second
    private const string Left = "left=1;button=left";
    private const string Right = "right=1;button=right";

    private static void Click(CapsuleRoom room, mob who, atom target, string @params, int seconds = 1)
    {
        room.Command(() => ClientSeam.Click(who.client, target, @params));
        room.Step(seconds * Second);
    }

    /// <summary>A reinforced table east of the human, on a tiled floor (overfloor_placed, as the station's tables stand).</summary>
    private static obj_structure_table_reinforced ReinforcedTable(CapsuleRoom room)
    {
        var table = room.In(() =>
        {
            var floor = (turf)Waiting.NoSleep(locate(6, 5, 1).ChangeTurf(typeof(turf_open_floor_iron)));
            Assert.True(Dm.truthy(floor.overfloor_placed));
            return Dm.@new<obj_structure_table_reinforced>(floor);
        });
        room.Step(Second);
        return table;
    }

    private static bool Ready(CapsuleRoom room, obj_structure_table table) => room.In(() => Dm.truthy(table.deconstruction_ready));

    [Fact]
    public void ALitWelderWeakensAReinforcedTableAndStrengthensItAgain()
    {
        var (room, a) = P4aToolTests.Room(4421);
        var table = ReinforcedTable(room);
        var welder = P4aToolTests.Hold<obj_item_weldingtool>(room, a);
        Assert.False(Ready(room, table), "a reinforced table starts welded");

        // unlit: tool_start_check refuses, nothing starts
        Click(room, a, table, Right, 7);
        Assert.False(Ready(room, table), "an unlit welder does not weaken it");
        Assert.Equal(0f, room.In(() => Dm.num(a.do_after_count())));

        room.In(() => Waiting.NoSleep(welder.attack_self(a)));
        Click(room, a, table, Right, 2);
        Assert.False(Ready(room, table), "not before use_tool's 5 second do_after");
        Assert.Equal(1f, room.In(() => Dm.num(a.do_after_count())));
        room.Step(4 * Second);
        Assert.True(Ready(room, table), "weakened after the do_after");
        Assert.Equal(0f, room.In(() => Dm.num(a.do_after_count())));
        Assert.Same(a, room.In(() => welder.loc));

        Click(room, a, table, Right, 6);
        Assert.False(Ready(room, table), "a second weld strengthens it");
        Assert.Same(a, room.In(() => welder.loc));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AttemptElectrocutionStopsAtTgsEarlyReturns()
    {
        var (room, a) = P4aToolTests.Room(4422);
        var table = ReinforcedTable(room);
        room.In(() =>
        {
            // over a tiled floor
            Assert.Equal(0f, Dm.num(Waiting.NoSleep(table.attempt_electrocution(a))));
            // out of range
            var far = Dm.@new<obj_structure_table_reinforced>(locate(9, 9, 1));
            Assert.False(in_range(far, a));
            Assert.Equal(0f, Dm.num(Waiting.NoSleep(far.attempt_electrocution(a))));
            // unanchored
            far.anchored = FALSE;
            Assert.Equal(0f, Dm.num(Waiting.NoSleep(far.attempt_electrocution(a))));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AWrenchRightClickOnAReinforcedTableRaisesNoRuntime()
    {
        var (room, a) = P4aToolTests.Room(4423);
        var table = ReinforcedTable(room);
        P4aToolTests.Hold<obj_item_wrench>(room, a);

        Click(room, a, table, Right, 5);
        Assert.False(Ready(room, table));
        Assert.False(room.In(() => QDELETED(table)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemClickedOnAnOperatingTableLandsOnItsTurf()
    {
        var (room, a) = P4aToolTests.Room(4424);
        var table = room.In(() => Dm.@new<obj_structure_table_optable>(locate(6, 5, 1)));
        room.Step(Second);
        var crowbar = P4aToolTests.Hold<obj_item_crowbar>(room, a);

        Click(room, a, table, Left);
        room.In(() =>
        {
            Assert.Same(table.loc, crowbar.loc);
            Assert.False(Dm.@in(crowbar, a.held_items));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ABreathMaskUsedOnAnOperatingTableIsAttached()
    {
        // The breath mask is an inert type that no hand picks up yet (its pickup and equipped are not ported), so the
        // branch is driven directly with the mask on the floor, which transferItemToLoc moves as tg's does.
        var (room, a) = P4aToolTests.Room(4425);
        var table = room.In(() => Dm.@new<obj_structure_table_optable>(locate(6, 5, 1)));
        var mask = room.In(() => Dm.@new<obj_item_clothing_mask_breath>(locate(5, 5, 1)));
        room.Step(Second);

        room.In(() =>
        {
            object result = Waiting.NoSleep(table.item_interaction(a, mask, Dm.list(Dm.kv(LEFT_CLICK, "1"))));
            Assert.Equal((float)ITEM_INTERACT_SUCCESS, Dm.num(result));
            Assert.Same(table, mask.loc);
            Assert.Same(mask, table.breath_mask);
        });
        room.AssertNoRuntimes();
    }
}
