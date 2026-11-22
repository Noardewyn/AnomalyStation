using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DynamicAdmin.tsx (syndicate package A3, D316): the Dynamic
// panel, from /datum/dynamic_panel's ui_data (dynamic_admin.dm:29): the status (the tier, the ruleset counts, the start times, the
// cooldowns and chances), the rulesets (queued, active, every tg ruleset with its queue or execute and disable buttons, the search) and the
// config. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class DynamicAdmin
{
    // tg: DynamicAdmin.tsx:75 formatTime: deciseconds as "Hh Mm Ss", or "never" when negative
    internal static string FormatTime(double seconds)
    {
        seconds /= 10;
        if (seconds < 0)
            return "never";
        double hours = Math.Floor(seconds / 3600);
        double minutes = Math.Floor((seconds % 3600) / 60);
        double secs = Math.Round(seconds % 60, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{hours}h {minutes}m {secs}s");
    }

    // tg: DynamicAdmin.tsx:88 getPlayerString
    internal static string PlayerString(IReadOnlyList<UiValue> players)
    {
        if (players.Count == 0)
            return "No one";
        if (players.Count == 1)
            return players[0].Str("key");
        if (players.Count == 2)
            return players[0].Str("key") + " and " + players[1].Str("key");
        string playerString = "";
        for (int i = 0; i < players.Count; i++)
        {
            playerString += players[i].Str("key");
            if (i < players.Count - 1)
                playerString += ", ";
            if (i == players.Count - 2)
                playerString += "and ";
        }
        return playerString;
    }

    // tg: DynamicAdmin.tsx:108 readableRulesesetCategory: underlines to spaces, every word capitalized
    internal static string ReadableCategory(string category)
        => string.Join(" ", category.Replace('_', ' ').Split(' ').Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));

    private static UiNode Item(string label, params UiNode[] content) => new UiNode(UiKind.LabeledItem) { Key = label, Label = label }.With(content);

    private static UiNode Flex(string key, params UiNode[] items)
        => new UiNode(UiKind.Stack) { Key = key }.With(items.Where(i => i != null).Select((i, n) => new UiNode(UiKind.StackItem) { Key = key + ":" + n }.With(i)));

    // tg: DynamicAdmin.tsx:117 StatusPanel
    private static UiNode StatusPanel(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue currentTier = data.Get("current_tier");
        bool roundstarted = data.Truthy("roundstarted");
        if (currentTier == null || currentTier.Kind == UiValueKind.Null)
        {
            return new UiNode(UiKind.LabeledList) { Key = "status" }.With(Item("Current Tier",
                new UiNode(UiKind.Button) { Key = "set_tier", Text = "(Click to set)", OnClick = () => c.Act("set_tier") }));
        }

        var list = new UiNode(UiKind.LabeledList) { Key = "status" };
        list.With(Item("Current Tier",
            Flex("tier",
                new UiNode(UiKind.Box) { Key = "tier", Text = currentTier.Num("number").ToString(CultureInfo.InvariantCulture) + " (" + currentTier.Str("name") + ")" },
                roundstarted ? null : new UiNode(UiKind.Button) { Key = "change_tier", Text = "(Change)", OnClick = () => c.Act("set_tier") })));
        foreach (KeyValuePair<string, UiValue> entry in data.Obj("ruleset_count"))
        {
            string name = entry.Key;
            double count = entry.Value.AsNum();
            bool editable = name != "roundstart" || !roundstarted;
            list.With(Item(ReadableCategory(name) + " Ruleset Count", Flex("count:" + name,
                new UiNode(UiKind.Box) { Key = "count", Text = count.ToString(CultureInfo.InvariantCulture) },
                editable ? new UiNode(UiKind.Button)
                {
                    Key = "add:" + name, Icon = "plus", Tooltip = "Add one max ruleset of this type",
                    OnClick = () => c.Act("add_ruleset_category_count", ("ruleset_category", name)),
                } : null,
                editable ? new UiNode(UiKind.Button)
                {
                    Key = "zero:" + name, Icon = "times", Disabled = count == 0, Tooltip = "Set max ruleset of this type to 0",
                    OnClick = () => c.Act("set_ruleset_category_count", ("ruleset_category", name), ("ruleset_count", 0)),
                } : null)));
        }
        MidroundRows(c, list, "Light", "lights", "light_start_now", "light_midround_chance", "max_light_chance", "light_chance_maxxed");
        MidroundRows(c, list, "Heavy", "heavies", "heavy_start_now", "heavy_midround_chance", "max_heavy_chance", "heavy_chance_maxxed");

        double untilLatejoins = data.Num("time_until_latejoins");
        if (untilLatejoins > 0)
        {
            list.With(Item("Latejoin Start", Flex("latejoin_start",
                new UiNode(UiKind.Box) { Key = "time", Text = FormatTime(untilLatejoins) },
                new UiNode(UiKind.Button) { Key = "latejoin_start_now", Text = "Start Now", OnClick = () => c.Act("latejoin_start_now") })));
        }
        else
        {
            double nextLatejoin = data.Num("time_until_next_latejoin");
            list.With(Item("Latejoin Cooldown", Flex("latejoin_cooldown",
                new UiNode(UiKind.Box) { Key = "time", Text = nextLatejoin != 0 ? FormatTime(nextLatejoin) : "Next latejoin" },
                new UiNode(UiKind.Button) { Key = "reset_latejoin_cooldown", Text = "Reset Cooldown", Disabled = nextLatejoin <= 0, OnClick = () => c.Act("reset_latejoin_cooldown") })));
            list.With(Item("Latejoin Chance", Flex("latejoin_chance",
                new UiNode(UiKind.Box)
                {
                    Key = "chance", Underline = true,
                    Text = Num(data.Num("latejoin_chance")) + "% (" + Num(data.Num("failed_latejoins")) + " failed attempts)",
                    Tooltip = "Chance the next person who joins the game will selected for a latejoin ruleset. Note this does not GUARANTEE a latejoin ruleset is ran - if it fails, the chance will increase for the next player who joins.",
                },
                new UiNode(UiKind.Button) { Key = "max_latejoin_chance", Text = data.Truthy("latejoin_chance_maxxed") ? "Reset" : "Set to 100%", OnClick = () => c.Act("max_latejoin_chance") })));
        }
        return list;
    }

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    // tg: DynamicAdmin.tsx:207-308 the light and heavy midround rows (the same JSX twice)
    private static void MidroundRows(TguiContext c, UiNode list, string kind, string until, string startNow, string chanceKey, string maxAction, string maxxed)
    {
        UiValue data = c.Data;
        double untilStart = data.Num("time_until_" + until);
        if (untilStart > 0)
        {
            list.With(Item(kind + " Midround Start", Flex(until + "_start",
                new UiNode(UiKind.Box) { Key = "time", Text = FormatTime(untilStart) },
                new UiNode(UiKind.Button) { Key = startNow, Text = "Start Now", OnClick = () => c.Act(startNow) })));
            return;
        }
        double nextMidround = data.Num("time_until_next_midround");
        list.With(Item(kind + " Midround Cooldown", Flex(until + "_cooldown",
            new UiNode(UiKind.Box) { Key = "time", Text = nextMidround > 0 ? FormatTime(nextMidround) : "Next dynamic tick (" + FormatTime(data.Num("next_dynamic_tick")) + ")" },
            new UiNode(UiKind.Button) { Key = "reset_midround_cooldown", Text = "Reset Cooldown", Disabled = nextMidround <= 0, OnClick = () => c.Act("reset_midround_cooldown") })));
        list.With(Item(kind + " Midround Chance", Flex(until + "_chance",
            new UiNode(UiKind.Box)
            {
                Key = "chance", Underline = true, Text = Num(data.Num(chanceKey)) + "%",
                Tooltip = "Chance of a " + kind.ToLowerInvariant() + " midround ruleset being selected on next dynamic tick",
            },
            new UiNode(UiKind.Button) { Key = maxAction, Text = data.Truthy(maxxed) ? "Reset" : "Set to 100%", OnClick = () => c.Act(maxAction) })));
    }

    // tg: DynamicAdmin.tsx:400 ConfigPanel: reports the entire config
    private static UiNode ConfigPanel(TguiContext c)
    {
        IReadOnlyList<KeyValuePair<string, UiValue>> config = c.Data.Obj("full_config");
        if (config.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "no_config", Text = "No config loaded - refer to repo defaults for reference." };
        string shown = c.UseState("shownConfig", config[0].Key);
        UiValue shownConfig = config.FirstOrDefault(e => e.Key == shown).Value ?? config[0].Value;
        var entries = new UiNode(UiKind.LabeledList) { Key = "entries" };
        foreach (KeyValuePair<string, UiValue> entry in shownConfig.Fields)
            entries.With(Item(entry.Key, new UiNode(UiKind.Box) { Key = "value", Wrap = true, Text = entry.Value.ToJson() }));
        return new UiNode(UiKind.Stack) { Key = "config", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "notice" }.With(new UiNode(UiKind.NoticeBox)
            {
                Text = "This is the current config read by the dynamic system when running rulesets. If you want to edit these values temporarily, you can do so via View Variables. (Note: Editing tier configs has no effect after roundstart)",
            }),
            new UiNode(UiKind.StackItem) { Key = "pick" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "shown", Options = config.Select(e => e.Key).ToArray(), TextValue = shown, OnSelected = key => c.SetState("shownConfig", key),
            }),
            new UiNode(UiKind.StackItem) { Key = "values", Height = "180px" }.With(new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(entries)));
    }

    // tg: DynamicAdmin.tsx:460 RulesetsPanel: the queued and active rulesets and every ruleset to queue or execute
    private static UiNode RulesetsPanel(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<KeyValuePair<string, UiValue>> allRulesets = data.Obj("all_rulesets");
        bool roundstarted = data.Truthy("roundstarted");
        bool anyDisabled = allRulesets.Any(e => e.Value.Items.Any(r => r.Truthy("admin_disabled")));
        bool allDisabled = allRulesets.All(e => e.Value.Items.All(r => r.Truthy("admin_disabled")));
        string searchText = c.UseState("searchText", "");
        Func<UiValue, bool> searchFilter = TguiFormat.CreateSearch<UiValue>(searchText, ruleset => ruleset.Str("name"));

        IReadOnlyList<UiValue> queued = data.Arr("queued_rulesets");
        var queuedStack = new UiNode(UiKind.Stack) { Key = "queued", Vertical = true };
        if (queued.Count == 0)
            queuedStack.With(new UiNode(UiKind.StackItem) { Key = "none", Grow = 1 }.With(new UiNode(UiKind.NoticeBox) { TextAlign = "center", Text = "No rulesets queued." }));
        foreach (UiValue ruleset in queued)
        {
            double index = ruleset.Num("index");
            queuedStack.With(new UiNode(UiKind.StackItem) { Key = "queued:" + Num(index) }.With(Flex("row",
                new UiNode(UiKind.Button) { Key = "remove", Icon = "times", Tooltip = "Remove from queue", OnClick = () => c.Act("remove_queued_ruleset", ("ruleset_index", index)) },
                new UiNode(UiKind.Box) { Key = "name", Text = ruleset.Str("name") + " (" + ruleset.Str("id") + ")" })));
        }

        IReadOnlyList<UiValue> active = data.Arr("active_rulesets");
        var activeStack = new UiNode(UiKind.Stack) { Key = "active", Vertical = true };
        if (active.Count == 0)
            activeStack.With(new UiNode(UiKind.StackItem) { Key = "none", Grow = 1 }.With(new UiNode(UiKind.NoticeBox) { TextAlign = "center", Text = "No rulesets active." }));
        foreach (UiValue ruleset in active)
        {
            double index = ruleset.Num("index");
            bool hidden = ruleset.Truthy("hidden");
            activeStack.With(new UiNode(UiKind.StackItem) { Key = "active:" + Num(index) }.With(
                Flex("row",
                    // tg's line-through for a hidden ruleset is drawn as a dimmed name (the kit has no strike-through)
                    new UiNode(UiKind.Box) { Key = "name", Text = ruleset.Str("name") + " (" + ruleset.Str("id") + ")", Opacity = hidden ? 0.5 : null },
                    new UiNode(UiKind.Button)
                    {
                        Key = "hide", Checkbox = hidden, Icon = "times", Tooltip = "If checked, this ruleset will not show in the roundend report.",
                        OnClick = () => c.Act("hide_ruleset", ("ruleset_index", index)),
                    }),
                new UiNode(UiKind.Box) { Key = "selected", Text = "Selected: " + PlayerString(ruleset.Arr("selected_players")) }));
        }

        var columns = new UiNode(UiKind.Stack) { Key = "columns" };
        foreach (KeyValuePair<string, UiValue> entry in allRulesets)
        {
            string category = entry.Key;
            bool queueable = category is "roundstart" or "latejoin";
            bool locked = category == "roundstart" && roundstarted;
            var column = new UiNode(UiKind.Stack) { Key = "list", Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "title", Align = "center" }.With(new UiNode(UiKind.Box) { Bold = true, Text = ReadableCategory(category) }));
            foreach (UiValue ruleset in entry.Value.Items.Where(searchFilter).OrderBy(r => r.Str("name"), StringComparer.Ordinal))
            {
                string typepath = ruleset.Str("typepath");
                bool disabled = ruleset.Truthy("admin_disabled");
                column.With(new UiNode(UiKind.StackItem) { Key = "ruleset:" + ruleset.Str("id") }.With(Flex("row",
                    queueable
                        ? new UiNode(UiKind.Button)
                        {
                            Key = "queue", Icon = "plus", Tooltip = locked ? "Round already started!" : "Add to queue", Disabled = locked,
                            OnClick = () => c.Act("add_queued_ruleset", ("ruleset_type", typepath)),
                        }
                        : new UiNode(UiKind.Button)
                        {
                            Key = "execute", Icon = "play", Tooltip = "Execute this ruleset",
                            OnClick = () => c.Act("execute_ruleset", ("ruleset_type", typepath)),
                        },
                    locked ? null : new UiNode(UiKind.Button)
                    {
                        Key = "disable", Checkbox = disabled, Color = disabled ? "bad" : "grey", Tooltip = "If checked, this ruleset will never run randomly.",
                        OnClick = () => c.Act("disable_ruleset", ("ruleset_type", typepath)),
                    },
                    new UiNode(UiKind.Box) { Key = "name", Underline = true, Text = ruleset.Str("name"), Tooltip = "ID: " + ruleset.Str("id") })));
            }
            columns.With(new UiNode(UiKind.StackItem) { Key = "category:" + category, Grow = 1 }.With(column));
        }

        return new UiNode(UiKind.Stack) { Key = "rulesets", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "queued" }.With(new UiNode(UiKind.Section) { Title = "Queued Rulesets" }.With(queuedStack)),
            new UiNode(UiKind.Divider) { Key = "divider1" },
            new UiNode(UiKind.StackItem) { Key = "active" }.With(new UiNode(UiKind.Section) { Title = "Active Rulesets" }.With(activeStack)),
            new UiNode(UiKind.Divider) { Key = "divider2" },
            new UiNode(UiKind.StackItem) { Key = "available", Height = "330px" }.With(new UiNode(UiKind.Section)
            {
                Title = "Available Rulesets", Fill = true, Scrollable = true,
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Input) { Key = "search", Tooltip = "Search for ruleset...", TextValue = searchText, OnInput = value => c.SetState("searchText", value) },
                    new UiNode(UiKind.Button) { Key = "disable_all", Text = "Disable All", Disabled = allDisabled, OnClick = () => c.Act("disable_all") },
                    new UiNode(UiKind.Button) { Key = "enable_all", Text = "Enable All", Disabled = !anyDisabled, OnClick = () => c.Act("enable_all") }),
            }.With(columns)));
    }

    // tg: DynamicAdmin.tsx:665 DynamicAdmin: the Status, Rulesets and Config tabs (Config only with a loaded config)
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string[] tabs = new[] { "Status", "Rulesets", "Config" }.Where(tab => tab != "Config" || data.Truthy("config_even_enabled")).ToArray();
        string currentTab = c.UseState("tab", tabs[0]);
        UiNode shown = currentTab switch
        {
            "Config" => ConfigPanel(c),
            "Rulesets" => RulesetsPanel(c),
            _ => StatusPanel(c),
        };
        bool wide = currentTab == "Rulesets";
        return c.Window(wide ? 800 : 500, wide ? 600 : 400, "Dynamic Admin Panel",
            new UiNode(UiKind.Section)
            {
                Key = "panel", Title = " ", Height = "100%", Width = "100%",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "antag_events", Text = "Antag Events", Checkbox = data.Truthy("antag_events_enabled"),
                        Tooltip = "If checked, random events that spawn antags or dynamic rulesets can trigger.", OnClick = () => c.Act("toggle_antag_events"),
                    },
                    new UiNode(UiKind.Button) { Key = "vv", Text = "VV", Tooltip = "Opens the Dynamic subsystem VV panel.", OnClick = () => c.Act("dynamic_vv") }),
            }.With(
                new UiNode(UiKind.Tabs) { Key = "tabs" }.With(tabs.Select(tab => new UiNode(UiKind.Tab)
                {
                    Key = "tab:" + tab, Text = tab, Selected = currentTab == tab, OnClick = () => c.SetState("tab", tab),
                })),
                shown));
    }
}
