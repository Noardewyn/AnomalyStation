using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosPowerMonitor.tsx (gaps S4b, D327): AmpCheck, the
// power monitoring console's PowerMonitor content in an NtOS window. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosPowerMonitor
{
    // tg: NtosPowerMonitor.tsx:4
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 550, 700, null, PowerMonitor.Content(c));
}
