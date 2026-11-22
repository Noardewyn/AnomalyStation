using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DynamicTester.tsx (syndicate package A3, D316): the Dynamic
// weight tester, from /datum/dynamic_tester's ui_static_data (dynamic_testing.dm:57): the tier and the number of players, and each
// ruleset's weight, odds and antagonist caps by tab. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class DynamicTester
{
    // tg: DynamicTester.tsx:31 TABS
    private static readonly (string Key, string Report)[] Tabs =
    {
        ("Roundstart", "roundstart_ruleset_report"), ("LightMidrounds", "light_midround_ruleset_report"), ("HeavyMidrounds", "heavy_midround_ruleset_report"),
    };

    // tg: DynamicTester.tsx:37
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string tab = c.UseState("tab", Tabs[0].Key);
        IReadOnlyList<UiValue> report = data.Arr(Tabs.First(t => t.Key == tab).Report);

        double totalWeight = report.Sum(r => r.Num("weight"));
        var table = new UiNode(UiKind.Table) { Key = "report" }.With(
            new UiNode(UiKind.TableRow) { Key = "header", Bold = true }.With(
                new UiNode(UiKind.TableCell) { Key = "h:name", Text = "Ruleset" },
                new UiNode(UiKind.TableCell) { Key = "h:weight", Text = "Weight" },
                new UiNode(UiKind.TableCell) { Key = "h:odds", Text = "Odds" },
                new UiNode(UiKind.TableCell) { Key = "h:max", Text = "Max Antags" },
                new UiNode(UiKind.TableCell) { Key = "h:min", Text = "Min Antags" }));
        foreach (UiValue r in report.OrderBy(r => r.Str("name"), StringComparer.Ordinal))
        {
            double weight = r.Num("weight");
            // Math.round(weight / total * 100): JavaScript rounds halves up
            double percentage = totalWeight == 0 ? 0 : Math.Floor(weight / totalWeight * 100 + 0.5);
            string comment = r.Str("comment");
            table.With(new UiNode(UiKind.TableRow) { Key = r.Str("name") + "-" + tab }.With(
                new UiNode(UiKind.TableCell) { Key = "name", Text = r.Str("name"), Tooltip = comment.Length > 0 ? comment : null, Underline = comment.Length > 0 },
                new UiNode(UiKind.TableCell) { Key = "weight", Text = Num(weight) },
                new UiNode(UiKind.TableCell) { Key = "odds", Text = Num(percentage) + "%" },
                new UiNode(UiKind.TableCell) { Key = "max", Text = Num(r.Num("max_candidates")) },
                new UiNode(UiKind.TableCell) { Key = "min", Text = Num(r.Num("min_candidates")) }));
        }

        return c.Window(500, 400, "Dynamic Weight Tester",
            new UiNode(UiKind.Section) { Key = "tester", Scrollable = true, Height = "100%", Width = "100%" }.With(
                new UiNode(UiKind.Stack) { Key = "stack", Vertical = true, Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "tier" }.With(new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem) { Key = "label" }.With(new UiNode(UiKind.Box) { Text = "Tier:" }),
                        new UiNode(UiKind.StackItem) { Key = "input" }.With(new UiNode(UiKind.NumberInput)
                        {
                            Key = "tier", Value = data.Num("tier"), MinValue = 1, MaxValue = 4, Step = 1,
                            OnChange = v => c.Act("set_tier", ("tier", v)),
                        }))),
                    new UiNode(UiKind.StackItem) { Key = "players" }.With(new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem) { Key = "label" }.With(new UiNode(UiKind.Box) { Text = "Number of players:" }),
                        new UiNode(UiKind.StackItem) { Key = "input" }.With(new UiNode(UiKind.NumberInput)
                        {
                            Key = "num_players", Value = data.Num("num_players"), MinValue = 1, MaxValue = 200, Step = 1,
                            OnChange = v => c.Act("set_num_players", ("num_players", v)),
                        }))),
                    new UiNode(UiKind.Divider) { Key = "divider" },
                    new UiNode(UiKind.StackItem) { Key = "tabs" }.With(new UiNode(UiKind.Tabs).With(Tabs.Select(t => new UiNode(UiKind.Tab)
                    {
                        Key = "tab:" + t.Key, Text = t.Key, Selected = tab == t.Key, OnClick = () => c.SetState("tab", t.Key),
                    }))),
                    new UiNode(UiKind.StackItem) { Key = "table" }.With(table))));
    }

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);
}
