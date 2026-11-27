using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The client's Microscope window (tg's Microscope.tsx): without a dish it says so; with one it lists each cell line with its grown
/// creature's picture, its rates and its required, supplementary (good, with the growth they add) and suppressive (bad) reagents,
/// and a virus with its own note; Take Dish sends eject_petridish. It is registered under its name.
/// </summary>
public class MicroscopeWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "Microscope", "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, Microscope.Render(context), acts);
    }

    private const string Dish = "{\"has_dish\":1,\"cell_lines\":["
        + "{\"type\":\"cell line\",\"name\":\"\",\"desc\":\"Murine cells\",\"icon\":\"icons/mob/simple/animal.dmi\",\"icon_state\":\"mouse_gray\","
        + "\"consumption_rate\":0.6,\"growth_rate\":8,\"suspectibility\":4,\"requireds\":{\"Protein\":0.6},"
        + "\"supplementaries\":{\"Growth Serum\":4},\"suppressives\":{\"Heparin\":-12}},"
        + "{\"type\":\"virus\",\"name\":\"\",\"desc\":\"Basic virus\"}]}";

    [Fact]
    public void TheMicroscopeListsTheDishsCellLinesAndGivesTheDishBack()
    {
        Assert.NotNull(TguiInterfaces.Find("Microscope"));

        var (context, empty, _) = Render("{\"has_dish\":0,\"cell_lines\":[]}");
        Assert.Equal((620.0, 620.0), (context.WindowWidth, context.WindowHeight));
        string emptyText = empty.AllText();
        Assert.Contains("No Petri Dish", emptyText);
        Assert.Contains("No micro-organisms found", emptyText);
        Assert.DoesNotContain(empty.Walk(), n => n.Key == "eject");

        var (_, tree, acts) = Render(Dish);
        string text = tree.AllText();
        Assert.Contains("Petri Dish Sample", text);
        Assert.Contains("Murine cells", text);
        Assert.Contains("Consume 0.6 units of every nutrient per second to grow by 8%.", text);
        Assert.Contains("Reduced by 4% when infected with viruses.", text);
        Assert.Contains("Basic virus", text);
        Assert.Contains("Reduces growth of other cell lines when not suppressed by Spaceacillin.", text);
        UiNode sprite = tree.Walk().First(n => n.Key == "sprite");
        Assert.Equal(("icons/mob/simple/animal.dmi", "mouse_gray"), (sprite.DmIcon, sprite.DmIconState));
        UiNode serum = tree.Walk().First(n => n.Key == "Growth Serum");
        Assert.Equal(("good", "+4% growth/sec."), (serum.Color, serum.Tooltip));
        UiNode heparin = tree.Walk().First(n => n.Key == "Heparin");
        Assert.Equal(("bad", "-12% growth/sec."), (heparin.Color, heparin.Tooltip));
        Assert.Contains(tree.Walk(), n => n.Key == "viruses" && n.Icon == "viruses");

        tree.Walk().First(n => n.Key == "eject").OnClick();
        Assert.Equal(new (string, string)[] { ("eject_petridish", null) }, acts);
    }
}
