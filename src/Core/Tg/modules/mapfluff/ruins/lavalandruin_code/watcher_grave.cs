// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the watchers' grave's egg, which hatches after a long walk into a watcher hatchling, the pet that orbits its owner
// and shoots the mining fauna about. The corpses' and the projectile's var blocks are generated.

/// Egg which hatches into a helpful pet. Or you can eat it if you want.
// tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:19
public partial class obj_item_food_egg_watcher : obj_item_food_egg
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:15-16 (file-local #defines)
    private const float WATCHER_EGG_LIVELY_MOD = 0.75f;
    private const float WATCHER_EGG_ACTIVE_MOD = 0.5f;

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pedometer = Dm.@new<datum_movement_detector>(this, Dm.@new<datum_callback>(this, nameof(on_stepped)));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:36
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref pedometer);
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:40
    public override void spawn_impact_chick(turf spawn_turf = null)
    {
        Dm.@new<obj_effect_spawner_random_lavaland_mob_watcher>(spawn_turf);
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:43
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        return Dm.add(await base.examine(user, __extra), span_notice(I18n.T("<i>Watch it more closely to see how it is doing...</i>")));
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:46
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (steps_travelled < (steps_to_hatch * WATCHER_EGG_ACTIVE_MOD))
            return Dm.add(dot, span_notice(I18n.T("Something stirs listlessly inside.")));
        if (steps_travelled < steps_to_hatch * WATCHER_EGG_LIVELY_MOD)
            return Dm.add(dot, span_notice(I18n.T("Something is moving actively inside.")));
        return Dm.add(dot, span_boldnotice(I18n.T("It's jiggling wildly, it's about to hatch!")));
    }

    /// Called when we are moved, whether inside an inventory or by ourself somehow
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:55
    public virtual void on_stepped(atom_movable egg = null, atom_movable mover = null, atom old_loc = null, object direction = null)
    {
        turf new_loc = get_turf(egg);
        if (new_loc == null || new_loc == get_turf(old_loc))
            return; // Didn't actually go anywhere
        steps_travelled++;
        if (steps_travelled == steps_to_hatch * WATCHER_EGG_ACTIVE_MOD)
        {
            jiggle();
            return;
        }
        if (steps_travelled < steps_to_hatch)
            return;
        visible_message(span_boldnotice(I18n.T("[] splits and unfurls into a baby Watcher!", this)));
        playsound(new_loc, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
        Dm.@new<obj_effect_decal_cleanable_greenglow>(new_loc);
        Dm.@new<obj_item_watcher_hatchling>(new_loc);
        qdel(this);
    }

    /// Animate the egg
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:72
    public virtual void jiggle()
    {
        float animation = isturf(loc) ? Dm.rand(1, 3) : 1; // Pixel_x/y animations don't work in an inventory
        switch (animation)
        {
            case 1:
                animate(this, Dm.named("transform", (transform as matrix).Scale(1.3f)), Dm.named("time", 1 * SECONDS), Dm.named("easing", BOUNCE_EASING));
                animate(Dm.named("transform", Dm.matrix()), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
                break;
            case 2:
                animate(this, Dm.named("pixel_y", 8), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
                animate(Dm.named("pixel_y", 0), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
                animate(Dm.named("pixel_y", 4), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
                animate(Dm.named("pixel_y", 0), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(BOUNCE_EASING, EASE_IN)));
                break;
            case 3:
                Shake(pixelshiftx: 2, pixelshifty: 0, shake_interval: 0.3f * SECONDS);
                break;
        }
        float next_jiggle = Dm.rand(5 * SECONDS, 10 * SECONDS) / (steps_travelled >= steps_to_hatch * WATCHER_EGG_LIVELY_MOD ? 2 : 1);
        _addtimer(Dm.@new<datum_callback>(this, nameof(jiggle)), next_jiggle, TIMER_DELETE_ME, file: "code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm", line: 89);
    }
}

/// A cute pet who will occasionally attack lavaland mobs for you
// tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:94
public partial class obj_item_watcher_hatchling : obj_item
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:104
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (orbiter != null)
        {
            watcher_return();
            return dot;
        }
        orbiter = Dm.@new<obj_effect_watcher_orbiter>(get_turf(this));
        orbiter.follow(user);
        owner = user as mob_living;
        RegisterSignal(owner, COMSIG_QDELETING, nameof(remove_owner));
        RegisterSignal(orbiter, COMSIG_QDELETING, nameof(remove_orbiter));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:115
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (orbiter == null)
            return dot;
        var holder = recursive_loc_check(this, typeof(mob)) as mob;
        if (holder != owner)
            watcher_return();
        return dot;
    }

    /// If the guy we are orbiting is deleted but somehow we aren't
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:124
    public virtual void remove_owner()
    {
        // SIGNAL_HANDLER
        UnregisterSignal(owner, COMSIG_QDELETING);
        owner = null;
    }

    /// In the more likely event that our orbiter is deleted, stop holding a reference to it
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:130
    public virtual void remove_orbiter()
    {
        // SIGNAL_HANDLER
        orbiter = null; // No need to unregister signal because we only call this when it deletes
    }

    /// Get back in your ball pikachu
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:135
    public virtual void watcher_return()
    {
        qdel(orbiter);
        remove_owner();
    }
}

/// Orbiting visual which shoots at mining mobs
// tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:142
public partial class obj_effect_watcher_orbiter : obj_effect
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:166
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(LAZYLEN(target_faction)))
            target_faction = string_list(target_faction) as DmList;
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // Shuttle rotation fucks with our position, we just want to stick with our guy
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:173
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:176
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        QDEL_NULL(ref tracker);
        return base.Destroy(force);
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:181
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!(shot_cooldown <= world.time)) // COOLDOWN_FINISHED(src, shot_cooldown), inline (rules 11.1 (a))
            return null;
        foreach (mob_living potential_target in Dm.each<mob_living>(oview(5, this)))
        {
            if (!ismining(potential_target) || potential_target.stat == DEAD)
                continue;
            if (!Dm.truthy(potential_target.has_faction(target_faction)) || Dm.truthy(parent.has_ally(potential_target)))
                continue;
            shoot_at(potential_target);
            return null;
        }
        return null;
    }

    /// Take a shot
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:192
    public virtual void shoot_at(atom target = null)
    {
        DmList ignore_targets = Dm.list(parent);
        if (parent is mob_living living_parent)
        {
            if (Dm.truthy(LAZYLEN(living_parent.buckled_mobs)))
                Dm.append(ref ignore_targets, living_parent.buckled_mobs);
            if (living_parent.buckled != null)
                Dm.append(ref ignore_targets, living_parent.buckled);
        }
        shot_cooldown = world.time + fire_delay; // COOLDOWN_START(src, shot_cooldown, fire_delay)
        Waiting.NoSleep(fire_projectile(projectile_type, target, projectile_sound, ignore_targets: ignore_targets));
    }

    /// Set ourselves up to track and orbit around a guy
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:205
    public virtual void follow(atom_movable target = null)
    {
        parent = target;
        glide_size = target.glide_size;
        animate(this, Dm.named("pixel_y", 26), Dm.named("alpha", 255), Dm.named("time", 0.5f * SECONDS));
        _addtimer(Dm.@new<datum_callback>(this, nameof(orbit_animation)), 0.5f * SECONDS, TIMER_DELETE_ME, file: "code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm", line: 209);
        tracker = Dm.@new<datum_movement_detector>(target, Dm.@new<datum_callback>(this, nameof(on_parent_moved)));
        RegisterSignal(target, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE, nameof(on_glide_size_changed));
        RegisterSignal(target, COMSIG_QDELETING, nameof(on_parent_deleted));
        RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_parent_died));
        RegisterSignal(target, COMSIG_LIVING_REVIVE, nameof(on_parent_revived));
    }

    /// Do our orbiting animation
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:217
    public virtual void orbit_animation()
    {
        animate(this, Dm.named("pixel_y", 26), Dm.named("time", 1 * SECONDS), Dm.named("loop", -1), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_PARALLEL));
        animate(Dm.named("pixel_y", 18), Dm.named("time", 1 * SECONDS), Dm.named("easing", SINE_EASING));
        animate(this, Dm.named("pixel_x", 20), Dm.named("time", 0.5f * SECONDS), Dm.named("loop", -1), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)), Dm.named("flags", ANIMATION_PARALLEL));
        animate(Dm.named("pixel_x", 0), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
        animate(Dm.named("pixel_x", -20), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
        animate(Dm.named("pixel_x", 0), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
    }

    /// Follow our parent
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:226
    public virtual void on_parent_moved(atom_movable parent = null, atom_movable mover = null, atom old_loc = null, object direction = null)
    {
        if (parent.loc == old_loc)
            return;
        turf new_turf = get_turf(parent);
        if (new_turf == null)
        {
            qdel(this);
            return;
        }
        if (loc != new_turf)
            Waiting.NoSleep(abstract_move(new_turf));
    }

    /// Make sure we glide at the same speed as our parent
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:237
    public virtual void on_glide_size_changed(atom source = null, object new_glide_size = null)
    {
        // SIGNAL_HANDLER
        glide_size = Dm.num(new_glide_size);
    }

    /// Called if the guy we're tracking is deleted somehow
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:242
    public virtual void on_parent_deleted()
    {
        // SIGNAL_HANDLER
        parent = null;
        qdel(this);
    }

    /// We must guard this corpse
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:248
    public virtual void on_parent_died(mob_living parent = null)
    {
        // SIGNAL_HANDLER
        visible_message(span_notice(I18n.T("[] emits a piteous keening in mourning of []!", this, parent)));
        fire_delay /= on_death_multiplier;
    }

    /// Exit hyperactive mode
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:254
    public virtual void on_parent_revived(mob_living parent = null)
    {
        // SIGNAL_HANDLER
        visible_message(span_notice(I18n.T("[] chirps happily as [] suddenly gasps for breath!", this, parent)));
        fire_delay *= on_death_multiplier;
    }
}

/// Beam fired by a baby watcher, doesn't actually do less damage than its parent
// tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:262
public partial class obj_projectile_baby_watcher_blast : obj_projectile
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/watcher_grave.dm:269
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        transform = (transform as matrix).Scale(0.5f);
        return dot;
    }
}
