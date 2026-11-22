// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/_incendiary.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the incendiary 7mm, .45 and 10mm rounds, and the flamethrower's and backblast's plumes). The var
// blocks are generated (D111).

// tg: code/modules/projectiles/projectile/bullets/_incendiary.dm:1
public partial class obj_projectile_bullet_incendiary : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/_incendiary.dm:9
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (iscarbon(target))
        {
            var M = target as mob_living_carbon;
            await M.adjust_fire_stacks(fire_stacks);
            M.ignite_mob();
        }
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/_incendiary.dm:16
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);

        if (!Dm.truthy(leaves_fire_trail))
            return dot;
        turf location = get_turf(this);
        if (location != null)
        {
            Dm.@new<obj_effect_hotspot>(location);
            location.hotspot_expose(700, 50, 1);
        }
        return dot;
    }
}

/// Incendiary bullet that more closely resembles a real flamethrower sorta deal, no visible bullet, just flames.
// tg: code/modules/projectiles/projectile/bullets/_incendiary.dm:27
public partial class obj_projectile_bullet_incendiary_fire : obj_projectile_bullet_incendiary
{
    // tg: code/modules/projectiles/projectile/bullets/_incendiary.dm:45
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        turf location = get_turf(target);
        if (isopenturf(location))
        {
            Dm.@new<obj_effect_hotspot>(location);
            location.hotspot_expose(700, 50, 1);
        }
        return dot;
    }
}

/// Used in [the backblast element][/datum/element/backblast]
// tg: code/modules/projectiles/projectile/bullets/_incendiary.dm:53
public partial class obj_projectile_bullet_incendiary_fire_backblast : obj_projectile_bullet_incendiary_fire
{
    /// we only try to knock back the first 6 items per tile
    // tg: code/modules/projectiles/projectile/bullets/_incendiary.dm:63  #define BACKBLAST_MAX_ITEM_KNOCKBACK 6
    private const int BACKBLAST_MAX_ITEM_KNOCKBACK = 6;

    // tg: code/modules/projectiles/projectile/bullets/_incendiary.dm:65
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        if (knockback_range <= 0)
            return dot;
        knockback_range--;
        turf current_turf = get_turf(this);
        if (current_turf == null)
            return dot;
        var throw_at_turf = get_turf_in_angle(Dm.num(angle), current_turf, 7) as turf;
        int thrown_items = 0;

        foreach (object iter in Dm.each(current_turf.contents))
        {
            if (thrown_items > BACKBLAST_MAX_ITEM_KNOCKBACK)
                break;
            if (isitem(iter))
            {
                var iter_item = iter as obj_item;
                if (Dm.truthy(iter_item.anchored) || Dm.truthy(LAZYFIND(launched_items, iter_item)) || iter_item.throwing != null)
                    continue;
                thrown_items++;
                await iter_item.throw_at(throw_at_turf, knockback_range, knockback_range);
                LAZYADD(ref launched_items, iter_item);
            }
            else if (isliving(iter))
            {
                var incineratee = iter as mob_living;
                await incineratee.take_bodypart_damage(0, damage, check_armor: TRUE, wound_bonus: wound_bonus, exposed_wound_bonus: exposed_wound_bonus);
                await incineratee.adjust_fire_stacks(fire_stacks);
            }
        }
        return dot;
    }
}
