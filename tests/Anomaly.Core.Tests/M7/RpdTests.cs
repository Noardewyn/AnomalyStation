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
/// M7 slice D172: tg's rapid pipe dispenser (game/objects/items/rcd/RPD.dm) with its recipe datums (_globalvars/pipe_info.dm,
/// _globalvars/lists/pipe_recipes.dm) in a booted toy room. The window's acts pick a recipe, its direction, the layers, the colour and the
/// modes; a click on the floor lays a pipe fitting after tg's 0.4 s (one per active layer), a pipe of a device that cannot sit on the outer
/// layers is refused there, the Connect mode wrenches the fitting down into the device joined to the pipes around it (gas flows through a
/// pump laid between two pipelines), Destroy removes a loose fitting, Reprogram changes a placed smart pipe's directions as tg does, and a
/// right click copies a pipe's colour and layer. The disposal category lays a disposal segment (D325) and the transit tube category
/// lays a tube.
/// </summary>
public class RpdTests
{
    private const int Second = 20;
    private const int Y = 5;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";
    private const string Right = "icon-x=16;icon-y=16;right=1;button=right";
    private const string Deconstruct = "sound/items/deconstruct.ogg";
    private const string ToolSwitch = "sound/items/tools/tool_switch.ogg";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_item_pipe_dispenser Rpd, datum_tgui Ui);

    private static Rig Build(ulong seed, int x = 3, int y = Y)
    {
        var r = new AudioRoom(13, 10, seed: seed);
        mob_living_carbon_human bob = r.Join(x, y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        var rpd = WallTests.Hold<obj_item_pipe_dispenser>(r, bob);
        datum_tgui ui = r.In(() => Dm.@new<datum_tgui>(bob, rpd, "RapidPipeDispenser", "rapid pipe dispenser"));
        return new Rig(r, bob, rpd, ui);
    }

    private static object Act(Rig g, string action, params (string Key, object Value)[] args)
    {
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        return g.R.In(() => Waiting.NoSleep(g.Rpd.ui_act(action, p, g.Ui, null)));
    }

    /// <summary>The recipe of that category and index (the window's pipe_type act) facing <paramref name="dir"/> (its setdir act).</summary>
    private static void Pick(Rig g, string category, int index, string dir = "north", float flipped = 0)
    {
        Act(g, "pipe_type", ("category", category), ("pipe_type", index));
        Act(g, "setdir", ("dir", dir), ("flipped", flipped));
    }

    private static float PIPE_LAYER(float n) => Dm.shl(1, n - 1);

    /// <summary>Only these of tg's four mode bits on (the window's mode act flips one bit).</summary>
    private static void Modes(Rig g, float wanted)
    {
        foreach (float bit in new float[] { 1, 2, 4, 8 })
        {
            if (Dm.truthy(Dm.bitand(Dm.num(g.Rpd.mode), bit)) != Dm.truthy(Dm.bitand(wanted, bit)))
                Act(g, "mode", ("mode", bit));
        }
        Assert.Equal(wanted, Dm.num(g.Rpd.mode));
    }

    private static turf At(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));

    private static void Click(Rig g, atom target, float seconds = 1, string how = Left)
    {
        g.R.Command(() => ClientSeam.Click(g.Bob.client, target, how));
        g.R.Step((int)(seconds * Second));
    }

    private static List<T> On<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.each<T>(locate(x, y, 1)).ToList());

    /// <summary>Smart pipes on (x0..x1, y) facing east, joined as a map template's machines are.</summary>
    private static List<obj_machinery_atmospherics_pipe_smart> Pipes(AudioRoom r, int x0, int x1, int y, float layer = 3)
    {
        var made = new List<obj_machinery_atmospherics_pipe_smart>();
        for (int x = x0; x <= x1; x++)
        {
            int xx = x;
            made.Add(r.In(() =>
            {
                var p = Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(xx, y, 1), TRUE, EAST);
                p.set_piping_layer(layer);
                return p;
            }));
        }
        r.In(() => SSair.setup_template_machinery(Dm.list(made.Cast<object>().ToArray())));
        r.Step(Second);
        return made;
    }

    [Fact]
    public void TheRpdStartsOnTheFirstAtmosRecipeAndItsWindowListsTgsCategoriesAndPreviews()
    {
        var g = Build(17201);
        g.R.In(() =>
        {
            DmList recipes = GLOB.atmos_pipe_recipes;
            Assert.Equal(new[] { "Pipes", "Binary", "Devices", "Heat Exchange" }, Dm.each(recipes).Select(Dm.str));
            Assert.Equal(5f, ((DmList)recipes["Pipes"]).len);
            Assert.Equal(8f, ((DmList)recipes["Binary"]).len);
            Assert.Equal(8f, ((DmList)recipes["Devices"]).len);
            Assert.Equal(5f, ((DmList)recipes["Heat Exchange"]).len);
            var pipe = (datum_pipe_info)((DmList)recipes["Pipes"])[1];
            Assert.Same(pipe, g.Rpd.recipe);
            Assert.Equal("Pipe", Dm.str(pipe.name));
            Assert.Equal(typeof(obj_machinery_atmospherics_pipe_smart), pipe.id);
            Assert.Equal("manifold4w", Dm.str(pipe.icon_state)); // the smart pipe's pipe_state
            Assert.Equal(PIPE_ONEDIR, Dm.num(pipe.dirtype)); // its construction type's RPD_type (the quaternary fitting: one direction)
            Assert.True(Dm.truthy(pipe.all_layers));
            var pump = (datum_pipe_info)((DmList)recipes["Binary"])[3];
            Assert.Equal("Pressure Pump", Dm.str(pump.name));
            Assert.Equal("pump", Dm.str(pump.icon_state));
            Assert.Equal(PIPE_UNARY, Dm.num(pump.dirtype)); // a directional fitting
            var meter = (datum_pipe_info)((DmList)recipes["Devices"])[8];
            Assert.IsType<datum_pipe_info_meter>(meter);
            Assert.Equal(PIPE_ONEDIR, Dm.num(meter.dirtype));
            var heManifold = (datum_pipe_info)((DmList)recipes["Heat Exchange"])[2];
            Assert.False(Dm.truthy(heManifold.all_layers));
            Assert.Equal(PIPE_TRINARY, Dm.num(heManifold.dirtype));

            var data = (DmList)Waiting.NoSleep(g.Rpd.ui_data(g.Bob));
            Assert.Equal(0f, Dm.num(data["category"]));
            Assert.Equal(4f, Dm.num(data["pipe_layers"])); // PIPE_LAYER(3)
            Assert.Equal("Pipe", Dm.str(data["selected_recipe"]));
            Assert.Equal("green", Dm.str(data["selected_color"]));
            Assert.Equal(15f, Dm.num(data["mode"]));
            Assert.Equal("Pipes", Dm.str(data["selected_category"]));
            DmList categories = (DmList)data["categories"];
            Assert.Equal(4f, categories.len);
            var first = (DmList)categories[1];
            Assert.Equal("Pipes", Dm.str(first["cat_name"]));
            var firstRecipe = (DmList)((DmList)first["recipes"])[1];
            Assert.Equal(1f, Dm.num(firstRecipe["pipe_index"]));
            // the smart pipe's one preview (PIPE_ONEDIR: south, named after the recipe), selected as the recipe is
            var previews = (DmList)firstRecipe["previews"];
            Assert.Equal(1f, previews.len);
            Assert.Equal("south", Dm.str(((DmList)previews[1])["dir"]));
            Assert.Equal("Pipe", Dm.str(((DmList)previews[1])["dir_name"]));
            Assert.Equal("manifold4w", Dm.str(((DmList)previews[1])["icon_state"]));
            Assert.True(Dm.truthy(((DmList)previews[1])["selected"]));
            // the heat exchanging pipe (bendable): six previews (pipe_info.dm:29-32), none selected
            var hePreviews = (DmList)((DmList)((DmList)((DmList)categories[4])["recipes"])[1])["previews"];
            Assert.Equal(new[] { "north", "east", "northwest", "northeast", "southwest", "southeast" }, Dm.each(hePreviews).Select(p => Dm.str(((DmList)p)["dir"])));
            Assert.Equal("Vertical", Dm.str(((DmList)hePreviews[1])["dir_name"]));
            Assert.All(Dm.each(hePreviews), p => Assert.False(Dm.truthy(((DmList)p)["selected"])));
            // the gas filter (a flippable trinary): eight previews, the diagonal ones flipped (DM's && gives ISDIAGONALDIR's bit)
            var filterPreviews = (DmList)((DmList)((DmList)((DmList)categories[3])["recipes"])[1])["previews"];
            Assert.Equal(8f, filterPreviews.len);
            Assert.Equal(0f, Dm.num(((DmList)filterPreviews[1])["flipped"]));
            Assert.Equal(8f, Dm.num(((DmList)filterPreviews[2])["flipped"])); // "[SOUTHWEST]" (PIPE_TRIN_M's second): 10 & 9
            DmList init = (DmList)data["init_directions"];
            Assert.All(new[] { "north", "south", "east", "west" }, d => Assert.True(Dm.truthy(init[d])));

            var statics = (DmList)Waiting.NoSleep(g.Rpd.ui_static_data(g.Bob));
            Assert.Same(GLOB.pipe_paint_colors, statics["paint_colors"]);
            Assert.Equal(5f, Dm.num(statics["max_pipe_layers"]));
        });
        g.R.AssertNoRuntimes();
    }

    /// <summary>
    /// D320 (DV352): Shift and the wheel turn the next build through its recipe's window previews (the gas filter's eight, the flipped ones
    /// with tg's flipped bit), the wheel alone is tg's layer; the build preview is the chosen preview's picture, coloured, on its layer, and
    /// none outside the Dispense mode.
    /// </summary>
    [Fact]
    public void ShiftAndTheWheelTurnTheNextBuildAndThePreviewFollowsIt()
    {
        var g = Build(17202);
        void Wheel(int delta, string how)
        {
            g.R.Command(() => ClientSeam.MouseWheel(g.Bob.client, At(g.R, 4, Y), delta, how));
            g.R.Step(1);
        }
        Pick(g, "Devices", 1); // the gas filter, north
        Assert.Equal(typeof(obj_machinery_atmospherics_components_trinary_filter), g.R.In(() => g.Rpd.recipe.id));
        Wheel(-120, "shift=1");
        Assert.Equal((SOUTHWEST, 8f), g.R.In(() => (g.Rpd.p_dir, g.Rpd.p_flipped))); // the second preview, "North West South", flipped
        Wheel(120, "shift=1");
        Assert.Equal((NORTH, 0f), g.R.In(() => (g.Rpd.p_dir, g.Rpd.p_flipped)));
        Wheel(120, "shift=1");
        Assert.Equal((EAST, 0f), g.R.In(() => (g.Rpd.p_dir, g.Rpd.p_flipped))); // back past the first: the last, "East South West"

        // the preview is what a click at the hovered thing would build: the floor next to Bob
        turf floor = At(g.R, 4, Y);
        Hover(g, floor);
        BuildPreview p = Preview(g).Value;
        Assert.Equal(new BuildPreview("icons/obj/pipes_n_cables/pipe_item.dmi", "filter", (int)EAST, "#00FF00", 0, 0, floor), p with { Color = p.Color.ToUpperInvariant() });
        Wheel(-120, ""); // tg's own wheel: layer 3 to 4, an east fitting shifted north
        Assert.Equal(PIPE_LAYER(4), g.R.In(() => g.Rpd.pipe_layers));
        Assert.Equal(5, Preview(g).Value.PixelY);
        Hover(g, At(g.R, 5, Y));
        Assert.Null(Preview(g)); // out of reach

        // a filter laid on layer 4 hogs the tile (tg's PIPING_ONE_PER_TURF): no second device there on any layer
        Click(g, floor);
        Assert.Single(On<obj_machinery_atmospherics_components_trinary_filter>(g.R, 4, Y));
        Hover(g, floor);
        Assert.Null(Preview(g));
        Act(g, "toggle_multi_layer");
        Act(g, "pipe_layers", ("pipe_layers", PIPE_LAYER(3)));
        Assert.Null(Preview(g));
        Act(g, "toggle_multi_layer"); // back to one layer: 4

        // smart pipes on one tile, layer 4: the preview shows exactly while a click builds one more; when the layer is full, a second
        // chosen layer (5) shows and builds
        Pick(g, "Pipes", 1);
        turf run = At(g.R, 3, Y - 1);
        Hover(g, run);
        int Smart(float layer) => On<obj_machinery_atmospherics_pipe_smart>(g.R, 3, Y - 1).Count(m => m.piping_layer == layer);
        for (int i = 0; i < 4 && Preview(g) != null; i++)
        {
            int before = Smart(4);
            Click(g, run);
            Assert.Equal(before + 1, Smart(4));
            Hover(g, run);
        }
        Assert.Null(Preview(g)); // layer 4 is taken
        int full = Smart(4);
        Click(g, run);
        Assert.Equal(full, Smart(4)); // tg: "There is already a pipe at that location!" (the fitting stays loose)
        Act(g, "toggle_multi_layer");
        Act(g, "pipe_layers", ("pipe_layers", PIPE_LAYER(5)));
        Hover(g, run);
        Assert.Equal(10, Preview(g).Value.PixelX); // layer 5: a south fitting shifted east
        Assert.Same(run, Preview(g).Value.Place);
        Hover(g, On<obj_machinery_atmospherics_pipe_smart>(g.R, 3, Y - 1)[0]); // the pipe itself: its turf
        Assert.Same(run, Preview(g).Value.Place);
        Click(g, run);
        Assert.Equal((full, 1), (Smart(4), Smart(5)));
        Act(g, "toggle_multi_layer");

        // a loose fitting (Connect off): a click with Destroy takes it away, so no preview; without Destroy the click builds on its turf
        Modes(g, 1);
        Click(g, At(g.R, 3, Y + 1));
        obj_item_pipe loose = On<obj_item_pipe>(g.R, 3, Y + 1).Single();
        Modes(g, 1 | 4);
        Hover(g, loose);
        Assert.Null(Preview(g));
        Modes(g, 1);
        Assert.Same(At(g.R, 3, Y + 1), Preview(g).Value.Place);
        Modes(g, 1 | 4);
        Click(g, loose);
        Assert.Empty(On<obj_item_pipe>(g.R, 3, Y + 1)); // the RPD still takes loose fittings

        Modes(g, 2 | 4); // Connect and Destroy without Dispense: nothing to show
        Hover(g, floor);
        Assert.Null(Preview(g));
        g.R.AssertNoRuntimes();
    }

    private static void Hover(Rig g, atom target)
    {
        g.R.Command(() => ClientSeam.Hover(g.Bob.client, target));
        g.R.Step(1);
    }

    private static BuildPreview? Preview(Rig g) => g.R.In(() => g.Rpd.build_preview(g.Bob));

    [Fact]
    public void UsingTheRpdInHandOpensItsWindow()
    {
        var g = Build(17202);
        g.R.Command(() => ClientSeam.Keybinding(g.Bob.client, 4, true)); // activate_inhand (Z): attack_self
        g.R.Command(() => ClientSeam.Keybinding(g.Bob.client, 4, false));
        g.R.Step(Second);
        g.R.In(() =>
        {
            var ui = SStgui.get_open_ui(g.Bob, g.Rpd) as datum_tgui;
            Assert.NotNull(ui);
            Assert.Equal("RapidPipeDispenser", Dm.str(ui.@interface));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AStraightPipeIsLaidOnEachLayerAfterTgsBuildTimeWithItsSounds()
    {
        var g = Build(17203);
        Modes(g, 1); // Dispense only: the fitting stays loose
        (int X, int Y)[] tiles = { (4, Y - 1), (4, Y), (4, Y + 1), (2, Y - 1), (2, Y) }; // around the player at (3, Y)
        for (int layer = 1; layer <= 5; layer++)
        {
            (int tx, int ty) = tiles[layer - 1];
            Assert.True(Dm.truthy(Act(g, "pipe_layers", ("pipe_layers", PIPE_LAYER(layer)))));
            g.R.Sounds.Heard.Clear();
            turf floor = At(g.R, tx, ty);
            Click(g, floor, 0.2f); // tg's atmos_build_speed is 0.4 s: nothing yet
            Assert.Empty(On<obj_item_pipe>(g.R, tx, ty));
            g.R.Step(Second);
            obj_item_pipe fitting = Assert.Single(On<obj_item_pipe>(g.R, tx, ty));
            int l = layer;
            g.R.In(() =>
            {
                Assert.IsType<obj_item_pipe_quaternary>(fitting);
                Assert.Equal(typeof(obj_machinery_atmospherics_pipe_smart), fitting.pipe_type);
                Assert.Equal((float)l, fitting.piping_layer);
                Assert.Equal(COLOR_VIBRANT_LIME, Dm.str(fitting.pipe_color)); // the default "green"
                Assert.Equal(COLOR_VIBRANT_LIME, Dm.str(fitting.color));
                Assert.Equal("pipe fitting", Dm.str(fitting.name)); // the smart pipe is tg's "pipe"
                Assert.Equal(NORTH, Dm.num(fitting.dir));
                Assert.Equal(ALL_CARDINALS, fitting.p_init_dir); // the RPD's smart pipe directions
            });
            Assert.NotEmpty(g.R.Sounds.Of(g.Bob, ToolSwitch));
            Assert.Single(g.R.Sounds.Of(g.Bob, Deconstruct));
        }
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AManifoldIsRefusedOnTheOuterLayersAndAPumpIsWrenchedDownOnEveryLayer()
    {
        var g = Build(17204);
        Modes(g, 1);
        Pick(g, "Heat Exchange", 2); // the heat exchanging manifold: not all_layers
        for (int layer = 1; layer <= 5; layer++)
        {
            Act(g, "pipe_layers", ("pipe_layers", PIPE_LAYER(layer)));
            Click(g, At(g.R, 4, Y), 1);
        }
        List<obj_item_pipe> manifolds = On<obj_item_pipe>(g.R, 4, Y);
        Assert.Equal(new float[] { 2, 3, 4 }, g.R.In(() => manifolds.Select(m => m.piping_layer).OrderBy(x => x).ToArray()));
        g.R.In(() => Assert.All(manifolds, m => Assert.Equal(typeof(obj_machinery_atmospherics_pipe_heat_exchanging_manifold), m.pipe_type)));

        // the layer adapter (all layers) and the pressure pump with Connect on: a device on each layer of one tile
        Modes(g, 1 | 2);
        Pick(g, "Binary", 3, "east");
        for (int layer = 1; layer <= 5; layer++)
        {
            Act(g, "pipe_layers", ("pipe_layers", PIPE_LAYER(layer)));
            Click(g, At(g.R, 4, Y + 1), 1);
        }
        List<obj_machinery_atmospherics_components_binary_pump> pumps = On<obj_machinery_atmospherics_components_binary_pump>(g.R, 4, Y + 1);
        Assert.Equal(new float[] { 1, 2, 3, 4, 5 }, g.R.In(() => pumps.Select(p => p.piping_layer).OrderBy(x => x).ToArray()));
        g.R.In(() => Assert.All(pumps, p =>
        {
            Assert.Equal(EAST, Dm.num(p.dir));
            Assert.Equal(COLOR_VIBRANT_LIME, p.pipe_color);
            Assert.False(Dm.truthy(p.on)); // build_pipe: a built device starts off
        }));
        Assert.Empty(On<obj_item_pipe>(g.R, 4, Y + 1)); // every fitting became its pump
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void EveryAtmosRecipeIsDispensedAndConnectedWithoutRuntimes()
    {
        var g = Build(17213);
        var built = new List<string>();
        int k = 0;
        foreach ((string category, int count) in new[] { ("Pipes", 5), ("Binary", 8), ("Devices", 8), ("Heat Exchange", 5) })
        {
            for (int i = 1; i <= count; i++, k++)
            {
                // a fresh tile for each: the player on (2 + 2 * column, 2 + row), the recipe east of him
                int bx = 2 + (k % 5) * 2, by = 2 + k / 5;
                g.R.In(() => Waiting.NoSleep(g.Bob.forceMove(locate(bx, by, 1))));
                Pick(g, category, i, "east");
                datum_pipe_info recipe = g.Rpd.recipe;
                Click(g, At(g.R, bx + 1, by), 1);
                g.R.In(() =>
                {
                    List<atom> there = Dm.each<atom>(locate(bx + 1, by, 1)).Where(a => a is obj_machinery_atmospherics || a is obj_item_pipe || a is obj_item_pipe_meter).ToList();
                    atom only = Assert.Single(there);
                    if (recipe is datum_pipe_info_meter)
                        Assert.IsType<obj_item_pipe_meter>(only); // no pipe to fasten to: it stays a fitting
                    else
                        Assert.True(recipe.id.IsInstanceOfType(only), Dm.str(recipe.name) + " built " + only.GetType().Name); // the fitting became its device
                    built.Add(Dm.str(recipe.name));
                });
            }
        }
        Assert.Equal(26, built.Count);
        g.R.AssertNoRuntimes(); // the multi-deck adapter, the pressure gate, the temperature gate and the temperature pump are fastened too
    }

    [Fact]
    public void MultiLayerLaysOnEveryChosenLayerAndSkipsTheOnesTheRecipeCannotUse()
    {
        var g = Build(17205);
        Modes(g, 1);
        Act(g, "toggle_multi_layer");
        Assert.Equal(1f, g.Rpd.multi_layer);
        Act(g, "pipe_layers", ("pipe_layers", PIPE_LAYER(1))); // 4 ^ 1
        Act(g, "pipe_layers", ("pipe_layers", PIPE_LAYER(5))); // ^ 16
        Assert.Equal(21f, g.Rpd.pipe_layers);
        Assert.Equal(new float[] { 1, 3, 5 }, g.R.In(() => Dm.each(g.Rpd.get_active_pipe_layers()).Select(Dm.num).ToArray()));
        Click(g, At(g.R, 4, Y), 2); // three do_afters of 0.4 s
        Assert.Equal(new float[] { 1, 3, 5 }, g.R.In(() => On<obj_item_pipe>(g.R, 4, Y).Select(p => p.piping_layer).OrderBy(x => x).ToArray()));

        Pick(g, "Heat Exchange", 1); // the heat exchanging pipe: layers 1 and 5 are refused, multi layer goes on
        Click(g, At(g.R, 4, Y + 1), 2);
        Assert.Equal(new float[] { 3 }, g.R.In(() => On<obj_item_pipe>(g.R, 4, Y + 1).Select(p => p.piping_layer).ToArray()));

        // off: the highest active layer is kept (RPD.dm:255)
        Act(g, "toggle_multi_layer");
        Assert.Equal((0f, 16f), (g.Rpd.multi_layer, g.Rpd.pipe_layers));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePaintColourIsTheFittingsAndTheBuiltPipes()
    {
        var g = Build(17206);
        Act(g, "color", ("paint_color", "red"));
        Assert.Equal("red", g.Rpd.paint_color);
        Click(g, At(g.R, 4, Y), 1); // all modes: dispensed and connected
        obj_machinery_atmospherics_pipe_smart pipe = Assert.Single(On<obj_machinery_atmospherics_pipe_smart>(g.R, 4, Y));
        g.R.In(() =>
        {
            Assert.Equal(COLOR_RED, pipe.pipe_color);
            Assert.Equal(3f, pipe.piping_layer);
            Assert.Equal(COLOR_RED, Dm.str(pipe.color));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void APumpLaidWithConnectJoinsTwoPipelinesAndMovesGasBetweenThem()
    {
        var g = Build(17207, x: 6, y: Y - 1);
        List<obj_machinery_atmospherics_pipe_smart> west = Pipes(g.R, 3, 5, Y);
        List<obj_machinery_atmospherics_pipe_smart> east = Pipes(g.R, 7, 9, Y);
        Assert.NotSame(g.R.In(() => west[0].parent), g.R.In(() => east[0].parent));
        Pick(g, "Binary", 3, "east");
        g.R.Sounds.Heard.Clear();
        Click(g, At(g.R, 6, Y), 1);
        var pump = Assert.Single(On<obj_machinery_atmospherics_components_binary_pump>(g.R, 6, Y));
        g.R.Step(Second);
        datum_pipeline input = null, output = null;
        g.R.In(() =>
        {
            Assert.Same(west[2], pump.nodes[1]);
            Assert.Same(east[0], pump.nodes[2]);
            input = (datum_pipeline)pump.parents[1];
            output = (datum_pipeline)pump.parents[2];
            Assert.Same(west[0].parent, input);
            Assert.Same(east[2].parent, output);
            Assert.Contains(west[2], Dm.each(input.members));
        });
        // tg's sounds: the switch, the dispense and the fitting's wrench
        Assert.NotEmpty(g.R.Sounds.Of(g.Bob, ToolSwitch));
        Assert.NotEmpty(g.R.Sounds.Of(g.Bob, Deconstruct));

        g.R.In(() =>
        {
            input.air.merge(SSair.parse_gas_string("o2=21;n2=79;TEMP=293.15") as datum_gas_mixture);
            input.update = TRUE;
            pump.on = TRUE;
            pump.target_pressure = 500;
            pump.update_appearance();
        });
        g.R.Step(6 * Second);
        g.R.In(() =>
        {
            Assert.True(Dm.num(output.air.total_moles()) > 0, "the pump moved gas into the east pipeline");
            Assert.True(Dm.num(output.air.return_pressure()) > 0);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void DestroyRemovesALooseFittingAndAMeter()
    {
        var g = Build(17208);
        Modes(g, 1);
        Click(g, At(g.R, 4, Y), 1);
        obj_item_pipe fitting = Assert.Single(On<obj_item_pipe>(g.R, 4, Y));
        Pick(g, "Devices", 8); // the meter
        Click(g, At(g.R, 4, Y + 1), 1);
        obj_item_pipe_meter meter = Assert.Single(On<obj_item_pipe_meter>(g.R, 4, Y + 1));
        g.R.In(() => Assert.Equal(3f, meter.piping_layer));

        Modes(g, 4); // Destroy only
        g.R.Sounds.Heard.Clear();
        Click(g, fitting, 0.5f);
        g.R.In(() => Assert.True(QDELETED(fitting)));
        Assert.Single(g.R.Sounds.Of(g.Bob, Deconstruct)); // at once, no do_after
        Click(g, meter, 0.5f);
        g.R.In(() => Assert.True(QDELETED(meter)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ReprogramChangesAPlacedSmartPipesFreeDirectionsAndConnectsTheNewOnes()
    {
        var g = Build(17209, x: 5, y: Y - 1);
        // a smart pipe at (5, Y) joined to one at (5, Y + 1): its only connection is north
        var a = g.R.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(5, Y, 1), TRUE, NORTH));
        var b = g.R.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(5, Y + 1, 1), TRUE, NORTH));
        g.R.In(() => SSair.setup_template_machinery(Dm.list(a, b)));
        g.R.Step(Second);
        g.R.In(() => Assert.Equal(NORTH, Dm.num(a.connections)));

        Modes(g, 8); // Reprogram only (with Dispense on, a click on a pipe builds on its turf)
        Assert.True(Dm.truthy(Act(g, "init_dir_setting", ("dir_flag", "west"))));
        Assert.Equal(7f /* NORTH | SOUTH | EAST */, g.Rpd.p_init_dir);
        Click(g, a, 0.5f);
        g.R.In(() =>
        {
            Assert.Equal(7f, Dm.num(a.get_init_directions())); // west closed; the north connection stays
            Assert.Contains(b, Dm.each(a.nodes));
        });
        Click(g, a, 0.5f); // already configured: nothing changes
        g.R.In(() => Assert.Equal(7f, Dm.num(a.get_init_directions())));

        // a lone smart pipe at (6, Y - 2) closed to the west, then a pipe west of it
        var d = g.R.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(6, Y - 2, 1), TRUE, NORTH));
        g.R.In(() => SSair.setup_template_machinery(Dm.list(d)));
        g.R.Step(Second);
        Click(g, d, 0.5f); // p_init_dir is still north, south and east
        g.R.In(() => Assert.Equal(7f, Dm.num(d.get_init_directions())));
        var c = g.R.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(5, Y - 2, 1), TRUE, EAST));
        g.R.In(() => SSair.setup_template_machinery(Dm.list(c)));
        g.R.Step(Second);
        g.R.In(() => Assert.DoesNotContain(c, Dm.each(d.nodes))); // west is not allowed
        // the RPD gives the direction back: the pipe connects at once (RPD.dm:382-400) and the pipenets join
        Assert.True(Dm.truthy(Act(g, "init_reset")));
        Click(g, d, 0.5f);
        g.R.Step(Second);
        g.R.In(() =>
        {
            Assert.Equal(ALL_CARDINALS, Dm.num(d.get_init_directions()));
            Assert.Contains(c, Dm.each(d.nodes));
            Assert.Contains(d, Dm.each(c.nodes));
            Assert.Equal(WEST, Dm.num(d.connections));
            Assert.Same(d.parent, c.parent);
        });

        // a one-direction pipe is refused by the window
        Act(g, "init_dir_setting", ("dir_flag", "north"));
        Act(g, "init_dir_setting", ("dir_flag", "south"));
        Assert.False(Dm.truthy(Act(g, "init_dir_setting", ("dir_flag", "east"))));
        Assert.Equal(12f /* EAST | WEST */, g.Rpd.p_init_dir);

        // a loose smart fitting takes the directions as they are
        var loose = g.R.In(() => Dm.@new<obj_item_pipe_quaternary>(locate(6, Y, 1), typeof(obj_machinery_atmospherics_pipe_smart), NORTH, null, ATMOS_COLOR_OMNI, ALL_CARDINALS));
        Click(g, loose, 0.5f);
        g.R.In(() => Assert.Equal(12f, loose.p_init_dir));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ARightClickCopiesAPipesColourAndLayer()
    {
        var g = Build(17210);
        var pipe = g.R.In(() =>
        {
            var p = Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(4, Y, 1), TRUE, EAST);
            Waiting.NoSleep(p.on_construction(null, COLOR_BLUE, 2));
            return p;
        });
        g.R.Step(Second);
        g.R.In(() =>
        {
            DmList context = Dm.list();
            Assert.Equal(CONTEXTUAL_SCREENTIP_SET, Dm.num(g.Rpd.add_item_context(g.Rpd, context, pipe, g.Bob)));
            Assert.Equal("Copy piping color and layer", Dm.str(context[SCREENTIP_CONTEXT_RMB]));
        });
        Click(g, pipe, 0.5f, Right);
        Assert.Equal(("blue", 2f), (g.Rpd.paint_color, g.Rpd.pipe_layers));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheDisposalCategoryLaysASegmentAndTheTransitCategoryLaysATube()
    {
        var g = Build(17211);
        Assert.True(Dm.truthy(Act(g, "category", ("category", 1))));
        g.R.In(() =>
        {
            Assert.Same(obj_item_pipe_dispenser.first_disposal, g.Rpd.recipe);
            Assert.Equal("Pipe", Dm.str(g.Rpd.recipe.name));
            Assert.Equal("conpipe", Dm.str(g.Rpd.recipe.icon_state)); // "con" + the segment's icon_state
            var data = (DmList)Waiting.NoSleep(g.Rpd.ui_data(g.Bob));
            var cat = (DmList)((DmList)data["categories"])[1];
            Assert.Equal("Disposal Pipes", Dm.str(cat["cat_name"]));
            Assert.Equal(11f, ((DmList)cat["recipes"]).len);
        });
        g.R.Sounds.Heard.Clear();
        Click(g, At(g.R, 4, Y), 1);
        g.R.AssertNoRuntimes();
        Assert.NotEmpty(g.R.Sounds.Of(g.Bob, Deconstruct));
        obj_structure_disposalconstruct segment = Assert.Single(On<obj_structure_disposalconstruct>(g.R, 4, Y));
        g.R.In(() =>
        {
            Assert.Equal(typeof(obj_structure_disposalpipe_segment), segment.pipe_type);
            // the Connect mode's wrench_act fastens it: the room's indestructible turf is no floor tile to pry up (construction.dm:116)
            Assert.Equal(1f, Dm.num(segment.anchored));
        });

        Assert.True(Dm.truthy(Act(g, "category", ("category", 2))));
        g.R.In(() =>
        {
            Assert.Same(obj_item_pipe_dispenser.first_transit, g.Rpd.recipe);
            Assert.Equal("Straight Tube", Dm.str(g.Rpd.recipe.name));
            var data = (DmList)Waiting.NoSleep(g.Rpd.ui_data(g.Bob));
            Assert.Equal(new[] { "Transit Tubes", "Station Equipment" }, Dm.each((DmList)data["categories"]).Select(c => Dm.str(((DmList)c)["cat_name"])));
        });
        g.R.Sounds.Heard.Clear();
        Click(g, At(g.R, 4, Y + 1), 1);
        g.R.AssertNoRuntimes(); // transit tubes are translated (D276): the unattached straight tube is laid
        Assert.NotEmpty(g.R.Sounds.Of(g.Bob, Deconstruct));
        Assert.Single(On<obj_structure_c_transit_tube>(g.R, 4, Y + 1));

        // back to atmospherics: the first atmos recipe again
        Act(g, "category", ("category", 0));
        Assert.Same(g.R.In(() => obj_item_pipe_dispenser.first_atmos), g.Rpd.recipe);
    }

    [Fact]
    public void TheEngineersLockersHoldAnRpd()
    {
        var g = Build(17212);
        g.R.In(() =>
        {
            var atmos = Dm.@new<obj_structure_closet_secure_closet_atmospherics>(locate(8, 8, 1));
            var ce = Dm.@new<obj_structure_closet_secure_closet_engineering_chief>(locate(9, 8, 1));
            Assert.Single(Dm.each<obj_item_pipe_dispenser>(atmos.contents));
            Assert.Single(Dm.each<obj_item_pipe_dispenser>(ce.contents));
        });
        g.R.AssertNoRuntimes();
    }
}
