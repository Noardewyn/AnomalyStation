using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BloodFilter.tsx (gaps J5, D298: the blood filter's whitelist).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BloodFilter
{
    // tg: BloodFilter.tsx:11
    public static UiNode Render(TguiContext c) =>
        c.Window(500, 300, null,
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(ChemFilter.ChemFilterPane(c, "Whitelist", c.Data.Arr("whitelist"), "green"))));
}
