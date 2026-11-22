using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LockedSafe.tsx with NuclearBomb.tsx's NukeKeypad (NuclearBomb.cs) (M10
// slice G2, D274: the secure safes' keypad, the captain's spare ID safe). Engine-free (compiled into the tests too). The display boxes
// (NuclearBomb__displayBox) are boxes of large bold text.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LockedSafe
{
    // tg: LockedSafe.tsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string inputCode = data.Str("input_code");
        bool locked = data.Truthy("locked");
        bool lockCode = data.Truthy("lock_code");
        c.WindowTheme = "ntos";
        string status = !lockCode ? "No password set." : !locked ? "Unlocked" : "Locked";
        return c.Window(300, 400, null,
            new UiNode(UiKind.Box) { Key = "code", Bold = true, FontSize = "24px", TextAlign = "center", Text = inputCode },
            new UiNode(UiKind.Box) { Key = "status", Bold = true, FontSize = "18px", TextAlign = "center", Text = status },
            NuclearBomb.NukeKeypad(c));
    }
}
