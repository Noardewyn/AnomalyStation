// Translated from tgstation (snapshot 1d99bd7f): code/datums/screentips/screentips.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D125, docs/ai/specs/interaction.md 6) but the GLOBAL_LIST_INIT_TYPED of the cursor hint icons (:4,
// GLOBAL_* initializers are not declared, m4a-ownership.md): prepare_screentip_context_icons' images and build_context's
// \icon are deferred leaves (presentation: the 3D client draws the mouse buttons itself, DV28).

// tg: code/datums/screentips/screentips.dm:6  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    // tg: code/datums/screentips/screentips.dm:1  #define HINT_ICON_FILE 'icons/ui/screentips/cursor_hints.dmi' (file-local)
    private const string HINT_ICON_FILE = "icons/ui/screentips/cursor_hints.dmi";

    // tg: code/datums/screentips/screentips.dm:6
    public static object prepare_screentip_context_icons()
    {
        DmList output = Dm.list();
        // tg: :8-9  for(var/state in icon_states(HINT_ICON_FILE)) output[state] = image(HINT_ICON_FILE, icon_state = state)
        Dm.Deferred("code/datums/screentips/screentips.dm:8", "prepare_screentip_context_icons: the cursor hint images of " + HINT_ICON_FILE + " (icon_states reads the .dmi; presentation: the 3D client draws the mouse buttons, DV28)");
        return output;
    }

    /*
     * # Compiles a string for this key
     * Args:
     * - context = list (REQUIRED)
     * 	- Must contain key
     * - key = string (REQUIRED)
     * - allow_image = boolean (not required)
    */
    // tg: code/datums/screentips/screentips.dm:20
    public static object build_context(DmList context = null, object key = null, object allow_image = null)
    {
        if (!Dm.truthy(Dm.length(context)) || !Dm.truthy(context[key]) || !Dm.truthy(key))
            return "";
        // Splits key combinations from mouse buttons. e.g. `Ctrl-Shift-LMB` goes in, `Ctrl-Shift-` goes out. Will be empty for single button actions.
        string key_combo = Dm.length(key) > 3 ? Dm.text("[]", copytext(key, 1, -3)) : "";
        // Grab the mouse button, LMB/RMB
        object button = copytext(key, -3);
        if (Dm.truthy(allow_image))
        {
            // Compile into image, if allowed
            // tg: :30  button = "\icon[GLOB.screentip_context_icons[button]]"
            Dm.Deferred("code/datums/screentips/screentips.dm:30", "build_context: the \\icon of the mouse button's cursor hint (GLOB.screentip_context_icons is not declared; presentation: the 3D client draws the buttons, DV28)");
        }
        // Voilá, final result
        return I18n.T("[][][] []", key_combo, button, Dm.truthy(allow_image) ? "" : ":", context[key]);
    }
}
