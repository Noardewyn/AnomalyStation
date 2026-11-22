// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/sign_language.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file (the sign language granter's reader): signing instead of speaking, with its say verbs, speech
// bubble, tonal overlays (tg's signlang states of icons/mob/effects/talk.dmi) and the hands it needs. The var block is generated (D111).

/**
* Reactive Sign Language Component for Carbons. Allows Carbons to speak with sign language if they have the relevant traits.
* Implements sign language by incrementally overriding several critical functions, variables, and argument lists.
*
* High-Level Theory of Operation:
*  1. Component is added to a Carbon via AddComponent.
*  2. Component grants sign language action to its parent, which adds and removes TRAIT_SIGN_LANG.
*  3. Component reacts to addition and removal of TRAIT_SIGN_LANG in parent:
*  4. If TRAIT_SIGN_LANG is added, then enable sign language. Listen for speech signals and modify the mob's speech, say_mod verbs, and typing indicator.
*  5. If TRAIT_SIGN_LANG is removed, then disable sign language. Unregister from speech signals and reset the mob's speech, say_mob verbs, and typing indicator.
*
* * Credits:
* - Original Tongue Tied created by @Wallemations (https://github.com/tgstation/tgstation/pull/52907)
* - Action sprite created by @Wallemations (icons/hud/actions.dmi:sign_language)
*/
// tg: code/datums/components/sign_language.dm:33
public partial class datum_component_sign_language : datum_component
{
    /// Defines used to determine whether a sign language user can sign or not, and if not, why they cannot.
    // tg: code/datums/components/sign_language.dm:2-9  (file-local, #undef at :334-341)
    private const float SIGN_OKAY = 0;
    private const float SIGN_ONE_HAND = 1;
    private const float SIGN_HANDS_FULL = 2;
    private const float SIGN_ARMLESS = 3;
    private const float SIGN_ARMS_DISABLED = 4;
    private const float SIGN_TRAIT_BLOCKED = 5;
    private const float SIGN_HANDS_COMPLETELY_RESTRAINED = 6;
    private const float SIGN_SLOWLY_FROM_CUFFS = 7;

    // Defines to determine the tone of the signer's message.
    // tg: code/datums/components/sign_language.dm:12-15  (file-local, #undef at :342-345)
    private const float TONE_NEUTRAL = 0; //! a statement
    private const float TONE_INQUISITIVE = 1; //! a question
    private const float TONE_EMPHATIC = 2; //! an exclamation
    private const float TONE_INQUISITIVE_EMPHATIC = 3; //! both a question and an exclamation (interrobang)

    /// Replace specific characters in the input string with periods.
    // tg: code/datums/components/sign_language.dm:44
    public virtual object sanitize_message(object input = null)
    {
        return replacetext(input, omissions, ".");
    }

    // tg: code/datums/components/sign_language.dm:47
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        // Non-Carbon mobs can't use sign language.
        if (!iscarbon(parent))
        {
            stack_trace(Dm.text("Sign Language component added to [] ([]) which is not a /mob/living/carbon subtype.", parent, parent?.GetType()), "code/datums/components/sign_language.dm", 50);
            return COMPONENT_INCOMPATIBLE;
        }
        linked_action = await Dm.new_async<datum_action_innate_sign_language>(this);
        return null;
    }

    // tg: code/datums/components/sign_language.dm:54
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref linked_action);
        return base.Destroy(force);
    }

    // tg: code/datums/components/sign_language.dm:58
    public override async DmTask RegisterWithParent()
    {
        // Sign language is toggled on/off via adding/removing TRAIT_SIGN_LANG.
        RegisterSignal(parent, SIGNAL_ADDTRAIT(TRAIT_SIGN_LANG), nameof(enable_sign_language));
        RegisterSignal(parent, SIGNAL_REMOVETRAIT(TRAIT_SIGN_LANG), nameof(disable_sign_language));
        await linked_action.Grant(parent as mob);
    }

    // tg: code/datums/components/sign_language.dm:64
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        disable_sign_language();
        UnregisterSignal(parent, Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_SIGN_LANG),
            SIGNAL_REMOVETRAIT(TRAIT_SIGN_LANG)));
        await DmTask.CompletedTask;
    }

    /// Signal handler for [COMSIG_SIGNLANGUAGE_ENABLE]
    /// Enables signing for the parent Carbon, stopping them from speaking vocally.
    /// This proc is only called directly after TRAIT_SIGN_LANG is added to the Carbon.
    // tg: code/datums/components/sign_language.dm:74
    [SignalHandler]
    public virtual object enable_sign_language()
    {
        mob_living_carbon carbon_parent = parent as mob_living_carbon;
        obj_item_organ_tongue tongue = carbon_parent.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (Dm.truthy(tongue))
            tongue.temp_say_mod = I18n.T("signs");
        //this speech relies on hands, which we have our own way of garbling speech when they're occupied, so we can have this always on
        ADD_TRAIT(carbon_parent, TRAIT_SPEAKS_CLEARLY, SPEAKING_FROM_HANDS);
        carbon_parent.verb_ask = I18n.T("signs");
        carbon_parent.verb_exclaim = I18n.T("signs");
        carbon_parent.verb_whisper = I18n.T("subtly signs");
        carbon_parent.verb_sing = I18n.T("rythmically signs");
        carbon_parent.verb_yell = I18n.T("emphatically signs");
        carbon_parent.bubble_icon = "signlang";
        RegisterSignal(carbon_parent, COMSIG_CARBON_GAIN_ORGAN, nameof(on_added_organ));
        RegisterSignal(carbon_parent, COMSIG_MOB_TRY_SPEECH, nameof(on_try_speech));
        RegisterSignal(carbon_parent, COMSIG_LIVING_TREAT_MESSAGE, nameof(on_treat_living_message));
        RegisterSignal(carbon_parent, COMSIG_MOVABLE_USING_RADIO, nameof(on_using_radio));
        RegisterSignal(carbon_parent, COMSIG_MOVABLE_SAY_QUOTE, nameof(on_say_quote));
        RegisterSignal(carbon_parent, COMSIG_MOB_SAY, nameof(on_say));
        RegisterSignal(carbon_parent, COMSIG_MOB_TRY_INVOKE_SPELL, nameof(can_cast_spell));
        return TRUE;
    }

    /// Signal handler for [COMSIG_SIGNLANGUAGE_DISABLE]
    /// Disables signing for the parent Carbon, allowing them to speak vocally.
    /// This proc is only called directly after TRAIT_SIGN_LANG is removed from the Carbon.
    // tg: code/datums/components/sign_language.dm:101
    [SignalHandler]
    public virtual object disable_sign_language()
    {
        mob_living_carbon carbon_parent = parent as mob_living_carbon;
        obj_item_organ_tongue tongue = carbon_parent.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (Dm.truthy(tongue))
            tongue.temp_say_mod = "";
        REMOVE_TRAIT(carbon_parent, TRAIT_SPEAKS_CLEARLY, SPEAKING_FROM_HANDS);
        carbon_parent.verb_ask = Dm.initial(carbon_parent, (mob_living_carbon p) => p.verb_ask);
        carbon_parent.verb_exclaim = Dm.initial(carbon_parent, (mob_living_carbon p) => p.verb_exclaim);
        carbon_parent.verb_whisper = Dm.initial(carbon_parent, (mob_living_carbon p) => p.verb_whisper);
        carbon_parent.verb_sing = Dm.initial(carbon_parent, (mob_living_carbon p) => p.verb_sing);
        carbon_parent.verb_yell = Dm.initial(carbon_parent, (mob_living_carbon p) => p.verb_yell);
        carbon_parent.bubble_icon = Dm.initial(carbon_parent, (mob_living_carbon p) => p.bubble_icon);
        UnregisterSignal(carbon_parent, Dm.list(
            COMSIG_CARBON_GAIN_ORGAN,
            COMSIG_MOB_TRY_SPEECH,
            COMSIG_LIVING_TREAT_MESSAGE,
            COMSIG_MOVABLE_USING_RADIO,
            COMSIG_MOVABLE_SAY_QUOTE,
            COMSIG_MOB_SAY,
            COMSIG_MOB_TRY_INVOKE_SPELL));
        return TRUE;
    }

    ///Signal proc for [COMSIG_CARBON_GAIN_ORGAN]
    ///Applies the new say mod to any tongues that have appeared!
    // tg: code/datums/components/sign_language.dm:128
    [SignalHandler]
    public virtual void on_added_organ(mob_living source = null, obj_item_organ new_organ = null)
    {
        if (!istype(new_organ, typeof(obj_item_organ_tongue)))
            return;
        obj_item_organ_tongue new_tongue = new_organ as obj_item_organ_tongue;
        new_tongue.temp_say_mod = I18n.T("signs");
    }

    /// Signal proc for [COMSIG_MOB_TRY_SPEECH]
    /// Sign languagers can always speak regardless of they're mute (as long as they're not mimes)
    // tg: code/datums/components/sign_language.dm:138
    [SignalHandler]
    public virtual object on_try_speech(mob_living source = null, object message = null, object ignore_spam = null, object forced = null)
    {
        mob_living_carbon carbon_parent = parent as mob_living_carbon;
        if (HAS_MIND_TRAIT(carbon_parent, TRAIT_MIMING))
        {
            to_chat(carbon_parent, span_green(I18n.T("You stop yourself from signing in favor of the artform of mimery!")));
            return COMPONENT_CANNOT_SPEAK;
        }

        float sw__1 = check_signables_state();
        if (sw__1 == SIGN_HANDS_FULL) // Full hands
        {
            carbon_parent.visible_message(I18n.T("tries to sign, but can't with [] hands full!", carbon_parent.p_their()), visible_message_flags: EMOTE_MESSAGE);
            return COMPONENT_CANNOT_SPEAK;
        }
        else if (sw__1 == SIGN_HANDS_COMPLETELY_RESTRAINED) // Restrained
        {
            carbon_parent.visible_message(I18n.T("tries to sign, but can't with [] hands bound!", carbon_parent.p_their()), visible_message_flags: EMOTE_MESSAGE);
            return COMPONENT_CANNOT_SPEAK;
        }
        // If we're handcuffed, we can still sign, but it's slow
        else if (sw__1 == SIGN_SLOWLY_FROM_CUFFS)
        {
            carbon_parent.visible_message(I18n.T("struggles, signing slowly with [] hands cuffed...", carbon_parent.p_their()), visible_message_flags: EMOTE_MESSAGE);
            return COMPONENT_IGNORE_CAN_SPEAK;
        }
        else if (sw__1 == SIGN_ARMLESS) // No arms
        {
            to_chat(carbon_parent, span_warning(I18n.T("You can't sign with no hands!")));
            return COMPONENT_CANNOT_SPEAK;
        }
        else if (sw__1 == SIGN_ARMS_DISABLED) // Arms but they're disabled
        {
            to_chat(carbon_parent, span_warning(I18n.T("You can't sign with your hands right now!")));
            return COMPONENT_CANNOT_SPEAK;
        }
        else if (sw__1 == SIGN_TRAIT_BLOCKED) // Hands blocked or emote mute
        {
            to_chat(carbon_parent, span_warning(I18n.T("You can't sign at the moment!")));
            return COMPONENT_CANNOT_SPEAK;
        }

        // Assuming none of the above fail, sign language users can speak
        // regardless of being muzzled or mute toxin'd or whatever.
        return COMPONENT_IGNORE_CAN_SPEAK;
    }

    /// Checks to see what state this person is in and if they are able to sign or not.
    // tg: code/datums/components/sign_language.dm:177
    public virtual float check_signables_state()
    {
        mob_living_carbon carbon_parent = parent as mob_living_carbon;
        // See how many hands we can actually use (this counts disabled / missing limbs for us)
        float total_hands = carbon_parent.usable_hands;
        // Look ma, no hands!
        if (total_hands <= 0)
        {
            // Either our hands are still attached (just disabled) or they're gone entirely
            return carbon_parent.num_hands > 0 ? SIGN_ARMS_DISABLED : SIGN_ARMLESS;
        }

        // Now let's see how many of our hands is holding something
        float busy_hands = 0;
        // Yes held_items can contain null values, which represents empty hands,
        // I'm just saving myself a variable cast by using as anything
        foreach (object held in Dm.each(carbon_parent.held_items))
        {
            obj_item held_item = held as obj_item;
            // items like slappers/zombie claws/etc. should be ignored
            if (held_item == null || Dm.truthy(Dm.bitand(held_item.item_flags, HAND_ITEM)))
                continue;

            busy_hands++;
        }

        // Handcuffed or otherwise restrained
        if (HAS_TRAIT(carbon_parent, TRAIT_RESTRAINED))
        {
            if (HAS_TRAIT_FROM_ONLY(carbon_parent, TRAIT_RESTRAINED, HANDCUFFED_TRAIT))
                return SIGN_SLOWLY_FROM_CUFFS;
            else
                return SIGN_HANDS_COMPLETELY_RESTRAINED;
        }
        // Some other trait preventing us from using our hands now
        else if (HAS_TRAIT(carbon_parent, TRAIT_HANDS_BLOCKED) || HAS_TRAIT(carbon_parent, TRAIT_EMOTEMUTE))
            return SIGN_TRAIT_BLOCKED;

        // Okay let's compare the total hands to the number of busy hands
        // to see how many we have left to use for signing right now
        float actually_usable_hands = total_hands - busy_hands;
        if (actually_usable_hands <= 0)
            return SIGN_HANDS_FULL;
        if (actually_usable_hands == 1)
            return SIGN_ONE_HAND;

        return SIGN_OKAY;
    }

    /**
     * Check if we can sign the given spell
     *
     * Checks to make sure the spell is not a mime spell, and that we are able to physically cast the spell.
     * Arguments:
     * * mob/living/carbon/source - the caster of the spell
     * * datum/action/cooldown/spell/spell - the spell we are trying to cast
     * * feedback - whether or not a message should be displayed in chat
     * *
     * * returns SPELL_INVOCATION_FAIL or SPELL_INVOCATION_SUCCESS
     */
    // tg: code/datums/components/sign_language.dm:228
    [SignalHandler]
    public virtual object can_cast_spell(mob_living_carbon source = null, datum_action_cooldown_spell spell = null, object feedback = null)
    {
        mob_living_carbon carbon_parent = parent as mob_living_carbon;
        if (Dm.eq(spell.invocation_type, INVOCATION_EMOTE)) // Mime spells are not cast with signs
            return NONE; // Run normal checks
        else if (check_signables_state() != SIGN_OKAY || HAS_MIND_TRAIT(carbon_parent, TRAIT_MIMING)) // Cannot cast if miming or not SIGN_OKAY
        {
            if (Dm.truthy(feedback))
                to_chat(carbon_parent, span_warning(I18n.T("You can't sign the words to invoke []!", spell)));
            return SPELL_INVOCATION_FAIL;
        }

        return SPELL_INVOCATION_ALWAYS_SUCCEED;
    }

    /// Signal proc for [COMSIG_LIVING_TREAT_MESSAGE]
    /// Changes our message based on conditions that limit or alter our ability to communicate
    // tg: code/datums/components/sign_language.dm:242
    [SignalHandler]
    public virtual void on_treat_living_message(atom_movable source = null, DmList message_args = null)
    {
        if (check_signables_state() == SIGN_ONE_HAND)
            message_args[TREAT_MESSAGE_ARG] = stars(message_args[TREAT_MESSAGE_ARG]);

        if (check_signables_state() == SIGN_SLOWLY_FROM_CUFFS)
            message_args[TREAT_MESSAGE_ARG] = stifled(message_args[TREAT_MESSAGE_ARG]);

        message_args[TREAT_TTS_MESSAGE_ARG] = "";
    }

    /// Signal proc for [COMSIG_MOVABLE_SAY_QUOTE]
    /// Removes exclamation/question marks.
    // tg: code/datums/components/sign_language.dm:255
    [SignalHandler]
    public virtual void on_say_quote(atom_movable source = null, DmList message_args = null)
    {
        message_args[MOVABLE_SAY_QUOTE_MESSAGE] = sanitize_message(message_args[MOVABLE_SAY_QUOTE_MESSAGE]);
    }

    /// Signal proc for [COMSIG_MOVABLE_USING_RADIO]
    /// Disallows us from speaking on comms if we don't have the special trait.
    // tg: code/datums/components/sign_language.dm:262
    [SignalHandler]
    public virtual object on_using_radio(atom_movable source = null, obj_item_radio radio = null)
    {
        return HAS_TRAIT(source, TRAIT_CAN_SIGN_ON_COMMS) ? NONE : COMPONENT_CANNOT_USE_RADIO;
    }

    /// Replaces emphatic punctuation with periods. Changes tonal indicator and emotes based on what is typed.
    // tg: code/datums/components/sign_language.dm:268
    [SignalHandler]
    public virtual void on_say(mob_living_carbon carbon_parent = null, DmList speech_args = null)
    {
        // The original message
        object message = speech_args[SPEECH_MESSAGE];
        // Is there a !
        float exclamation_found = findtext(message, "!");
        // Is there a ?
        float question_found = findtext(message, "?");
        float emote_tone = TONE_NEUTRAL;
        if (Dm.truthy(exclamation_found) && Dm.truthy(question_found))
            emote_tone = TONE_INQUISITIVE_EMPHATIC;
        else if (Dm.truthy(exclamation_found))
            emote_tone = TONE_EMPHATIC;
        else if (Dm.truthy(question_found))
            emote_tone = TONE_INQUISITIVE;

        // Cut our last overlay before we replace it
        if (Dm.gt(timeleft(tonal_timerid), 0))
        {
            remove_tonal_indicator();
            deltimer(tonal_timerid);
        }
        if (emote_tone == TONE_INQUISITIVE)
            tonal_indicator = mutable_appearance(Dm.resource("icons/mob/effects/talk.dmi"), "signlang1", TYPING_LAYER);
        else if (emote_tone == TONE_EMPHATIC)
            tonal_indicator = mutable_appearance(Dm.resource("icons/mob/effects/talk.dmi"), "signlang2", TYPING_LAYER);
        else if (emote_tone == TONE_INQUISITIVE_EMPHATIC)
            tonal_indicator = mutable_appearance(Dm.resource("icons/mob/effects/talk.dmi"), "signlang2", TYPING_LAYER);
        // If there's a tonal indicator
        if (tonal_indicator != null && Dm.truthy(carbon_parent.client?.typing_indicators))
        {
            carbon_parent.add_overlay(tonal_indicator);
            tonal_timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(remove_tonal_indicator)), 2.5f * SECONDS, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME)), file: "code/datums/components/sign_language.dm", line: 299);
        }
        else // If we're not gonna use it, just be sure we get rid of it
            tonal_indicator = null;

        // Only emote the tone if we have one and aren't already emoting the handcuffed message
        if (!Dm.truthy(carbon_parent.handcuffed) && Dm.truthy(emote_tone))
            this.emote_tone(carbon_parent, emote_tone);

        // remove the ! and ? symbols from message at the end
        message = sanitize_message(message);
        speech_args[SPEECH_MESSAGE] = message;
    }

    /// Send a visible message depending on the tone of the message that the sender is trying to convey to the world.
    // tg: code/datums/components/sign_language.dm:312
    public virtual void emote_tone(mob_living_carbon carbon_parent = null, object emote_tone = null)
    {
        if (ishuman(carbon_parent))
        {
            mob_living_carbon_human human_parent = carbon_parent as mob_living_carbon_human;
            if (Dm.truthy(human_parent.is_face_obscured()))
                return; // You can't see someone's expression if their face is obscured (or disfigured)
        }
        if (Dm.eq(emote_tone, TONE_INQUISITIVE))
            carbon_parent.visible_message(span_bold(I18n.T("quirks [] brows quizzically.", carbon_parent.p_their())), visible_message_flags: Dm.bitor(EMOTE_MESSAGE, BLOCK_SELF_HIGHLIGHT_MESSAGE));
        else if (Dm.eq(emote_tone, TONE_EMPHATIC))
            carbon_parent.visible_message(span_bold(I18n.T("widens [] eyes emphatically!", carbon_parent.p_their())), visible_message_flags: Dm.bitor(EMOTE_MESSAGE, BLOCK_SELF_HIGHLIGHT_MESSAGE));
        else if (Dm.eq(emote_tone, TONE_INQUISITIVE_EMPHATIC))
            carbon_parent.visible_message(span_bold(I18n.T("wears an intense, befuddled expression!")), visible_message_flags: Dm.bitor(EMOTE_MESSAGE, BLOCK_SELF_HIGHLIGHT_MESSAGE));
    }

    /// Removes the tonal indicator overlay completely
    // tg: code/datums/components/sign_language.dm:327
    public virtual void remove_tonal_indicator()
    {
        if (tonal_indicator == null)
            return;
        mob_living_carbon carbon_parent = parent as mob_living_carbon;
        carbon_parent.cut_overlay(tonal_indicator);
        tonal_indicator = null;
    }
}
