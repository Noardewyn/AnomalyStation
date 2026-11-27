using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AdminBookViewer.tsx: an archived book's text for the
// admins, raw or through the markdown renderer, which draws its source text as MarkdownViewer does. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AdminBookViewer
{
    // tg: AdminBookViewer.tsx:12
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        return c.Window(400, 400, "Reading: " + data.Str("title"),
            new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(
                new UiNode(UiKind.Box) { Key = data.Truthy("view_raw") ? "raw" : "markdown", Wrap = true, Text = data.Str("content") }));
    }
}
