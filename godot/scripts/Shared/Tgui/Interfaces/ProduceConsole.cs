using System;
using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ProduceConsole.tsx (gaps P4d, D298: the order
// consoles, the chef's produce console and the mining equipment console): the shopping tab with its category tabs, search and
// goods (minus, plus and the amount), the checkout tab with the free and paid rows and the purchase and express buttons, the
// condensed toggle, the points and free products line, and the order-sent dimmer. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ProduceConsole
{
    private const string TabKey = "produceTab";
    private const string CondensedKey = "produceCondensed";
    private const string CategoryKey = "produceCategory";
    private const string SearchKey = "produceSearch";

    // tg: ProduceConsole.tsx:391
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string creditType = data.Str("credit_type");
        double points = data.Num("points");
        bool offCooldown = data.Truthy("off_cooldown");
        IReadOnlyList<UiValue> categories = data.Get("order_categories").Items;
        IReadOnlyList<UiValue> itemAmts = data.Get("item_amts").Items;
        UiValue freeUsesValue = data.Get("free_uses");
        bool hasFreeUses = freeUsesValue != null && !freeUsesValue.IsNull;
        double freeUses = hasFreeUses ? freeUsesValue.AsNum() : 0;

        string tab = c.UseState(TabKey, "shopping");
        bool condensed = c.UseState(CondensedKey, false);
        double freeUsesRemaining = Math.Max(0, freeUses - itemAmts.Sum(item => item.Num("amt")));

        var content = new UiNode(UiKind.Fragment) { Key = "content" };
        if (!offCooldown)
            content.With(OrderSent());

        var counters = new UiNode(UiKind.Stack) { Key = "counters" }.With(
            new UiNode(UiKind.StackItem) { Key = "points" }.With(Cost(points, creditType)));
        if (hasFreeUses)
            counters.With(new UiNode(UiKind.StackItem) { Key = "free", Color = "grey", Text = "~ Free Products: " + TguiFormat.JsNumber(freeUsesRemaining) + " / " + TguiFormat.JsNumber(freeUses) });

        var header = new UiNode(UiKind.Section) { Key = "header", Fill = true }.With(new UiNode(UiKind.Stack) { Key = "headerStack", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "tabsItem" }.With(new UiNode(UiKind.Stack) { Key = "tabs", TextAlign = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "shoppingItem", Grow = 3 }.With(new UiNode(UiKind.Button)
                {
                    Key = "shopping",
                    Fluid = true,
                    Color = "green",
                    Icon = "cart-plus",
                    Text = "Shopping",
                    OnClick = () => c.SetState(TabKey, "shopping"),
                }),
                new UiNode(UiKind.StackItem) { Key = "checkoutItem", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "checkout",
                    Fluid = true,
                    Color = "green",
                    Icon = "dollar-sign",
                    Text = "Checkout",
                    OnClick = () => c.SetState(TabKey, "checkout"),
                }))),
            new UiNode(UiKind.StackItem) { Key = "toolsItem" }.With(new UiNode(UiKind.Stack) { Key = "tools" }.With(
                new UiNode(UiKind.StackItem) { Key = "condenseItem", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "condense",
                    Color = condensed ? "green" : "red",
                    Text = condensed ? "Expand" : "Condense",
                    OnClick = () => c.SetState(CondensedKey, !condensed),
                }),
                new UiNode(UiKind.StackItem) { Key = "countersItem" }.With(counters)))));

        content.With(new UiNode(UiKind.Stack) { Key = "main", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "headerItem" }.With(header),
            new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(tab == "checkout" ? CheckoutTab(c) : ShoppingTab(c, condensed))));
        return c.Window(Math.Max(categories.Count * 125, 500), 400, null, content);
    }

    // tg: ProduceConsole.tsx:85
    private static UiNode ShoppingTab(TguiContext c, bool condensed)
    {
        UiValue data = c.Data;
        string creditType = data.Str("credit_type");
        IReadOnlyList<UiValue> categories = data.Get("order_categories").Items;
        IReadOnlyList<UiValue> itemAmts = data.Get("item_amts").Items;
        IReadOnlyList<UiValue> orderDatums = data.Get("order_datums").Items;

        string shopCategory = c.UseState(CategoryKey, categories.Count > 0 ? categories[0].AsStr("") : "");
        string searchItem = c.UseState(SearchKey, "");

        // tgui-core's createSearch: a case-insensitive match of the words in the item's name
        IEnumerable<UiValue> goods = searchItem.Length > 0
            ? orderDatums.Where(item => item != null && !item.IsNull && Matches(searchItem, item.Str("name")))
            : orderDatums.Where(item => item != null && !item.IsNull && item.Str("cat") == shopCategory);

        var tabs = new UiNode(UiKind.Tabs) { Key = "categories", Fluid = true, TextAlign = "center" };
        foreach (UiValue categoryValue in categories)
        {
            string category = categoryValue.AsStr("");
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "category:" + category,
                Selected = category == shopCategory,
                Text = category,
                OnClick = () =>
                {
                    c.SetState(CategoryKey, category);
                    if (searchItem.Length > 0)
                        c.SetState(SearchKey, "");
                },
            });
        }

        var table = new UiNode(UiKind.Table) { Key = "goods" };
        foreach (UiValue item in goods)
        {
            string reference = item.Str("ref");
            string name = item.Str("name");
            double max = item.Num("max");
            double amount = FindAmount(itemAmts, name);
            var row = new UiNode(UiKind.TableRow) { Key = "item:" + reference, Candystripe = true };
            if (!condensed)
                row.With(new UiNode(UiKind.TableCell) { Key = "icon", Width = "36px" }.With(
                    new UiNode(UiKind.Box) { Key = "picture", DmIcon = item.Str("icon"), DmIconState = item.Str("icon_state"), Width = "36px", Height = "36px" }));
            row.With(
                new UiNode(UiKind.TableCell) { Key = "name", Color = "label", Text = TguiFormat.Capitalize(name) },
                new UiNode(UiKind.TableCell) { Key = "info", Color = "label", FontSize = "10px" }.With(
                    new UiNode(UiKind.Button) { Key = "desc", Color = "transparent", Icon = "info", Tooltip = item.Str("desc") }),
                new UiNode(UiKind.TableCell) { Key = "cost", FontSize = "10px", TextAlign = "right", Tooltip = CostTooltip(item, creditType) }.With(Cost(item.Num("cost"), creditType)),
                new UiNode(UiKind.TableCell) { Key = "amount" }.With(
                    new UiNode(UiKind.Button) { Key = "minus", Icon = "minus", Disabled = amount <= 0, OnClick = () => c.Act("remove_one", ("target", reference)) },
                    new UiNode(UiKind.Button) { Key = "plus", Icon = "plus", Disabled = amount >= max, OnClick = () => c.Act("add_one", ("target", reference)) },
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "count",
                        Value = amount,
                        Width = "41px",
                        MinValue = 0,
                        MaxValue = max,
                        Step = 1,
                        OnChange = value => c.Act("cart_set", ("target", reference), ("amt", value)),
                    }));
            table.With(row);
        }

        return new UiNode(UiKind.Stack) { Key = "shopping", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.Section) { Key = "categoriesSection" }.With(new UiNode(UiKind.StackItem) { Key = "tabsItem" }.With(
                new UiNode(UiKind.Stack) { Key = "tabsRow" }.With(
                    new UiNode(UiKind.StackItem) { Key = "tabsGrow", Grow = 1 }.With(tabs),
                    new UiNode(UiKind.StackItem) { Key = "searchItem" }.With(new UiNode(UiKind.Input)
                    {
                        Key = "search",
                        AutoFocus = true,
                        Width = "150px",
                        Tooltip = "Search item...",
                        TextValue = searchItem,
                        OnInput = value => c.SetState(SearchKey, value),
                    })))),
            new UiNode(UiKind.StackItem) { Key = "goodsItem", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "goodsSection", Fill = true, Scrollable = true }.With(table)));
    }

    // tg: ProduceConsole.tsx:221
    private static UiNode CheckoutTab(TguiContext c)
    {
        UiValue data = c.Data;
        string creditType = data.Str("credit_type");
        string purchaseTooltip = data.Str("purchase_tooltip");
        string expressTooltip = data.Str("express_tooltip");
        bool forcedExpress = data.Truthy("forced_express");
        double cargoValue = data.Num("cargo_value");
        IReadOnlyList<UiValue> orderDatums = data.Get("order_datums").Items;
        double totalCost = data.Num("total_cost");
        double cargoCostMultiplier = data.Num("cargo_cost_multiplier");
        double expressCostMultiplier = data.Num("express_cost_multiplier");
        IReadOnlyList<UiValue> itemAmts = data.Get("item_amts").Items;
        UiValue freeUsesValue = data.Get("free_uses");
        bool hasFreeUses = freeUsesValue != null && !freeUsesValue.IsNull;
        double freeUses = hasFreeUses ? freeUsesValue.AsNum() : 0;

        double totalCargoCost = Math.Floor(totalCost * cargoCostMultiplier);
        double totalItemCount = itemAmts.Sum(item => item.Num("amt"));

        // the rows: an item split between its free and its paid part when the free uses run out in the middle of it
        double freeUsesRemaining = freeUses;
        var checkout = new List<(UiValue Item, double Amt, double OtherAmt, bool IsFree)>();
        foreach (UiValue item in orderDatums.Where(food => food != null && !food.IsNull && FindAmount(itemAmts, food.Str("name")) != 0))
        {
            double amount = FindAmount(itemAmts, item.Str("name"));
            if (freeUsesRemaining >= amount)
                checkout.Add((item, amount, 0, true));
            else if (freeUsesRemaining > 0)
            {
                checkout.Add((item, freeUsesRemaining, amount - freeUsesRemaining, true));
                checkout.Add((item, amount - freeUsesRemaining, freeUsesRemaining, false));
            }
            else
                checkout.Add((item, amount, 0, false));
            freeUsesRemaining -= amount;
        }

        var table = new UiNode(UiKind.Table) { Key = "checkout" }.With(
            new UiNode(UiKind.TableRow) { Key = "head" }.With(
                new UiNode(UiKind.TableCell) { Key = "title", Bold = true, Color = "label", TextAlign = "center", Text = "Checkout list:" }));
        var nothing = new UiNode(UiKind.Fragment) { Key = "nothing" };
        if (checkout.Count == 0)
            nothing.With(
                new UiNode(UiKind.Box) { Key = "empty", TextAlign = "center", FontSize = "40px", Text = "Nothing!" },
                new UiNode(UiKind.Box) { Key = "orderSomething", TextAlign = "center", FontSize = "15px", Text = "(Go order something, will ya?)" });
        int index = 0;
        foreach ((UiValue item, double amt, double otherAmt, bool isFree) in checkout)
        {
            string reference = item.Str("ref");
            var cost = new UiNode(UiKind.TableCell) { Key = "cost", FontSize = "10px", TextAlign = "right" };
            if (isFree)
                cost.With(new UiNode(UiKind.Box) { Key = "free", Color = "green", FontSize = "10px", Text = "Free" });
            else
            {
                cost.Tooltip = CostTooltip(item, creditType);
                cost.With(Cost(item.Num("cost"), creditType));
            }
            table.With(new UiNode(UiKind.TableRow) { Key = "row:" + index++ + ":" + reference, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "name", Text = TguiFormat.Capitalize(item.Str("name")) },
                new UiNode(UiKind.TableCell) { Key = "desc", Color = "label", FontSize = "10px", Text = "\"" + item.Str("desc") + "\"" },
                cost,
                new UiNode(UiKind.TableCell) { Key = "amount" }.With(new UiNode(UiKind.NumberInput)
                {
                    Key = "count",
                    Value = amt,
                    Width = "41px",
                    MinValue = 0,
                    MaxValue = isFree ? (hasFreeUses ? freeUses : 10) : item.Num("max") - otherAmt,
                    Step = 1,
                    OnChange = value => c.Act("cart_set", ("target", reference), ("amt", value + otherAmt)),
                })));
        }

        var buttons = new UiNode(UiKind.Stack) { Key = "buttons", Fill = true };
        if (!forcedExpress)
            buttons.With(new UiNode(UiKind.StackItem) { Key = "purchaseItem", Grow = 1, TextAlign = "center" }.With(new UiNode(UiKind.Button)
            {
                Key = "purchase",
                Fluid = true,
                Icon = "plane-departure",
                Disabled = totalCargoCost < cargoValue,
                Tooltip = totalCargoCost < cargoValue ? "Total must be above or equal to " + TguiFormat.JsNumber(cargoValue) : purchaseTooltip,
                Text = "Purchase: " + TguiFormat.JsNumber(totalCargoCost) + " " + CreditText(creditType),
                OnClick = () => c.Act("purchase"),
            }));
        buttons.With(new UiNode(UiKind.StackItem) { Key = "expressItem", Grow = 1, TextAlign = "center" }.With(new UiNode(UiKind.Button)
        {
            Key = "express",
            Fluid = true,
            Icon = "parachute-box",
            Color = "yellow",
            Disabled = totalItemCount <= 0,
            Tooltip = totalItemCount <= 0 ? "Order at least 1 item" : expressTooltip,
            Text = "Express: " + TguiFormat.JsNumber(totalCost * expressCostMultiplier) + " " + CreditText(creditType),
            OnClick = () => c.Act("express"),
        }));

        return new UiNode(UiKind.Stack) { Key = "checkoutTab", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "listItem", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "list", Fill = true, Scrollable = true }.With(table, nothing)),
            new UiNode(UiKind.StackItem) { Key = "buttonsItem" }.With(new UiNode(UiKind.Section) { Key = "buttonsSection", Fill = true }.With(buttons)));
    }

    // tg: ProduceConsole.tsx:331  OrderSent
    private static UiNode OrderSent() => new UiNode(UiKind.Modal) { Key = "sent", TextAlign = "center" }.With(new UiNode(UiKind.Stack) { Key = "sentStack", Vertical = true }.With(
        new UiNode(UiKind.StackItem) { Key = "iconItem" }.With(new UiNode(UiKind.Icon) { Key = "icon", Color = "green", Icon = "plane-arrival", Size = 10 }),
        new UiNode(UiKind.StackItem) { Key = "text", FontSize = "18px", Color = "green", Text = "Order sent! Machine on cooldown..." }));

    // tg: ProduceConsole.tsx:70  CreditIcon, after the amount: the coins icon for credits, else the currency's name
    private static UiNode Cost(double amount, string creditType)
    {
        var row = new UiNode(UiKind.Stack) { Key = "cost" }.With(new UiNode(UiKind.StackItem) { Key = "amount", Text = TguiFormat.JsNumber(amount) });
        row.With(creditType == "credit"
            ? new UiNode(UiKind.StackItem) { Key = "credit" }.With(new UiNode(UiKind.Icon) { Key = "coins", Icon = "coins", Color = "gold" })
            : new UiNode(UiKind.StackItem) { Key = "credit", Text = creditType });
        return row;
    }

    // A Godot button draws one icon (its own on the left): the coins icon after a button's amount is written as tg's "cr".
    private static string CreditText(string creditType) => creditType == "credit" ? "cr" : creditType;

    private static string CostTooltip(UiValue item, string creditType) => "Costs " + TguiFormat.JsNumber(item.Num("cost")) + " " + creditType + " per order";

    // tg: ProduceConsole.tsx:80  findAmount
    private static double FindAmount(IReadOnlyList<UiValue> itemAmts, string name) =>
        itemAmts.FirstOrDefault(item => item.Str("name") == name)?.Num("amt") ?? 0;

    // tgui-core's createSearch: every word of the query is in the text, case ignored
    private static bool Matches(string query, string text) =>
        query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => (text ?? "").Contains(word, StringComparison.OrdinalIgnoreCase));
}
