// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/external/sniper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the anti-materiel rifle's .50 magazines). The var blocks are generated (D111).

// tg: code/modules/projectiles/boxes_magazines/external/sniper.dm:1
public partial class obj_item_ammo_box_magazine_sniper_rounds : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/sniper.dm:10
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, ammo_count() != 0 ? "-ammo" : "");
        return dot;
    }
}
