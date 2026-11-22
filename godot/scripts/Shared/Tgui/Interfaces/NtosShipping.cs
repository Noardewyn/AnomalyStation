using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosShipping.tsx (gaps P4d, D298: the GrandArk Exporter
// app): the current user, the inserted card, the paper, the profit on sale, and the shipping options. Engine-free (compiled into the
// tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosShipping
{
    // tg: NtosShipping.tsx:15
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 450, 350, null, ShippingHub(c), ShippingOptions(c));

    /** Returns information about the current user, available paper, etc */
    // tg: NtosShipping.tsx:27
    private static UiNode ShippingHub(TguiContext c)
    {
        UiValue data = c.Data;
        string currentUser = data.Str("current_user", null);
        string cardOwner = data.Str("card_owner", null);
        return new UiNode(UiKind.Section)
        {
            Key = "hub",
            Title = "NTOS Shipping Hub.",
            Buttons = new UiNode(UiKind.Button) { Key = "ejectid", Icon = "eject", Text = "Eject Id", OnClick = () => c.Act("ejectid") },
        }.With(new UiNode(UiKind.LabeledList) { Key = "hubList" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "user", Label = "Current User", Text = string.IsNullOrEmpty(currentUser) ? "N/A" : currentUser },
            new UiNode(UiKind.LabeledItem) { Key = "card", Label = "Inserted Card", Text = string.IsNullOrEmpty(cardOwner) ? "N/A" : cardOwner },
            new UiNode(UiKind.LabeledItem) { Key = "paper", Label = "Available Paper", Text = data.Str("paperamt") },
            new UiNode(UiKind.LabeledItem) { Key = "split", Label = "Profit on Sale", Text = data.Str("barcode_split") + "%" }));
    }

    /** Returns shipping options */
    // tg: NtosShipping.tsx:59
    private static UiNode ShippingOptions(TguiContext c)
    {
        UiValue data = c.Data;
        return new UiNode(UiKind.Section) { Key = "options", Title = "Shipping Options" }.With(
            new UiNode(UiKind.Box) { Key = "selectBox" }.With(new UiNode(UiKind.Button) { Key = "selectid", Icon = "id-card", Tooltip = "The currently ID card will become the current user.", Disabled = !data.Truthy("has_id_slot"), Text = "Set Current ID", OnClick = () => c.Act("selectid") }),
            new UiNode(UiKind.Box) { Key = "printBox" }.With(new UiNode(UiKind.Button) { Key = "print", Icon = "print", Tooltip = "Print a barcode to use on a wrapped package.", Disabled = !data.Truthy("current_user"), Text = "Print Barcode", OnClick = () => c.Act("print") }),
            new UiNode(UiKind.Box) { Key = "splitBox" }.With(new UiNode(UiKind.Button) { Key = "setsplit", Icon = "tags", Tooltip = "Set how much profit you'd like on your package.", Text = "Set Profit Margin", OnClick = () => c.Act("setsplit") }),
            new UiNode(UiKind.Box) { Key = "resetBox" }.With(new UiNode(UiKind.Button) { Key = "resetid", Icon = "sync-alt", Text = "Reset ID", OnClick = () => c.Act("resetid") }));
    }
}
