using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AtmosTempGate.tsx (the M7 slice D172: the temperature gate's window, which the
// rapid pipe dispenser's atmospherics tab builds). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AtmosTempGate
{
    // tg: tgui/packages/tgui/interfaces/AtmosTempGate.tsx:19
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool on = data.Truthy("on");
        double temperature = data.Num("temperature");
        double maxTemperature = data.Num("max_temperature");
        double minTemperature = data.Num("min_temperature");

        // tg: AtmosTempGate.tsx:23  <Window width={335} height={115}>
        return c.Window(335, 115, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: AtmosTempGate.tsx:27
                    new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Icon = on ? "power-off" : "times",
                            Text = on ? "On" : "Off",
                            Selected = on,
                            OnClick = () => c.Act("power"),
                        }),
                    // tg: AtmosTempGate.tsx:35
                    new UiNode(UiKind.LabeledItem) { Label = "Heat settings" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "temperature",
                            Value = temperature,
                            Unit = "K",
                            Width = "75px",
                            MinValue = minTemperature,
                            MaxValue = maxTemperature,
                            Step = 1,
                            OnChange = value => c.Act("temperature", ("temperature", value)),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "temperature-max",
                            Icon = "plus",
                            Text = "Max",
                            Disabled = temperature == maxTemperature,
                            OnClick = () => c.Act("temperature", ("temperature", "max")),
                        }))));
    }
}
