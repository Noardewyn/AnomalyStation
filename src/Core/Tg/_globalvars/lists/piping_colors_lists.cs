// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/piping_colors_lists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 I1b: GLOB.pipe_colors_ordered (the pipes' layer, pipes.dm:150); M4b slice 2: GLOB.pipe_paint_colors and
// GLOB.pipe_color_name (the machines' names, atmosmachinery.dm:556). The whole file.

public partial class datum_controller_global_vars
{
    ///All colors available to pipes and atmos components
    // tg: code/_globalvars/lists/piping_colors_lists.dm:2  GLOBAL_LIST_INIT(pipe_paint_colors, list(...))
    public DmList pipe_paint_colors;

    // tg: code/_globalvars/lists/piping_colors_lists.dm:2  (GLOBAL_MANAGED(pipe_paint_colors, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpipe_paint_colors()
    {
        pipe_paint_colors = Dm.list(
            Dm.kv("omni", ATMOS_COLOR_OMNI),
            Dm.kv("green", COLOR_VIBRANT_LIME),
            Dm.kv("blue", COLOR_BLUE),
            Dm.kv("red", COLOR_RED),
            Dm.kv("orange", COLOR_ENGINEERING_ORANGE),
            Dm.kv("cyan", COLOR_CYAN),
            Dm.kv("dark", COLOR_DARK),
            Dm.kv("yellow", COLOR_YELLOW),
            Dm.kv("brown", COLOR_BROWN),
            Dm.kv("pink", COLOR_LIGHT_PINK),
            Dm.kv("purple", COLOR_PURPLE),
            Dm.kv("violet", COLOR_STRONG_VIOLET));
        Dm.append(ref gvars_datum_init_order, "pipe_paint_colors");
    }

    ///List that sorts the colors and is used for setting up the pipes layer so that they overlap correctly
    // tg: code/_globalvars/lists/piping_colors_lists.dm:18  GLOBAL_LIST_INIT(pipe_colors_ordered, sort_list(list(...)))
    public DmList pipe_colors_ordered;

    // tg: code/_globalvars/lists/piping_colors_lists.dm:18  (GLOBAL_MANAGED(pipe_colors_ordered, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpipe_colors_ordered()
    {
        pipe_colors_ordered = (DmList)sort_list(Dm.list(
            Dm.kv(COLOR_AMETHYST, -6),
            Dm.kv(COLOR_BLUE, -5),
            Dm.kv(COLOR_BROWN, -4),
            Dm.kv(COLOR_CYAN, -3),
            Dm.kv(COLOR_DARK, -2),
            Dm.kv(COLOR_VIBRANT_LIME, -1),
            Dm.kv(ATMOS_COLOR_OMNI, 0),
            Dm.kv(COLOR_ENGINEERING_ORANGE, 1),
            Dm.kv(COLOR_PURPLE, 2),
            Dm.kv(COLOR_RED, 3),
            Dm.kv(COLOR_STRONG_VIOLET, 4),
            Dm.kv(COLOR_YELLOW, 5)));
        Dm.append(ref gvars_datum_init_order, "pipe_colors_ordered");
    }

    ///Names shown in the examine for every colored atmos component
    // tg: code/_globalvars/lists/piping_colors_lists.dm:34  GLOBAL_LIST_INIT(pipe_color_name, sort_list(list(...)))
    public DmList pipe_color_name;

    // tg: code/_globalvars/lists/piping_colors_lists.dm:34  (GLOBAL_MANAGED(pipe_color_name, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpipe_color_name()
    {
        pipe_color_name = (DmList)sort_list(Dm.list(
            Dm.kv(ATMOS_COLOR_OMNI, "omni"),
            Dm.kv(COLOR_BLUE, "blue"),
            Dm.kv(COLOR_RED, "red"),
            Dm.kv(COLOR_VIBRANT_LIME, "green"),
            Dm.kv(COLOR_ENGINEERING_ORANGE, "orange"),
            Dm.kv(COLOR_CYAN, "cyan"),
            Dm.kv(COLOR_DARK, "dark"),
            Dm.kv(COLOR_YELLOW, "yellow"),
            Dm.kv(COLOR_BROWN, "brown"),
            Dm.kv(COLOR_LIGHT_PINK, "pink"),
            Dm.kv(COLOR_PURPLE, "purple"),
            Dm.kv(COLOR_STRONG_VIOLET, "violet")));
        Dm.append(ref gvars_datum_init_order, "pipe_color_name");
    }
}
