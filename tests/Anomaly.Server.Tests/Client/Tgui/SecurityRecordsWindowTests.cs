using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps SC2 (D333): the client's SecurityRecords window (tg's SecurityRecords/ folder) of the security records console. Logged out it
/// shows tg's SecurityHUB screen; logged in it lists the records in their wanted status's colour, sets the status (Arrest only with a
/// valid crime), lists the crimes and citations (edited and voided by their author), writes a new crime or citation, and prints a
/// rapsheet or a poster with tg's default header and description; it is registered under its name.
/// </summary>
public class SecurityRecordsWindowTests
{
    private sealed class Window
    {
        public string Json;
        public TguiLocalState Local = new();
        public List<(string Action, string Params)> Acts = new();
        public TguiContext Context;

        public UiNode Render()
        {
            Context = new TguiContext(1, "SecurityRecords", "security records console", 2, UiValue.FromJson(Json), (a, p) => Acts.Add((a, p?.ToJson())), Local);
            return SecurityRecords.Render(Context);
        }
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Data = "{\"authenticated\":1,\"assigned_view\":\"preview_bob_[0x1]_records\",\"station_z\":1,"
        + "\"available_statuses\":[\"Arrest\",\"Discharged\",\"None\",\"Parole\",\"Incarcerated\",\"Suspected\"],"
        + "\"current_user\":\"Bob\",\"higher_access\":0,\"min_age\":17,\"max_age\":85,\"records\":["
        + "{\"age\":30,\"crew_ref\":\"[0x2]\",\"fingerprint\":\"abc\",\"gender\":\"Female\",\"name\":\"Carol\",\"note\":\"Watch her\","
        + "\"rank\":\"Assistant\",\"species\":\"Human\",\"trim\":\"Assistant\",\"wanted_status\":\"Arrest\","
        + "\"citations\":[{\"author\":\"Bob\",\"crime_ref\":\"[0x7]\",\"details\":\"Dropped a peel\",\"fine\":300,\"name\":\"Littering on the station\",\"paid\":50,\"time\":\"12:00\",\"valid\":1,\"voider\":null}],"
        + "\"crimes\":[{\"author\":\"Bob\",\"crime_ref\":\"[0x5]\",\"details\":\"Took it\",\"name\":\"Theft\",\"time\":\"12:01\",\"valid\":1,\"voider\":null},"
        + "{\"author\":\"Eve\",\"crime_ref\":\"[0x6]\",\"details\":\"Hit someone\",\"name\":\"Assault\",\"time\":\"12:02\",\"valid\":0,\"voider\":\"Eve\"}]},"
        + "{\"age\":40,\"crew_ref\":\"[0x3]\",\"fingerprint\":\"def\",\"gender\":\"Male\",\"name\":\"Alice\",\"note\":null,\"rank\":\"Botanist\","
        + "\"species\":\"Lizard\",\"trim\":\"Botanist\",\"wanted_status\":\"None\",\"citations\":[],\"crimes\":[]}]}";

    private static Window Selected(string crewRef)
    {
        var w = new Window { Json = Data };
        w.Local.Set("securityRecord", crewRef);
        return w;
    }

    [Fact]
    public void LoggedOutTheWindowShowsTheSecurityHubScreenAndItsLogin()
    {
        var w = new Window { Json = "{\"authenticated\":0}" };
        UiNode tree = w.Render();
        Assert.Equal((750.0, 550.0, "Security Records"), (w.Context.WindowWidth, w.Context.WindowHeight, w.Context.WindowTitle));
        Assert.Contains("Nanotrasen SecurityHUB", tree.AllText());
        Assert.Equal(("exclamation-triangle", "average"), tree.Walk().Where(n => n.Kind == UiKind.Icon).Select(n => (n.Icon, n.Color)).Single());
        ByKey(tree, "login").OnClick();
        Assert.Equal(new (string, string)[] { ("login", null) }, w.Acts);
    }

    [Fact]
    public void TheRecordsAreSortedAndColouredByTheirStatusAndTheStatusButtonsSendTgsActs()
    {
        var w = new Window { Json = Data };
        UiNode tree = w.Render();
        var tabs = tree.Walk().Where(n => n.Kind == UiKind.Tab && n.Key.StartsWith("record:")).ToList();
        Assert.Equal(new (string, string)[] { ("Alice", null), ("Carol", "bad") }, tabs.Select(t => (t.Text, t.Color)));
        Assert.True(ByKey(tree, "purge").Disabled); // no armoury access
        Assert.Equal("Nothing selected.", ByKey(tree, "none").Text);
        ByKey(tree, "search").OnInput("ABC"); // the fingerprint
        tree = w.Render();
        Assert.Equal(new[] { "Carol" }, tree.Walk().Where(n => n.Kind == UiKind.Tab && n.Key.StartsWith("record:")).Select(n => n.Text));
        ByKey(tree, "record:0").OnClick();
        Assert.Equal(2, w.Acts.Count(a => a.Action == "view_record")); // the preview's first pick
        w.Acts.Clear();

        tree = w.Render();
        Assert.Equal("Carol", ByKey(tree, "statusSection").Title);
        Assert.False(ByKey(tree, "status:0").Disabled); // a valid crime: Arrest may be set
        Assert.Equal(("check", "bad", "A"), (ByKey(tree, "status:0").Icon, ByKey(tree, "status:0").Color, ByKey(tree, "status:0").Text));
        ByKey(tree, "status:4").OnClick();
        Assert.Equal(new (string, string)[] { ("set_wanted", "{\"crew_ref\":\"[0x2]\",\"status\":\"Incarcerated\"}") }, w.Acts);

        // no crimes: no Arrest
        var alice = Selected("[0x3]");
        tree = alice.Render();
        Assert.True(ByKey(tree, "status:0").Disabled);
        Assert.Equal("grey", ByKey(tree, "status:0").Color);
        Assert.Contains("(none)", ByKey(tree, "editable:security_note:[0x3]").AllText());
    }

    [Fact]
    public void TheCrimesAreListedEditedAndVoidedByTheirAuthorAndTheCitationsShowTheirFine()
    {
        var w = Selected("[0x2]");
        UiNode tree = w.Render();
        Assert.Equal(("Crimes: 2", "Citations: 1"), (ByKey(tree, "crimes").Text, ByKey(tree, "citations").Text));
        UiNode theft = ByKey(tree, "crime:[0x5]");
        UiNode assault = ByKey(tree, "crime:[0x6]");
        Assert.Equal(("Theft", ""), (theft.Title, theft.Color));
        Assert.Equal(("Assault", "grey"), (assault.Title, assault.Color));
        Assert.Contains("Void", assault.AllText());
        Assert.Contains("Voided by", assault.AllText());
        Assert.True(assault.Walk().Single(n => n.Key == "edit").Disabled);
        Assert.False(theft.Walk().Single(n => n.Key == "edit").Disabled);

        // the author renames the crime
        theft.Walk().Single(n => n.Key == "edit").OnClick();
        tree = w.Render();
        w.Local.Set("input:crimeName:[0x5]", "Grand theft");
        ByKey(tree, "crimeName:[0x5]").OnEnter();
        tree = w.Render();
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "crimeName:[0x5]");
        ByKey(tree, "invalidate:[0x5]").OnClick();
        tree = w.Render();
        ByKey(tree, "invalidate:[0x5]").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("edit_crime", "{\"crew_ref\":\"[0x2]\",\"crime_ref\":\"[0x5]\",\"name\":\"Grand theft\"}"),
            ("invalidate_crime", "{\"crew_ref\":\"[0x2]\",\"crime_ref\":\"[0x5]\"}"),
        }, w.Acts);

        // the citations: the fine due in the title
        ByKey(tree, "citations").OnClick();
        tree = w.Render();
        UiNode littering = ByKey(tree, "crime:[0x7]");
        Assert.Equal(("Littering on the s: 300 cr", "average"), (littering.Title, littering.Color));
        Assert.Contains("300cr", littering.AllText());
        Assert.Contains("50cr", littering.AllText());
    }

    [Fact]
    public void ANewCrimeNeedsAThreeLetterNameAndAFineMakesItACitation()
    {
        var w = Selected("[0x2]");
        w.Local.Set("selectedTab", 2);
        UiNode tree = w.Render();
        Assert.True(ByKey(tree, "createCrime").Disabled);
        w.Local.Set("input:crimeName", "Arson");
        w.Local.Set("input:crimeDetails", "Lit the bar");
        tree = w.Render();
        Assert.False(ByKey(tree, "createCrime").Disabled);
        UiNode fine = ByKey(tree, "crimeFine");
        Assert.Equal((0.0, 1000.0), (fine.MinValue, fine.MaxValue));
        fine.OnChange(50);
        tree = w.Render();
        ByKey(tree, "createCrime").OnClick();
        tree = w.Render();
        ByKey(tree, "createCrime").OnClick();
        Assert.Equal(new (string, string)[] { ("add_crime", "{\"crew_ref\":\"[0x2]\",\"details\":\"Lit the bar\",\"fine\":50,\"name\":\"Arson\"}") }, w.Acts);
        Assert.Equal(1, w.Local.Get("selectedTab", -1)); // a fine: on to the citations
        Assert.Equal("", w.Local.Get("input:crimeName", "x"));
    }

    [Fact]
    public void ThePrintFormSwapsTgsDefaultsAndSendsTheSheet()
    {
        var w = Selected("[0x2]");
        UiNode tree = w.Render();
        ByKey(tree, "print").OnClick();
        tree = w.Render();
        Assert.Equal("Print Record", ByKey(tree, "print").Title);
        Assert.True(ByKey(tree, "missing").Selected);
        Assert.Equal("Carol", ByKey(tree, "printAlias").TextValue);

        ByKey(tree, "resetHeader").OnClick();
        ByKey(tree, "resetDescription").OnClick();
        tree = w.Render();
        Assert.Equal("MISSING", ByKey(tree, "printHeader").TextValue);
        ByKey(tree, "wanted").OnClick(); // the defaults follow the type
        tree = w.Render();
        Assert.Equal("WANTED", ByKey(tree, "printHeader").TextValue);
        Assert.StartsWith("A poster declaring Carol to be a wanted criminal", ByKey(tree, "printDescription").TextValue);
        ByKey(tree, "doPrint").OnClick();
        Assert.Equal("print_record", w.Acts.Single().Action);
        Assert.Contains("\"head\":\"WANTED\"", w.Acts.Single().Params);
        Assert.Contains("\"type\":\"wanted\"", w.Acts.Single().Params);
        Assert.Contains("\"alias\":\"Carol\"", w.Acts.Single().Params);
        tree = w.Render();
        Assert.Equal("Print", ByKey(tree, "print").Text); // closed
    }

    [Fact]
    public void AnInnocentRecordPrintsOnlyAMissingPoster()
    {
        var w = Selected("[0x3]");
        w.Local.Set("printOpen", true);
        UiNode tree = w.Render();
        Assert.True(ByKey(tree, "rapsheet").Disabled);
        Assert.True(ByKey(tree, "wanted").Disabled);
        Assert.EndsWith("(Requires crimes)", ByKey(tree, "wanted").Tooltip);
        ByKey(tree, "cancel").OnClick();
        Assert.False(w.Local.Get("printOpen", true));
    }

    [Fact]
    public void TheSecurityRecordsWindowIsRegisteredUnderItsName()
    {
        Assert.Equal((TguiInterface)SecurityRecords.Render, TguiInterfaces.Find("SecurityRecords"));
    }
}
