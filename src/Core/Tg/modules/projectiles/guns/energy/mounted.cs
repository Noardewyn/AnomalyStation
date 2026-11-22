// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/mounted.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package P6, D298: the arm-mounted laser and taser implants' guns). The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/energy/mounted.dm:1
public partial class obj_item_gun_energy_e_gun_advtaser_mounted : obj_item_gun_energy_e_gun_advtaser
{
    // tg: code/modules/projectiles/guns/energy/mounted.dm:12
    public override async DmTask add_seclight_point()
    {
    }
}

// tg: code/modules/projectiles/guns/energy/mounted.dm:15
public partial class obj_item_gun_energy_laser_mounted : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/mounted.dm:25
    public override async DmTask add_deep_lore()
    {
    }
}
