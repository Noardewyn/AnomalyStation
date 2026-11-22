// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/heart/_heart.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P2 (scope.md 2e "organs"): the heart's beat; M8 the stethoscope and the scanner text; silicons SI3 the cybernetic hearts. Gaps OG1
// (D339, D341): the rest of the file, squeezing a heart, the cursed, surplus, freedom, evolved and sacred hearts.

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:1
[DmPath("/obj/item/organ/heart")]
public partial class obj_item_organ_heart : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:2-26
    public obj_item_organ_heart()
    {
        name = "heart";
        desc = "I feel bad for the heartless bastard who lost this.";
        icon_state = "heart-on";
        base_icon_state = "heart";

        zone = BODY_ZONE_CHEST;
        slot = ORGAN_SLOT_HEART;
        item_flags = NO_BLOOD_ON_ITEM;
        healing_factor = STANDARD_ORGAN_HEALING;
        decay_factor = 2.5f * STANDARD_ORGAN_DECAY; //designed to fail around 6 minutes after death

        low_threshold_passed = span_info("Prickles of pain appear then die out from within your chest...");
        high_threshold_passed = span_warning("Something inside your chest hurts, and the pain isn't subsiding. You notice yourself breathing far faster than before.");
        now_fixed = span_info("Your heart begins to beat again.");
        high_threshold_cleared = span_info("The pain in your chest has died down, and your breathing becomes more relaxed.");

        cell_line = CELL_LINE_ORGAN_HEART;
        cells_minimum = 1;
        cells_maximum = 2;
        visual = FALSE;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:18-22  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        attack_verb_continuous = Dm.list("beats", "thumps");
        attack_verb_simple = Dm.list("beat", "thump");

        // Love is stored in the heart.
        food_reagents = Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_organ_tissue), 5), Dm.kv(typeof(datum_reagent_love), 2.5f));
    }

    // Heart attack code is in code/modules/mob/living/carbon/human/life.dm

    /// Whether the heart is currently beating.
    /// Do not set this directly. Use Restart() and Stop() instead.
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:33  VAR_PRIVATE/beating = TRUE
    [DmPrivate]
    public float beating = TRUE;

    /// is this mob having a heatbeat sound played? if so, which?
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:36
    public float beat = BEAT_NONE;
    /// whether the heart's been operated on to fix some of its damages
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:38
    public float operated = FALSE;
    /// The message that is displayed when listening to a heart via a stethoscope
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:40
    public string beat_noise = "a rhythmic thumping";
    /// The rate at which blood is pumped by the heart is multiplied by this (value of 0 disables blood regeneration entirely)
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:42
    public float blood_regeneration_multiplier = 1;
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:47 (interleaved types, rules 1.9)
public partial class obj_item_organ_heart : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:47
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, (Dm.truthy(beating) ? "on" : "off"));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:51
    [DmParams("heartless", "special", "movement_flags")]
    public override async DmTask Remove(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon heartless = organ_owner;
        await base.Remove(heartless, special, movement_flags);
        if (!Dm.truthy(special) && !QDELETED(this))
            _addtimer(Dm.@new<datum_callback>(this, nameof(stop_if_unowned)), 12 * SECONDS, TIMER_DELETE_ME, file: "code/modules/surgery/organs/internal/heart/_heart.dm", line: 54);
        beat = BEAT_NONE;
        if (owner != null)
        {
            owner.stop_sound_channel(CHANNEL_HEARTBEAT); // tg: code/modules/surgery/organs/internal/heart/_heart.dm:53
        }
    }
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:55
public partial class obj_item_organ_heart : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:55
    public virtual void stop_if_unowned()
    {
        if (Dm.truthy(IS_ROBOTIC_ORGAN(this)))
            return;
        if (owner == null)
            Stop();
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:75
    public virtual object Stop()
    {
        if (!Dm.truthy(beating))
            return FALSE;

        beating = FALSE;
        update_appearance();
        beat = BEAT_NONE;
        if (owner != null)
        {
            owner.stop_sound_channel(CHANNEL_HEARTBEAT); // tg: code/modules/surgery/organs/internal/heart/_heart.dm:82
        }
        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:85
    public virtual object Restart()
    {
        if (Dm.truthy(beating))
            return FALSE;

        beating = TRUE;
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:98
    public virtual object get_blood_regeneration_multiplier()
    {
        if (!Dm.truthy(is_beating()) || Dm.truthy(Dm.bitand(organ_flags, Dm.bitor(ORGAN_FAILING, ORGAN_EMP))))
            return 0;

        float health_percent = clamp((maxHealth - Dm.num(damage)) / maxHealth, 0, 1);
        return blood_regeneration_multiplier * health_percent;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:107
    public virtual object is_beating()
    {
        return beating;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:168
    public virtual async DmTask<object> hear_beat_noise(mob_living hearer = null)
    {
        return span_notice(I18n.T("[] heart produces [].", owner.p_Their(), beat_noise));
    }
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:96 (interleaved types, rules 1.9)
public partial class obj_item_organ_heart : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:96
    public override void OnEatFrom(object eater = null, object feeder = null)
    {
        base.OnEatFrom(eater, feeder);
        Stop();
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:123
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        // If the owner doesn't need a heart, we don't need to do anything with it.
        if (!Dm.truthy(owner.needs_heart()))
            return;

        // Handle "sudden" heart attack
        if (!Dm.truthy(beating) || Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            if (Dm.truthy(owner.can_heartattack()) && Dm.truthy(Stop()))
            {
                if (!IS_UNCONSCIOUS_OR_CRIT(owner))
                {
                    // GAP(lead): the pronoun procs (p_their, __HELPERS/pronouns.dm) are not declared; visible_message is a
                    // deferred leaf (M9), so the message text is never shown.
                    object p_their__gap = null;
                    owner.visible_message(span_danger(I18n.T("[] clutches at [] chest as if [] heart is stopping!", owner, p_their__gap, p_their__gap)));
                }
                to_chat(owner, span_userdanger(I18n.T("You feel a terrible pain in your chest, as if your heart has stopped!")));
            }
            return;
        }

        // Beyond deals with sound effects, so nothing needs to be done if no client
        if (owner.client == null)
            return;

        if (owner.stat == SOFT_CRIT)
        {
            if (beat != BEAT_SLOW)
            {
                beat = BEAT_SLOW;
                to_chat(owner, span_notice(I18n.T("You feel your heart slow down...")));
                // SEND_SOUND(owner, sound(...)) (__DEFINES/sound.dm: DIRECT_OUTPUT, rules 12)
                Dm.output_to(owner, sound(Dm.resource("sound/effects/health/slowbeat.ogg"), repeat: TRUE, channel: CHANNEL_HEARTBEAT, volume: 40));
            }
        }
        else if (owner.stat == HARD_CRIT)
        {
            // tg: code/modules/surgery/organs/internal/heart/_heart.dm:146 (/datum/status_effect/jitter's procs are generated
            // stubs; nothing applies it yet, so has_status_effect answers null until the jitter is translated)
            if (beat != BEAT_FAST && Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_jitter))))
            {
                // SEND_SOUND(owner, sound(...)) (__DEFINES/sound.dm: DIRECT_OUTPUT, rules 12)
                Dm.output_to(owner, sound(Dm.resource("sound/effects/health/fastbeat.ogg"), repeat: TRUE, channel: CHANNEL_HEARTBEAT, volume: 40)); // tg: code/modules/surgery/organs/internal/heart/_heart.dm:147
                beat = BEAT_FAST;
            }
        }
        else if (beat != BEAT_NONE)
        {
            owner.stop_sound_channel(CHANNEL_HEARTBEAT); // tg: code/modules/surgery/organs/internal/heart/_heart.dm:152
            beat = BEAT_NONE;
        }
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:155
    public override object get_availability(datum_species owner_species = null, mob_living owner_mob = null)
    {
        return owner_species.mutantheart;
    }
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:158 (M6 slice 2: the self-check's organ lines)
public partial class obj_item_organ_heart : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:158
    public override object feel_for_damage(object self_aware = null)
    {
        if (Dm.truthy(owner.needs_heart()) && (!Dm.truthy(beating) || Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING))))
            return span_boldwarning(Dm.truthy(self_aware) ? I18n.T("Your heart is not beating!") : I18n.T("You don't feel your heart beating."));
        if (Dm.num(damage) < low_threshold)
            return "";
        if (Dm.num(damage) < high_threshold)
            return span_warning(Dm.truthy(self_aware) ? I18n.T("Your heart hurts.") : I18n.T("It hurts, and your heart rate feels irregular."));
        return span_boldwarning(Dm.truthy(self_aware) ? I18n.T("Your heart seriously hurts!") : I18n.T("It seriously hurts, and your heart rate is all over the place."));
    }
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:110 (interleaved types, rules 1.9; M8 slice D: the organ status in a health scan)
public partial class obj_item_organ_heart : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:110
    public override object get_status_text(object scanpower = null, object add_tooltips = null, float? colored = null)
    {
        if (Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_heart_attack))))
            return conditional_tooltip(I18n.T("<font color='#cc3333'>Myocardial Infarction</font>"), I18n.T("Apply defibrillation immediately. Similar electric shocks may work in emergencies."), add_tooltips);
        if (!Dm.truthy(beating) && !Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) && Dm.truthy(owner.needs_heart()) && owner.stat != DEAD)
            return conditional_tooltip(I18n.T("<font color='#cc3333'>Cardiac Arrest</font>"), I18n.T("Repair tissue damage and apply defibrillation immediately."), add_tooltips);
        return base.get_status_text(scanpower, add_tooltips, colored);
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:117
    public override object show_on_condensed_scans()
    {
        // Always show if the guy needs a heart (so its status can be monitored)
        object dot = base.show_on_condensed_scans();
        return Dm.truthy(dot) ? dot : owner.needs_heart();
    }
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:205  (silicons SI3: the exosuit fabricator prints it)
public partial class obj_item_organ_heart_cybernetic : obj_item_organ_heart
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:226
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        // Some effects are byassed if our owner (should it exist) doesn't need a heart
        bool owner_needs_us = Dm.truthy(owner?.needs_heart());

        // COOLDOWN_FINISHED(src, severe_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (owner_needs_us && !(Dm.num(severe_cooldown) <= world.time)) //So we cant just spam emp to kill people.
        {
            await owner.set_dizzy_if_lower(15 * SECONDS / Dm.num(severity));
            owner.losebreath += 10;
            severe_cooldown = world.time + 15 * SECONDS; // COOLDOWN_START(src, severe_cooldown, 15 SECONDS)
        }

        if (prob(emp_vulnerability / Dm.num(severity))) //Chance of permanent effects
        {
            organ_flags = Dm.bitor(organ_flags, ORGAN_EMP); //Starts organ faliure - gonna need replacing soon.
            Stop();
            _addtimer(Dm.@new<datum_callback>(this, nameof(Restart)), 10 * SECONDS / Dm.num(severity), file: "code/modules/surgery/organs/internal/heart/_heart.dm", line: 241);
            if (owner_needs_us)
            {
                owner.visible_message(
                    span_danger(I18n.T("[] clutches at [] chest as if [] heart is stopping!", owner, owner.p_their(), owner.p_their())),
                    span_userdanger(I18n.T("You feel a terrible pain in your chest, as if your heart has stopped!"))
                );
            }
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:248
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_EMP)))
            return;

        if (Dm.truthy(stabilization_available) && owner.health <= owner.crit_threshold)
            stabilize_heart();

        // Wound healing is intentionally tied to blood volume.
        if (Dm.truthy(bleed_prevention) && ishuman(owner) && Dm.lt(owner.get_blood_volume(), BLOOD_VOLUME_NORMAL))
        {
            mob_living_carbon_human wounded_owner = owner as mob_living_carbon_human;

            if (Dm.truthy(toxification_probability) && prob(toxification_probability))
                await wounded_owner.adjust_tox_loss(1 * Dm.num(seconds_per_tick), updating_health: FALSE);

            await wounded_owner.coagulant_effect(1 * Dm.num(seconds_per_tick));
        }
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:266
    public virtual void stabilize_heart()
    {
        ADD_TRAIT(owner, TRAIT_NOSOFTCRIT, ORGAN_TRAIT);
        stabilization_available = FALSE;

        _addtimer(TRAIT_CALLBACK_REMOVE(owner, TRAIT_NOSOFTCRIT, ORGAN_TRAIT), stabilization_duration, file: "code/modules/surgery/organs/internal/heart/_heart.dm", line: 270);

        // VARSET_CALLBACK(src, stabilization_available, TRUE): ___callbackvarset's datum branch for this var (as powernet.cs's flickering)
        _addtimer(Dm.@new<datum_callback>(this, nameof(stabilization_available__varset), TRUE), 5 * MINUTES, TIMER_DELETE_ME, file: "code/modules/surgery/organs/internal/heart/_heart.dm", line: 272);
    }

    /// VARSET_CALLBACK(src, stabilization_available, TRUE)'s target: ___callbackvarset's datum branch (varset_callback.dm:8-23) for this var.
    public virtual void stabilization_available__varset(object var_value = null)
    {
        stabilization_available = Dm.num(var_value);
    }

    // Largely a sanity check
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:275
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        if (HAS_TRAIT_FROM(organ_owner, TRAIT_NOSOFTCRIT, ORGAN_TRAIT))
            REMOVE_TRAIT(organ_owner, TRAIT_NOSOFTCRIT, ORGAN_TRAIT);
    }
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:61 (gaps OG1)
public partial class obj_item_organ_heart : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:61
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(beating))
        {
            user.visible_message(
                span_notice(I18n.T("[] squeezes [] to make it beat again!", user, this)),
                span_notice(I18n.T("You squeeze [] to make it beat again!", this)));
            Restart();
            _addtimer(Dm.@new<datum_callback>(this, nameof(stop_if_unowned)), 8 * SECONDS, file: "code/modules/surgery/organs/internal/heart/_heart.dm", line: 72);
            return TRUE;
        }
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:171 (gaps OG1)
public partial class obj_item_organ_heart_cursed : obj_item_organ_heart
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:184
    [DmParams("accursed", "user", "target")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living_carbon_human accursed = target_mob as mob_living_carbon_human;
        if (accursed == user && istype(accursed, typeof(mob_living_carbon_human)))
        {
            playsound(user, Dm.resource("sound/effects/singlebeat.ogg"), 40, TRUE);
            await user.temporarilyRemoveItemFromInventory(this, TRUE);
            await Insert(user as mob_living_carbon);
        }
        else
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        return null;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:192
    [DmParams("accursed")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);

        await organ_owner.AddComponent(typeof(datum_component_manual_heart), Dm.named("pump_delay", pump_delay), Dm.named("blood_loss", blood_loss), Dm.named("heal_brute", heal_brute), Dm.named("heal_burn", heal_burn), Dm.named("heal_oxy", heal_oxy));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:197
    [DmParams("accursed", "special", "movement_flags")]
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;
        await base.on_mob_remove(organ_owner, special, movement_flags);

        qdel(((datum)organ_owner).GetComponent(typeof(datum_component_manual_heart)));
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:202
    public override async DmTask<object> hear_beat_noise(mob_living hearer = null)
    {
        await DmTask.CompletedTask;
        return span_danger(beat_noise);
    }
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:304 (gaps OG1)
public partial class obj_item_organ_heart_cybernetic_surplus : obj_item_organ_heart_cybernetic
{
    //surplus organs are so awful that they explode when removed, unless failing
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:315
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dangerous_organ_removal), /*surgical = */ TRUE));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:319
    public override async DmTask<object> hear_beat_noise(mob_living hearer = null)
    {
        await DmTask.CompletedTask;
        return span_danger(I18n.T("[] heart produces [].", owner.p_Their(), beat_noise));
    }
}

// tg: code/modules/surgery/organs/internal/heart/_heart.dm:322 (gaps OG1)
public partial class obj_item_organ_heart_freedom : obj_item_organ_heart
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:330
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (Dm.num(owner.health) < 5 && adrenaline_cooldown <= world.time) // COOLDOWN_FINISHED, inline
        {
            adrenaline_cooldown = world.time + Dm.num(rand(25 * SECONDS, 1 * MINUTES)); // COOLDOWN_START, inline
            to_chat(owner, span_userdanger(I18n.T("You feel yourself dying, but you refuse to give up!")));
            await owner.heal_overall_damage(brute: 15, burn: 15, required_bodytype: BODYTYPE_ORGANIC);
            if (Dm.num(((datum_reagents)owner.reagents).get_reagent_amount(typeof(datum_reagent_medicine_ephedrine))) < 20)
                await ((datum_reagents)owner.reagents).add_reagent(typeof(datum_reagent_medicine_ephedrine), 10);
        }
    }
}

/// An improved version of the organic heart, with more health and more "keeping you alive" potential
// tg: code/modules/surgery/organs/internal/heart/_heart.dm:347 (gaps OG1)
public partial class obj_item_organ_heart_evolved : obj_item_organ_heart
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:358
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        if (prob(healing_probability * Dm.num(seconds_per_tick)))
        {
            float damage_to_heal = base_healing * ((maxHealth - Dm.num(damage)) / Dm.initial(this, (obj_item_organ_heart_evolved p) => p.maxHealth)) * Dm.num(seconds_per_tick);
            await owner.heal_overall_damage(damage_to_heal, damage_to_heal, required_bodytype: BODYTYPE_ORGANIC);

            if (owner.stat == HARD_CRIT && !Dm.truthy(owner.has_reagent(typeof(datum_reagent_medicine_atropine), 5)))
                await ((datum_reagents)owner.reagents).add_reagent(typeof(datum_reagent_medicine_atropine), 1 * Dm.num(seconds_per_tick));
        }
    }
}

/// A weaker evolved heart, but can block magic in exchange for our organs health!
// tg: code/modules/surgery/organs/internal/heart/_heart.dm:369 (gaps OG1)
public partial class obj_item_organ_heart_evolved_sacred : obj_item_organ_heart_evolved
{
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:381
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        if (Dm.truthy(IS_CULTIST(owner)))
            await ((datum_reagents)owner.reagents).add_reagent(typeof(datum_reagent_water_holywater), 5 * Dm.num(seconds_per_tick));
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:387
    [DmParams("receiver", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);

        await organ_owner.AddComponent(typeof(datum_component_anti_magic), Dm.named("block_magic", Dm.@new<datum_callback>(this, nameof(on_blocked))), Dm.named("check_blocking", Dm.@new<datum_callback>(this, nameof(check_block))));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:392
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);

        qdel(((datum)organ_owner).GetComponent(typeof(datum_component_anti_magic)));
    }

    /// When we blocked damage, do PAIN on us
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:398
    public virtual async DmTask on_blocked()
    {
        await apply_organ_damage(damage_per_block);
        await owner.vomit(VOMIT_CATEGORY_BLOOD);
        playsound(owner, Dm.resource("sound/effects/health/slowbeat.ogg"), 80);
    }

    /// We don't block magic if it would kill our heart
    // tg: code/modules/surgery/organs/internal/heart/_heart.dm:404
    public virtual object check_block()
    {
        if (maxHealth - Dm.num(damage) <= damage_per_block)
            return FALSE;
        return TRUE;
    }
}
