using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps S4 (D327): the client's NtOS job app windows without the engine: NtosCard, NtosJobManager, NtosRecords and NtosStatus. The
/// payloads follow the servers' ui_data (card.dm:283, :306, jobmanagement.dm:106, records.dm:71, statusdisplay.dm:80).
/// </summary>
public class NtosJobAppsWindowsTests
{
    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(string ui, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, ui, ui, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, acts);
    }

    private const string Header = "\"PC_device_theme\":\"ntos\",\"PC_batteryicon\":null,\"PC_batterypercent\":null,\"PC_ntneticon\":\"sig_high.gif\","
        + "\"PC_stationtime\":\"00:12:30\",\"PC_stationdate\":\"Fri, October 23, 2566\",\"PC_programheaders\":[],\"PC_lowpower_mode\":0,\"PC_showexitprogram\":1";

    private const string Card = "\"id_name\":\"Bob Roe's ID Card (Assistant)\",\"id_rank\":\"Assistant\",\"id_owner\":\"Bob Roe\",\"access_on_card\":[\"maint_tunnels\"],"
        + "\"wildcard_slots\":{},\"id_age\":30,\"has_trim\":1,\"trim_assignment\":\"Assistant\",\"trim_access\":[\"maint_tunnels\"]";

    [Fact]
    public void TheIdWriterLogsInAndEditsTheTargetCard()
    {
        var (c, acts) = Context("NtosCard", "{" + Header + ",\"authed_user\":\"Captain's Spare ID\",\"auth_card\":{\"id_owner\":\"-----\",\"id_rank\":\"Captain\"},"
            + "\"modified_card\":{" + Card + "},\"is_holding_id\":0,"
            + "\"regions\":[{\"name\":\"General\",\"regid\":1,\"accesses\":[{\"desc\":\"Maintenance\",\"ref\":\"maint_tunnels\"},{\"desc\":\"Bar\",\"ref\":\"bar\"}]}],"
            + "\"access_flags\":{\"maint_tunnels\":1,\"bar\":1},\"wildcard_flags\":{},\"access_flag_names\":{\"1\":\"Common\"},\"show_basic\":1,"
            + "\"templates\":{\"/datum/id_trim/job/scientist\":\"Scientist\",\"/datum/id_trim/job/cook\":\"Cook\"}}");
        UiNode tree = NtosCard.Render(c);
        Assert.Equal((500.0, 670.0), (c.WindowWidth, c.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "Login: Captain's Spare ID");
        Assert.Equal("Bob Roe (Assistant)", tree.Walk().Single(n => n.Key == "card-alt-id").Text);
        tree.Walk().Single(n => n.Key == "card-login").OnClick();
        tree.Walk().Single(n => n.Key == "card-main-id").OnClick();
        tree.Walk().Single(n => n.Key == "card-print").OnClick();
        UiNode template = tree.Walk().Single(n => n.Key == "card-template");
        Assert.Equal(new[] { "Scientist", "Cook" }, template.Options);
        template.OnSelected("Cook");
        tree.Walk().Single(n => n.Key == "card-name").OnCommit("Robert Roe");
        tree.Walk().Single(n => n.Key == "card-age").OnChange(31);
        tree.Walk().Single(n => n.Key == "card-rank").OnCommit("Lab Rat");
        Assert.Equal(new[]
        {
            ("PRG_logout", (string)null), ("PRG_remove_main_id", (string)null), ("PRG_print", (string)null),
            ("PRG_template", "{\"name\":\"Cook\"}"), ("PRG_edit", "{\"name\":\"Robert Roe\"}"), ("PRG_age", "{\"id_age\":31}"), ("PRG_assign", "{\"assignment\":\"Lab Rat\"}"),
        }, acts);
        // the access list's terminate button asks first
        Assert.Contains(tree.Walk(), n => n.Key == "card-terminate" && n.Text == "Terminate Employment");
    }

    [Fact]
    public void TheIdWriterWithoutLoginAsksToLogIn()
    {
        var (c, acts) = Context("NtosCard", "{" + Header + ",\"authed_user\":null,\"auth_card\":null,\"modified_card\":null,\"is_holding_id\":1,"
            + "\"regions\":[],\"access_flags\":{},\"wildcard_flags\":{},\"access_flag_names\":{},\"show_basic\":1,\"templates\":{}}");
        UiNode tree = NtosCard.Render(c);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "Please log in to continue.");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "card-template" || n.Key == "card-terminate");
        tree.Walk().Single(n => n.Key == "card-main-id").OnClick(); // holding an ID: the slot takes it
        tree.Walk().Single(n => n.Key == "card-alt-id").OnClick();
        Assert.Equal(new[] { ("PRG_insert_main_id", (string)null), ("PRG_insert_alt_id", (string)null) }, acts);
    }

    [Fact]
    public void TheHrCoreListsTheSlotsAndItsCooldown()
    {
        var (c, acts) = Context("NtosJobManager", "{" + Header + ",\"authed\":1,\"cooldown\":12,\"prioritized\":[\"Cook\"],\"slots\":["
            + "{\"title\":\"Cook\",\"current\":1,\"total\":2,\"status_open\":1,\"status_close\":1},"
            + "{\"title\":\"Clown\",\"current\":0,\"total\":0,\"status_open\":0,\"status_close\":0}]}");
        UiNode tree = NtosJobManager.Render(c);
        Assert.Contains(tree.Walk(), n => n.Text == "On Cooldown: 12s");
        UiNode cook = tree.Walk().Single(n => n.Key == "slot:Cook");
        Assert.True(cook.Walk().Single(n => n.Key == "priority").Checkbox);
        Assert.Contains(cook.Walk(), n => n.Text == "1 / 2");
        UiNode clown = tree.Walk().Single(n => n.Key == "slot:Clown");
        Assert.True(clown.Walk().Single(n => n.Key == "priority").Disabled);
        Assert.True(clown.Walk().Single(n => n.Key == "open").Disabled);
        cook.Walk().Single(n => n.Key == "priority").OnClick();
        cook.Walk().Single(n => n.Key == "close").OnClick();
        Assert.Equal(new[] { ("PRG_priority", "{\"target\":\"Cook\"}"), ("PRG_close_job", "{\"target\":\"Cook\"}") }, acts);

        var (denied, _) = Context("NtosJobManager", "{" + Header + ",\"authed\":0,\"cooldown\":0,\"prioritized\":[],\"slots\":[]}");
        Assert.Contains(NtosJobManager.Render(denied).Walk(), n => n.Text == "Current ID does not have access permissions to change job slots.");
    }

    [Fact]
    public void TheRecordsFilterByTheSearch()
    {
        var local = new TguiLocalState();
        var (c, _) = Context("NtosRecords", "{" + Header + ",\"mode\":\"security\",\"records\":["
            + "{\"age\":30,\"fingerprint\":\"abc\",\"gender\":\"Male\",\"name\":\"Bob Roe\",\"rank\":\"Cook\",\"species\":\"Human\",\"wanted\":\"None\",\"voice\":\"x\"},"
            + "{\"age\":41,\"fingerprint\":\"def\",\"gender\":\"Female\",\"name\":\"Alice Doe\",\"rank\":\"Captain\",\"species\":\"Lizardperson\",\"wanted\":\"\",\"voice\":\"y\"}]}", local);
        UiNode tree = NtosRecords.Render(c);
        Assert.Equal(2, tree.Walk().Count(n => n.Key != null && n.Key.StartsWith("record:", StringComparison.Ordinal)));
        Assert.Contains(tree.Walk(), n => n.Text == "Criminal Status: DELETED"); // an empty status prints DELETED
        tree.Walk().Single(n => n.Key == "filter").OnInput("lizard");
        UiNode filtered = NtosRecords.Render(c);
        Assert.Single(filtered.Walk(), n => n.Key != null && n.Key.StartsWith("record:", StringComparison.Ordinal));
        Assert.Contains(filtered.Walk(), n => n.Text == "Alice Doe");

        var (med, _) = Context("NtosRecords", "{" + Header + ",\"mode\":\"medical\",\"records\":[{\"bloodtype\":\"O+\",\"ma_dis\":\"Blind<br>Deaf\",\"mi_dis\":\"None\","
            + "\"physical_status\":\"Active\",\"mental_status\":\"Stable\",\"name\":\"Bob Roe\",\"notes\":\"No notes.\"}]}");
        UiNode medTree = NtosRecords.Render(med);
        Assert.Contains(medTree.Walk(), n => n.Text == "• Blind");
        Assert.Contains(medTree.Walk(), n => n.Text == "• Deaf");
        Assert.Contains(medTree.Walk(), n => n.Text == "Notes: No notes.");
    }

    [Fact]
    public void TheStatusAppIsTheConsolesStatusControls()
    {
        var (c, acts) = Context("NtosStatus", "{" + Header + ",\"upperText\":\"HI\",\"lowerText\":\"THERE\",\"maxStatusLineLength\":40}");
        UiNode tree = NtosStatus.Render(c);
        Assert.Equal((400.0, 350.0), (c.WindowWidth, c.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Input && n.TextValue == "HI");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Input && n.TextValue == "THERE");
    }
}
