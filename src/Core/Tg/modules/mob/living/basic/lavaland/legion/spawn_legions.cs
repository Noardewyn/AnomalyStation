// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Spawns a little worm nearby
// tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:2
public partial class datum_action_cooldown_mob_cooldown_skull_launcher : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:20
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return dot;
        if (!isturf(owner.loc))
        {
            owner.balloon_alert(owner, "no room!");
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:29
    public override async DmTask<object> Activate(atom target = null)
    {
        turf target_turf = get_turf(target) as turf;

        if (Dm.num(get_dist(owner, target_turf)) > max_range)
            target_turf = get_ranged_target_turf_direct(owner, target_turf, max_range) as turf;

        if (Dm.truthy(await target_turf.is_blocked_turf()))
        {
            DmList near_turfs = Dm.sub(RANGE_TURFS(1, target_turf), target_turf) as DmList;
            foreach (turf check_turf in Dm.each_cast<turf>(near_turfs))
            {
                if (Dm.truthy(await check_turf.is_blocked_turf()))
                    Dm.remove(ref near_turfs, check_turf);
            }
            if (Dm.truthy(Dm.length(near_turfs)))
                target_turf = pick(near_turfs) as turf;
            else if (Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: TRUE)))
            {
                owner.balloon_alert(owner, "no room!");
                StartCooldown(0.5f * SECONDS);
                return null;
            }
        }

        object ai_target = isliving(target) ? target : null;
        if (ai_target == null)
            ai_target = (owner.ai_controller as datum_ai_controller)?.blackboard[ai_target_key];

        float target_dir = get_dir(owner, target);

        obj_effect_temp_visual_legion_skull_depart launch = Dm.@new<obj_effect_temp_visual_legion_skull_depart>(get_turf(owner));
        launch.set_appearance(spawn_type);
        await launch.setDir(target_dir);
        Dm.@new<obj_effect_temp_visual_legion_brood_indicator>(target_turf);
        obj_effect_temp_visual_legion_skull_land land = Dm.@new<obj_effect_temp_visual_legion_skull_land>(target_turf);
        await land.setDir(target_dir);
        land.set_appearance(spawn_type, Dm.@new<datum_callback>(this, nameof(spawn_skull), target_turf, ai_target));
        StartCooldown();
        return null;
    }

    /// Actually create a mob
    // tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:63
    public virtual async DmTask spawn_skull(turf spawn_location = null, object target = null)
    {
        mob_living_basic_mining_legion_brood brood = Dm.@new(spawn_type, spawn_location) as mob_living_basic_mining_legion_brood;
        if (brood is mob_living_basic_mining_legion_brood)
            brood.assign_creator(owner as mob_living);
        (brood.ai_controller as datum_ai_controller)?.set_blackboard_key(ai_target_key, target);
        await brood.setDir(get_dir(owner, spawn_location));
        if (target != null)
            brood.face_atom(target as atom);
        else
            await brood.setDir(get_dir(owner, spawn_location));
    }
}

/// Animation for launching a skull
// tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:76
public partial class obj_effect_temp_visual_legion_skull_depart : obj_effect_temp_visual
{
    /// Copy appearance from the passed atom type
    // tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:83
    public virtual void set_appearance(Type spawned_type = null)
    {
        icon = Dm.initial(spawned_type, (atom p) => p.icon);
        icon_state = Dm.initial(spawned_type, (atom p) => p.icon_state);
        animate(this, Dm.named("alpha", 0), Dm.named("pixel_y", 72), Dm.named("time", duration));
    }
}

/// Animation for landing a skull
// tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:89
public partial class obj_effect_temp_visual_legion_skull_land : obj_effect_temp_visual
{
    /// Copy appearance from the passed atom type and store what to do on animation complete
    // tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:98
    public virtual void set_appearance(Type spawned_type = null, datum_callback on_completed = null)
    {
        icon = Dm.initial(spawned_type, (atom p) => p.icon);
        icon_state = Dm.initial(spawned_type, (atom p) => p.icon_state);
        animate(this, Dm.named("alpha", 0), Dm.named("pixel_y", 72), Dm.named("time", duration / 2));
        animate(Dm.named("alpha", 255), Dm.named("pixel_y", 0), Dm.named("time", duration / 2));
        _addtimer(on_completed, duration, TIMER_DELETE_ME, file: "code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm", line: 103);
    }
}

/// A skull is going to be here! Oh no!
// tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:106
public partial class obj_effect_temp_visual_legion_brood_indicator : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/basic/lavaland/legion/spawn_legions.dm:114
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate(this, Dm.named("alpha", 255), Dm.named("time", 0.5f * SECONDS));
        animate(Dm.named("alpha", 0), Dm.named("time", 0.25f * SECONDS));
        return dot;
    }
}
