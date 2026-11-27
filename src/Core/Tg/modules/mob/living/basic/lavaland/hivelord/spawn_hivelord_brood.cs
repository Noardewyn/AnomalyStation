// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Spawns a little worm nearby
// tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:2
public partial class datum_action_cooldown_mob_cooldown_hivelord_spawn : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:24
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        if (owner == null)
            return dot;
        if (Dm.truthy(trigger_on_hit))
            RegisterSignal(owner, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:31
    public override async DmTask Remove(mob removed_from = null)
    {
        UnregisterSignal(removed_from, COMSIG_ATOM_WAS_ATTACKED);
        await base.Remove(removed_from);
    }

    // tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:35
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        if (!Dm.truthy(await spawn_brood(target, target_turf: get_turf(target) as turf)))
        {
            StartCooldown(0.5f * SECONDS);
            return dot;
        }
        StartCooldown();
        return dot;
    }

    /// Called when someone whacks us
    // tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:43
    [SignalHandler]
    public virtual void on_attacked(atom victim = null, atom attacker = null, object attack_flags = null)
    {
        if (!Dm.truthy(trigger_on_hit) || !Dm.truthy(Dm.bitand(attack_flags, ATTACKER_DAMAGING_ATTACK)) || !(on_hit_cooldown <= world.time)) // COOLDOWN_FINISHED(src, on_hit_cooldown)
            return;
        on_hit_cooldown = world.time + on_hit_delay; // COOLDOWN_START(src, on_hit_cooldown, on_hit_delay)
        Waiting.NoSleep(spawn_brood(attacker, target_turf: get_step_away(owner, attacker), feedback: FALSE));
    }

    /// Spawn a funny little worm
    // tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:51
    public virtual async DmTask<object> spawn_brood(object target = null, turf target_turf = null, float? feedback = null)
    {
        feedback ??= TRUE;
        object ai_target = isliving(target) ? target : null;
        if (ai_target == null)
            ai_target = (owner.ai_controller as datum_ai_controller)?.blackboard[ai_target_key];

        float dir_to_target = get_dir(owner, target_turf);
        DmList target_turfs = Dm.list();
        for (float i = -1; i <= 1; i++)
        {
            float turn_amount = rand(-1, 1) * 45;
            float test_dir = turn(dir_to_target, turn_amount);
            turf test_turf = get_step(owner, test_dir);
            if (Dm.truthy(await test_turf.is_blocked_turf(exclude_mobs: TRUE)))
                continue;
            Dm.append(target_turfs, test_turf);
        }

        if (!Dm.truthy(Dm.length(target_turfs)))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "no room!");
            StartCooldown(0.5f * SECONDS);
            return FALSE;
        }

        turf land_turf = pick(target_turfs) as turf;
        obj_effect_temp_visual_hivebrood_spawn forecast = Dm.@new<obj_effect_temp_visual_hivebrood_spawn>(land_turf);
        await forecast.create_from(spawn_type, get_turf(owner) as turf, Dm.@new<datum_callback>(this, nameof(complete_spawn), land_turf, ai_target));
        StartCooldown();

        return TRUE;
    }

    /// Actually create a mob
    // tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:80
    public virtual async DmTask complete_spawn(turf spawn_turf = null, object target = null)
    {
        mob_living brood = Dm.@new(spawn_type, spawn_turf) as mob_living;
        // SET_FACTION_AND_ALLIES_FROM(brood, owner) (__DEFINES/mobfactions.dm:119), inline
        brood.set_faction(owner.get_faction());
        brood.set_allies(LAZYLISTDUPLICATE(owner.allies));
        (brood.ai_controller as datum_ai_controller)?.set_blackboard_key(ai_target_key, target);
        await brood.setDir(get_dir(owner, spawn_turf));
    }
}

/// Fast animation to show a worm spawning
// tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:90
public partial class obj_effect_temp_visual_hivebrood_spawn : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:86  #define BROOD_ARC_Y_OFFSET 8 (file-local, #undef at :122)
    private const int BROOD_ARC_Y_OFFSET = 8;
    // tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:87  #define BROOD_ARC_ROTATION 45 (file-local, #undef at :123)
    private const int BROOD_ARC_ROTATION = 45;

    /// Set up our visuals and start a timer for a callback
    // tg: code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm:96
    public virtual async DmTask create_from(Type spawn_type = null, turf spawn_from = null, datum_callback on_completed = null)
    {
        _addtimer(on_completed, duration, TIMER_DELETE_ME, file: "code/modules/mob/living/basic/lavaland/hivelord/spawn_hivelord_brood.dm", line: 97);

        turf my_turf = get_turf(this) as turf;
        await setDir(get_dir(spawn_from, my_turf));
        float move_x = (my_turf.x - spawn_from.x) * ICON_SIZE_X;
        float move_y = (my_turf.y - spawn_from.y) * ICON_SIZE_Y;
        pixel_x = -move_x;
        pixel_y = -move_y;

        icon = Dm.initial(spawn_type, (mob_living p) => p.icon);
        icon_state = Dm.initial(spawn_type, (mob_living p) => p.icon_state);

        animate(this, Dm.named("pixel_x", 0), Dm.named("time", duration));
        animate(this, Dm.named("pixel_y", BROOD_ARC_Y_OFFSET - (move_y * 0.5f)), Dm.named("time", duration * 0.5f), Dm.named("flags", ANIMATION_PARALLEL), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
        animate(Dm.named("pixel_y", 0), Dm.named("time", duration * 0.5f), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
        animate(this, Dm.named("alpha", 255), Dm.named("time", duration * 0.5f), Dm.named("flags", ANIMATION_PARALLEL));

        if (Dm.truthy(Dm.bitand(dir, Dm.bitor(NORTH, EAST))))
        {
            transform = Dm.matrix().Turn(-BROOD_ARC_ROTATION);
            animate(this, Dm.named("transform", Dm.matrix()), Dm.named("time", duration), Dm.named("flags", ANIMATION_PARALLEL));
        }
        else
        {
            transform = Dm.matrix().Turn(BROOD_ARC_ROTATION);
            animate(this, Dm.named("transform", Dm.matrix()), Dm.named("time", duration), Dm.named("flags", ANIMATION_PARALLEL));
        }
    }
}
