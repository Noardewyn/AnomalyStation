// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/ion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice B: the ion bolt, whole. The var block is generated (D111).

// tg: code/modules/projectiles/projectile/special/ion.dm:1
public partial class obj_projectile_ion : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/ion.dm:10
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        await base.on_hit(target, blocked, pierce_hit);
        await empulse(target, emp_radius, emp_radius, emp_source: this);
        return BULLET_ACT_HIT;
    }
}
