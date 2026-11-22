using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Engineering package E14: the client's Blueprints and DecalPainter windows (tg's Blueprints.tsx and DecalPainter.tsx) without the
/// engine. The blueprints show the main menu, the wire legend's list of wire sets or one set's colours by tg's legend value, and send tg's
/// acts; the structural view's button turns red with a refresh beside it while viewing. The decal painter shows the categories, the
/// colours of the active one (none for a single colour, the custom colour's box), the decals with one button per dir or one for a
/// nondirectional decal, each a class of tg's "paintable_decals" sheet, and the sheet's classes draw tg's previews from the .dmi files.
/// </summary>
public class BlueprintsDecalPainterWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, iface, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Statics = "\"legend_viewing_list\":\"watching_list\",\"legend_off\":\"off\",\"fluff_notice\":\"Property of Nanotrasen.\","
        + "\"station_name\":\"Space Station 13\",\"wire_devices\":[{\"name\":\"Air Alarm\",\"ref\":\"/obj/machinery/airalarm\"},"
        + "{\"name\":\"Generic Airlock\",\"ref\":\"/obj/machinery/door/airlock\"}]";

    private static string Blueprint(string legend, int viewing, string extra = "") => "{" + Statics + ",\"area_notice\":\"You are now in Engineering\","
        + "\"area_name\":\"Engineering\",\"area_allows_shuttle_docking\":0,\"legend\":\"" + legend + "\",\"viewing\":" + viewing + ",\"wire_data\":[]" + extra + "}";

    [Fact]
    public void TheBlueprintsMainMenuSendsTgsActs()
    {
        var (context, tree, acts) = Render("Blueprints", Blueprints.Render, Blueprint("off", 0));
        Assert.Equal((450.0, 340.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Space Station 13 blueprints", tree.Walk().Single(n => n.Kind == UiKind.Section).Title);
        Assert.Equal("Property of Nanotrasen.", ByKey(tree, "fluff").Text);
        Assert.Equal("You are now in Engineering", ByKey(tree, "notice").Text);
        Assert.Equal(new[] { "Create or modify an existing area", "Change area name", "Allow shuttle docking", "View wire color legend", "View structural data" },
            tree.Walk().Where(n => n.Kind == UiKind.Button).Select(n => n.Text));
        Assert.Equal("toggle-off", ByKey(tree, "docking").Icon);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "refresh");
        Assert.Null(ByKey(tree, "structure").Color);
        foreach (string key in new[] { "create", "rename", "docking", "legend", "structure" })
            ByKey(tree, key).OnClick();
        Assert.Equal(new[] { "create_area", "edit_area", "toggle_allow_shuttle_docking", "view_legend", "view_blueprints" }, acts.Select(a => a.Action));

        // while viewing: the red hide button and the refresh beside it
        (_, tree, acts) = Render("Blueprints", Blueprints.Render, Blueprint("off", 1).Replace("\"area_allows_shuttle_docking\":0", "\"area_allows_shuttle_docking\":1", StringComparison.Ordinal));
        UiNode structure = ByKey(tree, "structure");
        Assert.Equal(("Hide structural data", "red"), (structure.Text, structure.Color));
        Assert.Equal("toggle-on", ByKey(tree, "docking").Icon);
        structure.OnClick();
        ByKey(tree, "refresh").OnClick();
        Assert.Equal(new[] { "hide_blueprints", "refresh" }, acts.Select(a => a.Action));
    }

    [Fact]
    public void TheWireLegendListsTheSetsThenOneSetsColours()
    {
        var (_, tree, acts) = Render("Blueprints", Blueprints.Render, Blueprint("watching_list", 0));
        Assert.Equal(new[] { "Back", "Air Alarm", "Generic Airlock" }, tree.Walk().Where(n => n.Kind == UiKind.Button).Select(n => n.Text));
        ByKey(tree, "wireset:/obj/machinery/airalarm").OnClick();
        ByKey(tree, "back").OnClick();
        Assert.Equal(new (string, string)[] { ("view_wireset", "{\"view_wireset\":\"/obj/machinery/airalarm\"}"), ("exit_legend", null) }, acts);

        (_, tree, acts) = Render("Blueprints", Blueprints.Render, Blueprint("/obj/machinery/airalarm", 0)
            .Replace("\"wire_data\":[]", "\"wire_data\":[{\"color\":\"crimson\",\"message\":\"Power\"},{\"color\":\"lime\",\"message\":\"AI Control\"}],\"wires_name\":\"Air Alarm\"", StringComparison.Ordinal));
        Assert.Equal("Air Alarm wires:", ByKey(tree, "wires_name").Text);
        UiNode power = ByKey(tree, "wire:Power");
        Assert.Equal(("crimson", "crimson", ": Power"), (power.Children[0].Color, power.Children[0].Text, power.Children[1].Text));
        ByKey(tree, "back").OnClick();
        Assert.Equal(new (string, string)[] { ("view_legend", null) }, acts);
    }

    private const string Categories = "\"icon_prefix\":\"paintable_decals32x32\",\"categories\":["
        + "{\"category\":\"Tiles\",\"decal_list\":[{\"name\":\"4 Corners\",\"icon_state\":\"tile_fourcorners\",\"directional\":0},"
        + "{\"name\":\"Corner\",\"icon_state\":\"tile_corner\",\"directional\":1}],"
        + "\"color_list\":[{\"name\":\"Neutral\",\"color\":\"#d4d4d432\"},{\"name\":\"White\",\"color\":\"#FFFFFF\"},{\"name\":\"Custom\",\"color\":\"custom\"}],"
        + "\"dir_list\":[{\"name\":\"North\",\"dir\":1},{\"name\":\"South\",\"dir\":2},{\"name\":\"East\",\"dir\":4},{\"name\":\"West\",\"dir\":8}]},"
        + "{\"category\":\"Normal Sidings\",\"decal_list\":[{\"name\":\"Wood\",\"icon_state\":\"siding_wood\",\"directional\":1}],"
        + "\"color_list\":[{\"name\":\"Default\",\"color\":\"#5d341f\"}],"
        + "\"dir_list\":[{\"name\":\"North\",\"dir\":1},{\"name\":\"South\",\"dir\":2},{\"name\":\"East\",\"dir\":4},{\"name\":\"West\",\"dir\":8}]}]";

    private static string Painter(string category, string color, string decal, int dir, string custom = "null") => "{" + Categories
        + ",\"current_decal\":\"" + decal + "\",\"current_color\":\"" + color + "\",\"current_dir\":" + dir
        + ",\"current_custom_color\":" + custom + ",\"active_category\":\"" + category + "\"}";

    [Fact]
    public void TheDecalPainterShowsTheActiveCategorysColoursAndDecalsAsSheetClasses()
    {
        var (context, tree, acts) = Render("DecalPainter", DecalPainter.Render, Painter("Tiles", "#d4d4d432", "tile_corner", 4));
        Assert.Equal((650.0, 455.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Category", "Color", "Style" }, tree.Walk().Where(n => n.Kind == UiKind.Section).Select(n => n.Title));
        Assert.True(ByKey(tree, "category:Tiles").Selected);
        Assert.False(ByKey(tree, "category:Normal Sidings").Selected);

        // the colours: the box drops the alpha; the neutral one is chosen
        UiNode neutral = ByKey(tree, "color:Neutral");
        Assert.True(neutral.Selected);
        Assert.Equal("#d4d4d4", neutral.Walk().Single(n => n.Kind == UiKind.ColorBox).Color);
        Assert.False(ByKey(tree, "color:Custom").Selected);

        // a nondirectional decal: one button at SOUTH; a directional one: a column of four; each a class of the sheet
        UiNode[] decals = tree.Walk().Where(n => n.Kind == UiKind.ImageButton).ToArray();
        Assert.Equal(new[] { "tile_fourcorners_2_d4d4d432", "tile_corner_1_d4d4d432", "tile_corner_2_d4d4d432", "tile_corner_4_d4d4d432", "tile_corner_8_d4d4d432" },
            decals.Select(d => d.DmIconState));
        Assert.All(decals, d => Assert.Equal(TguiSpritesheets.Decals32, d.DmIcon));
        Assert.Equal(new[] { "4 Corners", "North Corner", "South Corner", "East Corner", "West Corner" }, decals.Select(d => d.Tooltip));
        Assert.Equal(new[] { false, false, false, true, false }, decals.Select(d => d.Selected));

        ByKey(tree, "category:Normal Sidings").OnClick();
        ByKey(tree, "color:White").OnClick();
        ByKey(tree, "color:Custom").OnClick();
        decals[1].OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("select_category", "{\"category\":\"Normal Sidings\"}"),
            ("select_color", "{\"color\":\"#FFFFFF\"}"),
            ("pick_custom_color", null),
            ("select_decal", "{\"decal\":\"tile_corner\",\"dir\":1}"),
        }, acts);

        // a custom colour (one not in the list): the custom button is chosen and the previews are the rainbow class
        (_, tree, _) = Render("DecalPainter", DecalPainter.Render, Painter("Tiles", "#123456", "tile_fourcorners", 2, "\"#123456\""));
        Assert.True(ByKey(tree, "color:Custom").Selected);
        Assert.Equal("#123456", ByKey(tree, "color:Custom").Walk().Single(n => n.Kind == UiKind.ColorBox).Color);
        Assert.Equal("tile_fourcorners_2_custom", tree.Walk().First(n => n.Kind == UiKind.ImageButton).DmIconState);

        // one colour: no Color section
        (_, tree, _) = Render("DecalPainter", DecalPainter.Render, Painter("Normal Sidings", "#5d341f", "siding_wood", 2));
        Assert.Equal(new[] { "Category", "Style" }, tree.Walk().Where(n => n.Kind == UiKind.Section).Select(n => n.Title));
    }

    [Fact]
    public void TheSheetsClassesDrawTgsPreviewsFromTheDmiFiles()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // the converted icons are not here
        var library = new IconLibrary(new DirectoryIconFiles(root));
        Assert.True(library.TryGetIcon("icons/turf/floors.dmi", out IIcon floors));
        Assert.True(floors.TryGetState("floor", out IIconState floorState));
        Raster floor = floorState.Get(0, 0);

        // a white tile decal at the tile category's alpha (110) over the floor: some pixels change, none is the decal's pure white
        Assert.True(TguiSpritesheets.TryDrawDecal(library, "tile_full_2_FFFFFF", out Raster tile));
        Assert.Equal((32, 32), (tile.Width, tile.Height));
        Assert.NotEqual(floor.Pixels, tile.Pixels);
        Assert.True(TguiSpritesheets.TryDrawDecal(library, "tile_full_2_d4d4d432", out Raster neutral));
        Assert.NotEqual(tile.Pixels, neutral.Pixels); // a fainter, greyer tint

        // the warning stripes draw their coloured state as it is; the yellow one is the plain state
        Assert.True(TguiSpritesheets.TryDrawDecal(library, "warningline_4_red", out Raster red));
        Assert.True(TguiSpritesheets.TryDrawDecal(library, "warningline_4_yellow", out Raster yellow));
        Assert.NotEqual(red.Pixels, yellow.Pixels);
        // an 8-dir state and the rainbow of a custom colour
        Assert.True(TguiSpritesheets.TryDrawDecal(library, "trimline_fill__8_1_FFFFFF", out _));
        Assert.True(TguiSpritesheets.TryDrawDecal(library, "tile_corner_4_custom", out _));
        Assert.False(TguiSpritesheets.TryDrawDecal(library, "no_such_decal_2_FFFFFF", out _));
        Assert.False(TguiSpritesheets.TryDrawDecal(library, "tile_full", out _));
    }

    [Fact]
    public void BothWindowsAreRegistered()
    {
        Assert.NotNull(TguiInterfaces.Find("Blueprints"));
        Assert.NotNull(TguiInterfaces.Find("DecalPainter"));
    }
}
