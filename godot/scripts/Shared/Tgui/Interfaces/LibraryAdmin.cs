using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LibraryAdmin/ (index.tsx, BookListing.tsx, Search.tsx,
// Modify.tsx): the admins' archive console. The search over the archive with the uploader's ckey, the raw and deleted toggles,
// each book's view and delete or restore button; the modify page asks the reason, shows the book's moderation history (newest
// first, coloured by action) and sends the hide or unhide. The modify method and target are the window's own state, as tg's
// ModifyState context. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LibraryAdmin
{
    // tg: LibraryAdmin/types.ts:24  ModifyTypes
    private const string Delete = "delete";
    private const string Restore = "restore";

    // tg: LibraryAdmin/index.tsx:8
    public static UiNode Render(TguiContext c)
    {
        c.WindowTheme = "admin";
        string modifyMethod = c.UseState("modifyMethod", "");
        return c.Window(800, 600, "Admin Library Console", modifyMethod != "" ? ModifyPage(c) : BookListing(c));
    }

    // tg: LibraryAdmin/BookListing.tsx:9
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

    private static void SetModify(TguiContext c, string method, double target)
    {
        c.SetState("modifyTarget", target);
        c.SetState("modifyMethod", method);
    }

    // tg: LibraryAdmin/Search.tsx:24
    private static UiNode SearchAndDisplay(TguiContext c)
    {
        UiValue data = c.Data;
        bool canDbRequest = data.Truthy("can_db_request");
        bool viewRaw = data.Truthy("view_raw");
        bool showDeleted = data.Truthy("show_deleted");
        string authorCkey = data.Str("author_ckey");
        UiNode fields = LibraryConsole.SearchFields(c, withCategoryWidth: false).With(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
        {
            Key = "search_ckey",
            TextValue = authorCkey,
            Tooltip = authorCkey != "" ? authorCkey : "Ckey",
            OnCommit = value => c.Act("set_search_ckey", ("ckey", value)),
        }));
        var buttons = new UiNode(UiKind.Stack) { Key = "buttons", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "refresh_item" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "refresh", Disabled = !canDbRequest, TextAlign = "right", Color = data.Truthy("params_changed") ? "good" : "", Icon = "rotate-right", Text = "Refresh",
                    OnClick = () => c.Act("refresh"),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "clear_data", Disabled = !canDbRequest, TextAlign = "right", Color = "bad", Icon = "fire", Text = "Reset Search", OnClick = () => c.Act("clear_data"),
                }),
            new UiNode(UiKind.StackItem) { Key = "toggles" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "toggle_raw", TextAlign = "right", Color = viewRaw ? "purple" : "blue", Icon = viewRaw ? "theater-masks" : "glasses", Text = viewRaw ? "Raw" : "Normal",
                    OnClick = () => c.Act("toggle_raw"),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "toggle_deleted", TextAlign = "right", Color = showDeleted ? "purple" : "green", Icon = showDeleted ? "trash" : "mountain-sun", Text = showDeleted ? "All" : "Undeleted",
                    OnClick = () => c.Act("toggle_deleted"),
                }));

        var table = new UiNode(UiKind.Table) { Key = "books" }.With(new UiNode(UiKind.TableRow) { Key = "header" }.With(
            new UiNode(UiKind.TableCell) { Key = "number", FontSize = "1.5rem", Text = "#" },
            new UiNode(UiKind.TableCell) { Key = "category", FontSize = "1.5rem", Text = "Category" },
            new UiNode(UiKind.TableCell) { Key = "title", FontSize = "1.5rem", Text = "Title" },
            new UiNode(UiKind.TableCell) { Key = "author", FontSize = "1.5rem", Text = "Author" },
            new UiNode(UiKind.TableCell) { Key = "ckey", FontSize = "1.5rem", Text = "C-Key" },
            new UiNode(UiKind.TableCell) { Key = "modify", FontSize = "1.5rem", Text = "Un/Delete" }));
        int index = 0;
        foreach (UiValue book in data.Arr("pages"))
        {
            double id = book.Num("id");
            UiValue bookId = book.Field("id");
            table.With(new UiNode(UiKind.TableRow) { Key = "book:" + index++ }.With(
                new UiNode(UiKind.TableCell) { Key = "id" }.With(new UiNode(UiKind.Button)
                {
                    Key = "view:" + book.Str("id"), Icon = "book-reader", Text = book.Str("id"), OnClick = () => c.Act("view_book", ("book_id", bookId)),
                }),
                new UiNode(UiKind.TableCell) { Key = "category", Text = book.Str("category") },
                new UiNode(UiKind.TableCell) { Key = "title", Text = book.Str("title") },
                new UiNode(UiKind.TableCell) { Key = "author", Text = book.Str("author") },
                new UiNode(UiKind.TableCell) { Key = "ckey", Text = book.Str("author_ckey") },
                new UiNode(UiKind.TableCell) { Key = "modify" }.With(book.Truthy("deleted")
                    ? new UiNode(UiKind.Button)
                    {
                        Key = "restore:" + book.Str("id"), Icon = "undo", Color = "blue", Text = "Restore",
                        OnClick = () =>
                        {
                            SetModify(c, Restore, id);
                            c.Act("get_history", ("book_id", bookId));
                        },
                    }
                    : new UiNode(UiKind.Button)
                    {
                        Key = "delete:" + book.Str("id"), Icon = "hammer", Color = "violet", Text = "Delete",
                        OnClick = () =>
                        {
                            SetModify(c, Delete, id);
                            c.Act("get_history", ("book_id", bookId));
                        },
                    })));
        }
        return new UiNode(UiKind.Section) { Key = "query" }.With(
            new UiNode(UiKind.Stack) { Key = "row", Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Key = "fields" }.With(fields),
                new UiNode(UiKind.StackItem) { Key = "buttons" }.With(buttons)),
            table);
    }

    // tg: LibraryAdmin/Modify.tsx:16
    private static string GetActionColor(string reason) => reason switch
    {
        "deleted" => "rgba(255, 0, 0, 0.4)",
        "undeleted" => "rgba(0, 120, 70, 0.4)",
        _ => "rgba(0, 0, 0, 0.4)",
    };

    // tg: LibraryAdmin/Modify.tsx:27
    private static UiNode ModifyPage(TguiContext c)
    {
        UiValue data = c.Data;
        bool viewRaw = data.Truthy("view_raw");
        string modifyMethod = c.UseState("modifyMethod", "");
        double modifyTarget = c.UseState("modifyTarget", 0.0);
        string targetKey = TguiFormat.JsNumber(modifyTarget);
        List<UiValue> entries = data.Field("history")?.Get(targetKey) is { Kind: UiValueKind.Array } history
            ? history.Items.OrderByDescending(e => e.Num("id")).ToList()
            : new List<UiValue>();

        var table = new UiNode(UiKind.Table) { Key = "history" }.With(new UiNode(UiKind.TableRow) { Key = "header", Bold = true }.With(
            new UiNode(UiKind.TableCell) { Key = "id", Bold = true, Text = "ID" },
            new UiNode(UiKind.TableCell) { Key = "action", Bold = true, Text = "Action" },
            new UiNode(UiKind.TableCell) { Key = "reason", Bold = true, Text = "Reason" },
            new UiNode(UiKind.TableCell) { Key = "ckey", Bold = true, Text = "Admin Key" },
            new UiNode(UiKind.TableCell) { Key = "datetime", Bold = true, Text = "Datetime" }));
        foreach (UiValue entry in entries)
        {
            string color = GetActionColor(entry.Str("action"));
            table.With(new UiNode(UiKind.TableRow) { Key = "entry:" + entry.Str("id") }.With(
                new UiNode(UiKind.TableCell) { Key = "id" }.With(new UiNode(UiKind.Box) { Key = "id", Background = color, Text = entry.Str("id") }),
                new UiNode(UiKind.TableCell) { Key = "action" }.With(new UiNode(UiKind.Box) { Key = "action", Background = color, Text = TguiFormat.Capitalize(entry.Str("action")) }),
                new UiNode(UiKind.TableCell) { Key = "reason" }.With(new UiNode(UiKind.Box) { Key = "reason", Background = color, Wrap = true, Text = entry.Str("reason") }),
                new UiNode(UiKind.TableCell) { Key = "ckey" }.With(new UiNode(UiKind.Box) { Key = "ckey", Background = color, Text = entry.Str("ckey") }),
                new UiNode(UiKind.TableCell) { Key = "datetime" }.With(new UiNode(UiKind.Box) { Key = "datetime", Background = color, Text = entry.Str("datetime") })));
        }

        return new UiNode(UiKind.Section) { Key = "modify-page", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.NoticeBox) { Key = "heads-up", Text = "Heads Up! We do not allow you to fully delete books in game\nWhat you're doing here is a \"don't show this to anyone\" button\nIf you for whatever reason need to fully wipe a book, please speak to your database administrator" },
            new UiNode(UiKind.Stack) { Key = "question" }.With(
                new UiNode(UiKind.StackItem) { Key = "why", FontSize = "25px", Text = "Why do you want to " + modifyMethod + " this book?" },
                new UiNode(UiKind.StackItem) { Key = "view", FontSize = "17px" }.With(new UiNode(UiKind.Button)
                {
                    Key = "view_book", Icon = "book-reader", Text = "View", OnClick = () => c.Act("view_book", ("book_id", modifyTarget)),
                }),
                new UiNode(UiKind.StackItem) { Key = "raw", FontSize = "17px" }.With(new UiNode(UiKind.Button)
                {
                    Key = "toggle_raw", TextAlign = "right", Color = viewRaw ? "purple" : "blue", Icon = viewRaw ? "theater-masks" : "glasses", Text = viewRaw ? "Raw" : "Normal",
                    OnClick = () => c.Act("toggle_raw"),
                })),
            new UiNode(UiKind.TextArea) { Key = "reason", Fluid = true, Height = "20vh", Width = "100%", Background = "black", Color = "white", OnInput = value => c.Local.SetQuiet("reason", value) },
            new UiNode(UiKind.Stack) { Key = "buttons", Justify = "center", Align = "center" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "modify",
                    Disabled = !data.Truthy("can_db_request"),
                    Icon = "upload",
                    FontSize = "18px",
                    Color = "good",
                    Text = TguiFormat.Capitalize(modifyMethod),
                    OnClick = () =>
                    {
                        string why = c.UseState("reason", "null");
                        switch (modifyMethod)
                        {
                            case Delete:
                                c.Act("hide_book", ("book_id", modifyTarget), ("delete_reason", why));
                                break;
                            case Restore:
                                c.Act("unhide_book", ("book_id", modifyTarget), ("free_reason", why));
                                break;
                        }
                        SetModify(c, "", 0);
                    },
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "return", Icon = "times", FontSize = "18px", Color = "bad", Text = "Return", OnClick = () => SetModify(c, "", 0),
                })),
            table);
    }
}
