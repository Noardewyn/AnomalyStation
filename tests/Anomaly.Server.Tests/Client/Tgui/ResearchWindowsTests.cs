using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps RD2 (the R&amp;D consoles and servers): the client's ServerControl window (tg's ServerControl.tsx) lists the servers with their
/// switch, the consoles with their lock and the research history, and sends tg's acts; the Techweb window (tg's Techweb/) expands the
/// compressed static data, shows the header, the tabs of nodes and a node's research buttons, its detail and the disks, and sends
/// tg's acts; NtosTechweb is the same window in the device's frame. All are registered under their names.
/// </summary>
public class ResearchWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string name, Func<TguiContext, UiNode> render, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Control = "{\"server_connected\":1,"
        + "\"logs\":[{\"node_name\":\"Basic Robotics\",\"node_cost\":2500,\"node_researcher\":\"Bob\",\"node_research_location\":\"Research Division (50,60,2)\"}],"
        + "\"servers\":[{\"server_name\":\"R&D Server 4f2a\",\"server_details\":\"Nominal\",\"server_disabled\":0,\"server_ref\":\"[0x2001]\"}],"
        + "\"consoles\":[{\"console_name\":\"R&D Console\",\"console_location\":\"Research Division\",\"console_locked\":1,\"console_ref\":\"[0x2002]\"}]}";

    [Fact]
    public void TheServerControlWindowListsServersConsolesAndHistoryWithTgsActs()
    {
        var (context, tree, acts) = Render("ServerControl", ServerControl.Render, Control);
        Assert.Equal((575.0, 400.0), (context.WindowWidth, context.WindowHeight));
        UiNode lockdown = ByKey(tree, "lockdown");
        Assert.Equal(("Online", "good", "Nominal"), (lockdown.Text, lockdown.Color, lockdown.Tooltip));
        UiNode consoleLock = ByKey(tree, "lockConsole");
        Assert.Equal(("LOCKED", "bad"), (consoleLock.Text, consoleLock.Color));
        string text = tree.AllText();
        Assert.Contains("R&D Server 4f2a", text);
        Assert.Contains("R&D Console - Location: Research Division", text);
        Assert.Contains("Basic Robotics", text);
        Assert.Contains("2500", text);

        lockdown.OnClick();
        consoleLock.OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("lockdown_server", "{\"selected_server\":\"[0x2001]\"}"), ("lock_console", "{\"selected_console\":\"[0x2002]\"}"),
        }, acts);

        var (_, none, _) = Render("ServerControl", ServerControl.Render, "{\"server_connected\":0}");
        Assert.Contains("Not connected to a Server. Please sync one using a multitool.", none.AllText());
        var (_, empty, _) = Render("ServerControl", ServerControl.Render, "{\"server_connected\":1,\"logs\":[]}");
        Assert.Contains("No servers found.", empty.AllText());
        Assert.Contains("No consoles found.", empty.AllText());
        Assert.Contains("No history found.", empty.AllText());
    }

    // two nodes (an available one and a researched one), one design, compressed as the console sends them
    private const string TechwebJson = "{\"stored_research\":1,\"locked\":0,\"sec_protocols\":1,\"web_org\":\"Nanotrasen\","
        + "\"points\":{\"General Research\":3000},\"points_last_tick\":{\"General Research\":52},"
        + "\"point_types_abbreviations\":{\"General Research\":\"Gen. Res.\"},\"queue_nodes\":[],\"experiments\":[],"
        + "\"researched_designs\":{},\"t_disk\":null,\"d_disk\":null,"
        + "\"nodes\":[{\"path\":\"/datum/techweb_node/robotics\",\"is_free\":0,\"can_unlock\":1,\"have_experiments_done\":1,\"tier\":1,\"enqueued_by_user\":0,\"discount_boosted\":0},"
        + "{\"path\":\"/datum/techweb_node/base\",\"is_free\":1,\"can_unlock\":1,\"have_experiments_done\":1,\"tier\":0,\"enqueued_by_user\":0,\"discount_boosted\":0}],"
        + "\"static_data\":{\"id_cache\":[\"General Research\",\"/datum/techweb_node/base\",\"/datum/design/borg_suit\",\"/datum/techweb_node/robotics\"],"
        + "\"node_cache\":{\"2\":{\"name\":\"Basic Research Technology\",\"description\":\"NT default research technologies.\"},"
        + "\"4\":{\"name\":\"Basic Robotics\",\"description\":\"Programmable machines.\",\"costs\":{\"1\":2500},\"prerequisite_nodes\":[2],\"unlocked_designs\":[3]}},"
        + "\"design_cache\":{\"3\":[\"Cyborg Endoskeleton\",{\"iron\":20000},16,1,\"borg_suit\"]},"
        + "\"SHEET_MATERIAL_AMOUNT\":2000,\"build_types\":{\"16\":\"Exosuit Fabricator\"},\"department_flags\":{\"1\":\"Science\",\"2\":\"Medical\"}}}";

    [Fact]
    public void TheTechwebWindowExpandsItsStaticDataAndResearchesAvailableNodes()
    {
        var local = new TguiLocalState();
        var (context, tree, acts) = Render("Techweb", Techweb.Render, TechwebJson, local);
        Assert.Equal((640.0, 735.0), (context.WindowWidth, context.WindowHeight));
        string text = tree.AllText();
        Assert.Contains("Engaged", text);
        Assert.Contains("3000", text);
        Assert.Contains("(+52/sec)", text);
        Assert.Contains("Empty", text);

        // the Available tab: the robotics node with its cost, its tech progress and its design
        UiNode node = ByKey(tree, "node:/datum/techweb_node/robotics");
        Assert.Equal("Basic Robotics", node.Title);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "node:/datum/techweb_node/base");
        Assert.Contains("Gen. Res. (2500/2500)", node.AllText());
        Assert.Contains("Tech (1/1)", node.AllText());
        UiNode design = ByKey(node, "design:/datum/design/borg_suit");
        Assert.Equal(("design32x32", "borg_suit"), (design.DmIcon, design.DmIconState));
        Assert.Contains("Cyborg Endoskeleton", design.Tooltip);
        Assert.Contains("Exosuit Fabricator", design.Tooltip);
        Assert.Contains("Science", design.Tooltip);

        ByKey(node, "research").OnClick();
        ByKey(tree, "lock").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("researchNode", "{\"node_path\":\"/datum/techweb_node/robotics\"}"), ("toggleLock", null),
        }, acts);

        // the Researched tab and the node's details
        ByKey(tree, "researched").OnClick();
        UiNode researched = Techweb.Render(context);
        Assert.Contains(researched.Walk(), n => n.Key == "node:/datum/techweb_node/base");
        ByKey(tree, "details").OnClick();
        UiNode details = Techweb.Render(context);
        Assert.Contains("Required (1/1)", details.AllText());
        Assert.Contains("Unlocks (0)", details.AllText());
    }

    [Fact]
    public void ALockedConsoleShowsOnlyItsUnlockAndTheDisksHaveTheirMenus()
    {
        var (_, locked, acts) = Render("Techweb", Techweb.Render, "{\"stored_research\":1,\"locked\":1}");
        Assert.Contains("Console Locked", locked.AllText());
        ByKey(locked, "unlock").OnClick();
        Assert.Equal(("toggleLock", (string)null), acts.Single());

        var (_, noWeb, _) = Render("Techweb", Techweb.Render, "{\"stored_research\":0,\"locked\":0}");
        Assert.Contains("No research techweb found, please synchronize the console.", noWeb.AllText());

        string withDisks = TechwebJson.Replace("\"t_disk\":null,\"d_disk\":null",
            "\"t_disk\":{\"stored_research\":{\"/datum/techweb_node/base\":1}},\"d_disk\":{\"blueprints\":[\"/datum/design/borg_suit\",null]}");
        var (context, tree, diskActs) = Render("Techweb", Techweb.Render, withDisks);
        ByKey(tree, "designDisk").OnClick();
        UiNode design = Techweb.Render(context);
        Assert.Contains("Cyborg Endoskeleton", design.AllText());
        Assert.Contains("Empty", design.AllText());
        ByKey(design, "upload").OnClick();
        ByKey(design, "techDisk").OnClick();
        UiNode tech = Techweb.Render(context);
        Assert.Contains("Basic Research Technology", tech.AllText());
        ByKey(tech, "load").OnClick();
        ByKey(tech, "eject").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("uploadDisk", "{\"type\":\"design\"}"), ("loadTech", null), ("ejectDisk", "{\"type\":\"tech\"}"),
        }, diskActs);
    }

    [Fact]
    public void TheResearchWindowsAreRegisteredUnderTheirNames()
    {
        Assert.Equal((TguiInterface)ServerControl.Render, TguiInterfaces.Find("ServerControl"));
        Assert.Equal((TguiInterface)Techweb.Render, TguiInterfaces.Find("Techweb"));
        Assert.Equal((TguiInterface)Techweb.RenderNtos, TguiInterfaces.Find("NtosTechweb"));
    }
}
