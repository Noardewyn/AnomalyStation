using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>Gaps package F1: the client's SmokeMachine window without the engine reads tg's ui_data the way SmokeMachine.tsx does and sends tg's acts.</summary>
public class SmokeMachineWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "SmokeMachine", "SmokeMachine", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, SmokeMachine.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void The_tank_its_range_and_its_contents_show_as_tg_draws_them_and_send_tgs_acts()
    {
        Assert.NotNull(TguiInterfaces.Find("SmokeMachine"));
        var (context, tree, acts) = Render("{\"tank\":{\"maxVolume\":300,\"currentVolume\":60,\"contents\":[{\"name\":\"Water\",\"volume\":40},{\"name\":\"Condensed Capsaicin\",\"volume\":20}]},\"active\":1,\"setting\":2,\"maxSetting\":3}");
        Assert.Equal((350.0, 350.0), (context.WindowWidth, context.WindowHeight));
        UiNode power = ByKey(tree, "power");
        Assert.Equal(("On", "power-off", true), (power.Text, power.Icon, power.Selected));
        UiNode volume = ByKey(tree, "volume");
        Assert.Equal((0.2, "60 / 300"), (volume.Value, volume.Text));
        Assert.Equal(("bad", double.NegativeInfinity, 0.3), volume.Ranges.Single());
        Assert.Equal(new[] { "3", "6", "9", "12", "15" }, tree.Walk().Where(n => n.Key?.StartsWith("setting:", StringComparison.Ordinal) == true).Select(n => n.Text));
        Assert.True(ByKey(tree, "setting:2").Selected);
        Assert.True(ByKey(tree, "setting:4").Disabled); // the machine's servos give a range of 3 settings
        Assert.False(ByKey(tree, "setting:3").Disabled);
        Assert.Equal("40 units of Water", ByKey(tree, "chemical:Water").Text);
        Assert.Equal("label", ByKey(tree, "chemical:Condensed Capsaicin").Color);

        power.OnClick();
        ByKey(tree, "setting:3").OnClick();
        ByKey(tree, "purge").OnClick();
        Assert.Equal(new[] { ("power", (string)null), ("setting", "{\"amount\":3}"), ("purge", null) }, acts.ToArray());
    }

    [Fact]
    public void An_idle_empty_machine_shows_off_and_no_chemicals()
    {
        var (_, tree, _) = Render("{\"tank\":{\"maxVolume\":100,\"currentVolume\":0,\"contents\":[]},\"active\":0,\"setting\":1,\"maxSetting\":3}");
        UiNode power = ByKey(tree, "power");
        Assert.Equal(("Off", "times", false), (power.Text, power.Icon, power.Selected));
        Assert.DoesNotContain(tree.Walk(), n => n.Key?.StartsWith("chemical:", StringComparison.Ordinal) == true);
    }
}
