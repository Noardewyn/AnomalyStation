using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps TP1: the client's Teleporter window (tg's Teleporter.jsx) shows the missing links, or the regime, the target and the
/// calibration with their acts; the BluespaceLocator window (BluespaceLocator.tsx) lists the tracked implants, searches them and
/// points the selected one's arrow by its direction, coloured by its distance. Both are registered under their names.
/// </summary>
public class TeleporterWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheTeleporterWindowShowsItsLinksTargetAndCalibration()
    {
        Assert.NotNull(TguiInterfaces.Find("Teleporter"));
        var (_, unlinked, _) = Render("Teleporter", Teleporter.Render, "{\"power_station\":0,\"teleporter_hub\":0}");
        Assert.Contains("No power station linked.", unlinked.AllText());
        var (_, nohub, _) = Render("Teleporter", Teleporter.Render, "{\"power_station\":1,\"teleporter_hub\":0}");
        Assert.Contains("No hub linked.", nohub.AllText());

        var (context, tree, acts) = Render("Teleporter", Teleporter.Render,
            "{\"power_station\":1,\"teleporter_hub\":1,\"regime_set\":\"Teleporter\",\"target\":\"Bridge \",\"calibrating\":0,\"calibrated\":0}");
        Assert.Equal((360.0, 130.0), (context.WindowWidth, context.WindowHeight));
        string text = tree.AllText();
        Assert.Contains("Sub-Optimal", text);
        Assert.Equal("Teleporter", ByKey(tree, "regimeset").Text);
        Assert.Equal("Bridge ", ByKey(tree, "settarget").Text);
        ByKey(tree, "regimeset").OnClick();
        ByKey(tree, "settarget").OnClick();
        ByKey(tree, "calibrate").OnClick();
        Assert.Equal(new (string, string)[] { ("regimeset", null), ("settarget", null), ("calibrate", null) }, acts);

        var (_, calibrating, _) = Render("Teleporter", Teleporter.Render,
            "{\"power_station\":1,\"teleporter_hub\":1,\"regime_set\":\"Gate\",\"target\":\"None\",\"calibrating\":1,\"calibrated\":0}");
        Assert.Contains("In Progress", calibrating.AllText());
        var (_, optimal, _) = Render("Teleporter", Teleporter.Render,
            "{\"power_station\":1,\"teleporter_hub\":1,\"regime_set\":\"Gate\",\"target\":\"None\",\"calibrating\":0,\"calibrated\":1}");
        Assert.Contains("Optimal", optimal.AllText());
    }

    [Fact]
    public void TheBluespaceLocatorListsSearchesAndPointsAtTheImplants()
    {
        Assert.NotNull(TguiInterfaces.Find("BluespaceLocator"));
        var (_, none, _) = Render("BluespaceLocator", BluespaceLocator.Render, "{\"trackingrange\":35,\"trackimplants\":[]}");
        Assert.Contains("Bluespace signatures not detected", none.AllText());

        const string json = "{\"trackingrange\":35,\"trackimplants\":[{\"name\":\"John Doe\",\"direction\":\"east\",\"distance\":2},"
            + "{\"name\":\"Jane Roe\",\"direction\":\"southwest\",\"distance\":30}]}";
        var local = new TguiLocalState();
        var (context, tree, _) = Render("BluespaceLocator", BluespaceLocator.Render, json, local);
        Assert.Equal((550.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(90.0, ByKey(tree, "arrow").Rotation);
        Assert.Equal("green", ByKey(tree, "arrow").Color);
        Assert.Contains("John Doe", ByKey(tree, "name").Text);

        // pick Jane: the arrow turns southwest and goes red (beyond two thirds of the range)
        ByKey(tree, "select:Jane Roe").OnClick();
        var (_, jane, _) = Render("BluespaceLocator", BluespaceLocator.Render, json, local);
        Assert.Equal(225.0, ByKey(jane, "arrow").Rotation);
        Assert.Equal("red", ByKey(jane, "arrow").Color);
        Assert.Equal("Distance: 30", ByKey(jane, "distance").Text);

        // a search that matches nobody
        ByKey(jane, "search").OnInput("zzz");
        var (_, nomatch, _) = Render("BluespaceLocator", BluespaceLocator.Render, json, local);
        Assert.Contains("No targets matching search criteria", nomatch.AllText());
    }
}
