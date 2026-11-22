using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BankMachine.tsx (gaps P4a, D327: the vault's bank
// machine): the station's vault balance and the siphon button. tgui-core's AnimatedNumber renders as its text. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BankMachine
{
    // tg: BankMachine.tsx:20
    public static UiNode Render(TguiContext c)
    {
        double current_balance = c.Data.Num("current_balance");
        bool siphoning = c.Data.Truthy("siphoning");
        string station_name = c.Data.Str("station_name");
        return c.Window(350, 155, null,
            new UiNode(UiKind.NoticeBox) { Key = "authorized", Color = "danger", Text = "Authorized personnel only" },
            new UiNode(UiKind.Section) { Key = "vault", Title = station_name + " Vault" }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem)
                {
                    Key = "balance",
                    Label = "Current Balance",
                    Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "siphon",
                        Icon = siphoning ? "times" : "sync",
                        Text = siphoning ? "Stop Siphoning" : "Siphon Credits",
                        Selected = siphoning,
                        OnClick = () => c.Act(siphoning ? "halt" : "siphon"),
                    },
                    Text = TguiFormat.FormatMoney(current_balance) + " cr",
                })));
    }
}
