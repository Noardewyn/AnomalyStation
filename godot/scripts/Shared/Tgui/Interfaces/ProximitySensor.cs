using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ProximitySensor.jsx (gaps package C3): the proximity sensor (proximity.dm:145).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ProximitySensor
{
    // tg: ProximitySensor.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool timing = data.Truthy("timing");
        bool scanning = data.Truthy("scanning");
        double sensitivity = data.Num("sensitivity");
        string minutes = TguiFormat.JsNumber(data.Num("minutes")).PadLeft(2, '0');
        string seconds = TguiFormat.JsNumber(data.Num("seconds")).PadLeft(2, '0');

        // tg: ProximitySensor.jsx:10  <Window width={250} height={185}>
        return c.Window(250, 185, null,
            new UiNode(UiKind.Section).With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Status" }.With(new UiNode(UiKind.Button)
                {
                    Key = "scanning",
                    Icon = scanning ? "lock" : "unlock",
                    Text = scanning ? "Armed" : "Not Armed",
                    Selected = scanning,
                    OnClick = () => c.Act("scanning"),
                }),
                new UiNode(UiKind.LabeledItem) { Label = "Detection Range" }.With(new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "range-", Icon = "backward", Disabled = scanning, OnClick = () => c.Act("sense", ("range", -1)) }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "range", Text = TguiFormat.JsNumber(sensitivity) }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "range+", Icon = "forward", Disabled = scanning, OnClick = () => c.Act("sense", ("range", 1)) }))))),
            new UiNode(UiKind.Section)
            {
                Title = "Auto Arm",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "time",
                    Icon = "clock-o",
                    Text = timing ? "Stop" : "Start",
                    Selected = timing,
                    Disabled = scanning,
                    OnClick = () => c.Act("time"),
                },
            }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "adjust-30", Icon = "fast-backward", Disabled = scanning || timing, OnClick = () => c.Act("input", ("adjust", -30)) }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "adjust-1", Icon = "backward", Disabled = scanning || timing, OnClick = () => c.Act("input", ("adjust", -1)) }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "clock", Text = minutes + ":" + seconds }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "adjust+1", Icon = "forward", Disabled = scanning || timing, OnClick = () => c.Act("input", ("adjust", 1)) }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "adjust+30", Icon = "fast-forward", Disabled = scanning || timing, OnClick = () => c.Act("input", ("adjust", 30)) }))));
    }
}
