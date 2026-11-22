using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosBotanicalEncyclopedia.tsx (gaps S4c, D327): the
// Botanical Encyclopedia, the seed extractor's SeedTable over every plant. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosBotanicalEncyclopedia
{
    // tg: NtosBotanicalEncyclopedia.tsx:12
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 800, 700, null, new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(
            SeedTable.Render(c, c.Data.Arr("seeds"), c.Data.Arr("trait_db"), c.Data.Num("cycle_seconds"))));
}
