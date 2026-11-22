// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/external/rifle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice D: the battle rifle's .38 magazine (rifle.dm:18-35); the other rifle magazines are generated var blocks, D111.

// tg: code/modules/projectiles/boxes_magazines/external/rifle.dm:31
public partial class obj_item_ammo_box_magazine_m38 : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/rifle.dm:31
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, ammo_count() != 0 ? "-ammo" : "");
        return dot;
    }
}
