using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package VI1: the client's PanD.E.M.I.C. window (tgui's Pandemic/) without the engine: it shows the beaker, the blood's DNA and
/// antibodies, the specimen's viruses with their statistics and symptoms, and sends tg's acts (pandemic.dm:157).
/// </summary>
public class PandemicWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts, TguiLocalState Local) Render(string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        local ??= new TguiLocalState();
        var context = new TguiContext(1, "Pandemic", "PanD.E.M.I.C 2200", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, Pandemic.Render(context), acts, local);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Symptom = "{\"name\":\"Sneezing\",\"desc\":\"The virus causes irritation of the nasal cavity.\",\"stealth\":-2,\"resistance\":3,\"stage_speed\":0,"
        + "\"transmission\":4,\"level\":1,\"neutered\":0,\"threshold_desc\":{\"Transmission 9\":\"Increases sneezing range.\",\"Stealth 4\":\"The symptom remains hidden until active.\"},"
        + "\"symptom_cure\":\"Nothing\",\"cure_color\":null}";

    private const string Virus = "{\"agent\":\"advance microbes\",\"cure\":\"Spaceacillin\",\"description\":\"none\",\"index\":1,\"name\":\"Unknown\",\"spread\":\"Airborne\","
        + "\"can_rename\":1,\"is_adv\":1,\"resistance\":3,\"stage_speed\":0,\"stealth\":-2,\"severity\":1,\"transmission\":4,\"symptoms\":[" + Symptom + "]}";

    [Fact]
    public void The_window_without_a_beaker_says_so_and_only_ejects_nothing()
    {
        var (context, tree, acts, _) = Render("{\"is_ready\":1,\"has_beaker\":0,\"has_blood\":0}");
        Assert.Equal((650.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Text == "No beaker loaded.");
        Assert.True(ByKey(tree, "eject").Disabled);
        Assert.True(ByKey(tree, "empty").Disabled);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "specimen");
        Assert.Empty(acts);
    }

    [Fact]
    public void The_window_shows_the_blood_its_antibodies_and_its_virus_and_sends_the_acts()
    {
        string json = "{\"is_ready\":1,\"has_beaker\":1,\"has_blood\":1,\"beaker\":{\"volume\":20,\"capacity\":50},\"blood\":{\"dna\":\"abc123\",\"type\":\"o-\"},"
            + "\"resistances\":[{\"id\":\"cold\",\"name\":\"The Cold\"}],\"viruses\":[" + Virus + "]}";
        var (context, tree, acts, _) = Render(json);
        Assert.Contains(tree.Walk(), n => n.Label == "DNA" && n.Text == "Abc123");
        Assert.Contains(tree.Walk(), n => n.Label == "Type" && n.Text == "O-");
        Assert.Equal(20.0, ByKey(tree, "volume").Value);
        Assert.Equal(50.0, ByKey(tree, "volume").MaxValue);
        Assert.Equal("The Cold", ByKey(tree, "vaccine:The Cold:cold").Text);

        // the specimen: an advanced virus is renamable, with its statistics coloured by getColor and its symptom's modifiers
        Assert.Equal("", ByKey(tree, "rename:1").TextValue); // "Unknown" shows as an empty field
        Assert.Contains(tree.Walk(), n => n.Label == "Agent" && n.Text == "Advance microbes");
        Assert.Contains(tree.Walk(), n => n.Label == "Resistance" && n.Text == "3" && n.Color == "yellow");
        Assert.Contains(tree.Walk(), n => n.Label == "Stealth" && n.Text == "-2" && n.Color == "green");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Collapsible && n.Title == "Sneezing");
        Assert.Contains(tree.Walk(), n => n.Label == "Transmission 9" && n.Text == "Increases sneezing range.");
        Assert.Contains(tree.Walk(), n => n.Label == "Cure" && n.Text == "Nothing");

        ByKey(tree, "vaccine:The Cold:cold").OnClick();
        ByKey(tree, "culture").OnClick();
        ByKey(tree, "rename:1").OnCommit("Sniffles");
        ByKey(tree, "emptyEject").OnClick();
        Assert.Equal(new[]
        {
            ("create_vaccine_bottle", "{\"index\":\"cold\"}"),
            ("create_culture_bottle", "{\"index\":1}"),
            ("rename_disease", "{\"index\":1,\"name\":\"Sniffles\"}"),
            ("empty_eject_beaker", (string)null),
        }, acts);
    }

    [Fact]
    public void A_busy_machine_disables_its_bottles_and_two_viruses_get_tabs()
    {
        string other = Virus.Replace("\"index\":1,\"name\":\"Unknown\"", "\"index\":2,\"name\":\"Flu\"").Replace("\"can_rename\":1", "\"can_rename\":0");
        string json = "{\"is_ready\":0,\"has_beaker\":1,\"has_blood\":1,\"beaker\":{\"volume\":20,\"capacity\":50},\"blood\":{\"dna\":\"abc123\",\"type\":\"o-\"},"
            + "\"resistances\":[{\"id\":\"cold\",\"name\":\"The Cold\"}],\"viruses\":[" + Virus + "," + other + "]}";
        var (context, tree, acts, local) = Render(json);
        Assert.True(ByKey(tree, "culture").Disabled);
        Assert.True(ByKey(tree, "vaccine:The Cold:cold").Disabled);
        Assert.True(ByKey(tree, "tab:0").Selected);
        ByKey(tree, "tab:1").OnClick();
        tree = Pandemic.Render(context);
        Assert.True(ByKey(tree, "tab:1").Selected);
        Assert.Contains(tree.Walk(), n => n.Key == "name" && n.Text == "Flu" && n.Color == "bad"); // not renamable: the name in red
    }
}
