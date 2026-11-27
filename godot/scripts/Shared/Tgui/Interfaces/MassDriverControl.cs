using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MassDriverControl.jsx: the mass driver launch
// control's auto launch timer with its start button and its adjust buttons, the outer door's toggle, the driver's power level with
// the test fire, and the launch button; "No connected mass driver" without a driver. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MassDriverControl
{
    // tg: MassDriverControl.jsx:11
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool connected = data.Truthy("connected");
        bool timing = data.Truthy("timing");
        bool poddoor = data.Truthy("poddoor");
        string minutes = data.Str("minutes").PadLeft(2, '0');
        string seconds = data.Str("seconds").PadLeft(2, '0');
        var content = new UiNode(UiKind.Fragment);
        if (connected)
        {
            content.With(new UiNode(UiKind.Section)
            {
                Key = "auto",
                Title = "Auto Launch",
                Buttons = new UiNode(UiKind.Button) { Key = "time", Icon = "clock-o", Text = timing ? "Stop" : "Start", Selected = timing, OnClick = () => c.Act("time") },
            }.With(
                Adjust(c, "fast-backward", -30, timing),
                Adjust(c, "backward", -1, timing),
                new UiNode(UiKind.Box) { Key = "clock", Text = " " + minutes + ":" + seconds + " " },
                Adjust(c, "forward", 1, timing),
                Adjust(c, "fast-forward", 30, timing)));
        }
        UiNode controls = connected
            ? new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.LabeledList).With(new UiNode(UiKind.LabeledItem)
                {
                    Key = "power",
                    Label = "Power Level",
                    Buttons = new UiNode(UiKind.Button) { Key = "driver_test", Icon = "bomb", Text = "Test Fire", Disabled = timing, OnClick = () => c.Act("driver_test") },
                }.With(new UiNode(UiKind.NumberInput)
                {
                    Key = "power-input", Value = data.Num("power"), Width = "40px", Step = 1, MinValue = 0.25, MaxValue = 16,
                    OnChange = value => c.Act("set_power", ("power", value)),
                })),
                new UiNode(UiKind.Button) { Key = "launch", Fluid = true, Text = "Launch", Disabled = timing, Icon = "arrow-up", TextAlign = "center", OnClick = () => c.Act("launch") })
            : new UiNode(UiKind.Box) { Key = "none", Color = "bad", Text = "No connected mass driver" };
        content.With(new UiNode(UiKind.Section)
        {
            Key = "controls",
            Title = "Controls",
            Buttons = new UiNode(UiKind.Button) { Key = "door", Icon = "toggle-on", Text = "Toggle Outer Door", Disabled = timing || !poddoor, OnClick = () => c.Act("door") },
        }.With(controls));
        return c.Window(300, connected ? 215 : 107, c.ConfigTitle, content);
    }

    private static UiNode Adjust(TguiContext c, string icon, int adjust, bool timing) =>
        new UiNode(UiKind.Button) { Key = "adjust" + adjust, Icon = icon, Disabled = timing, OnClick = () => c.Act("input", ("adjust", adjust)) };
}
