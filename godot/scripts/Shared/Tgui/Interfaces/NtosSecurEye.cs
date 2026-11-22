using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosSecurEye.tsx (syndicate G3d, D316: SecurEye and
// the uplink's SyndEye). Engine-free (compiled into the tests too). The camera list and the map view are CameraConsole's content.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosSecurEye
{
    // tg: NtosSecurEye.tsx:4
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 800, 600, null, CameraConsole.Content(c));
}
