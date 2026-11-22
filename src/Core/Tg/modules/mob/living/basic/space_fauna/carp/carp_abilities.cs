// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * # Magicarp Bolt
 * Launches a magic bolt at the target, the projectile type is set up when the carp spawns
 */
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:5
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_magicarp_bolt : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:32
    public override async DmTask<object> InterceptClickOn(mob_living clicker = null, object @params = null, atom target = null)
    {
        if (!Dm.truthy(clicker.combat_mode))
            return FALSE;
        return await base.InterceptClickOn(clicker, @params, target);
    }
}

/**
 * # Chaos Magicarp Bolt
 * Fires a random spell
 */
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:19
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_magicarp_bolt_chaos : datum_action_cooldown_mob_cooldown_projectile_attack_magicarp_bolt
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:15 and :23 (two overrides on the same type; the later one wraps the earlier)
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        if (!Dm.truthy(Dm.length(permitted_projectiles)))
            return null;
        projectile_type = pick(permitted_projectiles) as Type;
        playsound(get_turf(firer), projectile_sound, 100, vary: TRUE);
        return await base.attack_sequence(firer, target);
    }
}

/**
 * # Lesser Carp Rift
 * Teleport a short distance and leave a short-lived portal for people to follow through
 */
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:41
public partial class datum_action_cooldown_mob_cooldown_lesser_carp_rift : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:51
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        if (Dm.num(get_dist(get_turf(owner), target_atom)) > max_range)
            target_atom = get_ranged_target_turf_direct(owner, target_atom, range: max_range) as atom;
        if (!Dm.truthy(await make_rift(target_atom)))
            return FALSE;
        StartCooldown();
        return TRUE;
    }

    /// Make a rift, return TRUE if successful
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:59
    public virtual async DmTask<object> make_rift(atom target_atom = null)
    {
        if (Dm.truthy(await owner.Adjacent(target_atom)))
        {
            owner.balloon_alert(owner, "too close!");
            return FALSE;
        }

        turf owner_turf = get_turf(owner) as turf;
        turf target_turf = get_turf(target_atom) as turf;
        if (target_turf == null)
            return FALSE;

        DmList open_exit_turfs = Dm.list();
        foreach (turf potential_exit in Dm.each<turf>(orange(1, target_turf)))
        {
            if (Dm.truthy(await potential_exit.is_blocked_turf(exclude_mobs: TRUE)))
                continue;
            Dm.append(ref open_exit_turfs, potential_exit);
        }

        if (Dm.length(open_exit_turfs) == 0)
        {
            owner.balloon_alert(owner, "no exit!");
            return FALSE;
        }
        if (!Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: TRUE)))
            Dm.append(ref open_exit_turfs, target_turf);

        Dm.@new<obj_effect_temp_visual_lesser_carp_rift_exit>(target_turf);
        obj_effect_temp_visual_lesser_carp_rift_entrance enter = Dm.@new<obj_effect_temp_visual_lesser_carp_rift_entrance>(owner_turf);
        enter.exit_locs = open_exit_turfs;
        enter.on_entered(enter, owner);
        return TRUE;
    }
}

/// If you touch the portal you can teleport, but only one person can use it.
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:89
public partial class obj_effect_temp_visual_lesser_carp_rift : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:96
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        destroy_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(animate_out)), duration - 1, TIMER_STOPPABLE, file: "code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm", line: 97);
        return base.Initialize(mapload, __extra);
    }

    /// Fade out and delete the rift
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:101
    public virtual void animate_out()
    {
        obj_effect_temp_visual_lesser_carp_rift_dissipating animate_out_effect = Dm.@new<obj_effect_temp_visual_lesser_carp_rift_dissipating>(loc);
        animate_out_effect.setup_animation(alpha);
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:105
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        deltimer(destroy_timer);
        return dot;
    }
}

/// If you touch this you will be teleported
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:110
public partial class obj_effect_temp_visual_lesser_carp_rift_entrance : obj_effect_temp_visual_lesser_carp_rift
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:116
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), TgState.Current.obj_effect_temp_visual_lesser_carp_rift_entrance__loc_connections));
        return dot;
    }

    /// Teleport the mover if they're a mob or an object
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:123
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable entered_atom = null)
    {
        if (Dm.length(exit_locs) == 0)
            return;
        if (!ismob(entered_atom) && !isobj(entered_atom))
            return;
        if (Dm.truthy(entered_atom.anchored))
            return;
        if (entered_atom.loc == null)
            return;
        if (isobserver(entered_atom))
            return;

        if (isliving(entered_atom))
        {
            mob_living teleported_mob = entered_atom as mob_living;
            teleported_mob.changeNext_move(disorient_time);
            if (teleported_mob.ai_controller != null)
                Waiting.NoSleep((teleported_mob.ai_controller as datum_ai_controller).cancel_current_plan());
        }

        turf destination = pick(exit_locs) as turf;
        Waiting.NoSleep(do_teleport(entered_atom, destination, channel: TELEPORT_CHANNEL_MAGIC));
        playsound(this, Dm.resource("sound/effects/magic/wand_teleport.ogg"), 50);
        playsound(destination, Dm.resource("sound/effects/magic/wand_teleport.ogg"), 50);
    }
}

/// Doesn't actually do anything, just a visual marker
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:143
public partial class obj_effect_temp_visual_lesser_carp_rift_exit : obj_effect_temp_visual_lesser_carp_rift
{
}

/// Just an animation
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:147
public partial class obj_effect_temp_visual_lesser_carp_rift_dissipating : obj_effect_temp_visual
{
    /// Sets up the animation
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:153
    public virtual void setup_animation(object new_alpha = null)
    {
        alpha = Dm.num(new_alpha);
        animate(this, Dm.named("alpha", 0), Dm.named("time", duration - 1));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_abilities.dm:118
    internal DmList obj_effect_temp_visual_lesser_carp_rift_entrance__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_temp_visual_lesser_carp_rift_entrance.on_entered)));
}
