using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ExoscannerConsole.tsx: the scanner array control
// console: the arrays' power and the site's scan conditions, the wide scan, the sites to target with their point and deep scans, the
// scan in progress with its emergency stop, and the scan failure. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ExoscannerConsole
{
    // tg: ExoscannerConsole.tsx:186
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double scanPower = data.Num("scan_power");
        string selectedSite = data.Str("selected_site");
        bool canStartWideScan = scanPower > 0;

        var power = new UiNode(UiKind.StackItem) { Key = "power" }.With(
            new UiNode(UiKind.Section) { Key = "power", Fill = true, Title = "Available array power" }.With(
                new UiNode(UiKind.Stack) { Key = "power" }.With(
                    new UiNode(UiKind.StackItem) { Key = "power", Grow = 1 }.With(scanPower > 0
                        ? new UiNode(UiKind.Fragment).With(
                            new UiNode(UiKind.Box) { Key = "power", FontSize = 2, Text = TguiFormat.JsNumber(scanPower) },
                            new UiNode(UiKind.Icon) { Key = "dish", Icon = "satellite-dish", Size = 3 })
                        : new UiNode(UiKind.Box) { Key = "none", Text = "No properly configured scanner arrays detected." })),
                new UiNode(UiKind.Section) { Key = "conditions", Title = "Special Scan Condtions" }.With(
                    data.Arr("scan_conditions").Select(condition => new UiNode(UiKind.NoticeBox) { Key = condition.AsStr(), Text = condition.AsStr() }))));

        var stack = new UiNode(UiKind.Stack) { Key = "content", Vertical = true, Fill = true }.With(power);
        if (!string.IsNullOrEmpty(selectedSite))
            stack.With(new UiNode(UiKind.StackItem) { Key = "selected", Grow = 1 }.With(ScanSelectionSection(c)));
        else
        {
            stack.With(
                new UiNode(UiKind.StackItem) { Key = "wide" }.With(new UiNode(UiKind.Section)
                {
                    Key = "wide",
                    Fill = true,
                    Title = "Configure Wide Scan",
                    Buttons = new UiNode(UiKind.Button) { Key = "scan", Icon = "search", Disabled = !canStartWideScan, Text = "Scan", OnClick = () => c.Act("start_wide_scan") },
                }.With(new UiNode(UiKind.Stack) { Key = "wide" }.With(
                    new UiNode(UiKind.StackItem) { Key = "quote" }.With(new UiNode(UiKind.Box) { Key = "quote", Wrap = true, Text = "Broad spectrum scan looking for anything not matching known start charts." }),
                    new UiNode(UiKind.StackItem) { Key = "cost" }.With(new UiNode(UiKind.Box)
                    {
                        Key = "cost",
                        Text = "Cost estimate: " + (scanPower > 0 ? TguiFormat.FormatTime(data.Num("wide_scan_eta"), "short") : "∞ minutes"),
                    })))),
                new UiNode(UiKind.StackItem) { Key = "targeted", Grow = 1 }.With(new UiNode(UiKind.Section)
                {
                    Key = "targeted",
                    Fill = true,
                    Title = "Configure Targeted Scans",
                    Scrollable = true,
                    Buttons = new UiNode(UiKind.Button) { Key = "experiments", Icon = "tasks", Text = "View Experiments", OnClick = () => c.Act("open_experiments") },
                }.With(new UiNode(UiKind.Stack) { Key = "sites", Vertical = true }.With(
                    data.Arr("possible_sites").Select(site =>
                    {
                        string reference = site.Str("ref");
                        return new UiNode(UiKind.StackItem) { Key = reference }.With(
                            new UiNode(UiKind.Button) { Key = reference, Text = site.Str("name"), OnClick = () => c.Act("select_site", ("site_ref", reference)) });
                    })))));
        }

        // tg: ExoscannerConsole.tsx:200  {!!scan_in_progress && <ScanInProgressModal />} {!!failed && <ScanFailedModal />}
        if (data.Truthy("scan_in_progress"))
            stack.With(ScanInProgressModal(c));
        if (data.Truthy("failed"))
            stack.With(ScanFailedModal(c));
        return c.Window(550, 600, null, stack);
    }

    // tg: ExoscannerConsole.tsx:34
    private static UiNode ScanFailedModal(TguiContext c) => new UiNode(UiKind.Modal) { Key = "failed" }.With(
        new UiNode(UiKind.Stack) { Key = "failed", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "text" }.With(new UiNode(UiKind.Box) { Key = "text", Color = "bad", Text = "SCAN FAILURE!" }),
            new UiNode(UiKind.StackItem) { Key = "confirm" }.With(new UiNode(UiKind.Button) { Key = "confirm", Text = "Confirm", OnClick = () => c.Act("confirm_fail") })));

    // tg: ExoscannerConsole.tsx:50
    private static UiNode ScanSelectionSection(TguiContext c)
    {
        UiValue data = c.Data;
        double scanPower = data.Num("scan_power");
        // tg reads the scan flags from the window's data, where the console does not put them (they are in site_data): both scans show
        bool pointScanComplete = data.Truthy("point_scan_complete"), deepScanComplete = data.Truthy("deep_scan_complete");
        UiValue site = data.Field("site_data");
        string pointCost = scanPower > 0 ? TguiFormat.FormatTime(data.Num("point_scan_eta"), "short") : "∞";
        string deepCost = scanPower > 0 ? TguiFormat.FormatTime(data.Num("deep_scan_eta"), "short") : "∞";
        bool scanAvailable = !pointScanComplete || !deepScanComplete;

        var siteData = new UiNode(UiKind.LabeledList) { Key = "site" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name", Text = site.Str("name") },
            new UiNode(UiKind.LabeledItem) { Key = "description", Label = "Description", Text = site.Truthy("revealed") ? site.Str("description") : "No Data" },
            new UiNode(UiKind.LabeledItem) { Key = "distance", Label = "Distance", Text = TguiFormat.JsNumber(site.Num("distance")) },
            new UiNode(UiKind.Divider) { Key = "divider1" },
            new UiNode(UiKind.LabeledItem) { Key = "spectrography", Label = "Spectrography Data" },
            new UiNode(UiKind.Divider) { Key = "divider2" });
        foreach (KeyValuePair<string, UiValue> band in site.Obj("band_info"))
            siteData.With(new UiNode(UiKind.LabeledItem) { Key = "band:" + band.Key, Label = band.Key, Text = band.Value.AsStr() });

        var stack = new UiNode(UiKind.Stack) { Key = "selection", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "site", Grow = 1 }.With(new UiNode(UiKind.Section)
            {
                Key = "site",
                Fill = true,
                Title = "Site Data",
                Buttons = new UiNode(UiKind.Button) { Key = "back", Text = "Back", OnClick = () => c.Act("select_site", ("site_ref", null)) },
            }.With(siteData)));
        if (scanAvailable)
        {
            var scans = new UiNode(UiKind.Section) { Key = "scans", Fill = true, Title = "Scans" };
            if (!pointScanComplete)
                scans.With(ScanChoice(c, "point", "Point Scan", "Point scan performs rudimentary scan of the site, revealing its general characteristics.", scanPower, pointCost, "start_point_scan"));
            if (!deepScanComplete)
                scans.With(ScanChoice(c, "deep", "Deep Scan", "Deep scan performs full scan of the site, revealing all details.", scanPower, deepCost, "start_deep_scan"));
            stack.With(new UiNode(UiKind.StackItem) { Key = "scans" }.With(scans));
        }
        return stack;
    }

    // tg: ExoscannerConsole.tsx:104-138  a point or deep scan's section
    private static UiNode ScanChoice(TguiContext c, string key, string title, string quote, double scanPower, string cost, string action) =>
        new UiNode(UiKind.Section) { Key = key, Title = title }.With(
            new UiNode(UiKind.Box) { Key = "quote", Wrap = true, Text = quote },
            new UiNode(UiKind.Stack) { Key = "scan" }.With(
                new UiNode(UiKind.StackItem) { Key = "button" }.With(new UiNode(UiKind.Button) { Key = "scan", Text = "Scan", Disabled = scanPower <= 0, OnClick = () => c.Act(action) }),
                new UiNode(UiKind.StackItem) { Key = "eta" }.With(new UiNode(UiKind.Box) { Key = "eta", Text = "Estimated Time: " + cost + "." })));

    // tg: ExoscannerConsole.tsx:149
    private static UiNode ScanInProgressModal(TguiContext c)
    {
        UiValue data = c.Data;
        return new UiNode(UiKind.Modal) { Key = "scanning" }.With(
            new UiNode(UiKind.NoticeBox) { Key = "notice", Text = "Scan in Progress!" },
            new UiNode(UiKind.LabeledList) { Key = "scan" }.With(
                new UiNode(UiKind.LabeledItem) { Key = "summary", Label = "Scan summary", Text = data.Str("scan_description") },
                new UiNode(UiKind.LabeledItem) { Key = "time", Label = "Time left", Text = TguiFormat.FormatTime(data.Num("scan_time")) },
                new UiNode(UiKind.LabeledItem) { Key = "power", Label = "Scanning array power", Text = TguiFormat.JsNumber(data.Num("scan_power")) },
                new UiNode(UiKind.LabeledItem) { Key = "stop", Label = "Emergency Stop" }.With(
                    c.ButtonConfirm("stop", new UiNode(UiKind.Button) { Key = "stop", Text = "STOP SCAN", Color = "red", Icon = "times", OnClick = () => c.Act("stop_scan") }))));
    }
}
