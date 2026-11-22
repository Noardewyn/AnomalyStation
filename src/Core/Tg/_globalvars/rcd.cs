// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/rcd.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the whole file, the pictures of the RCD's destruction scan holograms.

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/rcd.dm:1  GLOBAL_VAR_INIT(icon_holographic_wall, init_holographic_wall())
    public object icon_holographic_wall;

    // tg: code/_globalvars/rcd.dm:1  (GLOBAL_MANAGED(icon_holographic_wall, init_holographic_wall()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalicon_holographic_wall()
    {
        icon_holographic_wall = init_holographic_wall();
        Dm.append(ref gvars_datum_init_order, "icon_holographic_wall");
    }

    // tg: code/_globalvars/rcd.dm:2  GLOBAL_VAR_INIT(icon_holographic_window, init_holographic_window())
    public object icon_holographic_window;

    // tg: code/_globalvars/rcd.dm:2  (GLOBAL_MANAGED(icon_holographic_window, init_holographic_window()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalicon_holographic_window()
    {
        icon_holographic_window = init_holographic_window();
        Dm.append(ref gvars_datum_init_order, "icon_holographic_window");
    }
}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/rcd.dm:4
    public static object init_holographic_wall()
    {
        return Dm.icon(Dm.resource("icons/turf/walls/wall.dmi"), "wall-0");
    }

    // tg: code/_globalvars/rcd.dm:7
    public static object init_holographic_window()
    {
        icon grille_icon = Dm.icon(Dm.resource("icons/obj/structures.dmi"), "grille");
        icon window_icon = Dm.icon(Dm.resource("icons/obj/smooth_structures/window.dmi"), "window-0");

        grille_icon.Blend(window_icon, ICON_OVERLAY);

        return grille_icon;
    }
}
