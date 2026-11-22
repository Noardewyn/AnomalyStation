// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/energy/tesla.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the tesla bolts (the revolver's energy orb and the mech's tesla orb) zap around them as they fly
// and where they hit; the tesla cannon's hitscan bolt draws its arc from the shooter and shocks a living target. The var blocks
// are generated (D111).

// tg: code/modules/projectiles/projectile/energy/tesla.dm:1
public partial class obj_projectile_energy_tesla : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/energy/tesla.dm:10
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        await tesla_zap(source: this, zap_range: zap_range, power: power, cutoff: 1e3f, zap_flags: zap_flags);
        qdel(this);
        return dot;
    }

    // tg: code/modules/projectiles/projectile/energy/tesla.dm:15
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        //Many coders have given their blood for this speed
        Waiting.NoSleep(tesla_zap(source: this, zap_range: zap_range, power: power, cutoff: 1e3f, zap_flags: zap_flags));
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/energy/tesla.dm:28  (/obj/projectile/energy/tesla_cannon: the __2 of rules 2.3, beside
// /obj/projectile/energy/tesla/cannon)
public partial class obj_projectile_energy_tesla_cannon__2 : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/energy/tesla.dm:36
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        firer.Beam(target, icon_state: "tesla", time: 1, icon_state_variants: 24);

        if (isliving(target))
        {
            mob_living victim = target as mob_living;
            await victim.electrocute_act(shock_damage, this, siemens_coeff: 1, flags: Dm.bitor(SHOCK_NOSTUN, SHOCK_TESLA));
        }
        return dot;
    }
}
