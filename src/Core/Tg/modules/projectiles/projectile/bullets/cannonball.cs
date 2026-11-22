// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/cannonball.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate N2a, D316: the cannonballs, for the Bulldog's meteor slugs): the ballista spears (:90-170) come with the ballista. The
// var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/bullets/cannonball.dm:1
public partial class obj_projectile_bullet_cannonball : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/cannonball.dm:25
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        damage -= damage_decrease_on_hit;
        if (Dm.truthy(object_damage_decreases))
            object_damage -= Dm.min(Dm.num(damage), object_damage_decrease_on_hit);
        if (damage < stop_piercing_threshold)
            projectile_piercing = NONE; //so it finishes its rampage
        if (blocked == 100)
            return await base.on_hit(target, blocked, pierce_hit);
        if (isobj(target))
        {
            var hit_object = target as obj;
            await hit_object.take_damage(object_damage, BRUTE, BULLET, FALSE);
        }
        else if (isclosedturf(target))
        {
            damage -= Dm.max(Dm.num(damage) - 30, 10); //lose extra momentum from busting through a wall
            if (!isindestructiblewall(target))
            {
                var hit_turf = target as turf_closed;
                await hit_turf.ScrapeAway();
            }
        }
        return await base.on_hit(target, blocked, pierce_hit);
    }
}

// tg: code/modules/projectiles/projectile/bullets/cannonball.dm:43
public partial class obj_projectile_bullet_cannonball_explosive : obj_projectile_bullet_cannonball
{
    // tg: code/modules/projectiles/projectile/bullets/cannonball.dm:49
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        await explosion(target, devastation_range: 2, heavy_impact_range: 3, light_impact_range: 4, explosion_cause: this);
        return await base.on_hit(target, blocked, pierce_hit);
    }
}

// tg: code/modules/projectiles/projectile/bullets/cannonball.dm:53
public partial class obj_projectile_bullet_cannonball_emp : obj_projectile_bullet_cannonball
{
    // tg: code/modules/projectiles/projectile/bullets/cannonball.dm:59
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        await empulse(this, 4, 10, emp_source: this);
        return await base.on_hit(target, blocked, pierce_hit);
    }
}

// tg: code/modules/projectiles/projectile/bullets/cannonball.dm:63
public partial class obj_projectile_bullet_cannonball_biggest_one : obj_projectile_bullet_cannonball
{
    // tg: code/modules/projectiles/projectile/bullets/cannonball.dm:68
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        if (Dm.eq(projectile_piercing, NONE))
            await explosion(target, devastation_range: GLOB.MAX_EX_DEVESTATION_RANGE, heavy_impact_range: GLOB.MAX_EX_HEAVY_RANGE, light_impact_range: GLOB.MAX_EX_LIGHT_RANGE, flash_range: GLOB.MAX_EX_FLASH_RANGE, explosion_cause: this);
        return await base.on_hit(target, blocked, pierce_hit);
    }
}
