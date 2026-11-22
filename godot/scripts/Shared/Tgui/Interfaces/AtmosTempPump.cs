using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AtmosTempPump.tsx (the M7 slice D172: the temperature pump's window, which the
// rapid pipe dispenser's atmospherics tab builds). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AtmosTempPump
{
    // tg: tgui/packages/tgui/interfaces/AtmosTempPump.tsx:19
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool on = data.Truthy("on");
        double rate = data.Num("rate");
        double maxRate = data.Num("max_heat_transfer_rate");

        // tg: AtmosTempPump.tsx:23  <Window width={335} height={115}>
        return c.Window(335, 115, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: AtmosTempPump.tsx:27
                    new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Icon = on ? "power-off" : "times",
                            Text = on ? "On" : "Off",
                            Selected = on,
                            OnClick = () => c.Act("power"),
                        }),
                    // tg: AtmosTempPump.tsx:35
                    new UiNode(UiKind.LabeledItem) { Label = "Heat transfer rate" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "rate",
                            Value = rate,
                            Unit = "%",
                            Width = "75px",
                            MinValue = 0,
                            MaxValue = maxRate,
                            Step = 1,
                            OnChange = value => c.Act("rate", ("rate", value)),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "rate-max",
                            Icon = "plus",
                            Text = "Max",
                            Disabled = rate == maxRate,
                            OnClick = () => c.Act("rate", ("rate", "max")),
                        }))));
    }
}
