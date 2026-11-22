// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/_firing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice B: the casing's firing chain (fire_casing -> ready_proj -> throw_proj -> the projectile's aim_projectile and fire), whole but
// for `spread` (docs/ai/specs/not-translated.md section 47); M7x slice D: the pellet cloud of a casing with pellets > 1.

// tg: code/modules/projectiles/ammunition/_firing.dm:1 (interleaved with _ammunition.dm, rules 1.9)
public partial class obj_item_ammo_casing : obj_item
{
    // tg: code/modules/projectiles/ammunition/_firing.dm:1
    public virtual async DmTask<object> fire_casing(atom target = null, mob_living user = null, object @params = null, object distro = null, object quiet = null, object zone_override = null, object spread = null, atom fired_from = null)
    {
        distro = Dm.num(distro) + variance;
        turf targloc = get_turf(target);
        await ready_proj(target, user, quiet, zone_override as string, fired_from);
        obj_projectile thrown_proj = null;
        if (pellets == 1)
        {
            if (Dm.truthy(distro)) //We have to spread a pixel-precision bullet. throw_proj was called before so angles should exist by now...
            {
                if (Dm.truthy(randomspread))
                    spread = round((rand() - 0.5f) * Dm.num(distro));
                else //Smart spread
                    spread = round(1 - 0.5f) * Dm.num(distro);
            }
            thrown_proj = await throw_proj(target, targloc, user, @params, spread, fired_from) as obj_projectile;
            if (thrown_proj == null)
                return FALSE;
        }
        else
        {
            if (loaded_projectile == null)
                return FALSE;
            await AddComponent(typeof(datum_component_pellet_cloud), projectile_type, pellets);
        }

        float next_delay = Dm.truthy(click_cooldown_override) ? click_cooldown_override : CLICK_CD_RANGE;
        if (HAS_TRAIT(user, TRAIT_DOUBLE_TAP))
            next_delay = round(next_delay * 0.5f);
        user.changeNext_move(next_delay);

        if (!Dm.truthy(tk_firing(user, fired_from)))
        {
            await user.newtonian_move(get_angle(target, user), drift_force: newtonian_force);
        }
        else if (ismovable(fired_from))
        {
            atom_movable firer = fired_from as atom_movable;
            if (!Dm.truthy(await firer.newtonian_move(get_angle(target, fired_from), instant: TRUE, drift_force: newtonian_force)))
            {
                object throwtarget = get_step(fired_from, Dm.num(get_dir(target, fired_from)));
                await firer.safe_throw_at(throwtarget as atom, 1, 2);
            }
        }
        update_appearance();
        shot_timestamp = world.time;
        if (Dm.truthy(_listen_lookup?[COMSIG_FIRE_CASING]))
            _SendSignal(COMSIG_FIRE_CASING, Dm.list(this, target, user, fired_from, randomspread, spread, zone_override, @params, distro, thrown_proj));

        return TRUE;
    }

    // tg: code/modules/projectiles/ammunition/_firing.dm:38
    public virtual object tk_firing(mob_living user = null, atom fired_from = null)
    {
        return (fired_from != user && !Dm.truthy(user.contains(fired_from))) ? TRUE : FALSE;
    }

    // tg: code/modules/projectiles/ammunition/_firing.dm:41
    public virtual async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        zone_override ??= "";
        if (loaded_projectile == null)
            return;
        loaded_projectile.original = target;
        loaded_projectile.firer = user;
        loaded_projectile.fired_from = fired_from;
        loaded_projectile.hit_prone_targets = Dm.num(user.combat_mode);
        if (Dm.truthy(zone_override))
            loaded_projectile.def_zone = zone_override;
        else
            loaded_projectile.def_zone = user.zone_selected;
        loaded_projectile.suppressed = Dm.num(quiet);

        if (isgun(fired_from))
        {
            obj_item_gun gun = fired_from as obj_item_gun;

            float integrity_mult = 0.5f + Dm.num(gun.get_integrity_percentage()) * 0.5f;
            if (integrity_mult >= 0.95f) //Guns that are only mildly smudged don't debuff projectiles.
                integrity_mult = 1;

            loaded_projectile.damage *= gun.projectile_damage_multiplier * integrity_mult;
            loaded_projectile.stamina *= gun.projectile_damage_multiplier * integrity_mult;

            loaded_projectile.speed *= gun.projectile_speed_multiplier * integrity_mult;

            loaded_projectile.wound_bonus += gun.projectile_wound_bonus;
            loaded_projectile.wound_bonus *= loaded_projectile.wound_bonus >= 0 ? 1 : 2 - integrity_mult;
            loaded_projectile.exposed_wound_bonus += gun.projectile_wound_bonus;
            loaded_projectile.exposed_wound_bonus *= loaded_projectile.exposed_wound_bonus >= 0 ? 1 : 2 - integrity_mult;
        }

        if (Dm.truthy(tk_firing(user, fired_from)))
            loaded_projectile.ignore_source_check = TRUE;

        if (reagents != null && loaded_projectile.reagents != null)
        {
            await (reagents as datum_reagents).trans_to(loaded_projectile, (reagents as datum_reagents).total_volume, transferred_by: user); //For chemical darts/bullets
            qdel(reagents);
        }
        if (Dm.truthy(_listen_lookup?[COMSIG_CASING_READY_PROJECTILE]))
            _SendSignal(COMSIG_CASING_READY_PROJECTILE, Dm.list(this, target, user, quiet, zone_override, fired_from));
    }

    // tg: code/modules/projectiles/ammunition/_firing.dm:79
    public virtual async DmTask<object> throw_proj(atom target = null, turf targloc = null, mob_living user = null, object @params = null, object spread = null, atom fired_from = null)
    {
        turf curloc = get_turf(fired_from);
        if (targloc == null || curloc == null || loaded_projectile == null)
            return null;

        object firing_dir = null;
        if (loaded_projectile.firer != null)
            firing_dir = get_dir(fired_from, target);
        if (!Dm.truthy(loaded_projectile.suppressed) && firing_effect_type != null && !Dm.truthy(tk_firing(user, fired_from)))
            Dm.@new(firing_effect_type, Dm.truthy(user) ? user : get_turf(this), firing_dir);

        atom direct_target = null;
        // DEVIATION(DV413): an aimed shot at a thing or a turf in reach flies its line (tg's point-blank shot hits the clicked atom at once,
        // which for the ceiling over the shooter is the floor it stands on); a mob in reach is still hit point blank
        bool flies = !ismob(target) && ClientSeam.HasAim(params2list(@params) as DmList);
        if (target != null && !flies && Dm.truthy(await curloc.Adjacent(targloc, target: targloc, mover: this))) //if the target is right on our location or adjacent (including diagonally if reachable) we'll skip the travelling code in the proj's fire()
            direct_target = target;
        await loaded_projectile.aim_projectile(target, Dm.truthy(tk_firing(user, fired_from)) ? fired_from : user, params2list(@params), Dm.num(spread));
        obj_projectile loaded_projectile_cache = loaded_projectile;
        loaded_projectile = null;
        await loaded_projectile_cache.fire(null, direct_target);
        return loaded_projectile_cache;
    }

    // tg: code/modules/projectiles/ammunition/_firing.dm:99
    public virtual object spread(turf target = null, turf current = null, object distro = null)
    {
        Dm.NotTranslated("code/modules/projectiles/ammunition/_firing.dm:99", "spread: gaussian() (code/__DEFINES/maths.dm:170: its static second sample is per-world state); no station gun calls it");
        return default;
    }
}
