using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The client's PaintingMachine, PipeDispenser and Gateway windows (tg's PaintingMachine.jsx, PipeDispenser.tsx and Gateway.jsx)
/// without the engine: each reads tg's ui_data as the JSX does and sends tg's acts, and all three are registered.
/// </summary>
public class PainterDispenserGatewayWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json, TguiLocalState local = null, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var sink = acts;
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => sink.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Painter = "{\"pdaTypes\":{\"/obj/item/modular_computer/pda\":\"PDA\",\"/obj/item/modular_computer/pda/security\":\"Security PDA\"},"
        + "\"cardTrims\":{\"/datum/id_trim/job/assistant\":\"Assistant\",\"/datum/id_trim/job/security_officer\":\"Security Officer\"},"
        + "\"hasPDA\":1,\"pdaName\":\"Bob's PDA\",\"hasID\":0,\"idName\":null}";

    [Fact]
    public void ThePaintingMachinePaintsTheChosenPdaAfterAConfirmAndDisablesTheIdButtonsWithoutAnId()
    {
        var local = new TguiLocalState();
        var acts = new List<(string, string)>();
        var (context, tree, _) = Render(PaintingMachine.Render, "PaintingMachine", Painter, local, acts);
        Assert.Equal((500.0, 620.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Bob's PDA", ByKey(tree, "eject_pda").Text);
        Assert.Equal("-----", ByKey(tree, "eject_card").Text);
        Assert.True(ByKey(tree, "trim_card").Disabled);
        Assert.False(ByKey(tree, "trim_pda").Disabled);
        UiNode dropdown = ByKey(tree, "pdaSelection");
        Assert.Equal(new[] { "PDA", "Security PDA" }, dropdown.Options);
        Assert.Equal("PDA", dropdown.TextValue);

        dropdown.OnSelected("Security PDA");
        (_, tree, _) = Render(PaintingMachine.Render, "PaintingMachine", Painter, local, acts);
        ByKey(tree, "trim_pda").OnClick(); // the first click asks for a confirm
        Assert.Empty(acts);
        (_, tree, _) = Render(PaintingMachine.Render, "PaintingMachine", Painter, local, acts);
        Assert.Equal("Confirm?", ByKey(tree, "trim_pda").Text);
        ByKey(tree, "trim_pda").OnClick();
        ByKey(tree, "eject_pda").OnClick();
        Assert.Equal(new (string, string)[] { ("trim_pda", "{\"selection\":\"Security PDA\"}"), ("eject_pda", null) }, acts);
    }

    private const string Dispenser = "{\"category\":0,\"piping_layer\":3,\"selected_color\":\"green\","
        + "\"categories\":[{\"cat_name\":\"Pipes\",\"recipes\":[{\"pipe_name\":\"Pipe\",\"pipe_index\":1,\"all_layers\":0,\"dir\":1}]},"
        + "{\"cat_name\":\"Devices\",\"recipes\":[{\"pipe_name\":\"Gas pump\",\"pipe_index\":1,\"all_layers\":0,\"dir\":1},{\"pipe_name\":\"Meter\",\"pipe_index\":2,\"all_layers\":1,\"dir\":1}]}],"
        + "\"init_directions\":{\"north\":1,\"south\":1,\"east\":1,\"west\":1},\"paint_colors\":{\"green\":\"#1EFF00\",\"blue\":\"#0000FF\"}}";

    [Fact]
    public void ThePipeDispenserDispensesFromItsTabsAndShowsTheAtmosRowsOnlyForPipes()
    {
        var local = new TguiLocalState();
        var acts = new List<(string, string)>();
        var (context, tree, _) = Render(PipeDispenser.Render, "PipeDispenser", Dispenser, local, acts);
        Assert.Equal((530.0, 530.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Key == "layer-3" && n.Checkbox == true);
        Assert.Contains(tree.Walk(), n => n.Key == "init-reset");
        Assert.True(ByKey(tree, "tab-Pipes").Selected);

        ByKey(tree, "recipe-1-1").OnClick();
        ByKey(tree, "tab-Devices").OnClick();
        (_, tree, _) = Render(PipeDispenser.Render, "PipeDispenser", Dispenser, local, acts);
        ByKey(tree, "recipe-2-1").OnClick();
        ByKey(tree, "color-blue").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("pipe_type", "{\"pipe_type\":1,\"pipe_dir\":1,\"category\":\"Pipes\"}"),
            ("pipe_type", "{\"pipe_type\":2,\"pipe_dir\":1,\"category\":\"Devices\"}"),
            ("color", "{\"paint_color\":\"blue\"}"),
        }, acts);

        // the disposal dispenser: no colour, layer or smart pipe rows
        var (_, disposal, _) = Render(PipeDispenser.Render, "PipeDispenser", Dispenser.Replace("\"category\":0", "\"category\":1"));
        Assert.DoesNotContain(disposal.Walk(), n => n.Key == "init-reset" || n.Key == "layer-3");
    }

    [Fact]
    public void TheGatewayWindowLinksUpListsItsNodesAndActivatesAnAvailableOne()
    {
        var (context, tree, acts) = Render(Gateway.Render, "Gateway", "{\"gateway_present\":0,\"destinations\":[]}");
        Assert.Equal((350.0, 440.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("No linked gateway", ByKey(tree, "notice").Text);
        ByKey(tree, "linkup").OnClick();
        Assert.Equal(new (string, string)[] { ("linkup", null) }, acts);

        var (_, empty, _) = Render(Gateway.Render, "Gateway", "{\"gateway_present\":1,\"gateway_status\":1,\"current_target\":null,\"destinations\":[]}");
        Assert.Equal("No gateway nodes detected.", ByKey(empty, "none").Text);

        string nodes = "{\"gateway_present\":1,\"gateway_status\":0,\"current_target\":null,\"destinations\":["
            + "{\"ref\":\"[0x1]\",\"name\":\"Beach\",\"available\":1,\"reason\":\"Unreachable\"},"
            + "{\"ref\":\"[0x2]\",\"name\":\"Snow\",\"available\":0,\"reason\":\"Connection desynchronized. Recalibration in progress.\",\"timeout\":0.5}]}";
        var (_, listed, listedActs) = Render(Gateway.Render, "Gateway", nodes);
        Assert.Equal("Gateway Unpowered", ByKey(listed, "unpowered").Text);
        Assert.Equal(0.5, ByKey(listed, "timeout").Value);
        ByKey(listed, "activate").OnClick();
        Assert.Equal(new (string, string)[] { ("activate", "{\"destination\":\"[0x1]\"}") }, listedActs);

        var (_, active, activeActs) = Render(Gateway.Render, "Gateway", "{\"gateway_present\":1,\"gateway_status\":1,\"gateway_mapkey\":\"gateway_popup_x\",\"current_target\":{\"name\":\"Beach\"},\"destinations\":[]}");
        Assert.Equal("gateway_popup_x", active.Walk().Single(n => n.Kind == UiKind.ByondUi).MapRef);
        ByKey(active, "deactivate").OnClick();
        Assert.Equal(new (string, string)[] { ("deactivate", null) }, activeActs);
    }

    [Fact]
    public void AllThreeWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)PaintingMachine.Render, TguiInterfaces.Find("PaintingMachine"));
        Assert.Equal((TguiInterface)PipeDispenser.Render, TguiInterfaces.Find("PipeDispenser"));
        Assert.Equal((TguiInterface)Gateway.Render, TguiInterfaces.Find("Gateway"));
    }
}
