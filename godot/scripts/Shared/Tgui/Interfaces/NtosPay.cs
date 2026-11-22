using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosPay.tsx (gaps P4a, D327: the NT Pay app): the
// account's name, pay token and balance, the transfer by pay token, the token lookup by name and the transaction history. tgui's
// Tooltip around the pay token and amount fields is the field's placeholder here (the kit's Input shows its Tooltip as one).
// Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosPay
{
    // tg: NtosPay.tsx:32
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 495, 655, null, Content(c));

    // tg: NtosPay.tsx:42  NtosPayContent
    private static UiNode Content(TguiContext c)
    {
        string name = c.Data.Str("name", null);
        if (string.IsNullOrEmpty(name))
            return new UiNode(UiKind.NoticeBox) { Key = "no-id", Text = "You need to insert your ID card into the card slot in order to use this application." };

        return new UiNode(UiKind.Stack) { Key = "pay", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "intro" }.With(Introduction(c)),
            new UiNode(UiKind.StackItem) { Key = "transfer" }.With(TransferSection(c)),
            new UiNode(UiKind.StackItem) { Key = "history", Grow = 1 }.With(TransactionHistory(c)));
    }

    /** Displays the user's name and balance. */
    // tg: NtosPay.tsx:69
    private static UiNode Introduction(TguiContext c)
    {
        double money = c.Data.Num("money");
        return new UiNode(UiKind.Section) { Key = "introduction", TextAlign = "center" }.With(new UiNode(UiKind.Table).With(
            new UiNode(UiKind.TableRow) { Key = "hi" }.With(new UiNode(UiKind.TableCell) { Text = "Hi, " + c.Data.Str("name") + "." }),
            new UiNode(UiKind.TableRow) { Key = "token" }.With(new UiNode(UiKind.TableCell) { Text = "Your pay token is " + c.Data.Str("owner_token") + "." }),
            new UiNode(UiKind.TableRow) { Key = "balance" }.With(new UiNode(UiKind.TableCell)
            {
                Text = "Account balance: " + TguiFormat.JsNumber(money) + " credit" + (money == 1 ? "" : "s"),
            })));
    }

    /** Displays the transfer section. */
    // tg: NtosPay.tsx:86
    private static UiNode TransferSection(TguiContext c)
    {
        double money = c.Data.Num("money");
        string token = c.UseState("token", "");
        double moneyToSend = c.UseState("moneyToSend", 1.0);
        string nameToToken = c.UseState("nameToToken", "");
        bool moneyToSendIsValid = c.UseState("moneyToSendIsValid", true);

        return new UiNode(UiKind.Stack) { Key = "transfer-row" }.With(
            new UiNode(UiKind.StackItem) { Key = "send" }.With(new UiNode(UiKind.Section) { Key = "transfer-money", Title = "Transfer Money" }.With(
                new UiNode(UiKind.Box) { Key = "token-box" }.With(new UiNode(UiKind.Input)
                {
                    Key = "token",
                    Width = "190px",
                    Tooltip = "Pay Token",
                    TextValue = token,
                    OnInput = value => c.SetState("token", value ?? ""),
                }),
                new UiNode(UiKind.NumberInput)
                {
                    Key = "amount",
                    Width = "83px",
                    MinValue = 1,
                    MaxValue = money,
                    Value = moneyToSend,
                    OnChange = value => c.SetState("moneyToSend", value),
                    OnValidation = valid => c.SetState("moneyToSendIsValid", valid),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "send-credits",
                    Text = "Send credits",
                    Disabled = !moneyToSendIsValid,
                    OnClick = () => c.Act("Transaction", ("token", token), ("amount", moneyToSend)),
                })),
            new UiNode(UiKind.StackItem) { Key = "lookup" }.With(new UiNode(UiKind.Section) { Key = "get-token", Title = "Get Token", Width = "270px", Height = "98px" }.With(
                new UiNode(UiKind.Box) { Key = "name-box" }.With(
                    new UiNode(UiKind.Input)
                    {
                        Key = "wanted-name",
                        Width = "190px",
                        Tooltip = "Full name of account.",
                        TextValue = nameToToken,
                        OnInput = value => c.SetState("nameToToken", value ?? ""),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "get-it",
                        Text = "Get it",
                        OnClick = () => c.Act("GetPayToken", ("wanted_name", nameToToken)),
                    }),
                new UiNode(UiKind.Divider) { Key = "divider" },
                new UiNode(UiKind.Box) { Key = "wanted-token", Text = c.Data.Str("wanted_token", "") })));
    }

    /** Displays the transaction history. */
    // tg: NtosPay.tsx:166
    private static UiNode TransactionHistory(TguiContext c)
    {
        IReadOnlyList<UiValue> transactions = c.Data.Arr("transaction_list");
        var table = new UiNode(UiKind.Table) { Key = "transactions" };
        int index = 0;
        foreach (UiValue log in transactions)
        {
            double adjusted = log.Num("adjusted_money");
            table.With(new UiNode(UiKind.TableRow) { Key = "log:" + index++, Candystripe = true, Color = adjusted < 1 ? "red" : "green" }.With(
                new UiNode(UiKind.TableCell) { Key = "amount", Width = "100px", Text = (adjusted > 1 ? "+" : "") + TguiFormat.JsNumber(adjusted) },
                new UiNode(UiKind.TableCell) { Key = "reason", TextAlign = "center", Text = log.Str("reason") }));
        }
        // tg: NtosPay.tsx:194  TableHeaders, the sticky title of the inner section
        var headers = new UiNode(UiKind.Table) { Key = "headers" }.With(new UiNode(UiKind.TableRow).With(
            new UiNode(UiKind.TableCell) { Key = "amount-header", Color = "label", Width = "100px", Text = "Amount" },
            new UiNode(UiKind.TableCell) { Key = "reason-header", Color = "label", TextAlign = "center", Text = "Reason" }));
        return new UiNode(UiKind.Section) { Key = "transaction-history", Fill = true, Title = "Transaction History" }.With(
            new UiNode(UiKind.Section) { Key = "history-table", Fill = true, Scrollable = true }.With(headers, table));
    }
}
