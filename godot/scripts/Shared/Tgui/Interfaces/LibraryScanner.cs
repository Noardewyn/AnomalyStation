using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LibraryScanner.jsx: the scanner's cached book (its
// author as the section's title, its title inside) with the eject, scan and clear buttons. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LibraryScanner
{
    // tg: LibraryScanner.jsx:6
    public static UiNode Render(TguiContext c) => c.Window(350, 150, "Library Scanner", BookScanning(c));

    // tg: LibraryScanner.jsx:14
    private static UiNode BookScanning(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasBook = data.Truthy("has_book");
        bool hasCache = data.Truthy("has_cache");
        if (!hasBook && !hasCache)
            return new UiNode(UiKind.NoticeBox) { Key = "insert", Text = "Insert a book to scan" };
        UiValue book = data.Field("book");
        return new UiNode(UiKind.Stack) { Key = "scanning", Vertical = true, Height = "100%", Justify = "flex-end" }.With(
            new UiNode(UiKind.StackItem) { Key = "book", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "cache", TextAlign = "center", Height = "100%", Title = book.Str("author") }.With(
                new UiNode(UiKind.Box) { Key = "title", TextAlign = "center", Text = book.Str("title") })),
            new UiNode(UiKind.StackItem) { Key = "buttons" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "eject", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "eject", Fluid = true, TextAlign = "center", Icon = "eject", Disabled = !hasBook, Text = "Eject Book", OnClick = () => c.Act("eject"),
                }),
                new UiNode(UiKind.StackItem) { Key = "scan", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "scan", Fluid = true, TextAlign = "center", Color = "good", Icon = "qrcode", Disabled = !hasBook, Text = "Scan Book", OnClick = () => c.Act("scan"),
                }),
                new UiNode(UiKind.StackItem) { Key = "clear", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "clear", Fluid = true, TextAlign = "center", Icon = "fire", Color = "bad", Disabled = !hasCache, Text = "Clear Cache", OnClick = () => c.Act("clear"),
                }))));
    }
}
