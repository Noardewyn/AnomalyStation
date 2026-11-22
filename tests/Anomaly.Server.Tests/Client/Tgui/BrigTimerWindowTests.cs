using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Syndicate brig timers (D316): the client's BrigTimer window without the engine: the time as mm:ss, Start or Stop with the timing
/// state, Flash disabled while the flashers recharge, the four adjust buttons and the three presets sending tg's actions.
/// </summary>
public class BrigTimerWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "BrigTimer", "Cell 1", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, BrigTimer.Render(context), acts);
    }

    [Fact]
    public void The_window_shows_the_time_and_sends_tgs_actions()
    {
        var (context, tree, acts) = Render("{\"timing\":0,\"minutes\":2,\"seconds\":5,\"flash_charging\":0}");
        Assert.Equal((300.0, 138.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("BrigTimer", TguiInterfaces.Names);
        Assert.Contains(tree.Walk(), n => n.Text == "02:05");
        Assert.Equal("Cell Timer", tree.Walk().Single(n => n.Kind == UiKind.Section).Title);

        UiNode start = tree.Walk().Single(n => n.Text == "Start");
        Assert.False(start.Selected);
        start.OnClick();
        tree.Walk().Single(n => n.Text == "Flash").OnClick();
        tree.Walk().Single(n => n.Icon == "fast-forward").OnClick();
        tree.Walk().Single(n => n.Icon == "backward").OnClick();
        tree.Walk().Single(n => n.Text == "Medium").OnClick();
        Assert.Equal("start", acts[0].Action);
        Assert.Equal("flash", acts[1].Action);
        Assert.Equal(("time", "{\"adjust\":600}"), acts[2]);
        Assert.Equal(("time", "{\"adjust\":-100}"), acts[3]);
        Assert.Equal(("preset", "{\"preset\":\"medium\"}"), acts[4]);
    }

    [Fact]
    public void A_running_timer_offers_stop_and_a_charging_flash_is_disabled()
    {
        var (_, tree, acts) = Render("{\"timing\":1,\"minutes\":0,\"seconds\":42,\"flash_charging\":1}");
        UiNode stop = tree.Walk().Single(n => n.Text == "Stop");
        Assert.True(stop.Selected);
        stop.OnClick();
        Assert.Equal("stop", acts.Single().Action);
        Assert.True(tree.Walk().Single(n => n.Text == "Recharging").Disabled);
        Assert.Contains(tree.Walk(), n => n.Text == "00:42");
    }
}
