using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps SC2 (D333): the client's MedicalRecords window (tg's MedicalRecords/ folder) of the medical records console. Logged out it
/// shows tg's HealthPRO screen and its login; logged in it lists the records sorted by name and filtered by the search, previews the
/// selected one (tg's double view_record act), edits its fields, statuses and blood type, writes and deletes notes, and expunges it with
/// a confirmation; it is registered under its name.
/// </summary>
public class MedicalRecordsWindowTests
{
    private sealed class Window
    {
        public string Json;
        public TguiLocalState Local = new();
        public List<(string Action, string Params)> Acts = new();
        public TguiContext Context;

        public UiNode Render()
        {
            Context = new TguiContext(1, "MedicalRecords", "medical records console", 2, UiValue.FromJson(Json), (a, p) => Acts.Add((a, p?.ToJson())), Local);
            return MedicalRecords.Render(Context);
        }
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Statics = "\"min_age\":17,\"max_age\":85,\"physical_statuses\":[\"Active\",\"Debilitated\",\"Unconscious\",\"Deceased\"],"
        + "\"mental_statuses\":[\"Stable\",\"Watch\",\"Unstable\",\"Insane\"],\"blood_types\":[\"A+\",\"O-\"]";

    private const string Records = "\"records\":["
        + "{\"age\":30,\"blood_type\":\"A+\",\"crew_ref\":\"[0x2]\",\"dna\":\"FEED\",\"gender\":\"Male\",\"major_disabilities\":\"Blindness\","
        + "\"minor_disabilities\":\"Nearsighted<br>Lisp\",\"physical_status\":\"Active\",\"mental_status\":\"Stable\",\"name\":\"zed Zulu\","
        + "\"notes\":[{\"author\":\"Doc\",\"content\":\"Checked up\",\"note_ref\":\"[0x9]\",\"time\":\"12:00:00\"}],\"quirk_notes\":\"\","
        + "\"rank\":\"Assistant\",\"species\":\"Human\",\"trim\":\"Assistant\",\"cause_of_death\":\"\"},"
        + "{\"age\":40,\"blood_type\":\"O-\",\"crew_ref\":\"[0x3]\",\"dna\":\"BEEF\",\"gender\":\"Female\",\"major_disabilities\":null,"
        + "\"minor_disabilities\":null,\"physical_status\":\"Deceased\",\"mental_status\":\"Watch\",\"name\":\"Alice Able\",\"notes\":[],"
        + "\"quirk_notes\":null,\"rank\":\"Botanist\",\"species\":\"Lizard\",\"trim\":\"Botanist\",\"cause_of_death\":\"Bees\"}]";

    private static Window LoggedIn(int stationZ = 1) => new() { Json = "{\"authenticated\":1,\"assigned_view\":\"preview_bob_[0x1]_records\",\"station_z\":" + stationZ + "," + Statics + "," + Records + "}" };

    [Fact]
    public void LoggedOutTheWindowShowsTheHealthProScreenAndItsLogin()
    {
        var w = new Window { Json = "{\"authenticated\":0}" };
        UiNode tree = w.Render();
        Assert.Equal((750.0, 550.0, "Medical Records"), (w.Context.WindowWidth, w.Context.WindowHeight, w.Context.WindowTitle));
        Assert.Contains("Nanotrasen HealthPRO", tree.AllText());
        Assert.Contains("You are not logged in.", tree.AllText());
        Assert.Equal("staff-snake", tree.Walk().Single(n => n.Kind == UiKind.Icon).Icon);
        ByKey(tree, "login").OnClick();
        Assert.Equal(new (string, string)[] { ("login", null) }, w.Acts);
    }

    [Fact]
    public void TheRecordsAreSortedByNameFilteredByTheSearchAndTheFirstPickAsksForThePreviewTwice()
    {
        var w = LoggedIn();
        UiNode tree = w.Render();
        Assert.Equal(new[] { "Alice Able", "zed Zulu" }, tree.Walk().Where(n => n.Kind == UiKind.Tab && n.Key.StartsWith("record:")).Select(n => n.Text));
        Assert.Equal("No record selected.", ByKey(tree, "none").Text);

        ByKey(tree, "search").OnInput("beef"); // the DNA
        tree = w.Render();
        Assert.Equal(new[] { "Alice Able" }, tree.Walk().Where(n => n.Kind == UiKind.Tab && n.Key.StartsWith("record:")).Select(n => n.Text));
        ByKey(tree, "search").OnInput("nobody");
        tree = w.Render();
        Assert.Equal("No match. Refine your search.", ByKey(tree, "empty").Text);
        ByKey(tree, "search").OnInput("");
        tree = w.Render();

        ByKey(tree, "record:1").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("view_record", "{\"assigned_view\":\"preview_bob_[0x1]_records\",\"crew_ref\":\"[0x2]\"}"),
            ("view_record", "{\"assigned_view\":\"preview_bob_[0x1]_records\",\"crew_ref\":\"[0x2]\"}"),
        }, w.Acts);
        tree = w.Render();
        Assert.True(ByKey(tree, "record:1").Selected);
        UiNode map = tree.Walk().Single(n => n.Kind == UiKind.ByondUi);
        Assert.Equal(("preview_bob_[0x1]_records", true), (map.MapRef, map.MapPicture));
        Assert.Equal("zed Zulu", ByKey(tree, "record").Title);
        Assert.Contains("• Nearsighted", tree.AllText());
        Assert.Contains("• Lisp", tree.AllText());
        Assert.Contains("• Blindness", tree.AllText());

        // another record: one act
        w.Acts.Clear();
        ByKey(tree, "record:0").OnClick();
        Assert.Single(w.Acts);
    }

    [Fact]
    public void TheSelectedRecordsFieldsStatusesBloodTypeAndNotesSendTgsActs()
    {
        var w = LoggedIn();
        w.Local.Set("medicalRecord", "[0x3]");
        UiNode tree = w.Render();
        Assert.Equal("Alice Able", ByKey(tree, "record").Title);
        Assert.Equal("Bees", ByKey(tree, "cause:[0x3]").TextValue); // deceased: the cause of death

        ByKey(tree, "physical_statuses:0").OnClick();
        ByKey(tree, "mental_statuses:3").OnClick();
        Assert.Equal(("red", "grey"), (ByKey(tree, "physical_statuses:3").Color, ByKey(tree, "physical_statuses:0").Color));
        Assert.Equal("D", ByKey(tree, "physical_statuses:3").Text);
        ByKey(tree, "blood_type").OnSelected("O-");
        ByKey(tree, "cause:[0x3]").OnInput("Many bees");
        Assert.Equal(new (string, string)[]
        {
            ("set_physical_status", "{\"crew_ref\":\"[0x3]\",\"physical_status\":\"Active\"}"),
            ("set_mental_status", "{\"crew_ref\":\"[0x3]\",\"mental_status\":\"Insane\"}"),
            ("set_blood_type", "{\"crew_ref\":\"[0x3]\",\"blood_type\":\"O-\"}"),
            ("set_cause_of_death", "{\"crew_ref\":\"[0x3]\",\"cause\":\"Many bees\"}"),
        }, w.Acts);
        w.Acts.Clear();

        // an editable field: its text is a button, the field an input sent on Enter
        UiNode name = ByKey(tree, "editable:name:[0x3]");
        name.Walk().Single(n => n.Key == "edit").OnClick();
        tree = w.Render();
        UiNode input = ByKey(tree, "editable:name:[0x3]");
        Assert.Equal((UiKind.Input, "Alice Able"), (input.Kind, input.TextValue));
        w.Local.Set("input:editable:name:[0x3]", "Alice Baker");
        input.OnEnter();
        tree = w.Render();
        Assert.Equal(UiKind.Stack, ByKey(tree, "editable:name:[0x3]").Kind);
        ByKey(tree, "editable:species:[0x3]").Walk().Single(n => n.Key == "clear").OnClick();
        UiNode age = ByKey(tree, "age:[0x3]");
        Assert.Equal((UiKind.NumberInput, 40.0, 17.0, 85.0), (age.Kind, age.Value, age.MinValue, age.MaxValue));
        age.OnChange(41);
        age.OnEnter();
        Assert.Equal(new (string, string)[]
        {
            ("edit_field", "{\"field\":\"name\",\"ref\":\"[0x3]\",\"value\":\"Alice Baker\"}"),
            ("edit_field", "{\"field\":\"species\",\"ref\":\"[0x3]\",\"value\":\"\"}"),
            ("edit_field", "{\"field\":\"age\",\"ref\":\"[0x3]\",\"value\":41}"),
        }, w.Acts);
        w.Acts.Clear();

        // a note
        ByKey(tree, "new").OnClick();
        tree = w.Render();
        UiNode text = ByKey(tree, "noteText");
        Assert.Equal(UiKind.TextArea, text.Kind);
        w.Local.Set("input:noteText", "Allergic to bees");
        text.OnEnter();
        tree = w.Render();
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "noteText");
        Assert.Equal(new (string, string)[] { ("add_note", "{\"crew_ref\":\"[0x3]\",\"content\":\"Allergic to bees\"}") }, w.Acts);
        w.Acts.Clear();

        // the delete asks for a confirmation
        ByKey(tree, "delete").OnClick();
        Assert.Empty(w.Acts);
        tree = w.Render();
        Assert.Equal("Confirm?", ByKey(tree, "delete").Text);
        ByKey(tree, "delete").OnClick();
        Assert.Equal(new (string, string)[] { ("expunge_record", "{\"crew_ref\":\"[0x3]\"}") }, w.Acts);
    }

    [Fact]
    public void ANoteIsShownAndDeletedAndOffTheStationThePurgeAndDeleteAreLocked()
    {
        var w = LoggedIn(stationZ: 0);
        w.Local.Set("medicalRecord", "[0x2]");
        UiNode tree = w.Render();
        Assert.True(ByKey(tree, "purge").Disabled);
        Assert.True(ByKey(tree, "delete").Disabled);
        Assert.True(ByKey(tree, "create").Disabled);

        ByKey(tree, "note:0").OnClick();
        tree = w.Render();
        Assert.Contains("Checked up", tree.AllText());
        Assert.Contains("Doc", tree.AllText());
        ByKey(tree, "deleteNote").OnClick();
        Assert.Equal(new (string, string)[] { ("delete_note", "{\"crew_ref\":\"[0x2]\",\"note_ref\":\"[0x9]\"}") }, w.Acts);
        tree = w.Render();
        Assert.DoesNotContain("Checked up", tree.AllText());

        ByKey(tree, "logout").OnClick();
        Assert.Equal("logout", w.Acts[^1].Action);
    }

    [Fact]
    public void TheMedicalRecordsWindowIsRegisteredUnderItsName()
    {
        Assert.Equal((TguiInterface)MedicalRecords.Render, TguiInterfaces.Find("MedicalRecords"));
    }
}
