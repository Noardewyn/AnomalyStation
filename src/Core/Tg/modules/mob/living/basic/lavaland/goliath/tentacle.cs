// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/goliath/tentacle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// A tentacle which grabs you if you don't get away from it
// tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:2
public partial class obj_effect_goliath_tentacle : obj_effect
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:22
    [DmParams("mapload", "goliath")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living goliath = Dm.arg<mob_living>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (ismineralturf(loc))
        {
            turf_closed_mineral floor = loc as turf_closed_mineral;
            Waiting.NoSleep(floor.gets_drilled());
        }
        if (!isopenturf(loc) || is_space_or_openspace(loc))
            return INITIALIZE_HINT_QDEL;
        foreach (obj_effect_goliath_tentacle tentacle in Dm.each<obj_effect_goliath_tentacle>(loc))
        {
            if (tentacle != this)
                return INITIALIZE_HINT_QDEL;
        }
        deltimer(action_timer);
        action_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(animate_grab)), 0.7f * SECONDS, TIMER_STOPPABLE, file: "code/modules/mob/living/basic/lavaland/goliath/tentacle.dm", line: 33);
        update_appearance(UPDATE_OVERLAYS);
        set_owner(goliath);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:37
    public override object Destroy(float? force = null)
    {
        deltimer(action_timer);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:41
    public virtual void set_owner(mob_living goliath = null)
    {
        if (owner != null)
            UnregisterSignal(owner, COMSIG_QDELETING);
        owner = goliath;
        if (owner != null)
            RegisterSignal(owner, COMSIG_QDELETING, nameof(on_owner_del));
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:48
    [SignalHandler]
    public virtual void on_owner_del(datum source = null)
    {
        owner = null;
        retract();
    }

    /// Change to next icon state and set up grapple
    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:54
    public virtual void animate_grab()
    {
        icon_state = "goliath_tentacle_wiggle";
        update_appearance(UPDATE_OVERLAYS);
        deltimer(action_timer);
        _addtimer(Dm.@new<datum_callback>(this, nameof(grab)), 0.3f * SECONDS, TIMER_STOPPABLE, file: "code/modules/mob/living/basic/lavaland/goliath/tentacle.dm", line: 58);
    }

    /// Grab everyone we share space with. If it's nobody, go home.
    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:61
    public virtual async DmTask grab()
    {
        float trapped_mobs = FALSE;
        foreach (mob_living victim in Dm.each<mob_living>(loc))
        {
            if (victim.stat == DEAD || owner != null && Dm.truthy(victim.faction_check_atom(owner)))
                continue;
            if (HAS_TRAIT(victim, TRAIT_TENTACLE_IMMUNE) || Dm.truthy(Dm.bitand(!Dm.truthy(victim._listen_lookup?[COMSIG_GOLIATH_TENTACLED_GRABBED]) ? NONE : victim._SendSignal(COMSIG_GOLIATH_TENTACLED_GRABBED, Dm.list(victim)), COMPONENT_GOLIATH_CANCEL_TENTACLE_GRAB)))
                continue;
            obj_item_restraints_legcuffs_goliath_tentacle trap = Dm.@new(trap_type, loc, victim, this) as obj_item_restraints_legcuffs_goliath_tentacle;
            if (QDELETED(trap))
                continue;
            balloon_alert(victim, "grabbed");
            visible_message(span_danger(I18n.T("[] grabs hold of []!", this, victim)));
            await victim.apply_damage(rand(min_damage, max_damage), BRUTE, pick(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG), wound_bonus: CANT_WOUND);
            trapped_mobs = TRUE;
        }

        foreach (obj_vehicle_sealed_mecha mech in Dm.each<obj_vehicle_sealed_mecha>(loc))
            await mech.take_damage(rand(min_damage, max_damage), damage_type: BRUTE, damage_flag: MELEE, sound_effect: TRUE);

        // Already retracting
        if (icon_state == "goliath_tentacle_retract")
            return;

        if (!Dm.truthy(trapped_mobs))
        {
            retract();
            return;
        }

        deltimer(action_timer);
        action_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(retract)), grapple_time, TIMER_STOPPABLE, file: "code/modules/mob/living/basic/lavaland/goliath/tentacle.dm", line: 88);
    }

    /// Play exit animation.
    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:91
    public virtual void retract()
    {
        if (icon_state == "goliath_tentacle_retract")
            return; // Already retracting
        if (Dm.truthy(_listen_lookup?[COMSIG_GOLIATH_TENTACLE_RETRACTING]))
            _SendSignal(COMSIG_GOLIATH_TENTACLE_RETRACTING, Dm.list(this));
        icon_state = "goliath_tentacle_retract";
        update_appearance(UPDATE_OVERLAYS);
        deltimer(action_timer);
        // QDEL_IN_STOPPABLE(src, 0.7 SECONDS), inline (rules 11.1 (f))
        action_timer = _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 0.7f * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 0.7f * SECONDS, TIMER_STOPPABLE, file: "code/modules/mob/living/basic/lavaland/goliath/tentacle.dm", line: 98);
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:100
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.Deferred("code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:102", "update_overlays: the tentacle's emissive_appearance overlays, its glow in the dark (DEVIATION DV12)");
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:105
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(dot))
        {
            retract();
            return TRUE;
        }
        return dot;
    }
}

// tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:115
public partial class obj_item_restraints_legcuffs_goliath_tentacle : obj_item_restraints_legcuffs
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:134
    [DmParams("mapload", "target", "tentacle")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living target = Dm.arg<mob_living>(__extra, 0);
        obj_effect_goliath_tentacle tentacle = Dm.arg<obj_effect_goliath_tentacle>(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_IGNORE_DEMOLITION, INNATE_TRAIT);
        this.tentacle = tentacle;
        if (target == null || !Dm.truthy(Waiting.NoSleep(target.equip_to_slot_if_possible(this, ITEM_SLOT_LEGCUFFED, disable_warning: TRUE, bypass_equip_delay_self: TRUE))))
            return INITIALIZE_HINT_QDEL;
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:141
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        if (tentacle != null)
        {
            tentacle.retract();
            tentacle = null;
        }
        if (leash != null)
        {
            UnregisterSignal(leash, COMSIG_QDELETING);
            QDEL_NULL(ref leash);
        }
        QDEL_NULL(ref beam_effect);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:151
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.eq(slot, ITEM_SLOT_LEGCUFFED) || leash != null)
            return;
        await leash_target(user as mob_living);
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:157
    [SignalHandler]
    public virtual void release(datum source = null)
    {
        qdel(this);
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:161
    [SignalHandler]
    public virtual void on_tentacle_left(datum source = null)
    {
        if (tentacle != null)
        {
            UnregisterSignal(tentacle, Dm.list(COMSIG_QDELETING, COMSIG_GOLIATH_TENTACLE_RETRACTING));
            tentacle = null;
        }
        release();
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:168
    public virtual async DmTask leash_target(mob_living user = null)
    {
        leash = await user.AddComponent(typeof(datum_component_leash), Dm.named("owner", get_turf(user)), Dm.named("distance", 1), Dm.named("silent", TRUE)) as datum_component_leash;
        beam_effect = user.Beam(get_turf(user) as atom, "goliath_tentacle", beam_type: typeof(obj_effect_ebeam_goliath), emissive: NONE) as datum_beam;
        RegisterSignal(beam_effect.visuals, COMSIG_CLICK, nameof(on_beam_click));
        RegisterSignals(user, Dm.list(SIGNAL_ADDTRAIT(TRAIT_TENTACLE_IMMUNE), COMSIG_BRIMDUST_EXPLOSION), nameof(release));
        RegisterSignals(tentacle, Dm.list(COMSIG_QDELETING, COMSIG_GOLIATH_TENTACLE_RETRACTING), nameof(on_tentacle_left));
        if (tentacle.owner != null)
            RegisterSignal(tentacle.owner, COMSIG_MOB_STATCHANGE, nameof(on_tentacle_left));
        RegisterSignal(leash, COMSIG_QDELETING, nameof(release));
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:178
    [SignalHandler]
    public virtual void on_beam_click(atom source = null, atom location = null, object control = null, object @params = null, mob user = null)
    {
        Waiting.Detach(process_beam_click(source, location, @params, user));
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:182
    public virtual async DmTask process_beam_click(atom source = null, atom location = null, object @params = null, mob user = null)
    {
        if (world.time <= Dm.num(user.next_move))
            return;

        obj_item held_thing = user.get_active_held_item() as obj_item;
        if (held_thing == null)
            return;

        turf nearest_turf = null;
        foreach (turf line_turf in Dm.each<turf>(get_line(get_turf(this), get_turf(beam_effect.target))))
        {
            if (Dm.truthy(await line_turf.IsReachableBy(user)))
            {
                nearest_turf = line_turf;
                break;
            }
        }

        if (nearest_turf == null)
            return;

        if (!Dm.truthy(await user.can_perform_action(nearest_turf)))
        {
            nearest_turf.balloon_alert(user, "cannot reach!");
            return;
        }

        await held_thing.melee_attack_chain(user, this, params2list(@params));
        user.changeNext_move(held_thing.attack_speed);
        playsound(user, held_thing.hitsound ?? Dm.resource("sound/effects/blob/attackblob.ogg"), held_thing.get_clamped_volume(), TRUE, extrarange: Dm.truthy(held_thing.stealthy_audio) ? SILENCED_SOUND_EXTRARANGE : -1, falloff_distance: 0);
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:207
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        return null;
    }
}

// tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:210
public partial class obj_item_restraints_legcuffs_goliath_tentacle_drag : obj_item_restraints_legcuffs_goliath_tentacle
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:212
    [DmParams("mapload", "target", "tentacle")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // QDEL_IN(src, 15 SECONDS), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 15 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 15 * SECONDS, file: "code/modules/mob/living/basic/lavaland/goliath/tentacle.dm", line: 214);
        if (!Dm.eq(dot, INITIALIZE_HINT_QDEL))
            START_PROCESSING(SSprocessing, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:218
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        STOP_PROCESSING(SSprocessing, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:222
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.num(leash.distance) <= 1)
            return PROCESS_KILL;
        leash.set_distance(Dm.num(leash.distance) - 1);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/tentacle.dm:227
    public override async DmTask leash_target(mob_living user = null)
    {
        if (tentacle?.owner == null)
        {
            await base.leash_target(user);
            return;
        }
        leash = await user.AddComponent(typeof(datum_component_leash), Dm.named("owner", tentacle.owner), Dm.named("distance", get_dist(user, tentacle.owner)), Dm.named("silent", TRUE)) as datum_component_leash;
        beam_effect = user.Beam(tentacle.owner, "goliath_tentacle", beam_type: typeof(obj_effect_ebeam_goliath), emissive: NONE) as datum_beam;
        RegisterSignal(beam_effect.visuals, COMSIG_CLICK, nameof(on_beam_click));
        RegisterSignals(user, Dm.list(SIGNAL_ADDTRAIT(TRAIT_TENTACLE_IMMUNE), COMSIG_BRIMDUST_EXPLOSION), nameof(release));
        RegisterSignals(tentacle.owner, Dm.list(COMSIG_QDELETING, COMSIG_MOB_STATCHANGE), nameof(on_tentacle_left));
        RegisterSignal(leash, COMSIG_QDELETING, nameof(release));
        tentacle.retract();
        tentacle = null;
    }
}
