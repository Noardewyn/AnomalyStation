using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Package E5: the client's AtmosControlConsole window (tg's AtmosControlConsole.tsx) without the engine. A tank console shows its one
/// chamber's reading and the input injector's and output vent's controls with tg's acts; the ordnance console picks one of its two
/// chambers in a dropdown; a monitor-only console (nocontrol) has no controls; missing sensors and devices show tg's notices.
/// </summary>
public class AtmosControlConsoleWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "AtmosControlConsole", "console", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, AtmosControlConsole.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Gasmix = "{\"gases\":[[\"o2\",\"Oxygen\",500]],\"reactions\":[],\"name\":\"\",\"total_moles\":500,\"temperature\":293.15,"
        + "\"volume\":2500,\"pressure\":487.32,\"reference\":\"[0x1]\"}";

    private const string Statics = "\"maxInput\":200,\"maxOutput\":4500,\"gasInfo\":[],\"reactionInfo\":[]";

    [Fact]
    public void ATankConsoleShowsItsChambersReadingAndTheInjectorAndVentControlsWithTgsActs()
    {
        var (context, tree, acts) = Render("{" + Statics + ",\"control\":1,\"reconnecting\":1,\"defaultGas\":\"o2\",\"chambers\":[{\"id\":\"o2\","
            + "\"name\":\"Oxygen Supply\",\"gasmix\":" + Gasmix + ",\"input_info\":{\"active\":1,\"amount\":200},\"output_info\":{\"active\":0,\"amount\":4500}}]}");
        Assert.Equal((550.0, 600.0), (context.WindowWidth, context.WindowHeight));
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Dropdown); // one chamber: no selection
        UiNode reading = ByKey(tree, "reading");
        Assert.Equal("Oxygen Supply", reading.Title);
        Assert.Equal(("Reconnect", "undo"), (reading.Buttons.Text, reading.Buttons.Icon));
        Assert.Contains("500.00 mol (100.00 %)", reading.AllText());
        Assert.Contains("487.32 kPa", reading.AllText());
        Assert.Equal("o2", context.UseState("handbook:gas", "")); // tg: setActiveGasId(defaultGas)

        Assert.Equal("Chamber Controls", ByKey(tree, "controls").Title);
        Assert.Equal(new[] { "Input Injector", "Input Rate", "Output Regulator", "Output Pressure" },
            ByKey(tree, "controls").Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        UiNode input = ByKey(tree, "toggle_input");
        Assert.Equal(("Injecting", "power-off", true), (input.Text, input.Icon, input.Selected));
        UiNode rate = ByKey(tree, "adjust_input");
        Assert.Equal((UiKind.NumberInput, 200.0, "L/s", "63px", 0.0, 200.0, 1.0), (rate.Kind, rate.Value, rate.Unit, rate.Width, rate.MinValue, rate.MaxValue, rate.Step));
        UiNode output = ByKey(tree, "toggle_output");
        Assert.Equal(("Closed", "times", false), (output.Text, output.Icon, output.Selected));
        UiNode pressure = ByKey(tree, "adjust_output");
        Assert.Equal((UiKind.NumberInput, 4500.0, "kPa", "75px", 0.0, 4500.0, 10.0), (pressure.Kind, pressure.Value, pressure.Unit, pressure.Width, pressure.MinValue, pressure.MaxValue, pressure.Step));

        reading.Buttons.OnClick();
        input.OnClick();
        rate.OnChange(150);
        output.OnClick();
        pressure.OnChange(3000);
        Assert.Equal(new (string, string)[]
        {
            ("reconnect", null), ("toggle_input", "{\"chamber\":\"o2\"}"), ("adjust_input", "{\"chamber\":\"o2\",\"rate\":150}"),
            ("toggle_output", "{\"chamber\":\"o2\"}"), ("adjust_output", "{\"chamber\":\"o2\",\"rate\":3000}"),
        }, acts);
    }

    [Fact]
    public void TheOrdnanceConsolePicksAChamberInTheDropdownAndShowsTgsNoticesForWhatIsMissing()
    {
        const string json = "{" + Statics + ",\"control\":1,\"reconnecting\":1,\"defaultGas\":null,\"chambers\":["
            + "{\"id\":\"ordnanceburn\",\"name\":\"Ordnance Burn Chamber\",\"gasmix\":" + Gasmix + ",\"input_info\":{\"active\":0,\"amount\":200}},"
            + "{\"id\":\"ordnancefreezer\",\"name\":\"Ordnance Freezer Chamber\"}]}";
        var local = new TguiLocalState();
        var (_, tree, _) = Render(json, local);
        UiNode dropdown = ByKey(tree, "chamber");
        Assert.Equal(("Chamber Selection", "100%"), (ByKey(tree, "selection").Title, dropdown.Width));
        Assert.Equal(new[] { "Ordnance Burn Chamber", "Ordnance Freezer Chamber" }, dropdown.Options);
        Assert.Equal("Ordnance Burn Chamber", dropdown.TextValue); // useState(chambers[0]?.id)
        Assert.Equal(("Off", "times"), (ByKey(tree, "toggle_input").Text, ByKey(tree, "toggle_input").Icon));
        Assert.Equal("No Output Device Detected !", ByKey(tree, "no-output").Text);

        dropdown.OnSelected("Ordnance Freezer Chamber");
        var (_, freezer, acts) = Render(json, local);
        Assert.Equal("Ordnance Freezer Chamber", ByKey(freezer, "reading").Title);
        Assert.Equal("No Sensors Detected!", ByKey(freezer, "no-sensor").Text);
        Assert.Equal("No Input Device Detected!", ByKey(freezer, "no-input").Text);
        ByKey(freezer, "reading").Buttons.OnClick();
        Assert.Equal(new (string, string)[] { ("reconnect", null) }, acts);
    }

    [Fact]
    public void AMonitorOnlyConsoleHasNoControlsAndAFixedOneNoReconnect()
    {
        var (_, master, _) = Render("{" + Statics + ",\"control\":0,\"reconnecting\":1,\"defaultGas\":null,\"chambers\":["
            + "{\"id\":\"distro\",\"name\":\"Distribution Loop\",\"gasmix\":" + Gasmix + "},{\"id\":\"waste\",\"name\":\"Waste Loop\",\"gasmix\":" + Gasmix + "}]}");
        Assert.DoesNotContain(master.Walk(), n => n.Key == "controls");
        Assert.Equal("Distribution Loop", ByKey(master, "reading").Title);
        Assert.NotNull(ByKey(master, "reading").Buttons);

        var (_, none, _) = Render("{" + Statics + ",\"control\":0,\"reconnecting\":0,\"defaultGas\":null,\"chambers\":[]}");
        Assert.Equal("Chamber Reading", ByKey(none, "reading").Title);
        Assert.Null(ByKey(none, "reading").Buttons);
        Assert.Equal("No Sensors Detected!", ByKey(none, "no-sensor").Text);
    }

    [Fact]
    public void TheWindowIsRegisteredUnderItsInterfaceName()
    {
        Assert.Equal((TguiInterface)AtmosControlConsole.Render, TguiInterfaces.Find("AtmosControlConsole"));
    }
}
