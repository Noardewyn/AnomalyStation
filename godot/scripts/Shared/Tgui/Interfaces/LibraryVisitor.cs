using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LibraryVisitor.jsx: the visitor console's archive
// search (the id, category, title and author fields, the search and reset buttons, the listing) with the page selector of the
// LibraryConsole window, or tg's notice when the archive cannot be reached. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LibraryVisitor
{
    // tg: LibraryVisitor.jsx:18
    public static UiNode Render(TguiContext c) => c.Window(702, 421, "Library Lookup Console", BookListing(c));

    // tg: LibraryVisitor.jsx:26
    private static UiNode BookListing(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("can_connect"))
            return new UiNode(UiKind.NoticeBox) { Key = "no-connection", Text = "Unable to retrieve book listings. Please contact your system administrator for assistance." };
        return new UiNode(UiKind.Stack) { Key = "listing", Fill = true, Vertical = true, Justify = "space-between" }.With(
            new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(SearchAndDisplay(c))),
            new UiNode(UiKind.StackItem) { Key = "pages", Align = "center" }.With(LibraryConsole.PageSelect(c, "pages", 1, data.Num("page_count"), data.Num("our_page"),
                !data.Truthy("can_db_request"), value => c.Act("switch_page", ("page", value)))));
    }

    // tg: LibraryVisitor.jsx:62
    private static UiNode SearchAndDisplay(TguiContext c)
    {
        var table = new UiNode(UiKind.Table) { Key = "records" }.With(new UiNode(UiKind.TableRow) { Key = "header" }.With(
            new UiNode(UiKind.TableCell) { Key = "number", FontSize = "1.5rem", Text = "#" },
            new UiNode(UiKind.TableCell) { Key = "category", FontSize = "1.5rem", Text = "Category" },
            new UiNode(UiKind.TableCell) { Key = "title", FontSize = "1.5rem", Text = "Title" },
            new UiNode(UiKind.TableCell) { Key = "author", FontSize = "1.5rem", Text = "Author" }));
        int index = 0;
        foreach (UiValue record in c.Data.Arr("pages"))
        {
            table.With(new UiNode(UiKind.TableRow) { Key = "record:" + index++ }.With(
                new UiNode(UiKind.TableCell) { Key = "id", Text = record.Str("id") },
                new UiNode(UiKind.TableCell) { Key = "category", Text = record.Str("category") },
                new UiNode(UiKind.TableCell) { Key = "title", Text = record.Str("title") },
                new UiNode(UiKind.TableCell) { Key = "author", Text = record.Str("author") }));
        }
        return new UiNode(UiKind.Section) { Key = "query" }.With(
            new UiNode(UiKind.Stack) { Key = "row", Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Key = "fields" }.With(LibraryConsole.SearchFields(c, withCategoryWidth: false)),
                LibraryConsole.SearchButtons(c)),
            table);
    }
}
