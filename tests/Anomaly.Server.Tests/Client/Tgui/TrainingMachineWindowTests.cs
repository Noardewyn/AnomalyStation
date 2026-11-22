using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps ST4: the client's TrainingMachine window (tg's TrainingMachine.tsx) titled AURUMILL: its speed and range knobs within tg's
/// bounds and steps, and the BEGIN or END button by the machine's state; it is registered under its name.
/// </summary>
public class TrainingMachineWindowTests
{
    private static (TguiContext Context, UiNode Tree) Render(string json)
    {
        var context = new TguiContext(1, "TrainingMachine", "w", 2, UiValue.FromJson(json), (_, _) => { }, new TguiLocalState());
        return (context, TrainingMachine.Render(context));
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheTrainingMachineWindowSetsSpeedAndRangeAndBeginsOrEnds()
    {
        Assert.NotNull(TguiInterfaces.Find("TrainingMachine"));

        var (context, idle) = Render("{\"range\":3,\"movespeed\":4.5,\"moving\":0}");
        Assert.Equal((230.0, 150.0), (context.WindowWidth, context.WindowHeight));
        UiNode speed = ByKey(idle, "movespeed");
        Assert.Equal(UiKind.Knob, speed.Kind);
        Assert.Equal((4.5, 1.0, 10.0, 0.5), (speed.Value, speed.MinValue, speed.MaxValue, speed.Step));
        UiNode range = ByKey(idle, "range");
        Assert.Equal((3.0, 1.0, 7.0, 1.0), (range.Value, range.MinValue, range.MaxValue, range.Step));
        UiNode toggle = ByKey(idle, "toggle");
        Assert.Equal("BEGIN", toggle.Text);
        Assert.False(toggle.Selected);

        var (_, running) = Render("{\"range\":3,\"movespeed\":4.5,\"moving\":1}");
        UiNode stop = ByKey(running, "toggle");
        Assert.Equal("END", stop.Text);
        Assert.True(stop.Selected);
    }
}
