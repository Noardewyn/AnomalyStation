using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps SG1: the client's ScannerGate window (tg's ScannerGate.jsx) shows the lock notice, the mode menu and each mode's settings
/// and sends their acts; the HypnoChair window (HypnoChair.jsx) shows the occupant, the door, the trigger phrase and the
/// interrogation. Both are registered under their names.
/// </summary>
public class ScannerGateWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Species = "\"available_species\":[{\"specie_name\":\"Human\",\"specie_id\":\"human\"},{\"specie_name\":\"Lizardperson\",\"specie_id\":\"lizard\"}]";

    private static string Gate(string mode, int locked = 0, int reverse = 0) =>
        "{\"locked\":" + locked + ",\"scan_mode\":\"" + mode + "\",\"reverse\":" + reverse + ",\"disease_threshold\":\"Minor\",\"target_species_id\":\"lizard\","
        + "\"target_nutrition\":150,\"target_zombie\":0," + Species + "}";

    [Fact]
    public void TheScannerGateWindowPicksAModeAndSetsIt()
    {
        Assert.NotNull(TguiInterfaces.Find("ScannerGate"));
        var (context, off, acts) = Render("ScannerGate", ScannerGate.Render, Gate("Off"));
        Assert.Equal((400.0, 300.0), (context.WindowWidth, context.WindowHeight));
        string text = off.AllText();
        Assert.Contains("Swipe an ID card to lock this interface.", text);
        Assert.Contains("Scanner Mode: Off", text);
        Assert.Contains("Select a scanning mode below.", text);
        ByKey(off, "mode:Guns").OnClick();
        Assert.Equal(("set_mode", "{\"new_mode\":\"Guns\"}"), acts.Single());

        var (_, guns, gunActs) = Render("ScannerGate", ScannerGate.Render, Gate("Guns", reverse: 1));
        Assert.Contains("Trigger if the person scanned does not have any guns.", guns.AllText());
        Assert.Equal("Inverted", ByKey(guns, "toggle_reverse").Text);
        ByKey(guns, "toggle_reverse").OnClick();
        ByKey(guns, "back").OnClick();
        Assert.Equal(new (string, string)[] { ("toggle_reverse", null), ("set_mode", "{\"new_mode\":\"Off\"}") }, gunActs);

        var (_, species, speciesActs) = Render("ScannerGate", ScannerGate.Render, Gate("Species"));
        Assert.Contains("Trigger if the person scanned is  of the Lizardperson species.", species.AllText());
        Assert.True(ByKey(species, "species:lizard").Checkbox);
        ByKey(species, "species:human").OnClick();
        Assert.Equal(("set_target_species", "{\"new_species_id\":\"human\"}"), speciesActs.Single());

        var (_, nutrition, _) = Render("ScannerGate", ScannerGate.Render, Gate("Nutrition"));
        Assert.Contains("the Starving nutrition level.", nutrition.AllText());
        Assert.True(ByKey(nutrition, "nutrition:Starving").Checkbox);

        var (_, disease, _) = Render("ScannerGate", ScannerGate.Render, Gate("Disease"));
        Assert.Contains("a disease equal or worse than Minor.", disease.AllText());
        Assert.True(ByKey(disease, "threshold:Minor").Checkbox);

        // locked: only the notice
        var (_, locked, _) = Render("ScannerGate", ScannerGate.Render, Gate("Guns", locked: 1));
        Assert.Contains("Swipe an ID card to unlock this interface.", locked.AllText());
        Assert.DoesNotContain(locked.Walk(), n => n.Key == "mode");
    }

    [Fact]
    public void TheHypnoChairWindowShowsItsOccupantAndRunsTheInterrogation()
    {
        Assert.NotNull(TguiInterfaces.Find("HypnoChair"));
        var (context, tree, acts) = Render("HypnoChair", HypnoChair.Render,
            "{\"occupied\":1,\"open\":0,\"interrogating\":1,\"occupant\":{\"name\":\"John Doe\",\"stat\":1},\"trigger\":\"banana\"}");
        Assert.Equal((375.0, 480.0), (context.WindowWidth, context.WindowHeight));
        string text = tree.AllText();
        Assert.Contains("John Doe", text);
        Assert.Contains("Unconscious", text);
        Assert.Contains("Closed", text);
        Assert.Contains("Interrupt Interrogation", text);
        Assert.Equal("banana", ByKey(tree, "set_phrase").TextValue);
        Assert.True(ByKey(tree, "cog").Spin);
        ByKey(tree, "door").OnClick();
        ByKey(tree, "set_phrase").OnCommit("apple");
        ByKey(tree, "interrogate").OnClick();
        Assert.Equal(new (string, string)[] { ("door", null), ("set_phrase", "{\"phrase\":\"apple\"}"), ("interrogate", null) }, acts);

        var (_, empty, _) = Render("HypnoChair", HypnoChair.Render, "{\"occupied\":0,\"open\":1,\"interrogating\":0,\"occupant\":[],\"trigger\":\"\"}");
        string emptyText = empty.AllText();
        Assert.Contains("No Occupant", emptyText);
        Assert.Contains("Begin Enhanced Interrogation", emptyText);
        Assert.DoesNotContain(empty.Walk(), n => n.Key == "status");
    }
}
