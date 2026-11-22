using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosCouponMaster.tsx (gaps P4b, D298: the Coupon Master
// app): the code field, the redeemed and the printed coupons. tgui's placeholder is the field's Tooltip (the kit's Input shows its
// Tooltip as one). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosCouponMaster
{
    // tg: NtosCouponMaster.tsx:18
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("valid_id"))
            return NtosWindow.Render(c, 400, 400, null, new UiNode(UiKind.NoticeBox) { Key = "no-id", Color = "danger", Text = "No valid bank account detected. Insert a valid ID." });

        UiNode Coupons(string key, string title, string field)
        {
            var section = new UiNode(UiKind.Section) { Key = key, Title = title };
            int index = 0;
            foreach (UiValue coupon in data.Get(field).Items)
                section.With(new UiNode(UiKind.Box) { Key = "coupon:" + index++, Candystripe = true, Text = coupon.Str("goody") + " (" + coupon.Str("discount") + "% OFF)" });
            return section;
        }

        return NtosWindow.Render(c, 400, 400, null,
            new UiNode(UiKind.NoticeBox) { Key = "info", Color = "info", Text = "You can print redeemed coupons by right-clicking a photocopier." },
            new UiNode(UiKind.Input)
            {
                Key = "code",
                FontSize = 1.2,
                Tooltip = "Insert your coupon code here",
                TextValue = "",
                OnCommit = value => c.Act("redeem", ("code", value)),
            },
            Coupons("redeemed", "Redeemed Coupons", "redeemed_coupons"),
            Coupons("printed", "Printed Coupons", "printed_coupons"));
    }
}
