// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/ammo_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the rocket bouquet; the other ammo boxes are var blocks). The var blocks are generated (D111).

// tg: code/modules/projectiles/boxes_magazines/ammo_boxes.dm:42
public partial class obj_item_ammo_box_rocket : obj_item_ammo_box
{
    // tg: code/modules/projectiles/boxes_magazines/ammo_boxes.dm:49
    public override object can_load(mob user = null)
    {
        return FALSE;
    }
}
