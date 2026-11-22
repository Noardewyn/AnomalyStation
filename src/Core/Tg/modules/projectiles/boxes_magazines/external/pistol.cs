// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/external/pistol.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the pistol magazines; the Stechkin's picture steps by five rounds (the other magazines are var blocks, D111).

// tg: code/modules/projectiles/boxes_magazines/external/pistol.dm:34
public partial class obj_item_ammo_box_magazine_m9mm_aps : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/pistol.dm:45
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, round(ammo_count(), 5));
        return dot;
    }
}
