using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MarkdownViewer.tsx (gaps J2, D298: a book's window).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MarkdownViewer
{
    // tg: tgui/packages/tgui/interfaces/MarkdownViewer.tsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        c.WindowTheme = "paper";
        // tg: MarkdownViewer.tsx:16  <Window theme="paper" title={data.title} width={420} height={500}>; marked's rendered html is drawn as
        // its source text (the kit has no html node; a book's own content here is plain).
        return c.Window(420, 500, data.Str("title"),
            new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(
                new UiNode(UiKind.Box) { Text = data.Str("content") }));
    }
}
