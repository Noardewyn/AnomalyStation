// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/internal/_internal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C. The var blocks are generated (D111).

// tg: code/modules/projectiles/boxes_magazines/internal/_internal.dm:1
public partial class obj_item_ammo_box_magazine_internal : obj_item_ammo_box_magazine
{
    //internals magazines are accessible, so replace spent ammo if full when trying to put a live one in
    // tg: code/modules/projectiles/boxes_magazines/internal/_internal.dm:10
    public override object give_round(obj_item_ammo_casing R = null, float? replace_spent = null)
    {
        return base.give_round(R, 1);
    }
}
