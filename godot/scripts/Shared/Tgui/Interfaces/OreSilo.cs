using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/OreSilo.tsx (Lavaland L2a, D304).
// The window reads tg's ui_data and ui_static_data of the ore silo (machine_silo.dm:239, :242) and sends hold, remove, eject,
// toggle_ban and toggle_restrict. A machine's picture is its icon file and state (DV257; tg sends a base64 picture); the logs'
// fuzzy search is a case-insensitive substring of each entry's words, and the list draws every entry (tg's VirtualList).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class OreSilo
{
    private const string TabKey = "currentTab", QueryKey = "query", ExpandedKey = "expanded:";
    private const int Machines = 0, Logs = 1;

    // tg: OreSilo.tsx:73
    private static readonly Dictionary<string, string> ActionToColor = new(StringComparer.Ordinal)
    {
        ["DEPOSITED"] = "green",
        ["WITHDRAWN"] = "red",
        ["PROCESSED"] = "blue",
        ["RESTOCKED"] = "purple",
    };

    // tg: OreSilo.tsx:80
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int currentTab = c.UseState(TabKey, Logs);
        UiNode Tab(int tab, string icon, string text) => new UiNode(UiKind.Tab)
        {
            Key = "tab" + tab,
            Icon = icon,
            Selected = currentTab == tab,
            Text = text,
            OnClick = () => c.SetState(TabKey, tab),
        };
        // tg: OreSilo.tsx:87  <Window title="Ore Silo" width={620} height={600}>
        return c.Window(620, 600, "Ore Silo",
            new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "tabs" }.With(
                    new UiNode(UiKind.Tabs) { Fluid = true }.With(Tab(Machines, "plug", "Connections"), Tab(Logs, "book-bookmark", "Logs"))),
                new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(
                    currentTab == Machines ? MachineList(c, data.Arr("machines")) : LogsList(c, data.Arr("logs"))),
                new UiNode(UiKind.StackItem) { Key = "bar" }.With(
                    new UiNode(UiKind.Section) { Key = "materials", Fill = true }.With(
                        MaterialAccessBar.Render(data.Arr("materials"), data.Num("SHEET_MATERIAL_AMOUNT"),
                            (@ref, amount) => c.Act("eject", ("ref", @ref), ("amount", amount)))))));
    }

    // tg: OreSilo.tsx:137
    private static UiNode MachineList(TguiContext c, IReadOnlyList<UiValue> machines)
    {
        if (machines.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No machines connected!" };
        var list = new UiNode(UiKind.Section) { Key = "machines", Fill = true, Scrollable = true };
        for (int index = 0; index < machines.Count; index++)
        {
            int id = index + 1; // tg's lists count from 1
            list.With(MachineDisplay(machines[index], id, () => c.Act("hold", ("id", id)), () => c.Act("remove", ("id", id))));
        }
        return list;
    }

    // tg: OreSilo.tsx:164
    private static UiNode MachineDisplay(UiValue machine, int id, Action onPause, Action onRemove)
    {
        string name = machine.Str("name");
        string machineName = name;
        int index = machineName.IndexOf('('); // some techfabs have their location attached to their name
        if (index >= 0)
            machineName = machineName[..index];
        machineName = machineName.TrimEnd() + " (" + machine.Str("location") + ")";
        bool onHold = machine.Truthy("on_hold");
        return new UiNode(UiKind.Stack) { Key = "machine:" + id, Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "icon" }.With(
                new UiNode(UiKind.Box) { Key = "pic", DmIcon = machine.Str("icon"), DmIconState = machine.Str("icon_state"), Opacity = onHold ? 0.5 : 1 }),
            new UiNode(UiKind.StackItem) { Key = "label", Grow = 1 }.With(
                new UiNode(UiKind.Box) { Key = "name", Color = onHold ? "label" : null, Text = machineName }),
            new UiNode(UiKind.StackItem) { Key = "pause" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "pause",
                    Icon = onHold ? "circle-play" : "circle-pause",
                    Tooltip = onHold ? $"Resume {name} usage." : $"Put {name} on hold.",
                    OnClick = onPause,
                }),
            new UiNode(UiKind.StackItem) { Key = "remove" }.With(
                new UiNode(UiKind.Button) { Key = "remove", Icon = "trash-can", Tooltip = $"Disconnect {name}.", OnClick = onRemove }));
    }

    // tg: OreSilo.tsx:235
    private static UiNode RestrictButton(TguiContext c)
    {
        bool idRequired = c.Data.Truthy("ID_required");
        return new UiNode(UiKind.Button)
        {
            Key = "restrict",
            Color = idRequired ? "bad" : "good",
            Text = idRequired ? "Disable ID Requirement" : "Enable ID Requirement",
            OnClick = () => c.Act("toggle_restrict"),
        };
    }

    // tg: OreSilo.tsx:256
    private static UiNode LogsList(TguiContext c, IReadOnlyList<UiValue> logs)
    {
        string query = c.UseState(QueryKey, "");
        var shown = new List<(int Index, UiValue Log)>();
        for (int i = 0; i < logs.Count; i++)
        {
            UiValue log = logs[i];
            UiValue user = log.Get("user_data");
            string searchString = string.Join(" ", log.Str("action"), user.Str("name"), user.Str("assignment"), log.Str("raw_materials"),
                log.Str("machine_name"), log.Str("area_name"), log.Str("noun")).ToLowerInvariant();
            if (query.Length == 0 || query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).All(searchString.Contains))
                shown.Add((i, log));
        }

        var entries = new UiNode(UiKind.Section) { Key = "entries", Fill = true, Scrollable = shown.Count > 0 };
        if (shown.Count > 0)
            foreach ((int i, UiValue log) in shown)
                entries.With(LogEntry(c, i, log));
        else
            entries.With(new UiNode(UiKind.NoticeBox) { Key = "empty", TextAlign = "center", Text = query.Length > 0 ? "No logs seem to match your request." : "Nothing here..." });

        return new UiNode(UiKind.Stack) { Key = "logs", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "head" }.With(
                new UiNode(UiKind.Section) { Key = "search", Title = "Action Logs", Buttons = RestrictButton(c) }.With(
                    new UiNode(UiKind.Stack) { Key = "row" }.With(
                        new UiNode(UiKind.StackItem) { Key = "input", Grow = 1 }.With(
                            new UiNode(UiKind.Input)
                            {
                                Key = "query",
                                Fluid = true,
                                AutoFocus = true,
                                TextValue = query,
                                Tooltip = "Search for names, locations and resources...",
                                OnInput = value => c.SetState(QueryKey, value),
                            }),
                        new UiNode(UiKind.StackItem) { Key = "clear" }.With(
                            new UiNode(UiKind.Button) { Key = "clear", Icon = "close", Disabled = query.Length == 0, OnClick = () => c.SetState(QueryKey, "") })))),
            new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(entries));
    }

    // tg: OreSilo.tsx:323
    private static UiNode UserItem(TguiContext c, UiValue user)
    {
        IReadOnlyList<UiValue> bannedUsers = c.Data.Arr("banned_users");
        double accountId = user.Num("account_id");
        bool banned = bannedUsers.Any(b => b.Kind == UiValueKind.Number && b.AsNum(double.NaN) == accountId);
        string name = user.Str("name", "NAME_RES_FAIL");
        var row = new UiNode(UiKind.Stack) { Key = "user", Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "name", Text = name + "," },
            new UiNode(UiKind.StackItem) { Key = "assignment", Text = user.Str("assignment", "ASSGN-RES_FAIL") });
        // tg's UserData names id_read_failure and silicon_override; ID_DATA keys them by tg's defines' text
        if (!user.Truthy("id_read_failure") && !user.Truthy("silicon_override"))
            row.With(new UiNode(UiKind.StackItem) { Key = "ban" }.With(new UiNode(UiKind.Button)
            {
                Key = "ban",
                Color = banned ? "bad" : "good",
                Text = (banned ? "Unrestrict" : "Restrict") + " access",
                OnClick = () => c.Act("toggle_ban", ("user_data", user)),
            }));
        return row;
    }

    // tg: OreSilo.tsx:364
    private static string FormatAmount(string action, double amount)
    {
        bool isSheetAction = action == "WITHDRAWN" || action == "DEPOSITED";
        double rawAmount = Math.Abs(amount);
        if (!isSheetAction)
            return TguiFormat.JsNumber(rawAmount);
        double proportionalAmount = rawAmount / 100;
        return " " + TguiFormat.JsNumber(proportionalAmount) + " ";
    }

    // tg: OreSilo.tsx:374
    private static UiNode LogEntry(TguiContext c, int index, UiValue log)
    {
        string key = ExpandedKey + index;
        bool expanded = c.UseState(key, false);
        string action = log.Str("action").ToUpperInvariant();
        double amount = log.Num("amount");
        UiValue user = log.Get("user_data");
        var entry = new UiNode(UiKind.Box) { Key = "log:" + index }.With(
            new UiNode(UiKind.Stack) { Key = "line", Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "expand" }.With(
                    new UiNode(UiKind.Button) { Key = "expand", Icon = expanded ? "arrow-down" : "arrow-right", Color = "transparent", OnClick = () => c.SetState(key, !expanded) }),
                new UiNode(UiKind.StackItem) { Key = "action" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "action",
                        Color = ActionToColor.GetValueOrDefault(action),
                        Width = 8.0,
                        TextAlign = "center",
                        Text = action,
                        OnClick = () => c.SetState(key, !expanded),
                    }),
                new UiNode(UiKind.StackItem) { Key = "arrow" }.With(new UiNode(UiKind.Icon) { Key = "arrow", Icon = "arrow-right" }),
                new UiNode(UiKind.StackItem) { Key = "amount", Grow = 1, Text = " " + FormatAmount(action, amount) + " " + log.Str("noun") },
                new UiNode(UiKind.StackItem) { Key = "who" }.With(
                    new UiNode(UiKind.Button) { Key = "who", Icon = "user", Color = "gray", Text = user.Str("name") + " (" + user.Str("assignment") + ")" })));
        if (expanded)
        {
            UiNode Row(string rowKey, string label, UiNode value) => new UiNode(UiKind.TableRow) { Key = rowKey, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "label", Text = label }, value);
            entry.With(new UiNode(UiKind.Table) { Key = "details" }.With(
                Row("time", "Time", new UiNode(UiKind.TableCell) { Key = "value", Text = log.Str("time") }),
                Row("machine", "Machine", new UiNode(UiKind.TableCell) { Key = "value", Text = TguiFormat.Capitalize(log.Str("machine_name")) }),
                Row("location", "Location", new UiNode(UiKind.TableCell) { Key = "value", Text = log.Str("area_name") }),
                Row("materials", "Materials", new UiNode(UiKind.TableCell) { Key = "value", Color = amount > 0 ? "good" : "bad", Text = log.Str("raw_materials") }),
                Row("user", "User", new UiNode(UiKind.TableCell) { Key = "value" }.With(UserItem(c, user)))));
        }
        return entry;
    }
}
