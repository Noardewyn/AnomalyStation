using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemDisposer.tsx (engineering package E15, plumbing).
// The window reads tg's ui_data and ui_static_data of the chemical disposer (destroyer.dm:58, :63) and sends toggle_power and
// change_volume. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemDisposer
{
    // tg: ChemDisposer.tsx:18
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool enabled = data.Truthy("enabled");
        // tg: ChemDisposer.tsx:23  <Window width={320} height={105}>
        return c.Window(320, 105, null,
            new UiNode(UiKind.Section)
            {
                Title = "Control Panel",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "power",
                    Icon = "power-off",
                    Selected = enabled,
                    Text = enabled ? "On" : "Off",
                    OnClick = () => c.Act("toggle_power"),
                },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Volume" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "volume",
                            Value = data.Num("disposal_rate"),
                            Unit = "u",
                            Width = "50px",
                            MinValue = 0.1,
                            MaxValue = data.Num("max_volume"),
                            Step = 1,
                            StepPixelSize = 2,
                            OnChange = value => c.Act("change_volume", ("volume", value)),
                        }))));
    }
}
