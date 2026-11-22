using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Package E3: the client's PortablePump, PortableScrubber, PipeScrubber and Tank windows (tg's PortablePump.tsx, PortableScrubber.tsx,
/// PipeScrubber.tsx, Tank.jsx and common/PortableAtmos.tsx) without the engine. Each reads tg's ui_data the way the TSX does (the status
/// and holding tank sections, the pressure inputs and gauges, the filter buttons) and sends tg's acts; all four are registered under their
/// interface names.
/// </summary>
public class PortableAtmosWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private static List<string> Titles(UiNode tree) => tree.Walk().Where(n => n.Kind == UiKind.Section && n.Title != null).Select(n => n.Title).ToList();

    private static string Row(UiNode tree, string label) => tree.Walk().Single(n => n.Kind == UiKind.LabeledItem && n.Label == label).AllText();

    private const string FilterTypes = "[{\"gasId\":\"co2\",\"gasName\":\"Carbon Dioxide\",\"enabled\":1},{\"gasId\":\"o2\",\"gasName\":\"Oxygen\",\"enabled\":0},{\"gasId\":\"vapor\",\"gasName\":\"Some Vapor\",\"enabled\":0}]";

    private static string PumpJson(string on, string direction, string connected, string target, string holding, string crystal = "0", string suppressed = "0") =>
        "{\"on\":" + on + ",\"direction\":" + direction + ",\"connected\":" + connected + ",\"pressure\":2,\"targetPressure\":" + target +
        ",\"defaultPressure\":101,\"minPressure\":10,\"maxPressure\":2533,\"hasHypernobCrystal\":" + crystal + ",\"reactionSuppressionEnabled\":" + suppressed +
        ",\"holding\":" + holding + "}";

    [Fact]
    public void ThePortablePumpWindowShowsTheStatusTheHoldingTankAndThePumpingControlsWithTgsActs()
    {
        var (context, tree, acts) = Render(PortablePump.Render, "PortablePump", PumpJson("1", "1", "0", "101", "null"));
        Assert.Equal((300.0, 340.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Status", "Holding Tank", "Pumping" }, Titles(tree));

        // the status section: the power button in its title row, the pressure, the port; no crystal, so no reaction suppression row
        Assert.Equal(("On", "power-off", true), (ByKey(tree, "power").Text, ByKey(tree, "power").Icon, ByKey(tree, "power").Selected));
        Assert.Equal("2 kPa", ByKey(tree, "pressure-readout").Text);
        Assert.Equal(("Not Connected", "average"), (ByKey(tree, "port").Text, ByKey(tree, "port").Color));
        Assert.DoesNotContain(tree.Walk(), n => n.Label == "Reaction Suppression");

        // no tank: the Eject button is disabled and the section says so
        Assert.True(ByKey(tree, "eject").Disabled);
        Assert.Equal(("No holding tank", "average"), (ByKey(tree, "no-holding").Text, ByKey(tree, "no-holding").Color));

        // the pumping section: the direction reads "Area -> Pump" (into the pump), the output is a kPa number input, the presets
        Assert.Equal("Area → Pump", ByKey(tree, "direction").Text);
        Assert.Null(ByKey(tree, "direction").Color);
        UiNode input = ByKey(tree, "pressure");
        Assert.Equal((UiKind.NumberInput, 101.0, "kPa", 10.0, 2533.0, 10.0), (input.Kind, input.Value, input.Unit, input.MinValue, input.MaxValue, input.Step));
        Assert.Equal("75px", input.Width);
        Assert.Equal(new[] { "Output", "Presets" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem && n.Label is "Output" or "Presets").Select(n => n.Label));
        Assert.Equal(("minus", "sync", "plus"), (ByKey(tree, "min").Icon, ByKey(tree, "reset").Icon, ByKey(tree, "max").Icon));
        Assert.True(ByKey(tree, "reset").Disabled); // the target is the default

        ByKey(tree, "power").OnClick();
        ByKey(tree, "direction").OnClick();
        input.OnChange(300);
        ByKey(tree, "min").OnClick();
        ByKey(tree, "reset").OnClick();
        ByKey(tree, "max").OnClick();
        ByKey(tree, "eject").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("power", null), ("direction", null), ("pressure", "{\"pressure\":300}"), ("pressure", "{\"pressure\":\"min\"}"),
            ("pressure", "{\"pressure\":\"reset\"}"), ("pressure", "{\"pressure\":\"max\"}"), ("eject", null),
        }, acts);
    }

    [Fact]
    public void ThePortablePumpWindowNamesTheEndsOfItsFlowAndShowsTheTankItHolds()
    {
        // pumping out of the pump into the area, nothing held: the caution colour of the button (the pump may fill the room)
        var (_, area, _) = Render(PortablePump.Render, "PortablePump", PumpJson("0", "0", "0", "10", "null"));
        Assert.Equal(("Pump → Area", "caution"), (ByKey(area, "direction").Text, ByKey(area, "direction").Color));
        Assert.True(ByKey(area, "min").Disabled);
        Assert.False(ByKey(area, "max").Disabled);
        Assert.Equal(("Off", "times", false), (ByKey(area, "power").Text, ByKey(area, "power").Icon, ByKey(area, "power").Selected));

        // connected to a port with a tank held: the ends read Port and Tank, the holding tank section lists the tank
        var (_, held, _) = Render(PortablePump.Render, "PortablePump", PumpJson("1", "0", "1", "2533", "{\"name\":\"oxygen tank\",\"pressure\":1013}"));
        Assert.Equal("Port → Tank", ByKey(held, "direction").Text);
        Assert.Null(ByKey(held, "direction").Color);
        Assert.Equal(("Connected", "good"), (ByKey(held, "port").Text, ByKey(held, "port").Color));
        Assert.False(ByKey(held, "eject").Disabled);
        Assert.Equal("oxygen tank", ByKey(held, "holding-name").Text);
        Assert.Equal("1013 kPa", ByKey(held, "holding-pressure").Text);
        Assert.DoesNotContain(held.Walk(), n => n.Key == "no-holding");
        Assert.True(ByKey(held, "max").Disabled);

        var (_, toPort, _) = Render(PortablePump.Render, "PortablePump", PumpJson("1", "1", "1", "500", "{\"name\":\"plasma tank\",\"pressure\":5}"));
        Assert.Equal("Tank → Port", ByKey(toPort, "direction").Text);

        // a hyper-noblium crystal inside adds the reaction suppression row
        var (_, crystal, acts) = Render(PortablePump.Render, "PortablePump", PumpJson("1", "1", "0", "101", "null", crystal: "1", suppressed: "1"));
        UiNode suppression = ByKey(crystal, "reaction_suppression");
        Assert.Equal(("Enabled", "snowflake", true), (suppression.Text, suppression.Icon, suppression.Selected));
        suppression.OnClick();
        Assert.Equal(new (string, string)[] { ("reaction_suppression", null) }, acts);
        var (_, off, _) = Render(PortablePump.Render, "PortablePump", PumpJson("1", "1", "0", "101", "null", crystal: "1", suppressed: "0"));
        Assert.Equal(("Disabled", "times", false), (ByKey(off, "reaction_suppression").Text, ByKey(off, "reaction_suppression").Icon, ByKey(off, "reaction_suppression").Selected));
    }

    [Fact]
    public void ThePortableScrubberWindowListsTheFilterGasesAsToggleButtons()
    {
        var (context, tree, acts) = Render(PortableScrubber.Render, "PortableScrubber",
            "{\"on\":1,\"connected\":1,\"pressure\":50,\"hasHypernobCrystal\":0,\"reactionSuppressionEnabled\":0,\"filterTypes\":" + FilterTypes + ",\"holding\":null}");
        Assert.Equal((320.0, 420.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Status", "Holding Tank", "Filters" }, Titles(tree));
        Assert.Equal("50 kPa", ByKey(tree, "pressure-readout").Text);
        Assert.Equal(("Connected", "good"), (ByKey(tree, "port").Text, ByKey(tree, "port").Color));

        // a button for each gas: checked and selected when it is filtered, labelled with tgui's gas label (the name where it has none)
        UiNode co2 = ByKey(tree, "filter:co2");
        Assert.Equal(("CO₂", "check-square-o", true), (co2.Text, co2.Icon, co2.Selected));
        UiNode o2 = ByKey(tree, "filter:o2");
        Assert.Equal(("O₂", "square-o", false), (o2.Text, o2.Icon, o2.Selected));
        Assert.Equal("Some Vapor", ByKey(tree, "filter:vapor").Text);
        Assert.Equal(3, tree.Walk().Count(n => n.Key?.StartsWith("filter:") == true));

        co2.OnClick();
        o2.OnClick();
        ByKey(tree, "power").OnClick();
        Assert.Equal(new (string, string)[] { ("toggle_filter", "{\"val\":\"co2\"}"), ("toggle_filter", "{\"val\":\"o2\"}"), ("power", null) }, acts);
    }

    [Fact]
    public void ThePipeScrubberWindowShowsItsGaugesItsPowerButtonAndItsFiltersWhileFiltering()
    {
        const string Common = "\"connected\":1,\"pressureTank\":3000,\"pressureLimitTank\":10000,\"pressurePump\":20000,\"pressureLimitPump\":50000,\"hasHypernobCrystal\":0,\"reactionSuppressionEnabled\":0,\"filterTypes\":";
        var (context, tree, acts) = Render(PipeScrubber.Render, "PipeScrubber", "{\"on\":1,\"direction\":1," + Common + FilterTypes + "}");
        Assert.Equal((400.0, 350.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Status", "Direction" }, Titles(tree));
        Assert.Equal(new[] { "Buffer Port", "Buffer", "Tank", "Pump" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledControl).Select(n => n.Label));

        // the port: a plug that is checked and green when connected, with its tooltip
        UiNode port = ByKey(tree, "port");
        Assert.Equal((UiKind.Icon, "plug-circle-check", "good", "Connected", 2.0), (port.Kind, port.Icon, port.Color, port.Tooltip, port.Size));

        // the buffer and the tank gauges: the limit is the top of the scale, the ranges are 70 % and 85 % of it
        UiNode buffer = ByKey(tree, "pressurePump");
        Assert.Equal((UiKind.RoundGauge, 20000.0, 0.0, 50000.0, 35000.0, 1.75), (buffer.Kind, buffer.Value, buffer.MinValue, buffer.MaxValue, buffer.AlertAfter, buffer.Size));
        Assert.Equal(new[] { ("good", 0.0, 35000.0), ("average", 35000.0, 42500.0), ("bad", 42500.0, 50000.0) }, buffer.Ranges);
        Assert.Equal("20.00 MPa", buffer.Format(20000)); // tg's formatPressure keeps kPa below 10000 and switches to SI above
        UiNode tank = ByKey(tree, "pressureTank");
        Assert.Equal((3000.0, 10000.0, 7000.0), (tank.Value, tank.MaxValue, tank.AlertAfter));
        Assert.Equal("3000 kPa", tank.Format(3000));

        // the power button
        UiNode power = ByKey(tree, "power");
        Assert.Equal(("On", "power-off", true, "18px"), (power.Text, power.Icon, power.Selected, power.FontSize));

        // filtering into the tank: the direction button names the flow and the filter buttons show
        Assert.Equal("Buffer → Tank", ByKey(tree, "direction").Text);
        Assert.Contains("Filtering gases from the buffer into the internal tank.", tree.AllText());
        Assert.Equal(("CO₂", "check-square-o", true), (ByKey(tree, "filter:co2").Text, ByKey(tree, "filter:co2").Icon, ByKey(tree, "filter:co2").Selected));
        Assert.Equal(("O₂", "square-o", false), (ByKey(tree, "filter:o2").Text, ByKey(tree, "filter:o2").Icon, ByKey(tree, "filter:o2").Selected));

        ByKey(tree, "power").OnClick();
        ByKey(tree, "direction").OnClick();
        ByKey(tree, "filter:o2").OnClick();
        Assert.Equal(new (string, string)[] { ("power", null), ("direction", null), ("toggle_filter", "{\"val\":\"o2\"}") }, acts);

        // dumping the tank into the buffer: no filter buttons, a note instead; off; a disconnected plug; the crystal's button
        var (_, dump, _) = Render(PipeScrubber.Render, "PipeScrubber",
            "{\"on\":0,\"direction\":0,\"connected\":0,\"pressureTank\":0,\"pressureLimitTank\":10000,\"pressurePump\":0,\"pressureLimitPump\":50000,\"hasHypernobCrystal\":1,\"reactionSuppressionEnabled\":1,\"filterTypes\":" + FilterTypes + "}");
        Assert.Equal("Tank → Buffer", ByKey(dump, "direction").Text);
        Assert.Contains("Dumping internal tank gases into the buffer.", dump.AllText());
        Assert.DoesNotContain(dump.Walk(), n => n.Key?.StartsWith("filter:") == true);
        Assert.Equal(("Off", false), (ByKey(dump, "power").Text, ByKey(dump, "power").Selected));
        Assert.Equal(("plug-circle-xmark", "bad", "Disconnected"), (ByKey(dump, "port").Icon, ByKey(dump, "port").Color, ByKey(dump, "port").Tooltip));
        UiNode suppression = ByKey(dump, "reaction_suppression");
        Assert.Equal(("Reaction Suppression Enabled", "snowflake", true), (suppression.Text, suppression.Icon, suppression.Selected));
        var (_, noCrystal, _) = Render(PipeScrubber.Render, "PipeScrubber", "{\"on\":0,\"direction\":1," + Common + FilterTypes + "}");
        Assert.DoesNotContain(noCrystal.Walk(), n => n.Key == "reaction_suppression");
    }

    [Fact]
    public void TheTankWindowShowsItsPressureGaugeAndTheReleasePressureRegulatorWithTgsActs()
    {
        var (context, tree, acts) = Render(Tank.Render, "Tank",
            "{\"defaultReleasePressure\":16,\"minReleasePressure\":0,\"maxReleasePressure\":303,\"leakPressure\":3039,\"fragmentPressure\":4053,\"tankPressure\":1013,\"releasePressure\":16,\"connected\":1}");
        Assert.Equal((275.0, 120.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Pressure", "Pressure Regulator" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledControl).Select(n => n.Label));

        // the gauge: the fragment pressure plus 15 % is its top, the leak pressure its alert, the ranges good / average / bad
        UiNode gauge = ByKey(tree, "tankPressure");
        Assert.Equal((1013.0, 0.0, 4053 * 1.15, 3039.0, 2.0), (gauge.Value, gauge.MinValue, gauge.MaxValue, gauge.AlertAfter, gauge.Size));
        Assert.Equal(new[] { ("good", 0.0, 3039.0), ("average", 3039.0, 4053.0), ("bad", 4053.0, 4053 * 1.15) }, gauge.Ranges);
        Assert.Equal("1013 kPa", gauge.Format(1013));
        Assert.Equal("4500 kPa", gauge.Format(4500)); // below tg's 10000 switch: kPa
        Assert.Equal("12.00 MPa", gauge.Format(12000));

        // the regulator: min, the number input in kPa, max, reset; tg compares an undefined field, so none of them is ever disabled
        UiNode input = ByKey(tree, "releasePressure");
        Assert.Equal((UiKind.NumberInput, 16.0, "kPa", 0.0, 303.0, 1.0), (input.Kind, input.Value, input.Unit, input.MinValue, input.MaxValue, input.Step));
        Assert.Equal("65px", input.Width);
        Assert.Equal(("fast-backward", "fast-forward", "undo"), (ByKey(tree, "min").Icon, ByKey(tree, "max").Icon, ByKey(tree, "reset").Icon));
        Assert.All(new[] { "min", "max", "reset" }, key => Assert.False(ByKey(tree, key).Disabled));

        ByKey(tree, "min").OnClick();
        input.OnChange(100);
        ByKey(tree, "max").OnClick();
        ByKey(tree, "reset").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("pressure", "{\"pressure\":\"min\"}"), ("pressure", "{\"pressure\":100}"), ("pressure", "{\"pressure\":\"max\"}"), ("pressure", "{\"pressure\":\"reset\"}"),
        }, acts);
    }

    [Fact]
    public void AllFourWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)PortablePump.Render, TguiInterfaces.Find("PortablePump"));
        Assert.Equal((TguiInterface)PortableScrubber.Render, TguiInterfaces.Find("PortableScrubber"));
        Assert.Equal((TguiInterface)PipeScrubber.Render, TguiInterfaces.Find("PipeScrubber"));
        Assert.Equal((TguiInterface)Tank.Render, TguiInterfaces.Find("Tank"));
    }
}
