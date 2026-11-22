// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/severe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the pacifism came with gaps P1's pacification operation; the rest with the brain traumas, gaps B1, D327). The var
// blocks are generated (D111).

//Severe traumas, when your brain gets abused way too much.
//These range from very annoying to completely debilitating.
//They cannot be cured with chemicals, and require brain surgery to solve.

// tg: code/datums/brain_damage/severe.dm:5
[DmPath("/datum/brain_trauma/severe")]
public partial class datum_brain_trauma_severe : datum_brain_trauma
{
}

// tg: code/datums/brain_damage/severe.dm:9
public partial class datum_brain_trauma_severe_mute : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:17
    public override async DmTask<object> on_gain()
    {
        ADD_TRAIT(owner, TRAIT_MUTE, TRAUMA_TRAIT);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/severe.dm:21
    public override async DmTask on_lose(object silent = null)
    {
        REMOVE_TRAIT(owner, TRAIT_MUTE, TRAUMA_TRAIT);
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/severe.dm:25
public partial class datum_brain_trauma_severe_aphasia : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:33
    public override async DmTask<object> on_gain()
    {
        owner.add_blocked_language(Dm.sub(subtypesof(typeof(datum_language)), typeof(datum_language_aphasia)), source: LANGUAGE_APHASIA);
        owner.grant_language(typeof(datum_language_aphasia), source: LANGUAGE_APHASIA);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/severe.dm:38
    public override async DmTask on_lose(object silent = null)
    {
        if (!QDELING(owner))
        {
            owner.remove_blocked_language(subtypesof(typeof(datum_language)), source: LANGUAGE_APHASIA);
            owner.remove_language(typeof(datum_language_aphasia), source: LANGUAGE_APHASIA);
        }

        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/severe.dm:45
public partial class datum_brain_trauma_severe_blindness : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:53
    public override async DmTask<object> on_gain()
    {
        await owner.become_blind(TRAUMA_TRAIT);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/severe.dm:57
    public override async DmTask on_lose(object silent = null)
    {
        owner.cure_blind(TRAUMA_TRAIT);
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/severe.dm:61
public partial class datum_brain_trauma_severe_paralysis : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:72
    [DmParams("specific_type")]
    public override async DmTask New(params object[] __extra)
    {
        object specific_type = Dm.arg(__extra, 0);
        if (Dm.truthy(specific_type))
            paralysis_type = specific_type;
        if (!Dm.truthy(paralysis_type))
            paralysis_type = pick("full", "left", "right", "arms", "legs", "r_arm", "l_arm", "r_leg", "l_leg");
        object subject = null;
        switch (Dm.str(paralysis_type))
        {
            case "full":
                subject = I18n.T("your body");
                paralysis_traits = Dm.list(TRAIT_PARALYSIS_L_ARM, TRAIT_PARALYSIS_R_ARM, TRAIT_PARALYSIS_L_LEG, TRAIT_PARALYSIS_R_LEG);
                break;
            case "left":
                subject = I18n.T("the left side of your body");
                paralysis_traits = Dm.list(TRAIT_PARALYSIS_L_ARM, TRAIT_PARALYSIS_L_LEG);
                break;
            case "right":
                subject = I18n.T("the right side of your body");
                paralysis_traits = Dm.list(TRAIT_PARALYSIS_R_ARM, TRAIT_PARALYSIS_R_LEG);
                break;
            case "arms":
                subject = I18n.T("your arms");
                paralysis_traits = Dm.list(TRAIT_PARALYSIS_L_ARM, TRAIT_PARALYSIS_R_ARM);
                break;
            case "legs":
                subject = I18n.T("your legs");
                paralysis_traits = Dm.list(TRAIT_PARALYSIS_L_LEG, TRAIT_PARALYSIS_R_LEG);
                break;
            case "r_arm":
                subject = I18n.T("your right arm");
                paralysis_traits = Dm.list(TRAIT_PARALYSIS_R_ARM);
                break;
            case "l_arm":
                subject = I18n.T("your left arm");
                paralysis_traits = Dm.list(TRAIT_PARALYSIS_L_ARM);
                break;
            case "r_leg":
                subject = I18n.T("your right leg");
                paralysis_traits = Dm.list(TRAIT_PARALYSIS_R_LEG);
                break;
            case "l_leg":
                subject = I18n.T("your left leg");
                paralysis_traits = Dm.list(TRAIT_PARALYSIS_L_LEG);
                break;
        }

        gain_text = span_warning(I18n.T("You can't feel [] anymore!", subject));
        lose_text = span_notice(I18n.T("You can feel [] again!", subject));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/brain_damage/severe.dm:110
    public override async DmTask<object> on_gain()
    {
        object dot = await base.on_gain();
        foreach (object X in Dm.each(paralysis_traits))
            ADD_TRAIT(owner, X, TRAUMA_TRAIT);
        return dot;
    }

    // tg: code/datums/brain_damage/severe.dm:116
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        foreach (object X in Dm.each(paralysis_traits))
            REMOVE_TRAIT(owner, X, TRAUMA_TRAIT);
    }
}

// tg: code/datums/brain_damage/severe.dm:139
public partial class datum_brain_trauma_severe_narcolepsy : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:163  on_life's var/static/list/immunity_medicine (rules 10.6)
    private static ref DmList on_life__immunity_medicine => ref TgState.Current.datum_brain_trauma_severe_narcolepsy__on_life__immunity_medicine;

    // tg: code/datums/brain_damage/severe.dm:159
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (Dm.truthy(owner.IsSleeping()))
            return;

        /// If any of these are in the user's blood, return early
        //don't add too many, as most stimulant reagents already have a drowsy-removing effect
        foreach (object medicine in Dm.each(on_life__immunity_medicine))
        {
            if (Dm.truthy(((datum_reagents)owner.reagents).has_reagent(medicine)))
                return;
        }

        bool drowsy = owner.has_status_effect(typeof(datum_status_effect_drowsiness)) != null;
        bool caffeinated = HAS_TRAIT(owner, TRAIT_STIMULATED);
        float final_sleep_chance = sleep_chance;
        if (Dm.eq(owner.move_intent, MOVE_INTENT_RUN))
            final_sleep_chance += sleep_chance_running;
        if (drowsy)
            final_sleep_chance += sleep_chance_drowsy; //stack drowsy ontop of base or running odds with the += operator
        if (caffeinated)
            final_sleep_chance *= 0.5f; //make it harder to fall asleep on caffeine

        if (!SPT_PROB(final_sleep_chance, seconds_per_tick))
            return;

        //if not drowsy, don't fall asleep but make them drowsy
        if (!drowsy)
        {
            to_chat(owner, span_warning(I18n.T("You feel tired...")));
            await owner.adjust_drowsiness(rand(drowsy_time_minimum, drowsy_time_maximum));
            if (prob(50))
                await owner.emote("yawn");
            else if (prob(33)) //rarest message is a custom emote
                owner.visible_message(I18n.T("rubs [] eyes.", owner.p_their()), visible_message_flags: EMOTE_MESSAGE);
        }
        //drowsy, so fall asleep. you've had your chance to remedy it
        else
        {
            to_chat(owner, span_warning(I18n.T("You fall asleep.")));
            await owner.Sleeping(rand(sleep_time_minimum, sleep_time_maximum));
            if (prob(50) && Dm.truthy(owner.IsSleeping()))
                await owner.emote("snore");
        }
    }
}

// tg: code/datums/brain_damage/severe.dm:209
public partial class datum_brain_trauma_severe_monophobia : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:218
    public override async DmTask<object> on_gain()
    {
        object dot = await base.on_gain();
        await owner.AddComponentFrom(REF(this), typeof(datum_component_fearful), Dm.list(typeof(datum_terror_handler_vomiting), typeof(datum_terror_handler_simple_source_monophobia)));
        return dot;
    }

    // tg: code/datums/brain_damage/severe.dm:222
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        owner.RemoveComponentSource(REF(this), typeof(datum_component_fearful));
    }
}

// tg: code/datums/brain_damage/severe.dm:226
public partial class datum_brain_trauma_severe_discoordination : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:234
    public override async DmTask<object> on_gain()
    {
        object dot = await base.on_gain();
        await owner.apply_status_effect(typeof(datum_status_effect_discoordinated));
        return dot;
    }

    // tg: code/datums/brain_damage/severe.dm:238
    public override async DmTask on_lose(object silent = null)
    {
        owner.remove_status_effect(typeof(datum_status_effect_discoordinated));
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/severe.dm:242
public partial class datum_brain_trauma_severe_pacifism : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:251
    public override async DmTask<object> on_gain()
    {
        ADD_TRAIT(owner, TRAIT_PACIFISM, TRAUMA_TRAIT);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/severe.dm:255
    public override async DmTask on_lose(object silent = null)
    {
        REMOVE_TRAIT(owner, TRAIT_PACIFISM, TRAUMA_TRAIT);
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/severe.dm:259
public partial class datum_brain_trauma_severe_hypnotic_stupor : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:268
    public override async DmTask on_lose(object silent = null) //hypnosis must be cleared separately, but brain surgery should get rid of both anyway
    {
        await base.on_lose(silent);
        owner.remove_status_effect(typeof(datum_status_effect_trance));
    }

    // tg: code/datums/brain_damage/severe.dm:272
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (SPT_PROB(0.5f, seconds_per_tick) && owner.has_status_effect(typeof(datum_status_effect_trance)) == null)
            await owner.apply_status_effect(typeof(datum_status_effect_trance), rand(100, 300), FALSE);
    }
}

// tg: code/datums/brain_damage/severe.dm:277
public partial class datum_brain_trauma_severe_hypnotic_trigger : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:287
    [DmParams("phrase")]
    public override async DmTask New(params object[] __extra)
    {
        object phrase = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (Dm.truthy(phrase))
            trigger_phrase = Dm.str(phrase);
    }

    // tg: code/datums/brain_damage/severe.dm:292
    public override async DmTask on_lose(object silent = null) //hypnosis must be cleared separately, but brain surgery should get rid of both anyway
    {
        await base.on_lose(silent);
        owner.remove_status_effect(typeof(datum_status_effect_trance));
    }

    // tg: code/datums/brain_damage/severe.dm:296
    public override void handle_hearing(datum source = null, DmList hearing_args = null)
    {
        if (HAS_TRAIT(owner, TRAIT_DEAF) || owner == hearing_args[HEARING_SPEAKER])
            return;

        regex reg = Dm.@new<regex>(Dm.text("(\\b[]\\b)", REGEX_QUOTE(trigger_phrase)), "ig");

        if (Dm.truthy(findtext(hearing_args[HEARING_RAW_MESSAGE], reg)))
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(hypnotrigger)), 1 * SECONDS, file: "code/datums/brain_damage/severe.dm", line: 303); //to react AFTER the chat message
            hearing_args[HEARING_RAW_MESSAGE] = reg.Replace(hearing_args[HEARING_RAW_MESSAGE], span_hypnophrase("*********"));
        }
    }

    // tg: code/datums/brain_damage/severe.dm:306
    public virtual async DmTask hypnotrigger()
    {
        to_chat(owner, span_warning(I18n.T("The words trigger something deep within you, and you feel your consciousness slipping away...")));
        await owner.apply_status_effect(typeof(datum_status_effect_trance), rand(100, 300), FALSE);
    }
}

// tg: code/datums/brain_damage/severe.dm:310
public partial class datum_brain_trauma_severe_dyslexia : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:319
    public override async DmTask<object> on_gain()
    {
        ADD_TRAIT(owner, TRAIT_ILLITERATE, TRAUMA_TRAIT);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/severe.dm:323
    public override async DmTask on_lose(object silent = null)
    {
        REMOVE_TRAIT(owner, TRAIT_ILLITERATE, TRAUMA_TRAIT);
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/severe.dm:327
public partial class datum_brain_trauma_severe_kleptomaniac : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/severe.dm:338
    public override async DmTask<object> on_gain()
    {
        object dot = await base.on_gain();
        RegisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE, nameof(damage_taken));
        return dot;
    }

    // tg: code/datums/brain_damage/severe.dm:342
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        UnregisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE);
    }

    // tg: code/datums/brain_damage/severe.dm:346
    [SignalHandler]
    public virtual void damage_taken(datum source = null, object damage_amount = null, object damage_type = null, params object[] __extra)
    {
        // While you're fighting someone (or dying horribly) your mind has more important things to focus on than pocketing stuff
        if (Dm.num(damage_amount) >= 5 && (Dm.eq(damage_type, BRUTE) || Dm.eq(damage_type, BURN) || Dm.eq(damage_type, STAMINA)))
            steal_cd = world.time + 12 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
    }

    // tg: code/datums/brain_damage/severe.dm:352
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (Dm.num(owner.usable_hands) <= 0)
            return;
        if (!SPT_PROB(5, seconds_per_tick))
            return;
        if (!(steal_cd <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return;
        if (!Dm.truthy(owner.has_active_hand()) || !Dm.truthy(owner.get_empty_held_indexes()))
            return;

        // If our main hand is full, that means our offhand is empty, so try stealing with that
        bool steal_to_offhand = owner.get_active_held_item() != null;
        object curr_index = owner.active_hand_index;
        object pre_dir = owner.dir;
        if (steal_to_offhand)
            owner.swap_hand(owner.get_inactive_hand_index());

        DmList stealables = Dm.list();
        foreach (obj_item potential_stealable in Dm.each<obj_item>(oview(1, owner)))
        {
            if (Dm.num(potential_stealable.w_class) >= WEIGHT_CLASS_BULKY)
                continue;
            if (Dm.truthy(potential_stealable.anchored) || !Dm.truthy(Dm.bitand(potential_stealable.interaction_flags_item, INTERACT_ITEM_ATTACK_HAND_PICKUP)))
                continue;
            Dm.append(stealables, potential_stealable);
        }

        foreach (obj_item stealable in Dm.each_cast<obj_item>(shuffle(stealables)))
        {
            if (!Dm.truthy(await stealable.IsReachableBy(owner)) || Dm.truthy(stealable.IsObscured()))
                continue;
            // Try to do a raw click on the item with one of our empty hands, to pick it up (duh)
            owner.log_message("attempted to pick up (kleptomania)", LOG_ATTACK, color: "orange");
            await owner.ClickOn(stealable);
            // No feedback message. Intentional, you may not even realize you picked up something
            break;
        }

        if (steal_to_offhand)
            owner.swap_hand(curr_index);
        await owner.setDir(pre_dir);
        // Gives you a small buffer - not to avoid spam, but to make it more subtle / less predictable
        steal_cd = world.time + 8 * SECONDS; // COOLDOWN_START, inline
    }
}

public sealed partial class TgState
{
    // tg: code/datums/brain_damage/severe.dm:163  /datum/brain_trauma/severe/narcolepsy/on_life's var/static/list/immunity_medicine
    internal DmList datum_brain_trauma_severe_narcolepsy__on_life__immunity_medicine = Dm.list(
        typeof(datum_reagent_medicine_modafinil),
        typeof(datum_reagent_medicine_synaptizine));
}
