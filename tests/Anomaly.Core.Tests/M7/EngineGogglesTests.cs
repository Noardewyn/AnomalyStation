using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 (D170): the engineering scanner goggles (engine_goggles.dm): the engineer's (off, meson, T-ray) and the atmospheric technician's
/// (off, T-ray, pipe connections, thermal air view) with tg's order of modes, icon states, glass colours, the meson trait and sight, and
/// the images of each scan mode shown to the wearer's client only (client.images).
/// </summary>
public class EngineGogglesTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed, Action<World> extras = null)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            // iron tiles over the room (the cables under them are hidden by tg's undertile element and marked for the T-ray)
            for (int x = 1; x <= 12; x++)
                for (int y = 1; y <= 9; y++)
                    Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
            for (int x = 2; x <= 11; x++)
                Dm.@new<obj_structure_cable>(locate(x, 3, 1));
            extras?.Invoke(w);
        });
        return r;
    }

    private static T Wear<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item_clothing_glasses_meson_engine => r.In(() =>
    {
        T glasses = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(glasses, ITEM_SLOT_EYES))), "the goggles go on the eyes");
        return glasses;
    });

    private static void Toggle(AudioRoom r, obj_item_clothing_glasses_meson_engine g, mob_living_carbon_human who) => r.In(() => Waiting.NoSleep(g.attack_self(who)));

    private static string[] Images(AudioRoom r, mob_living_carbon_human who) => r.In(() => Dm.each_cast<image>(who.client.images).Select(i => $"{i.icon_state}@{(i.loc as turf)?.x},{(i.loc as turf)?.y}").ToArray());

    private static void StepUntil(AudioRoom r, Func<bool> done, int seconds = 3)
    {
        for (int i = 0; i < Second * seconds && !r.In(done); i++)
            r.Step(1);
    }

    [Fact]
    public void TheEngineersGogglesCycleOffMesonTRayWithTheirIconStatesGlassColoursAndTheMesonTraitAndSight()
    {
        var r = Room(9001);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        var goggles = Wear<obj_item_clothing_glasses_meson_engine>(r, bob);
        r.In(() =>
        {
            Assert.Equal(new[] { "", "meson", "t-ray" }, Dm.each(goggles.modes).Select(Dm.str));
            Assert.Equal("", goggles.mode);
            Assert.Equal("trayson-", goggles.icon_state); // Initialize ends with update_appearance (:40): "trayson-[mode]"
            Assert.Equal("trayson-", Dm.str(goggles.inhand_icon_state));
            Assert.Equal(typeof(datum_client_colour_glass_colour_gray), goggles.glass_colour_type);
            Assert.Single(Dm.each(goggles.actions));
            Assert.IsType<datum_action_item_action_toggle_mode>(Dm.each(goggles.actions).Single());
            // tg: the meson's clothing_traits are inherited and only the T-ray mode takes them away, so the off mode still sees turfs
            Assert.True(HAS_TRAIT(bob, TRAIT_MESON_VISION));
        });

        // meson: attach_clothing_traits (the trait stays), the cutoffs list(15, 12, 0), the yellow glass
        Toggle(r, goggles, bob);
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("meson", goggles.mode);
            Assert.Equal("trayson-meson", goggles.icon_state);
            Assert.Equal("trayson-meson", Dm.str(goggles.inhand_icon_state));
            Assert.True(HAS_TRAIT(bob, TRAIT_MESON_VISION));
            Assert.True(Dm.truthy(Dm.bitand(bob.sight, SEE_TURFS)), "SEE_TURFS from TRAIT_MESON_VISION");
            Assert.Equal(new float[] { 15, 12, 0 }, Dm.each(goggles.color_cutoffs).Select(Dm.num));
            Assert.Equal(15f, Dm.num(bob.lighting_color_cutoffs[1]), 3);
            Assert.Equal(12f, Dm.num(bob.lighting_color_cutoffs[2]), 3);
            Assert.Equal(typeof(datum_client_colour_glass_colour_yellow), goggles.glass_colour_type);
        });
        Assert.Contains(chat.Html, h => h.Contains("<span class='notice'>You turn the goggles to meson mode.</span>", StringComparison.Ordinal));

        // t-ray: the trait goes (detach_clothing_traits), the cutoffs go, the glass is light blue
        Toggle(r, goggles, bob);
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("t-ray", goggles.mode);
            Assert.Equal("trayson-t-ray", goggles.icon_state);
            Assert.False(HAS_TRAIT(bob, TRAIT_MESON_VISION));
            Assert.False(Dm.truthy(Dm.bitand(bob.sight, SEE_TURFS)));
            Assert.Equal(0, Dm.num(goggles.color_cutoffs?.len ?? 0f));
            Assert.Equal(0f, Dm.num(bob.lighting_color_cutoffs[1]), 3);
            Assert.Equal(typeof(datum_client_colour_glass_colour_lightblue), goggles.glass_colour_type);
        });
        Assert.Contains(chat.Html, h => h.Contains("You turn the goggles to t-ray mode.", StringComparison.Ordinal));

        // back to off (the list wraps): the initial glass colour again; the trait stays gone (the T-ray mode removed it from the list)
        Toggle(r, goggles, bob);
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("", goggles.mode);
            Assert.Equal(1f, goggles.mode_index);
            Assert.Equal("trayson-", goggles.icon_state);
            Assert.Equal(typeof(datum_client_colour_glass_colour_gray), goggles.glass_colour_type);
            Assert.False(HAS_TRAIT(bob, TRAIT_MESON_VISION));
        });
        Assert.Contains(chat.Html, h => h.Contains("You turn the goggles off.", StringComparison.Ordinal));

        // the trait is the mode's, not the glasses': an involuntary toggle (voluntary 0) is a warning in the third person
        r.In(() => Waiting.NoSleep(goggles.toggle_mode(bob, FALSE)));
        Assert.Contains(chat.Html, h => h.Contains("<span class='warning'>The goggles turn to meson mode!</span>", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAtmosphericTechniciansGogglesCycleOffTRayConnectableAndThermalAndTheTRayGogglesHaveARangeOfTwo()
    {
        var r = Room(9002);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        var goggles = Wear<obj_item_clothing_glasses_meson_engine_tray>(r, bob);
        r.In(() =>
        {
            Assert.Equal("optical t-ray scanner", goggles.name);
            Assert.Equal(2f, goggles.range);
            Assert.Equal(new[] { "", "t-ray", "connectable", "atmospheric-thermal" }, Dm.each(goggles.modes).Select(Dm.str));
        });
        string[] expected = { "t-ray", "connectable", "atmospheric-thermal", "" };
        Type[] glass = { typeof(datum_client_colour_glass_colour_lightblue), typeof(datum_client_colour_glass_colour_lightblue), null, typeof(datum_client_colour_glass_colour_gray) };
        for (int i = 0; i < expected.Length; i++)
        {
            Toggle(r, goggles, bob);
            r.In(() =>
            {
                Assert.Equal(expected[i], goggles.mode);
                Assert.Equal("trayson-" + expected[i], goggles.icon_state);
                if (glass[i] != null) // the thermal mode keeps the colour of the mode before (no case in toggle_mode)
                    Assert.Equal(glass[i], goggles.glass_colour_type);
                else
                    Assert.Equal(typeof(datum_client_colour_glass_colour_lightblue), goggles.glass_colour_type);
            });
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAtmosImagingGogglesAreOffAndThermalOnly()
    {
        var r = Room(9003);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var goggles = Wear<obj_item_clothing_glasses_meson_engine_atmos_imaging>(r, bob);
        r.In(() => Assert.Equal("trayson-", goggles.icon_state));
        Toggle(r, goggles, bob);
        r.In(() =>
        {
            Assert.Equal("atmospheric-thermal", goggles.mode);
            Assert.Equal("trayson-atmospheric-thermal", goggles.icon_state);
            Assert.Equal("trayson-atmospheric-thermal", Dm.str(goggles.inhand_icon_state));
        });
        Toggle(r, goggles, bob);
        r.In(() => Assert.Equal("trayson-", goggles.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTRayModeShowsTheHiddenCablesInRangeToTheWearerOnlyAndNotWhenTheGogglesAreOnlyHeldOrOff()
    {
        var r = Room(9004);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        mob_living_carbon_human eve = r.Join(11, 5, "Eve");
        r.Step(Second);
        var goggles = Wear<obj_item_clothing_glasses_meson_engine>(r, bob);
        r.Step(Second * 3);
        Assert.Empty(Images(r, bob)); // off: no scan (process runs in the mode's switch)

        Toggle(r, goggles, bob); // meson
        Toggle(r, goggles, bob); // t-ray
        StepUntil(r, () => bob.client.images.Count > 0);
        r.In(() =>
        {
            var shown = Dm.each_cast<image>(bob.client.images).ToList();
            // range 1 (the engineer's goggles): the cables of row 3 at x 2 to 4 (orange(1) round (3, 4))
            var expected = Dm.each<obj>(orange(1, bob)).Where(o => HAS_TRAIT(o, TRAIT_T_RAY_VISIBLE)).ToList();
            Assert.Equal(3, expected.Count);
            Assert.Equal(expected.Count, shown.Count);
            Assert.All(shown, i => Assert.Equal(128f, i.alpha));
            Assert.Equal(0, eve.client.images.Count);
        });

        // taken off the eyes into the hand: process returns at "user.glasses != src" (:90), nothing is shown after the images time out
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(goggles)));
        r.Step(Second * 5);
        Assert.Empty(Images(r, bob));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTRayModeShowsThePipesCablesAndMetersInAWall()
    {
        // D289: the cables and pipes under the walls show too (a wall's underfloor_accessibility is tg's UNDERFLOOR_HIDDEN);
        // DV356: and the visible pipes and gas meters mapped into a wall, which the 3D wall buries
        var r = Room(9010, w =>
        {
            Dm.@new<turf_closed_wall>(locate(4, 6, 1));
            Dm.@new<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_hidden>(locate(4, 6, 1));
            Dm.@new<obj_structure_cable>(locate(4, 6, 1));
            Dm.@new<turf_closed_wall>(locate(5, 6, 1));
            Dm.@new<obj_machinery_atmospherics_pipe_smart_simple_cyan_visible>(locate(5, 6, 1));
            Dm.@new<obj_machinery_meter>(locate(5, 6, 1));
        });
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        var goggles = Wear<obj_item_clothing_glasses_meson_engine>(r, bob);
        Toggle(r, goggles, bob); // meson
        Toggle(r, goggles, bob); // t-ray
        StepUntil(r, () => bob.client.images.Count > 0);
        r.In(() =>
        {
            var shown = Dm.each_cast<image>(bob.client.images).ToList();
            Assert.Equal(2, shown.Count(i => i.loc is turf t && t.x == 4 && t.y == 6));
            Assert.Equal(2, shown.Count(i => i.loc is turf t && t.x == 5 && t.y == 6));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AVisiblePipeAndAMeterInAWallAreWrenchedOutFromTheNextTileOntoTheWrenchersTile()
    {
        // DV356: the client picks them through the wall in T-ray mode; tg's own Adjacent and wrench_act then take them apart as in 2D;
        // the fitting and the meter drop at the wrencher's feet, not in the wall cell out of reach
        var r = Room(9011, w =>
        {
            Dm.@new<turf_closed_wall>(locate(4, 6, 1));
            Dm.@new<obj_machinery_atmospherics_pipe_smart_simple_cyan_visible>(locate(4, 6, 1));
            Dm.@new<obj_machinery_meter>(locate(4, 6, 1));
        });
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        var (pipe, meter) = r.In(() => (Dm.each_cast<atom>(locate(4, 6, 1).contents).OfType<obj_machinery_atmospherics_pipe>().Single(),
            Dm.each_cast<atom>(locate(4, 6, 1).contents).OfType<obj_machinery_meter>().Single()));
        M5.WallTests.Hold<obj_item_wrench>(r, bob);
        M5.WallTests.Click(r, bob, meter, 6); // meter.dm:174, 4 s
        Assert.True(r.In(() => QDELETED(meter)), "the meter is unwrenched");
        M5.WallTests.Click(r, bob, pipe, 6);
        Assert.True(r.In(() => QDELETED(pipe)), "the pipe is unwrenched");
        r.In(() =>
        {
            Assert.Empty(Dm.each_cast<atom>(locate(4, 6, 1).contents).OfType<obj_item>());
            var here = Dm.each_cast<atom>(locate(4, 5, 1).contents).ToList();
            Assert.Single(here.OfType<obj_item_pipe_meter>());
            Assert.Single(here.OfType<obj_item_pipe>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAtmosTechniciansTRayModeReachesTwoTilesAndAGogglesDropClearsTheCachedConnections()
    {
        var r = Room(9005);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        var goggles = Wear<obj_item_clothing_glasses_meson_engine_tray>(r, bob);
        Toggle(r, goggles, bob); // t-ray
        StepUntil(r, () => bob.client.images.Count > 0);
        r.In(() =>
        {
            var expected = Dm.each<obj>(orange(2, bob)).Where(o => HAS_TRAIT(o, TRAIT_T_RAY_VISIBLE)).ToList();
            Assert.Equal(4, expected.Count); // x 2 to 5 of row 3 (y 2 to 6, x 1 to 5)
            Assert.Equal(expected.Count, bob.client.images.Count);
            goggles.connection_images[bob] = Dm.list(); // anything in the cache
            Assert.Equal(1f, goggles.connection_images.len);
        });
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(goggles)));
        r.In(() => Assert.Equal(0f, goggles.connection_images.len)); // /tray/dropped (:159)
        r.AssertNoRuntimes();
    }

    private static List<obj_machinery_atmospherics_pipe_smart> Pipes(AudioRoom r, params (int X, int Y)[] at)
    {
        var made = new List<obj_machinery_atmospherics_pipe_smart>();
        foreach ((int x, int y) in at)
            made.Add(r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>(locate(x, y, 1))));
        r.In(() => SSair.setup_template_machinery(Dm.list(made.ToArray())));
        r.Step(Second);
        return made;
    }

    [Fact]
    public void ThePipeConnectionModeShowsAnArrowForEveryInitDirectionOfTheSmartPipesInRangeInThePipesColourAndForgetsThoseLeftBehind()
    {
        var r = Room(9006);
        // a line of three pipes next to Bob (4, 6) at (4, 5), (5, 5), (6, 5) and a far one at (11, 8)
        var pipes = Pipes(r, (4, 6), (5, 6), (6, 6), (11, 8));
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        var goggles = Wear<obj_item_clothing_glasses_meson_engine_tray>(r, bob);
        Toggle(r, goggles, bob); // t-ray
        Toggle(r, goggles, bob); // connectable
        r.In(() => Assert.Equal("connectable", goggles.mode));
        StepUntil(r, () => bob.client.images.Count > 0);
        r.In(() =>
        {
            // range 2 round (4, 5): x 2 to 6, y 3 to 7: the three near pipes
            var near = pipes.Take(3).ToList();
            int arrows = 0;
            foreach (var pipe in near)
            {
                float dirs = Dm.num(pipe.get_init_directions());
                var mine = Dm.each_cast<image>(bob.client.images).Where(i => ReferenceEquals(i.loc, pipe.loc)).ToList();
                var cardinals = new[] { (int)NORTH, (int)SOUTH, (int)EAST, (int)WEST }.Where(d => ((int)dirs & d) != 0).ToList();
                Assert.NotEmpty(cardinals);
                Assert.Equal(cardinals.Count, mine.Count);
                Assert.Equal(cardinals.OrderBy(d => d), mine.Select(i => (int)i.dir).OrderBy(d => d));
                foreach (image arrow in mine)
                {
                    Assert.Equal("connection_overlay", arrow.icon_state);
                    Assert.Equal("icons/obj/pipes_n_cables/simple.dmi", Dm.str(arrow.icon).Replace("\\", "/"));
                    Assert.Equal(Dm.str(pipe.pipe_color), Dm.str(arrow.color));
                    Assert.Equal(pipe.layer, arrow.layer);
                    Assert.Equal((pipe.piping_layer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X, arrow.pixel_x);
                }
                arrows += mine.Count;
                Assert.True(Dm.truthy(goggles.connection_images[pipe]), "cached");
            }
            Assert.Equal(arrows, bob.client.images.Count);
            Assert.False(Dm.truthy(goggles.connection_images[pipes[3]])); // the far one is not seen
        });

        // the arrows stay between the refreshes (DV91, a steady view), the cache stays
        r.Step((int)(1.6f * Second));
        Assert.NotEmpty(Images(r, bob));
        r.In(() => Assert.Equal(3f, goggles.connection_images.len));

        // walking away: the pipes beyond the range leave the cache (:126), the ones in range of the new place come in
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(10, 7, 1))));
        StepUntil(r, () => Dm.each_cast<image>(bob.client.images).Any(i => ReferenceEquals(i.loc, pipes[3].loc)), 4);
        r.In(() =>
        {
            Assert.Equal(1f, goggles.connection_images.len);
            Assert.True(Dm.truthy(goggles.connection_images[pipes[3]]));
            Assert.All(Dm.each_cast<image>(bob.client.images), i => Assert.Same(pipes[3].loc, i.loc));
        });

        // another mode cuts the cache (toggle_mode :50)
        Toggle(r, goggles, bob);
        r.In(() => Assert.Equal(0f, goggles.connection_images.len));
        r.AssertNoRuntimes();
    }

    private static string Hex(object color) => Dm.str(color).ToLowerInvariant();

    private static void AssertColour(string expected, object actual, int tolerance = 1)
    {
        string a = Hex(actual);
        Assert.Equal(7, a.Length);
        for (int i = 1; i < 7; i += 2)
        {
            int want = Convert.ToInt32(expected.Substring(i, 2), 16), got = Convert.ToInt32(a.Substring(i, 2), 16);
            Assert.True(Math.Abs(want - got) <= tolerance, $"{expected} vs {a}");
        }
    }

    [Fact]
    public void TheThermalModeCoversTheOpenTurfsInViewWithTgsColourOfTheirAirsTemperatureAtAlpha200ForOneSecond()
    {
        var r = Room(9007, w => Dm.@new<turf_closed_wall>(locate(6, 5, 1)));
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        // four temperatures on four tiles: below cyan, lime to yellow and yellow to red, over red
        (int X, int Y, float Kelvin, string Want, int Tolerance)[] heat =
        {
            (3, 5, 200, COLOR_STRONG_BLUE, 0),
            (4, 6, 350, "#ffbf00", 1), // BlendRGB(yellow, red, (350 - 300) / 200 = 0.25)
            (4, 4, 400, "#ff8000", 1), // 0.5
            (5, 5, 600, COLOR_RED, 0),
            (3, 6, 290, "#86e11e", 3), // BlendRGB(lime, yellow, (290 - 283.15) / 16.85 = 0.41 rounded to 0.01)
            (3, 4, 278, "#18b79b", 3), // BlendRGB(dark cyan, lime, (278 - 273.15) / 10 = 0.485)
        };
        r.In(() =>
        {
            foreach (var h in heat)
                ((turf_open)locate(h.X, h.Y, 1)).air.temperature = h.Kelvin;
        });
        var goggles = Wear<obj_item_clothing_glasses_meson_engine_atmos_imaging>(r, bob);
        r.In(() => atmos_thermal(bob)); // the call process makes in the mode; the timing is below
        r.In(() =>
        {
            var shown = Dm.each_cast<image>(bob.client.images).ToList();
            Assert.All(shown, i =>
            {
                Assert.Equal("greyOverlay", i.icon_state);
                Assert.Equal("icons/turf/overlays.dmi", Dm.str(i.icon).Replace("\\", "/"));
                Assert.Equal(200f, i.alpha);
                Assert.Equal((float)ABOVE_OPEN_TURF_LAYER, i.layer);
                Assert.Equal(MOUSE_OPACITY_TRANSPARENT, i.mouse_opacity);
            });
            // the wall (6, 5) blocks air: no overlay; every open turf of view(5) has one
            var expectedTurfs = Dm.each<turf>(view(5, bob)).Where(t => !Dm.truthy(t.blocks_air)).ToList();
            Assert.Equal(expectedTurfs.Count, shown.Count);
            Assert.DoesNotContain(shown, i => ReferenceEquals(i.loc, locate(6, 5, 1)));
            foreach (var h in heat)
            {
                image at = shown.Single(i => ReferenceEquals(i.loc, locate(h.X, h.Y, 1)));
                AssertColour(h.Want, at.color, h.Tolerance);
            }
        });

        // process runs the call in the mode (every 2 s SSobj tick) and the overlay lasts one second (duration 10)
        r.In(() => bob.client.images.Cut());
        Toggle(r, goggles, bob);
        r.In(() => Assert.Equal("atmospheric-thermal", goggles.mode));
        StepUntil(r, () => bob.client.images.Count > 0, 4);
        r.In(() => Assert.True(bob.client.images.Count > 20, "images " + bob.client.images.Count));
        r.Step((int)(1.1f * Second));
        r.In(() => Assert.NotEqual(0, bob.client.images.Count)); // still there: DV91, the view is steady
        r.AssertNoRuntimes();
    }

    // DV91 / D173: the images are on the client at every tick across several refreshes (never none, never two sets), and once the
    // goggles are off the eyes the last set goes by its own timer within about 2.5 s.
    private static void AssertSteadyThenGone(AudioRoom r, mob_living_carbon_human bob, obj_item_clothing_glasses_meson_engine goggles)
    {
        StepUntil(r, () => bob.client.images.Count > 0);
        int n = r.In(() => bob.client.images.Count);
        for (int t = 0; t < Second * 7; t++) // three or four refreshes (SSobj fires every 2 s)
        {
            r.Step(1);
            r.In(() =>
            {
                Assert.Equal(n, bob.client.images.Count);
                Assert.Equal(n, Dm.each_cast<image>(bob.client.images).Distinct().Count());
            });
        }
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(goggles)));
        r.Step((int)(2.6f * Second));
        Assert.Empty(Images(r, bob));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTRayModeShowsItsImagesSteadilyAndTheyGoWhenTheGogglesAreTakenOff()
    {
        var r = Room(9010);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        var goggles = Wear<obj_item_clothing_glasses_meson_engine>(r, bob);
        Toggle(r, goggles, bob);
        Toggle(r, goggles, bob);
        r.In(() => Assert.Equal("t-ray", goggles.mode));
        AssertSteadyThenGone(r, bob, goggles);
    }

    [Fact]
    public void ThePipeConnectionModeShowsItsArrowsSteadilyAndTheyGoWhenTheGogglesAreTakenOff()
    {
        var r = Room(9011);
        Pipes(r, (4, 6), (5, 6), (6, 6));
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        var goggles = Wear<obj_item_clothing_glasses_meson_engine_tray>(r, bob);
        Toggle(r, goggles, bob);
        Toggle(r, goggles, bob);
        r.In(() => Assert.Equal("connectable", goggles.mode));
        AssertSteadyThenGone(r, bob, goggles);
    }

    [Fact]
    public void TheThermalModeShowsItsOverlaysSteadilyAndTheyGoWhenTheGogglesAreTakenOff()
    {
        var r = Room(9012);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        var goggles = Wear<obj_item_clothing_glasses_meson_engine_atmos_imaging>(r, bob);
        Toggle(r, goggles, bob);
        Toggle(r, goggles, bob);
        Toggle(r, goggles, bob);
        r.In(() => Assert.Equal("atmospheric-thermal", goggles.mode));
        AssertSteadyThenGone(r, bob, goggles);
    }

    [Fact]
    public void TheTRayModeRescansTheMomentTheWearerStepsToAnotherTile()
    {
        var r = Room(9009);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        var goggles = Wear<obj_item_clothing_glasses_meson_engine>(r, bob);
        Toggle(r, goggles, bob); // meson
        Toggle(r, goggles, bob); // t-ray
        StepUntil(r, () => bob.client.images.Count > 0);
        Assert.DoesNotContain("@9,3", string.Join(" ", Images(r, bob)));
        // DV91 (D289): tg waits for the next SSobj fire (up to 2 s); the port refreshes on the goggles' next fast process after the step
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(9, 4, 1))));
        r.Step(Second / 2);
        Assert.Contains("@9,3", string.Join(" ", Images(r, bob)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void GogglesThatAreDestroyedStopProcessingAndDoNotThrowInTheShuttleMode()
    {
        var r = Room(9008);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var goggles = Wear<obj_item_clothing_glasses_meson_engine_tray>(r, bob);
        r.In(() => Assert.Contains(goggles, Dm.each(SSfastprocess.processing)));
        r.In(() => qdel(goggles));
        r.Step(Second);
        r.In(() => Assert.DoesNotContain(goggles, Dm.each(SSfastprocess.processing)));

        // the shuttle goggles' mode is a labelled leaf (shuttles, M10): the colour is tg's red, the scan is not translated
        var shuttle = r.In(() => Dm.@new<obj_item_clothing_glasses_meson_engine_shuttle>(get_turf(bob)));
        r.In(() =>
        {
            Waiting.NoSleep(shuttle.toggle_mode(bob, TRUE));
            Assert.Equal("shuttle", shuttle.mode);
            Assert.Equal("trayson-shuttle", shuttle.icon_state);
            Assert.Equal(typeof(datum_client_colour_glass_colour_red), shuttle.glass_colour_type);
        });
        r.AssertNoRuntimes();
    }
}
