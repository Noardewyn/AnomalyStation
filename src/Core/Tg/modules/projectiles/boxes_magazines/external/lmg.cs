// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/external/lmg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the L6 SAW's box magazines). The var blocks are generated (D111).

// tg: code/modules/projectiles/boxes_magazines/external/lmg.dm:1
public partial class obj_item_ammo_box_magazine_m7mm : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/lmg.dm:43
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("a7mm-[]", Dm.min(round(ammo_count(), 10), 50)); //Min is used to prevent high capacity magazines from attempting to get sprites with larger capacities
        return dot;
    }
}
