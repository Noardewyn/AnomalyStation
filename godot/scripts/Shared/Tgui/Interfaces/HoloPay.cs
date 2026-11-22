using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/HoloPay.tsx (gaps P4a, D327: the holographic pay stand):
// the payer's account, the terminal with the shop's logo, name and pay button (a confirm button for a forced fee), and the owner's
// settings (logo, name, forced fee). The name field's onBlur and the fee field's onEnter are the kit's commit (Enter or leaving the
// field). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class HoloPay
{
    // tg: HoloPay.tsx:33
    public static UiNode Render(TguiContext c)
    {
        string owner = c.Data.Str("owner", null);
        bool setupMode = c.UseState("setupMode", false);
        // User clicked the "Setup" or "Done" button.
        void OnClick() => c.SetState("setupMode", !setupMode);

        return c.Window(250, 300, "Holo Pay", string.IsNullOrEmpty(owner)
            ? new UiNode(UiKind.NoticeBox) { Key = "no-owner", Text = "Error! Swipe an ID first." }
            : new UiNode(UiKind.Stack) { Key = "holopay", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "account" }.With(AccountDisplay(c)),
                new UiNode(UiKind.StackItem) { Key = "screen", Grow = 1 }.With(!setupMode ? TerminalDisplay(c, OnClick) : SetupDisplay(c, OnClick))));
    }

    /**
     * Displays the current user's bank information (if any)
     */
    // tg: HoloPay.tsx:68
    private static UiNode AccountDisplay(TguiContext c)
    {
        UiValue user = c.Data.Get("user") is { Kind: UiValueKind.Object } u ? u : null;
        if (user == null)
            return new UiNode(UiKind.NoticeBox) { Key = "no-account", Text = "Error! No account detected." };

        return new UiNode(UiKind.Section) { Key = "account-display" }.With(new UiNode(UiKind.Table).With(new UiNode(UiKind.TableRow).With(
            new UiNode(UiKind.TableCell) { Key = "name" }.With(new UiNode(UiKind.Box) { Color = "label" }.With(
                new UiNode(UiKind.Icon) { Key = "check", Icon = "money-check", Color = "label" },
                new UiNode(UiKind.Box) { Key = "user-name", Text = " " + user.Str("name") })),
            new UiNode(UiKind.TableCell) { Key = "balance" } /* collapsing: the cell's shrink to its content is not drawn */.With(new UiNode(UiKind.Box) { Color = "good" }.With(
                new UiNode(UiKind.Box) { Key = "user-balance", Text = TguiFormat.JsNumber(user.Num("balance")) + " cr " },
                new UiNode(UiKind.Icon) { Key = "coins", Icon = "coins", Color = "gold" })))));
    }

    /**
     * Displays the payment processor. This is the main display.
     * Shows icon, name, payment button.
     */
    // tg: HoloPay.tsx:100
    private static UiNode TerminalDisplay(TguiContext c, Action onClick)
    {
        string description = c.Data.Str("description");
        double force_fee = c.Data.Num("force_fee");
        string name = c.Data.Str("name");
        string owner = c.Data.Str("owner");
        string shop_logo = c.Data.Str("shop_logo");
        UiValue user = c.Data.Get("user") is { Kind: UiValueKind.Object } u ? u : null;
        bool is_owner = owner == user?.Str("name");
        bool cannot_pay = is_owner || user == null || user.Num("balance") < 1 || user.Num("balance") < force_fee;

        UiNode payButton = force_fee != 0
            ? c.ButtonConfirm("pay", new UiNode(UiKind.Button)
            {
                Icon = "coins",
                Text = "Pay " + TguiFormat.JsNumber(force_fee) + " cr",
                Disabled = cannot_pay,
                Fluid = true,
                Height = "2rem",
                TextAlign = "center",
                OnClick = () => c.Act("pay"),
            })
            : new UiNode(UiKind.Button)
            {
                Key = "pay",
                Icon = "coins",
                Text = "Pay",
                Disabled = cannot_pay,
                Fluid = true,
                Height = "2rem",
                TextAlign = "center",
                OnClick = () => c.Act("pay"),
            };

        return new UiNode(UiKind.Section)
        {
            Key = "terminal",
            Title = "Terminal",
            Fill = true,
            Buttons = is_owner ? new UiNode(UiKind.Button) { Key = "setup", Icon = "edit", Text = "Setup", OnClick = onClick } : null,
        }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "logo", Align = "center" }.With(new UiNode(UiKind.Icon) { Key = "shop-logo", Color = "good", Icon = shop_logo, Size = 5 }),
            new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, TextAlign = "center" }.With(new UiNode(UiKind.Box)
            {
                Key = "shop-name",
                Color = "label",
                FontSize = "17px",
                Tooltip = description,
                Text = TguiFormat.DecodeHtmlEntities(name),
            }),
            new UiNode(UiKind.StackItem) { Key = "pay-item" }.With(payButton)));
    }

    /**
     * User has clicked "setup" button. Changes vars on the holopay.
     */
    // tg: HoloPay.tsx:165
    private static UiNode SetupDisplay(TguiContext c, Action onClick)
    {
        IReadOnlyList<string> available_logos = c.Data.Arr("available_logos").Select(v => v.AsStr()).ToList();
        double force_fee = c.Data.Num("force_fee");
        double max_fee = c.Data.Num("max_fee");
        string name = c.Data.Str("name");
        string shop_logo = c.Data.Str("shop_logo");
        bool isValid = c.UseState("isValid", true);

        return new UiNode(UiKind.Section)
        {
            Key = "settings",
            Title = "Settings",
            Fill = true,
            Scrollable = true,
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "done",
                Icon = "check",
                Text = "Done",
                OnClick = () =>
                {
                    c.Act("done");
                    onClick();
                },
            },
        }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "logo" }.With(
                new UiNode(UiKind.Box) { Key = "logo-label", Bold = true, Color = "label", Text = "Shop Logo" },
                new UiNode(UiKind.Dropdown)
                {
                    Key = "logo-dropdown",
                    Options = available_logos,
                    TextValue = shop_logo,
                    Width = "100%",
                    OnSelected = value => c.Act("logo", ("logo", value)),
                }),
            new UiNode(UiKind.StackItem) { Key = "name" }.With(
                new UiNode(UiKind.Box) { Key = "name-label", Bold = true, Color = "label", Text = "Name (3 - 42 chars)" },
                new UiNode(UiKind.TextArea)
                {
                    Key = "name-area",
                    Fluid = true,
                    Height = "3rem",
                    MaxLength = 42,
                    Tooltip = TguiFormat.DecodeHtmlEntities(name),
                    OnCommit = value =>
                    {
                        if (value?.Length > 3)
                            c.Act("rename", ("name", value));
                    },
                }),
            new UiNode(UiKind.StackItem) { Key = "fee" }.With(
                new UiNode(UiKind.Box) { Key = "fee-label", Bold = true, Color = "label", Text = "Forced Fee", Tooltip = "Set a forced fee rather than pay what you want." },
                new UiNode(UiKind.NumberInput)
                {
                    Key = "fee-input",
                    Fluid = true,
                    MinValue = 0,
                    MaxValue = max_fee,
                    Value = force_fee,
                    OnValidation = valid => c.SetState("isValid", valid),
                    OnChange = value =>
                    {
                        if (isValid)
                            c.Act("fee", ("amount", value));
                    },
                })));
    }
}
