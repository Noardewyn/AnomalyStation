using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Engineering package E4: the client's GravityGenerator window (tg's GravityGenerator.jsx) without the engine. It reads tg's ui_data of
/// the main gravity generator (the breaker, the charge, the charging state, on, operational) and sends tg's one act, gentoggle.
/// </summary>
public class GravityGeneratorWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "GravityGenerator", "gravitational generator", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, GravityGenerator.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void TheBreakerChargeAndModeShowTgValuesAndTheButtonSendsGentoggle()
    {
        var (context, tree, acts) = Render("{\"breaker\":1,\"charge_count\":100,\"charging_state\":0,\"on\":1,\"operational\":1}");
        Assert.Equal((400.0, 155.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Power", "Gravity Charge", "Charge Mode" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        UiNode power = ByKey(tree, "power");
        Assert.Equal(("On", "power-off", true, false), (power.Text, power.Icon, power.Selected, power.Disabled));
        Assert.Equal(1.0, ByKey(tree, "charge").Value);
        Assert.Equal(("Fully Charged", "good"), (ByKey(tree, "mode").Text, ByKey(tree, "mode").Color));

        power.OnClick();
        Assert.Equal(new (string, string)[] { ("gentoggle", null) }, acts);
    }

    [Theory]
    [InlineData(0, 0, "Not Charging", "bad")]
    [InlineData(1, 40, "Charging", "average")]
    [InlineData(2, 60, "Discharging", "average")]
    public void TheChargeModeFollowsTheChargingState(int state, int charge, string text, string color)
    {
        var (_, tree, _) = Render("{\"breaker\":" + (state == 1 ? 1 : 0) + ",\"charge_count\":" + charge + ",\"charging_state\":" + state + ",\"on\":0,\"operational\":1}");
        Assert.Equal((text, color), (ByKey(tree, "mode").Text, ByKey(tree, "mode").Color));
        Assert.Equal(charge / 100.0, ByKey(tree, "charge").Value);
        Assert.Equal(state == 1 ? ("On", "power-off") : ("Off", "times"), (ByKey(tree, "power").Text, ByKey(tree, "power").Icon));
    }

    [Fact]
    public void ABrokenGeneratorShowsOnlyTheNotice()
    {
        var (_, tree, _) = Render("{\"breaker\":0,\"charge_count\":0,\"charging_state\":0,\"on\":0,\"operational\":0}");
        Assert.Equal("No data available", tree.Walk().Single(n => n.Kind == UiKind.NoticeBox).Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Button || n.Kind == UiKind.ProgressBar);
    }

    [Fact]
    public void TheWindowIsRegisteredUnderItsInterfaceName()
    {
        Assert.Equal((TguiInterface)GravityGenerator.Render, TguiInterfaces.Find("GravityGenerator"));
    }
}
