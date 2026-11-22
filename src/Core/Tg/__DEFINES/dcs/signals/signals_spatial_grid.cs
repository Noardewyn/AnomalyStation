// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/dcs/signals/signals_spatial_grid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4b audio slice 1, the spatial grid): its two function-like signal macros (rules 11.1).

public static partial class GlobalProcs
{
    //spatial grid signals

    ///Called from base of /datum/controller/subsystem/spatial_grid/proc/enter_cell: (/atom/movable)
    // tg: code/__DEFINES/dcs/signals/signals_spatial_grid.dm:4  #define SPATIAL_GRID_CELL_ENTERED(contents_type) "spatial_grid_cell_entered_[contents_type]"
    public static string SPATIAL_GRID_CELL_ENTERED(object contents_type) => Dm.text("spatial_grid_cell_entered_[]", contents_type);

    ///Called from base of /datum/controller/subsystem/spatial_grid/proc/exit_cell: (/atom/movable)
    // tg: code/__DEFINES/dcs/signals/signals_spatial_grid.dm:6  #define SPATIAL_GRID_CELL_EXITED(contents_type) "spatial_grid_cell_exited_[contents_type]"
    public static string SPATIAL_GRID_CELL_EXITED(object contents_type) => Dm.text("spatial_grid_cell_exited_[]", contents_type);
}
