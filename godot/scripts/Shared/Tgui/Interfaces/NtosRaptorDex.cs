using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosRaptorDex.tsx: the RaptorDex's content in an NtOS
// window (the PDA's RaptorDex program, raptordex.dm). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosRaptorDex
{
    // tg: NtosRaptorDex.tsx:4
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 770, 370, null, RaptorDex.Content(c));
}
