// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/colors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOBAL_LIST_INIT(cable_colors) translated (M5 slice 2: the cable coil's colours, cable.dm:544) and
// GLOBAL_LIST_INIT(heretic_path_to_color) (the bitrunning heretic hunt's rune); the colour
// constants are generated into Defines, the other colour lists come with their users.

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/colors.dm:510  GLOBAL_LIST_INIT(cable_colors, list(...))
    public DmList cable_colors;

    // tg: code/__DEFINES/colors.dm:510  (GLOBAL_MANAGED(cable_colors, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcable_colors()
    {
        cable_colors = Dm.list(
            Dm.kv(CABLE_COLOR_BLUE, CABLE_HEX_COLOR_BLUE),
            Dm.kv(CABLE_COLOR_CYAN, CABLE_HEX_COLOR_CYAN),
            Dm.kv(CABLE_COLOR_GREEN, CABLE_HEX_COLOR_GREEN),
            Dm.kv(CABLE_COLOR_ORANGE, CABLE_HEX_COLOR_ORANGE),
            Dm.kv(CABLE_COLOR_PINK, CABLE_HEX_COLOR_PINK),
            Dm.kv(CABLE_COLOR_RED, CABLE_HEX_COLOR_RED),
            Dm.kv(CABLE_COLOR_WHITE, CABLE_HEX_COLOR_WHITE),
            Dm.kv(CABLE_COLOR_YELLOW, CABLE_HEX_COLOR_YELLOW),
            Dm.kv(CABLE_COLOR_BROWN, CABLE_HEX_COLOR_BROWN)
        );
        Dm.append(ref gvars_datum_init_order, "cable_colors");
    }

    // tg: code/__DEFINES/colors.dm:522  GLOBAL_LIST_INIT(heretic_path_to_color, list(...))
    public DmList heretic_path_to_color;

    // tg: code/__DEFINES/colors.dm:522  (GLOBAL_MANAGED(heretic_path_to_color, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalheretic_path_to_color()
    {
        heretic_path_to_color = Dm.list(
            Dm.kv(PATH_START, COLOR_LIME),
            Dm.kv(PATH_RUST, COLOR_CARGO_BROWN),
            Dm.kv(PATH_FLESH, COLOR_SOFT_RED),
            Dm.kv(PATH_ASH, COLOR_VIVID_RED),
            Dm.kv(PATH_VOID, COLOR_CYAN),
            Dm.kv(PATH_BLADE, COLOR_SILVER),
            Dm.kv(PATH_COSMIC, COLOR_PURPLE),
            Dm.kv(PATH_LOCK, COLOR_YELLOW),
            Dm.kv(PATH_MOON, COLOR_BLUE_LIGHT)
        );
        Dm.append(ref gvars_datum_init_order, "heretic_path_to_color");
    }
}

// Gaps R3 (D327): the pride flags' colour list macros (the pride week holiday's colours, holidays.dm:487); each builds a new list at
// every use, as the macro's expansion does.
public static partial class GlobalProcs
{
    // tg: code/__DEFINES/colors.dm:338
    public static DmList PRIDE_FLAG_COLORS() => Dm.list(COLOR_PRIDE_PURPLE, COLOR_PRIDE_BLUE, COLOR_PRIDE_GREEN, COLOR_PRIDE_YELLOW, COLOR_PRIDE_ORANGE, COLOR_PRIDE_RED);
    // tg: code/__DEFINES/colors.dm:345
    public static DmList LESBIAN_FLAG_COLORS() => Dm.list(COLOR_LESBIAN_MAGENTA, COLOR_LESBIAN_PINK, COLOR_LESBIAN_ORANGE, COLOR_LESBIAN_RED);
    // tg: code/__DEFINES/colors.dm:352
    public static DmList GAY_MAN_FLAG_COLORS() => Dm.list(COLOR_GAY_BLUE, COLOR_GAY_LIGHT_BLUE, COLOR_GAY_LIGHT_GREEN, COLOR_GAY_GREEN);
    // tg: code/__DEFINES/colors.dm:357
    public static DmList TRANS_FLAG_COLORS() => Dm.list(COLOR_TRANS_PINK, COLOR_WHITE, COLOR_TRANS_PINK, COLOR_TRANS_BLUE);
    // tg: code/__DEFINES/colors.dm:363
    public static DmList BI_FLAG_COLORS() => Dm.list(COLOR_BI_BLUE, COLOR_BI_PURPLE, COLOR_BI_PINK);
    // tg: code/__DEFINES/colors.dm:369
    public static DmList ACE_FLAG_COLORS() => Dm.list(COLOR_ACE_PURPLE, COLOR_WHITE, COLOR_ACE_GREY, COLOR_ACE_BLACK);
    // tg: code/__DEFINES/colors.dm:375
    public static DmList PAN_FLAG_COLORS() => Dm.list(COLOR_PAN_CYAN, COLOR_PAN_YELLOW, COLOR_PAN_MAGENTA);
}
