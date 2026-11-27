// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/lavalandruin_code/surface.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the dead god's wreck. The papers' and the wreck's var blocks are generated.

//ratvar

// tg: code/modules/mapfluff/ruins/lavalandruin_code/surface.dm:10
public partial class obj_structure_dead_ratvar : obj_structure
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/surface.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/seethrough, SEE_THROUGH_MAP_RATVAR_WRECK): DV499, a 2D view's fade of what stands behind a
        // large sprite, which the first-person view does not need
        return dot;
    }
}
