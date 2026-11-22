// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/speedloaders.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the .38 speed loader's pictures (the other loaders are var blocks, D111).

// tg: code/modules/projectiles/boxes_magazines/speedloaders.dm:41
public partial class obj_item_ammo_box_speedloader_c38 : obj_item_ammo_box_speedloader
{
    // tg: code/modules/projectiles/boxes_magazines/speedloaders.dm:53
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-base", base_icon_state);
        return dot;
    }

    // tg: code/modules/projectiles/boxes_magazines/speedloaders.dm:57
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stored_ammo == null || stored_ammo.Count == 0)
            return dot;
        for (int inserted_ammo = 1; inserted_ammo <= stored_ammo.Count; inserted_ammo++)
        {
            object boolet = stored_ammo[(float)inserted_ammo]; // a casing or, lazy, its typepath (boolet::lead_or_laser reads both)
            object lead_or_laser = boolet is Type t ? Dm.initial(t, (obj_item_ammo_casing_c38 p) => p.lead_or_laser) : (boolet as obj_item_ammo_casing_c38)?.lead_or_laser;
            Dm.append(ref dot, Dm.text("38-[]-[]", lead_or_laser, inserted_ammo));
        }
        return dot;
    }
}
