// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G4c, D316): His Grace's effect and its alert; silicons SI2 (D326) the Synthpax immunity; gaps B1 (D327) the desensitization of the combat PTSD and the obsession; gaps K1
// (D327) the changeling's Fleshmend; gaps X5 the alien nest's sustenance; gaps MO1 (D333) the MODsuit jump jet's anti-gravity; gaps SL1
// (D333) the exercised effect of a workout (its mood event since gaps MS1); gaps RG1
// (D339) the garibaldi's revolutionary resilience; the regenerative core's tendrils; the other buffs come with their systems.

//Largely beneficial effects go here, even if they have drawbacks.

// tg: code/datums/status_effects/buffs.dm:3
public partial class datum_status_effect_his_grace : datum_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:13
    [DmParams("new_owner", "inputgender")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object inputgender = Dm.arg(__extra, 0);
        object dot = await base.on_creation(new_owner, __extra);
        gender = inputgender as string;
        if (gender == MALE)
        {
            word = "His";
            word2 = "Him";
            linked_alert.icon_state = "his_grace";
        }
        else
        {
            word = "Her";
            word2 = "Her";
            linked_alert.icon_state = "her_grace";
        }
        return dot;
    }

    // tg: code/datums/status_effects/buffs.dm:44
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_stun_absorption(
            source: id,
            priority: 3,
            self_message: span_boldwarning(I18n.T("[] Grace protects you from the stun!", word)));
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/buffs.dm:51
    public override async DmTask<object> on_remove()
    {
        owner.remove_stun_absorption(id);
        return null;
    }

    // tg: code/datums/status_effects/buffs.dm:54
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        float seconds = Dm.num(seconds_between_ticks);
        bloodlust = 0;
        int graces = 0;
        foreach (obj_item_his_grace HG in Dm.each<obj_item_his_grace>(owner.held_items))
        {
            if (HG.bloodthirst > bloodlust)
                bloodlust = HG.bloodthirst;
            if (Dm.truthy(HG.awakened))
                graces++;
        }
        if (graces == 0)
        {
            await owner.apply_status_effect(typeof(datum_status_effect_his_wrath), word, word2);
            qdel(this);
            return null;
        }
        float grace_heal = bloodlust * 0.02f;
        object need_mob_update = FALSE;
        need_mob_update = Dm.add(need_mob_update, await owner.adjust_brute_loss(-grace_heal * seconds, updating_health: FALSE));
        need_mob_update = Dm.add(need_mob_update, await owner.adjust_fire_loss(-grace_heal * seconds, updating_health: FALSE));
        need_mob_update = Dm.add(need_mob_update, await owner.adjust_tox_loss(-grace_heal * seconds));
        need_mob_update = Dm.add(need_mob_update, await owner.adjust_oxy_loss(-(grace_heal * 2) * seconds, updating_health: FALSE));
        if (Dm.truthy(need_mob_update))
            await owner.updatehealth();
        return null;
    }
}

// tg: code/datums/status_effects/buffs.dm:25
public partial class atom_movable_screen_alert_status_effect_his_grace : atom_movable_screen_alert_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:31
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        desc = Dm.initial(this, (atom_movable_screen_alert_status_effect_his_grace p) => p.desc);
        datum_status_effect_his_grace HG = attached_effect as datum_status_effect_his_grace;
        object His = HG.word;
        object Him = HG.word2;
        name = Dm.text("[] Grace", His);
        desc = Dm.text("[] Grace hungers, and you must feed [].", His, Him);
        icon_state = Dm.text("[]_grace", lowertext(Dm.text("[]", His)));
        desc += Dm.text("<br><font size=3><b>Current Bloodthirst: []</b></font>", HG.bloodlust)
            + Dm.text("<br>Becomes undroppable at <b>[]</b>", HIS_GRACE_FAMISHED)
            + Dm.text("<br>Will consume you at <b>[]</b>", HIS_GRACE_CONSUME_OWNER);
        await base.MouseEntered(location, control, @params);
    }
}

//Used by changelings to rapidly heal
//Heals 10 brute and oxygen damage every second, and 5 fire
//Being on fire will suppress this healing
// tg: code/datums/status_effects/buffs.dm:181  (gaps SL1, D333)
public partial class datum_status_effect_exercised : datum_status_effect
{
    /// Having any of these reagents in your system extends the duration
    // tg: code/datums/status_effects/buffs.dm:187  var/static/list/supplementary_reagents_bonus (rules 10.6)
    public static ref DmList supplementary_reagents_bonus => ref TgState.Current.datum_status_effect_exercised__supplementary_reagents_bonus;

    // tg: code/datums/status_effects/buffs.dm:206
    public virtual object workout_duration(mob_living new_owner = null, object bonus_time = null)
    {
        if (!Dm.truthy(bonus_time) || !Dm.truthy(new_owner.mind) || !iscarbon(new_owner))
            return 0 * SECONDS;

        float modifier = 1;
        if (HAS_TRAIT(new_owner, TRAIT_HULK))
            modifier += 0.5f;

        if (HAS_TRAIT(new_owner, TRAIT_STIMMED)) // Naturally produces stimulants to help get you PUMPED
            modifier += 1;

        if (HAS_TRAIT(new_owner, TRAIT_FAT)) // less xp until you get into shape
            modifier -= 0.5f;

        if (Dm.truthy(((datum_reagents)new_owner.reagents).has_reagent(typeof(datum_reagent_drug_pumpup)))) // steriods? yes please!
            modifier += 3;

        if (Dm.truthy(((datum_reagents)new_owner.reagents).has_reagent(typeof(datum_reagent_inverse_oxandrolone)))) // MOREEEEE
            modifier += 2;

        float food_boost = 0;
        // DM: the typed loop var filters the items with istype, which no typepath of the list passes, so tg's food boost is 0
        foreach (datum_reagent workout_reagent in Dm.each<datum_reagent>(supplementary_reagents_bonus))
        {
            if (Dm.truthy(((datum_reagents)new_owner.reagents).has_reagent(workout_reagent)))
                food_boost += Dm.num(supplementary_reagents_bonus[workout_reagent]);
        }

        float skill_level_boost = (Dm.num(new_owner.mind?.get_skill_level(typeof(datum_skill_athletics))) - 1) * 2 * SECONDS;
        bonus_time = Dm.mul(Dm.add(Dm.add(bonus_time, food_boost), skill_level_boost), modifier);

        object exhaustion_limit = new_owner.mind?.get_skill_modifier(typeof(datum_skill_athletics), SKILL_VALUE_MODIFIER);
        if (Dm.ge(Dm.add(duration, bonus_time), exhaustion_limit))
        {
            duration = Dm.num_or_null(exhaustion_limit);
            to_chat(new_owner, span_userdanger(I18n.T("Your muscles are exhausted! Might be a good idea to sleep...")));
            Waiting.Detach(new_owner.emote("scream"));
            return null; // exhaustion_limit
        }

        return bonus_time;
    }

    // tg: code/datums/status_effects/buffs.dm:241
    [DmParams("new_owner", "bonus_time")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object bonus_time = Dm.arg(__extra, 0);
        duration = Dm.num(duration) + Dm.num(workout_duration(new_owner, bonus_time));
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/buffs.dm:245
    [DmParams("effect", "bonus_time")]
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        object bonus_time = Dm.arg(__extra, 0);
        duration = Dm.num(duration) + Dm.num(workout_duration(owner, bonus_time));
        owner.clear_mood_event("exercise"); // we need to reset the old mood event in case our fitness skill changes
        await owner.add_mood_event("exercise", typeof(datum_mood_event_exercise), owner.mind.get_skill_level(typeof(datum_skill_athletics)));
    }

    // tg: code/datums/status_effects/buffs.dm:250
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!Dm.truthy(owner.mind))
            return FALSE;
        await owner.add_mood_event("exercise", typeof(datum_mood_event_exercise), owner.mind.get_skill_level(typeof(datum_skill_athletics)));
        return TRUE;
    }

    // tg: code/datums/status_effects/buffs.dm:256
    public override async DmTask<object> on_remove()
    {
        owner.clear_mood_event("exercise");
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/status_effects/buffs.dm:187-202  /datum/status_effect/exercised/var/static/list/supplementary_reagents_bonus
    internal DmList datum_status_effect_exercised__supplementary_reagents_bonus = Dm.list(
        Dm.kv(typeof(datum_reagent_consumable_ethanol_protein_blend), 10 * SECONDS), // protein shakes are very robust
        Dm.kv(typeof(datum_reagent_inverse_oxandrolone), 8 * SECONDS),
        Dm.kv(typeof(datum_reagent_consumable_nutriment_protein), 5 * SECONDS),
        Dm.kv(typeof(datum_reagent_consumable_nutriment_vitamin), 4 * SECONDS),
        Dm.kv(typeof(datum_reagent_consumable_milk), 4 * SECONDS),
        Dm.kv(typeof(datum_reagent_consumable_rice), 3 * SECONDS),
        // keep in mind you can eat a raw egg to acquire both these reagents at the same time
        Dm.kv(typeof(datum_reagent_consumable_eggwhite), 3 * SECONDS),
        Dm.kv(typeof(datum_reagent_consumable_eggyolk), 2 * SECONDS),
        // weak workout food
        Dm.kv(typeof(datum_reagent_consumable_nutraslop), 2 * SECONDS), // prison food to bulk up with
        Dm.kv(typeof(datum_reagent_consumable_soymilk), 1 * SECONDS), // darn vegans!
        // time for the bad stuff
        Dm.kv(typeof(datum_reagent_consumable_sugar), -1 * SECONDS),
        Dm.kv(typeof(datum_reagent_consumable_monkey_energy), -1 * SECONDS), // the marketing was a lie
        Dm.kv(typeof(datum_reagent_consumable_nutriment_fat), -1 * SECONDS));
}

// tg: code/datums/status_effects/buffs.dm:129  (gaps K1, D327)
public partial class datum_status_effect_fleshmend : datum_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:135
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        if (iscarbon(owner))
        {
            mob_living_carbon carbon_owner = owner as mob_living_carbon;
            QDEL_LAZYLIST(ref carbon_owner.all_scars);
        }

        RegisterSignal(owner, COMSIG_LIVING_IGNITED, nameof(on_ignited));
        RegisterSignal(owner, COMSIG_LIVING_EXTINGUISHED, nameof(on_extinguished));
        return dot;
    }

    // tg: code/datums/status_effects/buffs.dm:144
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.on_creation(new_owner, __extra);
        if (!Dm.truthy(dot) || owner == null || linked_alert == null)
            return dot;
        if (Dm.truthy(owner.on_fire))
            linked_alert.icon_state = "fleshmend_fire";
        return dot;
    }

    // tg: code/datums/status_effects/buffs.dm:151
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(COMSIG_LIVING_IGNITED, COMSIG_LIVING_EXTINGUISHED));
        return null;
    }

    // tg: code/datums/status_effects/buffs.dm:154
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.truthy(owner.on_fire))
            return null;

        float need_mob_update = FALSE;
        need_mob_update += Dm.num(await owner.adjust_brute_loss(-4 * Dm.num(seconds_between_ticks), updating_health: FALSE));
        need_mob_update += Dm.num(await owner.adjust_fire_loss(-2 * Dm.num(seconds_between_ticks), updating_health: FALSE));
        need_mob_update += Dm.num(await owner.adjust_oxy_loss(-4 * Dm.num(seconds_between_ticks), updating_health: FALSE));
        if (need_mob_update != 0)
            await owner.updatehealth();
        return null;
    }

    // tg: code/datums/status_effects/buffs.dm:165
    [SignalHandler]
    public virtual void on_ignited(datum source = null)
    {
        linked_alert?.icon_state = "fleshmend_fire";
    }

    // tg: code/datums/status_effects/buffs.dm:170
    [SignalHandler]
    public virtual void on_extinguished(datum source = null)
    {
        linked_alert?.icon_state = "fleshmend";
    }
}

//used by the garibaldi cocktail to grant revolutionaries wound resistance and fearlessness
// tg: code/datums/status_effects/buffs.dm:707 (gaps RG1: the garibaldi cocktail)
public partial class datum_status_effect_rev_resilience : datum_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:712
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        to_chat(owner, span_warning(I18n.T("You feel your revolutionary spirit surging! You feel like nothing the oppressors could throw at you could wound your pride!")));
        owner.add_traits(Dm.list(TRAIT_HARDLY_WOUNDED, TRAIT_ANALGESIA, TRAIT_FEARLESS), TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/datums/status_effects/buffs.dm:717
    public override async DmTask<object> on_remove()
    {
        to_chat(owner, span_notice(I18n.T("You feel your surge of revolutionary zeal fade. You hope you don't get shot in the foot...")));
        owner.remove_traits(Dm.list(TRAIT_HARDLY_WOUNDED, TRAIT_ANALGESIA, TRAIT_FEARLESS), TRAIT_STATUS_EFFECT(id));
        return null;
    }
}

// tg: code/datums/status_effects/buffs.dm:722 (silicons SI2: the peacekeeper cyborg's Synthpax)
public partial class datum_status_effect_synthpax_immunity : datum_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:728
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0) ?? 5 * SECONDS;
        this.duration = Dm.num(duration);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/buffs.dm:732
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_SYNTHPAX_IMMUNE, TRAIT_STATUS_EFFECT(id));
        REMOVE_TRAIT(owner, TRAIT_PACIFISM, METABOLIZATION_TRAIT(typeof(datum_reagent_pax_peaceborg)));
        return TRUE;
    }

    // tg: code/datums/status_effects/buffs.dm:737
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_SYNTHPAX_IMMUNE, TRAIT_STATUS_EFFECT(id));
        if (Dm.truthy((owner.reagents as datum_reagents).has_reagent(typeof(datum_reagent_pax_peaceborg))))
            ADD_TRAIT(owner, TRAIT_PACIFISM, METABOLIZATION_TRAIT(typeof(datum_reagent_pax_peaceborg)));
        return null;
    }
}

// tg: code/datums/status_effects/buffs.dm:359  (silicons SI6c, D326: a musician's playing)
public partial class datum_status_effect_good_music : datum_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:366
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (!HAS_TRAIT(owner, TRAIT_DEAF))
        {
            await owner.adjust_dizzy(-4 * SECONDS);
            await owner.adjust_jitter(-4 * SECONDS);
            await owner.adjust_confusion(-1 * SECONDS);
            await owner.add_mood_event("goodmusic", typeof(datum_mood_event_goodmusic));
        }
        return null;
    }
}

/// Applies desensitized mood modifier to the mob, carrying between mind transfers
// tg: code/datums/status_effects/buffs.dm:665  (gaps B1: the combat PTSD and the obsession traumas)
public partial class datum_status_effect_desensitized : datum_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:675
    [DmParams("new_owner", "effect_id", "magnitude")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object effect_id = Dm.arg(__extra, 0);
        object magnitude = Dm.arg(__extra, 1);
        this.effect_id = effect_id;
        this.magnitude = max(DESENSITIZED_MINIMUM, Dm.num(magnitude));
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/buffs.dm:680
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (owner.mind != null)
            owner.mind.desensitized_level *= magnitude;
        RegisterSignal(owner, COMSIG_MOB_MIND_TRANSFERRED_INTO, nameof(add_magnitude));
        RegisterSignal(owner, COMSIG_MOB_MIND_TRANSFERRED_OUT_OF, nameof(remove_magnitude));
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/datums/status_effects/buffs.dm:686
    public override async DmTask<object> on_remove()
    {
        if (owner.mind != null)
            owner.mind.desensitized_level /= magnitude;
        UnregisterSignal(owner, Dm.list(COMSIG_MOB_MIND_TRANSFERRED_INTO, COMSIG_MOB_MIND_TRANSFERRED_OUT_OF));
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/status_effects/buffs.dm:690
    [DmParams("effect_id", "magnitude")]
    public override object before_remove(params object[] __extra)
    {
        object effect_id = Dm.arg(__extra, 0);
        object magnitude = Dm.arg(__extra, 1);
        if (istext(this.effect_id) && istext(effect_id)) // if an id is set, they must match
            return Dm.num(Dm.eq(this.effect_id, effect_id));
        if (isnum(magnitude)) // otherwise if a magnitude is passed, it must match
            return Dm.num(Dm.eq(this.magnitude, magnitude));
        return FALSE;
    }

    // tg: code/datums/status_effects/buffs.dm:697
    [SignalHandler]
    public virtual void add_magnitude(datum source = null, mob_living old_body = null, datum_mind swapping = null)
    {
        swapping.desensitized_level *= magnitude;
    }

    // tg: code/datums/status_effects/buffs.dm:701
    [SignalHandler]
    public virtual void remove_magnitude(datum source = null, mob_living old_body = null, datum_mind swapping = null)
    {
        swapping.desensitized_level /= magnitude;
    }
}

// tg: code/datums/status_effects/buffs.dm:529  (gaps X5: the alien nest's sustenance of a host)
public partial class datum_status_effect_nest_sustenance : datum_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:535
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await base.tick(seconds_between_ticks);

        if (owner.stat == DEAD) //If the victim has died due to complications in the nest
        {
            qdel(this);
            return null;
        }

        float need_mob_update = FALSE;
        need_mob_update += Dm.num(await owner.adjust_brute_loss(-2 * Dm.num(seconds_between_ticks), updating_health: FALSE));
        need_mob_update += Dm.num(await owner.adjust_fire_loss(-2 * Dm.num(seconds_between_ticks), updating_health: FALSE));
        need_mob_update += Dm.num(await owner.adjust_oxy_loss(-4 * Dm.num(seconds_between_ticks), updating_health: FALSE));
        need_mob_update += Dm.num(await owner.adjust_stamina_loss(-4 * Dm.num(seconds_between_ticks), updating_stamina: FALSE));
        if (Dm.truthy(need_mob_update))
            await owner.updatehealth();
        owner.adjust_bodytemperature(BODYTEMP_NORMAL, 0, BODYTEMP_NORMAL); //Won't save you from the void of space, but it will stop you from freezing or suffocating in low pressure
        return null;
    }
}

// gaps WP1 (D333): the speed boost, for the chain of command's hasten (weaponry/melee/chainofcommand.dm): its duration if given,
// tg's alert flash and chime, its move and action speed modifiers for as long as it lasts.
// tg: code/datums/status_effects/buffs.dm:467
public partial class datum_status_effect_speed_boost : datum_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:477
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object set_duration = Dm.arg(__extra, 0);
        if (isnum(set_duration))
            duration = Dm.num(set_duration);
        new_owner.do_alert_animation();
        playsound(new_owner, Dm.resource("sound/machines/chime.ogg"), 50, FALSE, -5);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/buffs.dm:484
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (move_datum != null)
            await owner.add_movespeed_modifier(move_datum, update: TRUE);
        if (action_datum != null)
            await owner.add_actionspeed_modifier(action_datum, update: TRUE);
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/buffs.dm:491
    public override async DmTask<object> on_remove()
    {
        if (move_datum != null)
            owner.remove_movespeed_modifier(move_datum, update: TRUE);
        if (action_datum != null)
            owner.remove_actionspeed_modifier(action_datum, update: TRUE);
        await DmTask.CompletedTask;
        return null;
    }
}

/// Gives you a brief period of anti-gravity
// tg: code/datums/status_effects/buffs.dm:596
public partial class datum_status_effect_jump_jet : datum_status_effect
{
    // tg: code/datums/status_effects/buffs.dm:602
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.AddElement(typeof(datum_element_forced_gravity), 0);
        return TRUE;
    }

    // tg: code/datums/status_effects/buffs.dm:606
    public override async DmTask<object> on_remove()
    {
        owner.RemoveElement(typeof(datum_element_forced_gravity), 0);
        return null;
    }
}

/// Same as the shadow alert, but also heal in darkness!! Mostly superseded but some simple mobs use this
// tg: code/datums/status_effects/buffs.dm:641
public partial class datum_status_effect_shadow_regeneration : datum_status_effect_shadow
{
    // tg: code/datums/status_effects/buffs.dm:643
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        await heal_owner();
        return TRUE;
    }

    // tg: code/datums/status_effects/buffs.dm:650
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        await base.refresh(effect, __extra);
        await heal_owner();
    }

    /// Regenerate health whenever this status effect is applied or reapplied
    // tg: code/datums/status_effects/buffs.dm:655
    public virtual async DmTask heal_owner()
    {
        await owner.heal_overall_damage(brute: 1, burn: 1, required_bodytype: BODYTYPE_ORGANIC);
    }
}
