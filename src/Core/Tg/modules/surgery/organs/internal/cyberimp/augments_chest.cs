// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/cyberimp/augments_chest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps package P6 (D298): the whole file: the nutriment pumps (the plus and the black market one), the reviver, the implantable
// thrusters (the jetpack component on the implant, with the locomotion bridge's preview, preview_thrust, after the translated procs)
// and the Herculean and Atlas spinal implants. The var blocks are generated (D111).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:7
public partial class obj_item_organ_cyberimp_chest_nutriment : obj_item_organ_cyberimp_chest
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:18
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        if (Dm.truthy(synthesizing))
            return;

        if (owner.nutrition <= hunger_threshold)
        {
            synthesizing = TRUE;
            string feed_text = I18n.T("You feel less hungry...");
            if (owner.nutrition >= NUTRITION_LEVEL_FED)
                feed_text = I18n.T("You feel very full...");
            to_chat(owner, span_notice(feed_text));
            await owner.adjust_nutrition(25 * Dm.num(seconds_per_tick));
            _addtimer(Dm.@new<datum_callback>(this, nameof(synth_cool)), 5 * SECONDS, file: "code/modules/surgery/organs/internal/cyberimp/augments_chest.dm", line: 31);
        }
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:33
    public virtual void synth_cool()
    {
        synthesizing = FALSE;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:36
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (owner == null || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await ((datum_reagents)owner.reagents).add_reagent(typeof(datum_reagent_toxin_bad_food), poison_amount / Dm.num(severity));
        to_chat(owner, span_warning(I18n.T("You feel like your insides are burning.")));
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:53
public partial class obj_item_organ_cyberimp_chest_nutriment_black_market : obj_item_organ_cyberimp_chest_nutriment
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:61
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (owner.nutrition >= NUTRITION_LEVEL_FAT && owner.overeatduration <= OVEREAT_TIME_LIMIT)
            owner.overeatduration = Dm.min(owner.overeatduration + 40 * SECONDS, OVEREAT_TIME_LIMIT);
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:66
public partial class obj_item_organ_cyberimp_chest_reviver : obj_item_organ_cyberimp_chest
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:79
    public override async DmTask on_death(object seconds_per_tick = null)
    {
        if (owner == null) // owner can be null, on_death() gets called by /obj/item/organ/process() for decay
            return;
        try_heal(); // Allows implant to work even on dead people
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:84
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        try_heal();
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:89
    public virtual void try_heal()
    {
        if (Dm.truthy(reviving))
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(owner))
            {
                reviver_cooldown = world.time + revive_cost; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
                reviving = FALSE;
                to_chat(owner, span_notice(I18n.T("Your reviver implant shuts down and starts recharging. It will be ready again in [].", DisplayTimeText(revive_cost))));
            }
            else
            {
                _addtimer(Dm.@new<datum_callback>(this, nameof(heal)), 3 * SECONDS, file: "code/modules/surgery/organs/internal/cyberimp/augments_chest.dm", line: 96);
            }
            return;
        }

        if (!(reviver_cooldown <= world.time) || HAS_TRAIT(owner, TRAIT_SUICIDED)) // COOLDOWN_FINISHED, inline
            return;

        if (IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            revive_cost = 0;
            reviving = TRUE;
            to_chat(owner, span_notice(I18n.T("You feel a faint buzzing as your reviver implant starts patching your wounds...")));
            defib_cooldown = world.time + 8 * SECONDS; // 5 seconds after heal proc delay (COOLDOWN_START, inline)
        }
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:109
    public virtual async DmTask heal()
    {
        if (owner == null)
            return; // a runtime in tg: the implant came out during the three seconds' timer
        if (defib_cooldown <= world.time) // COOLDOWN_FINISHED, inline
            await revive_dead();

        /// boolean that stands for if PHYSICAL damage being patched
        bool body_damage_patched = false;
        float need_mob_update = FALSE;
        if (Dm.truthy(owner.get_oxy_loss()))
        {
            need_mob_update += Dm.num(await owner.adjust_oxy_loss(-5, updating_health: FALSE));
            revive_cost += 5;
        }
        if (Dm.truthy(owner.get_brute_loss()))
        {
            need_mob_update += Dm.num(await owner.adjust_brute_loss(-2, updating_health: FALSE));
            revive_cost += 40;
            body_damage_patched = true;
        }
        if (Dm.truthy(owner.get_fire_loss()))
        {
            need_mob_update += Dm.num(await owner.adjust_fire_loss(-2, updating_health: FALSE));
            revive_cost += 40;
            body_damage_patched = true;
        }
        if (Dm.truthy(owner.get_tox_loss()))
        {
            need_mob_update += Dm.num(await owner.adjust_tox_loss(-1, updating_health: FALSE));
            revive_cost += 40;
        }
        if (Dm.truthy(need_mob_update))
            await owner.updatehealth();

        if (body_damage_patched && prob(35)) // healing is called every few seconds, not every tick
            owner.visible_message(span_warning(I18n.T("[]'s body twitches a bit.", owner)), span_notice(I18n.T("You feel like something is patching your injured body.")));
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:137
    public virtual async DmTask revive_dead()
    {
        if (!(defib_cooldown <= world.time) || owner.stat != DEAD || !Dm.eq(owner.can_defib(), DEFIB_POSSIBLE))
            return;
        owner.notify_revival(I18n.T("You are being revived by []!", this));
        revive_cost += 10 * MINUTES; // Additional 10 minutes cooldown after revival.
        owner.grab_ghost();

        defib_cooldown += 16 * SECONDS; // delay so it doesn't spam

        owner.visible_message(span_warning(I18n.T("[]'s body convulses a bit.", owner)));
        playsound(owner, SFX_BODYFALL, 50, TRUE);
        playsound(owner, Dm.resource("sound/machines/defib/defib_zap.ogg"), 75, TRUE, -1);
        owner.set_heartattack(FALSE);
        await owner.revive();
        await owner.emote("gasp");
        await owner.set_jitter_if_lower(200 * SECONDS);
        if (Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_MINOR_SHOCK]))
            owner._SendSignal(COMSIG_LIVING_MINOR_SHOCK, Dm.list(owner));
        log_game(Dm.text("[] been revived by []", owner, this));
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:157
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (owner == null || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        if (Dm.truthy(reviving))
            revive_cost += 200 / Dm.num(severity);
        else
            reviver_cooldown += 20 * SECONDS / Dm.num(severity);

        if (ishuman(owner))
        {
            mob_living_carbon_human human_owner = (mob_living_carbon_human)owner;
            if (human_owner.stat != DEAD && prob(50 / Dm.num(severity)) && Dm.truthy(human_owner.can_heartattack()))
            {
                human_owner.set_heartattack(TRUE);
                to_chat(human_owner, span_userdanger(I18n.T("You feel a horrible agony in your chest!")));
                _addtimer(Dm.@new<datum_callback>(this, nameof(undo_heart_attack)), 50 * SECONDS / Dm.num(severity), file: "code/modules/surgery/organs/internal/cyberimp/augments_chest.dm", line: 172);
            }
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:174
    public virtual void undo_heart_attack()
    {
        mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
        if (!istype(human_owner, typeof(mob_living_carbon_human)))
            return;
        human_owner.set_heartattack(FALSE);
        if (!IS_UNCONSCIOUS_OR_CRIT(human_owner))
            to_chat(human_owner, span_notice(I18n.T("You feel your heart beating again!")));
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:183
public partial class obj_item_organ_cyberimp_chest_thrusters : obj_item_organ_cyberimp_chest
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_jetpack),
            FALSE,
            1.5f * NEWTONS,
            COMSIG_THRUSTER_ACTIVATED,
            COMSIG_THRUSTER_DEACTIVATED,
            THRUSTER_ACTIVATION_FAILED,
            Dm.@new<datum_callback>(this, nameof(allow_thrust), 0.01f),
            Dm.@new<datum_callback>(this, nameof(allow_thrust), 0.01f),
            typeof(datum_effect_system_trail_follow_ion)));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:212
    public override async DmTask Remove(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        if (Dm.truthy(on))
            deactivate(silent: TRUE);
        await base.Remove(organ_owner, special, movement_flags);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:217
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        await toggle();
        return null;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:220
    public virtual async DmTask toggle(float? silent = null)
    {
        silent ??= FALSE;
        if (Dm.truthy(on))
            deactivate();
        else
            await activate();
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:226
    public virtual async DmTask activate(float? silent = null)
    {
        silent ??= FALSE;
        if (Dm.truthy(on))
            return;
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            if (!Dm.truthy(silent))
                to_chat(owner, span_warning(I18n.T("Your thrusters set seems to be broken!")));
            return;
        }
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_THRUSTER_ACTIVATED]) ? (object)NONE : _SendSignal(COMSIG_THRUSTER_ACTIVATED, Dm.list(this, owner)), THRUSTER_ACTIVATION_FAILED)))
            return;

        on = TRUE;
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_cybernetic));
        if (!Dm.truthy(silent))
            to_chat(owner, span_notice(I18n.T("You turn your thrusters set on.")));
        update_appearance();
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:242
    public virtual void deactivate(float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(on))
            return;
        if (Dm.truthy(_listen_lookup?[COMSIG_THRUSTER_DEACTIVATED]))
            _SendSignal(COMSIG_THRUSTER_DEACTIVATED, Dm.list(this, owner));
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_cybernetic));
        if (!Dm.truthy(silent))
            to_chat(owner, span_notice(I18n.T("You turn your thrusters set off.")));
        on = FALSE;
        update_appearance();
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:252
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(on) ? "-on" : null);
        await update_overlay_state();
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:257
    public virtual async DmTask<object> allow_thrust(object num = null, float? use_fuel = null)
    {
        use_fuel ??= TRUE;
        if (owner == null)
            return FALSE;

        turf owner_turf = get_turf(owner);
        if (owner_turf == null) // No more runtimes from being stuck in nullspace.
            return FALSE;

        // Priority 1: use air from environment.
        datum_gas_mixture environment = await owner_turf.return_air() as datum_gas_mixture;
        if (environment != null && Dm.num(environment.return_pressure()) > 30)
            return TRUE;

        // Priority 2: use plasma from internal plasma storage.
        // (just in case someone would ever use this implant system to make cyber-alien ops with jetpacks and taser arms)
        if (Dm.num(owner.getPlasma()) >= Dm.num(num) * 100)
        {
            if (Dm.truthy(use_fuel))
                await owner.adjustPlasma(-Dm.num(num) * 100);
            return TRUE;
        }

        // Priority 3: use internals tank.
        datum_gas_mixture internal_mix = owner.@internal != null ? await owner.@internal.return_air() as datum_gas_mixture : null;
        if (internal_mix != null && Dm.num(internal_mix.total_moles()) > Dm.num(num))
        {
            if (!Dm.truthy(use_fuel))
                return TRUE;
            datum_gas_mixture removed = internal_mix.remove(num) as datum_gas_mixture;
            if (Dm.num(removed.total_moles()) > 0.005f)
            {
                await owner_turf.assume_air(removed);
                return TRUE;
            }
            else
            {
                await owner_turf.assume_air(removed);
            }
        }

        deactivate(silent: TRUE);
        return FALSE;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:292
    public override object get_overlay_state()
    {
        return Dm.text("[][]", aug_overlay, Dm.truthy(on) ? "_on" : "");
    }

    /**
     * The port's own (the locomotion bridge's preview, through datum_component_jetpack.preview_stabilize): allow_thrust's
     * answer with use_fuel FALSE without its side effect (tg turns the thrusters off there when nothing is left to push with;
     * the preview leaves that to the real call). It reads the same three sources in tg's order.
     */
    internal bool preview_thrust(object num)
    {
        if (owner == null)
            return false;
        turf owner_turf = get_turf(owner);
        if (owner_turf == null)
            return false;
        if (Waiting.NoSleep(owner_turf.return_air()) is datum_gas_mixture environment && Dm.num(environment.return_pressure()) > 30)
            return true;
        if (Dm.num(owner.getPlasma()) >= Dm.num(num) * 100)
            return true;
        datum_gas_mixture internal_mix = owner.@internal != null ? Waiting.NoSleep(owner.@internal.return_air()) as datum_gas_mixture : null;
        return internal_mix != null && Dm.num(internal_mix.total_moles()) > Dm.num(num);
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:295
public partial class obj_item_organ_cyberimp_chest_spine : obj_item_organ_cyberimp_chest
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:315
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (owner == null || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        to_chat(owner, span_warning(I18n.T("You feel shearing pain as your body is crushed like a soda can!")));
        await owner.apply_damage(20 / Dm.num(severity), BRUTE, def_zone: BODY_ZONE_CHEST);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:322
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        stone_overlay = mutable_appearance(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "stone") as mutable_appearance;
        organ_owner.add_overlay(stone_overlay);
        add_organ_trait(TRAIT_BOULDER_BREAKER);
        if (Dm.truthy(core_applied))
        {
            await organ_owner.AddElement(typeof(datum_element_forced_gravity), 1);
            add_organ_trait(TRAIT_STURDY_FRAME);
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:331
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon loser = (mob_living_carbon)organ_owner;
        remove_organ_trait(TRAIT_BOULDER_BREAKER);
        if (stone_overlay != null)
        {
            loser.cut_overlay(stone_overlay);
            stone_overlay = null;
        }
        if (Dm.truthy(core_applied))
        {
            loser.RemoveElement(typeof(datum_element_forced_gravity), 1);
            remove_organ_trait(TRAIT_STURDY_FRAME);
        }
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_chest.dm:341
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_assembly_signaler_anomaly_grav)))
            return NONE;

        if (Dm.truthy(core_applied))
        {
            user.balloon_alert(user, I18n.T("core already installed!"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.balloon_alert(user, I18n.T("core installed"));
        name = Dm.initial(typeof(obj_item_organ_cyberimp_chest_spine_atlas), (obj_item_organ_cyberimp_chest_spine_atlas p) => p.name);
        desc = Dm.initial(typeof(obj_item_organ_cyberimp_chest_spine_atlas), (obj_item_organ_cyberimp_chest_spine_atlas p) => p.desc);
        athletics_boost_multiplier = Dm.initial(typeof(obj_item_organ_cyberimp_chest_spine_atlas), (obj_item_organ_cyberimp_chest_spine_atlas p) => p.athletics_boost_multiplier);
        added_throw_range = Dm.initial(typeof(obj_item_organ_cyberimp_chest_spine_atlas), (obj_item_organ_cyberimp_chest_spine_atlas p) => p.added_throw_range);
        added_throw_speed = Dm.initial(typeof(obj_item_organ_cyberimp_chest_spine_atlas), (obj_item_organ_cyberimp_chest_spine_atlas p) => p.added_throw_speed);
        strength_bonus = Dm.initial(typeof(obj_item_organ_cyberimp_chest_spine_atlas), (obj_item_organ_cyberimp_chest_spine_atlas p) => p.strength_bonus);
        core_applied = TRUE;
        icon_state = "herculean_implant_core";
        update_appearance();
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }
}
