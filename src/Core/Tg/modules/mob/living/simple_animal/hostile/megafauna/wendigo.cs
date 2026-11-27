// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/*

Difficulty: Hard

*/

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:9
public partial class mob_living_simple_animal_hostile_megafauna_wendigo : mob_living_simple_animal_hostile_megafauna
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:1  #define WENDIGO_ENRAGED (health <= maxHealth*0.5) (file-local)
    private bool WENDIGO_ENRAGED => health <= maxHealth * 0.5f;

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:72  (the earlier of two definitions)
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, INNATE_TRAIT);
        teleport = Dm.@new<datum_action_cooldown_mob_cooldown_teleport>(this);
        shotgun_blast = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_shotgun_blast_wendigo>(this);
        ground_slam = Dm.@new<datum_action_cooldown_mob_cooldown_ground_slam>(this);
        alternating_circle = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_alternating_circle>(this);
        spiral = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_spiral_shots_wendigo>(this);
        wave = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_wave>(this);
        Waiting.NoSleep(teleport.Grant(this));
        Waiting.NoSleep(shotgun_blast.Grant(this));
        Waiting.NoSleep(ground_slam.Grant(this));
        Waiting.NoSleep(alternating_circle.Grant(this));
        Waiting.NoSleep(spiral.Grant(this));
        Waiting.NoSleep(wave.Grant(this));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:88
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        starting = get_turf(this);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:92
    public override async DmTask OpenFire(atom A = null)
    {
        update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_MELEE, 10 * SECONDS), Dm.kv(COOLDOWN_UPDATE_SET_RANGED, 10 * SECONDS)));
        if (WENDIGO_ENRAGED)
        {
            speed = 4;
            move_to_delay = 4;
        }
        else
        {
            stomp_range = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile_megafauna_wendigo p) => p.stomp_range));
            speed = Dm.num(Dm.initial(this, "speed"));
            move_to_delay = Dm.num(Dm.initial(this, "move_to_delay"));
        }

        if (client != null)
            return;

        if (target is mob_living living_target && living_target.stat == DEAD)
            return;

        // tg never starts scream_cooldown, so the third attack is always open; kept.
        if (scream_cooldown <= world.time) // COOLDOWN_FINISHED(src, scream_cooldown)
            chosen_attack = rand(1, 3);
        else
            chosen_attack = rand(1, 2);
        switch (Dm.num(chosen_attack))
        {
            case 1:
                await ground_slam.Activate(target);
                break;
            case 2:
                await teleport.Activate(target);
                if (WENDIGO_ENRAGED)
                    await shotgun_blast.Activate(target);
                break;
            case 3:
                await do_teleport(this, starting, 0, channel: TELEPORT_CHANNEL_BLUESPACE, forced: TRUE);
                float shockwave_attack;
                if (WENDIGO_ENRAGED)
                    shockwave_attack = rand(1, 3);
                else
                    shockwave_attack = rand(1, 2);
                switch (shockwave_attack)
                {
                    case 1:
                        alternating_circle.enraged = WENDIGO_ENRAGED ? TRUE : FALSE;
                        await alternating_circle.Activate(target);
                        break;
                    case 2:
                        spiral.enraged = WENDIGO_ENRAGED ? TRUE : FALSE;
                        await spiral.Activate(target);
                        break;
                    case 3:
                        await wave.Activate(target);
                        break;
                }
                update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_MELEE, 3 * SECONDS), Dm.kv(COOLDOWN_UPDATE_SET_RANGED, 3 * SECONDS)));
                break;
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:136
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        stored_move_dirs = Dm.num(Dm.bitor(stored_move_dirs, direction));
        object dot = await base.Move(newloc, direction, glide_size_override, update_dir);
        // Remove after anyways in case the movement was prevented
        stored_move_dirs = Dm.num(Dm.bitand(stored_move_dirs, Dm.bitnot(Dm.num(direction))));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:142
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        stored_move_dirs = Dm.num(Dm.bitand(stored_move_dirs, Dm.bitnot(Dm.num(movement_dir))));
        if (!Dm.truthy(stored_move_dirs))
            Waiting.Detach(wendigo_slam(this, stomp_range, 1, 8)); // INVOKE_ASYNC(GLOBAL_PROC, ...)
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:186
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (health > 0)
            return null;

        if (!Dm.truthy(true_spawn))
            return await base.death(gibbed, __extra);

        await create_portal();
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:195
    public virtual async DmTask create_portal()
    {
        obj_effect_portal_permanent_one_way exit = Dm.@new<obj_effect_portal_permanent_one_way>(starting);
        exit.id = "wendigo arena exit";
        await exit.add_atom_colour(COLOR_RED_LIGHT, ADMIN_COLOUR_PRIORITY);
        await exit.set_light(20, 1, COLOR_SOFT_RED);
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:225
public partial class mob_living_simple_animal_hostile_megafauna_wendigo_noportal : mob_living_simple_animal_hostile_megafauna_wendigo
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:225
    public override async DmTask create_portal()
    {
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:201
public partial class obj_projectile_colossus_wendigo_shockwave : obj_projectile_colossus
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:218
    public override async DmTask<object> process_movement(object pixels_to_move = null, float? hitscan = null, float? tile_limit = null)
    {
        object dot = await base.process_movement(pixels_to_move, hitscan, tile_limit);
        if (QDELETED(this))
            return dot;
        pixel_moves += Dm.num(dot);
        set_angle(original_angle + pixel_moves * wave_speed);
        return dot;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:150
    public static async DmTask wendigo_scream(mob owner = null)
    {
        await sleep(5); // SLEEP_CHECK_DEATH(5, owner)
        if (QDELETED(owner) || owner.stat == DEAD)
            return;
        playsound(owner.loc, Dm.resource("sound/effects/magic/demon_dies.ogg"), 600, FALSE, 10);
        float pixel_shift = rand(5, 15);
        animate(owner, Dm.named("pixel_z", pixel_shift), Dm.named("time", 1), Dm.named("loop", 20), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", -pixel_shift), Dm.named("time", 1), Dm.named("flags", ANIMATION_RELATIVE));
        foreach (mob_living dizzy_target in Dm.each<mob_living>(Dm.sub(get_hearers_in_view(7, owner), owner)))
        {
            await dizzy_target.set_dizzy_if_lower(12 * SECONDS);
            to_chat(dizzy_target, span_danger(I18n.T("[] screams loudly!", owner)));
        }
        await sleep(1 * SECONDS); // SLEEP_CHECK_DEATH(1 SECONDS, owner)
        if (QDELETED(owner) || owner.stat == DEAD)
            return;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/wendigo.dm:162
    public static async DmTask wendigo_slam(mob owner = null, object range = null, object delay = null, object throw_range = null)
    {
        turf origin = get_turf(owner);
        if (origin == null)
            return;
        DmList all_turfs = RANGE_TURFS(range, origin);
        for (float sound_range = 0; sound_range <= Dm.num(range); sound_range++)
        {
            playsound(origin, Dm.resource("sound/effects/bamf.ogg"), 600, TRUE, 10);
            foreach (turf stomp_turf in Dm.each<turf>(all_turfs))
            {
                if (get_dist(origin, stomp_turf) > sound_range)
                    continue;
                Dm.@new<obj_effect_temp_visual_small_smoke_halfsecond>(stomp_turf);
                foreach (mob_living hit_mob in Dm.each<mob_living>(stomp_turf))
                {
                    if (hit_mob == owner || Dm.truthy(hit_mob.throwing))
                        continue;
                    to_chat(hit_mob, span_userdanger(I18n.T("[]'s ground slam shockwave sends you flying!", owner)));
                    turf thrownat = get_ranged_target_turf_direct(owner, hit_mob, throw_range, rand(-10, 10)) as turf;
                    await hit_mob.throw_at(thrownat, 8, 2, null, TRUE, force: MOVE_FORCE_OVERPOWERING, gentle: TRUE);
                    await hit_mob.apply_damage(20, BRUTE, wound_bonus: CANT_WOUND);
                    shake_camera(hit_mob, 2, 1);
                }
                Dm.remove(ref all_turfs, stomp_turf);
            }
            await sleep(Dm.num(delay)); // SLEEP_CHECK_DEATH(delay, owner)
            if (QDELETED(owner) || owner.stat == DEAD)
                return;
        }
    }
}
