using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Vendatray.tsx (gaps ST2): the vend-a-tray's product
// (its picture, name and price with the price button), the open and purchase buttons and its registration
// (structures/displaycase.dm:549 ui_data, :558 ui_static_data). The picture is the product's icon and state, drawn from tg's sprite
// (DV500). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Vendatray
{
    // tg: Vendatray.tsx:16
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string productName = ProductName(data);
        bool registered = data.Truthy("registered");
        var content = new List<UiNode>
        {
            new UiNode(UiKind.Stack) { Key = "product" }.With(
                new UiNode(UiKind.StackItem) { Key = "image_item" }.With(!string.IsNullOrEmpty(productName) ? VendingImage(data) : null),
                new UiNode(UiKind.StackItem) { Key = "info_item", Grow = 1 }.With(ProductInfo(c, data, productName))),
        };
        if (registered)
            // tg: Vendatray.tsx:30  <Section italic>Pays to the account of {owner_name}.</Section>
            content.Add(new UiNode(UiKind.Section) { Key = "owner" }.With(new UiNode(UiKind.Box) { Key = "owner_text", Text = "Pays to the account of " + data.Str("owner_name") + "." }));
        else
        {
            content.Add(new UiNode(UiKind.Section) { Key = "unregistered" }.With(new UiNode(UiKind.Box) { Key = "unregistered_text", Text = "Tray is unregistered." }));
            content.Add(new UiNode(UiKind.Button) { Key = "register", Fluid = true, Icon = "cash-register", Text = "Register Tray", Disabled = registered, OnClick = () => c.Act("Register") });
        }
        // tg: Vendatray.tsx:21  <Window width={300} height={270}>
        return c.Window(300, 270, null, content.ToArray());
    }

    private static string ProductName(UiValue data) => data.Field("product_name") is { IsNull: false } n ? n.AsStr() : null;

    /** Lists product info and buttons to open or purchase */
    // tg: Vendatray.tsx:49
    private static UiNode ProductInfo(TguiContext c, UiValue data, string productName)
    {
        bool trayOpen = data.Truthy("tray_open");
        bool hasProduct = !string.IsNullOrEmpty(productName);
        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Section) { Key = "info", FontSize = "18px", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Key = "name", Bold = true, Text = hasProduct ? productName : "Empty" },
                new UiNode(UiKind.Box) { Key = "cost_row", FontSize = "16px" }.With(
                    new UiNode(UiKind.Box) { Key = "cost", Text = (hasProduct ? TguiFormat.JsNumber(data.Num("product_cost")) : "N/A") + " cr " },
                    new UiNode(UiKind.Button) { Key = "adjust", Icon = "pen", OnClick = () => c.Act("Adjust") })),
            new UiNode(UiKind.Button)
            {
                Key = "open",
                Fluid = true,
                Icon = "window-restore",
                Text = trayOpen ? "Open" : "Closed",
                Selected = trayOpen,
                OnClick = () => c.Act("Open"),
            },
            // tg: Vendatray.tsx:70  <Button.Confirm fluid icon="money-bill-wave" content="Purchase Item" disabled={!product_name} onClick={() => act('Buy')} />
            c.ButtonConfirm("buy", new UiNode(UiKind.Button)
            {
                Fluid = true,
                Icon = "money-bill-wave",
                Text = "Purchase Item",
                Disabled = !hasProduct,
                OnClick = () => c.Act("Buy"),
            }));
    }

    /** Produces an image from the product icon */
    // tg: Vendatray.tsx:82
    private static UiNode VendingImage(UiValue data)
    {
        UiValue icon = data.Field("product_icon");
        var section = new UiNode(UiKind.Section) { Key = "image", Height = "100%" };
        if (icon is { IsNull: false })
            section.With(new UiNode(UiKind.Box) { Key = "icon", DmIcon = icon.Str("icon"), DmIconState = icon.Str("icon_state"), Width = "96px", Height = "96px" });
        return section;
    }
}
