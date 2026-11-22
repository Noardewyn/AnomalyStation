// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/mob_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Speech verbs.

// M9 slice A (docs/ai/specs/social.md). The verbs are called by tg's say window (tgui_say's delegate_speech, protocol version 38) as
// /client/verb calls of the mob; QUEUE_OR_CALL_VERB_FOR(VERB_CALLBACK(...), SSspeech_controller) queues the speech for the next tick
// when a verb runs during BYOND's map send, else calls it: the port's verbs run in the host's command phase, so it is a detached
// call (as ClientSeam.Topic's). The IC word filters (is_ic_filtered, is_soft_ic_filtered) read config/word_filter.toml, which the
// port has none of: tg's filters then match nothing, so their branches are left out with a comment. Admin mutes, bans and the lag
// switch's slowmode are the admin tools (M10): none is ever set, so their checks pass. Dead chat (say_dead) is the ghosts beyond
// D151's scope (not-translated.md section 16).

public partial class datum_controller_global_vars
{
    // tg: code/modules/admin/verbs/mapping.dm:187  GLOBAL_VAR_INIT(say_disabled, FALSE) (an admin verb toggles it, M10; the speech
    // verbs read it, so it is declared here)
    public object say_disabled;

    // tg: code/modules/admin/verbs/mapping.dm:187
    public virtual void InitGlobalsay_disabled()
    {
        say_disabled = FALSE;
        Dm.append(ref gvars_datum_init_order, "say_disabled");
    }
}

public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob_say.dm:186  #define MESSAGE_MODS_LENGTH 7 (file-local, #undef at :240)
    ///The amount of items we are looking for in the message
    private const float MESSAGE_MODS_LENGTH = 7;

    ///what clients use to speak. when you type a message into the chat bar in say mode, this is the first thing that goes off serverside.
    // tg: code/modules/mob/mob_say.dm:4  GAME_VERB(/mob, say_verb, VERB_SAY, null): the body
    public virtual async DmTask __gvb_say_verb(DmList structured_args = null)
    {
        object message = structured_args["message"];
        if (Dm.truthy(GLOB.say_disabled)) //This is here to try to identify lag problems
        {
            to_chat(usr, span_danger(I18n.T("Speech is currently admin-disabled.")));
            return;
        }

        //queue this message because verbs are scheduled to process after SendMaps in the tick and speech is pretty expensive when it happens.
        //by queuing this for next tick the mc can compensate for its cost instead of having speech delay the start of the next tick
        if (Dm.truthy(message))
            Waiting.Detach(say(message));
    }

    // tg: code/modules/mob/mob_say.dm:4  the verb proc (as succumb's, living.dm:557): a detached call of the body; a caller without a
    // client does nothing; the one argument (VERB_ARG_SOURCE_INPUT) is the positional message.
    public virtual void say_verb(object message = null, params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_say_verb(Dm.list(Dm.kv("message", message))));
    }

    ///Whisper verb
    // tg: code/modules/mob/mob_say.dm:17  GAME_VERB(/mob, whisper_verb, VERB_WHISPER, null): the body
    public virtual async DmTask __gvb_whisper_verb(DmList structured_args = null)
    {
        object message = structured_args["message"];
        if (Dm.truthy(GLOB.say_disabled)) //This is here to try to identify lag problems
        {
            to_chat(usr, span_danger(I18n.T("Speech is currently admin-disabled.")));
            return;
        }

        if (Dm.truthy(message))
            Waiting.Detach(whisper(message));
    }

    // tg: code/modules/mob/mob_say.dm:17  the verb proc
    public virtual void whisper_verb(object message = null, params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_whisper_verb(Dm.list(Dm.kv("message", message))));
    }

    /**
     * Whisper a message.
     *
     * Basic level implementation just speaks the message, nothing else.
     */
    // tg: code/modules/mob/mob_say.dm:33
    public virtual async DmTask<object> whisper(object message = null, object bubble_type = null, DmList spans = null, float? sanitize = null, object language = null, float? ignore_spam = null, object forced = null, float? filterproof = null)
    {
        if (!Dm.truthy(message))
            return null;
        await say(message, language: language);
        return null;
    }

    ///The me emote verb
    // tg: code/modules/mob/mob_say.dm:39  GAME_VERB(/mob, me_verb, VERB_ME, null): the body
    public virtual async DmTask __gvb_me_verb(DmList structured_args = null)
    {
        object message = structured_args["message"];
        if (Dm.truthy(GLOB.say_disabled)) //This is here to try to identify lag problems
        {
            to_chat(usr, span_danger(I18n.T("Speech is currently admin-disabled.")));
            return;
        }

        message = trim(copytext_char(sanitize(message), 1, MAX_MESSAGE_LEN));

        Waiting.Detach(emote("me", NONE, message, TRUE));
    }

    // tg: code/modules/mob/mob_say.dm:39  the verb proc
    public virtual void me_verb(object message = null, params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_me_verb(Dm.list(Dm.kv("message", message))));
    }

    // tg: code/modules/mob/mob_say.dm:50
    public override async DmTask<object> try_speak(object message = null, float? ignore_spam = null, object forced = null, float? filterproof = null)
    {
        ignore_spam ??= FALSE;
        filterproof ??= FALSE;
        // tg: :51-77  the IC word filters (is_ic_filtered, is_soft_ic_filtered and their warnings, logs and soft alert): the port has no
        // config/word_filter.toml, so tg's filters match nothing and these branches never run (see the file comment).

        if (client != null && !(Dm.truthy(ignore_spam) || Dm.truthy(forced)))
        {
            // tg: :80  if(client.prefs.muted & MUTE_IC): the admin mutes are M10's admin tools; nobody is muted
            if (Dm.truthy(client.handle_spam_prevention(message, MUTE_IC)))
                return FALSE;
        }

        object sigreturn = !Dm.truthy(_listen_lookup?[COMSIG_MOB_TRY_SPEECH]) ? NONE : _SendSignal(COMSIG_MOB_TRY_SPEECH, Dm.list(this, message, ignore_spam, forced));
        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_IGNORE_CAN_SPEAK)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_CANNOT_SPEAK)))
            return FALSE;

        if (!Dm.truthy(await base.try_speak(message, ignore_spam, forced, filterproof))) // the can_speak check
        {
            if (HAS_MIND_TRAIT(this, TRAIT_MIMING))
                to_chat(this, span_green(I18n.T("Your vow of silence prevents you from speaking!")));
            else
                to_chat(this, span_warning(I18n.T("You find yourself unable to speak!")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/mob/mob_say.dm:100
    public override object can_speak__proc(float? allow_mimes = null)
    {
        allow_mimes ??= FALSE;
        if (!Dm.truthy(allow_mimes) && HAS_MIND_TRAIT(this, TRAIT_MIMING))
            return FALSE;

        return base.can_speak__proc(allow_mimes);
    }

    /**
     * say_dead
     * allows you to speak as a dead person
     * Args:
     * - message: The message you're sending to chat.
     * - mannequin_controller: If someone else is forcing you to speak, this is the mob doing it.
     */
    // tg: code/modules/mob/mob_say.dm:112
    public virtual object say_dead(object message = null, mob mannequin_controller = null)
    {
        object name = real_name;
        object alt_name = "";

        if (Dm.truthy(GLOB.say_disabled)) //This is here to try to identify lag problems
        {
            to_chat(usr, span_danger(I18n.T("Speech is currently admin-disabled.")));
            return null;
        }

        object jb = is_banned_from(mannequin_controller?.ckey ?? ckey, "Deadchat");
        if (QDELETED(this))
            return null;

        if (Dm.truthy(jb))
        {
            to_chat(this, span_danger(I18n.T("You have been banned from deadchat.")));
            return null;
        }

        if (client != null)
        {
            if (Dm.truthy(Dm.bitand(client.prefs.muted, MUTE_DEADCHAT)))
            {
                to_chat(this, span_danger(I18n.T("You cannot talk in deadchat (muted).")));
                return null;
            }

            // tg: :133-137  SSlag_switch's SLOWMODE_SAY: the lag switch is not declared (its measures are off)

            if (Dm.truthy(client.handle_spam_prevention(message, MUTE_DEADCHAT)))
                return null;
        }

        mob_dead_observer O = this as mob_dead_observer;
        if (Dm.truthy(isobserver(this)) && Dm.truthy(O.deadchat_name))
            name = Dm.text("[]", O.deadchat_name);
        else
        {
            if (Dm.truthy(mind?.name))
                name = Dm.text("[]", mind.name);
            else
                name = real_name;
            if (!Dm.eq(name, real_name))
                alt_name = Dm.text(" (died as [])", real_name);
        }

        object spanned = generate_messagepart(message);
        object source = Dm.text("<span class='game'><span class='prefix'>DEAD:</span> <span class='name'>[]</span>[]", name, alt_name);
        // tg: :155  emoji_parse(spanned): the chat emojis (not-translated.md section 70); the text is kept as typed
        object rendered = Dm.text(" <span class='message'>[]</span></span>", spanned);
        // tg: :156  log_talk(message, LOG_SAY, tag="DEAD"): the say log (the admin logs, M10+)
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_MOB_DEADSAY]) ? NONE : _SendSignal(COMSIG_MOB_DEADSAY, Dm.list(this, message)), MOB_DEADSAY_SIGNAL_INTERCEPT)))
            return null;
        object displayed_key = key;
        if (Dm.truthy(client?.holder?.fakekey))
            displayed_key = null;
        deadchat_broadcast(rendered, source, follow_target: this, speaker_key: displayed_key, original_message: message);
        return null;
    }

    ///Check if this message is an emote
    // tg: code/modules/mob/mob_say.dm:163
    public virtual object check_emote(object message = null, object forced = null)
    {
        if (Dm.textchar(message, 1) == "*")
        {
            Waiting.Detach(emote(Dm.copytext(message, Dm.length(Dm.textchar(message, 1)) + 1), intentional: Dm.truthy(forced) ? FALSE : TRUE));
            return TRUE;
        }
        return null;
    }

    ///Check if the mob has a hivemind channel
    // tg: code/modules/mob/mob_say.dm:169
    public virtual object hivecheck()
    {
        return FALSE;
    }

    // tg: code/modules/mob/mob_say.dm:175
    public virtual object check_for_custom_say_emote(object message = null, DmList mods = null)
    {
        float customsaypos = Dm.findtext(message, "*");
        if (customsaypos == 0)
            return message;
        // tg: :179  if (!isnull(ckey) && is_banned_from(ckey, "Emote")): the bans are M10's admin tools; nobody is banned
        mods[MODE_CUSTOM_SAY_EMOTE] = Dm.copytext(message, 1, customsaypos);
        message = Dm.copytext(message, customsaypos + 1);
        if (!Dm.truthy(message))
        {
            mods[MODE_CUSTOM_SAY_ERASE_INPUT] = TRUE;
            message = "an interesting thing to say";
        }
        return message;
    }

    /**
     * Extracts and cleans message of any extenstions at the begining of the message
     * Inserts the info into the passed list, returns the cleaned message
     *
     * Result can be
     * * SAY_MODE (Things like aliens, channels that aren't channels)
     * * MODE_WHISPER (Quiet speech)
     * * MODE_SING (Singing)
     * * MODE_HEADSET (Common radio channel)
     * * RADIO_EXTENSION the extension we're using (lots of values here)
     * * RADIO_KEY the radio key we're using, to make some things easier later (lots of values here)
     * * LANGUAGE_EXTENSION the language we're trying to use (lots of values here)
     */
    // tg: code/modules/mob/mob_say.dm:201
    public virtual object get_message_mods(object message = null, DmList mods = null)
    {
        for (float I = 1; I <= MESSAGE_MODS_LENGTH; I++)
        {
            // Prevents "...text" from being read as a radio message
            if (Dm.length(message) > 1 && Dm.textchar(message, 2) == Dm.textchar(message, 1))
                continue;

            string key = Dm.textchar(message, 1);
            float chop_to = 2; //By default we just take off the first char
            if (key == "#" && !Dm.truthy(mods[WHISPER_MODE]))
                mods[WHISPER_MODE] = MODE_WHISPER;
            else if (key == "%" && !Dm.truthy(mods[MODE_SING]))
                mods[MODE_SING] = TRUE;
            else if (key == ";" && !Dm.truthy(mods[MODE_HEADSET]))
            {
                if (!IS_UNCONSCIOUS_OR_CRIT(this)) //necessary indentation so it gets stripped of the semicolon anyway.
                    mods[MODE_HEADSET] = TRUE;
            }
            else if (Dm.@in(key, GLOB.department_radio_prefixes) && Dm.length(message) > Dm.length(key) + 1 && !Dm.truthy(mods[RADIO_EXTENSION]))
            {
                mods[RADIO_KEY] = LOWER_TEXT(Dm.textchar(message, 1 + Dm.length(key)));
                mods[RADIO_EXTENSION] = GLOB.department_radio_keys[mods[RADIO_KEY]];
                chop_to = Dm.length(key) + 2;
            }
            else if (key == "," && !Dm.truthy(mods[LANGUAGE_EXTENSION]))
            {
                foreach (object LD in Dm.each(GLOB.all_languages))
                {
                    object lang_key = Dm.initial(LD, (datum_language p) => p.key);
                    if (Dm.truthy(lang_key) && Dm.eq(lang_key, Dm.textchar(message, 1 + Dm.length(Dm.textchar(message, 1)))))
                    {
                        // No, you cannot speak in xenocommon just because you know the key
                        if (!Dm.truthy(can_speak_language(LD)))
                            return message;
                        mods[LANGUAGE_EXTENSION] = LD;
                        chop_to = Dm.length(key) + Dm.length(lang_key) + 1;
                    }
                }
                if (!Dm.truthy(mods[LANGUAGE_EXTENSION]))
                    return message;
            }
            else
                return message;
            message = trim_left(copytext_char(message, chop_to));
            if (!Dm.truthy(message))
                return null;
        }
        return message;
    }
}
