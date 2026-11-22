using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// M7 slice D172: the client's AtmosTempGate and AtmosTempPump windows (tg's AtmosTempGate.tsx and AtmosTempPump.tsx) without the engine.
/// Each reads tg's ui_data the way the TSX does (the power button, one number input with its unit and bounds, the Max button that
/// disables at the maximum) and sends tg's acts; both are registered under their interface names.
/// </summary>
public class AtmosTempWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void TheTemperatureGateWindowShowsPowerAndTheTemperatureInKelvinWithTgsActs()
    {
        var (context, tree, acts) = Render(AtmosTempGate.Render, "AtmosTempGate", "{\"on\":1,\"temperature\":273,\"min_temperature\":2,\"max_temperature\":4500}");
        Assert.Equal((335.0, 115.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Power", "Heat settings" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        Assert.Equal(("On", "power-off", true), (ByKey(tree, "power").Text, ByKey(tree, "power").Icon, ByKey(tree, "power").Selected));
        UiNode input = ByKey(tree, "temperature");
        Assert.Equal((UiKind.NumberInput, 273.0, "K", 2.0, 4500.0, 1.0), (input.Kind, input.Value, input.Unit, input.MinValue, input.MaxValue, input.Step));
        Assert.Equal("75px", input.Width);
        Assert.False(ByKey(tree, "temperature-max").Disabled);

        ByKey(tree, "power").OnClick();
        input.OnChange(300);
        ByKey(tree, "temperature-max").OnClick();
        Assert.Equal(new (string, string)[] { ("power", null), ("temperature", "{\"temperature\":300}"), ("temperature", "{\"temperature\":\"max\"}") }, acts);

        // off, and already at the maximum: the Max button is disabled
        var (_, off, _) = Render(AtmosTempGate.Render, "AtmosTempGate", "{\"on\":0,\"temperature\":4500,\"min_temperature\":2,\"max_temperature\":4500}");
        Assert.Equal(("Off", "times", false), (ByKey(off, "power").Text, ByKey(off, "power").Icon, ByKey(off, "power").Selected));
        Assert.True(ByKey(off, "temperature-max").Disabled);
    }

    [Fact]
    public void TheTemperaturePumpWindowShowsPowerAndTheHeatTransferRateInPercentWithTgsActs()
    {
        var (context, tree, acts) = Render(AtmosTempPump.Render, "AtmosTempPump", "{\"on\":1,\"rate\":40,\"max_heat_transfer_rate\":100}");
        Assert.Equal((335.0, 115.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Power", "Heat transfer rate" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        UiNode input = ByKey(tree, "rate");
        Assert.Equal((UiKind.NumberInput, 40.0, "%", 0.0, 100.0, 1.0), (input.Kind, input.Value, input.Unit, input.MinValue, input.MaxValue, input.Step));
        Assert.False(ByKey(tree, "rate-max").Disabled);

        ByKey(tree, "power").OnClick();
        input.OnChange(75);
        ByKey(tree, "rate-max").OnClick();
        Assert.Equal(new (string, string)[] { ("power", null), ("rate", "{\"rate\":75}"), ("rate", "{\"rate\":\"max\"}") }, acts);

        var (_, full, _) = Render(AtmosTempPump.Render, "AtmosTempPump", "{\"on\":0,\"rate\":100,\"max_heat_transfer_rate\":100}");
        Assert.True(ByKey(full, "rate-max").Disabled);
    }

    [Fact]
    public void BothWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)AtmosTempGate.Render, TguiInterfaces.Find("AtmosTempGate"));
        Assert.Equal((TguiInterface)AtmosTempPump.Render, TguiInterfaces.Find("AtmosTempPump"));
        Assert.Contains("AtmosPump", TguiInterfaces.Names); // the pressure gate reuses tg's AtmosPump
    }
}
