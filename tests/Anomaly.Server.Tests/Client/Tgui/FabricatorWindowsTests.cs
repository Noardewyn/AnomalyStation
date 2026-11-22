using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps RD1 (the fabricators): the client's Fabricator window (tg's Fabricator.tsx) of the protolathes, circuit imprinters and techfabs
/// and its Autolathe window (Autolathe.tsx). They read tg's ui_static_data and ui_data the way the TSX does (the designs in the shared
/// design browser, a recipe greyed when its materials are short, its print buttons and its [Max] input; the fabricator's material bar,
/// its on-hold and busy dimmers; the autolathe's total and per-material bars with tg's eject amounts) and send tg's acts; they are
/// registered under their names.
/// </summary>
public class FabricatorWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string name, Func<TguiContext, UiNode> render, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Materials = "\"materials\":[{\"name\":\"iron\",\"ref\":\"[0x1]\",\"amount\":1000,\"color\":\"#878687\"},"
        + "{\"name\":\"glass\",\"ref\":\"[0x2]\",\"amount\":0,\"color\":\"#88cdf1\"}]";

    private static string FabricatorJson(int onHold = 0, int busy = 0) => "{\"fabName\":\"department protolathe (Engineering)\",\"SHEET_MATERIAL_AMOUNT\":100,"
        + "\"onHold\":" + onHold + ",\"busy\":" + busy + ",\"materialMaximum\":7500,\"queue\":[]," + Materials + ",\"designs\":{"
        + "\"/datum/design/basic_matter_bin\":{\"name\":\"Basic Matter Bin\",\"desc\":\"A stock part used in the construction of various devices.\","
        + "\"cost\":{\"iron\":100},\"path\":\"/datum/design/basic_matter_bin\",\"categories\":[\"/Stock Parts/Stock Parts 1\"],\"icon\":\"basic_matter_bin\"},"
        + "\"/datum/design/basic_cell\":{\"name\":\"Basic Power Cell\",\"desc\":\"A basic power cell that holds 10 KW of energy.\","
        + "\"cost\":{\"iron\":700,\"glass\":50},\"path\":\"/datum/design/basic_cell\",\"categories\":[\"/Stock Parts/Stock Parts 1\"],\"icon\":\"basic_cell\"}}}";

    [Fact]
    public void TheFabricatorWindowListsItsDesignsGreysTheUnaffordableAndSendsTgsActs()
    {
        var (context, tree, acts) = Render("Fabricator", Fabricator.Render, FabricatorJson());
        Assert.Equal((670.0, 600.0, "department protolathe (Engineering)"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        Assert.Contains("All Designs (2)", tree.AllText());

        UiNode bin = ByKey(tree, "recipe:/datum/design/basic_matter_bin");
        Assert.Contains(bin.Walk(), n => n.Key == "name" && n.Text == "Basic Matter Bin");
        Assert.Contains(bin.Walk(), n => n.Key == "pic" && n.DmIconState == "basic_matter_bin" && n.DmIcon == TguiSpritesheets.Design32);
        Assert.False(ByKey(bin, "print").Disabled);
        Assert.False(ByKey(bin, "x5").Disabled);
        Assert.False(ByKey(bin, "x10").Disabled); // 10 bins take all 1000 iron
        Assert.Equal("[Max: 10]", ByKey(bin, "max").Tooltip);

        // the cell needs glass, which is out
        UiNode cell = ByKey(tree, "recipe:/datum/design/basic_cell");
        Assert.True(ByKey(cell, "print").Disabled);
        Assert.True(ByKey(cell, "x5").Disabled);
        Assert.Equal("[Max: 0]", ByKey(cell, "max").Tooltip);

        ByKey(bin, "print").OnClick();
        ByKey(cell, "print").OnClick(); // greyed: nothing is sent
        ByKey(bin, "x5").OnClick();
        ByKey(bin, "max").OnCommit("3");
        // the material bar: one sheet of iron out
        ByKey(ByKey(tree, "mat:iron"), "one").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("build", "{\"design_path\":\"/datum/design/basic_matter_bin\",\"amount\":1}"),
            ("build", "{\"design_path\":\"/datum/design/basic_matter_bin\",\"amount\":5}"),
            ("build", "{\"design_path\":\"/datum/design/basic_matter_bin\",\"amount\":\"3\"}"),
            ("remove_mat", "{\"ref\":\"[0x1]\",\"amount\":1}"),
        }, acts);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "onHold" || n.Key == "busy");
    }

    [Fact]
    public void TheFabricatorWindowDimsWhileOnHoldOrBuilding()
    {
        var (_, held, _) = Render("Fabricator", Fabricator.Render, FabricatorJson(onHold: 1));
        Assert.Equal(UiKind.Modal, ByKey(held, "onHold").Kind);
        Assert.Contains("Mineral access is on hold, please contact the quartermaster.", held.AllText());

        var (_, busy, _) = Render("Fabricator", Fabricator.Render, FabricatorJson(busy: 1));
        Assert.Equal(UiKind.Modal, ByKey(busy, "busy").Kind);
        Assert.Contains("Building items...", busy.AllText());
    }

    private static string AutolatheJson(int active = 0) => "{\"SHEET_MATERIAL_AMOUNT\":100,\"materialtotal\":1000,\"materialsmax\":11250,\"active\":" + active + ","
        + Materials + ",\"designs\":["
        + "{\"name\":\"Pocket Crowbar\",\"desc\":\"A small crowbar.\",\"cost\":{\"iron\":80},\"path\":\"/datum/design/crowbar\","
        + "\"categories\":[\"initial\",\"/Tools/Engineering Tools\"],\"icon\":\"crowbar\",\"customMaterials\":0},"
        + "{\"name\":\"Toolbox\",\"desc\":\"Danger. Very robust.\",\"cost\":{\"Rigid material\":800},\"path\":\"/datum/design/toolbox\","
        + "\"categories\":[\"initial\",\"/Tools/Tool Storage\"],\"icon\":\"toolbox\",\"customMaterials\":1}]}";

    [Fact]
    public void TheAutolatheWindowShowsItsMaterialsAndPrintsWithTgsActs()
    {
        var (context, tree, acts) = Render("Autolathe", Autolathe.Render, AutolatheJson());
        Assert.Equal((670.0, 600.0, "Autolathe"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));

        UiNode total = ByKey(tree, "totalBar");
        Assert.Equal((UiKind.ProgressBar, 1000.0, 11250.0, "10/112.5 sheets"), (total.Kind, total.Value, total.MaxValue, total.Text));
        Assert.Equal(("bad", 0.0, 11250 * 0.25), total.Ranges[2]);

        // the materials with sheets in it: iron alone, its eject buttons sorted as JavaScript sorts [1, 5, 10]
        Assert.Contains(tree.Walk(), n => n.Key == "mat:iron" && n.Label == "Iron");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "mat:glass");
        UiNode iron = ByKey(tree, "mat:iron");
        Assert.Equal("10 sheets", ByKey(iron, "amount").Text);
        Assert.Equal(new[] { "x1", "x10", "x5" }, iron.Walk().Where(n => n.Key?.StartsWith("eject:", StringComparison.Ordinal) == true).Select(n => n.Text));
        ByKey(iron, "eject:1").OnClick();

        // the crowbar: 12 of 80 iron in 1000; the toolbox by its largest material, 1 of 800
        UiNode crowbar = ByKey(tree, "recipe:/datum/design/crowbar");
        Assert.Equal("[Max: 12]", ByKey(crowbar, "max").Tooltip);
        Assert.False(ByKey(crowbar, "x10").Disabled);
        UiNode toolbox = ByKey(tree, "recipe:/datum/design/toolbox");
        Assert.Equal("[Max: 1]", ByKey(toolbox, "max").Tooltip);
        Assert.False(ByKey(toolbox, "print").Disabled);
        Assert.True(ByKey(toolbox, "x5").Disabled);

        ByKey(crowbar, "print").OnClick();
        ByKey(crowbar, "x10").OnClick();
        ByKey(toolbox, "x5").OnClick(); // greyed: nothing is sent
        ByKey(toolbox, "max").OnCommit("1");
        Assert.Equal(new (string, string)[]
        {
            ("eject", "{\"ref\":\"[0x1]\",\"amount\":10}"),
            ("make", "{\"design_path\":\"/datum/design/crowbar\",\"multiplier\":1}"),
            ("make", "{\"design_path\":\"/datum/design/crowbar\",\"multiplier\":10}"),
            ("make", "{\"design_path\":\"/datum/design/toolbox\",\"multiplier\":\"1\"}"),
        }, acts);

        var (_, busy, _) = Render("Autolathe", Autolathe.Render, AutolatheJson(active: 1));
        Assert.Equal(UiKind.Modal, ByKey(busy, "busy").Kind);
    }

    [Fact]
    public void TheFabricatorAndAutolatheWindowsAreRegisteredUnderTheirNames()
    {
        Assert.Equal((TguiInterface)Fabricator.Render, TguiInterfaces.Find("Fabricator"));
        Assert.Equal((TguiInterface)Autolathe.Render, TguiInterfaces.Find("Autolathe"));
    }
}
