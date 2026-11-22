using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosNetMonitor.jsx (gaps S4b, D327): WireCarp, the relays'
// switches, the intrusion detection and the network's log, and the PDAs' mass messaging. useSharedState is the window's own state; the
// italic notice is plain text (the kit has no italics); several buttons in one slot are a horizontal Stack. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosNetMonitor
{
    // tg: NtosNetMonitor.jsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int tabMain = c.UseState("tab_main", 1);
        IReadOnlyList<UiValue> tablets = data.Arr("tablets");

        var content = new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "tabs" }.With(new UiNode(UiKind.Tabs).With(
                new UiNode(UiKind.Tab) { Key = "tab:ntnet", Icon = "network-wired", Text = "NtNet", Selected = tabMain == 1, OnClick = () => c.SetState("tab_main", 1) },
                new UiNode(UiKind.Tab) { Key = "tab:tablets", Icon = "tablet", Text = "Tablets (" + tablets.Count + ")", Selected = tabMain == 2, OnClick = () => c.SetState("tab_main", 2) })));
        if (tabMain == 1)
            content.With(new UiNode(UiKind.StackItem) { Key = "main" }.With(MainPage(c)));
        if (tabMain == 2)
            content.With(new UiNode(UiKind.StackItem) { Key = "tablets" }.With(TabletPage(c, tablets)));
        return NtosWindow.Render(c, 575, 700, null, content);
    }

    // tg: NtosNetMonitor.jsx:68
    private static UiNode MainPage(TguiContext c)
    {
        UiValue data = c.Data;
        bool idsalarm = data.Truthy("idsalarm");
        bool idsstatus = data.Truthy("idsstatus");

        var relays = new UiNode(UiKind.Section) { Key = "relays", Title = "Wireless Connectivity" };
        foreach (UiValue relay in data.Arr("ntnetrelays"))
        {
            string reference = relay.Str("ref");
            bool operational = relay.Truthy("is_operational");
            relays.With(new UiNode(UiKind.Section)
            {
                Key = "relay:" + reference,
                Title = relay.Str("name"),
                Buttons = c.ButtonConfirm("toggle-relay:" + reference, new UiNode(UiKind.Button)
                {
                    Color = operational ? "good" : "bad",
                    Text = operational ? "ENABLED" : "DISABLED",
                    OnClick = () => c.Act("toggle_relay", ("ref", reference)),
                }),
            });
        }

        var security = new UiNode(UiKind.Section) { Key = "security", Title = "Security Systems" };
        if (idsalarm)
            security.With(
                new UiNode(UiKind.NoticeBox) { Key = "incursion", Text = "NETWORK INCURSION DETECTED" },
                new UiNode(UiKind.Box) { Key = "incursion-text", Text = "Abnormal activity has been detected in the network. Check system logs for more information" });
        var logs = new UiNode(UiKind.Section)
        {
            Key = "log",
            Title = "System Log",
            Buttons = c.ButtonConfirm("purgelogs", new UiNode(UiKind.Button) { Icon = "trash", Text = "Clear Logs", OnClick = () => c.Act("purgelogs") }),
        };
        int i = 0;
        foreach (UiValue log in data.Arr("ntnetlogs"))
            logs.With(new UiNode(UiKind.Box) { Key = "entry:" + i++, Candystripe = true, Text = log.Str("entry") });
        security.With(
            new UiNode(UiKind.LabeledList).With(new UiNode(UiKind.LabeledItem)
            {
                Key = "ids",
                Label = "IDS Status",
                Buttons = new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "toggleIDS",
                        Icon = idsstatus ? "power-off" : "times",
                        Text = idsstatus ? "ENABLED" : "DISABLED",
                        Selected = idsstatus,
                        OnClick = () => c.Act("toggleIDS"),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "resetIDS", Icon = "sync", Text = "Reset", Color = "bad", OnClick = () => c.Act("resetIDS") })),
            }),
            logs);

        return new UiNode(UiKind.Section).With(
            new UiNode(UiKind.NoticeBox) { Key = "warning", Text = "WARNING: Disabling wireless transmitters when using a wireless device may prevent you from reenabling them!" },
            relays,
            security);
    }

    // tg: NtosNetMonitor.jsx:156
    private static UiNode TabletPage(TguiContext c, IReadOnlyList<UiValue> tablets)
    {
        if (tablets.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Text = "No tablets detected." };
        var list = new UiNode(UiKind.Stack) { Vertical = true };
        foreach (UiValue tablet in tablets)
        {
            string reference = tablet.Str("ref");
            bool spam = tablet.Truthy("enabled_spam");
            list.With(new UiNode(UiKind.Section)
            {
                Key = "tablet:" + reference,
                Title = tablet.Str("name"),
                Buttons = c.ButtonConfirm("mass:" + reference, new UiNode(UiKind.Button)
                {
                    Icon = spam ? "unlock" : "lock",
                    Color = spam ? "good" : "default",
                    Text = spam ? "Restrict Mass PDA" : "Allow Mass PDA",
                    OnClick = () => c.Act("toggle_mass_pda", ("ref", reference)),
                }),
            });
        }
        return new UiNode(UiKind.Section).With(
            new UiNode(UiKind.Stack) { Key = "head", Vertical = true }.With(new UiNode(UiKind.Section) { Fill = true, TextAlign = "center", Icon = "comment", Text = "Active Tablets" }),
            new UiNode(UiKind.Stack) { Key = "list", Vertical = true }.With(new UiNode(UiKind.Section) { Fill = true }.With(list)));
    }
}
