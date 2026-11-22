using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosSpectreMeter.jsx (gaps S4c, D327): the Spectre-Meter,
// its automatic or manual scan and the spookiness bar (the ghost icons do not spin here). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosSpectreMeter
{
    private static readonly (string, double, double)[] SpookRanges =
    {
        ("good", 0, 33),
        ("average", 33, 66),
        ("bad", 66, 100),
        ("purple", 100, double.PositiveInfinity),
    };

    // tg: NtosSpectreMeter.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool autoMode = data.Truthy("auto_mode");
        double spook = data.Num("spook_value");
        return NtosWindow.Render(c, 400, 180, null, new UiNode(UiKind.Section) { Title = "Spectre-Meter" }.With(
            new UiNode(UiKind.Stack) { Key = "buttons" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "toggle_mode",
                    Icon = "cog",
                    Text = autoMode ? "Auto" : "Manual",
                    Selected = autoMode,
                    Tooltip = "Toggle automatic scanning. Can be noisy",
                    OnClick = () => c.Act("toggle_mode"),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "scan",
                    Icon = "magnifying-glass",
                    Text = "Scan",
                    Disabled = autoMode || data.Truthy("on_cooldown"),
                    Tooltip = "Has cooldown of about 2 seconds",
                    OnClick = () => c.Act("manual_scan"),
                })),
            new UiNode(UiKind.ProgressBar)
            {
                Key = "spook",
                Value = spook,
                MaxValue = 100,
                Ranges = SpookRanges,
                Text = " Spookiness: " + TguiFormat.JsNumber(spook) + "% ",
            }));
    }
}
