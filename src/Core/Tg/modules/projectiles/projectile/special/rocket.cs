// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/rocket.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the rocket launcher's HE and HEAP rockets, the exosuits' missiles and the gyrojet's bolt). The var
// blocks are generated (D111).

// tg: code/modules/projectiles/projectile/special/rocket.dm:1
public partial class obj_projectile_bullet_gyro : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/special/rocket.dm:8
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        await base.on_hit(target, blocked, pierce_hit);
        await explosion(target, devastation_range: -1, light_impact_range: 2, explosion_cause: this);
        return BULLET_ACT_HIT;
    }
}

/// PM9 standard HE rocket
// tg: code/modules/projectiles/projectile/special/rocket.dm:14
public partial class obj_projectile_bullet_rocket : obj_projectile_bullet
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        speed *= obj_projectile_bullet_a40mm.ShellSpeedShare; // DEVIATION(DV579): a launcher's shell flies half tg's speed
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/rocket.dm:28
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        bool random_crit_gib = false;
        if (isliving(target) && prob(1) && Dm.truthy(random_crits_enabled))
        {
            var gibbed_dude = target as mob_living;
            if (!IS_UNCONSCIOUS_OR_CRIT(gibbed_dude))
            {
                await gibbed_dude.say("Is that a fucking ro-", forced: "hit by rocket");
                random_crit_gib = true;
            }
        }
        await base.on_hit(target, blocked, pierce_hit);

        await do_boom(target, random_crit_gib ? TRUE : FALSE);
        if (Dm.truthy(anti_armour_damage) && ismecha(target))
        {
            var M = target as obj_vehicle_sealed_mecha;
            await M.take_damage(anti_armour_damage);
        }
        if (issilicon(target))
        {
            var S = target as mob_living_silicon;
            await S.take_overall_damage(Dm.num(anti_armour_damage) * 0.75f, Dm.num(anti_armour_damage) * 0.25f);
        }
        return BULLET_ACT_HIT;
    }

    /** This proc allows us to customize the conditions necesary for the rocket to detonate, allowing for different explosions for living targets, turf targets,
    among other potential differences. This granularity is helpful for things like the special rockets mechs use. */
    // tg: code/modules/projectiles/projectile/special/rocket.dm:48
    public virtual async DmTask do_boom(atom target = null, float? random_crit_gib = null)
    {
        random_crit_gib ??= FALSE;
        if (!isliving(target)) //if the target isn't alive, so is a wall or something
            await explosion(target, heavy_impact_range: 1, light_impact_range: 2, flame_range: 3, flash_range: 4, explosion_cause: this);
        else
        {
            await explosion(target, light_impact_range: 2, flame_range: 3, flash_range: 4, explosion_cause: this);
            if (Dm.truthy(random_crit_gib))
            {
                var gibbed_dude = target as mob_living;
                Dm.@new<obj_effect_temp_visual_crit>(get_turf(gibbed_dude));
                await gibbed_dude.gib(DROP_ALL_REMAINS);
            }
        }
    }
}

/// PM9 HEAP rocket - the anti-anything missile you always craved.
// tg: code/modules/projectiles/projectile/special/rocket.dm:59
public partial class obj_projectile_bullet_rocket_heap : obj_projectile_bullet_rocket
{
    // tg: code/modules/projectiles/projectile/special/rocket.dm:68
    public override async DmTask do_boom(atom target = null, float? random_crit_gib = null)
    {
        await explosion(target, devastation_range: -1, heavy_impact_range: 1, light_impact_range: 3, flame_range: 4, flash_range: 1, adminlog: FALSE);
    }
}

/// PM9 weak rocket - just kind of a failure
// tg: code/modules/projectiles/projectile/special/rocket.dm:72
public partial class obj_projectile_bullet_rocket_weak : obj_projectile_bullet_rocket
{
    // tg: code/modules/projectiles/projectile/special/rocket.dm:78
    public override async DmTask do_boom(atom target = null, float? random_crit_gib = null)
    {
        if (!isliving(target)) //if the target isn't alive, so is a wall or something
            await explosion(target, heavy_impact_range: 1, light_impact_range: 2, flame_range: 3, flash_range: 4, explosion_cause: this);
        else
            await explosion(target, light_impact_range: 2, flame_range: 3, flash_range: 4, explosion_cause: this);
    }
}

/** PEP-6 Missile - Used by the PEP-6 Exosuit missile rack.
* Explodes only when it hits specifically one of the following types:
* (/obj/structure), (/obj/machinery), (/obj/vehicle), (/turf/closed), (/mob/living/silicon)
*/
// tg: code/modules/projectiles/projectile/special/rocket.dm:102
public partial class obj_projectile_bullet_rocket_pep : obj_projectile_bullet_rocket
{
    // tg: code/modules/projectiles/projectile/special/rocket.dm:110
    public override async DmTask do_boom(atom target = null, float? random_crit_gib = null)
    {
        if (issilicon(target)) //if the target is a borg, just give them one of these to make it loud, most of the damage is in the projectile itself
        {
            await explosion(target, light_impact_range: 1, flash_range: 2, explosion_cause: this);
            return;
        }
        if (isstructure(target) || isvehicle(target) || isclosedturf(target) || ismachinery(target)) //if the target is a structure, machine, vehicle or closed turf like a wall, explode that shit
        {
            if (Dm.truthy(target.density)) //Dense objects get blown up a bit harder
            {
                await explosion(target, heavy_impact_range: 1, light_impact_range: 1, flash_range: 2, explosion_cause: this);
                return;
            }
            await explosion(target, light_impact_range: 1, flash_range: 2, explosion_cause: this);
            return;
        }
        //if the target is anything else, we drop a missile on the ground and do nothing
        Dm.@new<obj_item_broken_missile>(get_turf(this), 1);
    }
}

//immediately hits firer
// tg: code/modules/projectiles/projectile/special/rocket.dm:131
public partial class obj_projectile_bullet_rocket_reverse : obj_projectile_bullet_rocket
{
    // tg: code/modules/projectiles/projectile/special/rocket.dm:135
    public override async DmTask fire(object fire_angle = null, atom direct_target = null)
    {
        await base.fire(fire_angle, direct_target);
        if (firer != null) //troll
        {
            firer.visible_message(span_danger(I18n.T("[] blows up as soon as [] fires it!", this, firer)));
            await on_hit(firer);
        }
    }
}
