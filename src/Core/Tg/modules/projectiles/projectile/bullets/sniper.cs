// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/sniper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the anti-materiel rifle's .50 rounds and the Lahti's 20mm). The var blocks are generated (D111).

// .50 BMG (Sniper)
// tg: code/modules/projectiles/projectile/bullets/sniper.dm:3
public partial class obj_projectile_bullet_p50 : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/sniper.dm:18
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        if (isobj(target) && blocked != 100)
        {
            var thing_to_break = target as obj;
            float damage_to_deal = object_damage;
            if (ismecha(thing_to_break) && Dm.truthy(mecha_damage))
                damage_to_deal += mecha_damage;
            if (damage_to_deal != 0)
                await thing_to_break.take_damage(damage_to_deal, BRUTE, BULLET, FALSE);
        }
        return await base.on_hit(target, blocked, pierce_hit);
    }
}

// tg: code/modules/projectiles/projectile/bullets/sniper.dm:35
public partial class obj_projectile_bullet_p50_disruptor : obj_projectile_bullet_p50
{
    // tg: code/modules/projectiles/projectile/bullets/sniper.dm:45
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (blocked != 100 && isliving(target))
        {
            var living_guy = target as mob_living;
            await living_guy.Sleeping(40 * SECONDS); //Yes, its really 40 seconds of sleep, I hope you had your morning coffee.
        }
        if (issilicon(target)) //also especially good against borgs
        {
            var borg_boy = target as mob_living_silicon;
            await borg_boy.apply_damage(damage, BRUTE);
        }
        await empulse(target, emp_radius, emp_radius, emp_source: this);
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/bullets/sniper.dm:55
public partial class obj_projectile_bullet_p50_incendiary : obj_projectile_bullet_p50
{
    // tg: code/modules/projectiles/projectile/bullets/sniper.dm:65
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (iscarbon(target))
        {
            var poor_burning_dork = target as mob_living_carbon;
            await poor_burning_dork.adjust_fire_stacks(20);
            poor_burning_dork.ignite_mob();
        }
        foreach (turf nearby_turf in Dm.each<turf>(RANGE_TURFS(2, target)))
            Dm.@new<obj_effect_hotspot>(nearby_turf);
        return dot;
    }
}

// 20x138mm bullet (lahti-l39) //
// tg: code/modules/projectiles/projectile/bullets/sniper.dm:124
public partial class obj_projectile_bullet_mm20x138 : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/sniper.dm:138
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        if (isobj(target) && blocked != 100)
        {
            var thing_to_break = target as obj;
            float damage_to_deal = Dm.num(object_damage);
            if (ismecha(thing_to_break) && Dm.truthy(mecha_damage))
                damage_to_deal += Dm.num(mecha_damage);
            if (damage_to_deal != 0)
                await thing_to_break.take_damage(damage_to_deal, BRUTE, BULLET, FALSE);
        }
        return await base.on_hit(target, blocked, pierce_hit);
    }
}
