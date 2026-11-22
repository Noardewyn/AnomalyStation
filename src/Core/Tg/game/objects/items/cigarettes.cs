// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/cigarettes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J3, D298): matches, cigarettes, cigars, pipes, butts and rolling paper; the e-cigarette since gaps OD1 (D333). Left: the smoke
// particles (no particle holder drawn), the choking on a cigarette (the choke status effect), the cigarette eater's achievement. The var
// blocks are generated.

///////////
//MATCHES//
///////////
// tg: code/game/objects/items/cigarettes.dm:17
public partial class obj_item_match : obj_item
{
    // tg: code/game/objects/items/cigarettes.dm:34
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_phosphorus), 2));
    }

    // tg: code/game/objects/items/cigarettes.dm:37
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        smoketime -= Dm.num(seconds_per_tick) * (1 * SECONDS);
        if (smoketime <= 0)
            matchburnout();
        else
            open_flame(heat);
        return null;
    }

    // tg: code/game/objects/items/cigarettes.dm:44
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        object dot = await base.fire_act(exposed_temperature, exposed_volume);
        matchignite();
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:48
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (Dm.truthy(lit))
            name = Dm.text("lit []", Dm.initial(this, (obj_item_match p) => p.name));
        else if (Dm.truthy(burnt))
            name = Dm.text("burnt []", Dm.initial(this, (obj_item_match p) => p.name));
        else if (Dm.truthy(broken))
            name = Dm.text("broken []", Dm.initial(this, (obj_item_match p) => p.name));
        else
            name = Dm.text("[]", Dm.initial(this, (obj_item_match p) => p.name));
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:59
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        if (Dm.truthy(lit))
            desc = Dm.text("[] This one is lit.", Dm.initial(this, (obj_item_match p) => p.desc));
        else if (Dm.truthy(burnt))
            desc = Dm.text("[] This one has seen better days.", Dm.initial(this, (obj_item_match p) => p.desc));
        else if (Dm.truthy(broken))
            desc = Dm.text("[] This one is broken.", Dm.initial(this, (obj_item_match p) => p.desc));
        else
            desc = Dm.initial(this, (obj_item_match p) => p.desc);
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:70
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        inhand_icon_state = "cigoff";
        if (Dm.truthy(lit))
        {
            icon_state = Dm.text("[]_lit", base_icon_state);
            inhand_icon_state = "cigon";
        }
        else if (Dm.truthy(burnt))
            icon_state = Dm.text("[]_burnt", base_icon_state);
        else if (Dm.truthy(broken))
            icon_state = Dm.text("[]_broken", base_icon_state);
        else
            icon_state = Dm.text("[]_unlit", base_icon_state);
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:83
    public virtual void snap()
    {
        if (Dm.truthy(broken))
            return;
        if (Dm.truthy(lit))
            matchburnout();

        playsound(this, Dm.resource("sound/effects/snap.ogg"), 15, TRUE);
        broken = TRUE;
        attack_verb_continuous = string_list(Dm.list("flicks")) as DmList;
        attack_verb_simple = string_list(Dm.list("flick")) as DmList;
        STOP_PROCESSING(SSobj, this);
        update_appearance();
    }

    // tg: code/game/objects/items/cigarettes.dm:96
    public virtual void matchignite()
    {
        if (Dm.truthy(lit) || Dm.truthy(burnt) || Dm.truthy(broken))
            return;

        playsound(this, Dm.resource("sound/items/match_strike.ogg"), 15, TRUE);
        lit = TRUE;
        damtype = BURN;
        force = 3;
        hitsound = Dm.resource("sound/items/tools/welder.ogg");
        attack_verb_continuous = string_list(Dm.list("burns", "singes")) as DmList;
        attack_verb_simple = string_list(Dm.list("burn", "singe")) as DmList;
        if (isliving(loc))
        {
            mob_living male_model = (mob_living)loc;
            if (Dm.truthy(male_model.fire_stacks) && !Dm.truthy(male_model.on_fire))
                male_model.ignite_mob();
        }
        START_PROCESSING(SSobj, this);
        update_appearance();
    }

    // tg: code/game/objects/items/cigarettes.dm:114
    public virtual void matchburnout()
    {
        if (!Dm.truthy(lit))
            return;

        lit = FALSE;
        burnt = TRUE;
        damtype = BRUTE;
        force = Dm.initial(this, (obj_item_match p) => p.force);
        attack_verb_continuous = string_list(Dm.list("flicks")) as DmList;
        attack_verb_simple = string_list(Dm.list("flick")) as DmList;
        STOP_PROCESSING(SSobj, this);
        update_appearance();
    }

    // tg: code/game/objects/items/cigarettes.dm:127
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        matchburnout();
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:131
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        matchburnout();
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/game/objects/items/cigarettes.dm:135
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!isliving(target_mob))
            return null;

        if (Dm.truthy(lit) && Dm.truthy(target_mob.ignite_mob()))
        {
            message_admins(Dm.text("[] set [] on fire with [] at []", ADMIN_LOOKUPFLW(user), key_name_admin(target_mob), this, AREACOORD(user)));
            user.log_message(Dm.text("set [] on fire with []", key_name(target_mob), this), LOG_ATTACK);
        }

        obj_item_cigarette cig = help_light_cig(target_mob) as obj_item_cigarette;
        if (!Dm.truthy(lit) || cig == null || Dm.truthy(user.combat_mode))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        if (Dm.truthy(cig.lit))
        {
            to_chat(user, span_warning(I18n.T("[] is already lit!", cig)));
            return null;
        }

        await cig.attempt_light(user, this, target_mob == user ? null : span_notice(I18n.T("[] holds [] out for [], and lights [].", user, this, target_mob, cig)));
        return null;
    }

    // tg: code/game/objects/items/cigarettes.dm:159
    public override object get_temperature()
    {
        return lit * heat;
    }
}

public partial class obj_item : obj
{
    /// Finds a cigarette on another mob to help light.
    // tg: code/game/objects/items/cigarettes.dm:154
    public virtual object help_light_cig(mob_living M = null)
    {
        object mask_item = M.get_item_by_slot(ITEM_SLOT_MASK);
        if (istype(mask_item, typeof(obj_item_cigarette)))
            return mask_item;
        return null;
    }
}

// tg: code/game/objects/items/cigarettes.dm:163
public partial class obj_item_match_firebrand : obj_item_match
{
    // tg: code/game/objects/items/cigarettes.dm:168
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_carbon), 2));
    }

    // tg: code/game/objects/items/cigarettes.dm:171
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        matchignite();
        return dot;
    }
}

// tg: code/game/objects/items/cigarettes.dm:176
public partial class obj_item_match_battery : obj_item_match
{
    // tg: code/game/objects/items/cigarettes.dm:182
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, this)))
            return dot;
        await ((mob_living)user).apply_damage(5, BURN, user.get_active_hand());
        matchignite();
        return dot;
    }
}

//////////////////
//FINE SMOKABLES//
//////////////////

// tg: code/game/objects/items/cigarettes.dm:194
public partial class obj_item_cigarette : obj_item
{
    // tg: code/game/objects/items/cigarettes.dm:251
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_knockoff), 90, Dm.list(BODY_ZONE_PRECISE_MOUTH), slot_flags)); //90% to knock off when wearing a mask
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        RegisterSignal(this, COMSIG_ATOM_TOUCHED_SPARKS, nameof(sparks_touched));
        icon_state = icon_off;
        inhand_icon_state = inhand_icon_off;

        // "It is called a cigarette"
        Waiting.NoSleep(AddComponentFrom(
            SOURCE_EDIBLE_INNATE,
            typeof(datum_component_edible),
            Dm.named("initial_reagents", list_reagents),
            Dm.named("food_flags", FOOD_NO_EXAMINE),
            Dm.named("foodtypes", JUNKFOOD),
            Dm.named("volume", chem_volume),
            Dm.named("eat_time", 0 * SECONDS),
            Dm.named("tastes", Dm.list("a never before experienced flavour", "finally sitting down after standing your entire life")),
            Dm.named("eatverbs", Dm.list("taste")),
            Dm.named("bite_consumption", chem_volume),
            Dm.named("junkiness", 0),
            Dm.named("reagent_purity", null),
            Dm.named("on_consume", Dm.@new<datum_callback>(this, nameof(on_consume)))));
        if (Dm.truthy(starts_lit))
            Waiting.NoSleep(light__proc());
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:278
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        QDEL_NULL(ref mob_smoke);
        QDEL_NULL(ref cig_smoke);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/cigarettes.dm:284
    public virtual void on_consume(mob_living eater = null, mob_living feeder = null)
    {
        Dm.Deferred("code/game/objects/items/cigarettes.dm:291", "on_consume: cigarette_eaters and the achievement of 500 eaten cigarettes (awards not ported)");
    }

    // tg: code/game/objects/items/cigarettes.dm:293
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_MASK)))
        {
            UnregisterSignal(user, Dm.list(COMSIG_HUMAN_FORCESAY, COMSIG_ATOM_DIR_CHANGE));
            return;
        }
        RegisterSignal(user, COMSIG_HUMAN_FORCESAY, nameof(on_forcesay));
        RegisterSignal(user, COMSIG_ATOM_DIR_CHANGE, nameof(on_mob_dir_change));
        if (Dm.truthy(lit) && iscarbon(loc))
            make_mob_smoke((mob_living)loc);
    }

    // tg: code/game/objects/items/cigarettes.dm:303
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        // Moving the cigarette from mask to hands (or pocket I guess) will emit a larger puff of smoke
        if (!QDELETED(this) && !QDELETED(user) && how_long_have_we_been_smokin >= 4 * SECONDS && user == loc && iscarbon(user))
        {
            // This relies on the fact that dropped is called before slot is nulled
            if (user.get_item_by_slot(ITEM_SLOT_MASK) == this && !Dm.truthy(user.incapacitated))
                await long_exhale((mob_living_carbon)user);
        }

        UnregisterSignal(user, Dm.list(COMSIG_HUMAN_FORCESAY, COMSIG_ATOM_DIR_CHANGE));
        QDEL_NULL(ref mob_smoke);
        how_long_have_we_been_smokin = 0 * SECONDS;
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:315
    [SignalHandler]
    public virtual void on_forcesay(mob_living source = null, object major = null)
    {
        if (!Dm.truthy(major))
            return;
        Dm.Deferred("code/game/objects/items/cigarettes.dm:320", "on_forcesay: apply_status_effect(/datum/status_effect/choke, ...): choking on a cigarette when hit (the choke status effect, datums/status_effects/debuffs/choke.dm, not ported)");
    }

    // tg: code/game/objects/items/cigarettes.dm:322
    [SignalHandler]
    public virtual void on_mob_dir_change(mob_living source = null, object old_dir = null, object new_dir = null)
    {
        if (mob_smoke == null)
            return;
        update_particle_position(mob_smoke, new_dir);
    }

    // tg: code/game/objects/items/cigarettes.dm:328
    public virtual void update_particle_position(obj_effect_abstract_particle_holder to_edit = null, object new_dir = null)
    {
        // the holder is never made (make_cig_smoke, make_mob_smoke), so nothing reaches this
        Dm.Deferred("code/game/objects/items/cigarettes.dm:340", "update_particle_position: to_edit.set_particle_position (the smoke particles: the particle holder is not ported)");
    }

    // tg: code/game/objects/items/cigarettes.dm:343
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is huffing [] as quickly as [] can! It looks like [] trying to give []self cancer.", user, this, user.p_they(), user.p_theyre(), user.p_them())));
        return Dm.bitor(TOXLOSS, OXYLOSS);
    }

    // tg: code/game/objects/items/cigarettes.dm:347
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        return await attempt_light(user, tool);
    }

    // tg: code/game/objects/items/cigarettes.dm:350
    public virtual async DmTask<object> attempt_light(mob_living user = null, obj_item tool = null, object text_override = null)
    {
        if (Dm.truthy(lit))
            return NONE;

        if (text_override == null)
        {
            text_override = await tool.ignition_effect(this, user);
            if (!Dm.truthy(text_override))
                return NONE;
        }
        // Maybe jank, but the reason it's like this is that you can ignore ignition_effect() and also provide no text by giving an empty string to text_override, while still lighting.

        if (!Dm.truthy(await check_oxygen(user))) //cigarettes need oxygen
        {
            balloon_alert(user, I18n.T("no air!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(smoketime))
        {
            to_chat(user, span_warning(I18n.T("There is nothing to smoke!")));
            return ITEM_INTERACT_BLOCKING;
        }

        await light__proc(text_override);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Checks that we have enough air to smoke
    // tg: code/game/objects/items/cigarettes.dm:372
    public virtual async DmTask<object> check_oxygen(mob user = null)
    {
        if (Dm.truthy(((datum_reagents)reagents).has_reagent(typeof(datum_reagent_oxygen))))
            return TRUE;
        datum_gas_mixture air = await return_air() as datum_gas_mixture;
        if (air != null && Dm.truthy(air.has_gas(typeof(datum_gas_oxygen), 1)))
            return TRUE;
        if (!ishuman(user))
            return FALSE;
        mob_living_carbon_human the_smoker = (mob_living_carbon_human)user;
        return the_smoker.can_breathe_helmet();
    }

    // tg: code/game/objects/items/cigarettes.dm:383
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(lit)) //can't dip if cigarette is lit (it will heat the reagents in the glass instead)
            return NONE;
        obj_item_reagent_containers_cup glass = interacting_with as obj_item_reagent_containers_cup;
        if (glass == null) //you can dip cigarettes into beakers
            return NONE;
        if (istype(glass, typeof(obj_item_reagent_containers_cup_mortar)))
            return NONE;
        datum_reagents glass_reagents = (datum_reagents)glass.reagents;
        if (Dm.truthy(await glass_reagents.trans_to(this, chem_volume, transferred_by: user))) //if reagents were transferred, show the message
            to_chat(user, span_notice(I18n.T("You dip \\the [] into \\the [].", this, glass)));
        //if not, either the beaker was empty, or the cigarette was full
        else if (!Dm.truthy(glass_reagents.total_volume))
            to_chat(user, span_warning(I18n.T("[] is empty!", glass)));
        else
            to_chat(user, span_warning(I18n.T("[] is full!", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/cigarettes.dm:400
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(lit))
        {
            icon_state = icon_on;
            inhand_icon_state = inhand_icon_on;
        }
        else
        {
            icon_state = icon_off;
            inhand_icon_state = inhand_icon_off;
        }
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:410
    [SignalHandler]
    public virtual void sparks_touched(datum source = null, obj_effect particle_effect = null)
    {
        if (Dm.truthy(lit))
            return;
        Waiting.NoSleep(light__proc());
    }

    /// Lights the cigarette with given flavor text.
    // tg: code/game/objects/items/cigarettes.dm:418
    public virtual async DmTask light__proc(object flavor_text = null)
    {
        if (Dm.truthy(lit))
            return;

        lit = TRUE;
        playsound(this.loc, Dm.resource("sound/items/lighter/cig_light.ogg"), 100, 1);
        make_cig_smoke();
        await set_light_on(TRUE);
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            update_appearance(UPDATE_ICON);
            return;
        }

        attack_verb_continuous = string_list(Dm.list("burns", "singes")) as DmList;
        attack_verb_simple = string_list(Dm.list("burn", "singe")) as DmList;
        hitsound = Dm.resource("sound/items/tools/welder.ogg");
        damtype = BURN;
        force = 4;

        datum_reagents holder = (datum_reagents)reagents;
        if (holder != null && Dm.truthy(Dm.bitand(await holder.spark_act(0, NONE, banned_reagents: typeof(datum_reagent_flash_powder)), SPARK_ACT_DESTRUCTIVE)))
        {
            usr?.log_message("lit a rigged cigarette", LOG_VICTIM);
            qdel(this);
            return;
        }

        // Custom handling for the hallucination effect
        if (holder != null && Dm.truthy(holder.has_reagent(typeof(datum_reagent_flash_powder))))
        {
            if (!isliving(loc))
            {
                loc.visible_message(span_hear(I18n.T("\\The [] burns up!", this)));
                qdel(this);
                return;
            }
            mob_living user = (mob_living)loc;
            loc.visible_message(span_hear(I18n.T("[]'s [] burns up as [] fall to the ground!", user, name, p_they(user))), span_danger(I18n.T("The solution violently explodes!")));
            await user.flash_act(INFINITY, visual: TRUE, length: 5 * SECONDS);
            user.playsound_local(get_turf(user), SFX_EXPLOSION, 50, TRUE);
            await user.cause_hallucination(typeof(datum_hallucination_death), Dm.text("trick trick []", name));
            qdel(this);
            return;
        }

        if (holder != null && Dm.truthy(holder.has_reagent(typeof(datum_reagent_drug_methamphetamine))))
            holder.flags = Dm.bitor(holder.flags, NO_REACT);

        // allowing reagents to react after being lit
        await holder.handle_reactions();
        if (QDELETED(this))
            return;
        START_PROCESSING(SSobj, this);

        update_appearance(UPDATE_ICON);
        if (Dm.truthy(flavor_text))
        {
            turf T = get_turf(this);
            T.visible_message(flavor_text);
        }

        if (iscarbon(loc))
        {
            mob_living_carbon smoker = (mob_living_carbon)loc;
            if (smoker.get_item_by_slot(ITEM_SLOT_MASK) == this)
                make_mob_smoke(smoker);
        }
    }

    // tg: code/game/objects/items/cigarettes.dm:474
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        if (!Dm.truthy(lit))
            return dot;
        attack_verb_continuous = null;
        attack_verb_simple = null;
        hitsound = null;
        damtype = BRUTE;
        force = 0;
        STOP_PROCESSING(SSobj, this);
        datum_reagents holder = (datum_reagents)reagents;
        holder.flags = Dm.bitor(holder.flags, NO_REACT);
        lit = FALSE;
        playsound(this.loc, Dm.resource("sound/items/lighter/cig_snuff.ogg"), 100, 1);
        update_appearance(UPDATE_ICON);
        await set_light_on(FALSE);
        if (ismob(loc))
            to_chat(loc, span_notice(I18n.T("Your [] goes out.", name)));
        QDEL_NULL(ref cig_smoke);
        QDEL_NULL(ref mob_smoke);
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:494
    public virtual async DmTask long_exhale(mob_living_carbon smoker = null)
    {
        // Find a mob to blow smoke at
        mob_living guy_infront = null;
        foreach (mob_living guy in Dm.each<mob_living>(get_step(smoker, smoker.dir)))
        {
            // one of you has to get on the other's level
            if (guy.body_position != smoker.body_position)
                continue;
            // ensures we're face to face
            if (!Dm.truthy(Dm.bitand(REVERSE_DIR(guy.dir), smoker.dir)))
                continue;
            guy_infront = guy;
            // in case we get a living first, we wanna prioritize humans
            if (ishuman(guy_infront))
                break;
        }

        if (guy_infront == null)
        {
            smoker.visible_message(
                span_notice(I18n.T("[] exhales a large cloud of smoke from [].", smoker, this)),
                span_notice(I18n.T("You exhale a large cloud of smoke from [].", this)));
        }
        else if (ishuman(guy_infront) && Dm.truthy(((mob_living_carbon)guy_infront).get_bodypart(BODY_ZONE_HEAD)) && !Dm.truthy(((mob_living_carbon)guy_infront).is_pepper_proof()))
        {
            smoker.visible_message(
                span_notice(I18n.T("[] exhales a large cloud of smoke from [] directly at []'s face!", smoker, this, guy_infront)),
                span_notice(I18n.T("You exhale a large cloud of smoke from [] directly at []'s face.", this, guy_infront)),
                ignored_mobs: guy_infront);
            to_chat(guy_infront, span_warning(I18n.T("You get a face full of smoke from []'s []!", smoker, name)));
            await smoke_in_face(guy_infront);
        }
        else
        {
            smoker.visible_message(
                span_notice(I18n.T("[] exhales a large cloud of smoke from [] at [].", smoker, this, guy_infront)),
                span_notice(I18n.T("You exhale a large cloud of smoke from [] at [].", this, guy_infront)));
        }

        if (!isturf(smoker.loc))
            return;

        // new /obj/effect/abstract/particle_holder(smoker.loc, /particles/smoke/cig/big), update_particle_position, QDEL_IN: the big puff
        Dm.Deferred("code/game/objects/items/cigarettes.dm:533", "long_exhale: the big puff's particle holder (the smoke particles: the particle holder is not ported)");
    }

    /// Called when a mob gets smoke blown in their face.
    // tg: code/game/objects/items/cigarettes.dm:538
    public virtual async DmTask smoke_in_face(mob_living getting_smoked = null)
    {
        await getting_smoked.add_mood_event("smoke_bm", typeof(datum_mood_event_smoke_in_face));
        if (prob(20) && !HAS_TRAIT(getting_smoked, TRAIT_SMOKER) && !HAS_TRAIT(getting_smoked, TRAIT_ANOSMIA))
            await getting_smoked.emote("cough");
    }

    /// Handles processing the reagents in the cigarette.
    // tg: code/game/objects/items/cigarettes.dm:544
    public virtual async DmTask handle_reagents(object seconds_per_tick = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume))
            return;
        await holder.expose_temperature(heat, 0.05f);
        if (!Dm.truthy(holder.total_volume)) //may have reacted and gone to 0 after expose_temperature
            return;

        float to_smoke = Dm.truthy(smoke_all) ? (holder.total_volume * (dragtime / smoketime)) : REAGENTS_METABOLISM;
        mob_living_carbon smoker = loc as mob_living_carbon;
        // These checks are a bit messy but at least they're fairly readable
        // Check if the smoker is a carbon mob, since it needs to have wear_mask
        if (smoker == null)
        {
            // If not, check if it's a gas mask
            if (!istype(loc, typeof(obj_item_clothing_mask_gas)))
            {
                await holder.remove_all(to_smoke);
                return;
            }

            smoker = loc.loc as mob_living_carbon;

            // If it is, check if that mask is on a carbon mob
            if (smoker == null || smoker.get_item_by_slot(ITEM_SLOT_MASK) != loc)
            {
                await holder.remove_all(to_smoke);
                return;
            }
        }
        else
        {
            if (smoker.get_item_by_slot(ITEM_SLOT_MASK) != this)
            {
                await holder.remove_all(to_smoke);
                return;
            }
        }

        how_long_have_we_been_smokin += Dm.num(seconds_per_tick) * (1 * SECONDS);
        await holder.expose(smoker, INHALE, min(to_smoke / holder.total_volume, 1));
        obj_item_organ_lungs lungs = smoker.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        if (lungs != null && Dm.truthy(IS_ORGANIC_ORGAN(lungs)))
        {
            float smoker_resistance = HAS_TRAIT(smoker, TRAIT_SMOKER) ? 0.5f : 1;
            await smoker.adjust_organ_loss(ORGAN_SLOT_LUNGS, lung_harm * smoker_resistance);
        }
        if (!Dm.truthy(await holder.trans_to(smoker, to_smoke, methods: INHALE, ignore_stomach: TRUE)))
            await holder.remove_all(to_smoke);
    }

    // tg: code/game/objects/items/cigarettes.dm:581
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        mob_living user = loc as mob_living;
        user?.ignite_mob();

        if (!Dm.truthy(Waiting.NoSleep(check_oxygen(user))))
        {
            Waiting.NoSleep(extinguish());
            return null;
        }

        smoketime -= Dm.num(seconds_per_tick) * (1 * SECONDS);
        if (smoketime <= 0)
        {
            Waiting.NoSleep(put_out(user));
            return null;
        }

        open_flame(heat);
        if (Dm.truthy(((datum_reagents)reagents)?.total_volume) && drag_cooldown <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
        {
            drag_cooldown = world.time + dragtime; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
            Waiting.NoSleep(handle_reagents(seconds_per_tick));
        }
        return null;
    }

    // tg: code/game/objects/items/cigarettes.dm:599
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(lit))
            await put_out(user, TRUE);
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/cigarettes.dm:604
    public virtual async DmTask put_out(mob user = null, float? done_early = null)
    {
        done_early ??= FALSE;
        atom location = drop_location() as atom;
        if (user != null)
        {
            if (Dm.truthy(done_early))
            {
                if (isfloorturf(location) && Dm.truthy(location.has_gravity()))
                {
                    user.visible_message(span_notice(I18n.T("[] calmly drops and treads on [], putting it out instantly.", user, this)));
                    Dm.@new<obj_effect_decal_cleanable_ash>(location);
                    await long_exhale(user as mob_living_carbon);
                }
                else
                    user.visible_message(span_notice(I18n.T("[] pinches out [].", user, this)));
                how_long_have_we_been_smokin = 0 * SECONDS;
            }
            else
                to_chat(user, span_notice(I18n.T("Your [] goes out.", name)));
        }
        Dm.@new(type_butt, location);
        qdel(this);
    }

    // tg: code/game/objects/items/cigarettes.dm:620
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (target_mob is not mob_living_carbon)
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        mob_living_carbon fire_guy = (mob_living_carbon)target_mob;
        if (Dm.truthy(fire_guy.on_fire) && !Dm.truthy(lit))
        {
            await light__proc(span_notice(I18n.T("[] lights [] with []'s burning body. What a cold-blooded badass.", user, this, fire_guy)));
            return null;
        }
        obj_item_cigarette cig = help_light_cig(fire_guy) as obj_item_cigarette;
        if (!Dm.truthy(lit) || cig == null || Dm.truthy(user.combat_mode))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        if (Dm.truthy(cig.lit))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is already lit!", cig)));
            return null;
        }

        await cig.attempt_light(user, this, fire_guy == user ? null : span_notice(I18n.T("[] holds \\the [] out for [], and lights [] [].", user, this, fire_guy, fire_guy.p_their(), cig.name)));
        return null;
    }

    // tg: code/game/objects/items/cigarettes.dm:638
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        await light__proc();
        return null;
    }

    // tg: code/game/objects/items/cigarettes.dm:641
    public override object get_temperature()
    {
        return lit * heat;
    }

    // tg: code/game/objects/items/cigarettes.dm:644
    public virtual object make_mob_smoke(mob_living smoker = null)
    {
        // mob_smoke = new(smoker, /particles/smoke/cig); update_particle_position(mob_smoke, smoker.dir)
        Dm.Deferred("code/game/objects/items/cigarettes.dm:645", "make_mob_smoke: the smoke rising from the smoker (the particle holder is not ported; the client's particles, D142 row)");
        return mob_smoke;
    }

    // tg: code/game/objects/items/cigarettes.dm:649
    public virtual object make_cig_smoke()
    {
        // cig_smoke = new(src, /particles/smoke/cig); cig_smoke.particles?.scale *= 1.5
        Dm.Deferred("code/game/objects/items/cigarettes.dm:650", "make_cig_smoke: the smoke rising from the cigarette (the particle holder is not ported; the client's particles, D142 row)");
        return cig_smoke;
    }
}

// Cigarette brands.
// tg: code/game/objects/items/cigarettes.dm:673
public partial class obj_item_cigarette_greytide : obj_item_cigarette
{
    // tg: code/game/objects/items/cigarettes.dm:679
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        /// Weighted list of random reagents to add
        DmList possible_reagents = Dm.list(
            Dm.kv(typeof(datum_reagent_toxin_fentanyl), 2),
            Dm.kv(typeof(datum_reagent_glitter_random), 2),
            Dm.kv(typeof(datum_reagent_drug_aranesp), 2),
            Dm.kv(typeof(datum_reagent_consumable_laughter), 2),
            Dm.kv(typeof(datum_reagent_medicine_insulin), 2),
            Dm.kv(typeof(datum_reagent_drug_maint_powder), 2),
            Dm.kv(typeof(datum_reagent_drug_maint_sludge), 2),
            Dm.kv(typeof(datum_reagent_toxin_staminatoxin), 2),
            Dm.kv(typeof(datum_reagent_toxin_leadacetate), 2),
            Dm.kv(typeof(datum_reagent_drug_space_drugs), 2),
            Dm.kv(typeof(datum_reagent_drug_pumpup), 2),
            Dm.kv(typeof(datum_reagent_drug_kronkaine), 2),
            Dm.kv(typeof(datum_reagent_consumable_mintextract), 2),
            Dm.kv(typeof(datum_reagent_pax), 1));
        if (prob(40))
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(pick_weight(possible_reagents), rand(10, 15)));
        return dot;
    }
}

// tg: code/game/objects/items/cigarettes.dm:706
public partial class obj_item_cigarette_carp : obj_item_cigarette
{
    // tg: code/game/objects/items/cigarettes.dm:708
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!prob(5))
            return dot;
        if (reagents != null)
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_toxin_carpotoxin), 3)); // They lied
        return dot;
    }
}

// tg: code/game/objects/items/cigarettes.dm:715
public partial class obj_item_cigarette_syndicate : obj_item_cigarette
{
    // tg: code/game/objects/items/cigarettes.dm:722
    public override async DmTask smoke_in_face(mob_living getting_smoked = null)
    {
        await base.smoke_in_face(getting_smoked);
        await getting_smoked.adjust_eye_blur(6 * SECONDS);
        await getting_smoked.adjust_temp_blindness(2 * SECONDS);
    }
}

// Rollies.
// tg: code/game/objects/items/cigarettes.dm:744
public partial class obj_item_cigarette_rollie : obj_item_cigarette
{
    // tg: code/game/objects/items/cigarettes.dm:756
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        name = Dm.str(pick(Dm.list(
            "bifta",
            "bifter",
            "bird",
            "blunt",
            "bloint",
            "boof",
            "boofer",
            "bomber",
            "bone",
            "bun",
            "doink",
            "doob",
            "doober",
            "doobie",
            "dutch",
            "fatty",
            "hogger",
            "hooter",
            "hootie",
            Dm.text("\\improper J"),
            "jay",
            "jimmy",
            "joint",
            "juju",
            "jeebie weebie",
            "number",
            "owl",
            "phattie",
            "puffer",
            "reef",
            "reefer",
            "rollie",
            "scoobie",
            "shorty",
            "spiff",
            "spliff",
            "toke",
            "torpedo",
            "zoot",
            "zooter")));
        object dot = base.Initialize(mapload, __extra);
        pixel_x = rand(-5, 5);
        pixel_y = rand(-5, 5);
        return dot;
    }
}

// tg: code/game/objects/items/cigarettes.dm:836
public partial class obj_item_cigbutt_roach : obj_item_cigbutt
{
    // tg: code/game/objects/items/cigarettes.dm:845
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = rand(-5, 5);
        pixel_y = rand(-5, 5);
        return dot;
    }
}

// tg: code/game/objects/items/cigarettes.dm:852
public partial class obj_item_cigarette_dart : obj_item_cigarette
{
    // tg: code/game/objects/items/cigarettes.dm:864
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //the compiled icon state is how it appears when it's on.
        //That's how we want it to show on orbies (little virtual PDA pets).
        //However we should reset their appearance on runtime.
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }
}

// tg: code/game/objects/items/cigarettes.dm:918
public partial class obj_item_cigbutt : obj_item
{
    // tg: code/game/objects/items/cigarettes.dm:925
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_carbon), 2));
    }
}

/////////////////
//SMOKING PIPES//
/////////////////
// tg: code/game/objects/items/cigarettes.dm:943
public partial class obj_item_cigarette_pipe : obj_item_cigarette
{
    // tg: code/game/objects/items/cigarettes.dm:960
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_NAME);
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:964
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.truthy(packeditem) ? Dm.text("[]-packed []", packeditem, Dm.initial(this, (obj_item_cigarette_pipe p) => p.name)) : Dm.text("empty []", Dm.initial(this, (obj_item_cigarette_pipe p) => p.name));
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:968
    public override async DmTask put_out(mob user = null, float? done_early = null)
    {
        done_early ??= FALSE;
        lit = FALSE;
        if (Dm.truthy(done_early))
            user.visible_message(span_notice(I18n.T("[] puts out [].", user, this)), span_notice(I18n.T("You put out [].", this)));
        else
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("Your [] goes out.", name)));
            packeditem = null;
        }
        update_appearance(UPDATE_ICON);
        await set_light_on(FALSE);
        STOP_PROCESSING(SSobj, this);
        QDEL_NULL(ref cig_smoke);
    }

    // tg: code/game/objects/items/cigarettes.dm:982
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(istype(tool, typeof(obj_item_food_grown)) || istype(tool, typeof(obj_item_food_drug))))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (Dm.truthy(packeditem))
        {
            to_chat(user, span_warning(I18n.T("It is already packed!")));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item to_smoke = tool;
        if (istype(to_smoke, typeof(obj_item_food_grown)) && !HAS_TRAIT(to_smoke, TRAIT_DRIED))
        {
            to_chat(user, span_warning(I18n.T("It has to be dried first!")));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You stuff [] into [].", to_smoke, this)));
        smoketime = 13 * MINUTES;
        packeditem = to_smoke.name;
        update_name();
        if (to_smoke.reagents != null)
            await ((datum_reagents)to_smoke.reagents).trans_to(this, ((datum_reagents)to_smoke.reagents).total_volume, transferred_by: user);
        qdel(to_smoke);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/cigarettes.dm:1005
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        atom location = drop_location() as atom;
        if (Dm.truthy(packeditem) && !Dm.truthy(lit))
        {
            to_chat(user, span_notice(I18n.T("You empty [] onto [].", this, location)));
            Dm.@new<obj_effect_decal_cleanable_ash>(location);
            packeditem = null;
            smoketime = 0;
            await ((datum_reagents)reagents).clear_reagents();
            update_name();
            return null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }
}

///////////
//ROLLING//
///////////
// tg: code/game/objects/items/cigarettes.dm:1041
public partial class obj_item_rollingpaper : obj_item
{
    // tg: code/game/objects/items/cigarettes.dm:1047
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_cigarette_rollie), CUSTOM_INGREDIENT_ICON_NOCHANGE, Dm.named("ingredient_type", CUSTOM_INGREDIENT_TYPE_DRYABLE), Dm.named("max_ingredients", 2))); // gaps H2
        return dot;
    }
}

// tg: code/game/objects/items/cigarettes.dm:1055  (gaps OD1)
public partial class obj_item_vape : obj_item
{
    // tg: code/game/objects/items/cigarettes.dm:1085
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(chem_volume, NO_REACT);
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_drug_nicotine), 50));
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:1090
    public override DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is puffin hard on dat vape, [] trying to join the vape life on a whole notha plane!", user, user.p_they())));//it doesn't give you cancer, it is cancer
        return DmTask<object>.FromResult(Dm.bitor(TOXLOSS, OXYLOSS));
    }

    // tg: code/game/objects/items/cigarettes.dm:1094
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(screw))
        {
            screw = TRUE;
            to_chat(user, span_notice(I18n.T("You open the cap on [].", this)));
            holder.flags = Dm.bitor(holder.flags, OPENCONTAINER);
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                icon_state = "vapeopen_high";
                await set_greyscale(new_config: typeof(datum_greyscale_config_vape_open_high));
            }
            else if (Dm.truthy(super))
            {
                icon_state = "vapeopen_med";
                await set_greyscale(new_config: typeof(datum_greyscale_config_vape_open_med));
            }
            else
            {
                icon_state = "vapeopen_low";
                await set_greyscale(new_config: typeof(datum_greyscale_config_vape_open_low));
            }
        }
        else
        {
            screw = FALSE;
            to_chat(user, span_notice(I18n.T("You close the cap on [].", this)));
            holder.flags = Dm.bitand(holder.flags, ~(int)OPENCONTAINER);
            object initial_post_init = Dm.initial(this, nameof(post_init_icon_state));
            icon_state = Dm.str(Dm.truthy(initial_post_init) ? initial_post_init : Dm.initial(this, nameof(icon_state)));
            await set_greyscale(new_config: Dm.initial(this, nameof(greyscale_config)));
        }
        return null;
    }

    // tg: code/game/objects/items/cigarettes.dm:1115
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = TRUE;
        if (Dm.truthy(screw) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))//also kinky
        {
            if (!Dm.truthy(super))
            {
                super = TRUE;
                to_chat(user, span_notice(I18n.T("You increase the voltage of [].", this)));
                icon_state = "vapeopen_med";
                await set_greyscale(new_config: typeof(datum_greyscale_config_vape_open_med));
            }
            else
            {
                super = FALSE;
                to_chat(user, span_notice(I18n.T("You decrease the voltage of [].", this)));
                icon_state = "vapeopen_low";
                await set_greyscale(new_config: typeof(datum_greyscale_config_vape_open_low));
            }
        }

        if (Dm.truthy(screw) && Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            to_chat(user, span_warning(I18n.T("[] can't be modified!", this)));
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:1132
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra) // I WON'T REGRET WRITTING THIS, SURLY.
    {
        if (!Dm.truthy(screw))
        {
            balloon_alert(user, I18n.T("open the cap first!"));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, I18n.T("already emagged!"));
            return FALSE;
        }

        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        super = FALSE;
        balloon_alert(user, I18n.T("voltage maximized"));
        icon_state = "vapeopen_high";
        await set_greyscale(new_config: typeof(datum_greyscale_config_vape_open_high));
        do_sparks(5, TRUE, this);
        return TRUE;
    }

    // tg: code/game/objects/items/cigarettes.dm:1150
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(screw))
        {
            balloon_alert(user, I18n.T("open the cap first!"));
            return null;
        }
        datum_reagents holder = (datum_reagents)reagents;
        if (holder.total_volume > 0)
        {
            to_chat(user, span_notice(I18n.T("You empty [] of all reagents.", this)));
            await holder.clear_reagents();
        }
        return null;
    }

    // tg: code/game/objects/items/cigarettes.dm:1158
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_MASK)))
            return;

        if (Dm.truthy(screw))
        {
            to_chat(user, span_warning(I18n.T("You need to close the cap first!")));
            return;
        }

        to_chat(user, span_notice(I18n.T("You start puffing on the vape.")));
        datum_reagents holder = (datum_reagents)reagents;
        holder.flags = Dm.bitand(holder.flags, ~(int)NO_REACT);
        await holder.handle_reactions();
        if (QDELETED(this))
            return;
        await set_light_on(TRUE);
        START_PROCESSING(SSobj, this);
    }

    // tg: code/game/objects/items/cigarettes.dm:1175
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (user.get_item_by_slot(ITEM_SLOT_MASK) == this)
        {
            datum_reagents holder = (datum_reagents)reagents;
            holder.flags = Dm.bitor(holder.flags, NO_REACT);
            STOP_PROCESSING(SSobj, this);
            await set_light_on(FALSE);
        }
        return dot;
    }

    // tg: code/game/objects/items/cigarettes.dm:1182
    public virtual async DmTask handle_reagents()
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume))
            return;

        mob_living_carbon vaper = loc as mob_living_carbon;
        if (!iscarbon(vaper) || vaper.get_item_by_slot(ITEM_SLOT_MASK) != this)
        {
            await holder.remove_all(REAGENTS_METABOLISM);
            return;
        }

        if (Dm.truthy(holder.get_reagent_amount(typeof(datum_reagent_fuel))))
        {
            //HOT STUFF
            await vaper.adjust_fire_stacks(2);
            vaper.ignite_mob();
        }

        // Preserve old welding fuel behavior
        if (Dm.truthy(Dm.bitand(await holder.spark_act(0, TRUE, banned_reagents: typeof(datum_reagent_fuel)), SPARK_ACT_DESTRUCTIVE)))
        {
            qdel(this);
            return;
        }

        if (!Dm.truthy(await holder.trans_to(vaper, REAGENTS_METABOLISM, methods: INHALE, ignore_stomach: TRUE)))
            await holder.remove_all(REAGENTS_METABOLISM);
    }

    // tg: code/game/objects/items/cigarettes.dm:1204
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        mob_living M = loc as mob_living;

        if (isliving(loc))
            M.ignite_mob();

        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume))
        {
            if (ismob(loc))
            {
                to_chat(M, span_warning(I18n.T("[] is empty!", this)));
                STOP_PROCESSING(SSobj, this);
                //it's reusable so it won't unequip when empty
            }
            return null;
        }

        if (!(drag_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
            return null;

        //Time to start puffing those fat vapes, yo.
        drag_cooldown = world.time + dragtime; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            float smoke_amount = DIAMOND_AREA(4);
            Waiting.NoSleep(do_chem_smoke(amount: smoke_amount, holder: this, location: loc, carry: holder, carry_limit: 20, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_chem_smoke_machine)));
            Waiting.NoSleep(holder.remove_all(smoke_amount / 24));
            if (prob(5)) //small chance for the vape to break and deal damage if it's emagged
            {
                playsound(get_turf(this), Dm.resource("sound/effects/pop_expl.ogg"), 50, FALSE);
                Waiting.NoSleep(M.apply_damage(20, BURN, BODY_ZONE_HEAD));
                Waiting.NoSleep(M.Paralyze(300));
                do_sparks(5, TRUE, this);
                to_chat(M, span_userdanger(I18n.T("[] suddenly explodes in your mouth!", this)));
                qdel(this);
                return null;
            }
        }
        else if (Dm.truthy(super))
        {
            float smoke_amount = DIAMOND_AREA(1);
            Waiting.NoSleep(do_chem_smoke(amount: smoke_amount, holder: this, location: loc, carry: holder, carry_limit: 20, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_chem_smoke_machine)));
            Waiting.NoSleep(holder.remove_all(smoke_amount / 24));
        }

        Waiting.NoSleep(handle_reagents());
        return null;
    }
}
