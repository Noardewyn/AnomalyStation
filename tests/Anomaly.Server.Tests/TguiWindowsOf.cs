using Anomaly.Protocol;

namespace Anomaly.Server.Tests;

/// <summary>G4 (protocol 52): a replica's tgui windows, without the escape menu's window that every client has 3 s after joining (tg's
/// initialize_escape_menu; the client draws it as its Esc menu, never as a window).</summary>
public static class TguiWindowsOf
{
    public static IEnumerable<UiWindowState> Tgui(this IReadOnlyDictionary<int, UiWindowState> windows)
        => windows.Values.Where(v => v.Interface != Limits.EscapeMenuUiInterface);
}
