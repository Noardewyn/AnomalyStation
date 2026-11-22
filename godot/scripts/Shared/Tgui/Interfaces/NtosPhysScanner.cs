using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosPhysScanner.jsx (gaps S4c, D327): the Physical
// Scanner, the last health scan's text (tg's sanitized HTML, drawn as the chat's HTML subset). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosPhysScanner
{
    // tg: NtosPhysScanner.jsx:7
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 600, 350, null, new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(
            new UiNode(UiKind.Section) { Key = "hint", Text = "Tap something (right-click) with your tablet to use the physical scanner." },
            new UiNode(UiKind.Section) { Key = "result" }.With(
                new UiNode(UiKind.Box) { Key = "title", Bold = true, Text = "LAST SAVED RESULT" },
                new UiNode(UiKind.Html) { Key = "record", TextValue = c.Data.Str("last_record") })));
}
