using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Engineering package E11: the client's TurbineComputer window (tg's TurbineComputer.tsx: the status with the power button, the intake
/// regulator, the integrity bar and the readings, or the out of service modal) and ThermoElectricGenerator window (tg's
/// ThermoElectricGenerator.tsx: the error, or the last output and the cold and hot loops), without the engine.
/// </summary>
public class TurbineTegWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string name, Func<TguiContext, UiNode> render, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Running = "{\"connected\":1,\"active\":1,\"rpm\":1500,\"power\":22500,\"integrity\":95.7,\"max_rpm\":50000,\"max_temperature\":50000,\"temp\":1200,\"regulator\":0.5}";

    [Fact]
    public void TheTurbineStatusShowsTgsRowsAndItsControlsSendTheActs()
    {
        var (context, tree, acts) = Render("TurbineComputer", TurbineComputer.Render, Running);
        Assert.Equal((310.0, 240.0), (context.WindowWidth, context.WindowHeight));
        UiNode section = tree.Walk().Single(n => n.Kind == UiKind.Section);
        Assert.Equal("Status", section.Title);
        Assert.Equal(new[] { "Intake Regulator", "Turbine Integrity", "Turbine Speed", "Max Turbine Speed", "Input Temperature", "Max Temperature", "Generated Power" },
            tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        UiNode power = ByKey(tree, "toggle_power");
        Assert.Equal(("Online", "power-off", true, true), (power.Text, power.Icon, power.Selected, power.Disabled)); // 1500 rpm: no turning off
        Assert.Equal(("1500 RPM", "50000 RPM", "1200 K", "50000 K", "22.50 kW"), (ByKey(tree, "rpm").Text, ByKey(tree, "max_rpm").Text, ByKey(tree, "temp").Text, ByKey(tree, "max_temperature").Text, ByKey(tree, "power").Text));
        UiNode integrity = ByKey(tree, "integrity");
        Assert.Equal((95.7, 0.0, 100.0), (integrity.Value, integrity.MinValue, integrity.MaxValue));
        Assert.Equal("good", integrity.Ranges.First(range => integrity.Value >= range.From && integrity.Value <= range.To).Color);
        UiNode regulator = ByKey(tree, "regulator");
        Assert.Equal((50.0, "%", 1.0, 1.0, 100.0), (regulator.Value, regulator.Unit, regulator.Step, regulator.MinValue, regulator.MaxValue));

        regulator.OnChange(80);
        power.OnClick();
        Assert.Equal(2, acts.Count);
        Assert.Equal("regulate", acts[0].Action);
        Assert.Equal(0.8, UiValue.FromJson(acts[0].Params).Num("regulate"), 6);
        Assert.Equal(("toggle_power", (string)null), acts[1]);
    }

    [Fact]
    public void AStoppedTurbineCanBeTurnedOnAndAnUnlinkedOneIsOutOfService()
    {
        var (_, tree, _) = Render("TurbineComputer", TurbineComputer.Render, Running.Replace("\"active\":1,\"rpm\":1500", "\"active\":0,\"rpm\":0"));
        UiNode power = ByKey(tree, "toggle_power");
        Assert.Equal(("Offline", "times", false, false), (power.Text, power.Icon, power.Selected, power.Disabled));

        (_, tree, _) = Render("TurbineComputer", TurbineComputer.Render, "{\"connected\":0}");
        UiNode modal = tree.Walk().Single(n => n.Kind == UiKind.Modal);
        Assert.Contains("Parts not connected", modal.AllText());
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Section || n.Kind == UiKind.Button);
    }

    [Fact]
    public void TheGeneratorShowsItsLoopsOrItsError()
    {
        const string json = "{\"error_message\":null,\"last_power_output\":\"1.25 MW\","
            + "\"cold_data\":[{\"temperature_inlet\":100,\"temperature_outlet\":845.3,\"pressure_inlet\":831,\"pressure_outlet\":415.5}],"
            + "\"hot_data\":[{\"temperature_inlet\":5000,\"temperature_outlet\":3617.2,\"pressure_inlet\":41550,\"pressure_outlet\":20775}]}";
        var (context, tree, acts) = Render("ThermoElectricGenerator", ThermoElectricGenerator.Render, json);
        Assert.Equal((350.0, 195.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Last Output: 1.25 MW", ByKey(tree, "output").Text);
        Assert.Equal(("Cold Loop", "cyan", true), (ByKey(tree, "cold").Text, ByKey(tree, "cold").Color, ByKey(tree, "cold").Bold));
        Assert.Equal(("Hot loop ", "red"), (ByKey(tree, "hot").Text, ByKey(tree, "hot").Color));
        Assert.Equal("Temperature Inlet: 100 K / Outlet: 845.3 K", ByKey(tree, "cold0-temperature").Text);
        Assert.Equal("Pressure Inlet: 831 kPa / Outlet: 415.5 kPa", ByKey(tree, "cold0-pressure").Text);
        Assert.Equal("Temperature Inlet: 5000 K / Outlet: 3617.2 K", ByKey(tree, "hot0-temperature").Text);
        Assert.Equal("Pressure Inlet: 41550 kPa / Outlet: 20775 kPa", ByKey(tree, "hot0-pressure").Text);
        Assert.Empty(acts);

        (context, tree, _) = Render("ThermoElectricGenerator", ThermoElectricGenerator.Render, "{\"error_message\":\"Unable to locate cold circulator!\"}");
        Assert.Equal((320.0, 100.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("ERROR: Unable to locate cold circulator!", ByKey(tree, "error").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "output");
    }

    [Fact]
    public void TheWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)TurbineComputer.Render, TguiInterfaces.Find("TurbineComputer"));
        Assert.Equal((TguiInterface)ThermoElectricGenerator.Render, TguiInterfaces.Find("ThermoElectricGenerator"));
    }
}
