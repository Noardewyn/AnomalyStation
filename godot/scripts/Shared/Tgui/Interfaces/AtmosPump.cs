using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AtmosPump.tsx (the M7 supermatter slice, part 2:
// the window of the pressure and volume pumps, MetaStation's engine loop). Original copyright and license: tgstation, AGPL-3.0
// (code). See ../tgstation/LICENSE.

public static class AtmosPump
{
    // tg: tgui/packages/tgui/interfaces/AtmosPump.tsx:20
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool on = data.Truthy("on");
        double maxRate = data.Num("max_rate");
        double maxPressure = data.Num("max_pressure");
        double rate = data.Num("rate");
        double pressure = data.Num("pressure");

        // tg: AtmosPump.tsx:25  <Window width={335} height={115}>
        return c.Window(335, 115, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: AtmosPump.tsx:29
                    new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Icon = on ? "power-off" : "times",
                            Text = on ? "On" : "Off",
                            Selected = on,
                            OnClick = () => c.Act("power"),
                        }),
                    // tg: AtmosPump.tsx:37  {max_rate ? (Transfer Rate) : (Output Pressure)}
                    maxRate != 0
                        ? new UiNode(UiKind.LabeledItem) { Label = "Transfer Rate" }.With(
                            new UiNode(UiKind.NumberInput)
                            {
                                Key = "rate",
                                Value = rate,
                                Step = 1,
                                Width = "63px",
                                Unit = "L/s",
                                MinValue = 0,
                                MaxValue = maxRate,
                                OnChange = value => c.Act("rate", ("rate", value)),
                            },
                            new UiNode(UiKind.Button)
                            {
                                Key = "rate-max",
                                Icon = "plus",
                                Text = "Max",
                                Disabled = rate == maxRate,
                                OnClick = () => c.Act("rate", ("rate", "max")),
                            })
                        : new UiNode(UiKind.LabeledItem) { Label = "Output Pressure" }.With(
                            new UiNode(UiKind.NumberInput)
                            {
                                Key = "pressure",
                                Value = pressure,
                                Unit = "kPa",
                                Width = "75px",
                                MinValue = 0,
                                MaxValue = maxPressure,
                                Step = 10,
                                OnChange = value => c.Act("pressure", ("pressure", value)),
                            },
                            new UiNode(UiKind.Button)
                            {
                                Key = "pressure-max",
                                Icon = "plus",
                                Text = "Max",
                                Disabled = pressure == maxPressure,
                                OnClick = () => c.Act("pressure", ("pressure", "max")),
                            }))));
    }
}
