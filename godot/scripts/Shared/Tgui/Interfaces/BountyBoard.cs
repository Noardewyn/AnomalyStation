using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BountyBoard.tsx and NtosBountyBoard.tsx (silicons SI6b,
// D326: the newscaster's request board and its app): the user details with the account reset, each request with its owner, value,
// description, apply and delete buttons and its applicants to pay, and the new bounty's text and value. Engine-free (compiled into
// the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BountyBoard
{
    // tg: BountyBoard.tsx:46
    public static UiNode Render(TguiContext c)
    {
        var content = new UiNode(UiKind.Fragment) { Key = "content" }.With(Content(c));
        return c.Window(550, 600, null, content);
    }

    // tg: BountyBoard.tsx:56  BountyBoardContent
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> requests = data.Get("requests").Items;
        IReadOnlyList<UiValue> applicants = data.Get("applicants").Items;
        double bountyValue = data.Num("bountyValue");
        string userName = data.Get("user")?.Str("name");

        var requestList = new UiNode(UiKind.StackItem) { Key = "requests", Grow = 1 };
        foreach (UiValue request in requests)
        {
            string owner = request.Str("owner");
            UiValue accNumber = request.Get("acc_number");
            var applicantSection = new UiNode(UiKind.Section) { Key = "applicants", Title = "Request Applicants" };
            foreach (UiValue applicant in applicants)
            {
                if (!SameValue(applicant.Get("request_id"), accNumber))
                    continue;
                UiValue requesteeId = applicant.Get("requestee_id");
                applicantSection.With(new UiNode(UiKind.Stack) { Key = "applicant:" + TguiFormat.JsNumber(requesteeId.AsNum()) }.With(
                    new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, Background = "rgba(50, 50, 170, 0.5)", TextAlign = "center", Text = applicant.Str("name") },
                    new UiNode(UiKind.StackItem) { Key = "payItem", Align = "end" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "pay",
                        Fluid = true,
                        Icon = "cash-register",
                        Tooltip = "Pay out to this applicant.",
                        OnClick = () => c.Act("payApplicant", ("applicant", requesteeId), ("request", accNumber)),
                    })));
            }
            requestList.With(new UiNode(UiKind.Collapsible) { Key = "request:" + owner, Title = owner, Width = "300px" }.With(
                new UiNode(UiKind.Section) { Key = "section", Width = "300px" }.With(
                    new UiNode(UiKind.Stack) { Key = "head", Align = "baseline" }.With(
                        new UiNode(UiKind.StackItem) { Key = "owner", Bold = true, Width = "310px", Text = owner },
                        new UiNode(UiKind.StackItem) { Key = "value", Width = "100px", Text = TguiFormat.FormatMoney(request.Num("value")) + " cr" },
                        new UiNode(UiKind.StackItem) { Key = "buttons" }.With(
                            new UiNode(UiKind.Button) { Key = "apply", Fluid = true, Icon = "pen-fancy", Text = "Apply", Disabled = owner == userName, OnClick = () => c.Act("apply", ("request", accNumber)) },
                            new UiNode(UiKind.Button) { Key = "delete", Fluid = true, Icon = "trash-alt", Text = "Delete", Color = "red", OnClick = () => c.Act("deleteRequest", ("request", accNumber)) })),
                    // tg's BlockQuote: the description in italics between quotes
                    new UiNode(UiKind.Box) { Key = "description", Color = "label", TextAlign = "center", Text = "\"" + request.Str("description") + "\"" },
                    applicantSection)));
        }

        var newBounty = new UiNode(UiKind.StackItem) { Key = "newItem" }.With(new UiNode(UiKind.Collapsible) { Key = "new", Title = "New Bounty", Width = "220px", Color = "green" }.With(
            new UiNode(UiKind.Section) { Key = "newSection" }.With(
                new UiNode(UiKind.TextArea) { Key = "bountyText", Height = "150px", Width = "200px", OnCommit = value => c.Act("bountyText", ("bountytext", value)) },
                new UiNode(UiKind.Box) { Key = "valueRow" }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "bountyValue",
                        Unit = "cr",
                        MinValue = 1,
                        MaxValue = 1000,
                        Value = bountyValue,
                        Step = 1,
                        Width = "80px",
                        OnChange = value => c.Act("bountyVal", ("bountyval", value)),
                    },
                    new UiNode(UiKind.Button) { Key = "submit", Icon = "print", Text = "Submit bounty", Disabled = userName == "Unknown", OnClick = () => c.Act("createBounty") }))));

        return new UiNode(UiKind.Fragment) { Key = "bountyBoard" }.With(
            new UiNode(UiKind.Section)
            {
                Key = "userDetails",
                Title = "User Details",
                Buttons = new UiNode(UiKind.Button) { Key = "reset", Icon = "power-off", Text = "Reset Account", OnClick = () => c.Act("clear") },
            }.With(Vending.UserDetails(data)),
            new UiNode(UiKind.Stack) { Key = "board" }.With(requestList, newBounty));
    }

    // a request's account number and an applicant's request id are numbers from tg's ui_data (an account id)
    private static bool SameValue(UiValue a, UiValue b) =>
        a != null && b != null && (a.Kind == UiValueKind.Number && b.Kind == UiValueKind.Number ? a.AsNum() == b.AsNum() : a.AsStr() == b.AsStr());
}

public static class NtosBountyBoard
{
    // tg: NtosBountyBoard.tsx:4
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 550, 600, null, BountyBoard.Content(c));
}
