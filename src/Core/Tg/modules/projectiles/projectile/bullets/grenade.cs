// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/grenade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the 40mm grenades of the M-90gl's underbarrel launcher and the china lake). The var blocks are
// generated (D111).

// 40mm (Grenade Launcher
// tg: code/modules/projectiles/projectile/bullets/grenade.dm:3
public partial class obj_projectile_bullet_a40mm : obj_projectile_bullet
{
    /// <summary>DEVIATION(DV579, the owner 2026-11-21): a launcher's shell flies this share of tg's speed, so it is seen flying.</summary>
    public const float ShellSpeedShare = 0.5f;

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        speed *= ShellSpeedShare; // DEVIATION(DV579): half tg's 1.25 tiles a tick
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/grenade.dm:17
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        await base.on_hit(target, blocked, pierce_hit);
        if (Dm.truthy(ex_light_range) || Dm.truthy(ex_flame_range))
            await explosion(target, devastation_range: Dm.num(ex_dev_range), light_impact_range: Dm.num(ex_light_range), flame_range: ex_flame_range, flash_range: 1, adminlog: FALSE, explosion_cause: this);
        await grenade_extra_effect(target);
        return BULLET_ACT_HIT;
    }

    // tg: code/modules/projectiles/projectile/bullets/grenade.dm:24
    public virtual async DmTask grenade_extra_effect(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        if (!prob(1))
            return;

        if (isliving(target))
        {
            var living_target = target as mob_living;
            playsound(living_target, "sound/effects/coin2.ogg", 40, TRUE);
            Dm.@new<obj_effect_temp_visual_crit>(get_turf(living_target));
        }
    }
}

// tg: code/modules/projectiles/projectile/bullets/grenade.dm:33
public partial class obj_projectile_bullet_a40mm_incendiary : obj_projectile_bullet_a40mm
{
    // tg: code/modules/projectiles/projectile/bullets/grenade.dm:40
    public override async DmTask grenade_extra_effect(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        if (iscarbon(target))
        {
            var i_want_them_to_burn = target as mob_living_carbon;
            await i_want_them_to_burn.adjust_fire_stacks(damage * 2);
            i_want_them_to_burn.ignite_mob();
        }
        foreach (turf nearby_turf in Dm.each<turf>(RANGE_TURFS(ex_flame_range, target)))
            Dm.@new<obj_effect_hotspot>(nearby_turf);
    }
}

// tg: code/modules/projectiles/projectile/bullets/grenade.dm:48
public partial class obj_projectile_bullet_a40mm_tear_gas : obj_projectile_bullet_a40mm
{
    // tg: code/modules/projectiles/projectile/bullets/grenade.dm:55
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_projectile_drop), typeof(obj_item_grenade_chem_grenade_teargas_instant)));
        return dot;
    }
}
