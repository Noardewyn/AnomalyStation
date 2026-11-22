using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps LC1: the client's GulagTeleporterConsole, GulagItemReclaimer and PrisonerManagement windows (tg's interfaces of the same
/// names) show their data and send their acts; all three are registered under their names.
/// </summary>
public class LaborCampWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheTeleporterConsoleShowsItsUnitsAndProcessesThePrisoner()
    {
        Assert.NotNull(TguiInterfaces.Find("GulagTeleporterConsole"));
        var (context, tree, acts) = Render("GulagTeleporterConsole", GulagTeleporterConsole.Render,
            "{\"prisoner\":{\"name\":\"Pat\",\"crimstat\":\"Arrest\"},\"teleporter\":\"[0x1]\",\"teleporter_location\":\"(6, 4, 1)\",\"teleporter_lock\":0,"
            + "\"teleporter_state_open\":0,\"beacon\":null,\"id\":\"[0x2]\",\"id_name\":\"Pat\",\"goal\":300,\"can_teleport\":1}");
        Assert.Equal((350.0, 295.0), (context.WindowWidth, context.WindowHeight));
        string text = tree.AllText();
        Assert.Contains("(6, 4, 1)", text);
        Assert.Contains("Not Connected", text);
        Assert.Contains("Arrest", text);
        Assert.Equal(300, ByKey(tree, "set_goal").Value);
        ByKey(tree, "scan_beacon").OnClick();
        ByKey(tree, "handle_id").OnClick();
        ByKey(tree, "set_goal").OnChange(500);
        ByKey(tree, "teleport").OnClick();
        Assert.Equal(new (string, string)[] { ("scan_beacon", null), ("handle_id", null), ("set_goal", "{\"value\":500}"), ("teleport", null) }, acts);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "scan_teleporter");
    }

    [Fact]
    public void TheReclaimerListsItsPrisonersAndTheManagementConsoleItsImplants()
    {
        Assert.NotNull(TguiInterfaces.Find("GulagItemReclaimer"));
        var (_, reclaimer, reclaimActs) = Render("GulagItemReclaimer", GulagItemReclaimer.Render, "{\"mobs\":[{\"name\":\"Pat\",\"mob\":\"[0x5]\"}],\"can_reclaim\":1}");
        Assert.Contains("Pat", reclaimer.AllText());
        ByKey(reclaimer, "release:[0x5]").OnClick();
        Assert.Equal(("release_items", "{\"mobref\":\"[0x5]\"}"), reclaimActs.Single());
        var (_, empty, _) = Render("GulagItemReclaimer", GulagItemReclaimer.Render, "{\"mobs\":[],\"can_reclaim\":0}");
        Assert.Contains("No stored items", empty.AllText());

        Assert.NotNull(TguiInterfaces.Find("PrisonerManagement"));
        var (_, login, loginActs) = Render("PrisonerManagement", PrisonerManagement.Render, "{\"authorized\":0,\"inserted_id\":null,\"implants\":[]}");
        Assert.Contains("Nanotrasen SecurityHUB", login.AllText());
        ByKey(login, "login").OnClick();
        Assert.Equal(("login", null), loginActs.Single());

        const string Console = "{\"authorized\":1,\"inserted_id\":{\"name\":\"prisoner ID card\",\"points\":40,\"goal\":100},\"implants\":["
            + "{\"info\":{\"ID\":\"Pat\",\"Volume\":\"10u\"},\"buttons\":[{\"name\":\"Self Destruct\",\"color\":\"bad\",\"action_key\":\"self_destruct\"},"
            + "{\"name\":\"Inject 5u\",\"color\":\"good\",\"action_key\":\"inject\",\"action_params\":{\"amount\":5}}],\"category\":\"chem implant\",\"ref\":\"[0x9]\"},"
            + "{\"info\":{\"ID\":\"Pat\",\"Location\":\"Brig\"},\"buttons\":[{\"name\":\"Warn\",\"color\":\"average\",\"action_key\":\"warn\"}],\"category\":\"tracking implant\",\"ref\":\"[0xa]\"}]}";
        var local = new TguiLocalState();
        var (_, console, acts) = Render("PrisonerManagement", PrisonerManagement.Render, Console, local);
        string text = console.AllText();
        Assert.Contains("prisoner ID card", text);
        Assert.Contains("10u", text);
        Assert.Contains("Space Law recommends quotas", text);
        Assert.True(ByKey(console, "tab:chem implant").Selected);
        Assert.Equal("Tracking", ByKey(console, "tab:tracking implant").Text);
        ByKey(console, "button:inject").OnClick();
        Assert.Equal(("handle_implant", "{\"implant_ref\":\"[0x9]\",\"implant_action\":\"inject\",\"amount\":5}"), acts.Single());
        ByKey(console, "tab:tracking implant").OnClick();
        var (_, tracking, _) = Render("PrisonerManagement", PrisonerManagement.Render, Console, local);
        Assert.Contains("Brig", tracking.AllText());
        Assert.DoesNotContain(tracking.Walk(), n => n.Key == "button:inject");
    }
}
