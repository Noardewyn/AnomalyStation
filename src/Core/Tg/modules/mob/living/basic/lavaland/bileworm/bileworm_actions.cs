// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:1
public partial class datum_action_cooldown_mob_cooldown_resurface : datum_action_cooldown_mob_cooldown
{
    /// Amount of frames in the jump animation
    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:24  #define BILEWORM_JUMP_FRAMES 14 (file-local, #undef at :81)
    private const int BILEWORM_JUMP_FRAMES = 14;

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:12
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        if (owner != null)
            await owner.AddElement(typeof(datum_element_relay_attackers));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:16
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        StartCooldownSelf(INFINITY);
        StartCooldownOthers(INFINITY);
        await burrow(owner as mob_living, target_atom);
        //spew now off cooldown shortly
        StartCooldownOthers(1.5f * SECONDS);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:26
    public virtual async DmTask burrow(mob_living burrower = null, atom target = null, float? force = null)
    {
        force ??= FALSE;
        turf unburrow_turf = get_unburrow_turf(burrower, target) as turf;
        if (unburrow_turf == null) // means all the turfs nearby are station turfs or something, not lavaland
        {
            to_chat(burrower, span_warning(I18n.T("Couldn't burrow anywhere near the target!")));
            if (Dm.eq((burrower.ai_controller as datum_ai_controller)?.ai_status, AI_STATUS_ON))
                //this is a valid reason to give up on a target
                (burrower.ai_controller as datum_ai_controller).clear_blackboard_key(BB_CURRENT_TARGET);
            return;
        }

        if (burrower is mob_living_basic_mining_bileworm && !Dm.truthy(force))
        {
            mob_living_basic_mining_bileworm worm = burrower as mob_living_basic_mining_bileworm;
            Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(worm));
            string old_icon_state = worm.icon_state;
            worm.icon_state = null;
            // Get rid of the base emissive
            worm.jumping = TRUE;
            worm.update_appearance(UPDATE_OVERLAYS);
            atom_movable visual = worm.flick_overlay_view(mutable_appearance(worm.icon_jump, Dm.text("[]_jump_1", worm.icon_living)), jump_length) as atom_movable;
            visual.vis_flags = Dm.bitor(visual.vis_flags, VIS_INHERIT_ID); // So you can attack it
            // tg also flicks the emissive jump frames (emissive_appearance(worm.icon_jump, "[worm.emissive_state]_jump_1", worm)): the glow in the dark (DEVIATION DV12)
            // We have to manually animate these to get around BYOND icon_state animations happening in world time and not per-object
            for (float i = 2; i <= BILEWORM_JUMP_FRAMES; i++)
                animate(visual, Dm.named("time", jump_length / BILEWORM_JUMP_FRAMES), Dm.named("icon_state", Dm.text("[]_jump_[]", worm.icon_living, i)), Dm.named("flags", ANIMATION_CONTINUE));
            jump_damaged = FALSE;
            RegisterSignal(worm, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
            // Not in an if check direclty to reduce duplicate code
            object jump_result = await do_after(worm, jump_length, worm, cog_icon: null, extra_checks: Dm.@new<datum_callback>(this, nameof(damage_check)));
            UnregisterSignal(worm, COMSIG_ATOM_WAS_ATTACKED);
            if (worm.icon_state == null)
                worm.icon_state = old_icon_state;
            worm.jumping = FALSE;
            worm.update_appearance(UPDATE_OVERLAYS);
            if (!Dm.truthy(jump_result) && Dm.truthy(jump_stun))
            {
                if (!QDELETED(visual))
                    QDEL_NULL(ref visual);
                worm.flick_overlay_view(mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "dazed"), jump_stun);
                await worm.Stun(jump_stun);
                _addtimer(Dm.@new<datum_callback>(this, nameof(burrow_again), burrower, target), jump_stun, file: "code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm", line: 65);
                return;
            }
        }

        (burrower.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BILEWORM_SCARED, FALSE);
        playsound(burrower, Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE);
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(burrower));
        ADD_TRAIT(burrower, TRAIT_GODMODE, REF(this));
        burrower.SetInvisibility(INVISIBILITY_MAXIMUM, id: type);
        await burrower.forceMove(unburrow_turf);
        //not that it's gonna die with godmode but still
        // SLEEP_CHECK_DEATH(rand(0.7 SECONDS, 1.2 SECONDS), burrower) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(rand(0.7f * SECONDS, 1.2f * SECONDS));
        if (QDELETED(burrower) || burrower.stat == DEAD)
            return;
        playsound(burrower, Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE);
        Dm.@new<obj_effect_temp_visual_mook_dust>(unburrow_turf);
        REMOVE_TRAIT(burrower, TRAIT_GODMODE, REF(this));
        burrower.RemoveInvisibility(type);
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:83
    public virtual async DmTask burrow_again(mob_living burrower = null, atom target = null)
    {
        if (!QDELETED(burrower) && !IS_UNCONSCIOUS_OR_CRIT(burrower))
            // Burrow immediatelly after being stunned out of the first jump to avoid chainstuns
            await burrow(burrower, target, force: TRUE);
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:88
    [SignalHandler]
    public virtual void on_attacked(datum source = null, atom attacker = null, object attack_flags = null)
    {
        if (!Dm.truthy(Dm.bitand(attack_flags, Dm.bitor(ATTACKER_STAMINA_ATTACK, ATTACKER_SHOVING))) && Dm.truthy(jump_stun))
            jump_damaged = TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:93
    public virtual object damage_check()
    {
        return Dm.not(jump_damaged);
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:96
    public virtual object get_unburrow_turf(mob_living burrower = null, atom target = null)
    {
        DmList potential_turfs = shuffle(oview(5, target)) as DmList; // get in view, shuffle
        foreach (turf_open_misc chosen_one in Dm.each<turf_open_misc>(potential_turfs)) // first turf that counts as ground
            return chosen_one;
        return null;
    }
}

// tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:101
public partial class datum_action_cooldown_mob_cooldown_bileworm_spew : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:115
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        StartCooldownSelf(INFINITY);
        await attack_sequence(owner as mob_living, target_atom);
        StartCooldownSelf();
        // Resurface now off cooldown shortly
        StartCooldownOthers(2 * SECONDS); // Enough time for a mark + detonation combo, or 3 shots with a PKA
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:122
    public virtual async DmTask attack_sequence(mob_living firer = null, atom target = null)
    {
        Dm.@new(acid_type, firer, target);
        playsound(firer, projectile_sound, 70);
        DmList all_dirs = GLOB.alldirs.Copy();
        turf hit_turf = get_turf(target) as turf;
        // One guaranteed to hit target's turf, the rest hit randomly around them
        for (float i = 1, i__end = additional_shots; i <= i__end; i++)
        {
            if (!Dm.truthy(Dm.length(all_dirs)))
                return;

            // SLEEP_CHECK_DEATH(shot_delay, firer) (__DEFINES/mobs.dm:627), expanded inline
            await sleep(shot_delay);
            if (QDELETED(firer) || firer.stat == DEAD)
                return;
            if (hit_turf != target.loc)
                all_dirs = Dm.add(Dm.list(NONE), GLOB.alldirs) as DmList; // Refresh potential target turfs if they've moved

            turf target_turf = null;
            // NODE drones get a much more focused barrage sent at them
            if (target is mob_living_basic_node_drone && prob(30))
                target_turf = get_turf(target) as turf;
            else
            {
                object target_dir = pick_n_take(all_dirs);
                target_turf = Dm.truthy(target_dir) ? get_step(target, Dm.num(target_dir)) : get_turf(target) as turf;
            }

            if (target_turf == firer.loc) // Don't hit ourselves
            {
                object target_dir = pick_n_take(all_dirs);
                target_turf = Dm.truthy(target_dir) ? get_step(target, Dm.num(target_dir)) : get_turf(target) as turf;
            }

            Dm.@new(acid_type, firer, target_turf);
            if (i % 2 == 0)
                playsound(firer, projectile_sound, 70);
        }
    }
}

// tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:152
public partial class obj_effect_bileworm_acid : obj_effect
{
    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:167
    [DmParams("mapload", "new_target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom new_target = Dm.arg<atom>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        if (new_target != null)
            Waiting.NoSleep(fire_at(get_turf(new_target) as turf));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:173
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref target_sign);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:177
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.Deferred("code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:179", "update_overlays: the bile glob's emissive_appearance, its glow in the dark (DEVIATION DV12)");
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:181
    public virtual async DmTask fire_at(turf target = null)
    {
        if (target == null)
            target = get_turf(target) as turf;
        turf start = get_turf(this) as turf;
        await forceMove(target); // Immediately move to the target turf so we can guarantee they can see us
        pixel_x = (start.x - target.x) * ICON_SIZE_X;
        pixel_y = (start.y - target.y) * ICON_SIZE_Y;
        // Because BYOND's get_dist is taxicab distance
        float travel_dist = Dm.sqrt(Dm.pow(start.x - target.x, 2) + Dm.pow(start.y - target.y, 2));
        // Slightly scale the speed with distance for fairer gameplay
        if (travel_dist < Dm.num(max_range))
            speed = Dm.num(speed) * (0.5f + travel_dist / Dm.num(max_range) * 0.5f);
        float travel_time = travel_dist / Dm.num(speed) * 1 * SECONDS;
        animate(this, Dm.named("pixel_x", 0), Dm.named("time", travel_time), Dm.named("flags", ANIMATION_PARALLEL), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT))); // Not a perfect arc but looks better
        animate(this, Dm.named("pixel_y", 160), Dm.named("time", travel_time / 2), Dm.named("flags", ANIMATION_PARALLEL), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_OUT)));
        animate(Dm.named("pixel_y", 0), Dm.named("time", travel_time / 2), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)));
        animate(this, Dm.named("transform", Dm.matrix().Turn(start.x > target.x ? -91 : 91)), Dm.named("time", travel_time / 2), Dm.named("flags", ANIMATION_PARALLEL), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
        animate(Dm.named("transform", Dm.matrix().Turn(start.x > target.x ? -180 : 180)), Dm.named("time", travel_time / 2), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
        target_sign = Dm.@new<obj_effect_bileworm_target>(target);
        _addtimer(Dm.@new<datum_callback>(this, nameof(impact), target), travel_time, file: "code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm", line: 200);
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:202
    public virtual async DmTask impact(turf target = null)
    {
        // new /obj/effect/abstract/particle_holder(target, /particles/bile), QDEL_IN: the splash of goop
        Dm.Deferred("code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:203", "impact: the bile splash's particle holder (the goop particles: the particle holder is not ported)");

        float hit_something = FALSE;
        foreach (mob_living victim in Dm.each<mob_living>(target))
        {
            if (HAS_TRAIT(target, TRAIT_UNHITTABLE_BY_PROJECTILES))
            {
                if (!HAS_TRAIT(target, TRAIT_BLOCKING_PROJECTILES) && isliving(target))
                {
                    mob_living living_target = (object)target as mob_living;
                    Waiting.NoSleep(living_target.block_projectile_effects());
                }
                continue;
            }

            // Doesn't make much sense to use melee for mobs, but its a mining mob so eeeeeh
            // Can't use acid either as its mostly for atom armor only
            object blocked = victim.run_armor_check(null, MELEE, armour_penetration: 40, silent: TRUE);
            if (Dm.num(blocked) >= 100)
                continue;

            hit_something = TRUE;
            await victim.apply_damage(damage, BURN, null, Dm.num(blocked), wound_bonus: CANT_WOUND);
            to_chat(victim, span_userdanger(I18n.T("You're hit by []!", this)));
        }

        foreach (obj thing in Dm.each<obj>(target))
        {
            if (!Dm.truthy(thing.uses_integrity) || !Dm.truthy(thing.density))
                continue;
            hit_something = TRUE;
            await thing.take_damage(damage, BURN, ACID, sound_effect: FALSE);
        }

        if (Dm.truthy(hit_something))
            playsound(target, Dm.resource("sound/items/weapons/sear.ogg"), 50, -1);

        qdel(this);
    }
}

// tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:235
public partial class obj_effect_bileworm_target : obj_effect
{
    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:241
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:245
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.Deferred("code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:247", "update_overlays: the target circle's emissive_appearance, its glow in the dark (DEVIATION DV12)");
        return dot;
    }
}

// tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:265
public partial class datum_action_cooldown_mob_cooldown_devour : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:270
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        if (target_atom == owner)
        {
            to_chat(owner, span_warning(I18n.T("You can't eat yourself!")));
            return null;
        }
        if (!isliving(target_atom))
        {
            to_chat(owner, span_warning(I18n.T("That's not food!")));
            return null;
        }
        mob_living living_target = target_atom as mob_living;
        if (!IS_UNCONSCIOUS(living_target))
        {
            to_chat(owner, span_warning(I18n.T("No way you're eating that while it's still kicking! It should at least be unconscious first.")));
            return null;
        }
        await burrow_and_devour(owner as mob_living, living_target);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_actions.dm:283
    public virtual async DmTask burrow_and_devour(mob_living devourer = null, mob_living target = null)
    {
        turf devour_turf = get_turf(target) as turf;
        if (!(devour_turf is turf_open_misc)) // means all the turfs nearby are station turfs or something, not lavaland
        {
            to_chat(devourer, span_warning(I18n.T("Your target is on something you can't burrow through!")));
            return; //this will give up on devouring the target which is fine by me
        }
        playsound(devourer, Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE);
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(devourer));
        ADD_TRAIT(devourer, TRAIT_GODMODE, REF(this));
        devourer.SetInvisibility(INVISIBILITY_MAXIMUM, id: type);
        await devourer.forceMove(devour_turf);
        //not that it's gonna die with godmode but still
        // SLEEP_CHECK_DEATH(rand(0.7 SECONDS, 1.2 SECONDS), devourer) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(rand(0.7f * SECONDS, 1.2f * SECONDS));
        if (QDELETED(devourer) || devourer.stat == DEAD)
            return;
        playsound(devourer, Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE);
        Dm.@new<obj_effect_temp_visual_mook_dust>(devour_turf);
        REMOVE_TRAIT(devourer, TRAIT_GODMODE, REF(this));
        devourer.RemoveInvisibility(type);
        if (!Dm.@in(target, devour_turf))
        {
            to_chat(devourer, span_warning(I18n.T("Someone stole your dinner!")));
            return;
        }
        to_chat(target, span_userdanger(I18n.T("You are consumed by []!", devourer)));
        devourer.visible_message(span_warning(I18n.T("[] consumes []!", devourer, target)));
        await devourer.fully_heal();
        playsound(devourer, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
        //to be received on death
        await target.forceMove(devourer);
    }
}
