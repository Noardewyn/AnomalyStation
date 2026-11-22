using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 power helpers slice: tg's rapid pipe cleaner layer (game/objects/items/rcd/RCL.dm) and the pipe cleaners it lays
/// (modules/power/pipecleaners.dm), the old cable code minus the power network. A coil laid by hand makes a stub on the tile
/// and joins stubs into wire; the layer takes a coil (its colour set from the layer's), is wielded with both hands, and lays
/// the wire behind its holder as he walks: a stub on each tile he enters, the previous tile's stub joined to the way he went,
/// at the cost of a piece for each; it runs empty, its colour action cycles the colour, its radial wiring menu picks the
/// direction of the wire to lay at the holder's feet and stays open after a pick, and a screwdriver takes the coil out.
/// </summary>
public class RclTests
{
    private const int Second = 20;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";
    private const int Y = 5;

    private static AudioRoom Build(ulong seed, out mob_living_carbon_human bob)
    {
        var r = new AudioRoom(13, 10, seed: seed);
        bob = r.Join(3, Y, "Bob");
        mob_living_carbon_human b = bob;
        r.In(() => ADD_TRAIT(b, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return r;
    }

    /// <summary>The item on the floor under the player picked up with an empty hand (attack_hand, items.dm:586: the pickup proc).</summary>
    private static void PickUp(AudioRoom r, mob_living_carbon_human who, obj_item item)
    {
        r.In(() => Waiting.NoSleep(who.drop_all_held_items()));
        r.In(() => Waiting.NoSleep(item.forceMove(who.loc as atom)));
        Click(r, who, item);
        r.In(() => Assert.Same(item, who.get_active_held_item()));
    }

    private static turf At(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, Left));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    // the activate_inhand keybinding (Z): the held item's attack_self
    private static void UseInHand(AudioRoom r, mob_living_carbon_human who)
    {
        r.Command(() => ClientSeam.Keybinding(who.client, 4, true));
        r.Command(() => ClientSeam.Keybinding(who.client, 4, false));
        r.Step(2);
    }

    private static List<obj_structure_pipe_cleaner> Wires(AudioRoom r, int x, int y)
        => r.In(() => Dm.each<obj_structure_pipe_cleaner>(locate(x, y, 1)).ToList());

    private static string Wire(AudioRoom r, int x, int y)
    {
        var wires = Wires(r, x, y);
        return wires.Count == 0 ? null : string.Join(",", r.In(() => wires.Select(w => w.d1 + "-" + w.d2)));
    }

    private static void Step(AudioRoom r, mob_living_carbon_human who, float dir)
    {
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.Move(get_step(who, Dm.num(dir)), dir))), "the step was refused"));
        r.Step(2);
    }

    private static obj_item_stack_pipe_cleaner_coil Coil(AudioRoom r, float amount = 30, string color = null)
        => r.In(() =>
        {
            var c = (obj_item_stack_pipe_cleaner_coil)Dm.@new(typeof(obj_item_stack_pipe_cleaner_coil), locate(3, 3, 1), amount);
            if (color != null)
                c.set_pipecleaner_color(color);
            return c;
        });

    [Fact]
    public void ACoilLaysAStubOnAClickedTileAndStubsJoinIntoWireAtAPieceEach()
    {
        var r = Build(7901, out var bob);
        var coil = WallTests.Hold<obj_item_stack_pipe_cleaner_coil>(r, bob, 10);
        r.In(() =>
        {
            Assert.Equal(10f, Dm.num(coil.amount));
            Assert.Equal("pipe cleaner coil", Dm.str(coil.name));
            coil.set_pipecleaner_color("blue");
            Assert.Equal("coil_blue", coil.inhand_icon_state);
        });
        // a click on the floor next to the player: a stub at the tile, pointing at the player (get_dir(T, user))
        Click(r, bob, At(r, 4, Y));
        Assert.Equal("0-8", Wire(r, 4, Y));
        r.In(() =>
        {
            Assert.Equal(9f, Dm.num(coil.amount));
            var wire = Wires(r, 4, Y).Single();
            Assert.Equal("pipe cleaner", Dm.str(wire.name));
            Assert.Equal("0-8", wire.icon_state);
            Assert.Equal("blue", wire.pipecleaner_color);
            Assert.Equal(GLOB.cable_colors["blue"], wire.color); // the coil's colour is the wire's (param_color)
            Assert.Equal(1f, Dm.num(wire.stored.amount));
        });
        // the tile again: a stub is already there with a free end, so the click joins it (turf.dm:410): its end points at the player,
        // so a stub is made on his own tile pointing back at it (pipe_cleaner_join, pipecleaners.dm:376-398)
        Click(r, bob, At(r, 4, Y));
        Assert.Equal("0-4", Wire(r, 3, Y));
        r.In(() => Assert.Equal(8f, Dm.num(coil.amount)));
        // from the tile north of the stub, a click on it (its end points west, not at him) makes it a full wire from west to north
        r.In(() => Waiting.NoSleep(((atom_movable)bob).forceMove(locate(4, Y + 1, 1))));
        Click(r, bob, Wires(r, 4, Y).Single());
        Assert.Equal("1-8", Wire(r, 4, Y));
        r.In(() =>
        {
            Assert.Equal(7f, Dm.num(coil.amount));
            Assert.Equal(2f, Dm.num(Wires(r, 4, Y).Single().stored.amount)); // update_stored(2): a wire of two pieces
        });
        // a click on the full wire, whose north end points at him: a stub on his tile pointing back
        Click(r, bob, Wires(r, 4, Y).Single());
        Assert.Equal("0-2", Wire(r, 4, Y + 1));
        r.In(() => Assert.Equal(6f, Dm.num(coil.amount)));
        // an empty coil lays nothing: "There is no pipe cleaner left!"
        r.In(() => coil.amount = 0);
        r.In(() => Assert.Null(Waiting.NoSleep(coil.place_turf(locate(5, Y + 1, 1), bob))));
        Assert.Null(Wire(r, 5, Y + 1));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APipeCleanerIsPulledUpIntoItsCoilByTheAltClickAndAWireOfTwoGivesTwoPieces()
    {
        var r = Build(7902, out var bob);
        var wire = r.In(() => (obj_structure_pipe_cleaner)Dm.@new(typeof(obj_structure_pipe_cleaner), locate(4, Y, 1), "red"));
        r.In(() =>
        {
            Assert.Equal((0f, 1f), (wire.d1, wire.d2)); // the icon state 0-1
            Assert.Equal(1f, Dm.num(wire.stored.amount));
            wire.d1 = 4;
            wire.d2 = 8;
            wire.update_stored(2, wire.color);
            wire.update_appearance();
            Assert.Equal("4-8", wire.icon_state);
        });
        // the alt click (click_alt): pulled up, the stored coil drops on the tile
        r.Command(() => ClientSeam.Click(bob.client, wire, "icon-x=16;icon-y=16;alt=1;button=left"));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(wire));
            var pieces = Dm.each<obj_item_stack_pipe_cleaner_coil>(locate(4, Y, 1)).ToList();
            Assert.Single(pieces);
            Assert.Equal(2f, Dm.num(pieces[0].amount));
            Assert.Equal("pipe cleaner piece", Dm.str(pieces[0].name));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLayerTakesACoilWithItsColourAndShowsItsContentAndAScrewdriverTakesTheCoilOut()
    {
        var r = Build(7903, out var bob);
        var rcl = WallTests.Hold<obj_item_rcl>(r, bob);
        var coil = Coil(r, 30, "white");
        r.In(() =>
        {
            Assert.Equal("rapid pipe cleaner layer", Dm.str(rcl.name));
            Assert.Equal("rcl-0", rcl.icon_state);
            Assert.Equal(90f, rcl.max_amount);
            Assert.Equal(8f, rcl.colors.len);
            Assert.Equal(1f, rcl.current_color_index);
        });
        // the layer on the coil on the floor: the layer's click takes it in (item_interaction of the layer is the coil's click)
        r.In(() => Waiting.NoSleep(bob.drop_all_held_items()));
        var held = WallTests.Hold<obj_item_stack_pipe_cleaner_coil>(r, bob, 30);
        r.In(() => bob.swap_hand());
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(rcl)))));
        r.In(() => bob.swap_hand());
        Click(r, bob, rcl);
        r.In(() =>
        {
            Assert.Same(held, rcl.loaded);
            Assert.Same(rcl, held.loc);
            Assert.Equal(90f, held.max_amount); // "We store a lot."
            Assert.Equal("red", held.pipecleaner_color); // the layer's first colour
            Assert.Equal("rcl-10", rcl.icon_state);
            var text = Dm.each(Waiting.NoSleep(rcl.examine(bob)) as DmList).Select(Dm.str).ToList();
            Assert.Contains(text, t => t.Contains("It contains 30/90 pipe cleaners."));
        });
        // more coil adds to it up to the maximum
        var more = Coil(r, 30);
        r.In(() => { Waiting.NoSleep(bob.drop_all_held_items()); Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(more)))); });
        r.In(() => bob.swap_hand());
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(rcl)))));
        r.In(() => bob.swap_hand());
        Click(r, bob, rcl);
        r.In(() =>
        {
            Assert.Equal(60f, Dm.num(rcl.loaded.amount));
            Assert.True(QDELETED(more));
            Assert.Equal("rcl-20", rcl.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WieldedItLaysWireBehindItsHolderStubFirstThenJoinedAndRunsEmpty()
    {
        var r = Build(7904, out var bob);
        var rcl = r.In(() => Dm.@new<obj_item_rcl_pre_loaded>(At(r, 3, Y)));
        PickUp(r, bob, rcl);
        r.In(() =>
        {
            Assert.Equal(90f, Dm.num(rcl.loaded.amount));
            Assert.Equal("rcl-30", rcl.icon_state);
            Assert.False(Dm.truthy(rcl.active));
            Assert.Same(bob, rcl.listeningTo); // pickup (:148) hooks the holder's moves
        });
        // not wielded: walking lays nothing
        Step(r, bob, EAST);
        Assert.Null(Wire(r, 4, Y));
        // wielded (use in hand, two-handed): active
        UseInHand(r, bob);
        r.In(() => Assert.True(Dm.truthy(rcl.active)));
        // walking east three tiles: a stub on the first (pointing back, west = 8), joined into 4-8 on the next step, and so on; the
        // tile he stands on holds the newest stub
        Step(r, bob, EAST); // to 5,Y
        Assert.Equal("0-8", Wire(r, 5, Y));
        Step(r, bob, EAST); // to 6,Y
        Assert.Equal("4-8", Wire(r, 5, Y));
        Assert.Equal("0-8", Wire(r, 6, Y));
        Step(r, bob, EAST); // to 7,Y
        Assert.Equal("4-8", Wire(r, 6, Y));
        Assert.Equal("0-8", Wire(r, 7, Y));
        // each stub and each join cost a piece: 3 stubs and 2 joins
        r.In(() => Assert.Equal(90f - 5f, Dm.num(rcl.loaded.amount)));
        // a turn: north from 7,Y: the stub of 7,Y is joined to the way he went (west and north: 1-8) and a new stub is made to the south
        Step(r, bob, NORTH); // to 7,Y+1
        Assert.Equal("1-8", Wire(r, 7, Y));
        Assert.Equal("0-2", Wire(r, 7, Y + 1));
        r.In(() => Assert.Equal(90f - 7f, Dm.num(rcl.loaded.amount)));
        // walking back over his own wire: the step toward the stub's open end resets the layer (tg: "Did we just walk backwards?")
        Step(r, bob, SOUTH);
        r.In(() => Assert.Null(rcl.last));
        Assert.Equal("1-8", Wire(r, 7, Y)); // nothing was laid
        // the wire's colour is the layer's
        r.In(() =>
        {
            var w = Wires(r, 6, Y).Single();
            Assert.Equal("red", w.pipecleaner_color);
        });
        // a layer with a piece or two left runs empty: "The last of the pipe cleaners unreel", the coil is gone
        r.In(() => { rcl.loaded.amount = 1; rcl.last = null; });
        Step(r, bob, WEST);
        r.In(() =>
        {
            Assert.Null(rcl.loaded);
            Assert.Equal("rcl-0", rcl.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheColourActionCyclesTheEightColoursAndRecoloursTheLoadedCoil()
    {
        var r = Build(7905, out var bob);
        var rcl = r.In(() => Dm.@new<obj_item_rcl_pre_loaded>(At(r, 3, Y)));
        PickUp(r, bob, rcl);
        var expected = new[] { "yellow", "green", "blue", "pink", "orange", "cyan", "white", "red" };
        foreach (string colour in expected)
        {
            r.In(() => Waiting.NoSleep(rcl.ui_action_click(bob, Dm.@new<datum_action_item_action_rcl_col>(rcl))));
            r.In(() =>
            {
                Assert.Equal(colour, rcl.loaded.pipecleaner_color);
                Assert.Equal(GLOB.cable_colors[colour], rcl.loaded.color);
            });
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePersistentWiringMenuLaysTheChosenDirectionStaysOpenAndItsCentreClosesIt()
    {
        var r = Build(7906, out var bob);
        var rcl = r.In(() => Dm.@new<obj_item_rcl_pre_loaded>(At(r, 3, Y)));
        PickUp(r, bob, rcl);
        // the menu action opens tg's persistent radial of the eight wire directions; the menu is the player's to pick from
        r.In(() => Waiting.NoSleep(rcl.ui_action_click(bob, Dm.@new<datum_action_item_action_rcl_gui>(rcl))));
        datum_radial_menu_persistent menu = r.In(() => rcl.wiring_gui_menu);
        Assert.NotNull(menu);
        r.In(() =>
        {
            Assert.Equal(8f, menu.dm_elements.Count);
            Assert.Equal(new[] { "1", "5", "4", "6", "2", "10", "8", "9" }, menu.dm_elements.Select(e => Dm.str(menu.choices_values[e.Choice])));
            Assert.Contains(menu, Dm.each_cast<datum_radial_menu>(bob.client.dm_radial_menus));
            Assert.NotNull(menu.dm_close_button);
        });
        // a pick of the fifth slice (direction 2, south): a wire stub at the holder's feet pointing south
        r.Command(() => ClientSeam.RadialPick(bob.client, menu.dm_id, 5));
        r.Step(2);
        Assert.Equal("0-2", Wire(r, 3, Y));
        r.In(() => Assert.Equal(89f, Dm.num(rcl.loaded.amount)));
        r.In(() => Assert.Same(menu, rcl.wiring_gui_menu)); // still open
        // the menu was updated for the stub at his feet (findLinkingCable: the direction 2 is now the invalid slice)
        r.In(() => Assert.Contains(Dm.each(bob.client.dm_radial_menus), m => ReferenceEquals(m, menu)));
        // a pick of another direction on the stub: it is joined (2 and 8: 2-8) and the layer's last is reset
        r.Command(() => ClientSeam.RadialPick(bob.client, menu.dm_id, 7));
        r.Step(2);
        Assert.Equal("2-8", Wire(r, 3, Y));
        // the centre closes it (the persistent menu answers the null choice, RCL.dm:272)
        r.Command(() => ClientSeam.RadialPick(bob.client, menu.dm_id, 0));
        r.Step(2);
        r.In(() =>
        {
            Assert.Null(rcl.wiring_gui_menu);
            Assert.True(QDELETED(menu));
            Assert.DoesNotContain(Dm.each(bob.client.dm_radial_menus), m => ReferenceEquals(m, menu));
            Assert.Equal(0f, GLOB.radial_menus.len);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AScrewdriverTakesTheCoilOutInPiecesOfThirtyAndAMakeshiftLayerNeverJamsWithoutAWield()
    {
        var r = Build(7907, out var bob);
        var rcl = r.In(() => Dm.@new<obj_item_rcl_pre_loaded>(At(r, 3, Y)));
        PickUp(r, bob, rcl);
        var driver = r.In(() =>
        {
            var d = Dm.@new<obj_item_screwdriver>(get_turf(bob));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(d, merge_stacks: FALSE))));
            return d;
        });
        r.In(() => bob.swap_hand());
        r.In(() => rcl.loaded.amount = 75);
        var loadedCoil = r.In(() => rcl.loaded);
        Click(r, bob, rcl);
        r.In(() =>
        {
            Assert.Null(rcl.loaded);
            // both hands are full (the layer, the screwdriver), so the coil goes to the floor with its max back at 30 (tg: put_in_hands, else
            // forceMove), and the surplus of the 75 comes out in pieces of 30 and the 15 left over: three coils of 30, 30 and 15
            var floor = Dm.each<obj_item_stack_pipe_cleaner_coil>(get_turf(bob)).ToList();
            Assert.Contains(loadedCoil, floor);
            Assert.Equal(new[] { 15f, 30f, 30f }, floor.Select(c => Dm.num(c.amount)).OrderBy(a => a).ToArray());
            Assert.All(floor, c => Assert.Equal(30f, c.max_amount));
        });
        r.AssertNoRuntimes();
    }
}
