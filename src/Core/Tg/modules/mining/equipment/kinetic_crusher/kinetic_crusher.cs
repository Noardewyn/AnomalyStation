// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Kinetic Crusher
 *
 * Lavaland's "Hard Mode" option for players, requiring melee attacks (backstabs even better),
 * but allowing you to upgrade it with trophies gained from fighting lavaland monsters, making it
 * a good tradeoff and a decent playstyle.
 */
// tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:8
public partial class obj_item_kinetic_crusher : obj_item
{
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_butchering),
            Dm.named("speed", 6 * SECONDS),
            Dm.named("effectiveness", 110)));
        update_reskin(null);
        //technically it's huge and bulky, but this provides an incentive to use it
        update_wielding();
        register_context();
        return dot;
    }

    /**
     * Adds or updates the reskinning component on the crusher.
     *
     * * default_skin_typepath: The typepath of skin to apply by default.
     * Passing null will either not apply a skin or will reset it to default if one is already applied.
     * If a supplied skin is blacklisted, it will be un-blacklisted.
     */
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:88
    public virtual void update_reskin(Type default_skin_typepath = null)
    {
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_reskinable_item),
            typeof(datum_atom_skin_crusher_skin),
            Dm.named("infinite", TRUE),
            Dm.named("initial_skin", default_skin_typepath != null ? Dm.initial(default_skin_typepath, (datum_atom_skin s) => s.preview_name) : null),
            Dm.named("blacklisted_subtypes", Dm.sub(subtypesof(typeof(datum_atom_skin_crusher_skin_locked)), default_skin_typepath))));
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:97
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_RMB] = I18n.T("Detach trophy");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Detach all trophies");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:107
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(trophies);
        return base.Destroy(force);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:111
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        Dm.remove(ref trophies, gone);
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:115
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Mark a large creature with a destabilizing force with right-click, then hit them in melee to do <b>[]</b> damage.", Dm.num(force_wielded) + detonation_damage)));
        Dm.append(ref dot, span_notice(I18n.T("Does <b>[]</b> damage if the target is backstabbed, instead of <b>[]</b>.", Dm.num(force_wielded) + detonation_damage + backstab_bonus, Dm.num(force_wielded) + detonation_damage)));
        foreach (obj_item_crusher_trophy crusher_trophy in Dm.each<obj_item_crusher_trophy>(trophies))
            Dm.append(ref dot, span_notice(I18n.T("It has \\a [] attached, which causes [].", crusher_trophy, crusher_trophy.effect_desc())));
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:122
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_crusher_trophy trophy)
        {
            await trophy.add_to(this, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:128
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        await base.crowbar_act(user, tool);
        if (!Dm.truthy(LAZYLEN(trophies)))
        {
            user.balloon_alert(user, I18n.T("no trophies!"));
            return ITEM_INTERACT_BLOCKING;
        }
        user.balloon_alert(user, I18n.T("trophies removed"));
        tool.play_tool_sound(this);
        foreach (obj_item_crusher_trophy crusher_trophy in Dm.each<obj_item_crusher_trophy>(trophies?.Copy()))
            await crusher_trophy.remove_from(this, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // adapted from kinetic accelerator attack_hand_secodary
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:140
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        if (!Dm.truthy(LAZYLEN(trophies)))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;

        DmList display_names = Dm.list();
        DmList items = Dm.list();
        for (int trophies_length = 1; trophies_length <= Dm.length(trophies); trophies_length++)
        {
            obj_item_crusher_trophy trophy = trophies[trophies_length] as obj_item_crusher_trophy;
            display_names[trophy.name] = REF(trophy);
            image item_image = image(icon: trophy.icon, icon_state: trophy.icon_state) as image;
            if (Dm.truthy(Dm.length(trophy.overlays)))
                item_image.copy_overlays(trophy);
            items[Dm.text("[]", trophy.name)] = item_image;
        }

        object pick = await show_radial_menu(user, this, items, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 36, require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(pick))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        object trophy_reference = display_names[pick];
        obj_item_crusher_trophy trophy_to_remove = Dm.locate_ref(trophy_reference) as obj_item_crusher_trophy;
        if (!Dm.@in(trophy_to_remove, trophies))
            trophy_to_remove = null;
        if (trophy_to_remove == null)
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        await trophy_to_remove.remove_from(this, user as mob_living);
        if (!Dm.truthy(await user.put_in_hands(trophy_to_remove)))
            await trophy_to_remove.forceMove(drop_location() as atom);

        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:172
    public virtual object check_menu(mob_living_carbon_human user = null)
    {
        if (user == null)
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:179
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return TRUE;
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
        {
            user.balloon_alert(user, I18n.T("must be wielded!"));
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:188
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        await target.apply_status_effect(typeof(datum_status_effect_crusher_damage));
        return await base.attack__proc(target, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:192
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!isliving(target))
            return;
        mob_living living_target = target as mob_living;
        // Melee effect
        foreach (obj_item_crusher_trophy crusher_trophy in Dm.each<obj_item_crusher_trophy>(trophies))
            await crusher_trophy.on_melee_hit(living_target, user as mob_living);
        if (QDELETED(target))
            return;
        datum_status_effect_crusher_mark mark = living_target.has_status_effect(typeof(datum_status_effect_crusher_mark)) as datum_status_effect_crusher_mark;
        if (mark != null)
            await mark.detonate(this, user as mob_living);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:204
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        obj_item_crusher_trophy new_trophy = interacting_with as obj_item_crusher_trophy;
        if (new_trophy == null)
            return NONE;
        if (Dm.truthy(await new_trophy.add_to(this, user)))
            return ITEM_INTERACT_SUCCESS;
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:212
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
        {
            balloon_alert(user, I18n.T("wield it first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (interacting_with == user)
        {
            balloon_alert(user, I18n.T("can't aim at yourself!"));
            return ITEM_INTERACT_BLOCKING;
        }
        await fire_kinetic_blast(interacting_with, user, modifiers);
        user.changeNext_move(CLICK_CD_MELEE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:223
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:226
    public virtual async DmTask fire_kinetic_blast(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(charged))
            return;
        turf proj_turf = user.loc as turf;
        if (proj_turf == null)
            return;
        last_projectile_pb = Dm.num(get_dist(target, user)) <= 1 && isliving(target) ? TRUE : FALSE;
        obj_projectile_destabilizer destabilizer = Dm.@new<obj_projectile_destabilizer>(proj_turf);
        if (Dm.truthy(_listen_lookup?[COMSIG_CRUSHER_FIRED_BLAST]))
            _SendSignal(COMSIG_CRUSHER_FIRED_BLAST, Dm.list(this, target, user, destabilizer));
        destabilizer.icon = projectile_icon;
        destabilizer.icon_state = projectile_icon_state;
        destabilizer.detonation_damage = detonation_damage;
        foreach (obj_item_crusher_trophy attached_trophy in Dm.each<obj_item_crusher_trophy>(trophies))
            attached_trophy.on_projectile_fire(destabilizer, user);
        await destabilizer.aim_projectile(target, user, modifiers);
        destabilizer.firer = user;
        destabilizer.fired_from = this;
        playsound(user, fire_kinetic_blast_sound, 100, TRUE);
        await destabilizer.fire();
        charged = FALSE;
        update_appearance();
        attempt_recharge_projectile();
    }

    /// Handles the timer for reloading the projectile
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:250
    public virtual void attempt_recharge_projectile(object set_recharge_time = null)
    {
        if (!Dm.truthy(set_recharge_time))
            set_recharge_time = charge_time;
        deltimer(charge_timer);
        charge_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(recharge_projectile)), Dm.num(set_recharge_time), TIMER_STOPPABLE, file: "code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm", line: 254);
    }

    /// Recharges the projectile
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:257
    public virtual void recharge_projectile()
    {
        if (!Dm.truthy(charged))
        {
            charged = TRUE;
            update_appearance();
            playsound(loc, projectile_recharge_sound, 60, TRUE);
        }
    }

    /// Updates the two handed component with new damage values
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:264
    public virtual void update_wielding()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("force_unwielded", 0), Dm.named("force_wielded", force_wielded)));
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:267
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        await set_light_on(!Dm.truthy(light_on) ? TRUE : FALSE);
        playsound(user, toggle_light_sound, 100, TRUE);
        update_appearance();
        return null;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:272
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        await set_light_on(FALSE);
        playsound(this, toggle_light_sound, 100, TRUE);
        return TRUE;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:278
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        inhand_icon_state = Dm.text("[][]", base_icon_state, HAS_TRAIT(this, TRAIT_WIELDED) ? 1 : 0); // this is not icon_state and not supported by 2hcomponent
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:282
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(charged))
            Dm.append(ref dot, Dm.text("[]_uncharged", icon_state));
        if (Dm.truthy(light_on))
            Dm.append(ref dot, Dm.text("[]_lit", icon_state));
        return dot;
    }
}

//destablizing force
// tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:294
public partial class obj_projectile_destabilizer : obj_projectile
{
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:308
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_parriable_projectile), Dm.named("parry_callback", Dm.@new<datum_callback>(this, nameof(on_parry)))));
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:312
    public override object Destroy(float? force = null)
    {
        fired_from = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:316
    public override object is_hostile_projectile()
    {
        return TRUE;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:319
    [SignalHandler]
    public virtual void on_parry(mob user = null)
    {
        boosted = TRUE;
        // Get a bit of a damage/range boost after being parried
        damage = 10;
        range = 9;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:326
    public override async DmTask<object> prehit_pierce(atom target = null)
    {
        if (!isliving(target) || firer == null || !Dm.truthy(ignore_allies))
            return await base.prehit_pierce(target);
        mob_living victim = target as mob_living;
        if (Dm.truthy((firer as mob)?.faction_check_atom(victim)))
            return PROJECTILE_PIERCE_PHASE;
        return await base.prehit_pierce(target);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher.dm:334
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        obj_item_kinetic_crusher used_crusher = fired_from as obj_item_kinetic_crusher;

        if (isliving(target))
        {
            mob_living living_target = target as mob_living;
            if (used_crusher != null)
            {
                foreach (obj_item_crusher_trophy crusher_trophy in Dm.each<obj_item_crusher_trophy>(used_crusher.trophies))
                    await crusher_trophy.on_projectile_hit_mob(living_target, firer as mob_living);
            }
            if (QDELETED(target))
                return await base.on_hit(target, blocked, pierce_hit);
            datum_status_effect_crusher_mark crusher_mark = await living_target.apply_status_effect(typeof(datum_status_effect_crusher_mark), boosted, used_crusher) as datum_status_effect_crusher_mark;
            if (crusher_mark == null)
                return await base.on_hit(target, blocked, pierce_hit);
            crusher_mark.detonation_damage = detonation_damage;
            if (used_crusher != null)
            {
                foreach (obj_item_crusher_trophy crusher_trophy in Dm.each<obj_item_crusher_trophy>(used_crusher.trophies))
                    crusher_trophy.on_mark_applied(living_target, firer as mob_living, crusher_mark);
            }
            return await base.on_hit(target, blocked, pierce_hit);
        }

        turf target_turf = get_turf(target);
        if (ismineralturf(target_turf))
        {
            turf_closed_mineral hit_mineral = target_turf as turf_closed_mineral;
            if (used_crusher != null)
            {
                foreach (obj_item_crusher_trophy crusher_trophy in Dm.each<obj_item_crusher_trophy>(used_crusher.trophies))
                    await crusher_trophy.on_projectile_hit_mineral(hit_mineral, firer as mob_living);
            }
            if (QDELETED(hit_mineral))
                return await base.on_hit(target, blocked, pierce_hit);
            Dm.@new<obj_effect_temp_visual_kinetic_blast>(hit_mineral);
            await hit_mineral.gets_drilled(firer as mob, 1);
            if (!iscarbon(firer))
                return await base.on_hit(target, blocked, pierce_hit);
            mob_living_carbon carbon_firer = firer as mob_living_carbon;
            object skill_modifier = 1;
            // If there is a mind, check for skill modifier to allow them to reload faster.
            if (carbon_firer.mind != null && used_crusher != null)
            {
                skill_modifier = carbon_firer.mind.get_skill_modifier(typeof(datum_skill_mining), SKILL_SPEED_MODIFIER);
                used_crusher.attempt_recharge_projectile(used_crusher.charge_time * Dm.num(skill_modifier)); //If you hit a mineral, you might get a quicker reload. epic gamer style.
            }
        }

        return await base.on_hit(target, blocked, pierce_hit);
    }
}
