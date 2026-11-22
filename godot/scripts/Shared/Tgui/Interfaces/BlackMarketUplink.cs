using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BlackMarketUplink.tsx (gaps P4d, D298: the market
// uplink): the money, the markets and categories, the goods with their stock and price, and the shipment selector. A one of a kind
// good's picture is not sent (D104), so the goods show their names. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BlackMarketUplink
{
    // tg: BlackMarketUplink.tsx:45
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        c.WindowTheme = "hackerman";
        double money = data.Num("money");
        string viewingMarket = data.Str("viewing_market", null);
        string viewingCategory = data.Str("viewing_category", null);

        var marketTabs = new UiNode(UiKind.Tabs) { Key = "markets" };
        foreach (UiValue market in data.Get("markets").Items)
        {
            string id = market.Str("id");
            marketTabs.With(new UiNode(UiKind.Tab) { Key = "market:" + id, Selected = id == viewingMarket, Text = market.Str("name"), OnClick = () => c.Act("set_market", ("market", id)) });
        }
        var categoryTabs = new UiNode(UiKind.Tabs) { Key = "categories", Vertical = true };
        foreach (UiValue category in data.Get("categories").Items)
        {
            string name = category.AsStr();
            categoryTabs.With(new UiNode(UiKind.Tab) { Key = "category:" + name, Selected = viewingCategory == name, Text = name, OnClick = () => c.Act("set_category", ("category", name)) });
        }
        var items = new UiNode(UiKind.Fragment) { Key = "items" };
        foreach (UiValue item in data.Get("items").Items)
        {
            string id = item.Str("id");
            double amount = item.Num("amount");
            double cost = item.Num("cost");
            items.With(new UiNode(UiKind.Box) { Key = "item:" + item.Str("name"), Candystripe = true }.With(
                new UiNode(UiKind.Stack) { Key = "row", Align = "baseline" }.With(
                    new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, Bold = true, Text = item.Str("name") },
                    new UiNode(UiKind.StackItem) { Key = "stock", Color = "label", Text = amount != 0 ? TguiFormat.JsNumber(amount) + " in stock" : "Out of stock" },
                    new UiNode(UiKind.StackItem) { Key = "cost", Text = TguiFormat.FormatMoney(cost) + " cr" },
                    new UiNode(UiKind.StackItem) { Key = "buyItem" }.With(new UiNode(UiKind.Button) { Key = "buy", Text = "Buy", Disabled = amount == 0 || cost > money, OnClick = () => c.Act("select", ("item", id)) })),
                new UiNode(UiKind.Box) { Key = "desc", Text = item.Str("desc") }));
        }

        var content = new UiNode(UiKind.Fragment) { Key = "content" }.With(
            new UiNode(UiKind.Section)
            {
                Key = "header",
                Title = "Black Market Uplink",
                Buttons = new UiNode(UiKind.Box) { Key = "money", Bold = true, Text = (data.Get("money").Kind == UiValueKind.Number ? TguiFormat.FormatMoney(money) : data.Str("money")) + " cr" },
            },
            marketTabs,
            new UiNode(UiKind.Stack) { Key = "body" }.With(
                new UiNode(UiKind.StackItem) { Key = "left" }.With(categoryTabs),
                new UiNode(UiKind.StackItem) { Key = "right", Grow = 1 }.With(items)));
        UiNode selector = ShipmentSelector(c);
        return selector == null ? c.Window(670, 480, null, content) : c.Window(670, 480, null, selector, content);
    }

    // tg: BlackMarketUplink.tsx:151
    private static UiNode ShipmentSelector(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("buying"))
            return null;
        double money = data.Num("money");
        bool ltsrbtBuilt = data.Truthy("ltsrbt_built");
        UiValue descriptions = data.Get("delivery_method_description");
        var methods = new UiNode(UiKind.Stack) { Key = "methods" };
        foreach (UiValue method in data.Get("delivery_methods").Items)
        {
            string name = method.Str("name");
            if (name == "LTSRBT" && !ltsrbtBuilt)
                continue;
            double price = method.Num("price");
            methods.With(new UiNode(UiKind.StackItem) { Key = "method:" + name, Width = "17.5rem" }.With(
                new UiNode(UiKind.Box) { Key = "name", FontSize = "30px", Text = name },
                new UiNode(UiKind.Box) { Key = "description", Text = descriptions.Str(name) },
                new UiNode(UiKind.Button) { Key = "buy", Text = TguiFormat.FormatMoney(price) + " cr", Disabled = money < price, OnClick = () => c.Act("buy", ("method", name)) }));
        }
        return new UiNode(UiKind.Modal) { Key = "selector", TextAlign = "center" }.With(
            methods,
            new UiNode(UiKind.Button) { Key = "cancel", Text = "Cancel", Color = "bad", OnClick = () => c.Act("cancel") });
    }
}
