// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/color.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The carp colours (gaps package C4) and the ethereal and lustrous colours (species package SP2b, D349).

public partial class datum_controller_global_vars
{
    /// The possible colours of a space carp
    // tg: code/_globalvars/lists/color.dm:11
    public DmList carp_colors;
    public virtual void InitGlobalcarp_colors()
    {
        carp_colors = Dm.list(
            Dm.kv(COLOR_CARP_PURPLE, 7f),
            Dm.kv(COLOR_CARP_PINK, 7f),
            Dm.kv(COLOR_CARP_GREEN, 7f),
            Dm.kv(COLOR_CARP_GRAPE, 7f),
            Dm.kv(COLOR_CARP_SWAMP, 7f),
            Dm.kv(COLOR_CARP_TURQUOISE, 7f),
            Dm.kv(COLOR_CARP_BROWN, 7f),
            Dm.kv(COLOR_CARP_TEAL, 7f),
            Dm.kv(COLOR_CARP_LIGHT_BLUE, 7f),
            Dm.kv(COLOR_CARP_RUSTY, 7f),
            Dm.kv(COLOR_CARP_RED, 7f),
            Dm.kv(COLOR_CARP_YELLOW, 7f),
            Dm.kv(COLOR_CARP_BLUE, 7f),
            Dm.kv(COLOR_CARP_PALE_GREEN, 7f),
            Dm.kv(COLOR_CARP_SILVER, 1f)); // The rare silver carp
        Dm.append(ref gvars_datum_init_order, "carp_colors");
    }

    /// List of possible ethereal colours
    // tg: code/_globalvars/lists/color.dm:30
    public DmList color_list_ethereal;
    public virtual void InitGlobalcolor_list_ethereal()
    {
        color_list_ethereal = Dm.list(
            Dm.kv("Blue", "#3399ff"),
            Dm.kv("Bright Yellow", "#ffff99"),
            Dm.kv("Burnt Orange", "#cc4400"),
            Dm.kv("Cyan Blue", "#00ffff"),
            Dm.kv("Dark Blue", "#6666ff"),
            Dm.kv("Dark Fuschia", "#cc0066"),
            Dm.kv("Dark Green", "#37835b"),
            Dm.kv("Dark Red", "#9c3030"),
            Dm.kv("Dull Yellow", "#fbdf56"),
            Dm.kv("Faint Blue", "#b3d9ff"),
            Dm.kv("Faint Green", "#ddff99"),
            Dm.kv("Faint Red", "#ffb3b3"),
            Dm.kv("Green", "#97ee63"),
            Dm.kv("Orange", "#ffa64d"),
            Dm.kv("Pink", "#ff99cc"),
            Dm.kv("Purple", "#ee82ee"),
            Dm.kv("Red", "#ff4d4d"),
            Dm.kv("Seafoam Green", "#00fa9a"),
            Dm.kv("White", "#f2f2f2"));
        Dm.append(ref gvars_datum_init_order, "color_list_ethereal");
    }

    /// List of possible lustrous ethereal colours
    // tg: code/_globalvars/lists/color.dm:53
    public DmList color_list_lustrous;
    public virtual void InitGlobalcolor_list_lustrous()
    {
        color_list_lustrous = Dm.list(
            Dm.kv("Cyan Blue", "#00ffff"),
            Dm.kv("Sky Blue", "#37c0ff"),
            Dm.kv("Blue", "#3374ff"),
            Dm.kv("Dark Blue", "#5b5beb"),
            Dm.kv("Bright Red", "#fa2d2d"));
        Dm.append(ref gvars_datum_init_order, "color_list_lustrous");
    }
}
