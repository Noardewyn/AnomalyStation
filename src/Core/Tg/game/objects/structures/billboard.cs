// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/billboard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole: the billboards (three tiles wide) are live; their see-through fade is not added (DV499).
// The var blocks are generated (D111).

// tg: code/game/objects/structures/billboard.dm:1
public partial class obj_structure_billboard : obj_structure
{
    // tg: code/game/objects/structures/billboard.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // DEVIATION(DV499): AddComponent(/datum/component/seethrough, SEE_THROUGH_MAP_BILLBOARD) fades the billboard's big sprite for a
        // top-down viewer standing behind it; the first-person view sees the billboard from its own eye, so it is not added.
        Dm.Deferred("code/game/objects/structures/billboard.dm:16", "Initialize: AddComponent(/datum/component/seethrough), the billboard's fade for a top-down viewer behind it (DV499)");
        return dot;
    }
}
