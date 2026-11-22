using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// M7 slice D172: the client's RapidPipeDispenser window (tg's RapidPipeDispenser.tsx) without the engine. It reads tg's ui_data the way the
/// TSX does: the three root categories, the four mode checkboxes, the paint colours with the selected one's name, the five layers and
/// Multi, the smart pipe direction panel (atmospherics only), the recipe tabs (the selected recipe's at first, then the player's), and
/// each recipe's previews as tg's "pipes32x32" sprite sheet classes, one button per recipe (D320, DV352: the chosen direction's, else the
/// first); its buttons send tg's acts; the sheet classes resolve to tg's .dmi files, the state and the dir.
/// </summary>
public class RapidPipeDispenserTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(UiValue data, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "RapidPipeDispenser", "rapid pipe dispenser", 2, data, (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        UiNode tree = RapidPipeDispenser.Render(context);
        return (context, tree, acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    // the RPD's ui_data and ui_static_data merged, cut to two categories (RPD.dm:156-205)
    private const string Atmos = "{\"category\":0,\"multi_layer\":0,\"pipe_layers\":4,\"selected_recipe\":\"Pipe\",\"selected_color\":\"green\",\"mode\":15,"
        + "\"categories\":["
        + "{\"cat_name\":\"Pipes\",\"recipes\":[{\"pipe_name\":\"Pipe\",\"pipe_desc\":null,\"pipe_index\":1,\"previews\":[{\"selected\":1,\"dir\":\"south\",\"dir_name\":\"Pipe\",\"icon_state\":\"manifold4w\",\"flipped\":0}]}]},"
        + "{\"cat_name\":\"Binary\",\"recipes\":[{\"pipe_name\":\"Pressure Pump\",\"pipe_desc\":\"Moves gas from one pipeline to another.<br>Transfers a variable amount of gas.\",\"pipe_index\":3,\"previews\":["
        + "{\"selected\":0,\"dir\":\"north\",\"dir_name\":\"North\",\"icon_state\":\"pump\",\"flipped\":0},{\"selected\":0,\"dir\":\"east\",\"dir_name\":\"East\",\"icon_state\":\"pump\",\"flipped\":0}]}]}],"
        + "\"selected_category\":\"Pipes\",\"init_directions\":{\"north\":1,\"south\":1,\"east\":1,\"west\":0},"
        + "\"paint_colors\":{\"omni\":\"#EEEEEE\",\"green\":\"#00FF00\",\"red\":\"#FF0000\"},\"max_pipe_layers\":5}";

    [Fact]
    public void TheWindowShowsTgsCategoriesModesColoursLayersAndDirections()
    {
        var (context, tree, acts) = Render(UiValue.FromJson(Atmos));
        Assert.Equal((550.0, 580.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("rapid pipe dispenser", context.WindowTitle);
        // the root categories, atmospherics selected
        Assert.True(ByKey(tree, "category-0").Selected);
        Assert.Equal(("Atmospherics", "wrench", "transparent"), (ByKey(tree, "category-0").Text, ByKey(tree, "category-0").Icon, ByKey(tree, "category-0").Color));
        Assert.Equal("bus", ByKey(tree, "category-2").Icon);
        // the modes: every bit of 15 on
        Assert.Equal(new[] { "Dispense", "Connect", "Destroy", "Reprogram" }, new[] { 1, 2, 4, 8 }.Select(b => ByKey(tree, "mode-" + b).Text));
        Assert.All(new[] { 1, 2, 4, 8 }, b => Assert.True(ByKey(tree, "mode-" + b).Checkbox));
        // the colours: tg's boxes, the selected one bordered, its name capitalized in its colour
        Assert.Equal(UiKind.ColorBox, ByKey(tree, "color-red").Kind);
        Assert.Equal("#FF0000", ByKey(tree, "color-red").Color);
        Assert.True(ByKey(tree, "color-green").Selected);
        Assert.False(ByKey(tree, "color-red").Selected);
        Assert.Equal(("Green", "#00FF00"), (ByKey(tree, "selected-color").Text, ByKey(tree, "selected-color").Color));
        // the layers: 4 is layer 3 alone
        Assert.Equal(new bool?[] { false, false, true, false, false }, Enumerable.Range(1, 5).Select(l => ByKey(tree, "layer-" + l).Checkbox));
        Assert.False(ByKey(tree, "multilayer").Checkbox);
        // the smart pipe panel: west blocked
        Assert.True(ByKey(tree, "dir-north").Selected);
        Assert.False(ByKey(tree, "dir-west").Selected);

        ByKey(tree, "category-1").OnClick();
        ByKey(tree, "mode-2").OnClick();
        ByKey(tree, "color-red").OnClick();
        ByKey(tree, "layer-5").OnClick();
        ByKey(tree, "multilayer").OnClick();
        ByKey(tree, "dir-west").OnClick();
        ByKey(tree, "init-reset").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("category", "{\"category\":1}"), ("mode", "{\"mode\":2}"), ("color", "{\"paint_color\":\"red\"}"), ("pipe_layers", "{\"pipe_layers\":16}"),
            ("toggle_multi_layer", null), ("init_dir_setting", "{\"dir_flag\":\"west\"}"), ("init_reset", null),
        }, acts.Select(a => (a.Action, a.Params)));

        // multi layer: each layer's own bit
        var (_, multi, _) = Render(UiValue.FromJson(Atmos.Replace("\"multi_layer\":0,\"pipe_layers\":4", "\"multi_layer\":1,\"pipe_layers\":21")));
        Assert.Equal(new bool?[] { true, false, true, false, true }, Enumerable.Range(1, 5).Select(l => ByKey(multi, "layer-" + l).Checkbox));
        // the other categories: no colours, no layers, no direction panel
        var (_, disposals, _) = Render(UiValue.FromJson(Atmos.Replace("\"category\":0", "\"category\":1")));
        Assert.True(ByKey(disposals, "category-1").Selected);
        Assert.DoesNotContain(disposals.Walk(), n => n.Key == "color-red" || n.Key == "layer-1" || n.Key == "dir-north");
    }

    [Fact]
    public void TheRecipeTabsShowTgsPipePreviewsAndAPreviewPicksTheRecipeAndItsDirection()
    {
        var local = new TguiLocalState();
        var (_, tree, acts) = Render(UiValue.FromJson(Atmos), local);
        // the tab of the selected recipe first (useState(selected_category))
        Assert.True(ByKey(tree, "tab-Pipes").Selected);
        Assert.Equal("grip-lines", ByKey(tree, "tab-Pipes").Icon);
        Assert.Equal("arrows-left-right", ByKey(tree, "tab-Binary").Icon);
        UiNode pipe = ByKey(tree, "preview-Pipe");
        Assert.Equal(UiKind.ImageButton, pipe.Kind);
        Assert.Equal((TguiSpritesheets.Pipes32, "south-manifold4w"), (pipe.DmIcon, pipe.DmIconState));
        Assert.True(pipe.Selected);
        Assert.Equal("Pipe", pipe.Tooltip); // one direction: nothing to turn
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "recipe-Pressure Pump");

        // the Binary tab (local state, no act), its pump's previews and description
        ByKey(tree, "tab-Binary").OnClick();
        Assert.Empty(acts);
        (_, tree, acts) = Render(UiValue.FromJson(Atmos), local);
        Assert.True(ByKey(tree, "tab-Binary").Selected);
        Assert.Equal("Pressure Pump", ByKey(tree, "recipe-Pressure Pump").Title);
        Assert.Equal("Moves gas from one pipeline to another.\nTransfers a variable amount of gas.", ByKey(tree, "info-Pressure Pump").Tooltip);
        // one button for the pump: its first direction while it is not chosen, Shift and the wheel turn it
        Assert.Single(tree.Walk(), n => n.Kind == UiKind.ImageButton && n.Key.StartsWith("preview-Pressure Pump", StringComparison.Ordinal));
        UiNode pump = ByKey(tree, "preview-Pressure Pump");
        Assert.Equal("north-pump", pump.DmIconState);
        Assert.False(pump.Selected);
        Assert.Equal("North\nShift + mouse wheel: turn it", pump.Tooltip);
        pump.OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("pipe_type", "{\"pipe_type\":3,\"category\":\"Binary\"}"), ("setdir", "{\"dir\":\"north\",\"flipped\":0}"),
        }, acts.Select(a => (a.Action, a.Params)));
        // chosen and turned east: the button shows the east picture
        (_, tree, _) = Render(UiValue.FromJson(Atmos.Replace("{\"selected\":0,\"dir\":\"east\"", "{\"selected\":1,\"dir\":\"east\"")), local);
        Assert.Equal("east-pump", ByKey(tree, "preview-Pressure Pump").DmIconState);
        Assert.True(ByKey(tree, "preview-Pressure Pump").Selected);
        // a server update that selects another category keeps the player's tab (React's state)
        (_, tree, _) = Render(UiValue.FromJson(Atmos.Replace("\"selected_category\":\"Pipes\"", "\"selected_category\":\"Binary\"")), local);
        Assert.True(ByKey(tree, "tab-Binary").Selected);
    }

    [Fact]
    public void TheSheetClassesResolveToTgsPipeFilesStateAndDirection()
    {
        Assert.True(TguiSpritesheets.TryResolve("pipes32x32", "east-pump", out SpritesheetIcon pump));
        Assert.Equal(("pump", (byte)4), (pump.State, pump.Dir));
        Assert.Equal("icons/obj/pipes_n_cables/pipe_item.dmi", pump.Files[^1]);
        Assert.Contains("icons/obj/pipes_n_cables/disposal.dmi", pump.Files);
        Assert.Contains("icons/obj/pipes_n_cables/transit_tube.dmi", pump.Files);
        Assert.True(TguiSpritesheets.TryResolve("pipes32x32", "southwest-he_manifold", out SpritesheetIcon diagonal));
        Assert.Equal(("he_manifold", (byte)10), (diagonal.State, diagonal.Dir));
        Assert.False(TguiSpritesheets.TryResolve("pipes32x32", "pump", out _));
        Assert.False(TguiSpritesheets.TryResolve("crafting32x32", "north-pump", out _));
        Assert.Contains("RapidPipeDispenser", TguiInterfaces.Names);
    }
}
