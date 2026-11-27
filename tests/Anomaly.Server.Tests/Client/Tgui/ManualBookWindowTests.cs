using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The client's DSMBook and IDCBook windows without the engine: the paginated book's table of contents, its page links and page
/// turner (each turn asking for the page sound), the DSM's entries with their aka line, the IDC's sorted by form and tinted by it.
/// </summary>
public class ManualBookWindowTests
{
    private static string Traumas(int count) => "{\"traumas\":[" + string.Join(",", Enumerable.Range(0, count).Select(i =>
        "{\"full_name\":\"Trauma " + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "\",\"scan_name\":\"" + (i % 2 == 0 ? "trauma " + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) : "other") + "\",\"desc\":\"Bad.\",\"symptoms\":\"Worse.\",\"id\":\"/datum/brain_trauma/t" + i + "\"}")) + "]}";

    [Fact]
    public void TheDsmOpensOnItsContentsAndTurnsToAnEntry()
    {
        var local = new TguiLocalState();
        var acts = new List<string>();
        var c = new TguiContext(1, "DSMBook", null, 2, UiValue.FromJson(Traumas(30)), (a, _) => acts.Add(a), local);
        UiNode tree = DSMBook.Render(c);
        Assert.Equal((765.0, 565.0, "SDSM-35"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        Assert.Equal("ntos_lightmode", c.WindowTheme);
        Assert.Equal("~ Table of Contents ~", tree.Walk().Single(n => n.Key == "title").Text);
        Assert.True(tree.Walk().Single(n => n.Key == "first").Disabled);
        // thirty entries: 17 a contents page (515 / 30), so two pages of contents; the first entry is on page 3
        UiNode link = tree.Walk().Single(n => n.Key == "goto:/datum/brain_trauma/t0");
        Assert.Equal("3", link.Text);
        Assert.Contains(tree.Walk(), n => n.Key == "scan_name" && n.Text == "(other)");
        link.OnClick();
        tree = DSMBook.Render(new TguiContext(1, "DSMBook", null, 2, UiValue.FromJson(Traumas(30)), (a, _) => acts.Add(a), local));
        Assert.Equal("~ SDSM-35 ~", tree.Walk().Single(n => n.Key == "title").Text);
        UiNode entry = tree.Walk().First(n => n.Key == "trauma");
        Assert.Equal("Trauma 00", entry.Title);
        tree.Walk().Single(n => n.Key == "next").OnClick();
        Assert.Equal(new[] { "play_flip_sound" }, acts);
    }

    [Fact]
    public void TheIdcSortsByFormAndTintsItsEntries()
    {
        string json = "{\"diseases\":["
            + "{\"name\":\"Cold\",\"desc\":\"Achoo.\",\"form\":\"Virus\",\"agent\":\"Rhino\",\"spread_by\":\"Airborne\",\"cured_by\":\"Rest\",\"illness\":\"N/A\",\"level\":\"N/A\",\"id\":\"/datum/disease/cold\"},"
            + "{\"name\":\"Cough\",\"desc\":\"Hack.\",\"form\":\"Symptom\",\"agent\":\"N/A\",\"spread_by\":\"N/A\",\"cured_by\":\"Spaceacillin\",\"illness\":\"Flu\",\"level\":\"1\",\"id\":\"/datum/symptom/cough\"},"
            + "{\"name\":\"Anxiety\",\"desc\":\"Eek.\",\"form\":\"Condition\",\"agent\":\"Fear\",\"spread_by\":\"None\",\"cured_by\":\"Calm\",\"illness\":\"N/A\",\"level\":\"N/A\",\"id\":\"/datum/disease/anxiety\"}]}";
        var local = new TguiLocalState();
        local.Set("page", 3);
        var c = new TguiContext(1, "IDCBook", null, 2, UiValue.FromJson(json), (_, _) => { }, local);
        UiNode tree = IDCBook.Render(c);
        Assert.Equal("IDC-27", c.WindowTitle);
        string[] order = tree.Walk().Where(n => n.Key == "disease").Select(n => n.Title).ToArray();
        Assert.Equal(new[] { "Anxiety", "Cough", "Cold" }, order); // Condition, Symptom, Virus
        UiNode[] bodies = tree.Walk().Where(n => n.Key == "body").ToArray();
        Assert.Equal(new[] { "lightsalmon", "lightgreen", "lightblue" }, bodies.Select(b => b.Background));
        Assert.Contains("(non-contagious)", tree.AllText());
        Assert.Contains("\"Flu\"", tree.AllText());
        Assert.Contains("(e.g. coughing, sneezing)", tree.AllText());
    }
}
