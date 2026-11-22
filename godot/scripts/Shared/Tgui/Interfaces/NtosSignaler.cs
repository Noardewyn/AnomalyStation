using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosSignaler.tsx (gaps S4b, D327): SignalCommander,
// the signaler's content in an NtOS window. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosSignaler
{
    // tg: NtosSignaler.tsx:4
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 400, 300, null, Signaler.Content(c));
}
