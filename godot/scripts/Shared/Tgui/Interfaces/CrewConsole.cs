using System;
using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CrewConsole.tsx (gaps J5, D298: the crew monitor of the console and the handheld monitor).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CrewConsole
{
    // tg: CrewConsole.tsx:10
    private static readonly string[] HEALTH_COLOR_BY_LEVEL = { "#17d568", "#c4cf2d", "#e67e22", "#ed5100", "#e74c3c", "#801308" };

    // tg: `x === undefined`: the sensor mode left the field out
    private static bool Missing(UiValue s, string key)
    {
        UiValue value = s.Get(key);
        return value == null || value.IsNull;
    }

    private const int STAT_LIVING = 0;
    private const int STAT_DEAD = 4;

    // tg: CrewConsole.tsx:22
    private static bool JobIsHead(double jobId) => jobId % 10 == 0;

    // tg: CrewConsole.tsx:24  SORT_OPTIONS
    private static readonly (string Name, Comparison<UiValue> Sort)[] SORT_OPTIONS =
    {
        ("Job", (a, b) => a.Num("ijob").CompareTo(b.Num("ijob"))),
        ("Name", (a, b) => string.CompareOrdinal(a.Str("name"), b.Str("name"))),
        ("Area", (a, b) =>
        {
            if (Missing(a, "area")) return 1;
            if (Missing(b, "area")) return -1;
            return string.CompareOrdinal(a.Str("area"), b.Str("area"));
        }),
        ("Vitals", (a, b) =>
        {
            if (a.Num("life_status") > b.Num("life_status")) return -1;
            if (a.Num("life_status") < b.Num("life_status")) return 1;
            if (Missing(b, "oxydam")) return -1;
            if (Missing(a, "oxydam")) return 1;
            if (a.Num("health") < b.Num("health")) return -1;
            if (a.Num("health") > b.Num("health")) return 1;
            return 0;
        }),
    };

    // tg: CrewConsole.tsx:64  (colours: tgui constants.ts COLORS.department)
    private static string JobToColor(double jobId)
    {
        if (jobId == 0) return "#c06616";
        if (jobId >= 10 && jobId < 20) return "#e74c3c";
        if (jobId >= 20 && jobId < 30) return "#3498db";
        if (jobId >= 30 && jobId < 40) return "#9b59b6";
        if (jobId >= 40 && jobId < 50) return "#f1c40f";
        if (jobId >= 50 && jobId < 60) return "#f39c12";
        if (jobId >= 60 && jobId < 200) return "#7cc46a";
        if (jobId >= 200 && jobId < 230) return "#00c100";
        return "#c38312";
    }

    // tg: CrewConsole.tsx:92
    private static string StatToIcon(double life_status)
    {
        if (life_status == STAT_LIVING) return "heart";
        if (life_status == STAT_DEAD) return "skull";
        return "heartbeat";
    }

    // tg: CrewConsole.tsx:102
    private static string HealthToAttribute(double oxy, double tox, double burn, double brute)
    {
        double healthSum = oxy + tox + burn + brute;
        int level = (int)Math.Min(Math.Max(Math.Ceiling(healthSum / 25), 0), 5);
        return HEALTH_COLOR_BY_LEVEL[level];
    }

    // tg: CrewConsole.tsx:120  HealthStat (colours: COLORS.damageType)
    private static UiNode HealthStat(string color, double value) =>
        new UiNode(UiKind.Box) { Color = color, TextAlign = "center", Text = TguiFormat.JsNumber(value) };

    // tg: CrewConsole.tsx:131
    public static UiNode Render(TguiContext c) =>
        c.Window(600, 600, "Crew Monitor", new UiNode(UiKind.Section).With(CrewTable(c)));

    // tg: CrewConsole.tsx:163
    private static UiNode CrewTable(TguiContext c)
    {
        bool link_allowed = c.Data.Truthy("link_allowed");
        bool sortAsc = c.UseState("sortAsc", true);
        string searchQuery = c.UseState("searchQuery", "");
        int indexOfSortingOption = c.UseState("indexOfSortingOption", 0);

        // createSearch: a case-insensitive substring of the name
        List<UiValue> sorted = c.Data.Arr("sensors")
            .Where(s => searchQuery.Length == 0 || s.Str("name").Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Comparison<UiValue> sort = SORT_OPTIONS[indexOfSortingOption].Sort;
        sorted = (sortAsc ? sorted.OrderBy(s => s, Comparer<UiValue>.Create(sort)) : sorted.OrderBy(s => s, Comparer<UiValue>.Create((a, b) => sort(b, a)))).ToList();

        var table = new UiNode(UiKind.Table).With(
            new UiNode(UiKind.TableRow) { Key = "head" }.With(
                new[]
                {
                    new UiNode(UiKind.TableCell) { Bold = true, Text = "Name" },
                    new UiNode(UiKind.TableCell) { Bold = true },
                    new UiNode(UiKind.TableCell) { Bold = true, TextAlign = "center", Text = "Vitals" },
                    new UiNode(UiKind.TableCell) { Bold = true, TextAlign = "center", Text = "Position" },
                }.Concat(link_allowed ? new[] { new UiNode(UiKind.TableCell) { Bold = true, TextAlign = "center", Text = "Tracking" } } : Array.Empty<UiNode>())));
        table.With(sorted.Select(s => CrewTableEntry(c, s, link_allowed)));

        return new UiNode(UiKind.Section)
        {
            Buttons = new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "sortby",
                    Text = SORT_OPTIONS[indexOfSortingOption].Name,
                    OnClick = () => c.SetState("indexOfSortingOption", (indexOfSortingOption + 1) % SORT_OPTIONS.Length),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "sortdir",
                    Icon = sortAsc ? "chevron-up" : "chevron-down",
                    OnClick = () => c.SetState("sortAsc", !sortAsc),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
                {
                    Key = "search",
                    Tooltip = "Search for name...",
                    OnInput = value => c.SetState("searchQuery", value),
                })),
        }.With(table);
    }

    // tg: CrewConsole.tsx:234
    private static UiNode CrewTableEntry(TguiContext c, UiValue s, bool link_allowed)
    {
        double ijob = s.Num("ijob");
        string assignment = !Missing(s, "assignment") ? s.Str("assignment") : null;
        double life_status = s.Num("life_status");
        bool hasVitals = !Missing(s, "oxydam");
        string area = !Missing(s, "area") ? s.Str("area") : null;
        string reference = s.Str("ref");

        UiNode status = hasVitals
            ? new UiNode(UiKind.Icon) { Icon = StatToIcon(life_status), Color = HealthToAttribute(s.Num("oxydam"), s.Num("toxdam"), s.Num("burndam"), s.Num("brutedam")) }
            : life_status != STAT_DEAD
                ? new UiNode(UiKind.Icon) { Icon = "heart", Color = "#17d568" }
                : new UiNode(UiKind.Icon) { Icon = "skull", Color = "#801308" };

        UiNode vitals = hasVitals
            ? new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(HealthStat("#3498db", s.Num("oxydam"))),
                new UiNode(UiKind.StackItem) { Text = "/" },
                new UiNode(UiKind.StackItem).With(HealthStat("#2ecc71", s.Num("toxdam"))),
                new UiNode(UiKind.StackItem) { Text = "/" },
                new UiNode(UiKind.StackItem).With(HealthStat("#e67e22", s.Num("burndam"))),
                new UiNode(UiKind.StackItem) { Text = "/" },
                new UiNode(UiKind.StackItem).With(HealthStat("#e74c3c", s.Num("brutedam"))))
            : new UiNode(UiKind.Box) { Text = life_status != STAT_DEAD ? "Alive" : "Dead" };

        var row = new UiNode(UiKind.TableRow) { Key = "row:" + reference }.With(
            new UiNode(UiKind.TableCell)
            {
                Bold = JobIsHead(ijob),
                Color = JobToColor(ijob),
                Text = s.Str("name") + (assignment != null ? " (" + assignment + ")" : ""),
            },
            new UiNode(UiKind.TableCell) { TextAlign = "center" }.With(status),
            new UiNode(UiKind.TableCell) { TextAlign = "center" }.With(vitals),
            area != "~" && area != null
                ? new UiNode(UiKind.TableCell) { Text = area }
                : new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Icon) { Icon = "question", Color = "#ffffff" }));
        if (link_allowed)
        {
            row.With(new UiNode(UiKind.TableCell) .With(new UiNode(UiKind.Button)
            {
                Key = "track:" + reference,
                Text = "Track",
                OnClick = () => c.Act("select_person", ("ref", reference)),
            }));
        }
        return row;
    }
}
