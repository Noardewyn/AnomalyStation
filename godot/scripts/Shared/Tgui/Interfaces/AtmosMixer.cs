using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AtmosMixer.tsx (package E1: the gas mixer's window, the
// supermatter loop's mixers). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AtmosMixer
{
    // tg: tgui/packages/tgui/interfaces/AtmosMixer.tsx:20
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool on = data.Truthy("on");
        double set_pressure = data.Num("set_pressure");
        double max_pressure = data.Num("max_pressure");
        double node1_concentration = data.Num("node1_concentration");
        double node2_concentration = data.Num("node2_concentration");

        // tg: AtmosMixer.tsx:35  <Window width={370} height={165}>
        return c.Window(370, 165, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: AtmosMixer.tsx:39
                    new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Icon = on ? "power-off" : "times",
                            Text = on ? "On" : "Off",
                            Selected = on,
                            OnClick = () => c.Act("power"),
                        }),
                    // tg: AtmosMixer.tsx:48
                    new UiNode(UiKind.LabeledItem) { Label = "Output Pressure" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "pressure",
                            Value = set_pressure,
                            Unit = "kPa",
                            Width = "75px",
                            MinValue = 0,
                            MaxValue = max_pressure,
                            Step = 10,
                            OnChange = value => c.Act("pressure", ("pressure", value)),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "pressure-max",
                            Icon = "plus",
                            Text = "Max",
                            Disabled = set_pressure == max_pressure,
                            OnClick = () => c.Act("pressure", ("pressure", "max")),
                        }),
                    // tg: AtmosMixer.tsx:70
                    new UiNode(UiKind.LabeledItem) { Label = "Main Node", LabelColor = "green" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "node1",
                            Value = node1_concentration,
                            Step = 1,
                            Unit = "%",
                            Width = "60px",
                            MinValue = 0,
                            MaxValue = 100,
                            StepPixelSize = 2,
                            OnChange = value => c.Act("node1", ("concentration", value)),
                        }),
                    // tg: AtmosMixer.tsx:88
                    new UiNode(UiKind.LabeledItem) { Label = "Side Node", LabelColor = "blue" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "node2",
                            Value = node2_concentration,
                            Step = 1,
                            Unit = "%",
                            Width = "60px",
                            MinValue = 0,
                            MaxValue = 100,
                            StepPixelSize = 2,
                            OnChange = value => c.Act("node2", ("concentration", value)),
                        }))));
    }
}
