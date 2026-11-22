// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/special.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the honkmother's spinning banana (it slips whoever it passes, unless magic stops it), the mime's
// finger gun bullet (it silences), the marksman revolver's hitscan nanoshot and its coin: a shot that passes a coin near its target
// tile splits into two shots of twice the damage that seek the next coin, then a living mob, then a fuel tank, grenade or window,
// the coin's landing spot shown to its flipper as a crosshair. The var blocks are generated (D111).

// Honker

// tg: code/modules/projectiles/projectile/bullets/special.dm:3
public partial class obj_projectile_bullet_honker : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/special.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        SpinAnimation();
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/special.dm:19
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        mob M = target as mob;
        if (M is mob)
        {
            if (Dm.truthy(M.can_block_magic()))
                return BULLET_ACT_BLOCK;
            else
                await M.slip(100, M.loc, Dm.bitor(GALOSHES_DONT_HELP, SLIDE));
        }
        return dot;
    }
}

// Mime

// tg: code/modules/projectiles/projectile/bullets/special.dm:30
public partial class obj_projectile_bullet_mime : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/special.dm:33
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!isliving(target))
            return dot;

        mob_living living_target = target as mob_living;
        await living_target.set_silence_if_lower(20 * SECONDS);
        return dot;
    }
}

// Marksman Revolver + Ricochet Coin

/// Marksman Shot
// tg: code/modules/projectiles/projectile/bullets/special.dm:45
public partial class obj_projectile_bullet_marksman : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/special.dm:56
    [DmParams("mapload", "casing", "incoming_ricoshot_level")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object incoming_ricoshot_level = Dm.arg(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        if (isnum(incoming_ricoshot_level))
            ricoshot_level = Dm.num(incoming_ricoshot_level);

        float sw__1 = ricoshot_level;
        if (sw__1 == 0)
            tracer_type = typeof(obj_effect_projectile_tracer_solar_thinnest);
        else if (sw__1 == 1)
            tracer_type = typeof(obj_effect_projectile_tracer_solar_thin);
        else if (sw__1 >= 2 && sw__1 <= INFINITY)
            tracer_type = typeof(obj_effect_projectile_tracer_solar);
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/special.dm:69
    public override async DmTask scan_moved_turf()
    {
        turf cur_turf = get_turf(this); // check to see if we're passing over a turf with a coin on it
        obj_projectile_bullet_coin coin_check = cur_turf != null ? Dm.locate<obj_projectile_bullet_coin>(cur_turf.contents) : null;

        if (coin_check == null || (ricoshot_level == 0 && get_dist(coin_check.target_turf, coin_check) >= 1) || Dm.truthy(coin_check.used)) // no coin, keep trucking
        {
            await base.scan_moved_turf();
            return;
        }

        await coin_check.check_splitshot(firer as mob_living, this);
        await impact(coin_check);
    }
}

/// Marksman Coin
// tg: code/modules/projectiles/projectile/bullets/special.dm:80
public partial class obj_projectile_bullet_coin : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/special.dm:100
    [DmParams("mapload", "the_target", "original_firer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        turf the_target = Dm.arg(__extra, 0) as turf;
        mob original_firer = Dm.arg(__extra, 1) as mob;
        this.original_firer = original_firer;
        target_turf = the_target;
        range = (get_dist(original_firer, target_turf) + 3) * 3; // 3 tiles past the origin (the *3 is because reduce_range() ticks 3 times a tile because of the slower speed)

        object dot = base.Initialize(mapload, __extra);

        if (!(original_firer is mob) || original_firer.client == null)
            return dot;

        client firing_client = original_firer.client;
        crosshair_indicator = image(Dm.resource("icons/obj/supplypods_32x32.dmi"), target_turf, "LZ");
        Dm.append(firing_client.images, crosshair_indicator);
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/special.dm:114
    public override object Destroy(float? force = null)
    {
        remove_crosshair_indicator();
        return base.Destroy(force);
    }

    // the coin must be on the target turf to be directly targetable
    // tg: code/modules/projectiles/projectile/bullets/special.dm:119
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!Dm.truthy(valid) && get_dist(loc, target_turf) < 1)
        {
            original_firer?.playsound_local(get_turf(this), Dm.resource("sound/machines/ping.ogg"), 30); // the coin's turf: playsound_local's turf_source
            valid = TRUE;
        }
        else if (Dm.truthy(valid) && get_dist(loc, target_turf) > 1)
        {
            valid = FALSE;
            remove_crosshair_indicator();
        }
        return dot;
    }

    /// Remove the crosshair indicator from the original firer if it exists
    // tg: code/modules/projectiles/projectile/bullets/special.dm:129
    public virtual void remove_crosshair_indicator()
    {
        if (original_firer?.client != null && crosshair_indicator != null)
            Dm.remove(original_firer.client.images, crosshair_indicator);
        QDEL_NULL(ref crosshair_indicator);
    }

    /// We've been shot by a marksman revolver shot, or the ricochet off another coin, check if we can actually ricochet. The forced var being TRUE means it's a ricochet from another coin
    // tg: code/modules/projectiles/projectile/bullets/special.dm:135
    public virtual async DmTask<object> check_splitshot(mob_living shooter = null, obj_projectile_bullet_marksman incoming_shot = null, float? forced = null)
    {
        forced ??= FALSE;
        if (!Dm.truthy(forced) && get_dist(this, target_turf) > 1)
            return FALSE;

        used = TRUE;
        turf cur_tur = get_turf(this);
        cur_tur.visible_message(span_nicegreen(I18n.T("[] impacts [] and splits!", incoming_shot, this)));
        await iterate_splitshots(shooter, incoming_shot);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 0.25f * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 0.25f * SECONDS, file: "code/modules/projectiles/projectile/bullets/special.dm", line: 143); // QDEL_IN(src, 0.25 SECONDS), inline (rules 11.1 (f)); may not be needed
        return null;
    }

    /// Now we actually create all the splitshots, loop through however many splits we'll create and fire them
    // tg: code/modules/projectiles/projectile/bullets/special.dm:146
    public virtual async DmTask iterate_splitshots(mob_living shooter = null, obj_projectile incoming_shot = null)
    {
        for (float i = 1; i <= num_of_splitshots; i++)
            await fire_splitshot(incoming_shot as obj_projectile_bullet_marksman);
    }

    /// Shoot an individual splitshot at a new target
    // tg: code/modules/projectiles/projectile/bullets/special.dm:151
    public virtual async DmTask fire_splitshot(obj_projectile_bullet_marksman incoming_shot = null)
    {
        atom next_target = find_next_target() as atom;

        ADD_TRAIT(next_target, TRAIT_RECENTLY_COINED, Dm.text("[]", type));
        _addtimer(TRAIT_CALLBACK_REMOVE(next_target, TRAIT_RECENTLY_COINED, Dm.text("[]", type)), 0.5f * SECONDS, file: "code/modules/projectiles/projectile/bullets/special.dm", line: 155);
        float outgoing_ricoshot_level = incoming_shot.ricoshot_level + 1;
        obj_projectile_bullet_marksman new_splitshot = Dm.@new<obj_projectile_bullet_marksman>(get_turf(this), null, outgoing_ricoshot_level);
        //Shooting Code:
        new_splitshot.original = next_target;
        new_splitshot.fired_from = incoming_shot.fired_from;
        new_splitshot.firer = incoming_shot.firer;
        new_splitshot.damage = 2 * Dm.num(incoming_shot.damage);

        turf current_turf = get_turf(this);
        turf target_turf = get_turf(next_target);

        if (Dm.truthy(await Adjacent(current_turf, target_turf)))
            await new_splitshot.fire(get_angle(current_turf, target_turf), direct_target: next_target);
        else
        {
            await new_splitshot.aim_projectile(next_target, get_turf(this));
            await new_splitshot.fire();
        }

        if (istype(next_target, typeof(obj_projectile_bullet_coin))) // handle further splitshot checks
        {
            obj_projectile_bullet_coin our_coin = next_target as obj_projectile_bullet_coin;
            await our_coin.check_splitshot(incoming_shot.firer as mob_living, new_splitshot, forced: TRUE);
        }
    }

    /// Find what the splitshots will want to target next, with the order roughly based off the UK coin
    // tg: code/modules/projectiles/projectile/bullets/special.dm:178
    public virtual object find_next_target()
    {
        DmList valid_targets = shuffle(oview(4, loc)) as DmList;
        Dm.remove(ref valid_targets, firer);

        foreach (obj_projectile_bullet_coin iter_coin in Dm.each<obj_projectile_bullet_coin>(valid_targets))
        {
            if (!Dm.truthy(iter_coin.used)) // this will prevent shooting itself as well
                return iter_coin;
        }

        DmList possible_victims = Dm.list();

        foreach (mob_living iter_living in Dm.each<mob_living>(valid_targets))
        {
            if (HAS_TRAIT(iter_living, TRAIT_RECENTLY_COINED) || IS_UNCONSCIOUS_OR_CRIT(iter_living))
                continue;

            if (get_dist(this, iter_living) <= 2) // prioritize close mobs
                return iter_living;
            Dm.append(ref possible_victims, iter_living);
        }

        if (possible_victims.Count > 0)
            return pick(possible_victims);

        // var/static/list/prioritized_targets (a proc-level static, rules 10.6)
        DmList prioritized_targets = TgState.Current.obj_projectile_bullet_coin__find_next_target__prioritized_targets;
        foreach (object iter_type in Dm.each(prioritized_targets))
        {
            for (float already_coined_tries = 1; already_coined_tries <= 3; already_coined_tries++)
            {
                atom iter_type_check = Dm.locate_type(iter_type as Type, valid_targets) as atom;
                if (iter_type_check != null)
                {
                    if (HAS_TRAIT(iter_type_check, TRAIT_RECENTLY_COINED))
                    {
                        Dm.remove(ref valid_targets, iter_type_check);
                        continue;
                    }
                    else
                        return iter_type_check;
                }
            }
        }

        foreach (atom last_ditch in Dm.each<atom>(valid_targets))
        {
            if (!HAS_TRAIT(last_ditch, TRAIT_RECENTLY_COINED))
                return last_ditch;
        }
        return null;
    }
}

// The static list of the coin (rules 10.6: proc-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/modules/projectiles/projectile/bullets/special.dm:199  var/static/list/prioritized_targets
    internal DmList obj_projectile_bullet_coin__find_next_target__prioritized_targets = Dm.list(typeof(obj_structure_reagent_dispensers), typeof(obj_item_grenade), typeof(obj_structure_window));
}
