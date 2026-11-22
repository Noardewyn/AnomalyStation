using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 5: tg's window and grille construction (game/objects/structures/window.dm, grille.dm) in a booted toy room, by a
/// joined player's clicks: glass sheets on a grille make an unanchored full-tile window (grille.dm:248-305) that lets the air
/// through until a screwdriver fastens it (window.dm:228, :413); the normal window's screwdriver, crowbar and wrench steps back
/// to its glass; the reinforced window's right-click sequence (welder, screwdriver, crowbar, wirecutters, wrench:
/// window.dm:529-636), its cooling bolts and its way back into the frame; the welder's repair; the grille's wirecutters,
/// screwdriver, rods on a broken grille and kick; the rotation element on an unanchored directional window.
/// </summary>
public class WindowTests
{
    private const int Second = 20;
    private const string Left = "left=1;icon-x=16;icon-y=16";
    private const string Right = "right=1;button=right;icon-x=16;icon-y=16";

    private static T Place<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.@new<T>(locate(x, y, 1)));

    private static List<T> On<T>(AudioRoom r, int x, int y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    private static mob_living_carbon_human Builder(AudioRoom r, int x, int y)
    {
        mob_living_carbon_human bob = r.Join(x, y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return bob;
    }

    /// <summary>A new <typeparamref name="T"/> in the builder's active hand, the hands emptied first.</summary>
    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, params object[] args) where T : obj_item => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        var all = new object[args.Length + 1];
        all[0] = get_turf(who);
        args.CopyTo(all, 1);
        T item = Dm.@new<T>(all);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item))), "the item reaches a hand");
        return item;
    });

    private static void Click(AudioRoom r, mob who, atom target, string @params, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, @params));
        r.Step((int)(seconds * Second));
    }

    private static bool AtmosPasses(AudioRoom r, obj_structure_window w)
        => r.In(() => Dm.truthy(w.can_atmos_pass__proc(get_step(w, WEST) as turf)));

    [Fact]
    public void GlassOnAGrilleMakesAnUnanchoredWindowThatAScrewdriverFastens()
    {
        var r = new AudioRoom(10, 10, seed: 5501);
        var grille = Place<obj_structure_grille>(r, 5, 5);
        mob_living_carbon_human bob = Builder(r, 4, 5);

        // one sheet is not enough (grille.dm:250)
        var one = Hold<obj_item_stack_sheet_glass>(r, bob, 1);
        Click(r, bob, grille, Left, 3);
        Assert.Empty(On<obj_structure_window>(r, 5, 5));

        var glass = Hold<obj_item_stack_sheet_glass>(r, bob, 5);
        Click(r, bob, grille, Left, 1);
        Assert.Empty(On<obj_structure_window>(r, 5, 5)); // the 2 s do_after is still running
        r.Step(2 * Second);
        var window = Assert.Single(On<obj_structure_window>(r, 5, 5));
        r.In(() =>
        {
            Assert.IsType<obj_structure_window_fulltile>(window);
            Assert.False(Dm.truthy(window.anchored), "a window placed on a grille comes unanchored (grille.dm:300)");
            Assert.Equal(WINDOW_OUT_OF_FRAME, Dm.num(window.state));
            Assert.Equal(3f, Dm.num(glass.get_amount()));
        });
        Assert.True(AtmosPasses(r, window), "an unanchored window lets the air through (window.dm:414)");

        // a second window is refused
        Click(r, bob, grille, Left, 3);
        Assert.Single(On<obj_structure_window>(r, 5, 5));
        Assert.Equal(3f, r.In(() => Dm.num(glass.get_amount())));

        // the screwdriver fastens the frame to the floor in decon_speed (3 s) and seals the tile
        Hold<obj_item_screwdriver>(r, bob);
        r.Sounds.Heard.Clear();
        Click(r, bob, window, Left, 1);
        Assert.False(r.In(() => Dm.truthy(window.anchored)));
        r.Step(3 * Second);
        Assert.True(r.In(() => Dm.truthy(window.anchored)));
        Assert.False(AtmosPasses(r, window), "an anchored full-tile window blocks the air");
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File != null && h.File.StartsWith("sound/items/tools/screwdriver", StringComparison.Ordinal));

        // reinforced glass makes a reinforced window
        var grille2 = Place<obj_structure_grille>(r, 5, 6);
        Hold<obj_item_stack_sheet_rglass>(r, bob, 2);
        Click(r, bob, grille2, Left, 3);
        Assert.IsType<obj_structure_window_reinforced_fulltile>(Assert.Single(On<obj_structure_window>(r, 5, 6)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ANormalWindowComesApartWithAScrewdriverACrowbarAndAWrench()
    {
        var r = new AudioRoom(10, 10, seed: 5502);
        var window = Place<obj_structure_window_fulltile>(r, 5, 5);
        mob_living_carbon_human bob = Builder(r, 4, 5);
        Assert.Equal(WINDOW_SCREWED_TO_FRAME, r.In(() => Dm.num(window.state)));

        // a wrench does nothing to an anchored window
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, window, Left, 4);
        Assert.False(r.In(() => QDELETED(window)));

        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, window, Left, 4);
        Assert.Equal(WINDOW_IN_FRAME, r.In(() => Dm.num(window.state)));
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, window, Left, 11);
        Assert.Equal(WINDOW_OUT_OF_FRAME, r.In(() => Dm.num(window.state)));
        Assert.True(r.In(() => Dm.truthy(window.anchored)));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, window, Left, 4);
        Assert.False(r.In(() => Dm.truthy(window.anchored)));

        r.Sounds.Heard.Clear();
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, window, Left, 4);
        Assert.True(r.In(() => QDELETED(window)));
        var sheets = Assert.Single(On<obj_item_stack_sheet_glass>(r, 4, 5));
        Assert.Equal(2f, r.In(() => Dm.num(sheets.get_amount()))); // a full-tile window's glass_amount
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == "sound/items/deconstruct.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheReinforcedWindowsDeconstructionGoesThroughItsStatesInOrderAndBack()
    {
        var r = new AudioRoom(10, 10, seed: 5503);
        var window = Place<obj_structure_window_reinforced_fulltile>(r, 5, 5);
        mob_living_carbon_human bob = Builder(r, 4, 5);
        float State() => r.In(() => Dm.num(window.state));
        Assert.Equal(RWINDOW_SECURE, State());

        // a tool out of order is refused (item_interaction_secondary, window.dm:529)
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, window, Right, 6);
        Assert.Equal(RWINDOW_SECURE, State());

        // heating the security screws takes 15 s; they cool back after 30 s (cool_bolts, window.dm:638)
        var welder = Hold<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        Click(r, bob, window, Right, 16);
        Assert.Equal(RWINDOW_BOLTS_HEATED, State());
        r.Step(31 * Second);
        Assert.Equal(RWINDOW_SECURE, State());
        if (!r.In(() => Dm.truthy(welder.welding)))
            r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        Click(r, bob, window, Right, 16);
        Assert.Equal(RWINDOW_BOLTS_HEATED, State());

        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, window, Right, 6);
        Assert.Equal(RWINDOW_BOLTS_OUT, State());
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, window, Right, 5);
        Assert.Equal(RWINDOW_POPPED, State());
        Hold<obj_item_wirecutters>(r, bob);
        Click(r, bob, window, Right, 3);
        Assert.Equal(RWINDOW_BARS_CUT, State());
        Assert.False(AtmosPasses(r, window));
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, window, Right, 5);
        Assert.Equal(WINDOW_OUT_OF_FRAME, State());
        Assert.False(r.In(() => Dm.truthy(window.anchored)));
        Assert.True(AtmosPasses(r, window), "the window popped loose lets the air through");

        // back: the screwdriver fastens the frame, the crowbar levers it into the frame (window.dm:555)
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, window, Left, 4);
        Assert.True(r.In(() => Dm.truthy(window.anchored)));
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, window, Left, 11);
        Assert.Equal(RWINDOW_SECURE, State());
        Assert.False(AtmosPasses(r, window));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnanchoredReinforcedWindowIsWrenchedApartIntoItsReinforcedGlass()
    {
        var r = new AudioRoom(10, 10, seed: 5504);
        var window = Place<obj_structure_window_reinforced_fulltile_unanchored>(r, 5, 5);
        mob_living_carbon_human bob = Builder(r, 4, 5);
        Assert.Equal(WINDOW_OUT_OF_FRAME, r.In(() => Dm.num(window.state)));
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, window, Left, 4);
        Assert.True(r.In(() => QDELETED(window)));
        var sheets = Assert.Single(On<obj_item_stack_sheet_rglass>(r, 4, 5));
        Assert.Equal(2f, r.In(() => Dm.num(sheets.get_amount())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWelderRepairsADamagedWindow()
    {
        var r = new AudioRoom(10, 10, seed: 5505);
        var window = Place<obj_structure_window_fulltile>(r, 5, 5);
        mob_living_carbon_human bob = Builder(r, 4, 5);
        float max = r.In(() => window.max_integrity);
        r.In(() => Waiting.NoSleep(window.take_damage(30, BRUTE, "")));
        Assert.Equal(max - 30f, r.In(() => Dm.num(window.get_integrity())));
        var welder = Hold<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        Click(r, bob, window, Left, 5);
        Assert.Equal(max, r.In(() => Dm.num(window.get_integrity())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGrilleIsCutUnfastenedRebuiltWithRodsAndKicked()
    {
        var r = new AudioRoom(10, 10, seed: 5506);
        var grille = Place<obj_structure_grille>(r, 5, 5);
        var cut = Place<obj_structure_grille>(r, 5, 6);
        mob_living_carbon_human bob = Builder(r, 4, 5);

        // the screwdriver toggles its anchoring at once (grille.dm:221); glass needs it fastened
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, grille, Left, 1);
        Assert.False(r.In(() => Dm.truthy(grille.anchored)));
        Hold<obj_item_stack_sheet_glass>(r, bob, 2);
        Click(r, bob, grille, Left, 3);
        Assert.Empty(On<obj_structure_window>(r, 5, 5));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, grille, Left, 1);
        Assert.True(r.In(() => Dm.truthy(grille.anchored)));

        // wirecutters (right click) cut it into its two rods (grille.dm:213, deconstruct(TRUE))
        r.Sounds.Heard.Clear();
        Hold<obj_item_wirecutters>(r, bob);
        Click(r, bob, cut, Right, 1);
        Assert.True(r.In(() => QDELETED(cut)));
        var rods = Assert.Single(On<obj_item_stack_rods>(r, 5, 6));
        Assert.Equal(2f, r.In(() => Dm.num(rods.get_amount())));

        // a kick (grille.dm:183): rand(5, 10) brute
        float max = r.In(() => grille.max_integrity);
        Hold<obj_item_wirecutters>(r, bob);
        r.In(() => { foreach (object held in Dm.each(bob.held_items.Copy())) if (held is obj_item o) qdel(o); });
        Click(r, bob, grille, Left, 1);
        float kicked = max - r.In(() => Dm.num(grille.get_integrity()));
        Assert.InRange(kicked, 5f * 0.5f, 10f); // through its 50 melee armour: 2.5 to 5
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == "sound/effects/grillehit.ogg");

        // broken, rods rebuild it in 1 s (grille.dm:235, repair_grille :343)
        r.In(() => Waiting.NoSleep(grille.take_damage(1000, BRUTE, "")));
        Assert.True(r.In(() => Dm.truthy(grille.broken)));
        Assert.Equal("brokengrille", r.In(() => grille.icon_state));
        var hand = Hold<obj_item_stack_rods>(r, bob, 3);
        Click(r, bob, grille, Left, 2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(grille.broken));
            Assert.True(Dm.truthy(grille.density));
            Assert.Equal(max, Dm.num(grille.get_integrity()));
            Assert.Equal("grille", grille.icon_state);
            Assert.Equal(2f, Dm.num(hand.get_amount()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnanchoredDirectionalWindowRotatesWithAltClick()
    {
        var r = new AudioRoom(10, 10, seed: 5507);
        var window = Place<obj_structure_window_unanchored>(r, 5, 5);
        var fixedWindow = Place<obj_structure_window>(r, 5, 6);
        mob_living_carbon_human bob = Builder(r, 4, 5);
        r.In(() => { foreach (object held in Dm.each(bob.held_items.Copy())) if (held is obj_item o) qdel(o); });
        float before = r.In(() => Dm.num(window.dir));
        Click(r, bob, window, "left=1;alt=1;icon-x=16;icon-y=16", 1);
        Assert.Equal(Dm.num(turn(before, ROTATION_COUNTERCLOCKWISE)), r.In(() => Dm.num(window.dir)));
        Click(r, bob, window, "right=1;button=right;alt=1;icon-x=16;icon-y=16", 1);
        Assert.Equal(before, r.In(() => Dm.num(window.dir)));
        // an anchored one stays (rotation.dm:100: "need to unscrew!")
        float fixedDir = r.In(() => Dm.num(fixedWindow.dir));
        Click(r, bob, fixedWindow, "left=1;alt=1;icon-x=16;icon-y=16", 1);
        Assert.Equal(fixedDir, r.In(() => Dm.num(fixedWindow.dir)));
        r.AssertNoRuntimes();
    }
}
