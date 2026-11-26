// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 projectiles slice (D165): the projectile base, whole: its firing, its pixel movement and tile crossing on SSprojectiles,
// hitscan with its tracers, muzzle and impact, Bump, impact, the hit loop, on_hit with the hit sounds and effects, range,
// ricochet (with the auto aim of a ricocheting bullet, M7x slice C), reflection and Destroy, and /atom/proc/fire_projectile. Left for
// the guns (after the core version), each at its site: the field-of-view
// flash of a shot (play_fov_effect, the FOV component) and the attack log of a shot mob are deferred. vv_edit_var (the admins'
// variable editor, M10) and CanEnterDisposals (whose /atom/movable root the port does not declare yet) are rows of
// docs/ai/specs/live-partial.txt. The var block is
// generated (D111); deletion_queued is declared here (tg gives it FALSE and the two deletion texts).

// tg: code/modules/projectiles/projectile.dm:7
[DmPath("/obj/projectile")]
public partial class obj_projectile : obj
{
    /// How many pixels to move the muzzle flash up so your character doesn't look like they're shitting out lasers.
    // tg: code/modules/projectiles/projectile.dm:4  #define MUZZLE_EFFECT_PIXEL_INCREMENT 17 (file-local, #undef at :1408)
    private const int MUZZLE_EFFECT_PIXEL_INCREMENT = 17;

    /// If FALSE, allow us to be hit by projectiles
    /// Are we going to be deleted on the next process?
    // tg: code/modules/projectiles/projectile.dm:244  (FALSE, PROJECTILE_IMPACT_DELETE or PROJECTILE_RANGE_DELETE: object, rules 3.2)
    public object deletion_queued = FALSE;

    // tg: code/modules/projectiles/projectile.dm:7  (the overrides of the type's var block; the class declares deletion_queued, so the
    // generator writes only the vars it does not declare, types-converter.md 3.2)
    public obj_projectile()
    {
        name = "projectile";
        icon = Dm.resource("icons/obj/weapons/guns/projectiles.dmi");
        icon_state = "bullet";
        abstract_type = typeof(obj_projectile);
        density = FALSE;
        anchored = TRUE;
        mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        movement_type = FLYING;
        wound_bonus = CANT_WOUND; // can't wound by default
        generic_canpass = FALSE;
        blocks_emissive = EMISSIVE_BLOCK_GENERIC;
        layer = MOB_LAYER;
        resistance_flags = Dm.bitor(Dm.bitor(Dm.bitor(LAVA_PROOF, FIRE_PROOF), UNACIDABLE), ACID_PROOF); // tg: :25
        pass_flags = PASSTABLE; // tg: :90
        /// Gliding does not enjoy something getting moved multiple turfs in a tick, which is why we animate it manually
        animate_movement = NO_STEPS; // tg: :119
    }

    // tg: code/modules/projectiles/projectile.dm:275
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        maximum_range = range;
        if (Dm.truthy(embed_type))
            set_embed(embed_type);
        add_traits(Dm.list(TRAIT_FREE_HYPERSPACE_MOVEMENT, TRAIT_FREE_HYPERSPACE_SOFTCORDON_MOVEMENT), INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/projectiles/projectile.dm:282
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(hitscan))
            Waiting.NoSleep(generate_hitscan_tracers());
        STOP_PROCESSING(SSprojectiles, this);
        firer = null;
        original = null;
        QDEL_NULL(ref embed_data);
        if (Dm.truthy(movement_vector))
            QDEL_NULL(ref movement_vector);
        if (Dm.truthy(beam_points))
            QDEL_LIST(beam_points);
        if (Dm.truthy(last_point))
            QDEL_NULL(ref last_point);
        return base.Destroy(force);
    }

    /// Called every time a projectile passes one tile worth of movement
    // tg: code/modules/projectiles/projectile.dm:298
    public virtual async DmTask<object> reduce_range()
    {
        Dm.dec(ref range);
        pixels_moved_last_tile -= ICON_SIZE_ALL;
        if (Dm.truthy(wound_falloff_tile) && wound_bonus != CANT_WOUND)
        {
            wound_bonus += Dm.num(wound_falloff_tile);
            exposed_wound_bonus = max(0, exposed_wound_bonus + Dm.num(wound_falloff_tile));
        }
        if (Dm.truthy(embed_falloff_tile) && Dm.truthy(get_embed()))
            embed_data.embed_chance += Dm.num(embed_falloff_tile);
        if (Dm.truthy(damage_falloff_tile) && Dm.num(damage) >= 0)
            Dm.append(ref damage, damage_falloff_tile);
        if (Dm.truthy(stamina_falloff_tile) && stamina >= 0)
            stamina += Dm.num(stamina_falloff_tile);

        if (Dm.truthy(this._listen_lookup?[COMSIG_PROJECTILE_RANGE]))
            this._SendSignal(COMSIG_PROJECTILE_RANGE, Dm.list(this));
        if (Dm.num(range) <= 0 && Dm.truthy(loc))
        {
            if (Dm.truthy(hitscan))
            {
                qdel(this);
                return null;
            }
            deletion_queued = PROJECTILE_RANGE_DELETE;
        }

        if (Dm.truthy(damage_falloff_tile) && Dm.num(damage) <= 0 || Dm.truthy(stamina_falloff_tile) && stamina <= 0)
        {
            if (Dm.truthy(hitscan))
            {
                qdel(this);
                return null;
            }
            deletion_queued = PROJECTILE_RANGE_DELETE;
        }
        return null;
    }

    /// Called next tick after the projectile reaches its maximum range so the animation has time to fully play out
    // tg: code/modules/projectiles/projectile.dm:325
    public virtual async DmTask on_range()
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_PROJECTILE_RANGE_OUT]))
            this._SendSignal(COMSIG_PROJECTILE_RANGE_OUT, Dm.list(this));
        qdel(this);
    }

    /**
     * Called when the projectile hits something
     *
     * By default parent call will always return [BULLET_ACT_HIT] (unless qdeleted)
     * so it is save to assume a successful hit in children (though not necessarily successfully damaged - it could've been blocked)
     *
     * Arguments
     * * target - thing hit
     * * blocked - percentage of hit blocked (0 to 100)
     * * pierce_hit - boolean, are we piercing through or regular hitting
     *
     * Returns
     * * Returns [BULLET_ACT_HIT] if we hit something. Default return value.
     * * Returns [BULLET_ACT_BLOCK] if we were hit but sustained no effects (blocked it). Note, Being "blocked" =/= "blocked is 100".
     * * Returns [BULLET_ACT_FORCE_PIERCE] to have the projectile keep going instead of "hitting", as if we were not hit at all.
     */
    // tg: code/modules/projectiles/projectile.dm:345
    [ShouldCallParent]
    public virtual async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;

        // i know that this is probably more with wands and gun mods in mind, but it's a bit silly that the projectile on_hit signal doesn't ping the projectile itself.
        // maybe we care what the projectile thinks! See about combining these via args some time when it's not 5AM
        object hit_limb_zone = null;
        if (isliving(target))
        {
            mob_living victim = target as mob_living;
            hit_limb_zone = victim.check_hit_limb_zone_name(def_zone);
            ShotGeometry.ShowHit(victim, hit_limb_zone); // DEVIATION(DV400): the hit is seen on the mob
            GunSight.NoteHit(this, victim); // DEVIATION(DV96): the firer's crosshair marks the hit
        }

        if (Dm.truthy(fired_from))
        {
            if (Dm.truthy(fired_from._listen_lookup?[COMSIG_PROJECTILE_ON_HIT]))
                fired_from._SendSignal(COMSIG_PROJECTILE_ON_HIT, Dm.list(fired_from, firer, target, angle, hit_limb_zone, blocked, pierce_hit));
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_PROJECTILE_SELF_ON_HIT]))
            this._SendSignal(COMSIG_PROJECTILE_SELF_ON_HIT, Dm.list(this, firer, target, angle, hit_limb_zone, blocked, pierce_hit));

        if (QDELETED(this) || Dm.truthy(deletion_queued)) // in case one of the above signals deleted the projectile for whatever reason
            return BULLET_ACT_BLOCK;

        turf target_turf = get_turf(target);
        if (target == original)
        {
            impact_x = target.pixel_x + Dm.num(p_x) - (float)ICON_SIZE_X / 2;
            impact_y = target.pixel_y + Dm.num(p_y) - (float)ICON_SIZE_Y / 2;
        }
        else
        {
            impact_x = Dm.add(entry_x, Dm.num(movement_vector?.pixel_x) * rand(0, (float)ICON_SIZE_X / 2));
            impact_y = Dm.add(entry_y, Dm.num(movement_vector?.pixel_y) * rand(0, (float)ICON_SIZE_Y / 2));
        }

        if (isturf(target) && Dm.truthy(hitsound_wall))
            playsound(this, hitsound_wall, clamp(Dm.num(vol_by_damage()) + (Dm.truthy(suppressed) ? 0 : 20), 0, 100), TRUE, -1);

        if (Dm.num(damage) > 0 && (damage_type == BRUTE || damage_type == BURN) && iswallturf(target_turf) && prob(75))
        {
            turf_closed_wall target_wall = target_turf as turf_closed_wall;
            if (Dm.truthy(impact_effect_type) && !Dm.truthy(hitscan))
                ShotGeometry.MarkImpact(this, Dm.@new(impact_effect_type, target_wall, impact_x, impact_y) as atom);

            // DEVIATION(DV95): an aimed shot's dent sits on the wall's face at the height its 3D line meets the wall
            // DEVIATION(DV214): on the one face the shot hit (the dent's dir), its x along that face
            (byte face, float along) = ShotGeometry.DentFace(this, target_wall, Dm.num(impact_x), Dm.num(impact_y));
            target_wall.add_dent(WALL_DENT_SHOT, along, dm_aimed ? ShotGeometry.DentHeight(this) : impact_y, face);
            return BULLET_ACT_HIT;
        }

        if (Dm.truthy(hitsound))
            playsound(this, hitsound, vol_by_damage(), TRUE, -1);

        // DEVIATION(DV577): an aimed shot dents the floor or the ceiling it hit with tg's odds of a wall's dent (above)
        if (dm_aimed && Dm.num(damage) > 0 && (damage_type == BRUTE || damage_type == BURN) && target is turf_open floor && prob(75))
            ShotGeometry.DentSurface(this, floor);

        if (!isliving(target))
        {
            if (Dm.truthy(impact_effect_type) && !Dm.truthy(hitscan))
                ShotGeometry.MarkImpact(this, Dm.@new(impact_effect_type, target_turf, impact_x, impact_y) as atom);
            return BULLET_ACT_HIT;
        }

        if ((Dm.num(blocked) >= 100 || (Dm.truthy(damage) && damage_type != BRUTE)) && Dm.truthy(impact_effect_type) && !Dm.truthy(hitscan))
            ShotGeometry.MarkImpact(this, Dm.@new(impact_effect_type, target_turf, impact_x, impact_y) as atom);

        mob_living living_target = target as mob_living;
        if (get_embed() is datum_embedding t__1)
            await t__1.try_embed_projectile(this, target, hit_limb_zone, blocked, pierce_hit);
        object reagent_note = null;
        if (Dm.truthy(((datum_reagents)reagents)?.reagent_list))
            reagent_note = Dm.text("REAGENTS: []", pretty_string_from_reagent_list(((datum_reagents)reagents).reagent_list)); // M8 slice C: the syringe gun's darts

        if (ismob(firer) && !Dm.truthy(do_not_log))
        {
            log_combat(firer, living_target, "shot", this, reagent_note);
            return BULLET_ACT_HIT;
        }

        if (isvehicle(firer))
        {
            obj_vehicle firing_vehicle = firer as obj_vehicle;
            DmList logging_mobs = firing_vehicle.return_controllers_with_flag(VEHICLE_CONTROL_EQUIPMENT);
            if (!Dm.truthy(LAZYLEN(logging_mobs)))
                logging_mobs = firing_vehicle.return_drivers();
            if (!Dm.truthy(do_not_log))
            {
                foreach (mob logged_mob in Dm.each<mob>(logging_mobs))
                    log_combat(logged_mob, living_target, "shot", this, Dm.text("from inside [][][]", firing_vehicle, Dm.num(logging_mobs.Count) > 1 ? " with multiple occupants" : null, Dm.truthy(reagent_note) ? Dm.text(" and contained []", reagent_note) : null));
            }
            return BULLET_ACT_HIT;
        }

        if (!Dm.truthy(do_not_log))
        {
            living_target.log_message(Dm.text("has been shot by [] with [][]", firer, this, Dm.truthy(reagent_note) ? Dm.text(" containing []", reagent_note) : null), LOG_ATTACK, color: "orange");
        }
        return BULLET_ACT_HIT;
    }

    // tg: code/modules/projectiles/projectile.dm:416
    public virtual object vol_by_damage()
    {
        if (Dm.truthy(suppressed))
            return 5;
        if (!Dm.truthy(damage))
            return 50; //if the projectile doesn't do damage, play its hitsound at 50% volume
        return clamp(Dm.num(damage) * 0.67f, 30, 100); // Multiply projectile damage by 0.67, then CLAMP the value between 30 and 1
    }

    // tg: code/modules/projectiles/projectile.dm:423
    [SignalHandler]
    public virtual void firer_deleted(datum source = null)
    {
        // Shooting yourself point-blank
        if (firer == original)
            original = null;
        if (firer == fired_from)
            fired_from = null;
        firer = null;
    }

    // tg: code/modules/projectiles/projectile.dm:432
    [SignalHandler]
    public virtual void original_deleted(datum source = null)
    {
        original = null;
    }

    // tg: code/modules/projectiles/projectile.dm:436
    [SignalHandler]
    public virtual void fired_from_deleted(datum source = null)
    {
        fired_from = null;
    }

    // tg: code/modules/projectiles/projectile.dm:440
    public virtual void on_ricochet(atom target = null)
    {
        ricochets++;
        if (!Dm.truthy(ricochet_auto_aim_angle) || !Dm.truthy(ricochet_auto_aim_range))
            return;

        mob_living unlucky_sob = null;
        float best_angle = ricochet_auto_aim_angle;
        if (firer != null && HAS_TRAIT(firer, TRAIT_NICE_SHOT))
            best_angle += NICE_SHOT_RICOCHET_BONUS;
        foreach (mob_living potential_target in Dm.each<mob_living>(range(ricochet_auto_aim_range, loc)))
        {
            if (potential_target.stat == DEAD || !Dm.truthy(is_in_sight(this, potential_target)) || (!Dm.truthy(ricochet_shoots_firer) && potential_target == firer))
                continue;
            float our_angle = abs(Dm.num(closer_angle_difference(angle, get_angle((atom)loc, (atom)potential_target.loc))));
            if (our_angle < best_angle)
            {
                best_angle = our_angle;
                unlucky_sob = potential_target;
            }
        }

        if (unlucky_sob != null)
        {
            set_angle(get_angle(this, (atom)unlucky_sob.loc));
            original = unlucky_sob;
        }
    }

    // tg: code/modules/projectiles/projectile.dm:461
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_BUMP]))
            this._SendSignal(COMSIG_MOVABLE_BUMP, Dm.list(this, bumped_atom));
        if (Dm.truthy(await can_hit_target(bumped_atom, Dm.num(bumped_atom == original), TRUE, TRUE)))
            await impact(bumped_atom);
        return null;
    }

    /**
     * Called when the projectile hits something
     * This can either be from it bumping something,
     * or it passing over a turf/being crossed and scanning that there is infact
     * a valid target it needs to hit.
     * This target isn't however necessarily WHAT it hits
     * that is determined by process_hit and select_target.
     *
     * Furthermore, this proc shouldn't check can_hit_target - this should only be called if can hit target is already checked.
     * Also, we select_target to find what to process_hit first.
     */
    // tg: code/modules/projectiles/projectile.dm:477
    public virtual async DmTask<object> impact(atom target = null)
    {
        // Don't impact anything if we've been queued for deletion
        if (Dm.truthy(deletion_queued))
            return null;

        // never doublehit, otherwise someone may end up running into a projectile from the back
        if (Dm.truthy(impacted[target.weak_reference]))
            return null;

        if (ricochets < ricochets_max && Dm.truthy(check_ricochet_flag(target)) && Dm.truthy(check_ricochet(target)) && Dm.truthy(await target.handle_ricochet(this)))
        {
            on_ricochet(target);
            impacted = Dm.list(); // Shoot a x-ray laser at a pair of mirrors I dare you
            ignore_source_check = TRUE; // Firer is no longer immune
            maximum_range = max(0, Dm.sub(maximum_range, reflect_range_decrease));
            ricochet_chance *= ricochet_decay_chance;
            damage = Dm.num(damage) * ricochet_decay_damage;
            stamina *= ricochet_decay_damage;
            range = Dm.num_or_null(maximum_range);
            return null;
        }

        last_impact_turf = get_turf(target);

        // If our target has TRAIT_DESIGNATED_TARGET, treat accuracy_falloff as 0
        float effective_accuracy = HAS_TRAIT(target, TRAIT_DESIGNATED_TARGET) ? 0 : accuracy_falloff;

        // Lower accurancy/longer range tradeoff. 7 is a balanced number to use.
        // DEVIATION(DV95): an aimed shot's zone is the body part its line crosses (can_hit_target), so tg's distance roll is not made
        if (!dm_aimed)
            def_zone = (string)ran_zone(def_zone, clamp(accurate_range - (effective_accuracy * get_dist(last_impact_turf, starting)), 5, 100));
        object impact_result = process_hit_loop(await select_target(last_impact_turf, target) as atom);
        if (Dm.eq(impact_result, PROJECTILE_IMPACT_PASSED))
            return null;
        if (Dm.truthy(hitscan))
        {
            qdel(this);
            return null;
        }
        deletion_queued = PROJECTILE_IMPACT_DELETE;
        return null;
    }

    /*
     * Main projectile hit loop code
     * As long as there are valid targets on the hit target's tile, we will loop through all the ones that we have not hit
     * (and thus invalidated) and try to hit them until either no targets remain or we've been deleted.
     * Should *never* be called directly, as impact() is the proc queueing projectiles for deletion
     * If you need to call this directly, you should reconsider the choices that led you to this point
     */
    // tg: code/modules/projectiles/projectile.dm:519
    [ShouldNotSleep, DmPrivate]
    public virtual object process_hit_loop(atom target = null)
    {
        // Don't impact anything if we've been queued for deletion
        if (Dm.truthy(deletion_queued))
            return PROJECTILE_IMPACT_PASSED;

        turf target_turf = get_turf(target);
        while (Dm.truthy(target) && !QDELETED(this) && !Dm.truthy(deletion_queued))
        {
            // Doublehitting can be an issue with slow projectiles or when the server is chugging
            impacted[WEAKREF(target)] = TRUE;
            object mode = Waiting.NoSleep(prehit_pierce(target));
            if (Dm.eq(mode, PROJECTILE_DELETE_WITHOUT_HITTING))
                return PROJECTILE_IMPACT_INTERRUPTED;

            // If we've phasing through a target, first set ourselves as phasing and then try to locate a new one
            if (Dm.eq(mode, PROJECTILE_PIERCE_PHASE))
            {
                if (!Dm.truthy(Dm.bitand(movement_type, PHASING)))
                {
                    temporary_unstoppable_movement = TRUE;
                    Dm.combine(ref movement_type, PHASING);
                }
                target = Waiting.NoSleep(select_target(target_turf, target)) as atom;
                continue;
            }

            object target_signal = (!Dm.truthy(target._listen_lookup?[COMSIG_PROJECTILE_PREHIT]) ? NONE : target._SendSignal(COMSIG_PROJECTILE_PREHIT, Dm.list(target, this)));
            if (Dm.truthy(Dm.bitand(target_signal, PROJECTILE_INTERRUPT_HIT_PHASE)))
                return PROJECTILE_IMPACT_PASSED;
            if (Dm.truthy(Dm.bitand(target_signal, PROJECTILE_INTERRUPT_HIT)))
                return PROJECTILE_IMPACT_INTERRUPTED;

            object self_signal = (!Dm.truthy(this._listen_lookup?[COMSIG_PROJECTILE_SELF_PREHIT]) ? NONE : this._SendSignal(COMSIG_PROJECTILE_SELF_PREHIT, Dm.list(this, target)));
            if (Dm.truthy(Dm.bitand(self_signal, PROJECTILE_INTERRUPT_HIT_PHASE)))
                return PROJECTILE_IMPACT_PASSED;
            if (Dm.truthy(Dm.bitand(self_signal, PROJECTILE_INTERRUPT_HIT)))
                return PROJECTILE_IMPACT_INTERRUPTED;

            if (Dm.eq(mode, PROJECTILE_PIERCE_HIT))
                pierces += 1;

            // Targets should handle their impact logic on our own and if they decide that we hit them, they call our on_hit
            object result = Waiting.NoSleep(target.projectile_hit(this, def_zone, Dm.num(Dm.eq(mode, PROJECTILE_PIERCE_HIT))));
            if (!Dm.eq(result, BULLET_ACT_FORCE_PIERCE) && Dm.truthy(max_pierces) && pierces >= max_pierces)
                return PROJECTILE_IMPACT_SUCCESSFUL;

            // If we're not piercing or phasing, delete ourselves
            if (!Dm.eq(result, BULLET_ACT_FORCE_PIERCE) && !Dm.eq(mode, PROJECTILE_PIERCE_HIT) && !Dm.eq(mode, PROJECTILE_PIERCE_PHASE))
                return PROJECTILE_IMPACT_SUCCESSFUL;

            // We've piercing though this one, go look for a new target
            if (!Dm.truthy(Dm.bitand(movement_type, PHASING)))
            {
                temporary_unstoppable_movement = TRUE;
                Dm.combine(ref movement_type, PHASING);
            }

            target = Waiting.NoSleep(select_target(target_turf, target)) as atom;
        }

        return PROJECTILE_IMPACT_PASSED;
    }

    /**
     * Selects a target to hit from a turf
     *
     * @params
     * our_turf - Turf on which we hit the target
     * bumped - What we've impacted and why this selection was called in the first place.
     *    If set, this atom is always treated as dense by can_hit_target.
     *
     * Priority:
     * 0. Anything that is already in impacted is ignored no matter what. Furthermore, in any bracket, if the target atom parameter is in it, that's hit first.
     * Furthermore, can_hit_target is always checked. This (entire proc) is PERFORMANCE OVERHEAD!! But, it shouldn't be ""too"" bad and I frankly don't have a better *generic non snowflakey* way that I can think of right now at 3 AM.
     * FURTHERMORE, mobs/objs have a density check from can_hit_target - to hit non dense objects over a turf, you must click on them, same for mobs that usually wouldn't get hit.
     * 1. Special check on what we bumped to see if it's a border object that intercepts hitting anything behind it
     * 2. The thing originally aimed at/clicked on
     * 3. Mobs - picks lowest buckled mob to prevent scarp piggybacking memes
     * 4. Objs
     * 5. Turf
     * 6. Nothing
     */
    // tg: code/modules/projectiles/projectile.dm:595
    public virtual async DmTask<object> select_target(turf our_turf = null, atom bumped = null)
    {
        // 1. special bumped border object check
        if (Dm.truthy(Dm.bitand(bumped?.flags_1, ON_BORDER_1)) && Dm.truthy(await can_hit_target(bumped, Dm.num(original == bumped), TRUE, TRUE)))
            return bumped;
        // 2. original
        if (Dm.truthy(await can_hit_target(original, TRUE, FALSE, Dm.num(original == bumped))))
            return original;
        DmList considering = Dm.list(); // let's define this ONCE
        // 3. mobs
        foreach (mob_living iter_possible_target in Dm.each<mob_living>(our_turf))
        {
            if (Dm.truthy(await can_hit_target(iter_possible_target, Dm.num(iter_possible_target == original), TRUE, Dm.num(iter_possible_target == bumped))))
                Dm.combine(ref considering, iter_possible_target);
        }
        // DEVIATION(DV95): an aimed shot hits the nearest of the mobs its line crosses, not a random one
        if (dm_aimed && Dm.num(length(considering)) > 1)
            return ShotGeometry.Nearest(this, considering);
        if (Dm.truthy(length(considering)))
            return pick(considering);
        // 4. objs and other dense things
        foreach (atom potential_target in Dm.each_cast<atom>(our_turf))
        {
            if (Dm.truthy(await can_hit_target(potential_target, Dm.num(potential_target == original), TRUE, Dm.num(potential_target == bumped))))
                Dm.append(ref considering, potential_target);
        }
        if (Dm.truthy(length(considering)))
            return pick(considering);
        // 5. turf
        if (Dm.truthy(await can_hit_target(our_turf, Dm.num(our_turf == original), TRUE, Dm.num(our_turf == bumped))))
            return our_turf;
        // 6. nothing
        // (returns null)
        return null;
    }

    /// Returns true if the target atom is on our current turf and above the right layer
    /// If direct target is true it's the originally clicked target.
    // tg: code/modules/projectiles/projectile.dm:623
    public virtual async DmTask<object> can_hit_target(atom target = null, float? direct_target = null, float? ignore_loc = null, float? cross_failed = null)
    {
        direct_target ??= FALSE;
        ignore_loc ??= FALSE;
        cross_failed ??= FALSE;
        if (QDELETED(target) || Dm.truthy(impacted[target.weak_reference]))
            return FALSE;
        if (!Dm.truthy(ignore_loc) && (loc != target.loc) && !(Dm.truthy(can_hit_turfs) && Dm.truthy(direct_target) && loc == target))
            return FALSE;
        // if pass_flags match, pass through entirely - unless direct target is set.
        if (Dm.truthy(Dm.bitand(target.pass_flags_self, pass_flags)) && !Dm.truthy(direct_target))
            return FALSE;
        if (Dm.truthy(direct_target) && ShotGeometry.SeesThrough(this, target)) // DEVIATION(DV595): the clicked glass of a 3D aim is passed
            return FALSE;
        if (HAS_TRAIT(target, TRAIT_UNHITTABLE_BY_PROJECTILES))
        {
            if (!HAS_TRAIT(target, TRAIT_BLOCKING_PROJECTILES) && isliving(target))
            {
                mob_living living_target = target as mob_living;
                await living_target.block_projectile_effects();
            }
            return FALSE;
        }
        if (HAS_TRAIT(target, TRAIT_UNHITTABLE_BY_LASERS) && Dm.truthy(Dm.bitand(armor_flag, LASER)))
            return FALSE;
        if (!Dm.truthy(ignore_source_check) && Dm.truthy(firer) && !Dm.truthy(direct_target))
        {
            if (target == firer || (target == firer.loc && ismecha(firer.loc)) || (Dm.@in(target, firer.buckled_mobs)))
                return FALSE;
            if (ismob(firer))
            {
                mob firer_mob = firer as mob;
                if (firer_mob.buckled == target)
                    return FALSE;
            }
        }
        if (Dm.truthy(LAZYLEN(faction)) && ismob(target) && !Dm.truthy(direct_target))
        {
            mob target_mob = target as mob;
            if (Dm.truthy(FAST_FACTION_CHECK(faction, target_mob.get_faction(), allies, target_mob.allies, FALSE)))
                return FALSE;
        }
        // DEVIATION(DV95): an aimed shot hits a carbon only where its 3D line crosses a drawn body part, whatever the density, the direct
        // target, tg's prone auto-aim or the lying skip say; a miss marks the mob impacted (tg's never-double-hit list), so CanPassThrough
        // lets the shot past it and it flies on. A dead body is skipped unless it was clicked, as tg's.
        if (dm_aimed && target is mob_living aimed_target)
        {
            if (aimed_target.stat == DEAD && !Dm.truthy(direct_target))
                return FALSE;
            // DEVIATION(DV527): a rewound shot meets the mob only on the turf of its point then; not marked, it may meet it there later
            if (!ShotRewind.SeenOn(this, aimed_target, loc as turf))
                return FALSE;
            switch (ShotGeometry.Cross(this, aimed_target, out string crossed_zone))
            {
                case ShotGeometry.Crossing.Hit:
                    def_zone = crossed_zone;
                    return TRUE;
                case ShotGeometry.Crossing.Miss when target == homing_target:
                    // DEVIATION(DV96): a homing shot hits the mob it homes on in its turf, as tg's does (its homing offsets put the line beside
                    // the drawn body as often as on it); the zone stays the one it was fired at
                    return TRUE;
                case ShotGeometry.Crossing.Miss:
                    impacted[WEAKREF(target)] = TRUE;
                    return FALSE;
            }
        }
        if (Dm.truthy(target.density) || Dm.truthy(cross_failed)) //This thing blocks projectiles, hit it regardless of layer/mob stuns/etc.
            return TRUE;
        if (!isliving(target))
        {
            if (isturf(target)) // non dense turfs
                return (can_hit_turfs is var and__1 && !Dm.truthy(and__1) ? (object)and__1 : direct_target);
            if (target.layer < hit_threshhold)
                return FALSE;
            else if (!Dm.truthy(direct_target)) // non dense objects do not get hit unless specifically clicked
                return FALSE;
        }
        else
        {
            mob_living living_target = target as mob_living;
            if (Dm.truthy(direct_target))
                return TRUE;
            if (living_target.stat == DEAD)
                return FALSE;
            if (HAS_TRAIT(living_target, TRAIT_IMMOBILIZED) && HAS_TRAIT(living_target, TRAIT_FLOORED) && HAS_TRAIT(living_target, TRAIT_HANDS_BLOCKED))
                return FALSE;
            if (Dm.truthy(hit_prone_targets))
            {
                mob_living buckled_to = living_target.lowest_buckled_mob() as mob_living;
                if (Dm.num(Dm.sub(maximum_range, range)) <= MAX_RANGE_HIT_PRONE_TARGETS) // after MAX_RANGE_HIT_PRONE_TARGETS tiles, auto-aim hit for mobs on the floor turns off
                    return TRUE;
                if (Dm.truthy(ignore_range_hit_prone_targets)) // doesn't apply to projectiles that must hit the target in combat mode or something else, no matter what
                    return TRUE;
                if (Dm.truthy(buckled_to.density)) // Will just be us if we're not buckled to another mob
                    return TRUE;
                return FALSE;
            }
            else if (living_target.body_position == LYING_DOWN)
                return FALSE;
        }
        return TRUE;
    }

    /**
     * Scans if we should hit something on the turf we just moved to if we haven't already
     *
     * This proc is a little high in overhead but allows us to not snowflake CanPass in living and other things.
     */
    // tg: code/modules/projectiles/projectile.dm:684
    public virtual async DmTask scan_moved_turf()
    {
        // Optimally, we scan: mobs --> objs --> turf for impact
        // but, overhead is a thing and 2 for loops every time it moves is a no-go.
        // realistically, since we already do select_target in impact, we can not do that
        // and hope projectiles get refactored again in the future to have a less stupid impact detection system
        // that hopefully won't also involve a ton of overhead
        if (Dm.truthy(await can_hit_target(original, TRUE, FALSE)))
        {
            await impact(original); // try to hit thing clicked on
            return;
        }
        // else, try to hit mobs
        // because if we impacted original and pierced we'll already have select target'd and hit everything else we should be hitting
        foreach (mob potential_target in Dm.each<mob>(loc)) // so I guess we're STILL doing a for loop of mobs because living movement would otherwise have snowflake code for projectile CanPass
        {
            // so the snowflake vs performance is pretty arguable here
            if (Dm.truthy(await can_hit_target(potential_target, Dm.num(potential_target == original), TRUE)))
            {
                await impact(potential_target);
                return;
            }
        }
        // DEVIATION(DV527): and the mobs a rewound shot meets here, where they stood when the shooter saw them
        foreach (mob_living seen in ShotRewind.ElsewhereSeenOn(this, loc as turf))
        {
            if (Dm.truthy(await can_hit_target(seen, Dm.num(seen == original), TRUE)))
            {
                await impact(seen);
                return;
            }
        }
    }

    /**
     * Projectile crossed: When something enters a projectile's tile, make sure the projectile hits it if it should be hitting it.
     */
    // tg: code/modules/projectiles/projectile.dm:704
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable entered_atom = null)
    {
        if (Dm.truthy(Waiting.NoSleep(can_hit_target(entered_atom, direct_target: Dm.num(entered_atom == original)))))
            Waiting.NoSleep(impact(entered_atom));
    }

    /**
     * Projectile can pass through
     * Used to not even attempt to Bump() or fail to Cross() anything we already hit.
     */
    // tg: code/modules/projectiles/projectile.dm:713
    public override object CanPassThrough(atom blocker = null, object movement_dir = null, object blocker_opinion = null)
    {
        if (ShotRewind.PassesNow(this, blocker)) // DEVIATION(DV527): a rewound shot is not stopped by a body where it stands now
            return TRUE;
        return (impacted[blocker.weak_reference] is var or__1 && Dm.truthy(or__1) ? or__1 : base.CanPassThrough(blocker, movement_dir, blocker_opinion));
    }

    /**
     * Projectile moved:
     *
     * If not fired yet, do not do anything. Else,
     *
     * If temporary unstoppable movement used for piercing through things we already hit (impacted list) is set, unset it.
     * Scan turf we're now in for anything we can/should hit. This is useful for hitting non dense objects the user
     * directly clicks on, as well as for PHASING projectiles to be able to hit things at all as they don't ever Bump().
     */
    // tg: code/modules/projectiles/projectile.dm:725
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = null;
        dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!Dm.truthy(fired))
            return dot;
        if (Dm.truthy(temporary_unstoppable_movement))
        {
            temporary_unstoppable_movement = FALSE;
            Dm.mask(ref movement_type, Dm.bitnot(PHASING));
        }
        // Mostly used for making sure we can hit a non-dense object the user directly clicked on, and for penetrating projectiles that don't bump
        await scan_moved_turf();
        return dot;
    }

    /**
     * Checks if we should pierce something.
     *
     * NOT meant to be a pure proc, since this replaces prehit() which was used to do things.
     * Return PROJECTILE_DELETE_WITHOUT_HITTING to delete projectile without hitting at all!
     */
    // tg: code/modules/projectiles/projectile.dm:741
    public virtual async DmTask<object> prehit_pierce(atom target = null)
    {
        if (Dm.truthy(Dm.bitand(projectile_phasing, target.pass_flags_self)) && (Dm.truthy(phasing_ignore_direct_target) || original != target))
            return PROJECTILE_PIERCE_PHASE;
        if (Dm.truthy(Dm.bitand(projectile_piercing, target.pass_flags_self)))
            return PROJECTILE_PIERCE_HIT;
        if (ismovable(target))
        {
            atom_movable movable_target = target as atom_movable;
            if (Dm.truthy(movable_target.throwing))
                return (Dm.truthy(Dm.bitand(projectile_phasing, LETPASSTHROW)) ? PROJECTILE_PIERCE_PHASE : (Dm.truthy(Dm.bitand(projectile_piercing, LETPASSTHROW)) ? PROJECTILE_PIERCE_HIT : PROJECTILE_PIERCE_NONE));
        }
        return PROJECTILE_PIERCE_NONE;
    }

    // tg: code/modules/projectiles/projectile.dm:752
    public virtual object check_ricochet(atom target = null)
    {
        float chance = ricochet_chance * target.receive_ricochet_chance_mod;
        if (Dm.truthy(firer) && HAS_TRAIT(firer, TRAIT_NICE_SHOT))
            chance += NICE_SHOT_RICOCHET_BONUS;
        if (ricochets < min_ricochets || prob(chance))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/projectiles/projectile.dm:760
    public virtual object check_ricochet_flag(atom target = null)
    {
        if ((Dm.@in(armor_flag, Dm.list(ENERGY, LASER))) && Dm.truthy(Dm.bitand(target.flags_ricochet, RICOCHET_SHINY)))
            return TRUE;
        if ((Dm.@in(armor_flag, Dm.list(BOMB, BULLET))) && Dm.truthy(Dm.bitand(target.flags_ricochet, RICOCHET_HARD)))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/projectiles/projectile.dm:767
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null)
    {
        return TRUE; //Bullets don't drift in space
    }

    // tg: code/modules/projectiles/projectile.dm:770
    public virtual async DmTask fire(object fire_angle = null, atom direct_target = null)
    {
        LAZYINITLIST(ref impacted);
        if (Dm.truthy(firer))
        {
            RegisterSignal(firer, COMSIG_QDELETING, nameof(firer_deleted));
            if (Dm.truthy(firer._listen_lookup?[COMSIG_PROJECTILE_FIRER_BEFORE_FIRE]))
                firer._SendSignal(COMSIG_PROJECTILE_FIRER_BEFORE_FIRE, Dm.list(firer, this, fired_from, original));
        }
        if (Dm.truthy(fired_from))
        {
            if (firer != fired_from)
                RegisterSignal(fired_from, COMSIG_QDELETING, nameof(fired_from_deleted));
            if (Dm.truthy(fired_from._listen_lookup?[COMSIG_PROJECTILE_BEFORE_FIRE]))
                fired_from._SendSignal(COMSIG_PROJECTILE_BEFORE_FIRE, Dm.list(fired_from, this, original));
        }
        if (Dm.truthy(original))
        {
            if (firer != original)
                RegisterSignal(original, COMSIG_QDELETING, nameof(original_deleted));
        }
        if (!Dm.truthy(log_override) && Dm.truthy(firer) && Dm.truthy(original) && !Dm.truthy(do_not_log))
            log_combat(firer, original, "fired at", this, Dm.text("from []", get_area_name(this, TRUE)));
        //note: mecha projectile logging is handled in /obj/item/mecha_parts/mecha_equipment/weapon/action(). try to keep these messages roughly the sameish just for consistency's sake.
        if (Dm.truthy(direct_target) && (get_dist(direct_target, get_turf(this)) <= 1)) // point blank shots
        {
            await impact(direct_target);
            if (QDELETED(this))
                return;
        }
        turf starting = get_turf(this);
        if (isnum(fire_angle))
            set_angle(fire_angle);
        else if (angle == null) //Try to resolve through offsets if there's no angle set.
        {
            if (xo == null || yo == null)
            {
                stack_trace(Dm.text("WARNING: Projectile [] deleted due to being unable to resolve a target after angle was null!", type), "code/modules/projectiles/projectile.dm", 794);
                qdel(this);
                return;
            }
            turf target = locate(Dm.num(clamp(Dm.add(starting.x, xo), 1, world.maxx)), Dm.num(clamp(Dm.add(starting.y, yo), 1, world.maxy)), starting.z);
            set_angle(get_angle(this, target));
        }
        if (Dm.truthy(spread))
            set_angle(Dm.add(angle, (rand() - 0.5f) * spread));
        original_angle = Dm.num(angle);
        // protocol version 34: a shot fired without an aim (fire_projectile, the emitter, turrets) is drawn level at a mob's middle
        if (!dm_shot)
            ShotGeometry.SetUnaimed(this);
        movement_vector = Dm.@new<datum_vector>(speed, angle);
        if (Dm.truthy(hitscan))
            beam_points = Dm.list();
        free_hitscan_forceMove = TRUE;
        await forceMove(starting);
        last_projectile_move = world.time;
        fired = TRUE;
        // tg: :809  play_fov_effect(starting, 6, "gunfire", dir = NORTH, angle = angle): the field-of-view flash of a shot for the
        // mobs that cannot see it (modules/mob/living/living_fov.dm, the FOV component, not in the port)
        Dm.Deferred("code/modules/projectiles/projectile.dm:809", "fire: play_fov_effect, the field-of-view flash of a shot (the FOV component, not in the port)");
        if (Dm.truthy(this._listen_lookup?[COMSIG_PROJECTILE_FIRE]))
            this._SendSignal(COMSIG_PROJECTILE_FIRE, Dm.list(this));
        if (Dm.truthy(hitscan) && !Dm.truthy(deletion_queued))
        {
            record_hitscan_start();
            await process_hitscan();
            if (QDELETED(this))
                return;
        }
        if (!Dm.truthy(Dm.bitand(datum_flags, DF_ISPROCESSING)))
            START_PROCESSING(SSprojectiles, this);
        // move it now to avoid potentially hitting yourself with firer-hitting projectiles
        if (!Dm.truthy(deletion_queued) && !Dm.truthy(hitscan))
            await process_movement(max(floor(speed)), tile_limit: TRUE);
    }

    /// Makes projectile home onto the passed target with minor inaccuracy
    // tg: code/modules/projectiles/projectile.dm:823
    public virtual object set_homing_target(atom target = null)
    {
        if (!Dm.truthy(target) || (!isturf(target) && !isturf(target.loc)))
            return FALSE;
        homing = TRUE;
        homing_target = target;
        homing_offset_x = rand(homing_inaccuracy_min, homing_inaccuracy_max);
        homing_offset_y = rand(homing_inaccuracy_min, homing_inaccuracy_max);
        if (prob(50))
            homing_offset_x = -homing_offset_x;
        if (prob(50))
            homing_offset_y = -homing_offset_y;
        return null;
    }

    // tg: code/modules/projectiles/projectile.dm:835
    public virtual void set_angle(object new_angle = null)
    {
        if (Dm.eq(angle, new_angle))
            return;
        if (!Dm.truthy(nondirectional_sprite))
            transform = ((matrix)transform).TurnTo(angle, Dm.add(new_angle, projectile_angle));
        // DEVIATION(DV95): a turn in flight (a ricochet, a reflection, homing) starts the 3D line again where the shot is, at its height
        if (Dm.truthy(fired) && dm_shot)
            ShotGeometry.Rebase(this);
        angle = new_angle;
        if (Dm.truthy(movement_vector))
            movement_vector.set_angle(new_angle);
        if (Dm.truthy(fired) && Dm.truthy(hitscan) && isturf(loc))
            create_hitscan_point();
    }

    /// Same as set_angle, but the reflection continues from the center of the object that reflects it instead of the side
    // tg: code/modules/projectiles/projectile.dm:847
    public virtual async DmTask set_angle_centered(object center_turf = null, object new_angle = null)
    {
        if (Dm.eq(angle, new_angle))
            return;
        if (!Dm.truthy(nondirectional_sprite))
            transform = ((matrix)transform).TurnTo(angle, Dm.add(new_angle, projectile_angle));
        free_hitscan_forceMove = TRUE;
        await forceMove(center_turf as atom);
        entry_x = 0;
        entry_y = 0;
        if (Dm.truthy(fired) && dm_shot)
            ShotGeometry.Rebase(this); // DEVIATION(DV95): from the reflector's centre, as set_angle
        angle = new_angle;
        if (Dm.truthy(movement_vector))
            movement_vector.set_angle(new_angle);
        if (Dm.truthy(fired) && Dm.truthy(hitscan) && isturf(loc))
            create_hitscan_point(tile_center: TRUE);
    }

    /*
     * Projectile's process calculates the amount of pixels that it needs to move per tick and calls moveloop processing
     * There is a strict cap on how many pixels it can move in a tick to prevent them from turning into hitscans during lag
     * Path that the projectile could not finish would be stored in the overrun variable to be processed next tick
     */

    // tg: code/modules/projectiles/projectile.dm:874  (set waitfor = FALSE, inherited from /datum/process: the wrapper of
    // waiting-model.md 5.4)
    [DmParams()]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(dot), dot);
    }

    private async DmTask<object> process__body(DotBox<object> dot)
    {
        last_process = world.time;
        if (!Dm.truthy(loc) || !Dm.truthy(fired) || !Dm.truthy(movement_vector))
        {
            fired = FALSE;
            return PROCESS_KILL;
        }

        // If last tick the projectile impacted something or reached its range, don't process it
        if (Dm.eq(deletion_queued, PROJECTILE_IMPACT_DELETE))
        {
            ticks_to_deletion -= 1;
            if (!Dm.truthy(ticks_to_deletion))
                qdel(this);
            return null;
        }

        if (Dm.eq(deletion_queued, PROJECTILE_RANGE_DELETE))
        {
            await on_range();
            return null;
        }

        if (Dm.truthy(paused) || !isturf(loc))
        {
            // Compensates for pausing, so it doesn't become a hitscan projectile when unpaused from charged up ticks.
            last_projectile_move = last_process;
            return null;
        }

        if (Dm.truthy(hitscan))
        {
            await process_hitscan();
            return null;
        }

        // Calculates how many pixels should be moved this tick, including overrun debt from the previous tick
        float elapsed_time = world.time - last_projectile_move;
        float pixels_to_move = elapsed_time * SSprojectiles.pixels_per_decisecond * speed + overrun;
        overrun = 0;

        if (pixels_to_move > SSprojectiles.max_pixels_per_tick)
        {
            overrun = pixels_to_move - SSprojectiles.max_pixels_per_tick;
            pixels_to_move = SSprojectiles.max_pixels_per_tick;
        }

        overrun += MODULUS(pixels_to_move, 1);
        pixels_to_move = floor(pixels_to_move);
        if (Dm.truthy(this._listen_lookup?[COMSIG_PROJECTILE_BEFORE_MOVE]))
            this._SendSignal(COMSIG_PROJECTILE_BEFORE_MOVE, Dm.list(this));

        // Registering turf entries is done here instead of a connect_loc because else it could be called multiple times per tick and waste performance
        if (Dm.truthy(last_tick_turf))
            UnregisterSignal(last_tick_turf, COMSIG_ATOM_ENTERED);

        await process_movement(pixels_to_move);

        if (!QDELETED(this) && !Dm.truthy(deletion_queued) && isturf(loc))
        {
            RegisterSignal(loc, COMSIG_ATOM_ENTERED, nameof(on_entered));
            last_tick_turf = loc as turf;
        }
        return null;
    }

    /*
     * Main projectile movement cycle.
     * Normal behavior moves projectiles in a straight line through tiles, but it gets trickier with homing.
     * Every pixels_per_decisecond we will stop and call process_homing(), which while a bit rough, does not have a significant performance impact
     * This proc needs to be very performant, so do not add overridable logic that can be handled in homing or animations here.
     * Return is how many tiles we've actually passed (or attempted to pass, if we ended up on a half-move)
     *
     * pixels_to_move determines how many pixels the projectile should move
     * hitscan prevents animation logic from running
     * tile_limit prevents any movements past the first tile change
     */
    // tg: code/modules/projectiles/projectile.dm:934
    public virtual async DmTask<object> process_movement(object pixels_to_move = null, float? hitscan = null, float? tile_limit = null)
    {
        hitscan ??= FALSE;
        tile_limit ??= FALSE;
        if (!isturf(loc) || !Dm.truthy(movement_vector))
            return 0;
        float total_move_distance = Dm.num(pixels_to_move);
        float movements_done = 0;
        last_projectile_move = world.time;
        while (Dm.gt(pixels_to_move, 0) && isturf(loc) && !QDELETED(this) && !Dm.truthy(deletion_queued))
        {
            // Because pixel_x/y represents offset and not actual visual position of the projectile, we add 16 pixels to each and cut the excess because projectiles are not meant to be highly offset by default
            float pixel_x_actual = pixel_x + (float)ICON_SIZE_X / 2;
            if (pixel_x_actual > ICON_SIZE_X)
                pixel_x_actual = Dm.mod(pixel_x_actual, ICON_SIZE_X);

            float pixel_y_actual = pixel_y + (float)ICON_SIZE_Y / 2;
            if (pixel_y_actual > ICON_SIZE_Y)
                pixel_y_actual = Dm.mod(pixel_y_actual, ICON_SIZE_Y);

            float distance_to_border = INFINITY;
            // What distances do we need to move to hit the horizontal/vertical turf border
            float x_to_border = INFINITY;
            float y_to_border = INFINITY;
            // If we're moving strictly up/down/left/right then one of these can be 0 and produce div by zero
            if (Dm.truthy(movement_vector.pixel_x))
            {
                float x_border_dist = -pixel_x_actual;
                if (Dm.num(movement_vector.pixel_x) > 0)
                    x_border_dist = ICON_SIZE_X - pixel_x_actual;
                x_to_border = Dm.div(x_border_dist, Dm.num(movement_vector.pixel_x));
                distance_to_border = x_to_border;
            }

            if (Dm.truthy(movement_vector.pixel_y))
            {
                float y_border_dist = -pixel_y_actual;
                if (Dm.num(movement_vector.pixel_y) > 0)
                    y_border_dist = ICON_SIZE_Y - pixel_y_actual;
                y_to_border = Dm.div(y_border_dist, Dm.num(movement_vector.pixel_y));
                distance_to_border = min(distance_to_border, y_to_border);
            }

            // Something went extremely wrong
            if (distance_to_border == INFINITY)
            {
                stack_trace("WARNING: Projectile had an empty movement vector and tried to process", "code/modules/projectiles/projectile.dm", 971);
                qdel(this);
                return movements_done;
            }

            float distance_to_move = min(distance_to_border, Dm.num(pixels_to_move));
            // For homing we cap the maximum distance to move every loop
            if (Dm.truthy(homing) && distance_to_move > SSprojectiles.pixels_per_decisecond)
                distance_to_move = SSprojectiles.pixels_per_decisecond;

            // DEVIATION(DV97): an aimed shot whose 3D line reaches the floor or the ceiling within this step stops there and hits the
            // turf it is over
            bool floor_hit = false;
            if (dm_aimed && ShotGeometry.DistanceToFloorOrCeiling(this) is double to_floor && to_floor <= distance_to_move)
            {
                distance_to_move = (float)to_floor;
                floor_hit = true;
            }
            // DEVIATION(DV526): the stairs' flight the 3D client draws over their tile stops the line where it meets it
            bool flight_hit = false;
            if (dm_aimed && ShotGeometry.DistanceToFlight(this) is double to_flight && to_flight <= distance_to_move)
            {
                distance_to_move = (float)to_flight;
                floor_hit = true;
                flight_hit = true;
            }

            // Figure out if we move to the next turf and if so, what its positioning relatively to us is
            float x_shift = !floor_hit && distance_to_move >= x_to_border ? sign(Dm.num(movement_vector.pixel_x)) : 0;
            float y_shift = !floor_hit && distance_to_move >= y_to_border ? sign(Dm.num(movement_vector.pixel_y)) : 0;
            float moving_turfs = (x_shift is var or__1 && Dm.truthy(or__1) ? or__1 : y_shift);
            // Calculate where in the turf we will be when we cross the edge.
            // This is a projectile variable because its also used in hit VFX
            entry_x = pixel_x + Dm.num(movement_vector.pixel_x) * distance_to_move - x_shift * ICON_SIZE_X;
            entry_y = pixel_y + Dm.num(movement_vector.pixel_y) * distance_to_move - y_shift * ICON_SIZE_Y;
            float delete_distance = 0;

            if (Dm.truthy(moving_turfs))
            {
                turf new_turf = locate(x + x_shift, y + y_shift, z);
                // We've hit an invalid turf, end of a z level or smth went wrong
                if (!istype(new_turf, typeof(turf)))
                {
                    qdel(this);
                    return movements_done;
                }

                // Move to the next tile
                await step_towards(this, new_turf);
                if (Dm.truthy(this._listen_lookup?[COMSIG_PROJECTILE_MOVE_PROCESS_STEP]))
                    this._SendSignal(COMSIG_PROJECTILE_MOVE_PROCESS_STEP, Dm.list(this));
                // We hit something and got deleted, stop the loop
                if (QDELETED(this))
                    return movements_done;
                if (loc != new_turf)
                    moving_turfs = FALSE;
                // If we've impacted something, we need to animate our movement until the actual hit
                // Otherwise the projectile visually disappears slightly before the actual impact
                // Not if we're hitscan, however, microop time!
                if (Dm.truthy(deletion_queued) && !Dm.truthy(hitscan))
                {
                    // distance_to_move is how much we have to step to get to the next turf, hypotenuse is how much we need
                    // to move in the next turf to get from entry to impact position
                    delete_distance = distance_to_move + sqrt(Dm.pow(Dm.sub(impact_x, entry_x), 2) + Dm.pow(Dm.sub(impact_y, entry_y), 2));
                }
            }

            if (floor_hit)
            {
                pixel_x = Dm.num(entry_x); // the crossing point, where the turf's impact is made
                pixel_y = Dm.num(entry_y);
                // DEVIATION(DV394): over open space the shot flies on into the level under (or over) it
                if (!flight_hit && await ShotGeometry.PassLevel(this))
                {
                    movements_done += 1;
                    Dm.remove(ref pixels_to_move, distance_to_move);
                    continue;
                }
                if (QDELETED(this))
                    return movements_done;
                // DEVIATION(DV414): a bullet bounces off the floor or the ceiling as off a wall
                if (!flight_hit && ShotGeometry.Bounce(this))
                {
                    movements_done += 1;
                    Dm.remove(ref pixels_to_move, distance_to_move);
                    continue;
                }
                atom hit_surface = loc as atom;
                turf top_of = null; // DEVIATION(DV578): the cell whose top the shot hit, where a blast it sets off goes off
                if (!flight_hit && dm_slope > 0)
                {
                    // the top of the level: the floor of the level over the cell (D325: the turf above is its boundary), which the shot hits from
                    // under it as it hits the floor it falls on; or the cell's own ceiling (D295: a floor block of the ceiling system, no atom: its
                    // hit on the cell, ShotGeometry.HitCeiling); nothing over the cell (open to the sky): the shot flies away
                    switch (ShotGeometry.Overhead(loc as turf, out turf above))
                    {
                        case ShotGeometry.Top.FloorAbove:
                            hit_surface = above;
                            top_of = loc as turf;
                            break;
                        case ShotGeometry.Top.Ceiling when ShotGeometry.ThroughGlassCeiling(this, loc as turf):
                            qdel(this);
                            return movements_done;
                        case ShotGeometry.Top.Ceiling:
                            await TopBlast.Hitting(loc as turf, null, () => ShotGeometry.HitCeiling(this));
                            qdel(this);
                            return movements_done;
                        case ShotGeometry.Top.None:
                            qdel(this);
                            return movements_done;
                    }
                }
                if (top_of != null)
                    await TopBlast.Hitting(top_of, hit_surface as turf, async () => await impact(hit_surface));
                else
                    await impact(hit_surface);
                if (QDELETED(this))
                    return movements_done;
                if (!Dm.truthy(deletion_queued))
                {
                    qdel(this); // the floor did not stop it (already hit, ricochet): a shot cannot fly on under the floor
                    return movements_done;
                }
                pixel_x -= Dm.num(movement_vector.pixel_x) * distance_to_move; // back to the step's start, so the delete animation runs to the crossing
                pixel_y -= Dm.num(movement_vector.pixel_y) * distance_to_move;
                delete_distance = distance_to_move;
            }

            movements_done += 1;
            // We cannot move more than one turf worth of distance per loop, so this is a safe solution
            pixels_moved_last_tile += distance_to_move;
            if (!Dm.truthy(deletion_queued) && pixels_moved_last_tile >= ICON_SIZE_ALL)
            {
                await reduce_range();
                if (QDELETED(this))
                    return movements_done;
                // Similarly with range out deletion, need to calculate how many pixels we can actually move before deleting
                if (Dm.truthy(deletion_queued))
                    delete_distance = distance_to_move - (ICON_SIZE_ALL - pixels_moved_last_tile);
            }

            if (Dm.truthy(deletion_queued))
            {
                // Hitscans don't need to wait before deleting
                if (Dm.truthy(hitscan))
                    return movements_done;

                // We moved to the next turf first, then impacted something
                // This means that we need to offset our visual position back to the previous turf, then figure out
                // how much we moved on the next turf (or we didn't move at all in which case we both shifts are 0 anyways)
                if (Dm.truthy(moving_turfs))
                {
                    pixel_x -= x_shift * ICON_SIZE_X;
                    pixel_y -= y_shift * ICON_SIZE_Y;
                }

                // Similarly to normal animate code, but use lowered deletion distance instead.
                float delete_x = pixel_x + Dm.num(movement_vector.pixel_x) * delete_distance;
                float delete_y = pixel_y + Dm.num(movement_vector.pixel_y) * delete_distance;
                // In order to keep a consistent speed, calculate at what point between ticks we get deleted
                float animate_time = Dm.div(world.tick_lag * delete_distance, total_move_distance);
                // Sometimes we need to move *just a bit* more than we can afford this tick - in this case, delete a tick after
                // so we don't disappear before impact. This shouldn't be more than 1, ever.
                if (Dm.lt(pixels_to_move, delete_distance))
                    ticks_to_deletion += 1;
                // We can use animation chains to visually disappear between ticks.
                if (!Dm.truthy(move_animate(delete_x, delete_y, animate_time, deleting: TRUE)))
                {
                    animate(this, Dm.named("pixel_x", delete_x), Dm.named("pixel_y", delete_y), Dm.named("time", animate_time), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_CONTINUE)));
                    animate(Dm.named("alpha", 0), Dm.named("time", 0), Dm.named("flags", ANIMATION_CONTINUE));
                }
                return movements_done;
            }

            Dm.remove(ref pixels_to_move, distance_to_move);
            // animate() instantly changes pixel_x/y values and just interpolates them client-side so next loop processes properly
            if (Dm.truthy(hitscan))
            {
                pixel_x = Dm.num(entry_x);
                pixel_y = Dm.num(entry_y);
            }
            else
            {
                // We need to shift back to the tile we were on before moving
                pixel_x -= x_shift * ICON_SIZE_X;
                pixel_y -= y_shift * ICON_SIZE_Y;
                if (!Dm.truthy(move_animate(entry_x, entry_y)))
                    animate(this, Dm.named("pixel_x", entry_x), Dm.named("pixel_y", entry_y), Dm.named("time", Dm.div(world.tick_lag * distance_to_move, total_move_distance)), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_CONTINUE)));
            }

            // Homing caps our movement speed per loop while leaving per tick speed intact, so we can just call process_homing every loop here
            if (Dm.truthy(homing))
                process_homing();

            // We've hit a timestop field, abort any remaining movement
            if (Dm.truthy(paused))
                return movements_done;

            // Prevents long-range high-speed projectiles from ruining the server performance by moving 100 tiles per tick when subsystem is set to a high cap
            if (TICK_CHECK())
            {
                // If we ran out of time, add whatever distance we're yet to pass to overrun debt to be processed next tick and break the loop
                overrun += Dm.num(pixels_to_move);
                return movements_done;
            }

            if (Dm.truthy(tile_limit) && Dm.truthy(moving_turfs))
                return movements_done;
        }

        return movements_done;
    }

    /// Called every time projectile animates its movement, in case child wants to have custom animations.
    /// Returning TRUE cancels normal animation
    // tg: code/modules/projectiles/projectile.dm:1084
    public virtual object move_animate(object animate_x = null, object animate_y = null, object animate_time = null, float? deleting = null)
    {
        animate_time ??= world.tick_lag;
        deleting ??= FALSE;
        return FALSE;
    }

    /// Called every projectile loop for homing or alternatively, custom trajectory changes.
    // tg: code/modules/projectiles/projectile.dm:1088
    public virtual void process_homing()
    {
        if (!Dm.truthy(homing_target))
            return;
        datum_point new_point = RETURN_PRECISE_POINT(homing_target);
        // DEVIATION(DV96): a 3D shot aims its offset at the body's scale: tg's 4 to 10 pixels off the middle stay in a 2D tile, but miss a man's
        // drawn body, a few pixels wide side on
        float offset_scale = dm_shot ? ShotGeometry.HomingOffsetScale : 1f;
        Dm.append(ref new_point.pixel_x, homing_offset_x * offset_scale);
        Dm.append(ref new_point.pixel_y, homing_offset_y * offset_scale);
        object new_angle = closer_angle_difference(angle, angle_between_points(RETURN_PRECISE_POINT(this), new_point));
        set_angle(Dm.add(angle, clamp(new_angle, -homing_turn_speed, homing_turn_speed)));
        ShotGeometry.Home(this, homing_target, homing_turn_speed); // DEVIATION(DV96): it homes in 3D
    }

    /// Attempts to force the projectile to move until the subsystem runs out of processing time, the projectile impacts something or gets frozen by timestop
    // tg: code/modules/projectiles/projectile.dm:1098
    public virtual async DmTask process_hitscan()
    {
        if (movement_vector == null)
        {
            qdel(this);
            return;
        }

        while (isturf(loc) && !QDELETED(this))
        {
            await process_movement(ICON_SIZE_ALL, hitscan: TRUE);

            if (QDELETED(this))
                return;

            if (!TICK_CHECK() && !Dm.truthy(paused))
                continue;

            create_hitscan_point();
            // Create tracers if we get timestopped or lagchunk so there aren't weird delays
            await generate_hitscan_tracers(impact_point: FALSE, impact_visual: FALSE);
            record_hitscan_start(offset: FALSE);
            return;
        }
    }

    /// Creates (or wipes clean) list of tracer keypoints and creates a first point.
    // tg: code/modules/projectiles/projectile.dm:1119
    public virtual void record_hitscan_start(float? offset = null)
    {
        offset ??= TRUE;
        if (beam_points == null)
            beam_points = Dm.list();
        else
        {
            QDEL_LIST_ASSOC(beam_points);
            QDEL_NULL(ref last_point);
        }
        last_point = RETURN_PRECISE_POINT(this);
        // If moving, increment its position a bit to prevent it from looking like its coming from firer's ass
        if (Dm.truthy(offset) && movement_vector != null)
            last_point.increment(Dm.num(movement_vector.pixel_x) * MUZZLE_EFFECT_PIXEL_INCREMENT, Dm.num(movement_vector.pixel_y) * MUZZLE_EFFECT_PIXEL_INCREMENT);
        beam_points[last_point] = null;
    }

    /// Creates a new keypoint in which the tracer will split
    // tg: code/modules/projectiles/projectile.dm:1132
    public virtual void create_hitscan_point(float? impact = null, float? tile_center = null, float? broken_segment = null)
    {
        impact ??= FALSE;
        tile_center ??= FALSE;
        broken_segment ??= FALSE;
        atom handle_atom = (last_impact_turf is var or__1 && Dm.truthy(or__1) ? (atom)or__1 : this);
        atom used_point = (Dm.truthy(tile_center) ? loc : this);
        datum_point new_point = (Dm.truthy(impact) ? Dm.@new<datum_point>(handle_atom.x, handle_atom.y, handle_atom.z, impact_x, impact_y) : RETURN_PRECISE_POINT(used_point));
        if (!Dm.truthy(broken_segment))
            beam_points[last_point] = new_point;
        beam_points[new_point] = null;
        last_point = new_point;
    }

    // tg: code/modules/projectiles/projectile.dm:1141
    [DmParams("target")]
    public override async DmTask<object> forceMove(atom target = null, params object[] __extra)
    {
        object dot = null;
        if (!Dm.truthy(hitscan) || beam_points == null)
            return await base.forceMove(target, __extra);
        create_hitscan_point();
        dot = await base.forceMove(target, __extra);
        if (!isturf(loc) || !isturf(target) || !Dm.truthy(z) || QDELETED(this) || Dm.truthy(deletion_queued))
            return dot;
        if (movement_vector == null || Dm.truthy(free_hitscan_forceMove))
            return dot;
        // Create firing VFX and start a new chain because we most likely got teleported
        await generate_hitscan_tracers(impact_point: FALSE);
        original_angle = Dm.num(angle);
        spawned_muzzle = FALSE;
        record_hitscan_start(offset: FALSE);
        return dot;
    }

    // tg: code/modules/projectiles/projectile.dm:1156
    public virtual async DmTask generate_hitscan_tracers(float? impact_point = null, float? impact_visual = null)
    {
        impact_point ??= TRUE;
        impact_visual ??= TRUE;
        if (!Dm.truthy(length(beam_points)))
            return;

        if (Dm.truthy(impact_point))
            create_hitscan_point(impact: TRUE);

        if (Dm.truthy(tracer_type))
        {
            // Stores all turfs we've created light effects on, in order to not dupe them if we enter a reflector loop
            // Uses an assoc list for performance reasons
            DmList passed_turfs = Dm.list();
            foreach (object beam_point in Dm.each(beam_points))
                generate_tracer(beam_point as datum_point, passed_turfs);
        }

        if (Dm.truthy(muzzle_type) && !Dm.truthy(spawned_muzzle))
        {
            spawned_muzzle = TRUE;
            datum_point start_point = beam_points[1] as datum_point;
            atom_movable muzzle_effect = Dm.@new(muzzle_type, loc) as atom_movable;
            await start_point.move_atom_to_src(muzzle_effect);
            ShotGeometry.CopyLine(this, muzzle_effect); // protocol version 34: drawn at the shot's height
            matrix matrix = Dm.matrix();
            matrix.Turn(original_angle);
            muzzle_effect.transform = matrix;
            muzzle_effect.color = color;
            await muzzle_effect.set_light(muzzle_flash_range, muzzle_flash_intensity, (muzzle_flash_color_override is var or__1 && Dm.truthy(or__1) ? or__1 : color));
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), PROJECTILE_TRACER_DURATION > GC_FILTER_QUEUE ? (object)WEAKREF(muzzle_effect) : muzzle_effect), PROJECTILE_TRACER_DURATION, file: "code/modules/projectiles/projectile.dm", line: 1180); // QDEL_IN(muzzle_effect, PROJECTILE_TRACER_DURATION), inline (rules 11.1 (f))
        }

        if (Dm.truthy(impact_type) && Dm.truthy(impact_visual))
        {
            atom_movable impact_effect = Dm.@new(impact_type, loc) as atom_movable;
            await last_point.move_atom_to_src(impact_effect);
            ShotGeometry.MarkImpact(this, impact_effect); // protocol version 34: drawn at the shot's height
            matrix matrix = Dm.matrix();
            matrix.Turn(Dm.num(angle));
            impact_effect.transform = matrix;
            impact_effect.color = color;
            await impact_effect.set_light(impact_light_range, impact_light_intensity, (impact_light_color_override is var or__2 && Dm.truthy(or__2) ? or__2 : color));
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), PROJECTILE_TRACER_DURATION > GC_FILTER_QUEUE ? (object)WEAKREF(impact_effect) : impact_effect), PROJECTILE_TRACER_DURATION, file: "code/modules/projectiles/projectile.dm", line: 1190); // QDEL_IN(impact_effect, PROJECTILE_TRACER_DURATION), inline (rules 11.1 (f))
        }
    }

    // tg: code/modules/projectiles/projectile.dm:1192
    public virtual void generate_tracer(datum_point start_point = null, DmList passed_turfs = null)
    {
        if (beam_points[start_point] == null)
            return;

        datum_point end_point = beam_points[start_point] as datum_point;
        datum_point midpoint = point_midpoint_points(start_point, end_point) as datum_point;
        obj_effect_projectile_tracer tracer_effect = Dm.@new(tracer_type, midpoint.return_turf()) as obj_effect_projectile_tracer;
        ShotGeometry.CopyLine(this, tracer_effect); // protocol version 34: drawn at the shot's height
        tracer_effect.apply_vars(
            angle_override: angle_between_points(start_point, end_point),
            p_x: midpoint.pixel_x,
            p_y: midpoint.pixel_y,
            color_override: color,
            scaling: Dm.num(pixel_length_between_points(start_point, end_point)) / ICON_SIZE_ALL
        );
        SET_PLANE_EXPLICIT(tracer_effect, GAME_PLANE, this);

        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), PROJECTILE_TRACER_DURATION > GC_FILTER_QUEUE ? (object)WEAKREF(tracer_effect) : tracer_effect), PROJECTILE_TRACER_DURATION, file: "code/modules/projectiles/projectile.dm", line: 1208); // QDEL_IN(tracer_effect, PROJECTILE_TRACER_DURATION), inline (rules 11.1 (f))

        if (!Dm.truthy(hitscan_light_range) || !Dm.truthy(hitscan_light_intensity))
            return;

        DmList light_line = get_line(start_point.return_turf() as atom, end_point.return_turf() as atom) as DmList;
        foreach (turf light_turf in Dm.each_cast<turf>(light_line))
        {
            if (Dm.truthy(passed_turfs[light_turf]))
                continue;
            passed_turfs[light_turf] = TRUE;
            obj_effect_abstract_projectile_lighting t__1 = Dm.@new<obj_effect_abstract_projectile_lighting>(light_turf, (hitscan_light_color_override is var or__1 && Dm.truthy(or__1) ? or__1 : color), hitscan_light_range, hitscan_light_intensity);
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), PROJECTILE_TRACER_DURATION > GC_FILTER_QUEUE ? (object)WEAKREF(t__1) : t__1), PROJECTILE_TRACER_DURATION, file: "code/modules/projectiles/projectile.dm", line: 1218); // QDEL_IN(new /obj/effect/abstract/projectile_lighting(...), PROJECTILE_TRACER_DURATION), inline (rules 11.1 (f))
        }
    }

    /**
     * Aims the projectile at a target.
     *
     * Must be passed at least one of a target or a list of click parameters.
     * If only passed the click modifiers the source atom must be a mob with a client.
     *
     * Arguments:
     * - [target][/atom]: (Optional) The thing that the projectile will be aimed at.
     * - [source][/atom]: The initial location of the projectile or the thing firing it.
     * - [modifiers][/list]: (Optional) A list of click parameters to apply to this operation.
     * - deviation: (Optional) How the trajectory should deviate from the target in degrees.
     *   - //Spread is FORCED!
     */
    // tg: code/modules/projectiles/projectile.dm:1233
    public virtual async DmTask<object> aim_projectile(atom target = null, atom source = null, DmList modifiers = null, float? deviation = null)
    {
        deviation ??= 0;
        if (!(modifiers == null || islist(modifiers)))
        {
            stack_trace(Dm.text("WARNING: Projectile [] fired with non-list modifiers, likely was passed click params. Modifiers were the following: []", type, modifiers), "code/modules/projectiles/projectile.dm", 1235);
            modifiers = null;
        }

        turf source_loc = get_turf(source);
        turf target_loc = get_turf(target);

        if (source_loc == null)
        {
            stack_trace(Dm.text("WARNING: Projectile [] fired from nullspace.", type), "code/modules/projectiles/projectile.dm", 1242);
            qdel(this);
            return FALSE;
        }

        if (Dm.truthy(fired))
        {
            stack_trace(Dm.text("WARNING: Projectile [] was aimed after already being fired.", type), "code/modules/projectiles/projectile.dm", 1247);
            qdel(this);
            return FALSE;
        }

        free_hitscan_forceMove = TRUE;
        await forceMove(source_loc);
        starting = source_loc;
        pixel_x = source.pixel_x - source.base_pixel_x;
        pixel_y = source.pixel_y - source.base_pixel_y;
        original = target;

        // Trim off excess pixel_x/y by converting them into turf offset
        if (abs(pixel_x) > (float)ICON_SIZE_X / 2)
        {
            for (float i = 1, i__end = floor(abs(pixel_x) + (float)ICON_SIZE_X / 2) / ICON_SIZE_X; i <= i__end; i++)
            {
                turf new_loc = get_step(source_loc, pixel_x > 0 ? EAST : WEST);
                if (!istype(new_loc, typeof(turf)))
                    break;
                source_loc = new_loc;
            }
            pixel_x = Dm.mod(pixel_x, (float)ICON_SIZE_X / 2);
        }

        if (abs(pixel_y) > (float)ICON_SIZE_Y / 2)
        {
            for (float i = 1, i__end = floor(abs(pixel_y) + (float)ICON_SIZE_Y / 2) / ICON_SIZE_Y; i <= i__end; i++)
            {
                turf new_loc = get_step(source_loc, pixel_y > 0 ? NORTH : SOUTH);
                if (!istype(new_loc, typeof(turf)))
                    break;
                source_loc = new_loc;
            }
            pixel_y = Dm.mod(pixel_y, (float)ICON_SIZE_X / 2);
        }

        // We've got moved by turf offsets
        if (starting != source_loc)
        {
            starting = source_loc;
            await forceMove(source_loc);
        }

        // DEVIATION(DV94): a shot aimed by a player's camera starts at the eye: its tile (the source's, or an open one next to it) with the
        // eye's offset in it, and its line takes the eye's height and the crosshair's pitch
        bool aimed = ClientSeam.TryReadAim(modifiers, out double aim_east, out double aim_north, out double aim_up, out double aim_yaw, out double aim_pitch);
        if (aimed)
        {
            turf eye_turf = locate((float)Math.Floor(aim_east / ICON_SIZE_X + 0.5), (float)Math.Floor(aim_north / ICON_SIZE_Y + 0.5), source_loc.z);
            if (eye_turf == null || (eye_turf != source_loc && (get_dist(eye_turf, source_loc) > 1 || Dm.truthy(eye_turf.density))))
                eye_turf = source_loc;
            if (eye_turf != starting)
            {
                starting = eye_turf;
                await forceMove(eye_turf);
            }
            pixel_x = (float)Math.Clamp(aim_east - Dm.num(eye_turf.x) * ICON_SIZE_X, -(float)ICON_SIZE_X / 2, (float)ICON_SIZE_X / 2);
            pixel_y = (float)Math.Clamp(aim_north - Dm.num(eye_turf.y) * ICON_SIZE_Y, -(float)ICON_SIZE_Y / 2, (float)ICON_SIZE_Y / 2);
            dm_aimed = true;
            dm_shot = true;
            dm_origin_x = Dm.num(eye_turf.x) * ICON_SIZE_X + pixel_x;
            dm_origin_y = Dm.num(eye_turf.y) * ICON_SIZE_Y + pixel_y;
            dm_origin_h = (float)Math.Clamp(aim_up, 0, 2 * ShotGeometry.CeilingPixels);
            dm_rewind = ClientSeam.ReadRewind(modifiers); // DEVIATION(DV527): the others are tested where the shooter saw them
            // DEVIATION(DV526): an eye high on the stairs' flight is over its level's top: the shot starts on the level above
            await ShotGeometry.StartOnLevelAbove(this);
        }

        if (Dm.truthy(length(modifiers)))
        {
            DmList calculated = calculate_projectile_angle_and_pixel_offsets(source, (target_loc is var and__1 && !Dm.truthy(and__1) ? (atom)and__1 : target), modifiers) as DmList;
            p_x = Dm.num_or_null(calculated[2]);
            p_y = Dm.num_or_null(calculated[3]);
            if (aimed)
            {
                // DEVIATION(DV96): tg's deviation turns the 3D aim by its size about a drawn direction (a cone, not a fan)
                (double yaw, double pitch) = ShotGeometry.Scatter(Dm.num(calculated[1]), aim_pitch, deviation.Value);
                dm_slope = ShotGeometry.SlopeOf(pitch);
                set_angle((float)yaw);
                return TRUE;
            }
            set_angle(Dm.add(calculated[1], deviation));
            return TRUE;
        }

        if (Dm.truthy(target_loc))
        {
            yo = target_loc.y - source_loc.y;
            xo = target_loc.x - source_loc.x;
            set_angle(Dm.add(get_angle(this, target_loc), deviation));
            return TRUE;
        }

        stack_trace(Dm.text("WARNING: Projectile [] fired without a target or mouse parameters to aim with.", type), "code/modules/projectiles/projectile.dm", 1293);
        qdel(this);
        return FALSE;
    }

    // tg: code/modules/projectiles/projectile.dm:1345
    public override void experience_pressure_difference(object pressure_difference = null, object direction = null, float? pressure_resistance_prob_delta = null)
    {
        return;
    }

    /**
     * Is this projectile considered "hostile"?
     *
     * By default all projectiles which deal damage or impart crowd control effects (including stamina) are hostile
     *
     * This is NOT used for pacifist checks, that's handled by [/obj/item/ammo_casing/var/harmful]
     * This is used in places such as AI responses to determine if they're being threatened or not (among other places)
     */
    // tg: code/modules/projectiles/projectile.dm:1356
    public virtual object is_hostile_projectile()
    {
        if (Dm.num(damage) > 0 || stamina > 0)
            return TRUE;

        if (paralyze + stun + immobilize + knockdown > 0 * SECONDS)
            return TRUE;

        return FALSE;
    }

    ///Checks if the projectile can embed into someone
    // tg: code/modules/projectiles/projectile.dm:1366
    public virtual object can_embed_into(atom hit = null)
    {
        return (shrapnel_type is var and__1 && !Dm.truthy(and__1) ? (object)and__1 : (get_embed() as datum_embedding)?.can_embed(this, hit as mob_living_carbon));
    }

    /// Reflects the projectile off of something
    // tg: code/modules/projectiles/projectile.dm:1370
    public virtual void reflect(atom hit_atom = null)
    {
        if (!Dm.truthy(starting))
            return;
        float new_x = starting.x + Dm.num(pick(0, 0, 0, 0, 0, -1, 1, -2, 2));
        float new_y = starting.y + Dm.num(pick(0, 0, 0, 0, 0, -1, 1, -2, 2));
        turf current_tile = get_turf(hit_atom);

        // redirect the projectile
        original = locate(new_x, new_y, z);
        starting = current_tile;
        firer = hit_atom as atom_movable;
        yo = new_y - current_tile.y;
        xo = new_x - current_tile.x;
        object new_angle_s = Dm.add(angle, rand(120, 240));
        while (Dm.gt(new_angle_s, 180)) // Translate to regular projectile degrees
            Dm.remove(ref new_angle_s, 360);
        set_angle(new_angle_s);
    }

    /// Fetches, or lazyloads, our embedding datum
    // tg: code/modules/projectiles/projectile.dm:1411
    public virtual object get_embed()
    {
        if (Dm.truthy(embed_data))
            return embed_data;
        if (Dm.truthy(embed_type))
            embed_data = Dm.@new(embed_type) as datum_embedding;
        return embed_data;
    }

    /// Sets our embedding datum to a different one. Can also take types
    // tg: code/modules/projectiles/projectile.dm:1420
    public virtual void set_embed(object new_embed = null, float? dont_delete = null)
    {
        dont_delete ??= FALSE;
        if (Dm.eq(new_embed, embed_data))
            return;

        if (embed_data != null && !Dm.truthy(dont_delete))
            qdel(embed_data);

        if (ispath(new_embed))
            new_embed = Dm.@new(new_embed as Type);

        embed_data = new_embed as datum_embedding;
    }
}

// tg: code/modules/projectiles/projectile.dm:1306 (a global proc between the projectile's procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    /**
     * Calculates the pixel offsets and angle that a projectile should be launched at.
     *
     * Arguments:
     * - [source][/atom]: The thing that the projectile is being shot from.
     * - [target][/atom]: (Optional) The thing that the projectile is being shot at.
     *   - If this is not provided the  source atom must be a mob with a client.
     * - [modifiers][/list]: A list of click parameters used to modify the shot angle.
     */
    // tg: code/modules/projectiles/projectile.dm:1306
    public static object calculate_projectile_angle_and_pixel_offsets(atom source = null, atom target = null, object modifiers = null)
    {
        float angle = 0;
        object p_x = (Dm.truthy(LAZYACCESS(modifiers, ICON_X)) ? text2num(LAZYACCESS(modifiers, ICON_X)) : (float)ICON_SIZE_X / 2); // ICON_(X|Y) are measured from the bottom left corner of the icon.
        object p_y = (Dm.truthy(LAZYACCESS(modifiers, ICON_Y)) ? text2num(LAZYACCESS(modifiers, ICON_Y)) : (float)ICON_SIZE_Y / 2); // This centers the target if modifiers aren't passed.

        // DEVIATION(DV94): a shot aimed by a player's camera flies along the crosshair's bearing (clockwise from north, tg's angle)
        if (ClientSeam.TryReadAim(modifiers as DmList, out double _east, out double _north, out double _up, out double aim_yaw, out double _pitch))
            return Dm.list((float)aim_yaw, p_x, p_y);

        if (Dm.truthy(target))
        {
            turf source_loc = get_turf(source);
            turf target_loc = get_turf(target);
            float dx = ((target_loc.x - source_loc.x) * ICON_SIZE_X) + (target.pixel_x - source.pixel_x) + (Dm.num(p_x) - ((float)ICON_SIZE_X / 2));
            float dy = ((target_loc.y - source_loc.y) * ICON_SIZE_Y) + (target.pixel_y - source.pixel_y) + (Dm.num(p_y) - ((float)ICON_SIZE_Y / 2));

            angle = ATAN2(dy, dx);
            return Dm.list(angle, p_x, p_y);
        }

        if (!ismob(source) || !Dm.truthy(LAZYACCESS(modifiers, SCREEN_LOC)))
            throw Dm.CRASH("Can't make trajectory calculations without a target or click modifiers and a client.", "code/modules/projectiles/projectile.dm", 1321);

        mob user = source as mob;
        if (!Dm.truthy(user.client))
            throw Dm.CRASH("Can't make trajectory calculations without a target or click modifiers and a client.", "code/modules/projectiles/projectile.dm", 1325);

        //Split screen-loc up into X+Pixel_X and Y+Pixel_Y
        DmList screen_loc_params = splittext(LAZYACCESS(modifiers, SCREEN_LOC), ",") as DmList;
        //Split X+Pixel_X up into list(X, Pixel_X)
        DmList screen_loc_X = splittext(screen_loc_params[1], ":") as DmList;
        //Split Y+Pixel_Y up into list(Y, Pixel_Y)
        DmList screen_loc_Y = splittext(screen_loc_params[2], ":") as DmList;

        float tx = (Dm.num(text2num(screen_loc_X[1])) - 1) * ICON_SIZE_X + Dm.num(text2num(screen_loc_X[2]));
        float ty = (Dm.num(text2num(screen_loc_Y[1])) - 1) * ICON_SIZE_Y + Dm.num(text2num(screen_loc_Y[2]));

        //Calculate the "resolution" of screen based on client's view and world's icon size. This will work if the user can view more tiles than average.
        DmList screenview = view_to_pixels(user.client.view);

        float ox = round(Dm.num(screenview[1]) * 0.5f) - user.client.pixel_x; //"origin" x
        float oy = round(Dm.num(screenview[2]) * 0.5f) - user.client.pixel_y; //"origin" y
        angle = ATAN2(ty - oy, tx - ox);
        return Dm.list(angle, p_x, p_y);
    }
}

// tg: code/modules/projectiles/projectile.dm:1389 (interleaved types, rules 1.9)
public partial class atom : datum
{
    /// Fire a projectile from this atom at another atom
    // tg: code/modules/projectiles/projectile.dm:1389
    public virtual async DmTask<object> fire_projectile(object projectile_type = null, atom target = null, object sound = null, object firer = null, DmList ignore_targets = null)
    {
        ignore_targets ??= Dm.list();
        if (sound != null)
            playsound(this, sound, vol: 100, vary: TRUE);

        turf startloc = get_turf(this);
        obj_projectile bullet = Dm.@new(projectile_type as Type, startloc) as obj_projectile;
        bullet.starting = startloc;
        foreach (atom thing in Dm.each_cast<atom>(ignore_targets))
            bullet.impacted[WEAKREF(thing)] = TRUE;
        bullet.firer = (firer is var or__1 && Dm.truthy(or__1) ? or__1 : this) as atom_movable;
        bullet.fired_from = this;
        bullet.yo = target.y - startloc.y;
        bullet.xo = target.x - startloc.x;
        bullet.original = target;
        await bullet.aim_projectile(target, this);
        await bullet.fire();
        return bullet;
    }
}
