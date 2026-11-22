using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosGasAnalyzer.tsx (silicons SI2, D326: the cyborg
// tablet's and the PDAs' gas scanner app). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosGasAnalyzer
{
    // tg: tgui/packages/tgui/interfaces/NtosGasAnalyzer.tsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string atmozphereMode = data.Str("atmozphereMode");
        var content = new UiNode(UiKind.Fragment);
        if (data.Truthy("clickAtmozphereCompatible"))
        {
            content.With(new UiNode(UiKind.Button)
            {
                Key = "scantoggle",
                Icon = "sync",
                Fluid = true,
                TextAlign = "center",
                Tooltip = atmozphereMode == "click"
                    ? "Right-click on objects while holding the tablet to scan them. Right-click on the tablet to scan the current location."
                    : "The app will update its gas mixture reading automatically.",
                Text = atmozphereMode == "click" ? "Scanning tapped objects. Click to switch." : "Scanning current location. Click to switch.",
                OnClick = () => c.Act("scantoggle"),
            });
        }
        content.With(GasAnalyzer.Content(c));
        // <NtosWindow width={500} height={450}><NtosWindow.Content scrollable>
        return NtosWindow.Render(c, 500, 450, null, content);
    }
}
