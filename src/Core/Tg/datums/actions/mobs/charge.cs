// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/charge.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate X2: the charger holoparasite's charge): only the charge (:1-181) and the basic charge (:183-229) translated; the
// post-charge tiredness and the triple, hallucination and other bosses' charges come with their mobs.

// tg: code/datums/actions/mobs/charge.dm:1
public partial class datum_action_cooldown_mob_cooldown_charge : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/charge.dm:24
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        // No charging and meleeing (overridded by StartCooldown after charge ends)
        next_melee_use_time = world.time + 100 * SECONDS;
        await charge_sequence(owner, target_atom, charge_delay, charge_past);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    // tg: code/datums/actions/mobs/charge.dm:33
    public virtual async DmTask charge_sequence(atom_movable charger = null, atom target_atom = null, object delay = null, object past = null)
    {
        await do_charge(owner, target_atom, charge_delay, charge_past);
    }

    // tg: code/datums/actions/mobs/charge.dm:36
    public virtual async DmTask<object> do_charge(atom_movable charger = null, atom target_atom = null, object delay = null, object past = null)
    {
        if (target_atom == null || target_atom == owner)
            return null;
        turf chargeturf = get_turf(target_atom) as turf;
        if (chargeturf == null)
            return null;
        object dir = get_dir(charger, target_atom);
        turf target = get_ranged_target_turf(chargeturf, dir, past) as turf;
        if (target == null)
            return null;

        if (Dm.@in(charger, charging))
        {
            // Stop any existing charging, this'll clean things up properly
            GLOB.move_manager.stop_looping(charger);
        }

        Dm.append(charging, charger);
        actively_moving = FALSE;
        // SEND_SIGNAL(owner, COMSIG_STARTED_CHARGE) (rules 11.1 (e), a statement)
        if (Dm.truthy(owner._listen_lookup?[COMSIG_STARTED_CHARGE]))
            owner._SendSignal(COMSIG_STARTED_CHARGE, Dm.list(owner));
        RegisterSignal(charger, COMSIG_MOVABLE_BUMP, nameof(on_bump), @override: TRUE);
        RegisterSignal(charger, COMSIG_MOVABLE_PRE_MOVE, nameof(on_move), @override: TRUE);
        RegisterSignal(charger, COMSIG_MOVABLE_MOVED, nameof(on_moved), @override: TRUE);
        RegisterSignal(charger, COMSIG_LIVING_DEATH, nameof(charge_end), @override: TRUE);
        await charger.setDir(dir);
        do_charge_indicator(charger, target);

        // SLEEP_CHECK_DEATH(delay, charger) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(Dm.num(delay));
        if (QDELETED(charger))
            return null;
        if (ismob(charger))
        {
            mob sleep_check_death_mob = (mob)charger;
            if (sleep_check_death_mob.stat == DEAD)
                return null;
        }

        float time_to_hit = min(get_dist(charger, target), charge_distance) * charge_speed;

        datum_move_loop new_loop = GLOB.move_manager.home_onto(charger, target, delay: charge_speed, timeout: time_to_hit, priority: MOVEMENT_ABOVE_SPACE_PRIORITY) as datum_move_loop;
        if (new_loop == null)
            return null;
        RegisterSignal(new_loop, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(pre_move), @override: TRUE);
        RegisterSignal(new_loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(post_move), @override: TRUE);
        RegisterSignal(new_loop, COMSIG_QDELETING, nameof(charge_end), @override: TRUE);

        // Yes this is disgusting. But we need to queue this stuff, and this code just isn't setup to support that right now. So gotta do it with sleeps
        await sleep(time_to_hit + charge_speed);
        await charger.setDir(dir);

        return TRUE;
    }

    // tg: code/datums/actions/mobs/charge.dm:78
    [SignalHandler]
    public virtual void pre_move(object datum = null)
    {
        // If you sleep in Move() you deserve what's coming to you
        actively_moving = TRUE;
    }

    // tg: code/datums/actions/mobs/charge.dm:83
    [SignalHandler]
    public virtual void post_move(object datum = null)
    {
        actively_moving = FALSE;
    }

    // tg: code/datums/actions/mobs/charge.dm:87
    [SignalHandler]
    public virtual void charge_end(datum source = null)
    {
        atom_movable charger = source as atom_movable;
        if (istype(source, typeof(datum_move_loop)))
        {
            datum_move_loop move_loop_source = (datum_move_loop)source;
            charger = move_loop_source.moving;
        }
        UnregisterSignal(charger, Dm.list(COMSIG_MOVABLE_BUMP, COMSIG_MOVABLE_PRE_MOVE, COMSIG_MOVABLE_MOVED, COMSIG_LIVING_DEATH));
        // SEND_SIGNAL(owner, COMSIG_FINISHED_CHARGE) (rules 11.1 (e), a statement)
        if (Dm.truthy(owner._listen_lookup?[COMSIG_FINISHED_CHARGE]))
            owner._SendSignal(COMSIG_FINISHED_CHARGE, Dm.list(owner));
        actively_moving = FALSE;
        Dm.remove(charging, charger);
    }

    // tg: code/datums/actions/mobs/charge.dm:98
    public override void update_status_on_signal(datum source = null, object new_stat = null, object old_stat = null)
    {
        base.update_status_on_signal(source, new_stat, old_stat);
        if (Dm.eq(new_stat, DEAD))
            GLOB.move_manager.stop_looping(source as atom_movable); //This will cause the loop to qdel, triggering an end to our charging
    }

    // tg: code/datums/actions/mobs/charge.dm:103
    public virtual void do_charge_indicator(atom charger = null, atom charge_target = null)
    {
        turf target_turf = get_turf(charge_target) as turf;
        if (target_turf == null)
            return;
        Dm.@new<obj_effect_temp_visual_dragon_swoop_bubblegum>(target_turf);
        obj_effect_temp_visual_decoy D = Dm.@new<obj_effect_temp_visual_decoy>(charger.loc, charger);
        animate(D, Dm.named("alpha", 0), Dm.named("color", COLOR_RED), Dm.named("transform", Dm.mul(Dm.matrix(), 2)), Dm.named("time", 3));
    }

    // tg: code/datums/actions/mobs/charge.dm:111
    [SignalHandler]
    public virtual object on_move(atom source = null, atom new_loc = null, params object[] __extra)
    {
        if (!Dm.truthy(actively_moving))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
        Dm.@new<obj_effect_temp_visual_decoy_fading>(source.loc, source);
        Waiting.Detach(DestroySurroundings(source as atom_movable)); // INVOKE_ASYNC
        return null;
    }

    // tg: code/datums/actions/mobs/charge.dm:118
    [SignalHandler]
    public virtual void on_moved(atom source = null)
    {
        playsound(source, Dm.resource("sound/effects/meteorimpact.ogg"), 200, TRUE, 2, TRUE);
        Waiting.Detach(DestroySurroundings(source as atom_movable)); // INVOKE_ASYNC
    }

    // tg: code/datums/actions/mobs/charge.dm:123
    public virtual async DmTask DestroySurroundings(atom_movable charger = null)
    {
        if (!Dm.truthy(destroy_objects))
            return;
        if (!isanimal(charger))
            return;
        foreach (object dir in Dm.each(GLOB.cardinals))
        {
            turf next_turf = get_step(charger, Dm.num(dir)) as turf;
            if (next_turf == null)
                continue;
            if (Dm.truthy(await next_turf.Adjacent(charger)) && (iswallturf(next_turf) || ismineralturf(next_turf)))
            {
                if (!isanimal(charger))
                {
                    Dm.append(SSexplosions.medturf, next_turf);
                    continue;
                }
                await next_turf.attack_animal(charger as mob);
                continue;
            }
            foreach (obj @object in Dm.each<obj>(next_turf.contents))
            {
                if (!Dm.truthy(await @object.Adjacent(charger)))
                    continue;
                if (!ismachinery(@object) && !isstructure(@object))
                    continue;
                if (!Dm.truthy(@object.density) || Dm.truthy(@object.IsObscured()))
                    continue;
                if (!isanimal(charger))
                {
                    // SSexplosions.med_mov_atom += target (:146): tg reads an undefined `target` here (a null), kept
                    Dm.append(SSexplosions.med_mov_atom, null);
                    break;
                }
                await @object.attack_animal(charger as mob);
                break;
            }
        }
    }

    // tg: code/datums/actions/mobs/charge.dm:151
    [SignalHandler]
    public virtual void on_bump(atom_movable source = null, atom target = null)
    {
        if (owner == target)
            return;
        if (Dm.truthy(destroy_objects))
        {
            if (isturf(target))
                Dm.append(SSexplosions.medturf, target);
            if (isobj(target) && Dm.truthy(target.density) && !ismecha(target))
                Waiting.NoSleep(((obj)target).take_damage(charge_damage));
            if (ismecha(target))
                Waiting.NoSleep(((obj)target).take_damage(charge_damage));
        }

        Waiting.Detach(DestroySurroundings(source)); // INVOKE_ASYNC
        Waiting.NoSleep(try_hit_target(source, target));
    }

    /// Attempt to hit someone with our charge
    // tg: code/datums/actions/mobs/charge.dm:167
    public virtual async DmTask try_hit_target(atom_movable source = null, atom target = null)
    {
        if (Dm.truthy(await can_hit_target(source, target)))
            await hit_target(source, target, charge_damage);
    }

    /// Returns true if we're allowed to charge into this target
    // tg: code/datums/actions/mobs/charge.dm:172
    public virtual async DmTask<object> can_hit_target(atom_movable source = null, atom target = null)
    {
        await DmTask.CompletedTask;
        return Dm.num(isliving(target));
    }

    /// Actually hit someone
    // tg: code/datums/actions/mobs/charge.dm:176
    public virtual async DmTask hit_target(atom_movable source = null, object target = null, object damage_dealt = null)
    {
        mob_living living_target = (mob_living)target;
        living_target.visible_message(span_danger(Dm.text("[] slams into []!", source, living_target)), span_userdanger(Dm.text("[] tramples you into the ground!", source)));
        await living_target.apply_damage(Dm.num(damage_dealt), BRUTE, wound_bonus: CANT_WOUND);
        playsound(get_turf(living_target), Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
        shake_camera(living_target, 4, 3);
        shake_camera(source as mob, 2, 3);
    }
}

// tg: code/datums/actions/mobs/charge.dm:183
public partial class datum_action_cooldown_mob_cooldown_charge_basic_charge : datum_action_cooldown_mob_cooldown_charge
{
    // tg: code/datums/actions/mobs/charge.dm:198
    public override void do_charge_indicator(atom charger = null, atom charge_target = null)
    {
        charger.Shake(shake_pixel_shift, shake_pixel_shift, shake_duration);
    }

    // tg: code/datums/actions/mobs/charge.dm:201
    public override async DmTask<object> can_hit_target(atom_movable source = null, atom target = null)
    {
        if (!isliving(target))
        {
            if (!Dm.truthy(target.density) || Dm.truthy(await target.CanPass(source, get_dir(target, source))))
                return FALSE;
            return TRUE;
        }
        return await base.can_hit_target(source, target);
    }

    // tg: code/datums/actions/mobs/charge.dm:208
    public override async DmTask hit_target(atom_movable source = null, object target = null, object damage_dealt = null)
    {
        mob_living living_source = null;
        if (isliving(source))
            living_source = (mob_living)source;

        if (!isliving(target))
        {
            source.visible_message(span_danger(Dm.text("[] smashes into []!", source, target)));
            if (recoil_duration >= 0) // Because 0 stun/knockdown is still a valid value
            {
                if (living_source != null)
                    await living_source.Stun(recoil_duration, ignore_canstun: TRUE);
            }
            return;
        }

        mob_living living_target = (mob_living)target;
        if (ishuman(living_target))
        {
            mob_living_carbon_human human_target = (mob_living_carbon_human)living_target;
            if (Dm.truthy(await human_target.check_block(source, 0, Dm.text("\\the []", source), attack_type: LEAP_ATTACK)) && living_source != null)
            {
                if (recoil_duration >= 0)
                    await living_source.Stun(recoil_duration, ignore_canstun: TRUE);
                return;
            }
        }

        living_target.visible_message(span_danger(Dm.text("[] charges into []!", source, living_target)), span_userdanger(Dm.text("[] charges into you!", source)));
        if (knockdown_duration >= 0)
            await living_target.Knockdown(knockdown_duration);
    }
}
