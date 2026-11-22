using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MatMarket.tsx (gaps P4d, D298: the galactic materials
// market): the balance and order, the cargo budget toggle, each material's elasticity, price, stock, order and trend with its buy
// buttons, the market crash notice and the instructions. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MatMarket
{
    private static readonly int[] Quantities = { 1, 5, 10, 25, 50 };

    // tg: MatMarket.tsx:43
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool orderingPrive = data.Truthy("orderingPrive");
        double creditBalance = data.Num("creditBalance");
        double orderBalance = data.Num("orderBalance");
        bool catastrophe = data.Truthy("catastrophe");
        // offset cost with crate value if there is currently nothing in the order
        double totalOrderCost = orderBalance != 0 ? orderBalance : data.Num("CARGO_CRATE_VALUE");
        // multiplier of 1.1 for private orders
        double multiplier = orderingPrive ? 1.1 : 1;
        double updateTime = data.Num("updateTime");

        var content = new UiNode(UiKind.Fragment) { Key = "content" };
        if (catastrophe)
            content.With(MarketCrashModal());
        UiNode toggle = data.Truthy("canOrderCargo")
            ? new UiNode(UiKind.Button)
            {
                Key = "toggle",
                Checkbox = orderingPrive,
                Icon = orderingPrive ? "fa-regular fa-square" : "square-check",
                Tooltip = "Place order from cargo budget.",
                Color = orderingPrive ? "transparent" : "green",
                Text = orderingPrive ? "Order via Cargo Budget?" : "Ordering via Cargo Budget",
                OnClick = () => c.Act("toggle_budget"),
            }
            : null;
        content.With(new UiNode(UiKind.Section) { Key = "header", Title = "Materials for sale", Buttons = toggle }.With(
            new UiNode(UiKind.Section) { Key = "summary" }.With(new UiNode(UiKind.Stack) { Key = "summaryStack" }.With(
                new UiNode(UiKind.StackItem) { Key = "balance", Width = "15%", Text = "Balance: " + TguiFormat.FormatMoney(creditBalance) + " cr." },
                new UiNode(UiKind.StackItem) { Key = "order", Width = "15%", Text = "Order: " + TguiFormat.FormatMoney(orderBalance) + " cr." },
                new UiNode(UiKind.StackItem) { Key = "update", Width = "20%", Color = updateTime > 150 ? "green" : "#ad7526", Text = TguiFormat.JsNumber(Math.Round(updateTime / 10, MidpointRounding.AwayFromZero)) + " seconds until next update" },
                new UiNode(UiKind.StackItem) { Key = "clearItem" }.With(new UiNode(UiKind.Button) { Key = "clear", Icon = "times", Color = "transparent", Text = "Clear", OnClick = () => c.Act("clear") })))));

        int index = 0;
        foreach (UiValue material in data.Get("materials").Items.OrderBy(m => m.Num("rarity")))
        {
            string name = material.Str("name");
            string title = TitleCase(name);
            double price = material.Num("price");
            double minThreshold = material.Num("min_threshold");
            double quantity = material.Num("quantity");
            double requested = material.Num("requested");
            double elastic = material.Num("elastic");
            string trend = material.Str("trend");

            var row = new UiNode(UiKind.Stack) { Key = "row" }.With(
                new UiNode(UiKind.StackItem) { Key = "name", Color = string.IsNullOrEmpty(material.Str("color")) ? "white" : material.Str("color"), FontSize = "125%", Width = "15%", Text = title },
                new UiNode(UiKind.StackItem) { Key = "elastic", Width = "10%", Color = elastic < 33 ? "red" : elastic < 66 ? "orange" : "green", Text = "Elasticity: " + TguiFormat.JsNumber(Math.Round(elastic, MidpointRounding.AwayFromZero)) + "%" },
                new UiNode(UiKind.StackItem) { Key = "price", Width = "15%", Underline = true, Tooltip = RangeText(minThreshold, material.Num("max_threshold")), Text = "Trading at " + TguiFormat.FormatMoney(price) + " cr." });
            if (price < minThreshold)
                row.With(new UiNode(UiKind.StackItem) { Key = "stock", Width = "33%", Color = "grey", Text = "Material price critical! Trading temporarily suspended." });
            else
            {
                row.With(new UiNode(UiKind.StackItem) { Key = "stock", Width = "33%" }.With(
                    new UiNode(UiKind.Box) { Key = "trading", Color = quantity == 0 ? "grey" : "white", Text = (quantity != 0 ? TguiFormat.JsNumber(quantity) : "Zero") + " sheets of " + name + " trading." },
                    new UiNode(UiKind.Box) { Key = "ordered", Bold = requested != 0, Color = requested > 0 ? "lightblue" : "white", Text = (requested != 0 ? TguiFormat.JsNumber(requested) : "Zero") + " sheets ordered." }));
            }
            row.With(new UiNode(UiKind.StackItem) { Key = "trend", Width = "20%", Color = trend == "up" ? "green" : trend == "down" ? "red" : "white", Text = title + " is trending " + trend + "." });

            var buttons = new UiNode(UiKind.StackItem) { Key = "buttons" };
            foreach (int amount in Quantities)
            {
                int qty = amount;
                buttons.With(new UiNode(UiKind.Button)
                {
                    Key = "buy:" + qty,
                    Disabled = catastrophe || price < minThreshold || creditBalance - totalOrderCost < price * qty * multiplier || requested + qty > quantity,
                    Tooltip = TguiFormat.JsNumber(price * qty),
                    Text = qty == 1 ? "Buy 1" : qty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    OnClick = () => c.Act("buy", ("quantity", qty), ("material", name)),
                });
            }
            content.With(new UiNode(UiKind.Section) { Key = "material:" + index++ }.With(new UiNode(UiKind.Stack) { Key = "materialStack", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "info", Width = "82%" }.With(row),
                buttons)));
        }

        content.With(new UiNode(UiKind.NoticeBox) { Key = "instructionsBox", Color = "info" }.With(new UiNode(UiKind.Collapsible) { Key = "instructions", Title = "Instructions", Color = "blue" }.With(
            new UiNode(UiKind.Box)
            {
                Key = "text",
                Text = "Buy orders for material sheets placed here will be ordered on the next cargo shipment.\n\nTo sell materials, please insert sheets or similar stacks of materials. All minerals sold on the market directly are subject to a scaling value decrease per material, but this will recover over time. To prevent market manipulation, all registered traders can buy a total of 10 full stacks of materials at a time.\n\nWhen selling materials, prices will be decreased based on the elastic modifier of the material, which will recover over time.\n\nAll new purchases will include the cost of the shipped crate, which may be recycled afterwards.",
            })));
        return c.Window(990, 615, null, content);
    }

    // tg: MatMarket.tsx:310
    private static UiNode MarketCrashModal() => new UiNode(UiKind.Modal) { Key = "crash", TextAlign = "center" }.With(
        new UiNode(UiKind.Box) { Key = "l1", Text = "ATTENTION! THE MARKET HAS CRASHED" },
        new UiNode(UiKind.Box) { Key = "l2", Text = "ALL MATERIALS ARE NOW WORTHLESS" },
        new UiNode(UiKind.Box) { Key = "l3", Text = "TRADING CIRCUIT BREAKER HAS BEEN ENGAGED FOR ALL TRADERS" },
        new UiNode(UiKind.Box) { Key = "l4", Bold = true, Text = "DO NOT PANIC, WE ARE FIXING THIS" });

    // tg: MatMarket.tsx:330  rangeText
    private static string RangeText(double minPrice, double maxPrice) =>
        "This material can be bought and sold between " + TguiFormat.FormatMoney(minPrice) + " - " + TguiFormat.FormatMoney(maxPrice) + " cr.";

    // tgui-core's toTitleCase: each word capitalized
    private static string TitleCase(string text) => string.Join(' ', (text ?? "").Split(' ').Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));
}
