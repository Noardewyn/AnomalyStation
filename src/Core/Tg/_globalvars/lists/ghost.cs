// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/ghost.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the ghosts, 2026-10-16).

// tg: code/_globalvars/lists/ghost.dm:2
public partial class datum_controller_global_vars : datum_controller
{
    /// Stores the ghost forms that support directional sprites
    // tg: code/_globalvars/lists/ghost.dm:2  GLOBAL_LIST_INIT(ghost_forms_with_directions_list, list(...))
    public DmList ghost_forms_with_directions_list;

    // tg: code/_globalvars/lists/ghost.dm:2  (GLOBAL_MANAGED(ghost_forms_with_directions_list, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalghost_forms_with_directions_list()
    {
        ghost_forms_with_directions_list = Dm.list(
            "catghost",
            "ghost_black",
            "ghost_blazeit",
            "ghost_blue",
            "ghost_camo",
            "ghost_cyan",
            "ghost_dblue",
            "ghost_dcyan",
            "ghost_dgreen",
            "ghost_dpink",
            "ghost_dred",
            "ghost_dyellow",
            "ghost_fire",
            "ghost_funkypurp",
            "ghost_green",
            "ghost_grey",
            "ghost_mellow",
            "ghost_pink",
            "ghost_pinksherbert",
            "ghost_purpleswirl",
            "ghost_rainbow",
            "ghost_red",
            "ghost_yellow",
            "ghost",
            "ghostian",
            "ghostian2",
            "ghostking",
            "skeleghost"
        );
        Dm.append(ref gvars_datum_init_order, "ghost_forms_with_directions_list");
    }

    /// Stores the ghost forms that support hair and other such things
    // tg: code/_globalvars/lists/ghost.dm:34  GLOBAL_LIST_INIT(ghost_forms_with_accessories_list, list(...))
    public DmList ghost_forms_with_accessories_list;

    // tg: code/_globalvars/lists/ghost.dm:34  (GLOBAL_MANAGED(ghost_forms_with_accessories_list, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalghost_forms_with_accessories_list()
    {
        ghost_forms_with_accessories_list = Dm.list(
            "ghost_black",
            "ghost_blazeit",
            "ghost_blue",
            "ghost_camo",
            "ghost_cyan",
            "ghost_dblue",
            "ghost_dcyan",
            "ghost_dgreen",
            "ghost_dpink",
            "ghost_dred",
            "ghost_dyellow",
            "ghost_fire",
            "ghost_funkypurp",
            "ghost_green",
            "ghost_grey",
            "ghost_mellow",
            "ghost_pink",
            "ghost_pinksherbert",
            "ghost_purpleswirl",
            "ghost_rainbow",
            "ghost_red",
            "ghost_yellow",
            "ghost",
            "skeleghost"
        );
        Dm.append(ref gvars_datum_init_order, "ghost_forms_with_accessories_list");
    }
}
