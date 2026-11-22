using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosStatus.tsx (gaps S4a, D327): the status display
// app, the communications console's StatusDisplayControls in an NtOS window. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosStatus
{
    // tg: NtosStatus.tsx:4
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 400, 350, null, StatusDisplayControls.Render(c));
}
