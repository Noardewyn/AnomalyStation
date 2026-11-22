using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosCard.tsx (gaps S4a, D327): Plexagon Access
// Management, the ID card writer: the login and target card slots, the card's details, the job templates and the access list
// (common/AccessList, the agent card's). Flex is drawn as a horizontal Stack (the client's layout has no Flex). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosCard
{
    private static List<string> Refs(IReadOnlyList<UiValue> list) =>
        list.Select(v => v.Text ?? v.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

    // tg: NtosCard.tsx:57
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 500, 670, null, new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(NtosCardContent(c)));

    // tg: NtosCard.tsx:67
    public static UiNode NtosCardContent(TguiContext c)
    {
        UiValue data = c.Data;
        string authedUser = data.Get("authed_user") is { IsNull: false } a ? a.Text : null;
        UiValue modifiedCard = data.Get("modified_card") is { IsNull: false } m ? m : null;
        bool showAccess = modifiedCard != null && !string.IsNullOrEmpty(authedUser);

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "pages" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "login-page", Width = "100%" }.With(LoginPage(c)),
                new UiNode(UiKind.StackItem) { Key = "card-page", Width = "100%" }.With(IdCardPage(c)))),
            showAccess
                ? new UiNode(UiKind.StackItem) { Key = "templates" }.With(new UiNode(UiKind.Section)
                {
                    Title = "Templates",
                    Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "card-templates-help",
                        Icon = "question-circle",
                        Tooltip = "Will attempt to apply all access for the template to the ID card.\nDoes not use wildcards unless the template specifies them.",
                    },
                }.With(modifiedCard.Truthy("has_trim")
                    ? TemplateDropdown(c, data.Get("templates"))
                    : new UiNode(UiKind.Box) { Text = "Templates require a trim already applied to the card. Please use an ID Painter to apply a trim." }))
                : null,
            new UiNode(UiKind.StackItem) { Key = "access", Grow = 1 }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(showAccess
                    ? AccessList.Render(c, data.Arr("regions"), modifiedCard.Get("wildcard_slots"), Refs(modifiedCard.Arr("access_on_card")),
                        (reference, wildcard) => c.Act("PRG_access", ("access_target", reference), ("access_wildcard", wildcard)),
                        Refs(modifiedCard.Arr("trim_access")), data.Get("access_flags"), data.Get("access_flag_names"), data.Get("wildcard_flags"),
                        ConfirmWith(c, "card-terminate", new UiNode(UiKind.Button) { Text = "Terminate Employment", Color = "bad", OnClick = () => c.Act("PRG_terminate") }, "Fire Employee?"),
                        data.Truthy("show_basic"))
                    : null))));
    }

    // tg: NtosCard.tsx:147
    private static UiNode LoginPage(TguiContext c)
    {
        UiValue data = c.Data;
        string authedUser = data.Get("authed_user") is { IsNull: false } a ? a.Text : null;
        UiValue authCard = data.Get("auth_card") is { IsNull: false } card ? card : null;
        bool isHoldingId = data.Truthy("is_holding_id");

        return new UiNode(UiKind.Section).With(new UiNode(UiKind.Stack) { Wrap = true }.With(
            new UiNode(UiKind.StackItem) { Key = "notice", Grow = 1 }.With(new UiNode(UiKind.NoticeBox)
            {
                Color = string.IsNullOrEmpty(authedUser) ? null : "info",
                Text = !string.IsNullOrEmpty(authedUser) ? "Login: " + authedUser : "Please log in to continue.",
            }),
            new UiNode(UiKind.StackItem) { Key = "buttons", Width = "100%" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "card-main-id",
                    Fluid = true,
                    Icon = "eject",
                    Disabled = authCard == null && !isHoldingId,
                    Text = authCard != null ? authCard.Str("id_owner") + " (" + authCard.Str("id_rank") + ")" : "Insert ID",
                    OnClick = () => c.Act(authCard != null ? "PRG_remove_main_id" : "PRG_insert_main_id"),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "card-login",
                    Icon = "sign-in-alt",
                    Color = !string.IsNullOrEmpty(authedUser) ? "bad" : "good",
                    Text = !string.IsNullOrEmpty(authedUser) ? "Log Out" : "Log In",
                    OnClick = () => c.Act(!string.IsNullOrEmpty(authedUser) ? "PRG_logout" : "PRG_authenticate"),
                })))));
    }

    // tg: NtosCard.tsx:196
    private static UiNode IdCardPage(TguiContext c)
    {
        UiValue data = c.Data;
        string authedUser = data.Get("authed_user") is { IsNull: false } a ? a.Text : null;
        UiValue modifiedCard = data.Get("modified_card") is { IsNull: false } m ? m : null;
        bool isHoldingId = data.Truthy("is_holding_id");

        var section = new UiNode(UiKind.Section).With(new UiNode(UiKind.Stack) { Wrap = true }.With(
            new UiNode(UiKind.StackItem) { Key = "buttons", Width = "100%" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "card-alt-id",
                    Fluid = true,
                    Icon = "eject",
                    Disabled = modifiedCard == null && !isHoldingId,
                    Text = modifiedCard != null ? modifiedCard.Str("id_owner") + " (" + modifiedCard.Str("id_rank") + ")" : "Insert ID",
                    OnClick = () => c.Act(modifiedCard != null ? "PRG_remove_alt_id" : "PRG_insert_alt_id"),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "card-print",
                    Icon = "print",
                    Disabled = modifiedCard == null || string.IsNullOrEmpty(authedUser),
                    Text = "Print Report",
                    OnClick = () => c.Act("PRG_print"),
                })))));
        if (modifiedCard != null && !string.IsNullOrEmpty(authedUser))
        {
            section.With(
                new UiNode(UiKind.Stack) { Key = "details" }.With(
                    new UiNode(UiKind.StackItem) { Text = "Details:" },
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Input)
                    {
                        Key = "card-name",
                        Width = "100%",
                        TextValue = modifiedCard.Str("id_owner"),
                        OnCommit = value => c.Act("PRG_edit", ("name", value)),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.NumberInput)
                    {
                        Key = "card-age",
                        Step = 1,
                        Value = modifiedCard.Num("id_age"),
                        Unit = "Years",
                        MinValue = 17,
                        MaxValue = 85,
                        OnChange = value => c.Act("PRG_age", ("id_age", value)),
                    })),
                new UiNode(UiKind.Stack) { Key = "assignment" }.With(
                    new UiNode(UiKind.StackItem) { Text = "Assignment:" },
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Input)
                    {
                        Key = "card-rank",
                        Fluid = true,
                        TextValue = modifiedCard.Str("id_rank"),
                        OnCommit = value => c.Act("PRG_assign", ("assignment", value)),
                    })));
        }
        return section;
    }

    // tg: NtosCard.tsx:286
    private static UiNode TemplateDropdown(TguiContext c, UiValue templates)
    {
        IReadOnlyList<KeyValuePair<string, UiValue>> templateKeys = templates?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>();
        if (templateKeys.Count == 0)
            return null;

        return new UiNode(UiKind.Stack).With(new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Dropdown)
        {
            Key = "card-template",
            Width = "100%",
            Tooltip = "Select a template...",
            Options = templateKeys.Select(path => path.Value.Text ?? "").ToList(),
            TextValue = "None",
            OnSelected = sel => c.Act("PRG_template", ("name", sel)),
        }));
    }

    // tgui-core's Button.Confirm with its confirmContent
    private static UiNode ConfirmWith(TguiContext c, string key, UiNode button, string confirmContent)
    {
        UiNode confirm = c.ButtonConfirm(key, button);
        if (c.UseState("confirm:" + key, false))
            confirm.Text = confirmContent;
        return confirm;
    }
}
