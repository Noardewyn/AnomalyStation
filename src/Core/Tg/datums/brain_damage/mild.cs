// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/mild.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the concussion came with the blunt head trauma, item_attack.dm:442; the rest with the brain traumas, gaps B1,
// D327). The var blocks are generated (D111).

//Mild traumas are the most common; they are generally minor annoyances.
//They can be cured with mannitol and patience, although brain surgery still works.
//Most of the old brain damage effects have been transferred to the dumbness trauma.

// tg: code/datums/brain_damage/mild.dm:5
[DmPath("/datum/brain_trauma/mild")]
public partial class datum_brain_trauma_mild : datum_brain_trauma
{
}

// tg: code/datums/brain_damage/mild.dm:8
public partial class datum_brain_trauma_mild_hallucinations : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:19
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (IS_UNCONSCIOUS(owner))
            return;
        if (HAS_TRAIT(owner, TRAIT_RDS_SUPPRESSED))
        {
            owner.remove_language(typeof(datum_language_aphasia), source: LANGUAGE_APHASIA);
            await owner.adjust_hallucinations(-10 * SECONDS * Dm.num(seconds_per_tick));
            return;
        }

        owner.grant_language(typeof(datum_language_aphasia), source: LANGUAGE_APHASIA);
        await owner.adjust_hallucinations_up_to(((Dm.truthy(uncapped) ? 12 * SECONDS : 5 * SECONDS) * Dm.num(seconds_per_tick)), (Dm.truthy(uncapped) ? 240 * SECONDS : 60 * SECONDS));
    }

    // tg: code/datums/brain_damage/mild.dm:30
    public override async DmTask on_lose(object silent = null)
    {
        owner.remove_status_effect(typeof(datum_status_effect_hallucination));
        if (!QDELING(owner))
            owner.remove_language(typeof(datum_language_aphasia), source: LANGUAGE_APHASIA);
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/mild.dm:36
public partial class datum_brain_trauma_mild_stuttering : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:44
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await owner.adjust_stutter_up_to(5 * SECONDS * Dm.num(seconds_per_tick), 50 * SECONDS);
    }

    // tg: code/datums/brain_damage/mild.dm:47
    public override async DmTask on_lose(object silent = null)
    {
        owner.remove_status_effect(typeof(datum_status_effect_speech_stutter));
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/mild.dm:51
public partial class datum_brain_trauma_mild_dumbness : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:59
    public override async DmTask<object> on_gain()
    {
        ADD_TRAIT(owner, TRAIT_DUMB, TRAUMA_TRAIT);
        await owner.add_mood_event("dumb", typeof(datum_mood_event_oblivious));
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/mild.dm:64
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await owner.adjust_derpspeech_up_to(5 * SECONDS * Dm.num(seconds_per_tick), 50 * SECONDS);
        if (SPT_PROB(1.5f, seconds_per_tick))
            await owner.emote("drool");
        else if (!IS_UNCONSCIOUS_OR_CRIT(owner) && SPT_PROB(1.5f, seconds_per_tick))
            await owner.say(pick_list_replacements(BRAIN_DAMAGE_FILE, "brain_damage"), forced: "brain damage", filterproof: TRUE);
    }

    // tg: code/datums/brain_damage/mild.dm:71
    public override async DmTask on_lose(object silent = null)
    {
        REMOVE_TRAIT(owner, TRAIT_DUMB, TRAUMA_TRAIT);
        owner.remove_status_effect(typeof(datum_status_effect_speech_stutter_derpspeech));
        owner.clear_mood_event("dumb");
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/mild.dm:77
public partial class datum_brain_trauma_mild_speech_impediment : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:85
    public override async DmTask<object> on_gain()
    {
        ADD_TRAIT(owner, TRAIT_UNINTELLIGIBLE_SPEECH, TRAUMA_TRAIT);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/mild.dm:89
    public override async DmTask on_lose(object silent = null)
    {
        REMOVE_TRAIT(owner, TRAIT_UNINTELLIGIBLE_SPEECH, TRAUMA_TRAIT);
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/mild.dm:93
public partial class datum_brain_trauma_mild_concussion : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:101
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB(2.5f, seconds_per_tick))
        {
            switch (rand(1, 11))
            {
                case 1:
                    await owner.vomit(VOMIT_CATEGORY_DEFAULT);
                    break;
                case 2 or 3:
                    await owner.adjust_dizzy(20 * SECONDS);
                    break;
                case 4 or 5:
                    await owner.adjust_confusion(10 * SECONDS);
                    await owner.set_eye_blur_if_lower(20 * SECONDS);
                    break;
                case >= 6 and <= 9:
                    await owner.adjust_slurring(1 * MINUTES);
                    break;
                case 10:
                    to_chat(owner, span_notice(I18n.T("You forget for a moment what you were doing.")));
                    await owner.Stun(20);
                    break;
                case 11:
                    to_chat(owner, span_warning(I18n.T("You faint.")));
                    await owner.Unconscious(80);
                    break;
            }
        }

        await base.on_life(seconds_per_tick);
    }
}

// tg: code/datums/brain_damage/mild.dm:122
public partial class datum_brain_trauma_mild_healthy : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:131
    public override async DmTask<object> on_gain()
    {
        await owner.apply_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy), GetType());
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/mild.dm:135
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await owner.adjust_stamina_loss(-6 * Dm.num(seconds_per_tick)); //no pain, no fatigue
    }

    // tg: code/datums/brain_damage/mild.dm:138
    public override async DmTask on_lose(object silent = null)
    {
        owner.remove_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy), GetType());
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/mild.dm:142
public partial class datum_brain_trauma_mild_muscle_weakness : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:150
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        float fall_chance = 1;
        if (Dm.eq(owner.move_intent, MOVE_INTENT_RUN))
            fall_chance += 2;
        if (SPT_PROB(0.5f * fall_chance, seconds_per_tick) && owner.body_position == STANDING_UP)
        {
            to_chat(owner, span_warning(I18n.T("Your leg gives out!")));
            await owner.Paralyze(35);
        }

        else if (owner.get_active_held_item() != null)
        {
            float drop_chance = 1;
            obj_item I = owner.get_active_held_item() as obj_item;
            drop_chance += Dm.num(I.w_class);
            if (SPT_PROB(0.5f * drop_chance, seconds_per_tick) && Dm.truthy(await owner.dropItemToGround(I)))
                to_chat(owner, span_warning(I18n.T("You drop []!", I)));
        }

        else if (SPT_PROB(1.5f, seconds_per_tick))
        {
            to_chat(owner, span_warning(I18n.T("You feel a sudden weakness in your muscles!")));
            await owner.adjust_stamina_loss(50);
        }
        await base.on_life(seconds_per_tick);
    }
}

// tg: code/datums/brain_damage/mild.dm:170
public partial class datum_brain_trauma_mild_muscle_spasms : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:178
    public override async DmTask<object> on_gain()
    {
        await owner.apply_status_effect(typeof(datum_status_effect_spasms));
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/mild.dm:182
    public override async DmTask on_lose(object silent = null)
    {
        owner.remove_status_effect(typeof(datum_status_effect_spasms));
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/mild.dm:186
public partial class datum_brain_trauma_mild_nervous_cough : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:194
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB(6, seconds_per_tick) && !HAS_TRAIT(owner, TRAIT_SOOTHED_THROAT))
        {
            if (prob(5))
            {
                to_chat(owner, span_warning(Dm.text("[]", pick(I18n.T("You have a coughing fit!"), I18n.T("You can't stop coughing!")))));
                await owner.Immobilize(20);
                await owner.emote("cough");
                _addtimer(Dm.@new<datum_callback>(owner, nameof(mob.emote), "cough"), 0.6f * SECONDS, file: "code/datums/brain_damage/mild.dm", line: 200);
                _addtimer(Dm.@new<datum_callback>(owner, nameof(mob.emote), "cough"), 1.2f * SECONDS, file: "code/datums/brain_damage/mild.dm", line: 201);
            }
            await owner.emote("cough");
        }
        await base.on_life(seconds_per_tick);
    }
}

// tg: code/datums/brain_damage/mild.dm:205
public partial class datum_brain_trauma_mild_expressive_aphasia : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:213
    public override void handle_speech(datum source = null, DmList speech_args = null)
    {
        object message = speech_args[SPEECH_MESSAGE];
        if (Dm.truthy(message))
        {
            DmList message_split = splittext(message, " ");
            DmList new_message = Dm.list();

            foreach (object word__it in Dm.each(message_split))
            {
                object word = word__it;
                string suffix = "";
                float suffix_foundon = 0;
                foreach (object potential_suffix in Dm.each(Dm.list(".", ",", ";", "!", ":", "?")))
                {
                    suffix_foundon = findtext(word, potential_suffix, -length(potential_suffix));
                    if (Dm.truthy(suffix_foundon))
                    {
                        suffix = (string)potential_suffix;
                        break;
                    }
                }

                if (Dm.truthy(suffix_foundon))
                    word = copytext(word, 1, suffix_foundon);
                word = html_decode(word);

                if (Dm.truthy(GLOB.most_common_words_alphabetical[LOWER_TEXT(word)]))
                    Dm.append(new_message, Dm.add(word, suffix));
                else
                {
                    if (prob(30) && message_split.len > 2)
                    {
                        Dm.append(new_message, pick("uh", "erm"));
                        break;
                    }
                    else
                    {
                        DmList charlist = text2charlist(word) as DmList;
                        charlist.len = round(charlist.len * 0.5f, 1);
                        shuffle_inplace(charlist);
                        Dm.append(new_message, Dm.add(jointext(charlist, ""), suffix));
                    }
                }
            }

            message = jointext(new_message, " ");
        }

        speech_args[SPEECH_MESSAGE] = trim(message);
    }
}

// tg: code/datums/brain_damage/mild.dm:248
public partial class datum_brain_trauma_mild_mind_echo : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:264  handle_hearing's var/static/regex/quoted_spoken_message (rules 10.6)
    private static ref regex handle_hearing__quoted_spoken_message => ref TgState.Current.datum_brain_trauma_mild_mind_echo__handle_hearing__quoted_spoken_message;

    // tg: code/datums/brain_damage/mild.dm:258
    public override void handle_hearing(datum source = null, DmList hearing_args = null)
    {
        if (HAS_TRAIT(owner, TRAIT_DEAF) || owner == hearing_args[HEARING_SPEAKER])
            return;

        if (hear_dejavu.len >= 5)
        {
            if (prob(25))
            {
                object deja_vu = pick_n_take(hear_dejavu);
                handle_hearing__quoted_spoken_message ??= Dm.regex("\".+\"", "gi");
                hearing_args[HEARING_RAW_MESSAGE] = handle_hearing__quoted_spoken_message.Replace(hearing_args[HEARING_RAW_MESSAGE], Dm.text("\"[]\"", deja_vu)); //Quotes included to avoid cases where someone says part of their name
                return;
            }
        }
        if (hear_dejavu.len >= 15)
        {
            if (prob(50))
            {
                popleft(hear_dejavu); //Remove the oldest
                Dm.append(hear_dejavu, hearing_args[HEARING_RAW_MESSAGE]);
            }
        }
        else
            Dm.append(hear_dejavu, hearing_args[HEARING_RAW_MESSAGE]);
    }

    // tg: code/datums/brain_damage/mild.dm:275
    public override void handle_speech(datum source = null, DmList speech_args = null)
    {
        if (speak_dejavu.len >= 5)
        {
            if (prob(25))
            {
                object deja_vu = pick_n_take(speak_dejavu);
                speech_args[SPEECH_MESSAGE] = deja_vu;
                return;
            }
        }
        if (speak_dejavu.len >= 15)
        {
            if (prob(50))
            {
                popleft(speak_dejavu); //Remove the oldest
                Dm.append(speak_dejavu, speech_args[SPEECH_MESSAGE]);
            }
        }
        else
            Dm.append(speak_dejavu, speech_args[SPEECH_MESSAGE]);
    }
}

// tg: code/datums/brain_damage/mild.dm:288
public partial class datum_brain_trauma_mild_color_blindness : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:296
    public override async DmTask<object> on_gain()
    {
        owner.add_client_colour(typeof(datum_client_colour_monochrome), TRAUMA_TRAIT);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/mild.dm:300
    public override async DmTask on_lose(object silent = null)
    {
        owner.remove_client_colour(TRAUMA_TRAIT);
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/mild.dm:304
public partial class datum_brain_trauma_mild_possessive : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/mild.dm:313
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        foreach (obj_item thing in Dm.each<obj_item>(owner.held_items))
            clear_trait(thing);
    }

    // tg: code/datums/brain_damage/mild.dm:318
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (!SPT_PROB(5, seconds_per_tick))
            return;

        obj_item my_thing = pick(owner.held_items) as obj_item; // can pick null, that's fine
        if (my_thing == null || HAS_TRAIT(my_thing, TRAIT_NODROP) || Dm.truthy(Dm.bitand(my_thing.item_flags, Dm.bitor(HAND_ITEM, ABSTRACT))))
            return;

        ADD_TRAIT(my_thing, TRAIT_NODROP, TRAUMA_TRAIT);
        RegisterSignals(my_thing, Dm.list(COMSIG_ITEM_DROPPED, COMSIG_MOVABLE_MOVED), nameof(clear_trait));
        to_chat(owner, span_warning(I18n.T("You feel a need to keep [] close...", my_thing)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(relax), my_thing), rand(30 * SECONDS, 3 * MINUTES), TIMER_DELETE_ME, file: "code/datums/brain_damage/mild.dm", line: 329);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/brain_damage/mild.dm:331
    public virtual void relax(obj_item my_thing = null)
    {
        if (QDELETED(my_thing))
            return;
        if (HAS_TRAIT_FROM_ONLY(my_thing, TRAIT_NODROP, TRAUMA_TRAIT)) // in case something else adds nodrop, somehow?
            to_chat(owner, span_notice(I18n.T("You feel more comfortable letting go of [].", my_thing)));
        clear_trait(my_thing);
    }

    // tg: code/datums/brain_damage/mild.dm:338
    [SignalHandler]
    public virtual void clear_trait(obj_item my_thing = null, params object[] __extra)
    {
        REMOVE_TRAIT(my_thing, TRAIT_NODROP, TRAUMA_TRAIT);
        UnregisterSignal(my_thing, Dm.list(COMSIG_ITEM_DROPPED, COMSIG_MOVABLE_MOVED));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/brain_damage/mild.dm:264  /datum/brain_trauma/mild/mind_echo/handle_hearing's var/static/regex/quoted_spoken_message
    internal regex datum_brain_trauma_mild_mind_echo__handle_hearing__quoted_spoken_message;
}
