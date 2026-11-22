// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/external/rechargable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the rechargeable power pack (the recharger's magazine branch needs it); the var block is generated (D111).

// tg: code/modules/projectiles/boxes_magazines/external/rechargable.dm:1
public partial class obj_item_ammo_box_magazine_recharge : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/rechargable.dm:10
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.text("[] It has [] shot\\s left.", Dm.initial(this, (obj_item p) => p.desc), stored_ammo.Count);
        return dot;
    }

    // tg: code/modules/projectiles/boxes_magazines/external/rechargable.dm:14
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, round(ammo_count(), 4));
        return dot;
    }

    // tg: code/modules/projectiles/boxes_magazines/external/rechargable.dm:18
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra) //No popping out the "bullets"
    {
        return null;
    }
}
