using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemAcclimator.tsx (engineering package E15, plumbing).
// The window reads tg's ui_data of the chemical acclimator (acclimator.dm:94) and sends set_target_temperature and change_volume.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemAcclimator
{
    // tg: ChemAcclimator.tsx:13
    private static readonly string[] States = { "Filling", "Heating", "Cooling", "Emptying" };

    // tg: ChemAcclimator.tsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int state = (int)data.Num("acclimate_state", -1);
        // tg: ChemAcclimator.tsx:20  <Window width={320} height={130}>
        return c.Window(320, 130, null,
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Current Temperature", Text = TguiFormat.JsNumber(data.Num("chem_temp")) + " K" },
                new UiNode(UiKind.LabeledItem) { Label = "Target Temperature" }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "target",
                        Value = data.Num("target_temperature"),
                        Unit = "K",
                        Width = "59px",
                        MinValue = 0,
                        MaxValue = 1000,
                        Step = 5,
                        StepPixelSize = 2,
                        OnChange = value => c.Act("set_target_temperature", ("temperature", value)),
                    }),
                new UiNode(UiKind.LabeledItem) { Label = "Buffer" }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "buffer",
                        Value = data.Num("max_volume"),
                        Unit = "u",
                        Width = "50px",
                        MinValue = 1,
                        MaxValue = 200,
                        Step = 2,
                        StepPixelSize = 2,
                        OnChange = value => c.Act("change_volume", ("volume", value)),
                    }),
                // tg: States[acclimate_state]: undefined (nothing drawn) outside the four
                new UiNode(UiKind.LabeledItem) { Label = "Current State", Text = state >= 0 && state < States.Length ? States[state] : "" }));
    }
}
