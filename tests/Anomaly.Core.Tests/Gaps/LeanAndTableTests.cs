using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package L1 (D249, D327): leaning on walls, the table flip and the table slam in a booted toy room. A crewman facing a wall drags
/// himself onto it, is turned around (DV421) and leans (offset towards it, passable) until he turns; a window takes the lean too, a border window does
/// not; a table flips over from the radial's Flip, not the right click (DV503: the things on it thrown off its far side, passage blocked
/// on that side, which the capsule's view sees as a face, tg's flipped picture) and back; a pulled crewman is placed on a table out of combat mode and slammed onto it from an aggressive grab.
/// </summary>
public class LeanAndTableTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, feed, bob);
    }

    // a drag of Bob onto the target, as the client's drop sends it (base_mouse_drop_handler with Bob as usr)
    private static void DragSelfOnto(AudioRoom r, mob_living_carbon_human bob, atom target) => r.In(() =>
    {
        World.Current.usr = bob;
        Waiting.NoSleep(bob.base_mouse_drop_handler(target, bob.loc, target is turf ? target : target.loc, ""));
        return 0;
    });

    [Fact]
    public void A_crewman_leans_on_the_wall_behind_him_until_he_turns()
    {
        var (r, feed, bob) = Room(9951);
        var wall = r.In(() =>
        {
            var t = locate(4, 5, 1);
            Waiting.NoSleep(t.ChangeTurf(typeof(turf_closed_wall)));
            return (turf_closed_wall)locate(4, 5, 1);
        });
        // beside it, facing away sideways, he cannot lean on it
        r.In(() => Waiting.NoSleep(bob.setDir(EAST)));
        DragSelfOnto(r, bob, wall);
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_UNDENSE)), "not his back to it");

        // a player facing it (the view turns his mob, DV15) is turned around (DV421) and leans: offset north towards it, passable
        r.In(() => Waiting.NoSleep(bob.setDir(NORTH)));
        DragSelfOnto(r, bob, wall);
        Assert.Equal(SOUTH, r.In(() => bob.dir));
        Assert.True(r.In(() => HAS_TRAIT_FROM(bob, TRAIT_UNDENSE, LEANING_TRAIT)), "leaning: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Equal(11f, r.In(() => Dm.num(bob.pixel_y)));
        Assert.True(feed.Any("You lean against"), "the lean is told");
        var leanable = r.In(() => wall.GetComponent(typeof(datum_component_leanable)) as datum_component_leanable);
        Assert.Contains(bob, r.In(() => Dm.each<mob_living>(leanable.leaning_mobs).ToList()));

        // turning ends it
        r.In(() => Waiting.NoSleep(bob.setDir(EAST)));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_UNDENSE)), "turned away");
        Assert.Equal(0f, r.In(() => Dm.num(bob.pixel_y)));
        Assert.Empty(r.In(() => Dm.each<mob_living>(leanable.leaning_mobs).ToList()));

        // the second lean goes through the component's own drop handler
        r.In(() => Waiting.NoSleep(bob.setDir(SOUTH)));
        DragSelfOnto(r, bob, wall);
        Assert.True(r.In(() => HAS_TRAIT_FROM(bob, TRAIT_UNDENSE, LEANING_TRAIT)), "leaning again");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_fulltile_window_takes_the_lean_and_a_border_window_does_not()
    {
        var (r, feed, bob) = Room(9952);
        var full = r.In(() => Dm.@new<obj_structure_window_fulltile>(locate(4, 5, 1)));
        r.In(() => Waiting.NoSleep(bob.setDir(SOUTH)));
        DragSelfOnto(r, bob, full);
        Assert.True(r.In(() => HAS_TRAIT_FROM(bob, TRAIT_UNDENSE, LEANING_TRAIT)), "leaning on the full window");
        r.In(() => Waiting.NoSleep(bob.setDir(EAST)));

        var border = r.In(() => Dm.@new<obj_structure_window>(locate(3, 4, 1)));
        r.In(() => Waiting.NoSleep(bob.setDir(EAST)));
        DragSelfOnto(r, bob, border);
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_UNDENSE)), "a border window takes no lean");
        Assert.Null(r.In(() => border.GetComponent(typeof(datum_component_leanable))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_table_flips_over_from_the_radial_and_back()
    {
        var (r, feed, bob) = Room(9953);
        var table = Place<obj_structure_table>(r, 5, 4);
        var cup = r.In(() => Dm.@new<obj_item_reagent_containers_cup_glass_coffee>(locate(5, 4, 1)));
        Assert.True(r.In(() => LocomotionSeam.IsSolid(World.Current, 5, 4, 1)), "an upright table is a solid tile");
        // DV503: the right click no longer flips
        r.Command(() => Waiting.Detach(table.attack_hand_secondary(bob)));
        r.Step(4 * Second);
        Assert.Equal(FALSE, r.In(() => table.is_flipped));
        Assert.Contains((RadialExtra.Flip, "Flip"), r.In(() => RadialExtras.Of(bob, table)));
        r.Command(() => Waiting.Detach(table.radial_flip(bob)));
        r.Step(4 * Second); // max_integrity * 0.25 = 2.5 s
        Assert.Equal(TRUE, r.In(() => table.is_flipped));
        Assert.Equal(EAST, r.In(() => table.dir)); // flipped away from Bob
        Assert.Equal("icons/obj/flipped_tables.dmi", r.In(() => (table.icon as DmResource)?.Path ?? Dm.str(table.icon)));
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(table.pass_flags_self, PASSTABLE))), "no longer a table to climb");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/trayhit/trayhit1.ogg");
        Assert.True(r.In(() => cup.loc == locate(6, 4, 1)), "the cup is thrown off the far side");
        // the flipped side blocks: from Bob's side in, east out
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(table.CanAllowThrough(bob, WEST)))), "passable from its open side");
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(table.CanAllowThrough(bob, EAST)))), "blocked by its top");
        // the capsule's view (the server's and the client's prediction) sees the same: a face on its east side, not a solid tile
        Assert.False(r.In(() => LocomotionSeam.IsSolid(World.Current, 5, 4, 1)), "a flipped table is no solid tile");
        Assert.Equal(Anomaly.Core.Motion.TileEdges.East, r.In(() => LocomotionSeam.FacesOf(World.Current, 5, 4, 1)));
        Assert.Contains((RadialExtra.Flip, "Flip upright"), r.In(() => RadialExtras.Of(bob, table)));

        r.Command(() => Waiting.Detach(table.radial_flip(bob)));
        r.Step(4 * Second);
        Assert.Equal(FALSE, r.In(() => table.is_flipped));
        Assert.Equal("icons/obj/smooth_structures/table.dmi", r.In(() => (table.icon as DmResource)?.Path ?? Dm.str(table.icon)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/trayhit/trayhit2.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_pulled_crewman_is_placed_on_a_table_or_slammed_onto_it()
    {
        var (r, feed, bob) = Room(9954);
        mob_living_carbon_human alice = r.Join(3, 4, "Alice");
        var table = Place<obj_structure_table>(r, 5, 4);

        // out of combat mode: placed after 3.5 seconds, lying down
        r.In(() => Waiting.NoSleep(bob.start_pulling(alice)));
        r.Command(() => Waiting.Detach(table.attack_hand(bob)));
        r.Step(5 * Second);
        Assert.True(r.In(() => alice.loc == table.loc), "placed on the table: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
        string aliceName = r.In(() => Dm.str(alice.name));
        Assert.True(feed.Any("places " + aliceName + " onto"), "the placing is told");
        Assert.Null(r.In(() => bob.pulling));

        // from an aggressive grab in combat mode: slammed, knocked down and hurt
        r.In(() => Waiting.NoSleep(alice.forceMove(locate(3, 4, 1))));
        r.In(() =>
        {
            Waiting.NoSleep(alice.set_resting(FALSE, TRUE));
            bob.set_combat_mode(TRUE);
            Waiting.NoSleep(bob.start_pulling(alice));
            Waiting.NoSleep(bob.setGrabState(GRAB_AGGRESSIVE));
            return 0;
        });
        float brute = r.In(() => Dm.num(alice.get_brute_loss()));
        float stamina = r.In(() => Dm.num(alice.get_stamina_loss()));
        r.Command(() => Waiting.Detach(table.attack_hand(bob)));
        r.Step(Second);
        Assert.True(r.In(() => alice.loc == table.loc), "slammed onto the table: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(feed.Any("slams " + aliceName + " onto"), "the slam is told");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/tableslam.ogg");
        Assert.Equal(brute + 10, r.In(() => Dm.num(alice.get_brute_loss())));
        Assert.True(r.In(() => Dm.num(alice.get_stamina_loss())) >= stamina + 39, "stamina damage");
        Assert.True(r.In(() => HAS_TRAIT(alice, TRAIT_KNOCKEDOUT) || alice.body_position == LYING_DOWN), "knocked down");
        r.AssertNoRuntimes();
    }
}
