// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/fire_stacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 3 (fire): the whole file. The var blocks of /datum/status_effect/fire_handler and its fire_stacks subtype are the
// hand-written ones of the M2 contracts (rules 10.2: kept); the others are generated. Deferred leaves (the burning memory
// since gaps package G4): the fire's emissive overlay (emissive_appearance, the emissive plane, which the client's
// light does not use: D24) and the wet stacks' droplet particles (particles, a client visual).

// tg: code/datums/status_effects/debuffs/fire_stacks.dm:1
[DmPath("/datum/status_effect/fire_handler")]
public partial class datum_status_effect_fire_handler : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:2-9  the var overrides of the type block (rules 10.2). Fix
    // translation: the contracts skeleton declared the type without them, so tg's status_effect_ids and status_effect_alert unit
    // tests (status_effect_validity.dm) saw a base id and alert type on it.
    public datum_status_effect_fire_handler()
    {
        duration = STATUS_EFFECT_PERMANENT;
        id = STATUS_EFFECT_ID_ABSTRACT;
        alert_type = null;
        status_type = STATUS_EFFECT_REFRESH; //Custom code
        on_remove_on_mob_delete = TRUE;
        tick_interval = 2 * SECONDS;
        processing_speed = STATUS_EFFECT_PRIORITY;
    }

    /// Current amount of stacks we have
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:11
    public object stacks;
    /// Maximum of stacks that we could possibly get
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:13
    public float stack_limit = MAX_FIRE_STACKS;
    /// For how much firestacks does one our stack count
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:20
    public float stack_modifier = 1;
    // enemy_types, merge_types and override_types (:15-19): generated (the vars the hand-written class does not declare)

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:22
    [DmParams("effect", "new_stacks", "forced")]
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        object new_stacks = Dm.arg(__extra, 0);
        object forced = Dm.arg(__extra, 1) ?? FALSE;
        if (Dm.truthy(forced))
            set_stacks(new_stacks);
        else
            adjust_stacks(new_stacks);
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:28
    [DmParams("new_owner", "new_stacks", "forced")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object new_stacks = Dm.arg(__extra, 0);
        object forced = Dm.arg(__extra, 1) ?? FALSE;
        object dot = await base.on_creation(new_owner, __extra);

        if (isanimal(owner))
        {
            qdel(this);
            return dot;
        }
        if (isbasicmob(owner))
        {
            if (!Dm.truthy(check_basic_mob_immunity(owner as mob_living_basic)))
            {
                qdel(this);
                return dot;
            }
        }

        owner = new_owner;
        set_stacks(new_stacks);

        foreach (object enemy_type in Dm.each(enemy_types))
        {
            datum_status_effect_fire_handler enemy_effect = owner.has_status_effect(enemy_type) as datum_status_effect_fire_handler;
            if (Dm.truthy(enemy_effect))
            {
                if (Dm.truthy(forced))
                {
                    qdel(enemy_effect);
                    continue;
                }

                float cur_stacks = Dm.num(stacks);
                adjust_stacks(-abs(Dm.num(enemy_effect.stacks) * enemy_effect.stack_modifier / stack_modifier));
                enemy_effect.adjust_stacks(-abs(cur_stacks * stack_modifier / enemy_effect.stack_modifier));
                if (Dm.num(enemy_effect.stacks) <= 0)
                    qdel(enemy_effect);

                if (Dm.num(stacks) <= 0)
                {
                    qdel(this);
                    return dot;
                }
            }
        }

        if (!Dm.truthy(forced))
        {
            DmList merge_effects = Dm.list();
            foreach (object merge_type in Dm.each(merge_types))
            {
                datum_status_effect_fire_handler merge_effect = owner.has_status_effect(merge_type) as datum_status_effect_fire_handler;
                if (Dm.truthy(merge_effect))
                    Dm.append(ref merge_effects, merge_effects);
            }

            if (Dm.truthy(LAZYLEN(merge_effects)))
            {
                foreach (datum_status_effect_fire_handler merge_effect in Dm.each<datum_status_effect_fire_handler>(merge_effects))
                    merge_effect.adjust_stacks(Dm.num(stacks) * stack_modifier / merge_effect.stack_modifier / LAZYLEN(merge_effects));
                qdel(this);
                return dot;
            }
        }

        foreach (object override_type in Dm.each(override_types))
        {
            datum_status_effect_fire_handler override_effect = owner.has_status_effect(override_type) as datum_status_effect_fire_handler;
            if (Dm.truthy(override_effect))
            {
                if (Dm.truthy(forced))
                {
                    qdel(override_effect);
                    continue;
                }

                adjust_stacks(override_effect.stacks);
                qdel(override_effect);
            }
        }
        return dot;
    }

    /**
     * Setter and adjuster procs for firestacks
     *
     * Arguments:
     * - new_stacks
     *
     */

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:90
    public virtual void set_stacks(object new_stacks = null)
    {
        stacks = max(0, min(stack_limit, Dm.num(new_stacks)));
        cache_stacks();
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:94
    public virtual void adjust_stacks(object new_stacks = null)
    {
        stacks = max(0, min(stack_limit, Dm.num(stacks) + Dm.num(new_stacks)));
        cache_stacks();
    }

    /// Checks if the applicable basic mob is immune to the status effect we're trying to apply. Returns TRUE if it is, FALSE if it isn't.
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:99
    public virtual object check_basic_mob_immunity(mob_living_basic basic_owner = null)
    {
        return Dm.bitand(basic_owner.basic_mob_flags, FLAMMABLE_MOB);
    }

    /**
     * Refresher for mob's fire_stacks
     */
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:106
    public virtual void cache_stacks()
    {
        owner.fire_stacks = 0;
        float was_on_fire = owner.on_fire;
        owner.on_fire = FALSE;
        foreach (datum_status_effect_fire_handler possible_fire in Dm.each<datum_status_effect_fire_handler>(owner.status_effects))
        {
            owner.fire_stacks += Dm.num(possible_fire.stacks) * possible_fire.stack_modifier;

            if (!(possible_fire is datum_status_effect_fire_handler_fire_stacks))
                continue;

            datum_status_effect_fire_handler_fire_stacks our_fire = (datum_status_effect_fire_handler_fire_stacks)possible_fire;
            if (Dm.truthy(our_fire.on_fire))
                owner.on_fire = TRUE;
        }

        if (Dm.truthy(was_on_fire) && !Dm.truthy(owner.on_fire))
            owner.clear_alert(ALERT_FIRE);
        else if (!Dm.truthy(was_on_fire) && Dm.truthy(owner.on_fire))
            owner.throw_alert(ALERT_FIRE, typeof(atom_movable_screen_alert_fire));
        owner.update_appearance(UPDATE_OVERLAYS);
        Waiting.NoSleep(update_particles());
    }
}

// tg: code/datums/status_effects/debuffs/fire_stacks.dm:127
[DmPath("/datum/status_effect/fire_handler/fire_stacks")]
public partial class datum_status_effect_fire_handler_fire_stacks : datum_status_effect_fire_handler
{
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:128-132  the var overrides of the type block (rules 10.2)
    public datum_status_effect_fire_handler_fire_stacks()
    {
        id = "fire_stacks"; //fire_stacks and wet_stacks should have different IDs or else has_status_effect won't work
        remove_on_fullheal = TRUE;
        stack_modifier = 1;
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:131  enemy_types = list(/datum/status_effect/fire_handler/wet_stacks)
    // (a list default: the pre-New hook, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        enemy_types = Dm.list(typeof(datum_status_effect_fire_handler_wet_stacks));
    }

    /// If we're on fire
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:135
    public float on_fire = FALSE;
    /// Cached particle type
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:141
    public object cached_state;
    // moblight and moblight_type (:137-139): generated (the vars the hand-written class does not declare)

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:143
    public override object get_examine_text(mob examiner = null)
    {
        if (Dm.truthy(owner.on_fire))
            return null;

        return I18n.T("[] [] covered in something flammable.", owner.p_They(), owner.p_are());
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:149
    [SignalHandler]
    public virtual void owner_touched_sparks()
    {
        // SIGNAL_HANDLER

        ignite();
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:154
    [DmParams("new_owner", "new_stacks", "forced")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = await base.on_creation(new_owner, __extra);
        RegisterSignal(owner, COMSIG_ATOM_TOUCHED_SPARKS, nameof(owner_touched_sparks));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:161  /datum/status_effect/fire_handler/fire_stacks/cache_stacks
    public override void cache_stacks()
    {
        base.cache_stacks();
        if (moblight == null)
            return;
        float stack_percent = Dm.num(stacks) / stack_limit;
        moblight.set_light_power(max(0.5f, round(Dm.initial(moblight_type, (obj_effect_dummy_lighting_obj p) => p.light_power) * stack_percent, 0.1f)));
        moblight.set_light_range(max(1.5f, round(Dm.initial(moblight_type, (obj_effect_dummy_lighting_obj p) => p.light_range) * stack_percent, 0.1f)));
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:169
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.num(stacks) <= 0)
        {
            qdel(this);
            return TRUE;
        }

        if (!Dm.truthy(on_fire))
            return TRUE;

        float decay_multiplier = HAS_TRAIT_NOT_FROM(owner, TRAIT_HUSK, Dm.initial(typeof(datum_status_effect_zombie), (datum_status_effect p) => p.id)) ? 2 : 1; // husks decay twice as fast
        adjust_stacks(owner.fire_stack_decay_rate * decay_multiplier * Dm.num(seconds_between_ticks));
        if (Dm.truthy(owner._listen_lookup?[COMSIG_FIRE_STACKS_UPDATED]))
            owner._SendSignal(COMSIG_FIRE_STACKS_UPDATED, Dm.list(owner, stacks));

        if (Dm.num(stacks) <= 0)
        {
            qdel(this);
            return TRUE;
        }

        datum_gas_mixture air = await owner.loc.return_air() as datum_gas_mixture;
        if (Dm.num(air.moles[typeof(datum_gas_oxygen)]) < 1)
        {
            qdel(this);
            return TRUE;
        }

        await deal_damage(seconds_between_ticks);
        return null;
    }

    /**
     * Proc that handles damage dealing and all special effects
     *
     * Arguments:
     * - seconds_between_ticks
     *
     */
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:200
    public virtual async DmTask deal_damage(object seconds_per_tick = null)
    {
        await owner.on_fire_stack(seconds_per_tick, this);

        turf location = get_turf(owner) as turf;
        location.hotspot_expose(700, 25 * Dm.num(seconds_per_tick), TRUE);
    }

    /**
     * Used to deal damage to humans and count their protection.
     *
     * Arguments:
     * - seconds_between_ticks
     * - no_protection: When set to TRUE, fire will ignore any possible fire protection
     *
     */
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:215
    public virtual async DmTask harm_human(object seconds_per_tick = null, float? no_protection = null)
    {
        no_protection ??= FALSE;
        mob_living_carbon_human victim = (mob_living_carbon_human)owner;
        float thermal_protection = Dm.num(victim.get_thermal_protection());

        if (!Dm.truthy(no_protection))
        {
            if (thermal_protection >= FIRE_IMMUNITY_MAX_TEMP_PROTECT)
                return;
            if (thermal_protection >= FIRE_SUIT_MAX_TEMP_PROTECT)
            {
                victim.adjust_bodytemperature(5.5f * Dm.num(seconds_per_tick));
                return;
            }
        }

        float amount_to_heat = (BODYTEMP_HEATING_MAX + (Dm.num(stacks) * 12)) * 0.5f * Dm.num(seconds_per_tick);
        if (owner.bodytemperature > BODYTEMP_FIRE_TEMP_SOFTCAP)
        {
            // Apply dimishing returns upon temp beyond the soft cap
            amount_to_heat = Dm.pow(amount_to_heat, (BODYTEMP_FIRE_TEMP_SOFTCAP / owner.bodytemperature));
        }

        victim.adjust_bodytemperature(amount_to_heat);
        if (!(HAS_TRAIT(victim, TRAIT_RESISTHEAT)))
        {
            await victim.add_mood_event("on_fire", typeof(datum_mood_event_on_fire));
            victim.add_mob_memory(typeof(datum_memory_was_burning));
        }
    }

    /**
     * Handles mob ignition, should be the only way to set on_fire to TRUE
     *
     * Arguments:
     * - silent: When set to TRUE, no message is displayed
     *
     */
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:244
    public virtual object ignite(float? silent = null)
    {
        silent ??= FALSE;
        if (HAS_TRAIT(owner, TRAIT_NOFIRE))
            return FALSE;

        on_fire = TRUE;
        if (!Dm.truthy(silent))
            owner.visible_message(span_warning(I18n.T("[] catches fire!", owner)), span_userdanger(I18n.T("You're set on fire!")));

        if (moblight_type != null)
        {
            if (Dm.truthy(moblight))
                qdel(moblight);
            moblight = Dm.@new(moblight_type, owner) as obj_effect_dummy_lighting_obj;
        }

        cache_stacks();
        if (Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_IGNITED]))
            owner._SendSignal(COMSIG_LIVING_IGNITED, Dm.list(owner, owner));
        return TRUE;
    }

    /**
     * Handles mob extinguishing, should be the only way to set on_fire to FALSE
     */
    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:265
    public virtual async DmTask extinguish()
    {
        QDEL_NULL(ref moblight);
        on_fire = FALSE;
        owner.clear_mood_event("on_fire");
        if (Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_EXTINGUISHED]))
            owner._SendSignal(COMSIG_LIVING_EXTINGUISHED, Dm.list(owner, owner));
        cache_stacks();
        foreach (obj_item equipped in Dm.each_cast<obj_item>(owner.get_equipped_items(INCLUDE_HELD | INCLUDE_PROSTHETICS | INCLUDE_ABSTRACT)))
            await equipped.extinguish();
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:274
    public override async DmTask<object> on_remove()
    {
        if (Dm.truthy(on_fire))
            await extinguish();
        set_stacks(0);
        UnregisterSignal(owner, COMSIG_ATOM_UPDATE_OVERLAYS);
        owner.update_appearance(UPDATE_OVERLAYS);
        return await on_remove__d0(); // the type defines on_remove twice: ..() is the earlier definition (rules 2.5)
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:158  (the earlier of two definitions, rules 2.5: it does not call ..())
    public async DmTask<object> on_remove__d0()
    {
        UnregisterSignal(owner, COMSIG_ATOM_TOUCHED_SPARKS);
        return null;
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:282
    [DmParams()]
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        RegisterSignal(owner, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(add_fire_overlay));
        RegisterSignal(owner, COMSIG_ATOM_EXTINGUISH, nameof(extinguish));
        owner.update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:288
    [SignalHandler]
    public virtual void add_fire_overlay(mob_living source = null, DmList overlays = null)
    {
        // SIGNAL_HANDLER

        if (Dm.num(stacks) <= 0 || !Dm.truthy(on_fire))
            return;

        mutable_appearance created_overlay = owner.get_fire_overlay(stacks, on_fire) as mutable_appearance;
        if (created_overlay == null)
            return;

        Dm.combine(overlays, created_overlay);
        // overlays |= source.make_fire_emissive(created_overlay): the emissive plane (the client's light does not use it, D24)
        Dm.Deferred("code/datums/status_effects/debuffs/fire_stacks.dm:299", "add_fire_overlay: make_fire_emissive (the emissive plane, D24)");
    }
}

// #define WET_STACKS_DAMP 3
// #define WET_STACKS_DRIPPING 7.5
// #define WET_STACKS_SOAKED 15
// #define WET_STACKS_MINIMUM_VFX WET_STACKS_DAMP
// tg: code/datums/status_effects/debuffs/fire_stacks.dm:301-304 (the defines) and :306
public partial class datum_status_effect_fire_handler_wet_stacks : datum_status_effect_fire_handler
{
    private const float WET_STACKS_DAMP = 3;
    private const float WET_STACKS_DRIPPING = 7.5f;
    private const float WET_STACKS_SOAKED = 15;
    private const float WET_STACKS_MINIMUM_VFX = WET_STACKS_DAMP;

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:314
    [DmParams()]
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        RegisterSignals(owner, Dm.list(SIGNAL_ADDTRAIT(TRAIT_WET_FOR_LONGER), SIGNAL_REMOVETRAIT(TRAIT_WET_FOR_LONGER)), nameof(update_wet_stack_modifier));
        update_wet_stack_modifier();
        RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_SLIPPERY_WHEN_WET), nameof(become_slippery));
        RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_SLIPPERY_WHEN_WET), nameof(no_longer_slippery));
        if (HAS_TRAIT(owner, TRAIT_SLIPPERY_WHEN_WET))
            become_slippery();
        ADD_TRAIT(owner, TRAIT_IS_WET, TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:324
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        REMOVE_TRAIT(owner, TRAIT_IS_WET, TRAIT_STATUS_EFFECT(id));
        if (HAS_TRAIT(owner, TRAIT_SLIPPERY_WHEN_WET))
            no_longer_slippery();
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:330
    [SignalHandler]
    public virtual void update_wet_stack_modifier()
    {
        // SIGNAL_HANDLER
        stack_modifier = HAS_TRAIT(owner, TRAIT_WET_FOR_LONGER) ? -3.5f : -1;
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:334
    [SignalHandler]
    public virtual void become_slippery()
    {
        // SIGNAL_HANDLER
        slipperiness = Waiting.NoSleep(owner.AddComponent(typeof(datum_component_slippery), 5 * SECONDS, Dm.named("lube_flags", SLIPPERY_WHEN_LYING_DOWN | NO_SLIP_WHEN_WALKING | WEAK_SLIDE))) as datum_component_slippery;
        ADD_TRAIT(owner, TRAIT_NO_SLIP_WATER, TRAIT_STATUS_EFFECT(id));
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:339
    [SignalHandler]
    public virtual void no_longer_slippery()
    {
        // SIGNAL_HANDLER
        QDEL_NULL(ref slipperiness);
        REMOVE_TRAIT(owner, TRAIT_NO_SLIP_WATER, TRAIT_STATUS_EFFECT(id));
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:344
    public override object get_examine_text(mob examiner = null)
    {
        if (Dm.num(stacks) <= WET_STACKS_DAMP)
            return I18n.T("[] seem[] damp.", owner.p_They(), owner.p_s());
        else if (Dm.num(stacks) >= WET_STACKS_SOAKED)
            return I18n.T("[] look[] completely soaked.", owner.p_They(), owner.p_s());
        else
            return I18n.T("[] appear[] to be dripping wet.", owner.p_They(), owner.p_s());
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:352
    public override void cache_stacks()
    {
        base.cache_stacks();
        // if(stacks > WET_STACKS_MINIMUM_VFX) owner.add_shared_particles(/particles/droplets); if(stacks <= WET_STACKS_MINIMUM_VFX)
        // owner.remove_shared_particles(/particles/droplets): the droplet particles (particles, a client visual)
        Dm.Deferred("code/datums/status_effects/debuffs/fire_stacks.dm:355", "cache_stacks: the wet stacks' droplet particles (particles, a client visual)");
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:359
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        float decay = HAS_TRAIT(owner, TRAIT_WET_FOR_LONGER) ? -0.035f : -0.5f;
        adjust_stacks(decay * Dm.num(seconds_between_ticks));
        if (Dm.num(stacks) <= 0)
        {
            qdel(this);
            return null;
        }

        if (HAS_TRAIT(owner, TRAIT_RESISTCOLD))
            return null;

        if (owner.bodytemperature <= WATER_VAPOR_DEPOSITION_POINT)
        {
            await owner.apply_status_effect(typeof(datum_status_effect_freon), Dm.num(stacks) * SECONDS);
            qdel(this);
        }
        return null;
    }

    // tg: code/datums/status_effects/debuffs/fire_stacks.dm:373
    public override object check_basic_mob_immunity(mob_living_basic basic_owner = null)
    {
        return !Dm.truthy(Dm.bitand(basic_owner.basic_mob_flags, IMMUNE_TO_GETTING_WET)) ? TRUE : FALSE;
    }
}
