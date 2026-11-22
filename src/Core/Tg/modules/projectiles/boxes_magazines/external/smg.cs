// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/external/smg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M7x slice D: the WT-550's magazine, its picture stepping by four rounds; syndicate G4c: the Uzi's; N2a: the Saber's and
// the C-20r's). The var blocks are generated (D111).

// tg: code/modules/projectiles/boxes_magazines/external/smg.dm:1
public partial class obj_item_ammo_box_magazine_wt550m9 : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/smg.dm:13
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, round(ammo_count(), 4));
        return dot;
    }
}

// tg: code/modules/projectiles/boxes_magazines/external/smg.dm:41
public partial class obj_item_ammo_box_magazine_uzim9mm : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/smg.dm:50
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, round(ammo_count(), 4));
        return dot;
    }
}

// tg: code/modules/projectiles/boxes_magazines/external/smg.dm:54
public partial class obj_item_ammo_box_magazine_smgm9mm : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/smg.dm:63
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, Dm.length(stored_ammo) != 0 ? "full" : "empty");
        return dot;
    }
}

// tg: code/modules/projectiles/boxes_magazines/external/smg.dm:77
public partial class obj_item_ammo_box_magazine_smgm45 : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/smg.dm:88
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, round(ammo_count(), 2));
        return dot;
    }
}

