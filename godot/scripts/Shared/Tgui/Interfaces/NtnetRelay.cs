using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtnetRelay.tsx (M9 slice E: the NTNet quantum relay).
// Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtnetRelay
{
    // tg: NtnetRelay.tsx:20
    private const string OutageWarning = "This system is suffering temporary outage due to overflow of traffic buffers. Until buffered traffic is processed, all "
        + "further requests will be dropped. Frequent occurences of this error may indicate insufficient hardware capacity of your network. "
        + "Please contact your network planning department for instructions on how to resolve this issue.";

    // tg: NtnetRelay.tsx:27
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool enabled = data.Truthy("enabled");
        double capacity = data.Num("dos_capacity");
        double overload = data.Num("dos_overload");
        bool crashed = data.Truthy("dos_crashed");

        UiNode body = !crashed
            ? new UiNode(UiKind.ProgressBar) { Key = "buffer", Value = overload, MinValue = 0, MaxValue = capacity, Text = TguiFormat.JsNumber(overload) + " GQ / " + TguiFormat.JsNumber(capacity) + " GQ" }
            : new UiNode(UiKind.Box) { Key = "crashed" }.With(
                new UiNode(UiKind.Box) { FontSize = "20px", Text = "NETWORK BUFFER OVERFLOW" },
                new UiNode(UiKind.Box) { FontSize = "16px", Text = "OVERLOAD RECOVERY MODE" },
                new UiNode(UiKind.Box) { Wrap = true, Text = OutageWarning },
                new UiNode(UiKind.Box) { FontSize = "20px", Color = "bad", Text = "ADMINISTRATOR OVERRIDE" },
                new UiNode(UiKind.Box) { FontSize = "16px", Color = "bad", Text = "CAUTION - DATA LOSS MAY OCCUR" },
                new UiNode(UiKind.Button) { Key = "restart", Icon = "signal", Text = "PURGE BUFFER", Color = "bad", OnClick = () => c.Act("restart") });

        return c.Window(400, 300, "NtNet Quantum Relay", new UiNode(UiKind.Section)
        {
            Title = "Network Buffer",
            Buttons = new UiNode(UiKind.Button) { Key = "toggle", Icon = "power-off", Selected = enabled, Text = enabled ? "ENABLED" : "DISABLED", OnClick = () => c.Act("toggle") },
        }.With(body));
    }
}
