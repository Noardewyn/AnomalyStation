using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LibraryConsole/ (index.tsx, components/PageSelect.tsx,
// PopoutMenu.tsx, ScrollableSection.tsx, Search.tsx, screens/Archive.tsx, Checkout.tsx, Forbidden.tsx, Inventory.tsx, Print.tsx,
// Upload.tsx): the book management console. The side menu of screens (the inventory, the check-outs with the loan modal, the
// archive's search, the upload of the scanner's cache with its modal, the printing of posters and the bible, the emagged forbidden
// lore), the page selector the visitor and admin consoles share. The modals' open flags are the window's own state, as tg's
// LibraryContext; a table cell's background colour is a box filling the cell. The bible's picture is the bibles sheet's class
// (the bible icon at seven times its size): the client draws it from its own copy of the bible's .dmi state. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LibraryConsole
{
    // tg: code/modules/library/bibles.dm:70  the bible's icon file, which tg's bibles sheet (asset_cache/assets/bibles.dm) scales up
    private const string BibleIcon = "icons/obj/storage/book.dmi";

    // tg: LibraryConsole/index.tsx:16
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool displayLore = data.Truthy("display_lore");
        int screenState = (int)data.Num("screen_state");
        c.WindowTheme = displayLore ? "spookyconsole" : null;

        UiNode screen = screenState switch
        {
            1 => Inventory(c),
            2 => Checkout(c),
            3 => Archive(c),
            4 => Upload(c),
            5 => Print(c),
            6 => Forbidden(c),
            _ => null,
        };
        return c.Window(880, 520, "Library Terminal", new UiNode(UiKind.Stack) { Key = "terminal", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "menu" }.With(PopoutMenu(c)),
            new UiNode(UiKind.StackItem) { Key = "screen", Grow = 1 }.With(screen)));
    }

    // tg: LibraryConsole/components/PageSelect.tsx:4  the first, previous, typed, next and last page; the typed page is the window's own
    // state (useState(current_page)), sent as typed text that the server reads as a number
    internal static UiNode PageSelect(TguiContext c, string key, double minimumPageCount, double pageCount, double currentPage, bool disabled, System.Action<object> callOnChange)
    {
        string pageKey = key + ":page";
        string page = c.UseState(pageKey, TguiFormat.JsNumber(currentPage));
        if (pageCount == 1)
            return null;
        return new UiNode(UiKind.Stack) { Key = key }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = key + ":first", Icon = "angle-double-left", Disabled = currentPage == minimumPageCount || disabled, OnClick = () => callOnChange(minimumPageCount),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = key + ":previous", Icon = "chevron-left", Disabled = currentPage == minimumPageCount || disabled, OnClick = () => callOnChange(currentPage - 1),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
            {
                Key = key + ":input",
                Tooltip = TguiFormat.JsNumber(currentPage) + "/" + TguiFormat.JsNumber(pageCount),
                TextValue = page,
                OnCommit = value =>
                {
                    if (value != "")
                    {
                        callOnChange(value);
                        c.SetState(pageKey, value);
                    }
                },
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = key + ":next", Icon = "chevron-right", Disabled = currentPage == pageCount || disabled, OnClick = () => callOnChange(currentPage + 1),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = key + ":last", Icon = "angle-double-right", Disabled = currentPage == pageCount || disabled, OnClick = () => callOnChange(pageCount),
            }));
    }

    // tg: LibraryConsole/components/PopoutMenu.tsx:6
    private static UiNode PopoutMenu(TguiContext c)
    {
        UiValue data = c.Data;
        bool displayLore = data.Truthy("display_lore");
        bool showDropdown = data.Truthy("show_dropdown");
        int screenState = (int)data.Num("screen_state");
        var stack = new UiNode(UiKind.Stack) { Key = "entries", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "toggle" }.With(new UiNode(UiKind.Button)
            {
                Key = "toggle_dropdown",
                Fluid = true,
                FontSize = "13px",
                Icon = data.Num("show_dropdown") == 1 ? "chevron-left" : "chevron-right",
                Tooltip = !showDropdown ? "Expand" : null,
                Text = showDropdown ? "Collapse" : null,
                OnClick = () => c.Act("toggle_dropdown"),
            }),
            PopoutEntry(c, 1, "list", "Inventory"),
            PopoutEntry(c, 2, "calendar", "Checkout"),
            PopoutEntry(c, 3, "server", "Archive"),
            PopoutEntry(c, 4, "upload", "Upload"),
            PopoutEntry(c, 5, "print", "Print"));
        if (displayLore)
            stack.With(PopoutEntry(c, 6, "question", screenState == 6 ? "Gur Fbeprere" : "Forbidden Lore", "black"));
        return new UiNode(UiKind.Section) { Key = "popout", Fill = true, Width = showDropdown ? "150px" : "36px" }.With(stack);
    }

    // tg: LibraryConsole/components/PopoutMenu.tsx:47  (the forbidden entry's copperplate font is the window's own)
    private static UiNode PopoutEntry(TguiContext c, int id, string icon, string text, string color = null)
    {
        UiValue data = c.Data;
        bool showDropdown = data.Truthy("show_dropdown");
        string selectedColor = color ?? "good";
        string deselectedColor = color ?? "";
        return new UiNode(UiKind.StackItem) { Key = "entry:" + id }.With(new UiNode(UiKind.Button)
        {
            Key = "screen:" + id,
            Fluid = true,
            FontSize = "13px",
            Color = id == (int)data.Num("screen_state") ? selectedColor : deselectedColor,
            Icon = icon,
            Tooltip = !showDropdown ? text : null,
            Text = showDropdown ? text : null,
            OnClick = () => c.Act("set_screen", ("screen_index", id)),
        });
    }

    // tg: LibraryConsole/components/ScrollableSection.tsx:9
    private static UiNode ScrollableSection(string key, string header, UiNode contents) =>
        new UiNode(UiKind.Stack) { Key = key, Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "header" }.With(new UiNode(UiKind.Section) { Key = "header", FontSize = "20px", TextAlign = "center", Color = "label" }.With(
                new UiNode(UiKind.Box) { Key = "header-text", FontSize = "20px", TextAlign = "center", Color = "label", Text = header })),
            new UiNode(UiKind.StackItem) { Key = "contents", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "contents", Fill = true, Scrollable = true }.With(contents)));

    // tg: LibraryConsole/components/Search.tsx:13  SearchAndDisplay: the search row over the results
    private static UiNode SearchAndDisplay(TguiContext c) =>
        new UiNode(UiKind.Stack) { Key = "search-display", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "tabs" }.With(SearchTabs(c)),
            new UiNode(UiKind.StackItem) { Key = "results", Grow = 1 }.With(SearchResults(c)));

    // tg: LibraryConsole/components/Search.tsx:26  the id, category, title and author fields (each sent when it loses the focus) and
    // the search and reset buttons; the visitor and admin consoles draw the same row
    internal static UiNode SearchFields(TguiContext c, bool withCategoryWidth = true)
    {
        UiValue data = c.Data;
        string bookIdText = data.Str("book_id", null);
        string title = data.Str("title");
        string author = data.Str("author");
        List<string> categories = data.Arr("search_categories").Select(v => v.AsStr()).ToList();
        return new UiNode(UiKind.Stack) { Key = "fields" }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
            {
                Key = "search_id",
                TextValue = bookIdText ?? "",
                Tooltip = bookIdText ?? "ID",
                Width = "70px",
                OnCommit = value => c.Act("set_search_id", ("id", value)),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Dropdown)
            {
                Key = "search_category",
                Width = withCategoryWidth ? "120px" : null,
                Options = categories,
                TextValue = data.Str("category"),
                OnSelected = value => c.Act("set_search_category", ("category", value)),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
            {
                Key = "search_title",
                TextValue = title,
                Tooltip = title != "" ? title : "Title",
                OnCommit = value => c.Act("set_search_title", ("title", value)),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
            {
                Key = "search_author",
                TextValue = author,
                Tooltip = author != "" ? author : "Author",
                OnCommit = value => c.Act("set_search_author", ("author", value)),
            }));
    }

    internal static UiNode SearchButtons(TguiContext c)
    {
        UiValue data = c.Data;
        bool canDbRequest = data.Truthy("can_db_request");
        return new UiNode(UiKind.StackItem) { Key = "buttons" }.With(
            new UiNode(UiKind.Button)
            {
                Key = "search",
                Disabled = !canDbRequest,
                TextAlign = "right",
                Color = data.Truthy("params_changed") ? "good" : "",
                Icon = "book",
                Text = "Search",
                OnClick = () => c.Act("search"),
            },
            new UiNode(UiKind.Button)
            {
                Key = "clear_data",
                Disabled = !canDbRequest,
                TextAlign = "right",
                Color = "bad",
                Icon = "fire",
                Text = "Reset Search",
                OnClick = () => c.Act("clear_data"),
            });
    }

    private static UiNode SearchTabs(TguiContext c) =>
        new UiNode(UiKind.Section) { Key = "search-tabs", Fill = true }.With(new UiNode(UiKind.Stack) { Justify = "space-between" }.With(
            new UiNode(UiKind.StackItem) { Key = "fields" }.With(SearchFields(c)),
            SearchButtons(c)));

    // tg: LibraryConsole/components/Search.tsx:124  the results, each id a button that prints the book
    private static UiNode SearchResults(TguiContext c)
    {
        var table = new UiNode(UiKind.Table) { Key = "results" }.With(new UiNode(UiKind.TableRow) { Key = "header", Candystripe = true }.With(
            new UiNode(UiKind.TableCell) { Key = "number", FontSize = "1.5rem", Text = "#" },
            new UiNode(UiKind.TableCell) { Key = "category", FontSize = "1.5rem", Text = "Category" },
            new UiNode(UiKind.TableCell) { Key = "title", FontSize = "1.5rem", Text = "Title" },
            new UiNode(UiKind.TableCell) { Key = "author", FontSize = "1.5rem", Text = "Author" }));
        int index = 0;
        foreach (UiValue record in c.Data.Arr("pages"))
        {
            string id = record.Str("id");
            table.With(new UiNode(UiKind.TableRow) { Key = "record:" + index++, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "id" }.With(new UiNode(UiKind.Button)
                {
                    Key = "print:" + id, Icon = "print", Text = id, OnClick = () => c.Act("print_book", ("book_id", record.Field("id"))),
                }),
                new UiNode(UiKind.TableCell) { Key = "category", Text = record.Str("category") },
                new UiNode(UiKind.TableCell) { Key = "title", Text = record.Str("title") },
                new UiNode(UiKind.TableCell) { Key = "author", Text = record.Str("author") }));
        }
        return new UiNode(UiKind.Section) { Key = "search-results", Fill = true, Scrollable = true }.With(table);
    }

    // tg: LibraryConsole/screens/Archive.tsx:8
    private static UiNode Archive(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("can_connect"))
            return new UiNode(UiKind.NoticeBox) { Key = "no-connection", Text = "Unable to retrieve book listings. Please contact your system administrator for assistance." };

        return new UiNode(UiKind.Stack) { Key = "archive_screen", Vertical = true, Justify = "space-between", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(SearchAndDisplay(c)),
            new UiNode(UiKind.StackItem) { Key = "pages", Align = "center" }.With(PageSelect(c, "archive-pages", 1, data.Num("page_count"), data.Num("our_page"),
                !data.Truthy("can_db_request"), value => c.Act("switch_page", ("page", value)))));
    }

    // tg: LibraryConsole/screens/Inventory.tsx:8
    private static UiNode Inventory(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("has_inventory"))
            return new UiNode(UiKind.NoticeBox) { Key = "no-inventory", Text = "No Book Records detected. Update your inventory!" };

        return new UiNode(UiKind.Stack) { Key = "inventory", Vertical = true, Justify = "space-between", Height = "100%" }.With(
            new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(ScrollableSection("inventory-section", "Library Inventory", InventoryDetails(c))),
            new UiNode(UiKind.StackItem) { Key = "pages", Align = "center" }.With(PageSelect(c, "inventory-pages", 1, data.Num("inventory_page_count"), data.Num("inventory_page"),
                false, value => c.Act("switch_inventory_page", ("page", value)))));
    }

    // tg: LibraryConsole/screens/Inventory.tsx:44
    private static UiNode InventoryDetails(TguiContext c)
    {
        var table = new UiNode(UiKind.Table) { Key = "inventory" }.With(new UiNode(UiKind.TableRow) { Key = "header", Bold = true, Candystripe = true }.With(
            new UiNode(UiKind.TableCell) { Key = "title", Bold = true, Text = "Title" },
            new UiNode(UiKind.TableCell) { Key = "author", Bold = true, Text = "Author" },
            new UiNode(UiKind.TableCell) { Key = "remove", Bold = true, TextAlign = "center", Text = "Remove" }));
        int index = 0;
        foreach (UiValue book in c.Data.Arr("inventory"))
        {
            string bookRef = book.Str("ref");
            table.With(new UiNode(UiKind.TableRow) { Key = "book:" + index++, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "title", Text = book.Str("title") },
                new UiNode(UiKind.TableCell) { Key = "author", Text = book.Str("author") },
                new UiNode(UiKind.TableCell) { Key = "remove" }.With(new UiNode(UiKind.Button)
                {
                    Key = "remove:" + bookRef, Color = "bad", Icon = "times", Text = "Clear Record", OnClick = () => c.Act("inventory_remove", ("book_id", bookRef)),
                })));
        }
        return table;
    }

    // tg: LibraryConsole/screens/Checkout.tsx:21
    private static UiNode Checkout(TguiContext c)
    {
        UiValue data = c.Data;
        bool checkoutBook = c.UseState("checkoutBook", false);
        var stack = new UiNode(UiKind.Stack) { Key = "checkout", Vertical = true, Height = "100%", Justify = "space-between" }.With(
            new UiNode(UiKind.StackItem) { Key = "listing", Grow = 1 }.With(new UiNode(UiKind.Stack) { Vertical = true, Height = "100%" }.With(
                new UiNode(UiKind.StackItem) { Key = "entries", Grow = 1 }.With(ScrollableSection("checkout-section", "Checked Out Books", CheckoutEntries(c))),
                new UiNode(UiKind.StackItem) { Key = "pages", Align = "center" }.With(PageSelect(c, "checkout-pages", 1, data.Num("checkout_page_count"), data.Num("checkout_page"),
                    false, value => c.Act("switch_checkout_page", ("page", value)))))),
            new UiNode(UiKind.StackItem) { Key = "check-out" }.With(new UiNode(UiKind.Button)
            {
                Key = "check-out-book", Fluid = true, Icon = "barcode", FontSize = "20px", Text = "Check-Out Book", OnClick = () => c.SetState("checkoutBook", true),
            }));
        if (checkoutBook)
            stack.With(CheckoutModal(c));
        return stack;
    }

    // tg: LibraryConsole/screens/Checkout.tsx:72
    private static UiNode CheckoutModal(TguiContext c)
    {
        UiValue data = c.Data;
        string checkoutTitle = data.Field("checkout_title") is UiValue t && t.Kind != UiValueKind.Null ? t.AsStr() : null;
        List<string> inventory = data.Arr("inventory").Select(book => book.Str("title")).ToList();
        string checkoutee = c.UseState("checkoutee", "Recipient");
        double checkoutPeriod = c.UseState("checkoutPeriod", 5.0);
        return new UiNode(UiKind.Modal) { Key = "checkout-modal", Width = "500px" }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "question", FontSize = "20px", Text = "Are you sure you want to loan out this book?" },
            new UiNode(UiKind.StackItem) { Key = "book" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "book_name",
                Width = "100%",
                TextValue = checkoutTitle,
                Tooltip = "Insert Book name...",
                Options = inventory,
                OnSelected = e => c.Act("set_checkout", ("book_name", e)),
            }),
            new UiNode(UiKind.StackItem) { Key = "loan" }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "loan-to", Label = "Loan To" }.With(new UiNode(UiKind.Input)
                {
                    Key = "loaned_to", Width = "160px", TextValue = checkoutee, OnInput = value => c.Local.SetQuiet("checkoutee", value),
                }),
                new UiNode(UiKind.LabeledItem) { Key = "loan-period", Label = "Loan Period" }.With(new UiNode(UiKind.NumberInput)
                {
                    Key = "checkout_time",
                    Value = checkoutPeriod,
                    Unit = " Minutes",
                    MinValue = 1,
                    MaxValue = 120,
                    Step = 1,
                    StepPixelSize = 10,
                    OnChange = value => c.SetState("checkoutPeriod", value),
                }))),
            new UiNode(UiKind.StackItem) { Key = "buttons" }.With(new UiNode(UiKind.Stack) { Justify = "center", Align = "center" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "loan-out",
                    Icon = "upload",
                    FontSize = "16px",
                    Color = "good",
                    Text = "Loan Out",
                    OnClick = () =>
                    {
                        c.SetState("checkoutBook", false);
                        c.Act("checkout", ("loaned_to", c.UseState("checkoutee", "Recipient")), ("checkout_time", c.UseState("checkoutPeriod", 5.0)));
                    },
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "return", Icon = "times", FontSize = "16px", Color = "bad", Text = "Return", OnClick = () => c.SetState("checkoutBook", false),
                })))));
    }

    // tg: LibraryConsole/screens/Checkout.tsx:166
    private static UiNode CheckoutEntries(TguiContext c)
    {
        IReadOnlyList<UiValue> checkouts = c.Data.Arr("checkouts");
        var table = new UiNode(UiKind.Table) { Key = "checkouts" }.With(new UiNode(UiKind.TableRow) { Key = "header", Bold = true, Candystripe = true }.With(
            new UiNode(UiKind.TableCell) { Key = "title", Bold = true, Text = "Title" },
            new UiNode(UiKind.TableCell) { Key = "author", Bold = true, Text = "Author" },
            new UiNode(UiKind.TableCell) { Key = "borrower", Bold = true, Text = "Borrower" },
            new UiNode(UiKind.TableCell) { Key = "time", Bold = true, Text = "Time Left" },
            new UiNode(UiKind.TableCell) { Key = "check-in", Bold = true, Text = "Check-In" }));
        if (checkouts.Count == 0)
        {
            table.With(new UiNode(UiKind.TableRow) { Key = "empty" }.With(new UiNode(UiKind.TableCell) { Key = "empty", TextAlign = "center" }.With(
                new UiNode(UiKind.NoticeBox) { Key = "no-checkouts", Text = "No books checked out." })));
            return table;
        }
        foreach (UiValue entry in checkouts)
        {
            string id = entry.Str("id");
            bool overdue = entry.Truthy("overdue");
            string checkoutRef = entry.Str("ref");
            table.With(new UiNode(UiKind.TableRow) { Key = "checkout:" + id, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "title", Text = entry.Str("title") },
                new UiNode(UiKind.TableCell) { Key = "author", Text = entry.Str("author") },
                new UiNode(UiKind.TableCell) { Key = "borrower", Text = entry.Str("borrower") },
                new UiNode(UiKind.TableCell) { Key = "time" }.With(new UiNode(UiKind.Box)
                {
                    Key = "due", Background = overdue ? "bad" : "good", Text = overdue ? "Overdue" : TguiFormat.JsNumber(entry.Num("due_in_minutes")) + " Minutes",
                }),
                new UiNode(UiKind.TableCell) { Key = "check-in", Width = "70px", TextAlign = "center" }.With(new UiNode(UiKind.Button)
                {
                    Key = "checkin:" + checkoutRef, Icon = "box-open", OnClick = () => c.Act("checkin", ("checked_out_id", checkoutRef)),
                })));
        }
        return table;
    }

    // tg: LibraryConsole/screens/Forbidden.tsx:4  (the rot13 text tg draws under the modal, in its cult font)
    private const string ForbiddenDescription =
        "Abf vqrnz cebprffhf pbzchgngvbanyvf fghqrer vapvcvrzhf\nCebprffhf pbzchgngvbanyrf fhag erf nofgenpgnr dhnr pbzchgngberf vapbyhag\nHg ribyihag, cebprffhf nyvn nofgenpgn dhnr qngn znavchyner qvphaghe\nRibyhgvbavf cebprffhf qvevtvghe cre rkrzcyhz erthynr cebtenzzngvf ibpngv\nUbzvarf cebtenzzngn nq cebprffhf erpgbf rssvpvhag\nEriren fcvevghf pbzchgngbevv phz vapnagnzragvf pbavhatvzhf\nCebprffhf pbzchgngvbanyvf rfg zhyghz fvzvyvf vqrnr irarsvpnr fcvevghf\nivqrev nhg gnatv aba cbgrfg\nAba rfg rk zngrevn pbzcbfvgn\nFrq vq cynpreng vcfhz\nAba cbgrfg bcrenev bchf vagryyrpghnyr\nErfcbaqrev cbgrfg\nZhaqhz nssvprer cbgrfg rebtnaqb crphavnz nq evcnz iry cre oenppuvhz \nebobgv snoevpnaqb zbqrenaqb\nPbafvyvvf hgvzhe cebprffvohf nhthenaqv fhag fvphg vapnagnzragn irarsvpvv";

    // tg: LibraryConsole/screens/Forbidden.tsx:7
    private static UiNode Forbidden(TguiContext c) =>
        new UiNode(UiKind.Box) { Key = "forbidden", Color = "#e00", Wrap = true, Text = ForbiddenDescription }.With(
            new UiNode(UiKind.Modal) { Key = "forbidden-modal" }.With(
                new UiNode(UiKind.Box) { Key = "vault", Color = "#e00", FontSize = "28px", Text = "Accessing Forbidden Lore Vault v 1.3:" },
                new UiNode(UiKind.Box) { Key = "sure", Color = "#e00", Text = "Are you absolutely sure you want to proceed?" },
                new UiNode(UiKind.Box) { Key = "responsibility", Color = "#e00", Bold = true, Text = "EldritchRelics Inc. will take no responsibility for this choice" },
                new UiNode(UiKind.Stack) { Key = "choice", Justify = "center", Align = "center" }.With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "lore_spawn", Fluid = true, Icon = "check", Color = "good", FontSize = "20px", Text = "Assent", OnClick = () => c.Act("lore_spawn"),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "lore_deny", Fluid = true, Icon = "times", Color = "bad", FontSize = "20px", Text = "Decline", OnClick = () => c.Act("lore_deny"),
                    }))));

    // tg: LibraryConsole/screens/Print.tsx:8
    private static UiNode Print(TguiContext c)
    {
        UiValue data = c.Data;
        List<string> posters = data.Arr("posters").Select(v => v.AsStr()).ToList();
        string selectedPoster = c.UseState("selectedPoster", posters.Count > 0 ? posters[0] : null);
        var tabs = new UiNode(UiKind.Tabs) { Key = "posters", Vertical = true };
        foreach (string poster in posters)
        {
            bool selected = selectedPoster == poster;
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "poster:" + poster, Selected = selected, Color = selected ? "good" : null, Text = poster, OnClick = () => c.SetState("selectedPoster", poster),
            });
        }
        string bibleSprite = data.Str("bible_sprite");
        string bibleState = bibleSprite.StartsWith("display-", System.StringComparison.Ordinal) ? bibleSprite["display-".Length..] : bibleSprite;
        return new UiNode(UiKind.Stack) { Key = "print", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "choices", Grow = 1 }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "poster-list", Width = "50%" }.With(new UiNode(UiKind.Section) { Key = "poster-section", Fill = true, Scrollable = true }.With(tabs)),
                new UiNode(UiKind.StackItem) { Key = "bible" }.With(new UiNode(UiKind.Stack) { Vertical = true, Height = "100%" }.With(
                    new UiNode(UiKind.StackItem) { Key = "bible_name", TextAlign = "center", FontSize = "25px", Bold = true, Color = "#0b94c4", Text = data.Str("bible_name") },
                    new UiNode(UiKind.StackItem) { Key = "deity", TextAlign = "center", FontSize = "22px", Color = "purple", Text = "In the Name of " + data.Str("deity") },
                    new UiNode(UiKind.StackItem) { Key = "religion", TextAlign = "center", FontSize = "22px", Color = "purple", Text = "For the Sake of " + data.Str("religion") },
                    new UiNode(UiKind.StackItem) { Key = "sprite", Align = "center" }.With(new UiNode(UiKind.Box)
                    {
                        Key = "bible_sprite", DmIcon = BibleIcon, DmIconState = bibleState, Width = "224px", Height = "224px",
                    }))))),
            new UiNode(UiKind.StackItem) { Key = "buttons" }.With(new UiNode(UiKind.Stack) { Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Key = "poster", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "print_poster", Fluid = true, Icon = "scroll", FontSize = "30px", TextAlign = "center", Text = "Poster",
                    OnClick = () => c.Act("print_poster", ("poster_name", selectedPoster)),
                }),
                new UiNode(UiKind.StackItem) { Key = "bible", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "print_bible", Fluid = true, Icon = "cross", FontSize = "30px", TextAlign = "center", Text = "Bible", OnClick = () => c.Act("print_bible"),
                }))));
    }

    // tg: LibraryConsole/screens/Upload.tsx:19
    private static UiNode Upload(TguiContext c)
    {
        UiValue data = c.Data;
        bool uploadToDB = c.UseState("uploadToDB", false);
        if (!data.Truthy("has_scanner"))
            return new UiNode(UiKind.NoticeBox) { Key = "no-scanner", Text = "No nearby scanner detected, construct one to continue." };
        if (!data.Truthy("has_cache"))
            return new UiNode(UiKind.NoticeBox) { Key = "no-cache", Text = "Scan in a book to upload." };

        bool activeNewscasterCooldown = data.Truthy("active_newscaster_cooldown");
        string cacheTitle = data.Str("cache_title");
        string cacheAuthor = data.Str("cache_author");
        var page = new UiNode(UiKind.Fragment) { Key = "upload-page" }.With(new UiNode(UiKind.Stack) { Key = "upload-stack", Vertical = true, Height = "100%" }.With(
            new UiNode(UiKind.StackItem) { Key = "heading" }.With(new UiNode(UiKind.Box) { Key = "heading", FontSize = "20px", TextAlign = "center", Text = "Current Scan Cache" }),
            new UiNode(UiKind.StackItem) { Key = "cache", Grow = 1 }.With(new UiNode(UiKind.Stack) { Vertical = true, Height = "100%" }.With(
                new UiNode(UiKind.StackItem) { Key = "fields" }.With(new UiNode(UiKind.Stack) { Justify = "center" }.With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "title-label", FontSize = "20px", Text = "Title:" }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
                    {
                        Key = "cache_title", FontSize = "20px", TextValue = cacheTitle, Tooltip = cacheTitle != "" ? cacheTitle : "Title", Width = "22rem",
                        OnCommit = value => c.Act("set_cache_title", ("title", value)),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "author-label", FontSize = "20px", Text = "Author:" }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
                    {
                        Key = "cache_author", FontSize = "20px", TextValue = cacheAuthor, Tooltip = cacheAuthor != "" ? cacheAuthor : "Author",
                        OnCommit = value => c.Act("set_cache_author", ("author", value)),
                    }))),
                new UiNode(UiKind.StackItem) { Key = "content", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true, FontSize = "15px", Title = "Content:" }.With(
                    new UiNode(UiKind.Html) { Key = "cache_content", TextValue = data.Str("cache_content") })))),
            new UiNode(UiKind.StackItem) { Key = "actions" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "newscaster", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "news_post",
                    Disabled = !activeNewscasterCooldown,
                    Fluid = true,
                    Tooltip = activeNewscasterCooldown
                        ? "Send your book to the station's newscaster's channel."
                        : "Please wait " + data.Str("cooldown_string") + " before sending your book to the newscaster!",
                    Icon = "newspaper",
                    FontSize = "30px",
                    TextAlign = "center",
                    Text = "Newscaster",
                    OnClick = () => c.Act("news_post"),
                }),
                new UiNode(UiKind.StackItem) { Key = "archive_item", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "archive",
                    Disabled = !data.Truthy("can_db_request"),
                    Fluid = true,
                    Icon = "server",
                    FontSize = "30px",
                    TextAlign = "center",
                    Text = "Archive",
                    OnClick = () => c.SetState("uploadToDB", true),
                })))));
        if (uploadToDB)
            page.With(UploadModal(c));
        return page;
    }

    // tg: LibraryConsole/screens/Upload.tsx:156
    private static UiNode UploadModal(TguiContext c)
    {
        UiValue data = c.Data;
        List<string> categories = data.Arr("upload_categories").Select(v => v.AsStr()).ToList();
        string uploadCategory = c.UseState("uploadCategory", "");
        string displayCategory = uploadCategory != "" ? uploadCategory : data.Str("default_category");
        return new UiNode(UiKind.Modal) { Key = "upload-modal", Width = "650px" }.With(
            new UiNode(UiKind.Box) { Key = "question", FontSize = "20px", Text = "Are you sure you want to upload this book to the database?" },
            new UiNode(UiKind.LabeledList) { Key = "category" }.With(new UiNode(UiKind.LabeledItem) { Key = "category", Label = "Category" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "upload_category", Options = categories, TextValue = displayCategory, OnSelected = value => c.SetState("uploadCategory", value),
            })),
            new UiNode(UiKind.Stack) { Key = "buttons", Justify = "center", Align = "center" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "upload",
                    Disabled = !data.Truthy("can_db_request"),
                    Icon = "upload",
                    FontSize = "18px",
                    Color = "good",
                    Text = "Upload To DB",
                    OnClick = () =>
                    {
                        c.SetState("uploadToDB", false);
                        c.Act("upload", ("category", displayCategory));
                    },
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "return", Icon = "times", FontSize = "18px", Color = "bad", Text = "Return", OnClick = () => c.SetState("uploadToDB", false),
                })));
    }
}
