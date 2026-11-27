// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/projectileattack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/datums/actions/mobs/projectileattack.dm:2
public partial class datum_action_cooldown_mob_cooldown_projectile_attack : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/projectileattack.dm:22
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        RegisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE, nameof(on_move), @override: TRUE);
        await attack_sequence(owner as mob_living, target_atom);
        UnregisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:31
    [SignalHandler]
    public virtual object on_move(atom source = null, atom new_loc = null)
    {
        if (!Dm.truthy(can_move))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
        return null;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:36
    public virtual async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        shoot_projectile(firer, target, null, firer, rand(-default_projectile_spread, default_projectile_spread), null);
        return null;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:39
    public virtual obj_projectile shoot_projectile(atom origin = null, atom target = null, object set_angle = null, mob firer = null, object projectile_spread = null, object speed_multiplier = null, object override_projectile_type = null, object override_homing = null)
    {
        turf startloc = get_turf(origin) as turf;
        turf endloc = get_turf(target) as turf;
        if (startloc == null || endloc == null)
            return null;
        obj_projectile our_projectile;
        if (override_projectile_type != null)
            our_projectile = Dm.@new((Type)override_projectile_type, startloc) as obj_projectile;
        else
            our_projectile = Dm.@new(projectile_type, startloc) as obj_projectile;
        if (speed_multiplier is not float)
            speed_multiplier = projectile_speed_multiplier;
        our_projectile.speed = Dm.num(our_projectile.speed) * Dm.num(speed_multiplier);
        Waiting.NoSleep(our_projectile.aim_projectile(endloc, startloc, null, Dm.num(projectile_spread)));
        our_projectile.firer = firer;
        if (target != null)
            our_projectile.original = target;
        if ((override_homing == null && Dm.truthy(has_homing)) || Dm.truthy(override_homing))
        {
            our_projectile.homing_turn_speed = homing_turn_speed;
            our_projectile.set_homing_target(target);
        }
        if (set_angle is float)
        {
            Waiting.NoSleep(our_projectile.fire(set_angle));
            return our_projectile;
        }
        Waiting.NoSleep(our_projectile.fire());
        return our_projectile;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:66
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/datums/actions/mobs/projectileattack.dm:78
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        for (float i = 1, i__end = shot_count; i <= i__end; i++)
        {
            shoot_projectile(firer, target, null, firer, rand(-default_projectile_spread, default_projectile_spread), null);
            // SLEEP_CHECK_DEATH(shot_delay, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(shot_delay);
            if (QDELETED(firer))
                return null;
            if (ismob(firer))
            {
                mob sleep_check_death_mob = firer;
                if (sleep_check_death_mob.stat == DEAD)
                    return null;
            }
        }
        return null;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:258
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_dir_shots : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/datums/actions/mobs/projectileattack.dm:268
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(firing_directions))
            firing_directions = GLOB.alldirs.Copy();
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:273
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        fire_in_directions(firer, target, firing_directions);
        return null;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:276
    public virtual void fire_in_directions(mob_living firer = null, atom target = null, object dirs = null)
    {
        if (!islist(dirs))
            dirs = GLOB.alldirs.Copy();
        playsound(firer, projectile_sound, 200, TRUE, 2);
        foreach (object dir in Dm.each(dirs as DmList))
            shoot_projectile(firer, target, dir2angle(dir), firer);
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:87
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_shrapnel : datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire
{
    // tg: code/datums/actions/mobs/projectileattack.dm:103
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        for (float i = 1, i__end = shot_count; i <= i__end; i++)
        {
            obj_projectile to_explode = shoot_projectile(firer, target, null, firer, rand(-default_projectile_spread, default_projectile_spread), null);
            _addtimer(Dm.@new<datum_callback>(this, nameof(explode_into_shrapnel), firer, target, to_explode), break_time, file: "code/datums/actions/mobs/projectileattack.dm", line: 106);
            // SLEEP_CHECK_DEATH(shot_delay, src) (__DEFINES/mobs.dm:627), expanded inline: src is the action, not a mob
            await sleep(shot_delay);
            if (QDELETED(this))
                return null;
        }
        return null;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:109
    public virtual void explode_into_shrapnel(mob_living firer = null, atom target = null, obj_projectile to_explode = null)
    {
        if (to_explode == null)
            return;
        foreach (object angle in Dm.each(shrapnel_angles))
        {
            // no speed multiplier for shrapnel
            shoot_projectile(to_explode, target, Dm.num(angle) + rand(-shrapnel_spread, shrapnel_spread), firer, null, 1, shrapnel_projectile_type, FALSE);
        }
        qdel(to_explode);
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:122
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_spiral_shots : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/datums/actions/mobs/projectileattack.dm:133
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        if (Dm.truthy(enraged))
        {
            // SLEEP_CHECK_DEATH(1 * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(1 * SECONDS);
            if (QDELETED(firer) || firer.stat == DEAD)
                return null;
            Waiting.Detach(create_spiral_attack(firer, target, TRUE)); // INVOKE_ASYNC
            await create_spiral_attack(firer, target, FALSE);
            return null;
        }
        await create_spiral_attack(firer, target);
        return null;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:141
    public virtual async DmTask<object> create_spiral_attack(mob_living firer = null, atom target = null, object negative = null)
    {
        negative ??= pick(TRUE, FALSE);
        float counter = 8;
        for (float i = 1; i <= 80; i++)
        {
            if (Dm.truthy(negative))
                counter--;
            else
                counter++;
            if (counter > 16)
                counter = 1;
            if (counter < 1)
                counter = 16;
            shoot_projectile(firer, target, counter * 22.5f, firer, null, null);
            playsound(get_turf(firer), projectile_sound, 20, TRUE);
            // SLEEP_CHECK_DEATH(0.1f * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(0.1f * SECONDS);
            if (QDELETED(firer) || firer.stat == DEAD)
                return null;
        }
        return null;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:156
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_spiral_shots_colossus : datum_action_cooldown_mob_cooldown_projectile_attack_spiral_shots
{
    // tg: code/datums/actions/mobs/projectileattack.dm:159
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        // SLEEP_CHECK_DEATH(1.5f * SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1.5f * SECONDS);
        if (QDELETED(owner) || owner.stat == DEAD)
            return null;
        return await base.Activate(target_atom);
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:163
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_spiral_shots_wendigo : datum_action_cooldown_mob_cooldown_projectile_attack_spiral_shots
{
    // tg: code/datums/actions/mobs/projectileattack.dm:168
    public override async DmTask<object> create_spiral_attack(mob_living firer = null, atom target = null, object negative = null)
    {
        negative ??= pick(TRUE, FALSE);
        await wendigo_scream(firer);
        float shots_spiral = 40;
        float angle_to_target = Dm.num(get_angle(firer, target));
        float spiral_direction = Dm.num(pick(-1, 1));
        for (float shot = 1; shot <= shots_spiral; shot++)
        {
            float shots_per_tick = 5 - enraged * 3;
            float angle_change = (5 + enraged * shot / 6) * spiral_direction;
            for (float count = 1; count <= shots_per_tick; count++)
            {
                float angle = angle_to_target + shot * angle_change + count * 360 / shots_per_tick;
                shoot_projectile(firer, target, angle, firer, null, null);
            }
            // SLEEP_CHECK_DEATH(1, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(1);
            if (QDELETED(firer) || firer.stat == DEAD)
                return null;
        }
        // SLEEP_CHECK_DEATH(3 * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(3 * SECONDS);
        if (QDELETED(firer) || firer.stat == DEAD)
            return null;
        return null;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:182
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_random_aoe : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/datums/actions/mobs/projectileattack.dm:191
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        turf U = get_turf(firer);
        playsound(U, projectile_sound, 300, TRUE, 5);
        for (float i = 1; i <= 32; i++)
            shoot_projectile(firer, target, rand(0, 360), firer, null, null);
        return null;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:197
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_random_aoe_colossus : datum_action_cooldown_mob_cooldown_projectile_attack_random_aoe
{
    // tg: code/datums/actions/mobs/projectileattack.dm:200
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        // SLEEP_CHECK_DEATH(1.5f * SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1.5f * SECONDS);
        if (QDELETED(owner) || owner.stat == DEAD)
            return null;
        return await base.Activate(target_atom);
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:204
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_shotgun_blast : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/datums/actions/mobs/projectileattack.dm:214
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        fire_shotgun(firer, target, shot_angles);
        return null;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:217
    public virtual void fire_shotgun(mob_living firer = null, atom target = null, DmList chosen_angles = null)
    {
        // DEVIATION(DV625): tg plays the sound unchecked, and the frost miner's patterns, which have none, crash there before a shot
        // is fired
        if (Dm.truthy(projectile_sound))
            playsound(firer, projectile_sound, 200, TRUE, 2);
        foreach (object spread in Dm.each(chosen_angles))
            shoot_projectile(firer, target, null, firer, spread, null);
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:230
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_shotgun_blast_colossus : datum_action_cooldown_mob_cooldown_projectile_attack_shotgun_blast
{
    // tg: code/datums/actions/mobs/projectileattack.dm:233
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        // SLEEP_CHECK_DEATH(1.5f * SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1.5f * SECONDS);
        if (QDELETED(owner) || owner.stat == DEAD)
            return null;
        return await base.Activate(target_atom);
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:237
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_shotgun_blast_pattern : datum_action_cooldown_mob_cooldown_projectile_attack_shotgun_blast
{
    // tg: code/datums/actions/mobs/projectileattack.dm:245
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        for (float i = 1, i__end = shot_count; i <= i__end; i++)
        {
            DmList pattern = shot_angles[Dm.mod(i, Dm.length(shot_angles)) + 1] as DmList; // changing patterns
            fire_shotgun(firer, target, pattern);
            // SLEEP_CHECK_DEATH(0.8f * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(0.8f * SECONDS);
            if (QDELETED(firer) || firer.stat == DEAD)
                return null;
        }
        return null;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:288
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_dir_shots_alternating : datum_action_cooldown_mob_cooldown_projectile_attack_dir_shots
{
    // tg: code/datums/actions/mobs/projectileattack.dm:288
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        fire_in_directions(firer, target, GLOB.diagonals);
        // SLEEP_CHECK_DEATH(1 * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1 * SECONDS);
        if (QDELETED(firer) || firer.stat == DEAD)
            return null;
        fire_in_directions(firer, target, GLOB.cardinals);
        // SLEEP_CHECK_DEATH(1 * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1 * SECONDS);
        if (QDELETED(firer) || firer.stat == DEAD)
            return null;
        fire_in_directions(firer, target, GLOB.diagonals);
        // SLEEP_CHECK_DEATH(1 * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1 * SECONDS);
        if (QDELETED(firer) || firer.stat == DEAD)
            return null;
        fire_in_directions(firer, target, GLOB.cardinals);
        return null;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:297
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_dir_shots_alternating_colossus : datum_action_cooldown_mob_cooldown_projectile_attack_dir_shots_alternating
{
    // tg: code/datums/actions/mobs/projectileattack.dm:300
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        // SLEEP_CHECK_DEATH(1.5f * SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1.5f * SECONDS);
        if (QDELETED(owner) || owner.stat == DEAD)
            return null;
        return await base.Activate(target_atom);
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:304
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_kinetic_accelerator : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/datums/actions/mobs/projectileattack.dm:313
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        object dot = await base.Activate(target_atom);
        playsound(owner, projectile_sound, 200, TRUE, 2);
        owner.visible_message(span_danger(I18n.T("[] fires the proto-kinetic accelerator!", owner)));
        owner.face_atom(target_atom);
        Dm.@new<obj_effect_temp_visual_dir_setting_firing_effect>(owner.loc, owner.dir);
        return dot;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:320
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_colossus_final : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/datums/actions/mobs/projectileattack.dm:326
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        object dot = await base.Activate(target_atom);
        await Remove(owner);
        return dot;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:330
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        mob_living_simple_animal_hostile_megafauna_colossus colossus = null;
        if (firer is mob_living_simple_animal_hostile_megafauna_colossus)
        {
            colossus = firer as mob_living_simple_animal_hostile_megafauna_colossus;
            await colossus.say(I18n.T("Perish."), spans: Dm.list(SPAN_COLOSSUS, SPAN_YELL));
        }

        // SLEEP_CHECK_DEATH(1.5f * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(1.5f * SECONDS);
        if (QDELETED(firer) || firer.stat == DEAD)
            return null; //gives dumbasses in melee range a slim chance to retreat
        float finale_counter = 10;
        for (float i = 1; i <= 20; i++)
        {
            if (finale_counter > 4 && colossus != null)
            {
                colossus.telegraph();
                await colossus.shotgun_blast.attack_sequence(firer, target);
            }

            if (finale_counter > 1)
                finale_counter -= 1;

            turf start_turf = get_turf(firer);
            foreach (turf target_turf in Dm.each<turf>(RANGE_TURFS(12, start_turf)))
            {
                if (prob(min(finale_counter, 2)) && target_turf != get_turf(firer))
                    shoot_projectile(firer, target_turf, null, firer, null, null);
            }

            // SLEEP_CHECK_DEATH(finale_counter + 1, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(finale_counter + 1);
            if (QDELETED(firer) || firer.stat == DEAD)
                return null;
        }

        for (float i = 1; i <= 3; i++)
        {
            if (colossus != null)
            {
                colossus.telegraph();
                await colossus.random_shots.attack_sequence(firer, target);
            }
            finale_counter += 6;
            // SLEEP_CHECK_DEATH(finale_counter, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(finale_counter);
            if (QDELETED(firer) || firer.stat == DEAD)
                return null;
        }

        for (float i = 1; i <= 3; i++)
        {
            if (colossus != null)
            {
                colossus.telegraph();
                await colossus.dir_shots.attack_sequence(firer, target);
            }
            // SLEEP_CHECK_DEATH(1 * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(1 * SECONDS);
            if (QDELETED(firer) || firer.stat == DEAD)
                return null;
        }
        return null;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:366
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_alternating_circle : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/datums/actions/mobs/projectileattack.dm:376
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        await wendigo_scream(firer);
        if (Dm.truthy(enraged))
            projectile_speed_multiplier = 1;
        else
            projectile_speed_multiplier = 0.66f;
        float shots_per = 24;
        for (float shoot_times = 1; shoot_times <= 8; shoot_times++)
        {
            float offset = Dm.mod(shoot_times, 2);
            for (float shot = 1; shot <= shots_per; shot++)
            {
                float angle = shot * 360 / shots_per + (offset * 360 / shots_per) * 0.5f;
                shoot_projectile(firer, target, angle, firer, null, null);
            }
            // SLEEP_CHECK_DEATH(6 - enraged * 2, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(6 - enraged * 2);
            if (QDELETED(firer) || firer.stat == DEAD)
                return null;
        }
        // SLEEP_CHECK_DEATH(3 * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(3 * SECONDS);
        if (QDELETED(firer) || firer.stat == DEAD)
            return null;
        return null;
    }
}

// tg: code/datums/actions/mobs/projectileattack.dm:391
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_wave : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/datums/actions/mobs/projectileattack.dm:400
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        await wendigo_scream(firer);
        float shots_per = 6;
        float difference = 360 / shots_per;
        float wave_direction = Dm.num(pick(-1, 1));
        if (wave_direction == -1)
            projectile_type = typeof(obj_projectile_colossus_wendigo_shockwave_wave_alternate);
        else if (wave_direction == 1)
            projectile_type = typeof(obj_projectile_colossus_wendigo_shockwave_wave);
        for (float shoot_times = 1; shoot_times <= 12; shoot_times++)
        {
            for (float shot = 1; shot <= shots_per; shot++)
            {
                float angle = shot * difference + shoot_times * 5 * wave_direction * -1;
                shoot_projectile(firer, target, angle, firer, null, null);
            }
            // SLEEP_CHECK_DEATH(0.6f * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(0.6f * SECONDS);
            if (QDELETED(firer) || firer.stat == DEAD)
                return null;
        }
        // SLEEP_CHECK_DEATH(3 * SECONDS, firer) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(3 * SECONDS);
        if (QDELETED(firer) || firer.stat == DEAD)
            return null;
        return null;
    }
}
