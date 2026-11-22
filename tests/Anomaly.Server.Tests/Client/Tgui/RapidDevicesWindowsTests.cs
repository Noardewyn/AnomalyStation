using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Engineering package E13: the client's RapidConstructionDevice, RapidTilingDevice and RapidDecorationDevice windows (tg's TSX) without the
/// engine. They read tg's ui_data the way the TSX does (the units left, the root categories, the design tabs with their sprite sheet
/// classes, the airlock electronics' section under Airlock Access, the tile preview and its directions, the decoration cards); their buttons
/// send tg's acts; the "rcd-tgui", "rtd" and "rdd" sheet classes resolve to tg's .dmi files and states as the asset datums insert them.
/// </summary>
public class RapidDevicesWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, UiValue data, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "device", 2, data, (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    // the RCD's ui_data and ui_static_data merged (RHD.dm:216-233, RCD.dm:345-392), cut to two categories, with the electronics' data
    private const string Rcd = "{\"matterLeft\":160,\"silo_upgraded\":0,\"silo_enabled\":0,"
        + "\"root_categories\":[\"Construction\",\"Airlocks\",\"Airlock Access\"],\"selected_root\":\"Construction\","
        + "\"categories\":[{\"cat_name\":\"Structures\",\"designs\":[{\"title\":\"Floor/Wall\",\"icon\":\"FloorWall\"},{\"title\":\"full tile window\",\"icon\":\"fulltilewindow\"}]},"
        + "{\"cat_name\":\"Furniture\",\"designs\":[{\"title\":\"chair\",\"icon\":\"chair\"}]}],"
        + "\"selected_category\":\"Structures\",\"selected_design\":\"Floor/Wall\","
        + "\"regions\":[],\"accesses\":[],\"oneAccess\":0,\"unres_direction\":0,\"passedName\":\"\",\"passedCycleId\":\"\",\"shell\":0}";

    [Fact]
    public void TheRcdWindowShowsTgsUnitsCategoriesAndDesignsAndSendsItsActs()
    {
        var (context, tree, acts) = Render("RapidConstructionDevice", RapidConstructionDevice.Render, UiValue.FromJson(Rcd));
        Assert.Equal((450.0, 590.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Label == "Units Left" && n.Text == " 160 Units");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "toggle_silo"); // no silo upgrade
        Assert.True(ByKey(tree, "root-Construction").Selected);
        Assert.True(ByKey(tree, "tab-Structures").Selected);
        // the designs: tg's sheet class, capitalized titles, the wide windows at 0.7
        Assert.True(ByKey(tree, "design-Floor/Wall").Selected);
        Assert.Equal((TguiSpritesheets.Rcd32, "FloorWall", 1.0), (ByKey(tree, "icon-Floor/Wall").DmIcon, ByKey(tree, "icon-Floor/Wall").DmIconState, ByKey(tree, "icon-Floor/Wall").Size));
        Assert.Equal(0.7, ByKey(tree, "icon-full tile window").Size);
        Assert.Contains(tree.Walk(), n => n.Text == "Full Tile Window");

        ByKey(tree, "design-full tile window").OnClick();
        ByKey(tree, "root-Airlocks").OnClick();
        Assert.Equal(new (string, string)[] { ("design", "{\"category\":\"Structures\",\"index\":2}"), ("root_category", "{\"root_category\":\"Airlocks\"}") },
            acts.Select(a => (a.Action, a.Params)));

        // the Airlock Access root shows the electronics' section; a silo upgrade shows its toggle
        var (_, access, _) = Render("RapidConstructionDevice", RapidConstructionDevice.Render,
            UiValue.FromJson(Rcd.Replace("\"selected_root\":\"Construction\"", "\"selected_root\":\"Airlock Access\"").Replace("\"silo_upgraded\":0", "\"silo_upgraded\":1")));
        Assert.Contains(access.Walk(), n => n.Key == "one_access");
        Assert.DoesNotContain(access.Walk(), n => n.Key == "tab-Structures");
        Assert.Equal("Silo Offline", ByKey(access, "toggle_silo").Text);
    }

    // the RTD's data (RTD.dm:156-186)
    private const string Rtd = "{\"matterLeft\":350,\"silo_upgraded\":0,\"silo_enabled\":0,\"root_categories\":[\"Conventional\",\"Decorated\",\"Interior\"],\"selected_root\":\"Conventional\","
        + "\"categories\":[{\"category_name\":\"Standard\",\"recipes\":[{\"name\":\"Base\",\"icon\":\"iconsturffloorsdmifloor\"},{\"name\":\"Edge\",\"icon\":\"iconsturffloorsdmifloor_edge\"}]}],"
        + "\"selected_category\":\"Standard\",\"selected_recipe\":\"Edge\",\"selected_icon\":\"iconsturffloorsdmifloor_edge\",\"tile_dirs\":[\"south\",\"north\",\"east\",\"west\"],\"selected_direction\":\"east\"}";

    [Fact]
    public void TheRtdWindowShowsTheTilePreviewItsDirectionsAndTheRecipes()
    {
        var (context, tree, acts) = Render("RapidTilingDevice", RapidTilingDevice.Render, UiValue.FromJson(Rtd));
        Assert.Equal((500.0, 540.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal((TguiSpritesheets.Rtd32, "iconsturffloorsdmifloor_edgeeast"), (ByKey(tree, "preview-icon").DmIcon, ByKey(tree, "preview-icon").DmIconState));
        Assert.Equal("green", ByKey(tree, "preview").Background);
        Assert.True(ByKey(tree, "dir-east").Checkbox);
        Assert.False(ByKey(tree, "dir-south").Checkbox);
        Assert.True(ByKey(tree, "recipe-Edge").Selected);
        Assert.Equal("iconsturffloorsdmifloorsouth", ByKey(tree, "icon-Base").DmIconState);

        ByKey(tree, "dir-north").OnClick();
        ByKey(tree, "recipe-Base").OnClick();
        Assert.Equal(new (string, string)[] { ("set_dir", "{\"dir\":\"north\"}"), ("recipe", "{\"category_name\":\"Standard\",\"id\":1}") },
            acts.Select(a => (a.Action, a.Params)));

        // a design that does not rotate: no direction list, the south picture
        var (_, still, _) = Render("RapidTilingDevice", RapidTilingDevice.Render,
            UiValue.FromJson(Rtd.Replace(",\"tile_dirs\":[\"south\",\"north\",\"east\",\"west\"],\"selected_direction\":\"east\"", ",\"selected_direction\":null")));
        Assert.DoesNotContain(still.Walk(), n => n.Key == "dir-south");
        Assert.Equal("iconsturffloorsdmifloor_edgesouth", ByKey(still, "preview-icon").DmIconState);
    }

    // the RDD's data (RDD.dm:224-248)
    private const string Rdd = "{\"matterLeft\":60,\"silo_upgraded\":0,\"silo_enabled\":0,\"matter\":57,\"max_matter\":60,\"selected_category\":\"Rocks\",\"selected_design\":\"Plastic Rock\","
        + "\"categories\":[{\"cat_name\":\"Grasses\",\"designs\":[{\"name\":\"Plastic Grass Patch\",\"icon\":\"PlasticGrassPatch\"}]},{\"cat_name\":\"Rocks\",\"designs\":[{\"name\":\"Plastic Rock\",\"icon\":\"PlasticRock\"}]}]}";

    [Fact]
    public void TheRddWindowShowsTheUnitsTheSelectionAndTheDecorationCards()
    {
        var (context, tree, acts) = Render("RapidDecorationDevice", RapidDecorationDevice.Render, UiValue.FromJson(Rdd));
        Assert.Equal((470.0, 580.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Label == "Units Left" && n.Text == "57/60 Units");
        Assert.Contains(tree.Walk(), n => n.Label == "Selected" && n.Text == "Plastic Rock");
        UiNode rock = ByKey(tree, "design-Plastic Rock");
        Assert.Equal((UiKind.ImageButton, TguiSpritesheets.Rdd32, "PlasticRock", true), (rock.Kind, rock.DmIcon, rock.DmIconState, rock.Selected));
        Assert.False(ByKey(tree, "design-Plastic Grass Patch").Selected);
        ByKey(tree, "design-Plastic Grass Patch").OnClick();
        Assert.Equal(("design", "{\"category\":\"Grasses\",\"name\":\"Plastic Grass Patch\"}"), Assert.Single(acts));
    }

    [Fact]
    public void TheSheetClassesResolveToTgsIconsAsTheAssetDatumsInsertThem()
    {
        // the RCD: a design's own icon, the radial pictures of the directional windows, a grille under the full tile ones, a fill over a solid airlock
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Rcd32, "FloorWall", out SpritesheetIcon floor));
        Assert.Equal(("icons/hud/radial.dmi", "wallfloor"), (floor.Files[0], floor.State));
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Rcd32, "window", out SpritesheetIcon window));
        Assert.Equal(("icons/hud/radial.dmi", "windowsize"), (window.Files[0], window.State));
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Rcd32, "fulltilewindow", out SpritesheetIcon full));
        Assert.Equal(new SpritesheetLayer("icons/obj/structures.dmi", "grille"), full.Underlay);
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Rcd32, "Airlock", out SpritesheetIcon airlock));
        Assert.Equal(("icons/obj/doors/airlocks/station/public.dmi", "closed"), (airlock.Files[0], airlock.State));
        Assert.Equal(new SpritesheetLayer("icons/obj/doors/airlocks/station/public.dmi", "fill_closed"), airlock.Overlay);
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Rcd32, "glassairlock", out SpritesheetIcon glass));
        Assert.Null(glass.Overlay);
        // the RTD: the turf's icon and state in each direction
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Rtd32, "iconsturffloorsdmifloorsouth", out SpritesheetIcon tile));
        Assert.Equal(("icons/turf/floors.dmi", "floor", (byte)2), (tile.Files[0], tile.State, tile.Dir));
        // the RDD: the decoration's flora picture
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Rdd32, "PlasticGrassPatch", out SpritesheetIcon grass));
        Assert.Equal("snowgrass1gb", grass.State);
        Assert.False(TguiSpritesheets.TryResolve(TguiSpritesheets.Rcd32, "nothing", out _));
    }
}
