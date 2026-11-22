using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BrigTimer.tsx (syndicate brig timers, D316: the brig cells'
// door timers). Engine-free (compiled into the tests too). tgui flows the section's buttons and the time inline with a line break; here
// they are two rows.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BrigTimer
{
    // tg: BrigTimer.tsx:14
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool timing = data.Truthy("timing");
        bool flashCharging = data.Truthy("flash_charging");
        int minutes = (int)data.Num("minutes");
        int seconds = (int)data.Num("seconds");
        var buttons = new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Button)
            {
                Icon = "clock-o",
                Text = timing ? "Stop" : "Start",
                Selected = timing,
                OnClick = () => c.Act(timing ? "stop" : "start"),
            },
            new UiNode(UiKind.Button)
            {
                Icon = "lightbulb-o",
                Text = flashCharging ? "Recharging" : "Flash",
                Disabled = flashCharging,
                OnClick = () => c.Act("flash"),
            });
        UiNode Item(UiNode child) => new UiNode(UiKind.StackItem).With(child);
        var time = new UiNode(UiKind.Stack).With(
            Item(new UiNode(UiKind.Button) { Icon = "fast-backward", OnClick = () => c.Act("time", ("adjust", -600)) }),
            Item(new UiNode(UiKind.Button) { Icon = "backward", OnClick = () => c.Act("time", ("adjust", -100)) }),
            Item(new UiNode(UiKind.Box) { Text = minutes.ToString("00") + ":" + seconds.ToString("00") }),
            Item(new UiNode(UiKind.Button) { Icon = "forward", OnClick = () => c.Act("time", ("adjust", 100)) }),
            Item(new UiNode(UiKind.Button) { Icon = "fast-forward", OnClick = () => c.Act("time", ("adjust", 600)) }));
        var presets = new UiNode(UiKind.Stack).With(
            Item(new UiNode(UiKind.Button) { Icon = "hourglass-start", Text = "Short", OnClick = () => c.Act("preset", ("preset", "short")) }),
            Item(new UiNode(UiKind.Button) { Icon = "hourglass-start", Text = "Medium", OnClick = () => c.Act("preset", ("preset", "medium")) }),
            Item(new UiNode(UiKind.Button) { Icon = "hourglass-start", Text = "Long", OnClick = () => c.Act("preset", ("preset", "long")) }));
        return c.Window(300, 138, null,
            new UiNode(UiKind.Section) { Title = "Cell Timer", Buttons = buttons }.With(time, presets));
    }
}
