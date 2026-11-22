using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosMODsuit.jsx (gaps MO1): the MODsuit Control
// program (maintenance/modsuit.dm): the synced suit's MODsuit panel (MODsuit.Content), or tg's notice while no suit is synced.
// tg passes theme={ui_theme} to NtosWindow, whose layout takes the device's PC_device_theme and ignores the prop: so here.
// Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosMODsuit
{
    // tg: NtosMODsuit.jsx:7  <NtosWindow theme={ui_theme}> (NtosWindow's default 575 x 700), <NtosWindow.Content scrollable>
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 575, 700, null, new UiNode(UiKind.Box) { Key = "content", Scrollable = true, Fill = true }.With(Content(c)));

    // tg: NtosMODsuit.jsx:19
    private static UiNode Content(TguiContext c)
    {
        if (!c.Data.Truthy("has_suit"))
            return new UiNode(UiKind.NoticeBox)
            {
                Key = "no_suit",
                Color = "danger",
                FontSize = "12px",
                Text = "No Modular suit connected, please tap a suit on the application host to sync on",
            };
        return MODsuit.Content(c);
    }
}
