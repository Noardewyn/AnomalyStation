// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/elevator/elev_doors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z, elevators): the whole file. The elevator windoors (/obj/machinery/door/window/elevator and its mapping
// helpers) are var-only types, generated.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/transport/elevator/elev_doors.dm:1  GLOBAL_LIST_EMPTY(elevator_doors)
    public DmList elevator_doors;

    // tg: code/modules/transport/elevator/elev_doors.dm:1  (GLOBAL_MANAGED(elevator_doors, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalelevator_doors()
    {
        elevator_doors = Dm.list();
        Dm.append(ref gvars_datum_init_order, "elevator_doors");
    }
}
