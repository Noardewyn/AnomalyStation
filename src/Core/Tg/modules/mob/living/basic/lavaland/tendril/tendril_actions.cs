// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/// Fires out two cross patterns of damaging tentacles which reel in anything they hit, then causes a followup attack
// tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:2
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_tendril_lash : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:15
    public override async DmTask<object> Activate(atom target = null)
    {
        disable_cooldown_actions();
        float range_of_projectile = Dm.num(Dm.initial(projectile_type, (obj_projectile p) => p.range));

        foreach (object swipe_dir in Dm.each(GLOB.cardinals))
        {
            turf_open line_turf = get_step(owner, Dm.num(swipe_dir)) as turf_open;
            for (float i = 1, i__end = range_of_projectile; i <= i__end; i++)
            {
                if (!(line_turf is turf_open) || Dm.truthy(await line_turf.is_blocked_turf(exclude_mobs: TRUE)))
                    break;
                obj_effect_temp_visual_telegraphing_line telegraph = Dm.@new<obj_effect_temp_visual_telegraphing_line>(line_turf);
                telegraph.dir = Dm.num(swipe_dir);
                line_turf = get_step(line_turf, Dm.num(swipe_dir)) as turf_open;
            }
        }

        // SLEEP_CHECK_DEATH(1.2f * SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1.2f * SECONDS);
        if (QDELETED(owner))
            return null;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return null;
        }

        foreach (object swipe_dir in Dm.each(GLOB.cardinals))
            shoot_projectile(get_turf(owner), get_step(owner, Dm.num(swipe_dir)), dir2angle(Dm.num(swipe_dir)), owner as mob);

        // SLEEP_CHECK_DEATH(1.8f * SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1.8f * SECONDS);
        if (QDELETED(owner))
            return null;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return null;
        }

        foreach (object swipe_dir in Dm.each(GLOB.diagonals))
        {
            turf_open line_turf = get_step(owner, Dm.num(swipe_dir)) as turf_open;
            for (float i = 1, i__end = range_of_projectile; i <= i__end; i++)
            {
                if (!(line_turf is turf_open) || Dm.truthy(await line_turf.is_blocked_turf(exclude_mobs: TRUE)))
                    break;
                obj_effect_temp_visual_telegraphing_line telegraph = Dm.@new<obj_effect_temp_visual_telegraphing_line>(line_turf);
                telegraph.dir = Dm.num(swipe_dir);
                line_turf = get_step(line_turf, Dm.num(swipe_dir)) as turf_open;
            }
        }

        // SLEEP_CHECK_DEATH(1.2f * SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1.2f * SECONDS);
        if (QDELETED(owner))
            return null;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return null;
        }

        foreach (object swipe_dir in Dm.each(GLOB.diagonals))
            shoot_projectile(get_turf(owner), get_step(owner, Dm.num(swipe_dir)), dir2angle(Dm.num(swipe_dir)), owner as mob);

        // SLEEP_CHECK_DEATH(1.2f * SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1.2f * SECONDS);
        if (QDELETED(owner))
            return null;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return null;
        }
        StartCooldown();
        // SLEEP_CHECK_DEATH(0.6f * SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(0.6f * SECONDS);
        if (QDELETED(owner))
            return null;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return null;
        }
        enable_cooldown_actions();
        return TRUE;
    }
}

// tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:54
public partial class obj_projectile_tentacle_lash : obj_projectile
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:82
    public override async DmTask fire(object fire_angle = null, atom direct_target = null)
    {
        await base.fire(fire_angle, direct_target);
        if (firer == null)
            return;
        tentacle_beam = Beam(firer as atom, icon_state: "goliath_tentacle", beam_type: Dm.truthy(reel_in) ? typeof(obj_effect_ebeam_reacting) : typeof(obj_effect_ebeam), emissive: NONE) as datum_beam;
        if (Dm.truthy(reel_in))
            RegisterSignal(tentacle_beam, COMSIG_BEAM_ENTERED, nameof(on_beam_entered));
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:90
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref tentacle_beam);
        if (QDELETED(firer) || !isturf(loc))
            return base.Destroy(force);
        // Animate tentacle retraction for visual flair
        turf our_turf = loc as turf;
        turf firer_turf = get_turf(firer as atom);
        obj_effect_abstract holder = Dm.@new<obj_effect_abstract>(firer_turf);
        holder.icon = icon;
        holder.icon_state = icon_state;
        holder.transform = transform;
        holder.pixel_x = (firer_turf.x - our_turf.x) * ICON_SIZE_X + Dm.num(pixel_x);
        holder.pixel_y = (firer_turf.y - our_turf.y) * ICON_SIZE_Y + Dm.num(pixel_y);
        holder.Beam(firer as atom, icon_state: "goliath_tentacle", emissive: NONE);
        float anim_time = sqrt(Dm.num(Dm.pow(Dm.num(holder.pixel_x) / ICON_SIZE_X, 2)) + Dm.num(Dm.pow(Dm.num(holder.pixel_y) / ICON_SIZE_Y, 2))) / (Dm.num(speed) * Dm.num(SSprojectiles.wait) * retract_speed);
        animate(holder, Dm.named("pixel_x", 0), Dm.named("pixel_y", 0), Dm.named("time", anim_time));
        // QDEL_IN_CLIENT_TIME(holder, anim_time) (__DEFINES/qdel.dm:62), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), anim_time > GC_FILTER_QUEUE ? WEAKREF(holder) : holder), anim_time, Dm.bitor(TIMER_STOPPABLE, TIMER_CLIENT_TIME), file: "code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm", line: 106);
        return base.Destroy(force);
    }

    // Don't range out, stop and persist until we're done
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:110
    public override async DmTask on_range()
    {
        STOP_PROCESSING(SSprojectiles, this);
        // Reset to tile center
        pixel_x = 0;
        pixel_y = 0;
        // QDEL_IN(src, duration) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
        float t__1 = Dm.num(duration);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), t__1 > GC_FILTER_QUEUE ? WEAKREF(this) : this), t__1, file: "code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm", line: 115);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:117
    public override async DmTask<object> prehit_pierce(atom target = null)
    {
        // Ye 'ole colossus cheese
        if ((target as mob_living)?.stat == DEAD)
            return PROJECTILE_PIERCE_PHASE;
        return await base.prehit_pierce(target);
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:123
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!Dm.truthy(reel_in) || !isliving(target) || Dm.num(blocked) >= 100 || Dm.truthy(pierce_hit) || !Dm.eq(dot, BULLET_ACT_HIT))
            return dot;
        await snatch_target(target as mob_living);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:129
    [SignalHandler]
    public virtual void on_beam_entered(datum_beam source = null, obj_effect_ebeam hit = null, atom_movable entered = null)
    {
        if (!Dm.truthy(reel_in) || entered == firer || !isliving(entered))
            return;

        mob_living victim = entered as mob_living;
        if ((firer == null || !Dm.truthy(((atom_movable)firer).faction_check_atom(victim))) && victim.stat != DEAD)
            Waiting.Detach(snatch_target(entered as mob_living));
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:139
    public virtual async DmTask snatch_target(mob_living victim = null)
    {
        if (HAS_TRAIT(victim, TRAIT_TENTACLE_IMMUNE) || Dm.truthy(Dm.bitand(victim.SEND_SIGNAL(COMSIG_TENDRIL_TENTACLED_GRABBED, Dm.list(victim)), COMPONENT_TENDRIL_CANCEL_TENTACLE_GRAB)))
            return;

        to_chat(victim, span_userdanger(I18n.T("You're snatched by []'s tentacles!", firer)));
        await victim.apply_damage(snatch_damage, BRUTE, BODY_ZONE_CHEST, wound_bonus: CANT_WOUND);
        if (QDELETED(victim))
        {
            qdel(this);
            return;
        }
        datum_callback snatch_callback = null;
        if (firer is mob_living_basic_mining_tendril tendril)
            snatch_callback = Dm.@new<datum_callback>(tendril, nameof(mob_living_basic_mining_tendril.snatch_react));
        await victim.throw_at(firer as atom, Dm.initial(this, (obj_projectile p) => p.range), 1, firer as atom, FALSE, gentle: TRUE, callback: snatch_callback);
        playsound(victim, hitsound, 50, -3, pressure_affected: FALSE);
        qdel(this);
    }
}

/// An ability which makes spikes come out of the ground towards your target
// tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:157
public partial class datum_action_cooldown_mob_cooldown_tendril_chaser : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:172
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        RegisterSignal(granted_to, COMSIG_MOB_ABILITY_STARTED, nameof(on_ability_started));
        RegisterSignal(granted_to, COMSIG_MOB_ABILITY_FINISHED, nameof(on_ability_finished));
        return dot;
    }

    // Clean up after ourselves
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:178
    public override async DmTask Remove(mob removed_from = null)
    {
        UnregisterSignal(removed_from, Dm.list(COMSIG_MOB_ABILITY_STARTED, COMSIG_MOB_ABILITY_FINISHED));
        // tg's list holds weak references, so QDEL_LIST deletes the references and leaves the trails running; kept as tg has it
        QDEL_LIST(active_chasers);
        await base.Remove(removed_from);
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:183
    [SignalHandler]
    public virtual void on_ability_started(mob_living owner = null, datum_action_cooldown activated = null)
    {
        // Delete all of our chasers when our owner triggers cross spikes as to not cause guaranteed damage
        if (activated is datum_action_cooldown_mob_cooldown_tendril_cross_spikes)
            QDEL_LIST(active_chasers);
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:190
    [SignalHandler]
    public virtual void on_ability_finished(mob_living owner = null, datum_action_cooldown activated = null)
    {
        if (activated is datum_action_cooldown_mob_cooldown_tendril_cross_spikes)
            ResetCooldown();
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:196
    public override async DmTask<object> Activate(atom target = null)
    {
        disable_cooldown_actions();
        Type primary_type = typeof(obj_effect_temp_visual_effect_trail_tendril_chaser);
        if (isliving(owner))
        {
            mob_living as_living = owner as mob_living;
            if (Dm.num(as_living.health) / Dm.num(as_living.maxHealth) <= Dm.num(wide_chaser_threshold))
                primary_type = typeof(obj_effect_temp_visual_effect_trail_tendril_chaser_wide_area);
        }

        obj_effect_temp_visual_effect_trail_tendril_chaser chaser = Dm.@new(primary_type, get_turf(owner), target) as obj_effect_temp_visual_effect_trail_tendril_chaser;
        LAZYADD(ref active_chasers, WEAKREF(chaser));
        RegisterSignal(chaser, COMSIG_QDELETING, nameof(on_chaser_destroyed));
        playsound(owner, Dm.resource("sound/effects/magic/demon_attack1.ogg"), vol: 100, vary: TRUE, pressure_affected: FALSE);
        // SLEEP_CHECK_DEATH(0.6 SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(0.6f * SECONDS);
        if (QDELETED(owner))
            return null;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return null;
        }
        StartCooldown();
        enable_cooldown_actions();
        return null;
    }

    /// Remove a spike trail from our list of active trails
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:213
    [SignalHandler]
    public virtual void on_chaser_destroyed(atom chaser = null)
    {
        LAZYREMOVE(ref active_chasers, WEAKREF(chaser));
    }
}

// tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:217
public partial class obj_effect_temp_visual_effect_trail_tendril_chaser : obj_effect_temp_visual_effect_trail
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:227
    public override async DmTask add_spawner()
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:230
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!Dm.truthy(area_spawn))
        {
            turf spawn_turf = get_turf(this);
            if (Dm.locate<obj_effect_temp_visual_emerging_ground_spike_tendril>(spawn_turf) == null && isopenturf(spawn_turf))
                Dm.@new(spawned_effect, spawn_turf, this);
            return dot;
        }

        foreach (object spawn_dir in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(Dm.num(spawn_dir), REVERSE_DIR(Dm.num(movement_dir)))))
                continue;
            turf spawn_loc = get_step(this, Dm.num(spawn_dir));
            if (Dm.locate<obj_effect_temp_visual_emerging_ground_spike_tendril>(spawn_loc) == null && isopenturf(spawn_loc))
                Dm.@new(spawned_effect, spawn_loc, this);
        }
        return dot;
    }
}

// tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:245
public partial class obj_effect_temp_visual_emerging_ground_spike_tendril : obj_effect_temp_visual_emerging_ground_spike
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:260
    public override async DmTask<object> impale()
    {
        object dot = await base.impale();
        hit_loser = Dm.num(Dm.bitor(hit_loser, dot));
        RegisterSignal(loc, COMSIG_ATOM_ENTERED, nameof(on_entered));
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_impaling)), hit_entry, file: "code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm", line: 264);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:266
    public virtual void stop_impaling()
    {
        UnregisterSignal(loc, COMSIG_ATOM_ENTERED);
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:269
    [SignalHandler]
    public virtual void on_entered(atom source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (!isliving(arrived))
            return;

        if (Dm.truthy(Waiting.NoSleep(harm_mob(arrived as mob_living))) && !Dm.truthy(hit_loser))
        {
            playsound(this, Dm.resource("sound/items/weapons/slice.ogg"), vol: 50, vary: TRUE, pressure_affected: FALSE);
            hit_loser = TRUE;
        }
    }
}

/// Subtype which deletes the chaser once it hits the target
// tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:287
public partial class obj_effect_temp_visual_emerging_ground_spike_tendril_chaser : obj_effect_temp_visual_emerging_ground_spike_tendril
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:291
    [DmParams("mapload", "spawner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object spawner = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        this.spawner = spawner as obj_effect_temp_visual_effect_trail_tendril_chaser;
        RegisterSignal(spawner as datum, COMSIG_QDELETING, nameof(on_spawner_destroy));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:296
    [SignalHandler]
    public virtual void on_spawner_destroy(datum source = null)
    {
        spawner = null;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:300
    public override object Destroy(float? force = null)
    {
        spawner = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:304
    public override async DmTask<object> harm_mob(mob_living victim = null)
    {
        // Don't multihit mobs by spikes from the same chaser
        if (spawner == null)
            return FALSE;
        object dot = await base.harm_mob(victim);
        if (Dm.truthy(dot))
            qdel(spawner);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:312
public partial class datum_action_cooldown_mob_cooldown_tendril_cross_spikes : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:328
    public override async DmTask<object> Activate(atom target = null)
    {
        disable_cooldown_actions();
        await spawn_spikes();
        // SLEEP_CHECK_DEATH(0.4 SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(0.4f * SECONDS);
        if (QDELETED(owner))
            return null;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return null;
        }
        await spawn_spikes(inverse: TRUE);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:337
    public virtual async DmTask spawn_spikes(float? inverse = null)
    {
        inverse ??= FALSE;
        DmList spike_turfs = Dm.list();
        turf owner_turf = get_turf(owner);

        float reduced_spawns = FALSE;
        if (isliving(owner))
        {
            mob_living as_living = owner as mob_living;
            if (Dm.num(as_living.health) / Dm.num(as_living.maxHealth) <= Dm.num(health_threshold))
                reduced_spawns = TRUE;
        }

        foreach (turf_open target_turf in Dm.each<turf_open>(oview(spike_range, owner_turf)))
        {
            if (sqrt(Dm.num(Dm.pow(target_turf.x - owner_turf.x, 2)) + Dm.num(Dm.pow(target_turf.y - owner_turf.y, 2))) > 9.5f) // big circle is a lie
                continue;

            if (Dm.truthy(reduced_spawns))
            {
                if (Dm.mod(abs(target_turf.x - owner_turf.x), 2) == Dm.mod(abs(target_turf.y - owner_turf.y + Dm.num(inverse)), 2))
                {
                    Dm.@new<obj_effect_temp_visual_telegraphing_circle_short>(target_turf);
                    Dm.append(spike_turfs, target_turf);
                }
                continue;
            }

            float row_diff = Dm.truthy(inverse) ? abs(target_turf.x - owner_turf.x) : abs(target_turf.y - owner_turf.y);
            float column_diff = Dm.truthy(inverse) ? abs(target_turf.y - owner_turf.y) : abs(target_turf.x - owner_turf.x);
            float row = floor((row_diff + 1) / 3);
            if (Dm.mod(row, 2) == 0)
            {
                if (row_diff - row * 3 == 0)
                {
                    if (Dm.mod(column_diff, 4) == 2)
                        continue;
                }
                else
                {
                    if (Dm.mod(column_diff, 4) != 0)
                        continue;
                }
            }
            else
            {
                if (row_diff - row * 3 == 0)
                {
                    if (Dm.mod(column_diff, 4) == 0)
                        continue;
                }
                else
                {
                    if (Dm.mod(column_diff, 4) != 2)
                        continue;
                }
            }

            Dm.@new<obj_effect_temp_visual_telegraphing_circle_short>(target_turf);
            Dm.append(spike_turfs, target_turf);
        }

        // SLEEP_CHECK_DEATH(1 SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1 * SECONDS);
        if (QDELETED(owner))
            return;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return;
        }

        foreach (turf_open to_spawn in Dm.each<turf_open>(spike_turfs))
            Dm.@new<obj_effect_temp_visual_emerging_ground_spike_tendril_single>(to_spawn);

        // SLEEP_CHECK_DEATH(0.8 SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(0.8f * SECONDS);
        if (QDELETED(owner))
            return;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return;
        }
    }
}

// tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:385
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_tendril_melee : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:398
    // tg declares Activate(atom/target_atom, warning = TRUE), the one override of the cooldown action's Activate with a second
    // argument; the port keeps the slot's signature and moves the body to stab, which the tendril's snatch calls with no warning.
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        return await stab(target_atom, TRUE);
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril_actions.dm:398 (the body of Activate)
    public virtual async DmTask<object> stab(atom target_atom = null, float? warning = null)
    {
        warning ??= TRUE;
        if (Dm.truthy(warning))
        {
            foreach (object stab_dir in Dm.each(GLOB.alldirs))
            {
                turf_open stab_turf = get_step(owner, Dm.num(stab_dir)) as turf_open;
                if (!(stab_turf is turf_open))
                    continue;
                obj_effect_temp_visual_telegraphing_line_short telegraph = Dm.@new<obj_effect_temp_visual_telegraphing_line_short>(stab_turf);
                telegraph.dir = Dm.num(stab_dir);
            }

            // SLEEP_CHECK_DEATH(0.5 SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(0.5f * SECONDS);
            if (QDELETED(owner))
                return null;
            if (ismob(owner))
            {
                mob sleep_check_death_mob = owner as mob;
                if (sleep_check_death_mob.stat == DEAD)
                    return null;
            }
        }

        foreach (object stab_dir in Dm.each(GLOB.alldirs))
            shoot_projectile(get_turf(owner), get_step(owner, Dm.num(stab_dir)), firer: owner as mob);

        // SLEEP_CHECK_DEATH(0.5 SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(0.5f * SECONDS);
        if (QDELETED(owner))
            return null;
        if (ismob(owner))
        {
            mob sleep_check_death_mob = owner as mob;
            if (sleep_check_death_mob.stat == DEAD)
                return null;
        }
        StartCooldown();
        return TRUE;
    }
}
