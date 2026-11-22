using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SeedExtractor.tsx (gaps package P2). The window reads
// the seed extractor's ui_data (seed_extractor.dm:219: the seed piles) and ui_static_data (the trait table, the cycle length) and
// sends scrap and take. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SeedExtractor
{
    // tg: SeedExtractor.tsx:12
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: SeedExtractor.tsx:16  <Window width={900} height={500}><Window.Content scrollable>
        return c.Window(900, 500, null,
            SeedTable.Render(c, data.Arr("seeds"), data.Arr("trait_db"), data.Num("cycle_seconds"), item => new[]
            {
                // tg: SeedExtractor.tsx:23
                new UiNode(UiKind.Button)
                {
                    Key = "scrap:" + item.Str("key"),
                    Icon = "trash",
                    Text = "Scrap",
                    Color = "bad",
                    OnClick = () => c.Act("scrap", ("item", item.Str("key"))),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "take:" + item.Str("key"),
                    Icon = "eject",
                    Text = "Take",
                    OnClick = () => c.Act("take", ("item", item.Str("key"))),
                },
            }));
    }
}
