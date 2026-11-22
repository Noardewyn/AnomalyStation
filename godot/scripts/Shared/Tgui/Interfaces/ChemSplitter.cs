using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemSplitter.tsx (engineering package E15, plumbing).
// The window reads tg's ui_data and ui_static_data of the chemical splitter (splitters.dm:29, :34) and sends set_amount for each of
// its three outputs. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemSplitter
{
    // tg: ChemSplitter.tsx:14
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double max_transfer = data.Num("max_transfer");
        // tg: ChemSplitter.tsx:19  <Window width={270} height={140}>
        return c.Window(270, 140, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    Output(c, "Straight", "straight", data.Num("straight"), max_transfer),
                    Output(c, "Left", "left", data.Num("left"), max_transfer),
                    Output(c, "Right", "right", data.Num("right"), max_transfer))));
    }

    // tg: ChemSplitter.tsx:23, :40, :57  the three identical NumberInputs
    private static UiNode Output(TguiContext c, string label, string target, double value, double max_transfer) =>
        new UiNode(UiKind.LabeledItem) { Label = label }.With(
            new UiNode(UiKind.NumberInput)
            {
                Key = target,
                Value = value,
                Unit = "u",
                Width = "80px",
                MinValue = 1,
                MaxValue = max_transfer,
                Format = v => TguiFormat.ToFixed(v, 2),
                Step = 0.05,
                StepPixelSize = 4,
                OnChange = v => c.Act("set_amount", ("target", target), ("amount", v)),
            });
}
