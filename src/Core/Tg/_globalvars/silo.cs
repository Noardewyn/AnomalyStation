// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/silo.dm, code/_globalvars/lists/silo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2a (D304): both files, the station's ore silo and the silos' logs.

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/silo.dm:2  GLOBAL_DATUM(ore_silo_default, /obj/machinery/ore_silo)
    public obj_machinery_ore_silo ore_silo_default;

    // tg: code/_globalvars/silo.dm:2  (GLOBAL_UNMANAGED(ore_silo_default), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalore_silo_default()
    {
        return;
    }

    // tg: code/_globalvars/lists/silo.dm:2  GLOBAL_LIST_EMPTY(silo_access_logs)
    public DmList silo_access_logs;

    // tg: code/_globalvars/lists/silo.dm:2
    public virtual void InitGlobalsilo_access_logs()
    {
        silo_access_logs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "silo_access_logs");
    }
}
