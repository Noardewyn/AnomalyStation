// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/ai/ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (silicons SI8, D326): the radial menu's eight directions (the malf AI's remote vendor tilting); the defines are generated.

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/ai/ai.dm:100  GLOBAL_LIST_INIT(all_radial_directions, list(...))
    public DmList all_radial_directions;

    // tg: code/__DEFINES/ai/ai.dm:100  (GLOBAL_MANAGED(all_radial_directions, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_radial_directions()
    {
        image Arrow(float dir) => image(icon: "icons/testing/turf_analysis.dmi", icon_state: "red_arrow", dir: dir);
        all_radial_directions = Dm.list(
            Dm.kv("NORTH", Arrow(NORTH)),
            Dm.kv("NORTHEAST", Arrow(NORTHEAST)),
            Dm.kv("EAST", Arrow(EAST)),
            Dm.kv("SOUTHEAST", Arrow(SOUTHEAST)),
            Dm.kv("SOUTH", Arrow(SOUTH)),
            Dm.kv("SOUTHWEST", Arrow(SOUTHWEST)),
            Dm.kv("WEST", Arrow(WEST)),
            Dm.kv("NORTHWEST", Arrow(NORTHWEST)));
        Dm.append(ref gvars_datum_init_order, "all_radial_directions");
    }
}
