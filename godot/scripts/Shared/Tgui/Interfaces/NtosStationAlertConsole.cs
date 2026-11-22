namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosStationAlertConsole.tsx (the engineering package
// E6, D287: Canary, the engineering consoles' station alert program). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosStationAlertConsole
{
    // tg: NtosStationAlertConsole.tsx:4  <NtosWindow.Content scrollable>: the content in the window's own area
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 335, 587, null, StationAlertConsole.Content(c));
}
