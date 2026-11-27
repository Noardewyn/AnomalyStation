// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Do something nasty to everyone nearby if they're looking at us.
 */
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:4
public partial class datum_action_cooldown_mob_cooldown_watcher_gaze : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:31
    public override async DmTask<object> Activate(atom target = null)
    {
        proximity_monitor = Dm.@new<datum_proximity_monitor_watcher_gaze>(owner, effect_radius, Dm.named("ability", this));
        // Start tracking all potential victims in range as proxmon won't trigger on them
        foreach (mob_living victim in Dm.each<mob_living>(viewers(effect_radius, owner)))
            on_entered(victim);
        show_indicator_overlay("eye_open");
        stage_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(show_indicator_overlay), "eye_pulse"), animation_time, TIMER_STOPPABLE, file: "code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm", line: 37);
        StartCooldown(360 * SECONDS, 360 * SECONDS);
        owner.visible_message(span_warning(I18n.T("[]'s eye glows ominously!", owner)));
        if (Dm.truthy(await do_after(owner, delay: wait_delay, target: owner, cog_icon: null)))
        {
            await trigger_effect();
            proxmon_cleanup();
        }
        else
        {
            deltimer(stage_timer);
            clear_current_overlay();
            proxmon_cleanup();
            tracked_mobs.Cut();
        }
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:51
    public override object Destroy(float? force = null)
    {
        proxmon_cleanup();
        tracked_mobs.Cut();
        deltimer(stage_timer);
        clear_current_overlay();
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:58
    public override async DmTask Remove(mob removed_from = null)
    {
        proxmon_cleanup();
        tracked_mobs.Cut();
        deltimer(stage_timer);
        clear_current_overlay();
        await base.Remove(removed_from);
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:65
    public virtual void proxmon_cleanup()
    {
        if (!QDELETED(proximity_monitor))
            QDEL_NULL(ref proximity_monitor);
        foreach (object victim_ref in Dm.each(tracked_mobs))
        {
            mob_living victim = Dm.locate_ref(victim_ref) as mob_living;
            if (victim != null)
                UnregisterSignal(victim, Dm.list(COMSIG_ATOM_POST_DIR_CHANGE, COMSIG_MOB_STATCHANGE));
        }
    }

    /// Do some effects to whoever is looking at us
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:74
    public virtual async DmTask trigger_effect()
    {
        deltimer(stage_timer);
        show_indicator_overlay("eye_flash");
        foreach (mob_living viewer in Dm.each<mob_living>(viewers(effect_radius, owner)))
        {
            if (!Dm.truthy(valid_target(viewer)))
                continue;
            if (!Dm.truthy(await apply_effect(viewer)) || viewer.client == null)
                continue;
            image flashed_overlay = image(
                icon: Dm.resource("icons/effects/eldritch.dmi"),
                loc: viewer,
                icon_state: "eye_flash");
            flashed_overlay.pixel_w = -viewer.pixel_x;
            flashed_overlay.pixel_z = -viewer.pixel_y;
            flick_overlay_global(flashed_overlay, show_to: GLOB.clients, duration: animation_time);
        }
        stage_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(hide_eye)), animation_time, TIMER_STOPPABLE, file: "code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm", line: 90);
        mob_living living_owner = owner as mob_living;
        await living_owner.Stun(1.5f * SECONDS, ignore_canstun: TRUE);
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:94
    public virtual object valid_target(mob_living viewer = null)
    {
        if (viewer == null || IS_UNCONSCIOUS_OR_CRIT(viewer) || viewer == owner)
            return FALSE;
        if (!Dm.truthy(Dm.bitand(viewer.dir, get_dir(viewer, owner))))
            return FALSE;
        return TRUE;
    }

    /// Do something bad to someone who was looking at us
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:102
    public virtual async DmTask<object> apply_effect(mob_living viewer = null)
    {
        if (!Dm.truthy(await viewer.flash_act(intensity: 4, affect_silicon: TRUE, visual: TRUE, length: 3 * SECONDS)))
            return FALSE;
        await viewer.set_confusion_if_lower(12 * SECONDS);
        to_chat(viewer, span_warning(I18n.T("You are blinded by []'s piercing gaze!", owner)));
        return TRUE;
    }

    /// Animate our effect out
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:110
    public virtual void hide_eye()
    {
        show_indicator_overlay("eye_close");
        tracked_mobs.Cut();
        stage_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(clear_current_overlay)), animation_time, TIMER_STOPPABLE, file: "code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm", line: 113);
    }

    /// Display an animated overlay over our head to indicate what's going on
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:116
    public virtual void show_indicator_overlay(object overlay_state = null)
    {
        clear_current_overlay();
        current_overlay = image(icon: Dm.resource("icons/effects/eldritch.dmi"), loc: owner, icon_state: Dm.text("[]_y", overlay_state), layer: ABOVE_ALL_MOB_LAYER);
        current_overlay.pixel_w = -owner.pixel_x;
        current_overlay.pixel_z = 28;
        SET_PLANE_EXPLICIT(current_overlay, ABOVE_LIGHTING_PLANE, owner);
        // This will cause turning to reset the animation *but* this is the best option
        // as modifying alpha requires readding the image to client.images for it to actually update
        danger_overlay = image(icon: Dm.resource("icons/effects/eldritch.dmi"), loc: owner, icon_state: overlay_state, layer: ABOVE_ALL_MOB_LAYER);
        danger_overlay.pixel_w = -owner.pixel_x;
        danger_overlay.pixel_z = 28;
        SET_PLANE_EXPLICIT(danger_overlay, ABOVE_LIGHTING_PLANE, owner);

        foreach (client add_to in Dm.each<client>(GLOB.clients))
        {
            mob_living victim = add_to.mob as mob_living;
            if (victim != null && Dm.truthy(tracked_mobs[REF(victim)]))
                Dm.append(add_to.images, danger_overlay);
            else
                Dm.append(add_to.images, current_overlay);
        }
    }

    /// Hide whatever overlay we are showing
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:137
    public virtual void clear_current_overlay()
    {
        if (current_overlay != null)
        {
            remove_image_from_clients(current_overlay, GLOB.clients);
            remove_image_from_clients(danger_overlay, GLOB.clients);
        }
        current_overlay = null;
        danger_overlay = null;
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:144
    public virtual void on_entered(mob_living arrived = null)
    {
        if (arrived == owner)
            return;
        // Already tracked
        if (tracked_mobs[REF(arrived)] == null)
            RegisterSignals(arrived, Dm.list(COMSIG_ATOM_POST_DIR_CHANGE, COMSIG_MOB_STATCHANGE), nameof(update_state));
        update_state(arrived);
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:152
    public virtual void on_exited(mob_living exited = null)
    {
        if (exited == owner)
            return;
        UnregisterSignal(exited, Dm.list(COMSIG_ATOM_POST_DIR_CHANGE, COMSIG_MOB_STATCHANGE));
        Dm.remove(ref tracked_mobs, REF(exited));
        if (current_overlay != null && exited.client != null)
        {
            Dm.append(exited.client.images, current_overlay);
            Dm.remove(exited.client.images, danger_overlay);
        }
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:161
    [SignalHandler]
    public virtual void update_state(mob_living target = null)
    {
        // Don't do viewers(), too costly and only applies for thermals anyways
        if (Dm.truthy(valid_target(target)))
        {
            if (!Dm.truthy(tracked_mobs[REF(target)]))
            {
                tracked_mobs[REF(target)] = TRUE;
                if (current_overlay != null && target.client != null)
                {
                    Dm.remove(target.client.images, current_overlay);
                    Dm.append(target.client.images, danger_overlay);
                }
            }
        }
        else if (!Dm.eq(tracked_mobs[REF(target)], FALSE)) // Can be null
        {
            tracked_mobs[REF(target)] = FALSE;
            if (current_overlay != null && target.client != null)
            {
                Dm.append(target.client.images, current_overlay);
                Dm.remove(target.client.images, danger_overlay);
            }
        }
    }
}

// No need to refresh targets when the owner moves as the ability uses a do_after and will stop if the owner moves anyways
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:177
public partial class datum_proximity_monitor_watcher_gaze : datum_proximity_monitor
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:181
    public override object Destroy(float? force = null)
    {
        gaze = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:185
    [DmParams("_host", "range", "_ignore_if_not_on_turf", "ability")]
    public override async DmTask New(params object[] __extra)
    {
        object _host = Dm.arg(__extra, 0);
        object range = Dm.arg(__extra, 1);
        object _ignore_if_not_on_turf = Dm.arg(__extra, 2);
        datum_action_cooldown_mob_cooldown_watcher_gaze ability = Dm.arg<datum_action_cooldown_mob_cooldown_watcher_gaze>(__extra, 3);
        _ignore_if_not_on_turf ??= TRUE;
        await base.New(Dm.forward(__extra, _host, range, _ignore_if_not_on_turf, ability));
        gaze = ability;
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:189
    public override void on_entered(atom source = null, atom_movable arrived = null, turf old_loc = null)
    {
        if (source != host && arrived != host && isliving(arrived))
            gaze.on_entered(arrived as mob_living);
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:193
    public override void on_uncrossed(atom source = null, atom_movable gone = null, object direction = null)
    {
        if (source != host && gone != host && isliving(gone))
            gaze.on_exited(gone as mob_living);
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:197
    public override void on_initialized(turf location = null, atom created = null, object init_flags = null)
    {
        if (isliving(created))
            gaze.on_entered(created as mob_living);
    }
}

/// Magmawing glare burns you
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:202
public partial class datum_action_cooldown_mob_cooldown_watcher_gaze_fire : datum_action_cooldown_mob_cooldown_watcher_gaze
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:206
    public override async DmTask<object> apply_effect(mob_living viewer = null)
    {
        to_chat(viewer, span_warning(I18n.T("[]'s searing glare forces you to the ground!", owner)));
        await viewer.Paralyze(3 * SECONDS);
        await viewer.adjust_fire_stacks(10);
        viewer.ignite_mob();
        return TRUE;
    }
}

/// Icewing glare freezes you
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:214
public partial class datum_action_cooldown_mob_cooldown_watcher_gaze_ice : datum_action_cooldown_mob_cooldown_watcher_gaze
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_gaze.dm:220 (tg freezes only the cold-resistant: kept as tg has it)
    public override async DmTask<object> apply_effect(mob_living viewer = null)
    {
        if (!HAS_TRAIT(viewer, TRAIT_RESISTCOLD))
            return null;
        to_chat(viewer, span_warning(I18n.T("You are repulsed by the force of []'s cold stare!", owner)));
        await viewer.apply_status_effect(typeof(datum_status_effect_freon_watcher_extended));
        await viewer.safe_throw_at(
            target: get_edge_target_turf(owner, get_dir(owner, get_step_away(viewer, owner))) as atom,
            range: max_throw,
            speed: 1,
            thrower: owner,
            force: MOVE_FORCE_EXTREMELY_STRONG);
        return null;
    }
}
