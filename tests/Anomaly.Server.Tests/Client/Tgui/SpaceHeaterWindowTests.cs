using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Engineering package E9: the client's SpaceHeater window (tg's SpaceHeater.jsx) without the engine. It reads tg's ui_data of the space
/// heater and of the improvised chem heater (the cell, the thermostat, the mode, the open hatch, the hacked beaker) the way the JSX does
/// (sizes, labels, the cell bar's colours, the temperature box's colour by the gap, the fields that only an open hatch offers) and sends
/// tg's acts: power, mode, target, eject and ejectBeaker.
/// </summary>
public class SpaceHeaterWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "SpaceHeater", "space heater", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, SpaceHeater.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Closed = "{\"open\":0,\"on\":1,\"mode\":\"auto\",\"hasPowercell\":1,\"chemHacked\":0,\"powerLevel\":80,"
        + "\"targetTemp\":20,\"minTemp\":0,\"maxTemp\":60,\"currentTemp\":19.5}";

    [Fact]
    public void AClosedHeaterShowsTheCellTheTemperaturesAndTheModeAsText()
    {
        var (context, tree, acts) = Render(Closed);
        Assert.Equal((400.0, 305.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Power", "Thermostat" }, tree.Walk().Where(n => n.Kind == UiKind.Section).Select(n => n.Title));
        Assert.Equal(new[] { "Cell", "Current Temperature", "Target Temperature", "Mode" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));

        // the buttons of the Power section: no beaker button, the cell can only be ejected from an open hatch, the power button shows the state
        UiNode eject = ByKey(tree, "eject");
        Assert.Equal(("Eject Cell", "eject", true), (eject.Text, eject.Icon, eject.Disabled));
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "eject-beaker");
        UiNode power = ByKey(tree, "power");
        Assert.Equal(("On", "power-off", true, false), (power.Text, power.Icon, power.Selected, power.Disabled));

        UiNode cell = ByKey(tree, "cell");
        Assert.Equal((UiKind.ProgressBar, 0.8, "80%"), (cell.Kind, cell.Value, cell.Text));
        Assert.Contains(cell.Ranges, r => r.Color == "good" && cell.Value >= r.From && cell.Value < r.To);

        // 19.5 against 20 degrees: good; the target and the mode are plain text while the hatch is shut
        UiNode current = ByKey(tree, "current");
        Assert.Equal(("19.5°C", "good", "18px"), (current.Text, current.Color, (string)current.FontSize));
        Assert.Equal("20°C", ByKey(tree, "target").Text);
        Assert.Equal("Auto", ByKey(tree, "mode").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.NumberInput);
        Assert.DoesNotContain(tree.Walk(), n => n.Key?.StartsWith("mode:") == true);

        power.OnClick();
        Assert.Equal(new (string, string)[] { ("power", null) }, acts);
    }

    [Fact]
    public void AnOpenHatchOffersTheTargetFieldTheThreeModesAndTheCellEject()
    {
        var (_, tree, acts) = Render("{\"open\":1,\"on\":0,\"mode\":\"heat\",\"hasPowercell\":1,\"chemHacked\":0,\"powerLevel\":20,"
            + "\"targetTemp\":25,\"minTemp\":-30,\"maxTemp\":90,\"currentTemp\":\"N/A\"}");
        Assert.False(ByKey(tree, "eject").Disabled);
        Assert.Equal(("Off", "times", false), (ByKey(tree, "power").Text, ByKey(tree, "power").Icon, ByKey(tree, "power").Selected));
        UiNode cell = ByKey(tree, "cell");
        Assert.Equal(0.2, cell.Value);
        Assert.Contains(cell.Ranges, r => r.Color == "bad" && cell.Value >= r.From && cell.Value < r.To);

        // no air to read: tg's "N/A" in the box, and NaN against the target keeps the colour good
        Assert.Equal(("N/A°C", "good"), (ByKey(tree, "current").Text, ByKey(tree, "current").Color));

        UiNode target = ByKey(tree, "target");
        Assert.Equal((UiKind.NumberInput, 25.0, "°C", -30.0, 90.0, 1.0), (target.Kind, target.Value, target.Unit, target.MinValue, target.MaxValue, target.Step));
        Assert.Equal("65px", target.Width);

        List<UiNode> modes = tree.Walk().Where(n => n.Key?.StartsWith("mode:") == true).ToList();
        Assert.Equal(new[] { "Auto", "Heat", "Cool" }, modes.Select(m => m.Text));
        Assert.Equal(new[] { "thermometer-half", "fire-alt", "fan" }, modes.Select(m => m.Icon));
        Assert.Equal(new[] { false, true, false }, modes.Select(m => m.Selected));

        target.OnChange(31);
        ByKey(tree, "mode:cool").OnClick();
        ByKey(tree, "mode:auto").OnClick();
        ByKey(tree, "eject").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("target", "{\"target\":31}"), ("mode", "{\"mode\":\"cool\"}"), ("mode", "{\"mode\":\"auto\"}"), ("eject", null),
        }, acts);
    }

    [Theory]
    [InlineData(20, 19, "good")]
    [InlineData(20, 70.5, "bad")] // more than 50 apart
    [InlineData(20, -31, "bad")]
    [InlineData(20, 41, "average")] // more than 20
    [InlineData(20, 40, "good")] // exactly 20 is not over it
    [InlineData(60, 10, "average")] // exactly 50 is not over it
    public void TheCurrentTemperatureBoxIsColouredByTheGapToTheTarget(double target, double current, string color)
    {
        var (_, tree, _) = Render("{\"open\":0,\"on\":1,\"mode\":\"auto\",\"hasPowercell\":1,\"powerLevel\":50,\"targetTemp\":" + target
            + ",\"minTemp\":0,\"maxTemp\":60,\"currentTemp\":" + current + "}");
        Assert.Equal(color, ByKey(tree, "current").Color);
    }

    [Fact]
    public void AHeaterWithoutACellShowsNoneAndCannotBeSwitched()
    {
        var (_, tree, _) = Render("{\"open\":1,\"on\":0,\"mode\":\"auto\",\"hasPowercell\":0,\"chemHacked\":0,\"targetTemp\":20,\"minTemp\":0,\"maxTemp\":60,\"currentTemp\":20}");
        UiNode cell = ByKey(tree, "cell");
        Assert.Equal((UiKind.Box, "None", "bad"), (cell.Kind, cell.Text, cell.Color));
        Assert.True(ByKey(tree, "power").Disabled);
        Assert.True(ByKey(tree, "eject").Disabled);
    }

    [Fact]
    public void TheImprovisedChemHeaterAddsTheBeakerEjectThatNeedsABeaker()
    {
        var (_, tree, acts) = Render("{\"open\":1,\"on\":1,\"mode\":\"heat\",\"hasPowercell\":1,\"chemHacked\":1,\"powerLevel\":100,\"beaker\":\"the beaker\","
            + "\"targetTemp\":40,\"minTemp\":-30,\"maxTemp\":80,\"currentTemp\":37}");
        UiNode beaker = ByKey(tree, "eject-beaker");
        Assert.Equal(("Eject beaker", "eject", false), (beaker.Text, beaker.Icon, beaker.Disabled));
        beaker.OnClick();
        Assert.Equal(new (string, string)[] { ("ejectBeaker", null) }, acts);

        var (_, empty, _) = Render("{\"open\":1,\"on\":0,\"mode\":\"auto\",\"hasPowercell\":1,\"chemHacked\":1,\"powerLevel\":100,\"beaker\":null,"
            + "\"targetTemp\":20,\"minTemp\":-30,\"maxTemp\":80,\"currentTemp\":\"N/A\"}");
        Assert.True(ByKey(empty, "eject-beaker").Disabled);
    }

    [Fact]
    public void TheWindowIsRegisteredUnderItsInterfaceName()
    {
        Assert.Equal((TguiInterface)SpaceHeater.Render, TguiInterfaces.Find("SpaceHeater"));
    }
}
