// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/adventure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOBAL_LIST_INIT(exodrone_tool_metadata) translated; the file's #defines are generated (Defines).

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/adventure.dm:46  GLOBAL_LIST_INIT(exodrone_tool_metadata, list(...))
    public DmList exodrone_tool_metadata;

    // tg: code/__DEFINES/adventure.dm:46  (GLOBAL_MANAGED(exodrone_tool_metadata, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexodrone_tool_metadata()
    {
        exodrone_tool_metadata = Dm.list(
            Dm.kv(EXODRONE_TOOL_WELDER, Dm.list(Dm.kv("description", "A heavy duty welder."), Dm.kv("icon", "burn"))),
            Dm.kv(EXODRONE_TOOL_TRANSLATOR, Dm.list(Dm.kv("description", "Powerful translation and data recording software."), Dm.kv("icon", "language"))),
            Dm.kv(EXODRONE_TOOL_LASER, Dm.list(Dm.kv("description", "Multipurpose tool suitable for combat and precision cutting."), Dm.kv("icon", "bolt"))),
            Dm.kv(EXODRONE_TOOL_MULTITOOL, Dm.list(Dm.kv("description", "Multipurpose tool for electronics manipulation. Comes with suite of radiation and radiowave sensors."), Dm.kv("icon", "broadcast-tower"))),
            Dm.kv(EXODRONE_TOOL_DRILL, Dm.list(Dm.kv("description", "Heavy duty drill useful for mining."), Dm.kv("icon", "screwdriver"))));
        Dm.append(ref gvars_datum_init_order, "exodrone_tool_metadata");
    }
}
