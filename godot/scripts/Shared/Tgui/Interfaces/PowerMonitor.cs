using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PowerMonitor.tsx (M7 power monitor, D162).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PowerMonitor
{
    // tg: PowerMonitor.tsx:69
    private const double PeakDraw = 500000;

    private const string SortKey = "sortByField";

    // tg: PowerMonitor.tsx:34
    public static double PowerRank(string text)
    {
        string[] parts = text.Split(' ');
        string unit = (parts.Length > 1 ? parts[1] : "undefined").ToLowerInvariant();
        return Array.IndexOf(new[] { "w", "kw", "mw", "gw" }, unit);
    }

    // JavaScript's parseFloat: the leading number of the text, NaN when there is none
    public static double ParseFloat(string text)
    {
        text = text.TrimStart();
        int end = 0;
        while (end < text.Length && (char.IsAsciiDigit(text[end]) || text[end] is '.' or '-' or '+' or 'e' or 'E'))
            end++;
        for (; end > 0; end--)
        {
            if (double.TryParse(text[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return value;
        }
        return double.NaN;
    }

    private sealed record Area(string Name, double Charge, double Charging, double Eqp, double Lgt, double Env, string Load);

    // tg: PowerMonitor.tsx:40  Oh no not another sorting algorithm
    private static double PowerSort(Area a, Area b)
    {
        double sortedByRank = PowerRank(a.Load) - PowerRank(b.Load);
        double sortedByLoad = ParseFloat(a.Load) - ParseFloat(b.Load);
        if (sortedByRank != 0)
            return sortedByRank;
        return sortedByLoad;
    }

    // tg: PowerMonitor.tsx:51  (JavaScript compares strings by UTF-16 code units)
    private static int NameSort(Area a, Area b) => string.CompareOrdinal(a.Name, b.Name);

    // tg: PowerMonitor.tsx:62
    public static UiNode Render(TguiContext c)
    {
        UiNode content = Content(c);
        return c.Window(550, 700, null, content);
    }

    // tg: PowerMonitor.tsx:73  (PowerMonitorContent, which NtosPowerMonitor shares)
    public static UiNode Content(TguiContext c)
    {
        UiValue history = c.Data.Get("history");
        if (history == null || history.Kind != UiValueKind.Object)
            return new UiNode(UiKind.Box) { Key = "loading", Text = "Loading..." };

        List<double> supplyHistory = history.Arr("supply").Select(v => v.AsNum()).ToList();
        List<double> demandHistory = history.Arr("demand").Select(v => v.AsNum()).ToList();
        double supply = supplyHistory.Count > 0 && supplyHistory[^1] != 0 ? supplyHistory[^1] : 0;
        double demand = demandHistory.Count > 0 && demandHistory[^1] != 0 ? demandHistory[^1] : 0;

        // tg: PowerMonitor.tsx:86  Math.max(PEAK_DRAW, ...history.supply, ...history.demand)
        double maxValue = Math.Max(PeakDraw, Math.Max(supplyHistory.Count > 0 ? supplyHistory.Max() : double.NegativeInfinity, demandHistory.Count > 0 ? demandHistory.Max() : double.NegativeInfinity));

        if (supplyHistory.Count == 0 || demandHistory.Count == 0)
            return LoadingScreen();

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(
                // tg: PowerMonitor.tsx:94  <Flex mx={-0.5}>
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Width = "200px" }.With(
                        new UiNode(UiKind.Section).With(
                            new UiNode(UiKind.LabeledList).With(
                                new UiNode(UiKind.LabeledItem) { Label = "Supply" }.With(
                                    new UiNode(UiKind.ProgressBar) { Key = "supply", Value = supply, MinValue = 0, MaxValue = maxValue, Color = "teal", Text = $"{TguiFormat.ToFixed(supply / 1000)} kW" }),
                                new UiNode(UiKind.LabeledItem) { Label = "Draw" }.With(
                                    new UiNode(UiKind.ProgressBar) { Key = "draw", Value = demand, MinValue = 0, MaxValue = maxValue, Color = "pink", Text = $"{TguiFormat.ToFixed(demand / 1000)} kW" })))),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                        new UiNode(UiKind.Section) { Fill = true }.With(
                            new UiNode(UiKind.Chart) { Key = "chart", Plain = true, Height = "48px" }.With(
                                Line(supplyHistory, maxValue, "rgba(0, 181, 173, 1)", "rgba(0, 181, 173, 0.25)", "supplyLine"),
                                Line(demandHistory, maxValue, "rgba(224, 57, 151, 1)", "rgba(224, 57, 151, 0.25)", "demandLine")))))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(StationAreas(c)));
    }

    // tg: PowerMonitor.tsx:112  <Chart.Line fillPositionedParent data={...} rangeX={[0, data.length - 1]} rangeY={[0, maxValue]} strokeColor fillColor />
    private static UiNode Line(List<double> values, double maxValue, string stroke, string fill, string key) => new(UiKind.ChartLine)
    {
        Key = key,
        Data = values.Select((value, i) => ((double)i, value)).ToList(),
        RangeX = (0, values.Count - 1),
        RangeY = (0, maxValue),
        StrokeColor = stroke,
        FillColor = fill,
    };

    // tg: common/LoadingScreen.tsx:25  (the toolbox does not spin here)
    private static UiNode LoadingScreen() => new UiNode(UiKind.Stack) { Key = "loading", Align = "center", Fill = true, Justify = "center", Vertical = true }.With(
        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Color = "blue", Icon = "toolbox", Size = 4 }),
        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "Please wait..." }));

    // tg: PowerMonitor.tsx:149
    private static UiNode StationAreas(TguiContext c)
    {
        string sortByField = c.UseState(SortKey, "");

        List<Area> areas = c.Data.Arr("areas")
            .Select(a => new Area(a.Str("name"), a.Num("charge"), a.Num("charging"), a.Num("eqp"), a.Num("lgt"), a.Num("env"), a.Str("load")))
            .ToList();
        // tg: PowerMonitor.tsx:163  .sort(...): a stable sort, a comparator that returns NaN counts as equal
        Comparison<Area> compare = sortByField switch
        {
            "name" => NameSort,
            "charge" => (a, b) => Sign(a.Charge - b.Charge),
            "draw" => (a, b) => Sign(PowerSort(b, a)),
            _ => (a, b) => 0,
        };
        areas = areas.Select((area, i) => (area, i)).OrderBy(x => x, Comparer<(Area area, int i)>.Create((x, y) => compare(x.area, y.area) is var r && r != 0 ? r : x.i.CompareTo(y.i))).Select(x => x.area).ToList();

        UiNode SortBox(string field, string label) => new UiNode(UiKind.Button)
        {
            Key = "sort:" + field,
            Checkbox = sortByField == field,
            Text = label,
            OnClick = () => c.SetState(SortKey, sortByField != field ? field : ""),
        };

        var table = new UiNode(UiKind.Table).With(
            // tg: PowerMonitor.tsx:209  <Table.Row header>
            new UiNode(UiKind.TableRow) { Key = "head" }.With(
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Area" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Charge" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Draw", TextAlign = "right", Width = 7 },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Eqp", Tooltip = "Equipment power" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Lgt", Tooltip = "Lighting power" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Env", Tooltip = "Environment power" }));
        for (int i = 0; i < areas.Count; i++)
        {
            Area area = areas[i];
            // tg: PowerMonitor.tsx:224  key={area.id} (area.name + i)
            table.With(new UiNode(UiKind.TableRow) { Key = "area:" + area.Name + i, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "name", Text = area.Name },
                AreaCharge(area.Charging, area.Charge),
                new UiNode(UiKind.TableCell) { Key = "load", TextAlign = "right", Text = area.Load },
                StatusCell("eqp", area.Eqp),
                StatusCell("lgt", area.Lgt),
                StatusCell("env", area.Env)));
        }

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            // tg: PowerMonitor.tsx:179
            new UiNode(UiKind.Section) { Height = 3 }.With(
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.Box) { Color = "label", Text = "Sort by:" },
                    SortBox("name", "Name"),
                    SortBox("charge", "Charge"),
                    SortBox("draw", "Draw"))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(table)));
    }

    private static int Sign(double value) => double.IsNaN(value) ? 0 : Math.Sign(value);

    // tg: PowerMonitor.tsx:262  const NOT_CHARGING = 0, CHARGING = 1, CHARGED = 2 (with the table cell both windows wrap it in;
    // ApcControl's too, package E6)
    public static UiNode AreaCharge(double charging, double charge)
    {
        string name;
        if (charging == 0)
            name = charge > 50 ? "battery-half" : "battery-quarter";
        else if (charging == 1)
            name = "bolt";
        else
            name = "battery-full";

        string color = charging == 0 ? (charge > 50 ? "yellow" : "red") : charging == 1 ? "yellow" : charging == 2 ? "green" : null;
        return new UiNode(UiKind.TableCell) { Key = "charge", TextAlign = "right" }.With(
            new UiNode(UiKind.Icon) { Key = "icon", Icon = name, Color = color },
            new UiNode(UiKind.Box) { Key = "percent", Width = "36px", TextAlign = "right", Text = $"{TguiFormat.ToFixed(charge)}%" });
    }

    // tg: PowerMonitor.tsx:298  AreaStatusColorBox: bit 2 is the channel's power, bit 1 its auto mode
    private static UiNode StatusCell(string key, double status)
    {
        bool power = ((int)status & 2) != 0;
        bool mode = ((int)status & 1) != 0;
        return new UiNode(UiKind.TableCell) { Key = key, TextAlign = "center" }.With(
            new UiNode(UiKind.ColorBox)
            {
                Key = "box",
                Color = power ? "good" : "bad",
                Text = mode ? null : "M",
                Tooltip = $"{(power ? "On" : "Off")} [{(mode ? "auto" : "manual")}]",
            });
    }
}
