using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CivCargoHoldTerminal.tsx and NtosCivCargoHoldTerminal.jsx
// (gaps P4c, D298): the civilian bounty terminal (the personal bounty with its pad and the three choices, the station bounties with
// their progress and Send & Claim) and the Civilian Bounties app. A bounty's description is HTML (tg's dangerouslySetInnerHTML: the
// kit's Html node). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CivCargoHoldTerminal
{
    private const string TabKey = "civtab";
    private const string BountyKey = "localBounty";
    private const string BountyTabKey = "bountyTab";
    private const string InText = "Welcome valued employee.";
    private const string OutText = "To begin, insert your ID into the console.";

    // tg: CivCargoHoldTerminal.tsx:53
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool idInserted = data.Truthy("id_inserted");
        string tab = c.UseState(TabKey, "personal");
        UiNode Tab(string id, string icon, string text, string color) => new UiNode(UiKind.Tab)
        {
            Key = "tab:" + id,
            Icon = icon,
            Text = text,
            Selected = tab == id,
            Background = tab == id ? color : null,
            OnClick = () => c.SetState(TabKey, id),
        };
        var content = new UiNode(UiKind.Fragment) { Key = "content" }.With(
            new UiNode(UiKind.Section) { Key = "tabsSection" }.With(new UiNode(UiKind.Tabs) { Key = "tabs", Fluid = true }.With(
                Tab("personal", "user", "Personal Bounties", "green"),
                Tab("station", "space-shuttle", "Station Bounties", "brown"))),
            new UiNode(UiKind.NoticeBox) { Key = "notice", Color = idInserted ? "blue" : null, Text = idInserted ? InText : OutText },
            tab == "personal" ? PersonalBountyBlock(c) : GlobalBountyBlock(c));
        return c.Window(580, 375, null, content);
    }

    // tg: CivCargoHoldTerminal.tsx:108
    private static UiNode PersonalBountyBlock(TguiContext c)
    {
        UiValue data = c.Data;
        bool pad = data.Truthy("pad");
        bool sending = data.Truthy("sending");
        bool idInserted = data.Truthy("id_inserted");
        bool hasBounty = data.Truthy("id_bounty_info");
        var buttons = new UiNode(UiKind.Fragment) { Key = "padButtons" }.With(
            new UiNode(UiKind.Button) { Key = "recalc", Icon = "sync", Tooltip = "Check Contents", Disabled = !pad || !idInserted, OnClick = () => c.Act("recalc") },
            new UiNode(UiKind.Button)
            {
                Key = "send",
                Icon = sending ? "times" : "arrow-up",
                Tooltip = sending ? "Stop Sending" : "Send Goods",
                Selected = sending,
                Disabled = !pad || !idInserted,
                OnClick = () => c.Act(sending ? "stop" : "send"),
            },
            new UiNode(UiKind.Button)
            {
                Key = "bounty",
                Icon = hasBounty ? "recycle" : "pen",
                Color = hasBounty ? "green" : "default",
                Tooltip = hasBounty ? "Replace Bounty" : "New Bounty",
                Disabled = !idInserted,
                OnClick = () => c.Act("bounty"),
            },
            new UiNode(UiKind.Button) { Key = "eject", Icon = "download", Text = "Eject ID", Disabled = !idInserted, OnClick = () => c.Act("eject") });
        return new UiNode(UiKind.Fragment) { Key = "personal" }.With(
            new UiNode(UiKind.Section) { Key = "padSection", Title = "Cargo Pad", Buttons = buttons }.With(new UiNode(UiKind.LabeledList) { Key = "padList" }.With(
                new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status", Color = pad ? "good" : "bad", Text = pad ? "Online" : "Not Found" },
                new UiNode(UiKind.LabeledItem) { Key = "report", Label = "Cargo Report", Text = data.Str("status_report") })),
            data.Truthy("picking") ? BountyPickBox(c) : BountyTextBox(c));
    }

    // tg: CivCargoHoldTerminal.tsx:158
    internal static UiNode BountyTextBox(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasBounty = data.Truthy("id_bounty_info");
        return new UiNode(UiKind.Section) { Key = "bountyInfo", Title = "Bounty Info" }.With(new UiNode(UiKind.LabeledList) { Key = "infoList" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "description", Label = "Description", Text = hasBounty ? data.Str("id_bounty_info") : "N/A, please add a new bounty." },
            new UiNode(UiKind.LabeledItem) { Key = "quantity", Label = "Quantity", Text = hasBounty ? data.Str("id_bounty_num") : "N/A" },
            new UiNode(UiKind.LabeledItem) { Key = "value", Label = "Value", Text = hasBounty ? data.Str("id_bounty_value") : "N/A" }));
    }

    // tg: CivCargoHoldTerminal.tsx:181  (htmlInfo: the console draws the description as HTML, the app as text)
    internal static UiNode BountyPickBox(TguiContext c, bool htmlInfo = true)
    {
        UiValue data = c.Data;
        var names = data.Get("id_bounty_names").Items;
        var infos = data.Get("id_bounty_infos").Items;
        var values = data.Get("id_bounty_values").Items;
        var row = new UiNode(UiKind.Stack) { Key = "choices", Wrap = true };
        for (int i = 0; i < 3; i++)
        {
            int pickValue = i + 1;
            string name = i < names.Count ? names[i].AsStr(null) : null;
            string info = i < infos.Count ? infos[i].AsStr(null) : null;
            string value = i < values.Count ? values[i].AsStr(null) : null;
            // tg: CivCargoHoldTerminal.tsx:218  BountyPickButton
            var button = new UiNode(UiKind.Button) { Key = "pick", Fluid = true, Color = "green", OnClick = () => c.Act("pick", ("value", pickValue)) }.With(
                new UiNode(UiKind.Box) { Key = "name", Text = name },
                htmlInfo
                    ? new UiNode(UiKind.Box) { Key = "infoBox", Color = "black", Background = "linen", TextAlign = "left" }.With(new UiNode(UiKind.Html) { Key = "info", TextValue = info ?? "" })
                    : new UiNode(UiKind.Box) { Key = "infoBox", Color = "black", Background = "linen", TextAlign = "left", Text = info },
                new UiNode(UiKind.Box) { Key = "payout", Text = "Payout: " + value + " cr" });
            row.With(new UiNode(UiKind.StackItem) { Key = "choice:" + pickValue, Grow = 0.5 }.With(button));
        }
        return new UiNode(UiKind.Section) { Key = "pickSection", Title = "Please Select a Bounty:", TextAlign = "center" }.With(row);
    }

    // tg: CivCargoHoldTerminal.tsx:243
    private static UiNode GlobalBountyBlock(TguiContext c)
    {
        UiValue data = c.Data;
        var listBounty = data.Get("listBounty").Items;
        bool sending = data.Truthy("sending");
        bool pad = data.Truthy("pad");
        bool idInserted = data.Truthy("id_inserted");
        string selected = c.UseState<string>(BountyKey, null);
        int bountyTab = c.UseState(BountyTabKey, 0);
        UiValue localBounty = selected == null ? null : listBounty.FirstOrDefault(bounty => bounty.Str("name") == selected);

        var tabs = new UiNode(UiKind.Tabs) { Key = "bountyTabs", Vertical = true, Fluid = true };
        if (listBounty.Count < 1)
        {
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "update",
                Background = "blue",
                Color = "white",
                Bold = true,
                Icon = "refresh",
                Text = "Update List",
                OnClick = () =>
                {
                    c.Act("update_list");
                    c.SetState(BountyTabKey, 0);
                },
            });
        }
        else
        {
            double claimed = data.Num("claimed_bounties");
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "served",
                Color = "#ffffffe5",
                Tooltip = "The total number of global bounties will go up by 1 for every 3 completed!",
                Text = TguiFormat.JsNumber(claimed) + " bount" + (claimed == 1 ? "y" : "ies") + " served" + (claimed > 0 ? "!" : "."),
            });
        }
        tabs.With(new UiNode(UiKind.Tab) { Key = "print", Background = "#ffffff70", Color = "white", Bold = true, Icon = "print", Text = "Printout List", OnClick = () => c.Act("print") });
        for (int i = 0; i < listBounty.Count; i++)
        {
            UiValue bounty = listBounty[i];
            int index = i;
            string name = bounty.Str("name");
            bool priority = bounty.Truthy("priority");
            bool unique = bounty.Truthy("unique");
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "bounty:" + name,
                Background = priority ? "#cec328a8" : unique ? "#00fff270" : "#d1d1d170",
                Color = "white",
                Bold = bountyTab == index,
                Icon = priority ? "star" : unique ? "gem" : "",
                Selected = bountyTab == index,
                Text = name,
                OnClick = () =>
                {
                    c.SetState(BountyKey, name);
                    c.SetState(BountyTabKey, index);
                },
            });
        }

        UiNode detail;
        if (localBounty != null && localBounty.Num("reward") != 0)
        {
            double reward = localBounty.Num("reward");
            detail = new UiNode(UiKind.Section) { Key = "bountyDetail", Title = localBounty.Str("name") }.With(
                new UiNode(UiKind.ProgressBar)
                {
                    Key = "progress",
                    Value = localBounty.Num("shipped"),
                    MaxValue = localBounty.Num("maximum"),
                    Text = localBounty.Str("shipped") + " / " + localBounty.Str("maximum") + " shipped.",
                },
                new UiNode(UiKind.Html) { Key = "description", TextValue = localBounty.Str("description") ?? "" },
                new UiNode(UiKind.Box)
                {
                    Key = "reward",
                    Background = "green",
                    Color = "white",
                    Underline = true,
                    Tooltip = "You'll receive a cut of " + TguiFormat.JsNumber(Math.Round(reward * 0.3, MidpointRounding.AwayFromZero)) + " Credits.",
                    Text = "Reward: " + TguiFormat.JsNumber(reward) + " Credits",
                },
                new UiNode(UiKind.Button)
                {
                    Key = "claim",
                    Width = "100%",
                    Icon = sending ? "times" : "arrow-up",
                    Tooltip = sending ? "Stop Sending" : "Send Goods",
                    Selected = sending,
                    Disabled = !pad || !idInserted,
                    Text = "Send & Claim",
                    OnClick = () =>
                    {
                        c.Act(sending ? "stop" : "send", ("global", true));
                        c.SetState(BountyTabKey, 0);
                    },
                });
        }
        else
        {
            detail = new UiNode(UiKind.NoticeBox) { Key = "select", Width = "100%", Text = "Please select a bounty from the list." };
        }

        return new UiNode(UiKind.Stack) { Key = "station", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "list", Width = "30%" }.With(tabs),
            new UiNode(UiKind.StackItem) { Key = "detail", Grow = 1 }.With(detail));
    }
}

public static class NtosCivCargoHoldTerminal
{
    // tg: NtosCivCargoHoldTerminal.jsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool idInserted = data.Truthy("id_inserted");
        bool hasBounty = data.Truthy("id_bounty_info");
        return NtosWindow.Render(c, 580, 375, null,
            new UiNode(UiKind.NoticeBox) { Key = "notice", Color = idInserted ? "blue" : null, Text = idInserted ? "Welcome valued employee." : "To begin, insert your ID into the console." },
            new UiNode(UiKind.Section)
            {
                Key = "choice",
                Title = "Bounty Choice",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "bounty",
                    Icon = hasBounty ? "recycle" : "pen",
                    Color = hasBounty ? "green" : "default",
                    Tooltip = hasBounty ? "Replace Bounty" : "New Bounty",
                    Disabled = !idInserted,
                    OnClick = () => c.Act("bounty"),
                },
            }.With(new UiNode(UiKind.LabeledList) { Key = "list" }.With(
                new UiNode(UiKind.LabeledItem) { Key = "report", Label = "Cargo Report", Text = data.Str("status_report") })),
            data.Truthy("picking") ? CivCargoHoldTerminal.BountyPickBox(c, htmlInfo: false) : CivCargoHoldTerminal.BountyTextBox(c));
    }
}
