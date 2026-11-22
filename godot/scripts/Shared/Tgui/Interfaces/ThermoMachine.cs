using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ThermoMachine.jsx (M7 slice 4, the cooling loop).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ThermoMachine
{
    // JavaScript's Math.round: halves go up
    private static double JsRound(double value) => System.Math.Floor(value + 0.5);

    // tg: tgui/packages/tgui/interfaces/ThermoMachine.jsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool on = data.Truthy("on");
        double target = data.Num("target");
        double min = data.Num("min");
        double max = data.Num("max");
        double initial = data.Num("initial");

        // tg: ThermoMachine.jsx:16  <Window width={300} height={350}> <Window.Content>
        return c.Window(300, 350, null,
            // tg: ThermoMachine.jsx:18
            new UiNode(UiKind.Section) { Title = "Status" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Temperature" }.With(
                        new UiNode(UiKind.Box) { Key = "temperature", Text = TguiFormat.ToFixed(data.Num("temperature"), 2) + " K" }),
                    new UiNode(UiKind.LabeledItem) { Label = "Pressure" }.With(
                        new UiNode(UiKind.Box) { Key = "pressure", Text = TguiFormat.ToFixed(data.Num("pressure"), 2) + " kPa" }))),
            // tg: ThermoMachine.jsx:36
            new UiNode(UiKind.Section)
            {
                Title = "Controls",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "power",
                    Icon = on ? "power-off" : "times",
                    Text = on ? "On" : "Off",
                    Selected = on,
                    OnClick = () => c.Act("power"),
                },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: ThermoMachine.jsx:49  (animated, tickWhileDragging, stepPixelSize 3: the kit's field commits on Enter, as the air alarm's)
                    new UiNode(UiKind.LabeledItem) { Label = "Target Temperature" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "target",
                            CommitOnly = true,
                            Value = JsRound(target),
                            Unit = "K",
                            Width = "62px",
                            MinValue = JsRound(min),
                            MaxValue = JsRound(max),
                            Step = 5,
                            StepPixelSize = 3,
                            OnChange = value => c.Act("target", ("target", value)),
                        }),
                    // tg: ThermoMachine.jsx:66
                    new UiNode(UiKind.LabeledItem) { Label = "Presets" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "min",
                            Icon = "fast-backward",
                            Disabled = target == min,
                            Tooltip = "Minimum temperature",
                            OnClick = () => c.Act("target", ("target", min)),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "initial",
                            Icon = "sync",
                            Disabled = target == initial,
                            Tooltip = "Room Temperature",
                            OnClick = () => c.Act("target", ("target", initial)),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "max",
                            Icon = "fast-forward",
                            Disabled = target == max,
                            Tooltip = "Maximum Temperature",
                            OnClick = () => c.Act("target", ("target", max)),
                        }))));
    }
}
