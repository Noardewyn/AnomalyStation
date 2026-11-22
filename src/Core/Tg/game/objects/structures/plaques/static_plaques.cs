// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/plaques/static_plaques.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the static plaques, engraved already: on a wall they mount on it, on an open floor away from a wall
// they lie on the floor plane; the atmospherics, Thunderdome, golden, captain's and the stations' commission plaques). The plastic and
// wooden plaques (/obj/structure/sign/plaques) are signs and were live already. The var blocks are generated (D111).

//These are static plaques, they are made of gold and start engraved with a title and description.

// tg: code/game/objects/structures/plaques/static_plaques.dm:3
public partial class obj_structure_plaque_static_plaque : obj_structure_plaque
{
    // tg: code/game/objects/structures/plaques/static_plaques.dm:6
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (isopenturf(loc) && !isProbablyWallMounted(this))
        {
            SET_PLANE_IMPLICIT(this, FLOOR_PLANE);
            layer = HIGH_TURF_LAYER;
        }
        return dot;
    }

    // tg: code/game/objects/structures/plaques/static_plaques.dm:12
    public override object get_mountable_objects()
    {
        return Dm.list();
    }

    // tg: code/game/objects/structures/plaques/static_plaques.dm:15
    public override async DmTask<object> find_and_mount_on_atom(float? mark_for_late_init = null, float? late_init = null)
    {
        if (isProbablyWallMounted(this))
            return await base.find_and_mount_on_atom(mark_for_late_init, late_init);
        return null;
    }
}
