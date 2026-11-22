using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Cargo/*. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps P4b (D298): the whole window, with the budget, the prices, private buying and the help tab. CargoContent and the catalog are
// shared with NtosCargo (and the express console's catalog takes express).

public static class Cargo
{
    // tg: Cargo/index.tsx:23
    public static UiNode Render(TguiContext c) => c.Window(800, 750, null, Content(c));

    private const string TabKey = "cargotab";
    private const string SupplyKey = "supply";
    private const string SearchKey = "search_text";
    private const string ContentsKey = "showContents";

    // tg: Cargo/index.tsx:33
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        var cart = data.Get("cart").Items;
        var requests = data.Get("requests").Items;
        bool requestonly = data.Truthy("requestonly");
        string tab = c.UseState(TabKey, "catalog");
        int amount = (int)cart.Sum(entry => entry.Num("amount"));

        UiNode Tab(string id, string icon, string text, bool highlight) => new UiNode(UiKind.Tab)
        {
            Key = "tab:" + id,
            Icon = icon,
            Text = text,
            Color = tab != id && highlight ? "yellow" : null,
            Selected = tab == id,
            OnClick = () => c.SetState(TabKey, id),
        };

        var tabs = new UiNode(UiKind.Tabs) { Key = "tabs", Fluid = true }.With(
            Tab("catalog", "list", "Catalog", false),
            Tab("active requests", "envelope", "Requests (" + requests.Count + ")", requests.Count > 0));
        if (!requestonly)
        {
            tabs.With(Tab("cart", "shopping-cart", "Checkout (" + amount + ")", amount > 0));
            tabs.With(Tab("help", "question", "Help", false));
        }

        UiNode buttons = tab == "active requests" && !requestonly
            ? new UiNode(UiKind.Button) { Key = "denyall", Icon = "times", Color = "transparent", Text = "Clear", OnClick = () => c.Act("denyall") }
            : tab is "catalog" or "cart" ? CartButtons(c, amount) : null;

        UiNode body = tab switch
        {
            "active requests" => RequestsView(c),
            "cart" => CartView(c),
            "help" => Help(),
            _ => Catalog(c),
        };

        return new UiNode(UiKind.Stack) { Key = "root", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "status" }.With(Status(c)),
            new UiNode(UiKind.StackItem) { Key = "head" }.With(new UiNode(UiKind.Section) { Key = "headSection", Title = TitleCase(tab), Buttons = buttons }.With(tabs)),
            new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(body));
    }

    private static string TitleCase(string text) => string.Join(' ', text.Split(' ').Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    // tg: Cargo/CargoStatus.tsx:14
    private static UiNode Status(TguiContext c)
    {
        UiValue data = c.Data;
        string location = data.Str("location");
        bool grocery = data.Truthy("grocery");
        UiNode shuttle = data.Truthy("docked") && !data.Truthy("requestonly") && data.Truthy("can_send")
            ? new UiNode(UiKind.Button)
            {
                Key = "send",
                Color = grocery ? "orange" : "green",
                Tooltip = grocery ? "The kitchen is waiting for their grocery supply delivery!" : "",
                Text = location,
                OnClick = () => c.Act("send"),
            }
            : new UiNode(UiKind.Box) { Key = "send", Text = location };
        var list = new UiNode(UiKind.LabeledList) { Key = "list" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "shuttle", Label = "Shuttle" }.With(shuttle),
            new UiNode(UiKind.LabeledItem) { Key = "message", Label = "CentCom Message" }.With(new UiNode(UiKind.Box) { Key = "text", Text = data.Str("message") }));
        if (data.Truthy("loan") && !data.Truthy("requestonly"))
        {
            list.With(new UiNode(UiKind.LabeledItem) { Key = "loan", Label = "Loan" }.With(!data.Truthy("loan_dispatched")
                ? new UiNode(UiKind.Button) { Key = "loanButton", Disabled = !(data.Truthy("away") && data.Truthy("docked")), Text = "Loan Shuttle", OnClick = () => c.Act("loan") }
                : new UiNode(UiKind.Box) { Key = "loaned", Color = "bad", Text = "Loaned to Centcom" }));
        }
        return new UiNode(UiKind.Section)
        {
            Key = "statusSection",
            Title = data.Str("department"),
            Buttons = new UiNode(UiKind.Box) { Key = "points", Bold = true, Text = TguiFormat.FormatMoney(data.Num("points")) + data.Str("displayed_currency_full_name") },
        }.With(list);
    }

    // tg: Cargo/CargoButtons.tsx:7
    private static UiNode CartButtons(TguiContext c, int amount)
    {
        UiValue data = c.Data;
        bool canClear = !data.Truthy("requestonly") && data.Truthy("can_send") && data.Truthy("can_approve_requests") && data.Get("cart").Items.Count > 0;
        double total = data.Get("cart").Items.Sum(entry => entry.Num("cost"));
        string count = amount == 0 ? "Cart is empty" : amount == 1 ? "1 item" : amount + " items";
        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Box) { Key = "count", Text = count + " " + (total > 0 ? "(" + TguiFormat.FormatMoney(total) + data.Str("displayed_currency_name") + ")" : "") },
            new UiNode(UiKind.Button) { Key = "clear", Disabled = !canClear, Icon = "times", Color = "transparent", Text = "Clear", OnClick = () => c.Act("clear") });
    }

    // tg: Cargo/CargoCatalog.tsx:25
    public static UiNode Catalog(TguiContext c, bool express = false)
    {
        var categories = c.Data.Get("supplies").Fields.Select(field => field.Value).OrderBy(category => category.Str("name"), StringComparer.Ordinal).ToList();
        string searchText = c.UseState(SearchKey, "");
        string active = c.UseState(SupplyKey, categories.FirstOrDefault()?.Str("name"));
        string showContents = c.UseState<string>(ContentsKey, null);

        // tg: Cargo/helpers.ts:15  the first page of what matches the search
        IEnumerable<UiValue> packs = active == "search_results"
            ? categories.SelectMany(category => category.Get("packs").Items)
                .Where(pack => pack.Str("name")?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true || pack.Str("desc")?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true)
                .Take(25)
            : categories.FirstOrDefault(category => category.Str("name") == active)?.Get("packs").Items ?? Array.Empty<UiValue>();
        var sorted = packs.OrderBy(pack => pack.Str("name"), StringComparer.Ordinal).ToList();

        // tg: Cargo/CargoCatalog.tsx:95  CatalogTabs
        var tabs = new UiNode(UiKind.Tabs) { Key = "categories", Vertical = true };
        foreach (UiValue category in categories)
        {
            string name = category.Str("name");
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "category:" + name,
                Candystripe = true,
                Color = name == active ? "green" : null,
                Text = name + " (" + category.Get("packs").Items.Count + ")",
                Selected = name == active,
                OnClick = () =>
                {
                    c.SetState(SupplyKey, name);
                    c.SetState(SearchKey, "");
                },
            });
        }
        bool selfPaid = c.Data.Truthy("self_paid");
        var left = new UiNode(UiKind.Stack) { Key = "leftStack", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "searchItem" }.With(new UiNode(UiKind.Input)
            {
                Key = "search",
                Fluid = true,
                Icon = "search",
                TextValue = searchText,
                OnInput = value =>
                {
                    if (value == searchText)
                        return;
                    if (value.Length > 0)
                        c.SetState(SupplyKey, "search_results"); // Start showing results
                    else if (active == "search_results")
                        c.SetState(SupplyKey, categories.FirstOrDefault()?.Str("name")); // return to normal category
                    c.SetState(SearchKey, value);
                },
            }),
            new UiNode(UiKind.StackItem) { Key = "tabsItem", Grow = 1, Scrollable = true }.With(tabs));
        if (!express)
        {
            left.With(new UiNode(UiKind.StackItem) { Key = "privateItem" }.With(new UiNode(UiKind.Button)
            {
                Key = "toggleprivate",
                Fluid = true,
                Color = selfPaid ? "caution" : "transparent",
                Icon = selfPaid ? "check-square-o" : "square-o",
                Tooltip = "Use your own funds to purchase items.",
                Text = "Buy Privately",
                OnClick = () => c.Act("toggleprivate"),
            }));
        }

        var root = new UiNode(UiKind.Stack) { Key = "catalog", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "left", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "leftSection", Fill = true }.With(left)),
            new UiNode(UiKind.Divider) { Key = "divider" },
            new UiNode(UiKind.StackItem) { Key = "right", Grow = express ? 2 : 3 }.With(new UiNode(UiKind.Section) { Key = "rightSection", Fill = true, Scrollable = true }.With(CatalogList(c, sorted))));

        UiValue shown = showContents == null ? null : sorted.FirstOrDefault(pack => pack.Str("name") == showContents);
        return shown == null ? root : new UiNode(UiKind.Fragment) { Key = "catalogRoot" }.With(PackInfo(c, shown), root);
    }

    // tg: Cargo/CargoCatalog.tsx:185
    private static UiNode CatalogList(TguiContext c, IReadOnlyList<UiValue> packs)
    {
        UiValue data = c.Data;
        var cart = data.Get("cart").Items;
        double maxOrder = data.Num("max_order");
        bool selfPaid = data.Truthy("self_paid");
        bool appCost = data.Truthy("app_cost");
        string currency = data.Str("displayed_currency_name");
        var list = new UiNode(UiKind.Fragment) { Key = "packs" };
        foreach (UiValue pack in packs)
        {
            double cost = pack.Num("cost");
            string color = "";
            double digits = Math.Floor(Math.Log10(cost) + 1);
            if (selfPaid)
                color = "yellow";
            else if (digits >= 5 && digits <= 6)
                color = "orange";
            else if (digits > 6)
                color = "bad";

            bool privateBuy = (selfPaid && !pack.Truthy("goody")) || appCost;

            double amount = cart.FirstOrDefault(entry => entry.Str("object") == pack.Str("name"))?.Num("amount") ?? 0;
            string id = pack.Str("id");
            string name = pack.Str("name");

            var row = new UiNode(UiKind.Stack) { Key = "row", Fill = true, TextAlign = "right" }.With(
                new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, TextAlign = "left", Text = name });
            if (pack.Truthy("small_item") || pack.Truthy("access") || pack.Truthy("contraband"))
            {
                var icons = new UiNode(UiKind.Stack) { Key = "icons", Reverse = true };
                if (pack.Truthy("small_item"))
                    icons.With(new UiNode(UiKind.StackItem) { Key = "small" }.With(new UiNode(UiKind.Icon) { Key = "icon", Tooltip = "Small Item", Icon = "compress-alt", Color = "purple" }));
                if (pack.Truthy("access"))
                    icons.With(new UiNode(UiKind.StackItem) { Key = "access" }.With(new UiNode(UiKind.Icon) { Key = "icon", Tooltip = "Restricted", Icon = "lock", Color = "average" }));
                if (pack.Truthy("contraband"))
                    icons.With(new UiNode(UiKind.StackItem) { Key = "contraband" }.With(new UiNode(UiKind.Icon) { Key = "icon", Tooltip = "Contraband", Icon = "pastafarianism", Color = "bad" }));
                row.With(new UiNode(UiKind.StackItem) { Key = "icons" }.With(icons));
            }
            var prices = new UiNode(UiKind.Stack) { Key = "prices", Vertical = true, Color = "gold", FontSize = 0.85 }.With(
                new UiNode(UiKind.StackItem) { Key = "cost", Opacity = privateBuy ? 0.75 : null, Slashed = privateBuy, Text = TguiFormat.FormatMoney(cost) + currency });
            if (privateBuy)
                prices.With(new UiNode(UiKind.StackItem) { Key = "private", Text = TguiFormat.FormatMoney(Math.Round(cost * 1.1, MidpointRounding.AwayFromZero)) + currency });
            row.With(new UiNode(UiKind.StackItem) { Key = "price", Align = "center", Width = 5.5 }.With(prices));

            list.With(new UiNode(UiKind.ImageButton)
            {
                Key = "pack:" + id,
                Fluid = true,
                DmIcon = pack.Str("first_item_icon"),
                DmIconState = pack.Str("first_item_icon_state"),
                Color = color,
                Disabled = amount >= maxOrder,
                ButtonsAlt = new UiNode(UiKind.Button) { Key = "open", Color = "transparent", Icon = "info", OnClick = () => c.SetState(ContentsKey, name) },
                OnClick = () => c.Act("add", ("id", id)),
            }.With(row));
        }
        return list;
    }

    // tg: Cargo/CargoCatalog.tsx:284
    private static UiNode PackInfo(TguiContext c, UiValue pack)
    {
        var contents = new UiNode(UiKind.Section) { Key = "contents", Fill = true, Scrollable = true };
        foreach (UiValue item in pack.Get("contains").Items)
        {
            contents.With(new UiNode(UiKind.ImageButton)
            {
                Key = "item:" + item.Str("name"),
                Fluid = true,
                DmIcon = item.Str("icon"),
                DmIconState = item.Str("icon_state"),
                Text = item.Str("name"),
                ButtonsAlt = item.Truthy("amount") ? new UiNode(UiKind.Box) { Key = "amount", Text = "x" + item.Str("amount") } : null,
            });
        }
        if (contents.Children.Count == 0)
            contents.With(new UiNode(UiKind.Box) { Key = "unknown", Color = "label", TextAlign = "center", Text = "We can't find information about even the approximate contents of this order." });
        return new UiNode(UiKind.Modal) { Key = "info", Width = "50%", Height = "50%" }.With(
            new UiNode(UiKind.Section)
            {
                Key = "title",
                Title = pack.Str("name"),
                Buttons = new UiNode(UiKind.Button) { Key = "close", Icon = "close", Color = "bad", OnClick = () => c.SetState<string>(ContentsKey, null) },
            }.With(new UiNode(UiKind.Box) { Key = "desc", Text = pack.Str("desc") ?? "No description available." }),
            contents);
    }

    // tg: Cargo/CargoCart.tsx:17
    private static UiNode CartView(TguiContext c)
    {
        UiValue data = c.Data;
        var cart = data.Get("cart").Items;
        bool canSend = data.Truthy("can_send");
        bool sendable = data.Truthy("away") && data.Truthy("docked");
        var root = new UiNode(UiKind.Stack) { Key = "cartRoot", Fill = true, Vertical = true };

        root.With(new UiNode(UiKind.StackItem) { Key = "items", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "itemsSection", Fill = true, Scrollable = true }.With(CheckoutItems(c))));

        if (cart.Count > 0 && canSend)
        {
            var confirm = new UiNode(UiKind.Stack) { Key = "confirmStack", Fill = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "spinner", Grow = 1 }.With(sendable ? null : new UiNode(UiKind.Icon) { Key = "icon", Size = 1.5, Color = "blue", Icon = "toolbox", Spin = true }),
                new UiNode(UiKind.StackItem) { Key = "button" }.With(new UiNode(UiKind.Button)
                {
                    Key = "confirmButton",
                    Color = "green",
                    Disabled = !sendable,
                    Text = "Confirm the order",
                    Tooltip = sendable ? "" : "Shuttle is at " + data.Str("location"),
                    OnClick = () => c.Act("send"),
                }));
            root.With(new UiNode(UiKind.StackItem) { Key = "confirm" }.With(new UiNode(UiKind.Section) { Key = "confirmSection", TextAlign = "right" }.With(confirm)));
        }
        return root;
    }

    // tg: Cargo/CargoCart.tsx:57
    private static UiNode CheckoutItems(TguiContext c)
    {
        UiValue data = c.Data;
        var cart = data.Get("cart").Items;
        bool canSend = data.Truthy("can_send");
        double maxOrder = data.Num("max_order");
        bool isValid = c.UseState("cartAmountValid", true);
        if (cart.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "empty", Text = "Nothing in cart" };

        var table = new UiNode(UiKind.Table) { Key = "cartTable" }.With(
            new UiNode(UiKind.TableRow) { Key = "head", Color = "gray" }.With(
                new UiNode(UiKind.TableCell) { Key = "id", Bold = true, Text = "ID" },
                new UiNode(UiKind.TableCell) { Key = "type", Bold = true, Text = "Supply Type" },
                new UiNode(UiKind.TableCell) { Key = "amount", Bold = true, Text = "Amount" },
                new UiNode(UiKind.TableCell) { Key = "tags", Bold = true },
                new UiNode(UiKind.TableCell) { Key = "cost", Bold = true, TextAlign = "right", Text = "Cost" }));
        foreach (UiValue entry in cart)
        {
            string name = entry.Str("object");
            var amount = new UiNode(UiKind.TableCell) { Key = "amount", Width = 11 };
            if (canSend && entry.Truthy("can_be_cancelled"))
            {
                amount.With(
                    new UiNode(UiKind.Button) { Key = "minus", Icon = "minus", OnClick = () => c.Act("remove", ("order_name", name)) },
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "count",
                        Width = 5,
                        MinValue = 0,
                        MaxValue = maxOrder,
                        Value = entry.Num("amount"),
                        OnValidation = valid => c.SetState("cartAmountValid", valid),
                        OnChange = value =>
                        {
                            if (isValid)
                                c.Act("modify", ("order_name", name), ("amount", value));
                        },
                    },
                    new UiNode(UiKind.Button) { Key = "plus", Icon = "plus", Disabled = entry.Num("amount") >= maxOrder, OnClick = () => c.Act("add_by_name", ("order_name", name)) });
            }
            else
            {
                amount.With(new UiNode(UiKind.NumberInput) { Key = "count", Width = "40px", Value = entry.Num("amount"), MaxValue = Math.Max(maxOrder, entry.Num("amount")), Disabled = true });
            }
            var tags = new UiNode(UiKind.TableCell) { Key = "tags", Color = "average" };
            if (entry.Truthy("paid"))
                tags.With(new UiNode(UiKind.Box) { Key = "paid", Bold = true, Text = "[Private x " + entry.Str("amount") + "]" });
            if (entry.Truthy("dep_order"))
                tags.With(new UiNode(UiKind.Box) { Key = "dep", Bold = true, Text = "[Department x " + entry.Str("amount") + "]" });
            table.With(new UiNode(UiKind.TableRow) { Key = "entry:" + entry.Str("id"), Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "id", Color = "label", Text = "#" + entry.Str("id") },
                new UiNode(UiKind.TableCell) { Key = "name", Text = name },
                amount,
                tags,
                new UiNode(UiKind.TableCell) { Key = "cost", Color = "gold", TextAlign = "right", Text = TguiFormat.FormatMoney(entry.Num("cost")) + " " + entry.Str("cost_type") }));
        }
        return table;
    }

    // tg: Cargo/CargoRequests.tsx:8
    private static UiNode RequestsView(TguiContext c)
    {
        UiValue data = c.Data;
        var requests = data.Get("requests").Items;
        var section = new UiNode(UiKind.Section) { Key = "requests", Fill = true, Scrollable = true };
        if (requests.Count == 0)
            return section.With(new UiNode(UiKind.NoticeBox) { Key = "none", Color = "good", Text = "No Requests" });

        bool canAct = (!data.Truthy("requestonly") || data.Truthy("can_send")) && data.Truthy("can_approve_requests");
        string currency = data.Str("displayed_currency_name");
        var head = new UiNode(UiKind.TableRow) { Key = "head", Color = "gray" }.With(
            new UiNode(UiKind.TableCell) { Key = "id", Bold = true, Text = "ID" },
            new UiNode(UiKind.TableCell) { Key = "object", Bold = true, Text = "Object" },
            new UiNode(UiKind.TableCell) { Key = "orderer", Bold = true, Text = "Orderer" },
            new UiNode(UiKind.TableCell) { Key = "reason", Bold = true, Text = "Reason" },
            new UiNode(UiKind.TableCell) { Key = "account", Bold = true, Text = "Account" },
            new UiNode(UiKind.TableCell) { Key = "cost", Bold = true, Text = "Cost" });
        if (canAct)
            head.With(new UiNode(UiKind.TableCell) { Key = "actions", Bold = true, Text = "Actions" });
        var table = new UiNode(UiKind.Table) { Key = "table" }.With(head);
        foreach (UiValue request in requests)
        {
            string id = request.Str("id");
            var row = new UiNode(UiKind.TableRow) { Key = "request:" + id, Candystripe = true, Color = "label" }.With(
                new UiNode(UiKind.TableCell) { Key = "id", Text = "#" + id },
                new UiNode(UiKind.TableCell) { Key = "object", Text = request.Str("object") },
                new UiNode(UiKind.TableCell) { Key = "orderer", Bold = true, Text = request.Str("orderer") },
                new UiNode(UiKind.TableCell) { Key = "reason", Color = "lightgray", Width = "25%", Text = System.Net.WebUtility.HtmlDecode(request.Str("reason") ?? "") },
                new UiNode(UiKind.TableCell) { Key = "account", Text = request.Str("account") },
                new UiNode(UiKind.TableCell) { Key = "cost", Color = "gold", Text = TguiFormat.FormatMoney(request.Num("cost")) + currency });
            if (canAct)
            {
                row.With(new UiNode(UiKind.TableCell) { Key = "actions" }.With(
                    new UiNode(UiKind.Button) { Key = "approve", Icon = "check", Color = "good", OnClick = () => c.Act("approve", ("id", id)) },
                    new UiNode(UiKind.Button) { Key = "deny", Icon = "times", Color = "bad", OnClick = () => c.Act("deny", ("id", id)) }));
            }
            table.With(row);
        }
        return section.With(table);
    }

    // tg: Cargo/CargoHelp.tsx:3
    private const string OrderText = "Each department on the station will order crates from their own personal consoles. These orders are ENTIRELY FREE! They do not come out of cargo's budget, and rather put the consoles on cooldown. So here's where you come in: The ordered crates will show up on your supply console, and you need to deliver the crates to the orderers. You'll actually be paid the full value of the department crate on delivery if the crate was not tampered with, making the system a good source of income.";

    // tg: Cargo/CargoHelp.tsx:12
    private const string DisposalText = "In addition to MULEs and hand-deliveries, you can also make use of the disposals mailing system. Note that a break in the disposal piping could cause your package to be lost (this hardly ever happens), so this is not always the most secure ways to deliver something. You can wrap up a piece of paper and mail it the same way if you (or someone at the desk) wants to mail a letter.";

    // tg: Cargo/CargoHelp.tsx:19
    private static UiNode Help()
    {
        UiNode Step(string key, string number, string text) => new UiNode(UiKind.Box) { Key = key, Text = number + " " + text };
        var departments = new UiNode(UiKind.Section) { Key = "departments", Color = "label", Title = "Department Orders" }.With(
            new UiNode(UiKind.Box) { Key = "order", Text = OrderText },
            new UiNode(UiKind.Box) { Key = "examine", Text = "Examine a department order crate to get specific details about where the crate needs to go." });
        var mules = new UiNode(UiKind.Section) { Key = "mules", Title = "MULEbots" }.With(
            new UiNode(UiKind.Box) { Key = "about", Color = "label", Text = "MULEbots are slow but loyal delivery bots that will get crates delivered with minimal technician effort required. It is slow, though, and can be tampered with while en route." },
            new UiNode(UiKind.Box) { Key = "setup", Bold = true, Color = "green", Text = "Setting up a MULEbot is easy:" },
            Step("s1", "1.", "Drag the crate you want to deliver next to the MULEbot."),
            Step("s2", "2.", "Drag the crate on top of MULEbot. It should load on."),
            Step("s3", "3.", "Open your PDA."),
            Step("s4", "4.", "Click Delivery Bot Control."),
            Step("s5", "5.", "Click Scan for Active Bots."),
            Step("s6", "6.", "Choose your MULE."),
            Step("s7", "7.", "Click on Destination: (set)."),
            Step("s8", "8.", "Choose a destination and click OK."),
            Step("s9", "9.", "Click Proceed."));
        var disposals = new UiNode(UiKind.Section) { Key = "disposals", Title = "Disposals Delivery System" }.With(
            new UiNode(UiKind.Box) { Key = "about", Color = "label", Text = DisposalText },
            new UiNode(UiKind.Box) { Key = "easier", Bold = true, Color = "green", Text = "Using the Disposals Delivery System is even easier:" },
            Step("s1", "1.", "Wrap your item/crate in packaging paper."),
            Step("s2", "2.", "Use the destinations tagger to choose where to send it."),
            Step("s3", "3.", "Tag the package."),
            Step("s4", "4.", "Stick it on the conveyor and let the system handle it."));
        return new UiNode(UiKind.Stack) { Key = "help", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "helpSection", Fill = true, Scrollable = true }.With(departments, mules, disposals)),
            new UiNode(UiKind.StackItem) { Key = "notice" }.With(new UiNode(UiKind.NoticeBox) { Key = "ask", Color = "info", TextAlign = "center", Text = "Pondering something not included here? When in doubt, ask the QM!" }));
    }
}
