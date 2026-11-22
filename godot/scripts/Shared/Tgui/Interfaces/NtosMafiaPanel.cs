using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosMafiaPanel.tsx (gaps S4d, D327): Mafia's panel in
// the PDA's NtOS window. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosMafiaPanel
{
    // tg: NtosMafiaPanel.tsx:4
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 900, 600, null, MafiaPanel.Data(c));
}
