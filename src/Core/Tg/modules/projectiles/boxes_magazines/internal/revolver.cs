// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/internal/revolver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the Russian revolver's cylinder (one live round among spent ones); the other cylinders are var blocks (D111).

// tg: code/modules/projectiles/boxes_magazines/internal/revolver.dm:13
public partial class obj_item_ammo_box_magazine_internal_cylinder_rus357 : obj_item_ammo_box_magazine_internal_cylinder
{
    // tg: code/modules/projectiles/boxes_magazines/internal/revolver.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        for (float i = 1; i <= max_ammo - 1; i++)
            stored_ammo.Add(Dm.@new<obj_item_ammo_casing_c357_spent>(this));
        stored_ammo.Add(Dm.@new<obj_item_ammo_casing_c357>(this));
        return dot;
    }
}
