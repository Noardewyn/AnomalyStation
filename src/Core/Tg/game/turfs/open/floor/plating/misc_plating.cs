// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/plating/misc_plating.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The abductors' alien floor (Lavaland's crashed UFO). The var blocks are generated.

// tg: code/game/turfs/open/floor/plating/misc_plating.dm:18
public partial class turf_open_floor_plating_abductor : turf_open_floor_plating
{
    // tg: code/game/turfs/open/floor/plating/misc_plating.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("alienpod[]", Dm.rand(1, 9));
        return dot;
    }

    // tg: code/game/turfs/open/floor/plating/misc_plating.dm:28
    public override object break_tile() => FALSE;
}
