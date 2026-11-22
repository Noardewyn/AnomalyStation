using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/layouts/NtosWindow.tsx (M9 slice E: NtOS's window, which every
// program of a modular computer draws in). Engine-free (compiled into the tests too). The window's theme is the device's
// (PC_device_theme: the client's palettes, TguiTheme); the header pictures are tg's GIFs (headers.dm) converted by the icons tool.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosWindow
{
    /// <summary>The file of one of tg's NtOS header pictures (/datum/asset/simple/headers: "batt_100.gif" is icons/ui/programs/batt_100.gif).</summary>
    public static string HeaderIcon(string name) => "icons/ui/programs/" + name;

    private static UiNode Header(string key, string name) => new UiNode(UiKind.Box)
    {
        Key = key,
        DmIcon = HeaderIcon(name),
        DmIconState = "",
        Width = "40px",
        Height = "22px",
    };

    // tg: NtosWindow.tsx:57
    public static UiNode Render(TguiContext c, double width, double height, string title, params UiNode[] content)
    {
        UiValue data = c.Data;
        string theme = data.Str("PC_device_theme");
        string batteryIcon = data.Str("PC_batteryicon");
        string batteryPercent = data.Str("PC_batterypercent");
        string ntnetIcon = data.Str("PC_ntneticon");
        string stationDate = data.Str("PC_stationdate");
        string stationTime = data.Str("PC_stationtime");
        IReadOnlyList<UiValue> headers = data.Get("PC_programheaders")?.Items ?? Array.Empty<UiValue>();
        bool showExit = data.Truthy("PC_showexitprogram");
        bool lowPower = data.Truthy("PC_lowpower_mode");

        var right = new List<UiNode>();
        foreach (UiValue header in headers)
            right.Add(new UiNode(UiKind.StackItem) { Key = "header:" + header.Str("icon") }.With(Header("programheader", header.Str("icon"))));
        if (!string.IsNullOrEmpty(ntnetIcon))
            right.Add(new UiNode(UiKind.StackItem).With(Header("ntnet", ntnetIcon)));
        if (!string.IsNullOrEmpty(batteryIcon))
        {
            right.Add(new UiNode(UiKind.StackItem).With(Header("battery", batteryIcon)));
            right.Add(new UiNode(UiKind.StackItem) { Key = "batterypercent", Text = batteryPercent });
        }
        if (showExit)
        {
            right.Add(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "minimize", Color = "transparent", Icon = "window-minimize-o", Tooltip = "Minimize", OnClick = () => c.Act("PC_minimize") }));
            right.Add(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "close", Color = "transparent", Icon = "window-close-o", Tooltip = "Close", OnClick = () => c.Act("PC_exit") }));
        }
        else
            right.Add(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "shutdown", Color = "transparent", Icon = "power-off", Tooltip = "Power off", OnClick = () => c.Act("PC_shutdown") }));

        var header_row = new UiNode(UiKind.Stack) { Key = "ntosheader", Align = "center" }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "calendar", Color = "transparent", Icon = "calendar", Tooltip = stationDate }),
            new UiNode(UiKind.StackItem) { Key = "time", Bold = true, Text = stationTime },
            new UiNode(UiKind.StackItem) { Key = "os", Opacity = 0.33, Text = (theme == "syndicate" ? "Syndix" : "NtOS") + (lowPower ? " - RUNNING ON LOW POWER MODE" : "") },
            new UiNode(UiKind.StackItem) { Grow = 1 },
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack) { Align = "center" }.With(right)));

        c.WindowTheme = theme;
        return c.Window(width, height, title, new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem).With(header_row),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(content)));
    }
}
