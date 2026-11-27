using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.M5.FurnitureTests;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 computer frames (D171, game/machinery/computer/buildandrepair.dm) in a booted toy room: a power monitoring console is taken
/// apart with a screwdriver (_computer.dm:94, spawn_frame), the frame is taken down stage by stage and built up again with the
/// real tools, items and sounds into a working console; a frame made from five sheets of metal (the recipe) takes a board, cable
/// and glass to a computer; frames are taken apart (unanchor, the screwdriver and the welder) and leave what tg says.
/// </summary>
public class ComputerFrameTests
{
    private const int Second = 20;

    private static float State(AudioRoom r, obj_structure_frame_computer f) => r.In(() => Dm.num(f.state));

    private static string Icon(AudioRoom r, atom a) => r.In(() => Dm.str(a.icon_state));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => h.File == file);

    // puts the item in Bob's active hand (his other items are deleted)
    private static void Hand(AudioRoom r, mob_living_carbon_human bob, obj_item item) => r.In(() =>
    {
        foreach (object held in Dm.each(bob.held_items.Copy()))
            if (held is obj_item old && old != item)
                qdel(old);
        Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(item))), "the item reaches the hand");
    });

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o switch
    {
        string t => t,
        mutable_appearance m => m.icon_state,
        DmAppearance d => d.icon_state,
        _ => null,
    }).Where(s => s != null).ToList());

    private static T Take<T>(AudioRoom r, int x = 5, int y = 4) where T : obj_item => On<T>(r, x, y).First();

    [Fact]
    public void AnInertConsoleTakenApartWithAScrewdriverLeavesItsFrameAndBoard()
    {
        var (r, bob) = Room(7103);
        // the experimental cloner's console is inert (D112): its Initialize, which makes the board (_machinery.dm:179), never ran (the
        // launchpad console was the example until the anomaly lab's pads were ported, the teleporter console until gaps TP1 made it
        // live, the station alert console until E6, the library console until the library's machines were ported)
        var console = Place<obj_machinery_computer_experimental_cloner>(r);
        r.Step(Second);
        Assert.True(r.In(() => InertSeam.IsInert(console)));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, console, Left, 3);
        Assert.True(r.In(() => QDELETED(console)), string.Join("\n", r.Room.RuntimeText));
        var frame = Assert.Single(On<obj_structure_frame_computer>(r));
        Assert.Equal(FRAME_COMPUTER_STATE_GLASSED, State(r, frame));
        Assert.IsType<obj_item_circuitboard_computer_experimental_cloner>(r.In(() => frame.circuit_value));
        Assert.Same(r.In(() => frame.circuit_value), r.In(() => Dm.each(frame.contents).Single()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APowerMonitorIsTakenApartWithAScrewdriverAndBuiltBackStageByStageIntoAWorkingConsole()
    {
        var (r, bob) = Room(7101);
        var monitor = Place<obj_machinery_computer_monitor>(r);
        r.Step(Second);
        var board = r.In(() => monitor.circuit_value);
        Assert.IsType<obj_item_circuitboard_computer_powermonitor>(board);
        // the board came with the machine as its component (circuitboard.dm:35): a computer board is live now
        Assert.Equal(new object[] { board }, r.In(() => Dm.each(monitor.component_parts).ToArray()));
        float dir = r.In(() => Dm.num(monitor.dir));

        // ---- 1. The screwdriver takes the monitor out (_computer.dm:94, 2 s): the frame is glassed, anchored, wired
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, monitor, Left, 3);
        Assert.True(r.In(() => QDELETED(monitor)));
        var frame = Assert.Single(On<obj_structure_frame_computer>(r));
        Assert.Equal(FRAME_COMPUTER_STATE_GLASSED, State(r, frame));
        Assert.Equal("4", Icon(r, frame));
        Assert.True(r.In(() => Dm.truthy(frame.anchored)));
        Assert.Equal(dir, r.In(() => Dm.num(frame.dir)));
        Assert.Same(board, r.In(() => frame.circuit_value));
        Assert.Same(board, r.In(() => Dm.each(frame.contents).Single()));
        // the screwdriver's usesound is a pick of its two (screwdriver.dm:25)
        Assert.Contains(r.Sounds.Heard, h => h.File is "sound/items/tools/screwdriver.ogg" or "sound/items/tools/screwdriver2.ogg");

        // ---- 2. Down: the crowbar pries out the glass (2 glass sheets), the wirecutter the cable (5), the screwdriver
        //         unsecures the board, the crowbar takes it out
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.Equal(FRAME_COMPUTER_STATE_WIRED, State(r, frame));
        Assert.Equal("3", Icon(r, frame));
        Assert.Equal(2f, Amount<obj_item_stack_sheet_glass>(r));
        Assert.True(Heard(r, "sound/items/tools/crowbar.ogg"));
        // a crowbar on a wired frame: remove the wiring first (:210)
        Click(r, bob, frame, Left, 1);
        Assert.Equal(FRAME_COMPUTER_STATE_WIRED, State(r, frame));
        Hold<obj_item_wirecutters>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.Equal(FRAME_COMPUTER_STATE_BOARD_SECURED, State(r, frame));
        Assert.Equal("2", Icon(r, frame));
        Assert.Equal(5f, Amount<obj_item_stack_cable_coil>(r));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.Equal(FRAME_COMPUTER_STATE_BOARD_INSTALLED, State(r, frame));
        Assert.Equal("1", Icon(r, frame));
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.Equal(FRAME_COMPUTER_STATE_EMPTY, State(r, frame));
        Assert.Equal("0", Icon(r, frame));
        Assert.Null(r.In(() => frame.circuit_value));
        Assert.Same(board, Take<obj_item_circuitboard>(r)); // Exited (constructable_frame.dm:44) took it out of the frame

        // ---- 3. Up again, with the tools and the dropped items
        Hand(r, bob, board);
        Click(r, bob, frame, Left, 1);
        Assert.Equal(FRAME_COMPUTER_STATE_BOARD_INSTALLED, State(r, frame));
        Assert.Equal("1", Icon(r, frame));
        Assert.Same(board, r.In(() => frame.circuit_value));
        Assert.True(Heard(r, "sound/items/deconstruct.ogg"));
        // glass or cable in the wrong order is refused
        Hand(r, bob, Take<obj_item_stack_cable_coil>(r));
        Click(r, bob, frame, Left, 3);
        Assert.Equal(FRAME_COMPUTER_STATE_BOARD_INSTALLED, State(r, frame));
        r.In(() =>
        {
            obj_item cable = (obj_item)bob.get_active_held_item();
            Waiting.NoSleep(bob.dropItemToGround(cable));
            Waiting.NoSleep(cable.forceMove(get_turf(frame)));
        });
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.Equal(FRAME_COMPUTER_STATE_BOARD_SECURED, State(r, frame));
        Assert.Equal("2", Icon(r, frame));
        Hand(r, bob, Take<obj_item_stack_cable_coil>(r));
        Click(r, bob, frame, Left, 3); // 2 s
        Assert.Equal(FRAME_COMPUTER_STATE_WIRED, State(r, frame));
        Assert.Equal("3", Icon(r, frame));
        Assert.Empty(On<obj_item_stack_cable_coil>(r)); // all 5 used
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1); // "no glass!" (:186)
        Assert.Equal(FRAME_COMPUTER_STATE_WIRED, State(r, frame));
        Hand(r, bob, Take<obj_item_stack_sheet_glass>(r));
        Click(r, bob, frame, Left, 3);
        Assert.Equal(FRAME_COMPUTER_STATE_GLASSED, State(r, frame));
        Assert.Equal("4", Icon(r, frame));
        Assert.Empty(On<obj_item_stack_sheet_glass>(r));

        // ---- 4. The screwdriver completes it: a new console of the board's build_path, with the frame's parts
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.True(r.In(() => QDELETED(frame)));
        var built = Assert.Single(On<obj_machinery_computer_monitor>(r));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(board, built.circuit_value);
            Assert.Equal(new object[] { board }, Dm.each(built.component_parts).ToArray());
            Assert.Equal(new object[] { board }, Dm.each(built.contents).ToArray()); // the machine's own default board is gone (clear_components)
            Assert.True(Dm.truthy(built.anchored));
            Assert.Equal(dir, Dm.num(built.dir));
            Assert.Equal("power", built.icon_screen);
        });
        Assert.Contains("power", Overlays(r, built));
        Assert.Contains("power_key", Overlays(r, built));
        Assert.Empty(On<obj_item_circuitboard>(r));
        // and it comes apart again
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, built, Left, 3);
        Assert.True(r.In(() => QDELETED(built)));
        Assert.Equal(FRAME_COMPUTER_STATE_GLASSED, State(r, Assert.Single(On<obj_structure_frame_computer>(r))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFrameMadeFromFiveSheetsOfMetalTakesABoardCableAndGlassToAComputer()
    {
        var (r, bob) = Room(7102);
        // the metal sheet's recipe (sheet_types.dm:103): five sheets, a computer frame under the builder
        var iron = Hold<obj_item_stack_sheet_iron>(r, bob, 20);
        // nothing dense is built where the builder stands (DV65): Bob aims at the tile north of him
        r.In(() =>
        {
            Waiting.NoSleep(bob.forceMove(locate(4, 5, 1)));
            Waiting.NoSleep(bob.setDir(NORTH));
            bob.client.dm_aim_turf = locate(4, 6, 1);
        });
        var recipe = r.In(() => Dm.each(iron.recipes).OfType<datum_stack_recipe>().Single(x => x.result_type == typeof(obj_structure_frame_computer)));
        r.In(() => Waiting.Detach(iron.make_item(bob, recipe, 1)));
        r.Step(4 * Second);
        Assert.Equal(15f, r.In(() => Dm.num(iron.amount)));
        var frame = Assert.Single(On<obj_structure_frame_computer>(r, 4, 6));
        Assert.Equal(NORTH, (int)r.In(() => Dm.num(frame.dir))); // setDir(builder.dir) (stack.dm:472)
        Assert.Equal(FRAME_COMPUTER_STATE_EMPTY, State(r, frame));
        Assert.Equal("0", Icon(r, frame));
        Assert.False(r.In(() => Dm.truthy(frame.anchored)));

        var board = r.In(() => Dm.@new<obj_item_circuitboard_computer_powermonitor>(locate(4, 5, 1)));
        // a machine board is the wrong kind (install_board: board_type)
        var wrong = r.In(() => Dm.@new<obj_item_circuitboard_machine_suit_storage_unit>(locate(4, 5, 1)));
        Hand(r, bob, wrong);
        Click(r, bob, frame, Left, 1);
        Assert.Equal(FRAME_COMPUTER_STATE_EMPTY, State(r, frame));
        Assert.Null(r.In(() => frame.circuit_value));

        // anchored with the wrench (4 s, ratchet), then the board
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, frame, Left, 5);
        Assert.True(r.In(() => Dm.truthy(frame.anchored)));
        Assert.True(Heard(r, "sound/items/tools/ratchet.ogg"));
        Hand(r, bob, board);
        Click(r, bob, frame, Left, 1);
        Assert.Equal(FRAME_COMPUTER_STATE_BOARD_INSTALLED, State(r, frame));
        // a second board does not go in (:106)
        var second = r.In(() => Dm.@new<obj_item_circuitboard_computer_powermonitor>(locate(4, 5, 1)));
        Hand(r, bob, second);
        Click(r, bob, frame, Left, 1);
        Assert.Same(board, r.In(() => frame.circuit_value));
        r.In(() => qdel(second));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1);
        Hold<obj_item_stack_cable_coil>(r, bob, 5);
        Click(r, bob, frame, Left, 3);
        Assert.Equal(FRAME_COMPUTER_STATE_WIRED, State(r, frame));
        Hold<obj_item_stack_sheet_glass>(r, bob, 2);
        Click(r, bob, frame, Left, 3);
        Assert.Equal(FRAME_COMPUTER_STATE_GLASSED, State(r, frame));
        Assert.Equal("4", Icon(r, frame));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.True(r.In(() => QDELETED(frame)));
        var built = Assert.Single(On<obj_machinery_computer_monitor>(r, 4, 6));
        Assert.Same(board, r.In(() => built.circuit_value));
        Assert.True(r.In(() => Dm.truthy(built.anchored)));
        Assert.Equal(NORTH, (int)r.In(() => Dm.num(built.dir)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnanchoredFrameCannotBeFinishedByTheScrewdriver()
    {
        var (r, bob) = Room(7103);
        var frame = Place<obj_structure_frame_computer>(r);
        var board = r.In(() => Dm.@new<obj_item_circuitboard_computer_powermonitor>(locate(5, 4, 1)));
        r.In(() =>
        {
            frame.circuit_value = board;
            frame.circuit = board.GetType();
            Waiting.NoSleep(board.forceMove(frame));
            frame.state = FRAME_COMPUTER_STATE_GLASSED;
            frame.update_appearance(UPDATE_ICON_STATE);
        });
        r.Step(2);
        Assert.Equal("4", Icon(r, frame));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.False(r.In(() => QDELETED(frame)), "frame must be anchored (:293)");
        Assert.Equal(FRAME_COMPUTER_STATE_GLASSED, State(r, frame));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFrameComesApartWithAScrewdriverOrAWelderOnceUnanchoredAndLeavesTgsPartsPerState()
    {
        var (r, bob) = Room(7104);
        var anchored = Place<obj_structure_frame_computer>(r);
        r.In(() => Waiting.NoSleep(anchored.set_anchored(TRUE)));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, anchored, Left, 1);
        Assert.False(r.In(() => QDELETED(anchored)), "must be unanchored first (constructable_frame.dm:67)");
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, anchored, Left, 5);
        Assert.False(r.In(() => Dm.truthy(anchored.anchored)));
        // the screwdriver (8 s): the frame's 5 sheets of metal
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, anchored, Left, 9);
        Assert.True(r.In(() => QDELETED(anchored)));
        Assert.Equal(5f, Amount<obj_item_stack_sheet_iron>(r));
        Assert.Empty(On<obj_structure_frame_computer>(r));

        // the welder (2 s) on a wired and glassed frame with a board: metal, the board, 2 glass sheets, 5 cable
        var full = Place<obj_structure_frame_computer>(r, 6, 4);
        var board = r.In(() => Dm.@new<obj_item_circuitboard_computer_powermonitor>(locate(6, 4, 1)));
        r.In(() =>
        {
            full.circuit_value = board;
            full.circuit = board.GetType();
            Waiting.NoSleep(board.forceMove(full));
            full.state = FRAME_COMPUTER_STATE_GLASSED;
            Waiting.NoSleep(full.deconstruct(TRUE));
        });
        r.Step(2);
        Assert.Equal(5f, Amount<obj_item_stack_sheet_iron>(r, 6, 4));
        Assert.Equal(2f, Amount<obj_item_stack_sheet_glass>(r, 6, 4));
        Assert.Equal(5f, Amount<obj_item_stack_cable_coil>(r, 6, 4));
        Assert.Contains(board, On<obj_item_circuitboard>(r, 6, 4));
        // broken apart (disassembled FALSE): two shards, no glass; a half built frame only its cable
        var broken = Place<obj_structure_frame_computer>(r, 7, 4);
        r.In(() =>
        {
            broken.state = FRAME_COMPUTER_STATE_GLASSED;
            Waiting.NoSleep(broken.deconstruct(FALSE));
        });
        r.Step(2);
        Assert.Equal(2, On<obj_item_shard>(r, 7, 4).Count);
        Assert.Equal(0f, Amount<obj_item_stack_sheet_glass>(r, 7, 4));
        Assert.Equal(5f, Amount<obj_item_stack_cable_coil>(r, 7, 4));
        var welded = Place<obj_structure_frame_computer>(r, 8, 4);
        Hold<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(8, 5, 1))));
        Click(r, bob, welded, Left, 3);
        Assert.True(r.In(() => QDELETED(welded)));
        Assert.Equal(5f, Amount<obj_item_stack_sheet_iron>(r, 8, 4));
        r.AssertNoRuntimes();
    }
}
