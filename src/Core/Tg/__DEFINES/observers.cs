// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/observers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the notification flags are generated; the global list is here (rules 10.6).

///Assoc List of types of ghost lightings & the player-facing name.
// tg: code/__DEFINES/observers.dm:15
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/__DEFINES/observers.dm:15  GLOBAL_LIST_INIT(ghost_lightings, list(...))
    public DmList ghost_lightings;

    // tg: code/__DEFINES/observers.dm:15  (GLOBAL_MANAGED(ghost_lightings, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalghost_lightings()
    {
        ghost_lightings = Dm.list(
            Dm.kv("Normal", LIGHTING_CUTOFF_VISIBLE),
            Dm.kv("Darker", LIGHTING_CUTOFF_MEDIUM),
            Dm.kv("Night Vision", LIGHTING_CUTOFF_HIGH),
            Dm.kv("Fullbright", LIGHTING_CUTOFF_FULLBRIGHT)
        );
        Dm.append(ref gvars_datum_init_order, "ghost_lightings");
    }
}
