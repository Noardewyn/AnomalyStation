// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/energy/radiation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps GU1 (D333): the file whole: the radiation beam (toxin damage and a chance to irradiate a human it hits) and its weak variant (a
// var block). The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/energy/radiation.dm:1
public partial class obj_projectile_energy_radiation : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/energy/radiation.dm:11
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (ishuman(target) && prob(radiation_chance))
            radiation_pulse(target, max_range: 0, threshold: RAD_FULL_INSULATION);
        return dot;
    }
}
