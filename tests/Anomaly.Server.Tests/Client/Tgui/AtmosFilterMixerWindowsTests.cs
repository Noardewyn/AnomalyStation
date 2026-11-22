using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Package E1: the client's AtmosFilter and AtmosMixer windows (tg's AtmosFilter.tsx and AtmosMixer.tsx) without the engine. Each reads
/// tg's ui_data the way the TSX does (sizes, labels, icons, bounds, the Max buttons that disable at the maximum, the filter toggles,
/// the mixer's two coloured node labels) and sends tg's acts; both are registered under their interface names.
/// </summary>
public class AtmosFilterMixerWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string FilterData = "{\"on\":1,\"rate\":150,\"max_rate\":200,\"filter_types\":["
        + "{\"gas_id\":\"o2\",\"enabled\":0},{\"gas_id\":\"plasma\",\"enabled\":1},{\"gas_id\":\"co2\",\"enabled\":0}]}";

    [Fact]
    public void TheFilterWindowShowsThePowerButtonInTheTitleTheRateAndOneToggleButtonPerGasWithTgsActs()
    {
        var (context, tree, acts) = Render(AtmosFilter.Render, "AtmosFilter", FilterData);
        Assert.Equal((440.0, 240.0), (context.WindowWidth, context.WindowHeight));
        UiNode section = tree.Walk().Single(n => n.Kind == UiKind.Section);
        Assert.Equal(("Gas Filter", true), (section.Title, section.Fill));
        UiNode power = section.Buttons;
        Assert.Equal(("power", "On", "power-off", true), (power.Key, power.Text, power.Icon, power.Selected));
        Assert.Equal(new[] { "Transfer Rate", "Filter Types" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));

        UiNode input = ByKey(tree, "rate");
        Assert.Equal((UiKind.NumberInput, 150.0, "L/s", 0.0, 200.0, 1.0), (input.Kind, input.Value, input.Unit, input.MinValue, input.MaxValue, input.Step));
        Assert.Equal("63px", input.Width);
        Assert.False(ByKey(tree, "rate-max").Disabled);

        // one button per gas: the checked box and selected look for the enabled one, tg's gas labels
        List<UiNode> gases = tree.Walk().Where(n => n.Kind == UiKind.Button && n.Key is "o2" or "plasma" or "co2").ToList();
        Assert.Equal(new[] { "O₂", "Plasma", "CO₂" }, gases.Select(g => g.Text));
        Assert.Equal(new[] { "square-o", "check-square-o", "square-o" }, gases.Select(g => g.Icon));
        Assert.Equal(new[] { false, true, false }, gases.Select(g => g.Selected));

        power.OnClick();
        input.OnChange(75);
        ByKey(tree, "rate-max").OnClick();
        ByKey(tree, "co2").OnClick();
        ByKey(tree, "plasma").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("power", null), ("rate", "{\"rate\":75}"), ("rate", "{\"rate\":\"max\"}"),
            ("toggle_filter", "{\"val\":\"co2\"}"), ("toggle_filter", "{\"val\":\"plasma\"}"),
        }, acts);

        // off, at the maximum rate, no gas list yet (tg: filter_types = [] by default)
        var (_, off, _) = Render(AtmosFilter.Render, "AtmosFilter", "{\"on\":0,\"rate\":200,\"max_rate\":200}");
        UiNode offPower = off.Walk().Single(n => n.Kind == UiKind.Section).Buttons;
        Assert.Equal(("Off", "times", false), (offPower.Text, offPower.Icon, offPower.Selected));
        Assert.True(ByKey(off, "rate-max").Disabled);
        Assert.DoesNotContain(off.Walk(), n => n.Icon is "square-o" or "check-square-o");
    }

    [Fact]
    public void TheMixerWindowShowsPowerPressureAndTheTwoNodesWithTgsActs()
    {
        var (context, tree, acts) = Render(AtmosMixer.Render, "AtmosMixer",
            "{\"on\":1,\"set_pressure\":1000,\"max_pressure\":4500,\"node1_concentration\":30,\"node2_concentration\":70}");
        Assert.Equal((370.0, 165.0), (context.WindowWidth, context.WindowHeight));
        List<UiNode> rows = tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).ToList();
        Assert.Equal(new[] { "Power", "Output Pressure", "Main Node", "Side Node" }, rows.Select(n => n.Label));
        Assert.Equal(new string[] { null, null, "green", "blue" }, rows.Select(n => n.LabelColor));
        Assert.Equal(("On", "power-off", true), (ByKey(tree, "power").Text, ByKey(tree, "power").Icon, ByKey(tree, "power").Selected));

        UiNode pressure = ByKey(tree, "pressure");
        Assert.Equal((UiKind.NumberInput, 1000.0, "kPa", 0.0, 4500.0, 10.0), (pressure.Kind, pressure.Value, pressure.Unit, pressure.MinValue, pressure.MaxValue, pressure.Step));
        Assert.Equal("75px", pressure.Width);
        Assert.False(ByKey(tree, "pressure-max").Disabled);
        foreach ((string key, double value) in new[] { ("node1", 30.0), ("node2", 70.0) })
        {
            UiNode node = ByKey(tree, key);
            Assert.Equal((UiKind.NumberInput, value, "%", 0.0, 100.0, 1.0, 2.0), (node.Kind, node.Value, node.Unit, node.MinValue, node.MaxValue, node.Step, node.StepPixelSize));
            Assert.Equal("60px", node.Width);
        }

        ByKey(tree, "power").OnClick();
        pressure.OnChange(2500);
        ByKey(tree, "pressure-max").OnClick();
        ByKey(tree, "node1").OnChange(40);
        ByKey(tree, "node2").OnChange(10);
        Assert.Equal(new (string, string)[]
        {
            ("power", null), ("pressure", "{\"pressure\":2500}"), ("pressure", "{\"pressure\":\"max\"}"),
            ("node1", "{\"concentration\":40}"), ("node2", "{\"concentration\":10}"),
        }, acts);

        var (_, off, _) = Render(AtmosMixer.Render, "AtmosMixer",
            "{\"on\":0,\"set_pressure\":4500,\"max_pressure\":4500,\"node1_concentration\":50,\"node2_concentration\":50}");
        Assert.Equal(("Off", "times", false), (ByKey(off, "power").Text, ByKey(off, "power").Icon, ByKey(off, "power").Selected));
        Assert.True(ByKey(off, "pressure-max").Disabled);
    }

    [Fact]
    public void BothWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)AtmosFilter.Render, TguiInterfaces.Find("AtmosFilter"));
        Assert.Equal((TguiInterface)AtmosMixer.Render, TguiInterfaces.Find("AtmosMixer"));
    }
}
