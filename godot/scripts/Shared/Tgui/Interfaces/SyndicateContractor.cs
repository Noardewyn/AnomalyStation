using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SyndicateContractor.tsx (syndicate X1a, D316: the
// contractor's Syndicate Contractor Uplink app). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SyndicateContractor
{
    // tg: SyndicateContractor.tsx:19  enum CONTRACT
    private const double ContractInactive = 1;
    private const double ContractComplete = 5;

    // tg: SyndicateContractor.tsx:51
    private static readonly string[] InfoEntries =
    [
        "SyndTract v2.0",
        "",
        "We've identified potentional high-value targets that are",
        "currently assigned to your mission area. They are believed",
        "to hold valuable information which could be of immediate",
        "importance to our organisation.",
        "",
        "Listed below are all of the contracts available to you. You",
        "are to bring the specified target to the designated",
        "drop-off, and contact us via this uplink. We will send",
        "a specialised extraction unit to put the body into.",
        "",
        "We want targets alive - but we will sometimes pay slight",
        "amounts if they're not, you just won't receive the shown",
        "bonus. You can redeem your payment through this uplink in",
        "the form of raw telecrystals, which can be put into your",
        "regular Syndicate uplink to purchase whatever you may need.",
        "We provide you with these crystals the moment you send the",
        "target up to us, which can be collected at anytime through",
        "this system.",
        "",
        "Targets extracted will be ransomed back to the station once",
        "their use to us is fulfilled, with us providing you a small",
        "percentage cut. You may want to be mindful of them",
        "identifying you when they come back. We provide you with",
        "a standard contractor loadout, which will help cover your",
        "identity.",
    ];

    // tg: SyndicateContractor.tsx:81
    public static UiNode Render(TguiContext c)
        => NtosWindow.Render(c, 500, 600, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(Content(c))));

    // tg: SyndicateContractor.tsx:91
    private static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        string error = data.Str("error");
        bool logged_in = data.Truthy("logged_in");
        bool first_load = data.Truthy("first_load");
        bool info_screen = data.Truthy("info_screen");

        // tg makes the account number with Math.random() at each render; it is drawn once here, for the window
        double account = c.UseState("account", double.NaN);
        if (double.IsNaN(account))
        {
            account = Math.Round(Random.Shared.NextDouble() * 20000);
            c.Local.SetQuiet("account", account);
        }
        string[] terminalMessages =
        [
            "Recording biometric data...",
            "Analyzing embedded syndicate info...",
            "STATUS CONFIRMED",
            "Contacting syndicate database...",
            "Awaiting response...",
            "Awaiting response...",
            "Awaiting response...",
            "Awaiting response...",
            "Awaiting response...",
            "Awaiting response...",
            "Response received, ack 4851234...",
            "CONFIRM ACC " + account.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "Setting up private accounts...",
            "CONTRACTOR ACCOUNT CREATED",
            "Searching for available contracts...",
            "Searching for available contracts...",
            "Searching for available contracts...",
            "Searching for available contracts...",
            "CONTRACTS FOUND",
            "WELCOME, AGENT",
        ];

        // tg: SyndicateContractor.tsx:116  errorPane
        UiNode errorPane = string.IsNullOrEmpty(error) ? null : new UiNode(UiKind.Modal) { Key = "error", Background = "red" }.With(
            new UiNode(UiKind.Stack) { Align = "center" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Icon = "exclamation-triangle", Size = 4 }),
                new UiNode(UiKind.StackItem) { Grow = 1, TextAlign = "center" }.With(
                    new UiNode(UiKind.Box) { Width = "260px", TextAlign = "left", Text = error, Wrap = true },
                    new UiNode(UiKind.Button) { Key = "dismiss", Text = "Dismiss", OnClick = () => c.Act("PRG_clear_error") })));

        if (!logged_in)
        {
            var section = new UiNode(UiKind.Section) { Key = "register" }.With(
                new UiNode(UiKind.Box) { TextAlign = "center" }.With(
                    new UiNode(UiKind.Button) { Key = "login", Color = "transparent", Text = "REGISTER USER", OnClick = () => c.Act("PRG_login") }));
            if (!string.IsNullOrEmpty(error))
                section.With(new UiNode(UiKind.NoticeBox) { Text = error });
            return section;
        }

        if (first_load)
        {
            return new UiNode(UiKind.Box) { Key = "first-load", Background = "rgba(0, 0, 0, 0.8)" }.With(
                FakeTerminal.Render(c, "terminal:first", terminalMessages, finishedTimeout: 3000, onFinished: () => c.Act("PRG_set_first_load_finished")));
        }

        if (info_screen)
        {
            return new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Box) { Key = "info", Background = "rgba(0, 0, 0, 0.8)" }.With(
                    FakeTerminal.Render(c, "terminal:info", InfoEntries, linesPerSecond: 10)),
                new UiNode(UiKind.Button)
                {
                    Key = "continue",
                    Fluid = true,
                    Color = "transparent",
                    TextAlign = "center",
                    Text = "CONTINUE",
                    OnClick = () => c.Act("PRG_toggle_info"),
                });
        }

        var content = new UiNode(UiKind.Fragment);
        if (errorPane != null)
            content.With(errorPane);
        return content.With(StatusPane(c), ContractsTab(c));
    }

    // tg: SyndicateContractor.tsx:173
    private static UiNode StatusPane(TguiContext c)
    {
        UiValue data = c.Data;
        double redeemable_tc = data.Num("redeemable_tc");
        return new UiNode(UiKind.Section)
        {
            Key = "status",
            Title = "Contractor Status",
            Buttons = new UiNode(UiKind.Button) { Key = "info-again", Color = "transparent", Text = "View Information Again", OnClick = () => c.Act("PRG_toggle_info") },
        }.With(new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem)
                {
                    Label = "TC Available",
                    Text = Fmt(redeemable_tc),
                    Buttons = new UiNode(UiKind.Button) { Key = "claim", Text = "Claim", Disabled = redeemable_tc <= 0, OnClick = () => c.Act("PRG_redeem_TC") },
                },
                new UiNode(UiKind.LabeledItem) { Label = "TC Earned", Text = Fmt(data.Num("earned_tc")) })),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Contracts Completed", Text = Fmt(data.Num("contracts_completed")) },
                new UiNode(UiKind.LabeledItem) { Label = "Current Status", Text = "ACTIVE" }))));
    }

    // tg: SyndicateContractor.tsx:225
    private static UiNode ContractsTab(TguiContext c)
    {
        UiValue data = c.Data;
        bool ongoing_contract = data.Truthy("ongoing_contract");
        bool extraction_enroute = data.Truthy("extraction_enroute");
        var list = new UiNode(UiKind.Section)
        {
            Key = "contracts",
            Title = "Available Contracts",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "extraction",
                Text = "Call Extraction",
                Disabled = !ongoing_contract || extraction_enroute,
                OnClick = () => c.Act("PRG_call_extraction"),
            },
        };
        foreach (UiValue contract in data.Arr("contracts"))
        {
            double status = contract.Num("status");
            if (ongoing_contract && status != 2)
                continue;
            bool active = status > ContractInactive;
            if (status >= ContractComplete)
                continue;
            string target = contract.Str("target");
            double id = contract.Num("id");
            list.With(new UiNode(UiKind.Section)
            {
                Key = "contract:" + Fmt(id),
                Title = !string.IsNullOrEmpty(target) ? $"{target} ({contract.Str("target_rank")})" : "Invalid Target",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Box) { Bold = true, Text = $"{Fmt(contract.Num("payout"))} (+{Fmt(contract.Num("payout_bonus"))}) TC" },
                    new UiNode(UiKind.Button)
                    {
                        Key = "contract-button:" + Fmt(id),
                        Disabled = contract.Truthy("extraction_enroute"),
                        Color = active ? "bad" : null,
                        Text = active ? "Abort" : "Accept",
                        OnClick = () => c.Act(active ? "PRG_contract_abort" : "PRG_contract-accept", ("contract_id", id)),
                    }),
            }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Text = contract.Str("message"), Wrap = true }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Bold = true, Text = "Dropoff Location:" }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = contract.Str("dropoff") }))))));
        }
        return new UiNode(UiKind.Fragment).With(
            list,
            new UiNode(UiKind.Section) { Key = "locator", Title = "Dropoff Locator", TextAlign = "center", Opacity = ongoing_contract ? 1 : 0 }.With(
                new UiNode(UiKind.Box) { Bold = true, Text = data.Str("dropoff_direction") }));
    }

    private static string Fmt(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
