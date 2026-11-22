using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemAutomaticBuffer.tsx (engineering package E15,
// plumbing). The window reads tg's ui_data of the automatic buffer (plumbing_buffer.dm:139) and sends set_threshold and sync.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemAutomaticBuffer
{
    // tg: ChemAutomaticBuffer.tsx:10
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double connections = data.Num("connections");
        // tg: ChemAutomaticBuffer.tsx:14  <Window width={250} height={120}>
        return c.Window(250, 120, null,
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Threshold" }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "threshold",
                        MinValue = 1,
                        MaxValue = 200,
                        Value = data.Num("threshold"),
                        Step = 1,
                        OnChange = value => c.Act("set_threshold", ("threshold", value)),
                    }),
                // tg: {connections || 'N/A'}
                new UiNode(UiKind.LabeledItem) { Label = "Connections", Text = connections != 0 ? TguiFormat.JsNumber(connections) : "N/A" },
                new UiNode(UiKind.LabeledItem) { Label = "Sync" }.With(
                    new UiNode(UiKind.Button) { Key = "sync", Text = "Sync Values", OnClick = () => c.Act("sync") })));
    }
}
