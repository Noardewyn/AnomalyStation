using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/GasAnalyzer.tsx (M7 slice T3, D164).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class GasAnalyzer
{
    // tg: tgui/packages/tgui/interfaces/GasAnalyzer.tsx:16  GasAnalyzerContent (NtosGasAnalyzer shows it too)
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        var mixes = new UiNode(UiKind.Fragment);
        foreach (UiValue gasmix in data.Arr("gasmixes"))
        {
            mixes.With(new UiNode(UiKind.Section) { Key = "mix:" + gasmix.Str("reference"), Title = gasmix.Str("name") }.With(
                GasmixParser.Render(gasmix, id => AtmosHandbook.SetActiveGas(c, id), id => AtmosHandbook.SetActiveReaction(c, id))));
        }
        return new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "mixes" }.With(mixes),
            new UiNode(UiKind.StackItem) { Key = "handbook", Grow = 1 }.With(AtmosHandbook.Content(c)));
    }

    // tg: tgui/packages/tgui/interfaces/GasAnalyzer.tsx:30  GasAnalyzer, <Window width={500} height={500}>
    public static UiNode Render(TguiContext c) => c.Window(500, 500, null, Content(c));
}
