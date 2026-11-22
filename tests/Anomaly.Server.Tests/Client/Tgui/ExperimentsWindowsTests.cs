using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps RD3 (the experiments): the client's ExperimentConfigure window (tg's ExperimentConfigure.tsx, the experiment handler's) and
/// DestructiveAnalyzer window, without the engine. They read tg's ui_data the way the TSX does (the techwebs grouped with their servers,
/// the experiments sorted by name with their progress stages, the Perform Experiment button; the analyzer's notices, its item, its
/// deconstruction and its nodes) and send tg's acts; the Experiment panel is public for the Techweb window; both are registered.
/// </summary>
public class ExperimentsWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string name, string json, TguiInterface render, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode Button(UiNode tree, string text) => tree.Walk().Single(n => n.Kind == UiKind.Button && n.Text == text);

    private const string Web = "{\"web_id\":\"SCIENCE\",\"web_org\":\"Nanotrasen\",\"selected\":1,\"ref\":\"[0x21]\",\"all_servers\":[\"R&D Server\"]}";

    private const string Crystal = "{\"selected\":0,\"name\":\"Bluespace Crystal Sampling\",\"description\":\"Investigate bluespace crystals.\",\"tag\":\"Scan\","
        + "\"progress\":[[\"integer\",\"Scan samples of the following objects to accumulate enough points to complete this experiment.\",0,1],"
        + "[\"detail\",\"1 point: bluespace crystal, bluespace polycrystal\",0]],\"completed\":0,\"performance_hint\":\"Use a scanner.\",\"ref\":\"[0x31]\"}";

    private const string Meat = "{\"selected\":1,\"name\":\"Extreme Cooking Experiment\",\"description\":\"Cook a wall.\",\"tag\":\"Physical Experiment\","
        + "\"progress\":[[\"bool\",\"Fire an emitter at a tracked meat wall\",1]],\"completed\":0,\"performance_hint\":\"Track a wall.\",\"ref\":\"[0x32]\"}";

    [Fact]
    public void TheExperimentWindowListsTheWebAndItsExperimentsSortedWithTheirStagesAndSendsTgsActs()
    {
        var (context, tree, acts) = Render("ExperimentConfigure",
            "{\"always_active\":0,\"has_start_callback\":1,\"techwebs\":[" + Web + "],\"experiments\":[" + Meat + "," + Crystal + "]}",
            ExperimentConfigure.Render);
        Assert.Equal((600.0, 735.0), (context.WindowWidth, context.WindowHeight));
        string text = tree.AllText();
        Assert.Contains("Please select a techweb to connect to...", text);
        Assert.Contains("SCIENCE / Nanotrasen", text);
        Assert.Contains("R&D Server", text);
        Assert.Contains("Select one of the following experiments...", text);

        // sorted by name: the crystal's panel before the meat wall's
        List<string> titles = tree.Walk().Where(n => n.Kind == UiKind.Button && n.Key == "title").Select(n => n.Text).ToList();
        Assert.Equal(new[] { "Bluespace Crystal Sampling", "Extreme Cooking Experiment" }, titles);
        Assert.True(Button(tree, "Extreme Cooking Experiment").Selected);

        // the stages: an integer's count, a detail's arrow, a bool's check icon
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.TableCell && n.Text == "0/1" && n.Color == "bad");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.TableCell && n.Text == "⤷");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Icon && n.Icon == "check" && n.Color == "good");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Icon && n.Icon == "question-circle" && n.Tooltip == "Use a scanner.");

        Button(tree, "Disconnect").OnClick();
        Button(tree, "Bluespace Crystal Sampling").OnClick();
        Button(tree, "Extreme Cooking Experiment").OnClick();
        UiNode perform = Button(tree, "Perform Experiment");
        Assert.False(perform.Disabled);
        perform.OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("clear_server", null), ("select_experiment", "{\"ref\":\"[0x31]\"}"), ("clear_experiment", null), ("start_experiment_callback", null),
        }, acts);
    }

    [Fact]
    public void WithoutAServerTheWindowSaysSoAndAnAlwaysActiveDeviceNeedsNoSelection()
    {
        var (_, none, _) = Render("ExperimentConfigure", "{\"always_active\":0,\"has_start_callback\":0,\"techwebs\":[],\"experiments\":[]}", ExperimentConfigure.Render);
        Assert.Contains("Found no servers connected to a techweb!", none.AllText());
        Assert.DoesNotContain(none.Walk(), n => n.Kind == UiKind.Section && n.Title == "Experiments");
        Assert.DoesNotContain(none.Walk(), n => n.Text == "Perform Experiment");

        var (_, active, _) = Render("ExperimentConfigure",
            "{\"always_active\":1,\"has_start_callback\":1,\"techwebs\":[" + Web.Replace("\"selected\":1", "\"selected\":0") + "],\"experiments\":[" + Crystal + "]}",
            ExperimentConfigure.Render);
        Assert.Contains("Connect", active.Walk().Where(n => n.Kind == UiKind.Button).Select(n => n.Text));
        Assert.DoesNotContain(active.Walk(), n => n.Kind == UiKind.Section && n.Title == "Experiments"); // no web connected yet
        Assert.True(Button(active, "Perform Experiment").Disabled);

        var (_, connected, _) = Render("ExperimentConfigure", "{\"always_active\":1,\"has_start_callback\":0,\"techwebs\":[" + Web + "],\"experiments\":[" + Crystal + "]}",
            ExperimentConfigure.Render);
        Assert.Contains("This device is configured to attempt to perform all available experiments, so no further configuration is necessary.", connected.AllText());
    }

    [Fact]
    public void TheExperimentPanelIsPublicWithItsChildrenBeforeTheStages()
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "Techweb", "device", 2, UiValue.FromJson("{}"), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        UiNode note = new UiNode(UiKind.Box) { Key = "note", Text = "Discount: 80 points" };
        UiNode panel = ExperimentConfigure.Experiment(context, UiValue.FromJson(Crystal), note);
        List<UiNode> nodes = panel.Walk().ToList();
        int noteAt = nodes.IndexOf(note);
        int descriptionAt = nodes.FindIndex(n => n.Text == "Investigate bluespace crystals.");
        int stagesAt = nodes.FindIndex(n => n.Kind == UiKind.Table);
        Assert.True(descriptionAt < noteAt && noteAt < stagesAt, "the children are not between the description and the stages");
        Button(panel, "Bluespace Crystal Sampling").OnClick();
        Assert.Equal(("select_experiment", "{\"ref\":\"[0x31]\"}"), acts.Single());

        // can_select false (the operating computer): the title is a bar, not a button
        UiNode bar = ExperimentConfigure.Experiment(context, UiValue.FromJson(Crystal), null, canSelect: false);
        Assert.DoesNotContain(bar.Walk(), n => n.Kind == UiKind.Button);
        Assert.Contains(bar.Walk(), n => n.Kind == UiKind.Box && n.Text == "Bluespace Crystal Sampling");
    }

    [Fact]
    public void TheDestructiveAnalyzerWindowShowsItsNoticesItsItemAndItsDeconstructions()
    {
        var (context, noServer, _) = Render("DestructiveAnalyzer", "{\"server_connected\":0,\"node_data\":[],\"loaded_item\":null}", DestructiveAnalyzer.Render);
        Assert.Equal((400.0, 260.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Destructive Analyzer", context.WindowTitle);
        Assert.Contains("Not connected to a server. Please sync one using a multitool.", noServer.AllText());
        var (_, empty, _) = Render("DestructiveAnalyzer", "{\"server_connected\":1,\"node_data\":[],\"loaded_item\":null}", DestructiveAnalyzer.Render);
        Assert.Contains("No item loaded!", empty.AllText());

        var local = new TguiLocalState();
        const string Loaded = "{\"server_connected\":1,\"loaded_item\":\"alien cautery\",\"item_icon\":{\"icon\":\"icons/obj/antags/abductor.dmi\",\"icon_state\":\"cautery\"},"
            + "\"indestructible\":1,\"already_deconstructed\":0,\"recoverable_points\":\"\","
            + "\"node_data\":[{\"node_name\":\"Alien Technology\",\"node_path\":\"/datum/techweb_node/alien/base\",\"node_hidden\":1},"
            + "{\"node_name\":\"Alien Surgery\",\"node_path\":\"/datum/techweb_node/alien/surgery\",\"node_hidden\":0}]}";
        var (_, tree, acts) = Render("DestructiveAnalyzer", Loaded, DestructiveAnalyzer.Render, local);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Section && n.Title == "alien cautery");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Box && n.DmIcon == "icons/obj/antags/abductor.dmi" && n.DmIconState == "cautery");
        Assert.DoesNotContain("Research points from deconstruction", tree.AllText());
        Assert.True(Button(tree, "Alien Surgery").Disabled);
        Assert.Equal("This node has already been researched.", Button(tree, "Alien Surgery").Tooltip);

        tree.Walk().Single(n => n.Key == "eject").OnClick();
        Assert.Equal(new (string, string)[] { ("eject_item", null) }, acts);
        // Button.Confirm: the first click asks, the second one (in the next render) acts
        Button(tree, "Deconstruct").OnClick();
        var (_, confirm, confirmActs) = Render("DestructiveAnalyzer", Loaded, DestructiveAnalyzer.Render, local);
        Button(confirm, "Confirm?").OnClick();
        Assert.Equal(new (string, string)[] { ("deconstruct", null) }, confirmActs);
        tree = Render("DestructiveAnalyzer", Loaded, DestructiveAnalyzer.Render, local).Tree;
        Button(tree, "Alien Technology").OnClick();
        var (_, confirmNode, nodeActs) = Render("DestructiveAnalyzer", Loaded, DestructiveAnalyzer.Render, local);
        Button(confirmNode, "Confirm?").OnClick();
        Assert.Equal(new (string, string)[] { ("deconstruct", "{\"deconstruct_path\":\"/datum/techweb_node/alien/base\"}") }, nodeActs);

        // an indestructible item cannot be deconstructed; the points it would give are shown
        var (_, solid, _) = Render("DestructiveAnalyzer", Loaded.Replace("\"indestructible\":1", "\"indestructible\":0"), DestructiveAnalyzer.Render);
        Assert.Contains("This item can't be deconstructed!", solid.AllText());
        Assert.DoesNotContain(solid.Walk(), n => n.Kind == UiKind.Button && n.Text == "Deconstruct");
        var (_, points, _) = Render("DestructiveAnalyzer", Loaded.Replace("\"recoverable_points\":\"\"", "\"recoverable_points\":\"200 Gen. Res.\""), DestructiveAnalyzer.Render);
        Assert.Contains("Research points from deconstruction", points.AllText());
        Assert.Contains("200 Gen. Res.", points.AllText());
    }

    [Fact]
    public void TheExperimentWindowsAreRegisteredUnderTheirNames()
    {
        Assert.Equal((TguiInterface)ExperimentConfigure.Render, TguiInterfaces.Find("ExperimentConfigure"));
        Assert.Equal((TguiInterface)DestructiveAnalyzer.Render, TguiInterfaces.Find("DestructiveAnalyzer"));
    }
}
