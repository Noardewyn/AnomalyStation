// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/energy/gravity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the gravity gun's casings remember the gun they sit in, whose power their bolts read. The
// repulse, attract and chaos casings declare no procs. The var blocks are generated (D111).

// tg: code/modules/projectiles/ammunition/energy/gravity.dm:1
public partial class obj_item_ammo_casing_energy_gravity : obj_item_ammo_casing_energy
{
    // tg: code/modules/projectiles/ammunition/energy/gravity.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (istype(loc, typeof(obj_item_gun_energy_gravity_gun)))
            gun = loc as obj_item_gun_energy_gravity_gun;
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/projectiles/ammunition/energy/gravity.dm:15
    public override object Destroy(float? force = null)
    {
        gun = null;
        return base.Destroy(force);
    }
}
