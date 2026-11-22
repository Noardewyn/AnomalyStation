namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosCargo.tsx (gaps P4b, D298: the NT Shopping Network
// app): the supply console's content in a computer's window. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosCargo
{
    // tg: NtosCargo.tsx:4
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 800, 500, null, Cargo.Content(c));
}
