// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/living_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A (docs/ai/specs/social.md); text to speech since D307, its radio and filters since D308. Left out with a comment: the lag switch's slowmode and runechat switch (the admin tools, M10: never on), the say log (log_sayverb_talk, the admin
// logs, M10). The say modes of SSradio (hiveminds, binary) are the antagonists' and the silicons' (not-translated.md 73). D253: a shouted line (one
// ending in "!!", tg's yell) carries farther than a said one (SHOUT_RANGE, DV162), because the 3D world is seen and heard farther.

public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/living_say.dm:1  GLOBAL_LIST_INIT(department_radio_prefixes, list(":", "."))
    public DmList department_radio_prefixes;

    // tg: code/modules/mob/living/living_say.dm:1
    public virtual void InitGlobaldepartment_radio_prefixes()
    {
        department_radio_prefixes = Dm.list(":", ".");
        Dm.append(ref gvars_datum_init_order, "department_radio_prefixes");
    }

    // tg: code/modules/mob/living/living_say.dm:3  GLOBAL_LIST_INIT(department_radio_keys, list(...))
    public DmList department_radio_keys;

    // tg: code/modules/mob/living/living_say.dm:3
    public virtual void InitGlobaldepartment_radio_keys()
    {
        department_radio_keys = Dm.list(
            // Location
            Dm.kv(MODE_KEY_R_HAND, MODE_R_HAND),
            Dm.kv(MODE_KEY_L_HAND, MODE_L_HAND),
            Dm.kv(MODE_KEY_INTERCOM, MODE_INTERCOM),

            // Department
            Dm.kv(MODE_KEY_DEPARTMENT, MODE_DEPARTMENT),
            Dm.kv(RADIO_KEY_COMMAND, RADIO_CHANNEL_COMMAND),
            Dm.kv(RADIO_KEY_SCIENCE, RADIO_CHANNEL_SCIENCE),
            Dm.kv(RADIO_KEY_MEDICAL, RADIO_CHANNEL_MEDICAL),
            Dm.kv(RADIO_KEY_ENGINEERING, RADIO_CHANNEL_ENGINEERING),
            Dm.kv(RADIO_KEY_SECURITY, RADIO_CHANNEL_SECURITY),
            Dm.kv(RADIO_KEY_SUPPLY, RADIO_CHANNEL_SUPPLY),
            Dm.kv(RADIO_KEY_SERVICE, RADIO_CHANNEL_SERVICE),

            // Faction
            Dm.kv(RADIO_KEY_SYNDICATE, RADIO_CHANNEL_SYNDICATE),
            Dm.kv(RADIO_KEY_UPLINK, RADIO_CHANNEL_UPLINK),
            Dm.kv(RADIO_KEY_CENTCOM, RADIO_CHANNEL_CENTCOM),

            // Admin
            Dm.kv(MODE_KEY_ADMIN, MODE_ADMIN),
            Dm.kv(MODE_KEY_DEADMIN, MODE_DEADMIN),
            Dm.kv(MODE_KEY_PUPPET, MODE_PUPPET),

            // Misc
            Dm.kv(RADIO_KEY_AI_PRIVATE, RADIO_CHANNEL_AI_PRIVATE), // AI Upload channel
            Dm.kv(RADIO_KEY_ENTERTAINMENT, RADIO_CHANNEL_ENTERTAINMENT), // Entertainment monitors

            //kinda localization -- rastaf0
            //same keys as above, but on russian keyboard layout.
            // Location
            Dm.kv("к", MODE_R_HAND),
            Dm.kv("л", MODE_L_HAND),
            Dm.kv("ш", MODE_INTERCOM),

            // Department
            Dm.kv("р", MODE_DEPARTMENT),
            Dm.kv("с", RADIO_CHANNEL_COMMAND),
            Dm.kv("т", RADIO_CHANNEL_SCIENCE),
            Dm.kv("ь", RADIO_CHANNEL_MEDICAL),
            Dm.kv("у", RADIO_CHANNEL_ENGINEERING),
            Dm.kv("ы", RADIO_CHANNEL_SECURITY),
            Dm.kv("г", RADIO_CHANNEL_SUPPLY),
            Dm.kv("м", RADIO_CHANNEL_SERVICE),

            // Faction
            Dm.kv("е", RADIO_CHANNEL_SYNDICATE),
            Dm.kv("н", RADIO_CHANNEL_CENTCOM),

            // Admin
            Dm.kv("з", MODE_ADMIN),
            Dm.kv("в", MODE_KEY_DEADMIN),

            // Misc
            Dm.kv("щ", RADIO_CHANNEL_AI_PRIVATE),
            Dm.kv("з", RADIO_CHANNEL_ENTERTAINMENT));
        Dm.append(ref gvars_datum_init_order, "department_radio_keys");
    }

    /**
     * Whitelist of saymodes or radio extensions that can be spoken through even if not fully conscious.
     * Associated values are their maximum allowed mob stats.
     */
    // tg: code/modules/mob/living/living_say.dm:67  GLOBAL_LIST_INIT(message_modes_stat_limits, list(...))
    public DmList message_modes_stat_limits;

    // tg: code/modules/mob/living/living_say.dm:67
    public virtual void InitGlobalmessage_modes_stat_limits()
    {
        message_modes_stat_limits = Dm.list(
            Dm.kv(MODE_INTERCOM, HARD_CRIT),
            Dm.kv(MODE_CHANGELING, HARD_CRIT),
            Dm.kv(MODE_ALIEN, HARD_CRIT),
            Dm.kv(MODE_BINARY, HARD_CRIT), //extra stat check on human/binarycheck()
            Dm.kv("MODE_MONKEY", HARD_CRIT), // tg defines no MODE_MONKEY: DM's list(name = value) makes the undefined name a text key
            Dm.kv(MODE_MAFIA, HARD_CRIT));
        Dm.append(ref gvars_datum_init_order, "message_modes_stat_limits");
    }
}

public partial class mob_living : mob
{
    /// DEVIATION(DV162, D253): how far a shouted line (one ending in "!!", tg's yell) carries, twice tg's say range of 7: the 3D view reaches
    /// farther than tg's screen of 15 by 15 tiles. Port-only (tg's yell changes only the verb).
    public const float SHOUT_RANGE = 14;

    // tg: code/modules/mob/living/living_say.dm:76
    public virtual object Ellipsis(object original_msg = null, float? chance = null, object keep_words = null)
    {
        chance ??= 50;
        if (chance <= 0)
            return "...";
        if (chance >= 100)
            return original_msg;

        DmList words = Dm.splittext(original_msg, " ");
        DmList new_words = Dm.list();

        string new_msg = "";

        foreach (object w in Dm.each(words))
        {
            if (Dm.prob(chance.Value))
            {
                Dm.append(ref new_words, "...");
                if (!Dm.truthy(keep_words))
                    continue;
            }
            Dm.append(ref new_words, w);
        }

        new_msg = Dm.jointext(new_words, " ");

        return new_msg;
    }

    // tg: code/modules/mob/living/living_say.dm:99
    public override async DmTask<object> say(object message = null, object bubble_type = null, DmList spans = null, float? sanitize = null, object language = null, float? ignore_spam = null, object forced = null, float? filterproof = null, float? message_range = null, datum_saymode saymode = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        sanitize ??= TRUE;
        ignore_spam ??= FALSE;
        filterproof ??= FALSE;
        message_range ??= 7;
        message_mods ??= Dm.list();
        if (Dm.truthy(sanitize))
            message = trim(copytext_char(GlobalProcs.sanitize(message), 1, MAX_MESSAGE_LEN));
        if (!Dm.truthy(message) || Dm.eq(message, ""))
            return null;

        object original_message = message;
        message = get_message_mods(message, message_mods);
        saymode = SSradio.get_available_say_mode(this, message_mods[RADIO_KEY]);
        if (!Dm.truthy(forced) && (saymode == null || Dm.truthy(saymode.allows_custom_say_emotes)))
            message = check_for_custom_say_emote(message, message_mods);

        if (!Dm.truthy(message))
            return null;

        if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_ADMIN))
        {
            // tg: :123  SSadmin_verbs.dynamic_invoke_verb(client, /datum/admin_verb/cmd_admin_say, message): the admin tools (M10)
            Dm.Deferred("code/modules/mob/living/living_say.dm:123", "say: the admin channel (asay; the admin tools, M10+)");
            return null;
        }

        if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_DEADMIN))
        {
            // tg: :127  SSadmin_verbs.dynamic_invoke_verb(client, /datum/admin_verb/dsay, message): the admin tools (M10)
            Dm.Deferred("code/modules/mob/living/living_say.dm:127", "say: the admins' dead chat (dsay; the admin tools, M10+)");
            return null;
        }

        // dead is the only state you can never emote
        if (stat != DEAD && Dm.truthy(check_emote(original_message, forced)))
            return null;

        // Checks if the saymode or channel extension can be used even if not totally conscious.
        object say_radio_or_mode = (saymode is var or__1 && Dm.truthy(or__1) ? or__1 : message_mods[RADIO_EXTENSION]);
        if (Dm.truthy(say_radio_or_mode))
        {
            object mob_stat_limit = GLOB.message_modes_stat_limits[say_radio_or_mode];
            if (Dm.gt(stat, mob_stat_limit == null ? STABLE : mob_stat_limit))
            {
                saymode = null;
                Dm.remove(ref message_mods, RADIO_EXTENSION);
            }
        }

        // this is what stops you from talking while dead
        if (stat == DEAD)
        {
            say_dead(original_message, message_mods[MANNEQUIN_CONTROLLED] as mob);
            return null;
        }

        // this is what stops you from talking while asleep, and also what allows you to deathgasp in hard crit
        if (IS_UNCONSCIOUS(this) && (stat != HARD_CRIT || !Dm.truthy(message_mods[WHISPER_MODE])))
            return null;

        if (HAS_TRAIT(this, TRAIT_FORCE_WHISPER))
            message_mods[WHISPER_MODE] = MODE_WHISPER;

        // tg: :156-160  the lag switch's slowmode (SSlag_switch.measures[SLOWMODE_SAY]): the admin tools (M10), never on

        if (!Dm.truthy(await try_speak(original_message, ignore_spam, forced, filterproof)))
            return null;

        if (!Dm.truthy(language))
            language = (message_mods[LANGUAGE_EXTENSION] is var or__2 && Dm.truthy(or__2) ? or__2 : get_selected_language());

        bool succumbed = false;

        // If it's not erasing the input portion, then something is being said and this isn't a pure custom say emote.
        if (!Dm.truthy(message_mods[MODE_CUSTOM_SAY_ERASE_INPUT]))
        {
            if (Dm.eq(message_mods[WHISPER_MODE], MODE_WHISPER))
            {
                message_range = 1;
                // this is where deathgasping is processed
                if (stat == HARD_CRIT)
                {
                    float health_diff = Dm.round(-dead_threshold + health);
                    // If we cut our message short, abruptly end it with a-..
                    float message_len = Dm.length_char(message);
                    message = Dm.add(copytext_char(message, 1, health_diff), message_len > health_diff ? "-.." : "...");
                    message = Ellipsis(message, 10, 1);
                    last_words = message;
                    message_mods[WHISPER_MODE] = MODE_WHISPER_CRIT;
                    succumbed = true;
                }
            }
            // DEVIATION(DV162, D253): a shouted line (tg's yell, the "!!" ending that say_mod gives verb_yell) carries SHOUT_RANGE tiles,
            // farther than tg's 7, because the 3D world is seen and heard farther than tg's screen
            else if (copytext_char(message, -2) == "!!")
                message_range = Dm.max(message_range.Value, SHOUT_RANGE);
        }

        // tg: :184  log_sayverb_talk(message, message_mods, forced_by = forced): the say log (the admin logs, M10)

        // Make sure the arglist is passed exactly - don't pass a copy of it. Say signal handlers will modify some of the parameters.
        DmList args = Dm.list(message, bubble_type, spans, sanitize, language, ignore_spam, forced, filterproof, message_range, saymode, message_mods);
        object sigreturn = !Dm.truthy(_listen_lookup?[COMSIG_MOB_SAY]) ? NONE : _SendSignal(COMSIG_MOB_SAY, Dm.list(this, args));
        message = args[1];
        spans = args[3] as DmList;
        language = args[5];
        message_range = Dm.num_or_null(args[9]);
        message_mods = args[11] as DmList;
        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_UPPERCASE_SPEECH)))
            message = Dm.uppertext(message);

        DmList message_data = treat_message(message) as DmList; // unfortunately we still need this
        message = message_data["message"];
        object tts_message = message_data["tts_message"];
        DmList tts_filter = message_data["tts_filter"] as DmList;

        Dm.combine(ref spans, speech_span);

        datum_language spoken_lang = GLOB.language_datum_instances[language] as datum_language;
        if (Dm.truthy(LAZYLEN(spoken_lang?.spans)))
            Dm.combine(ref spans, spoken_lang.spans);

        if (Dm.truthy(message_mods[MODE_SING]))
        {
            object randomnote = Dm.pick("♩", "♪", "♫");
            message = Dm.text("[] [] []", randomnote, message, randomnote);
            Dm.combine(ref spans, SPAN_SINGING);
        }

        if (Dm.truthy(message_mods[WHISPER_MODE])) // whisper away
            Dm.combine(ref spans, SPAN_ITALICS);

        if (!Dm.truthy(message))
        {
            if (succumbed)
                succumb();
            return null;
        }

        //Get which verb is prefixed to the message before radio but after most modifications
        if (!Dm.truthy(message_mods[SAY_MOD_VERB]))
            message_mods[SAY_MOD_VERB] = say_mod__proc(message, message_mods);

        object identifier = "invalid";
        object tts_message_to_use = Dm.truthy(tts_message) ? tts_message : message;

        if (Dm.truthy(SStts?.tts_enabled) && Dm.truthy(voice) && !Dm.truthy(message_mods[MODE_CUSTOM_SAY_ERASE_INPUT]) && !HAS_TRAIT(this, TRAIT_SIGN_LANG) && !HAS_TRAIT(this, TRAIT_UNKNOWN_VOICE))
        {
            DmList filter = Dm.list();
            DmList special_filter = Dm.list();
            if (Dm.length(voice_filter) > 0)
                Dm.append(ref filter, voice_filter);

            if (Dm.length(tts_filter) > 0)
                Dm.append(ref filter, tts_filter.Join(","));

            string shell_scrubbed_input = tts_speech_filter(html_decode(Dm.str(tts_message_to_use)));
            identifier = Dm.text("[].[]", Dm.sha1(Dm.text("[][][][][][][]", get_tts_voice(filter, special_filter), filter.Join(","), num2text(pitch), special_filter.Join("|"), shell_scrubbed_input, blip_base, blip_number)), world.time);
            message_mods[MODE_TTS_IDENTIFIER] = identifier;
        }

        //This is before anything that sends say a radio message, and after all important message type modifications, so you can scumb in alien chat or something
        if (saymode != null && Dm.truthy(Dm.bitand(await saymode.handle_message(this, message, spans, language, message_mods), SAYMODE_MESSAGE_HANDLED)))
            return null;

        object radio_return = await radio__proc(message, message_mods, spans, language);//roughly 27% of living/say()'s total cost
        if (Dm.truthy(Dm.bitand(radio_return, NOPASS)))
            return TRUE;

        if (Dm.truthy(Dm.bitand(radio_return, ITALICS)))
            Dm.combine(ref spans, SPAN_ITALICS);
        if (Dm.truthy(Dm.bitand(radio_return, REDUCE_RANGE)))
        {
            message_range = 1;
            if (!Dm.truthy(message_mods[WHISPER_MODE]))
            {
                message_mods[WHISPER_MODE] = MODE_WHISPER;
                message_mods[SAY_MOD_VERB] = say_mod__proc(message, message_mods);
            }
        }

        //No screams in space, unless you're next to someone.
        turf T = get_turf(this);
        datum_gas_mixture environment = await T.return_air() as datum_gas_mixture;
        float pressure = Dm.truthy(environment) ? Dm.num(environment.return_pressure()) : 0;
        if (pressure < SOUND_MINIMUM_PRESSURE && !HAS_TRAIT(this, TRAIT_SIGN_LANG))
            message_range = 1;

        if (pressure < ONE_ATMOSPHERE * (HAS_TRAIT(this, TRAIT_SPEECH_BOOSTER) ? 0.1f : 0.4f)) //Thin air, let's italicise the message unless we have a loud low pressure speech trait and not in vacuum
            Dm.combine(ref spans, SPAN_ITALICS);

        await send_speech(message, message_range, this, bubble_type, spans, language, message_mods, forced: forced, tts_message: tts_message, tts_filter: tts_filter);//roughly 58% of living/say()'s total cost
        if (succumbed)
        {
            succumb(TRUE);
            to_chat(this, await compose_message(this, language, message, null, null, null, spans, message_mods));
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/living_say.dm:281
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        message_range ??= 0;
        DmList hear_args = Dm.list(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_MOVABLE_PRE_HEAR]) ? NONE : _SendSignal(COMSIG_MOVABLE_PRE_HEAR, Dm.list(this, hear_args)), COMSIG_MOVABLE_CANCEL_HEARING)) || !Dm.truthy(GET_CLIENT(this)))
            return FALSE;
        raw_message = hear_args[3];
        spans = hear_args[7] as DmList;
        message_mods = hear_args[8] as DmList;

        object deaf_message = null;
        object deaf_type = null;

        if (speaker != this)
            deaf_type = !Dm.truthy(radio_freq) ? MSG_VISUAL : null;
        else
            deaf_type = MSG_AUDIBLE;

        atom_movable_virtualspeaker holopad_speaker = speaker as atom_movable_virtualspeaker;
        bool avoid_highlight = this == (holopad_speaker != null ? holopad_speaker.source : speaker);

        object is_custom_emote = message_mods[MODE_CUSTOM_SAY_ERASE_INPUT];
        bool understood = true;
        if (!Dm.truthy(is_custom_emote)) // we do not translate emotes
        {
            object untranslated_raw_message = raw_message;
            raw_message = translate_language(speaker, message_language, raw_message, spans, message_mods); // translate
            if (!Dm.eq(raw_message, untranslated_raw_message))
                understood = false;
        }

        bool speaker_is_signing = HAS_TRAIT(speaker, TRAIT_SIGN_LANG);
        bool use_runechat = Dm.truthy(safe_read_pref(client, typeof(datum_preference_toggle_enable_runechat)));
        if (IS_UNCONSCIOUS_AND_ALIVE(this))
            use_runechat = false;
        else if (!ismob(speaker) && !Dm.truthy(safe_read_pref(client, typeof(datum_preference_toggle_enable_runechat_non_mobs))))
            use_runechat = false;

        object message = "";
        string speaker_name = span_name(Dm.text("[]", message_mods[MODE_SPEAKER_NAME_OVERRIDE] is var or__1 && Dm.truthy(or__1) ? or__1 : speaker));

        // Infinite range implies something like telecomms, ie something that should never be distance modified
        if (message_range != INFINITY && !HAS_TRAIT(this, TRAIT_GOOD_HEARING))
        {
            float raw_dist = get_dist(speaker, this);
            // Check for projected whispers, calculate distance from the projected tile if so
            if (Dm.truthy(message_mods[WHISPER_MODE]))
            {
                turf in_front = get_step(speaker, speaker.dir) as turf;
                if (Dm.truthy(in_front) && HAS_TRAIT(in_front, TRAIT_TURF_PROJECTS_WHISPERS))
                    raw_dist = Dm.min(raw_dist, get_dist(in_front, this));
            }

            // How far we are we outside the message range?
            float outside_dist = Dm.max(raw_dist - message_range.Value, 0);
            // Out of message range AND out of eavesdrop range - interrupt message entirely
            if (outside_dist > eavesdrop_range)
            {
                // Can't see them speak either. No message
                if (Dm.truthy(is_blind()) || HAS_TRAIT(speaker, TRAIT_INVISIBLE_MAN))
                    return FALSE;
                // If they're inside of something, probably can't see them speak. No message
                if (!isturf(speaker.loc))
                    return FALSE;

                // But we can still see them speak
                if (speaker_is_signing)
                    deaf_message = I18n.T("[] [] something, but the motions are too subtle to make out from afar.", speaker_name, speaker.get_default_say_verb());
                // If we can't hear we want to continue to the default deaf message
                else if (!HAS_TRAIT(this, TRAIT_DEAF))
                {
                    if (isliving(speaker))
                    {
                        mob_living living_speaker = speaker as mob_living;
                        bool mouth_hidden = Dm.truthy(living_speaker.is_mouth_covered()) || HAS_TRAIT(living_speaker, TRAIT_FACE_COVERED);
                        if (mouth_hidden && !HAS_TRAIT(this, TRAIT_SEE_MASK_WHISPER)) // Can't see them speak if their mouth is covered or hidden, unless we're an empath
                            return FALSE;
                    }

                    deaf_message = I18n.T("[] [] something, but you are too far away to hear [].", speaker_name, speaker.verb_whisper, speaker.p_them());
                }

                if (Dm.truthy(deaf_message))
                {
                    deaf_type = MSG_VISUAL;
                    message = deaf_message;
                    show_message(message, MSG_VISUAL, deaf_message, deaf_type, avoid_highlight ? TRUE : FALSE);
                    return FALSE; // Return false so TTS doesn't attempt to read this message.
                }
                return FALSE;
            }
            // Out of message range but within eavesdrop range - alter displayed message
            if (outside_dist > 0)
                raw_message = stars(raw_message);
        }

        // we need to send this signal before compose_message() is used since other signals need to modify
        // the raw_message first. After the raw_message is passed through the various signals, it's ready to be formatted
        // by compose_message() to be displayed in chat boxes for to_chat or runechat
        hear_args = Dm.list(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (Dm.truthy(_listen_lookup?[COMSIG_MOVABLE_HEAR]))
            _SendSignal(COMSIG_MOVABLE_HEAR, Dm.list(this, hear_args));
        raw_message = hear_args[3];
        spans = hear_args[7] as DmList;
        message_mods = hear_args[8] as DmList;

        if (speaker_is_signing) //Checks if speaker is using sign language
        {
            deaf_message = await compose_message(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, TRUE);

            if (speaker != this)
            {
                if (!Dm.truthy(radio_freq)) //I'm about 90% sure there's a way to make this less cluttered
                    deaf_type = MSG_VISUAL;
            }
            else
                deaf_type = MSG_AUDIBLE;

            // Create map text prior to modifying message for goonchat, sign lang edition
            if (use_runechat && !Dm.truthy(is_blind()))
            {
                if (Dm.truthy(is_custom_emote))
                    create_chat_message(speaker, null, message_mods[MODE_CUSTOM_SAY_EMOTE], spans, EMOTE_MESSAGE);
                else
                    create_chat_message(speaker, message_language, raw_message, spans);
            }

            if (Dm.truthy(is_blind()))
                return FALSE;

            message = deaf_message;

            object signed_hearflags = NONE;
            if (Dm.truthy(show_message(message, MSG_VISUAL, deaf_message, deaf_type, avoid_highlight ? TRUE : FALSE)))
                signed_hearflags = Dm.bitor(signed_hearflags, HEAR_HEARD);
            if (understood)
                signed_hearflags = Dm.bitor(signed_hearflags, HEAR_UNDERSTOOD);
            return signed_hearflags;
        }

        if (speaker != this)
        {
            if (!Dm.truthy(radio_freq)) //These checks have to be separate, else people talking on the radio will make "You can't hear yourself!" appear when hearing people over the radio while deaf.
            {
                deaf_message = I18n.T("[] [] something but you cannot hear [].", speaker_name, speaker.get_default_say_verb(), speaker.p_them());
                deaf_type = MSG_VISUAL;
            }
        }
        else
        {
            deaf_message = span_notice(I18n.T("You can't hear yourself!"));
            deaf_type = MSG_AUDIBLE; // Since you should be able to hear yourself without looking
        }

        // Create map text prior to modifying message for goonchat
        if (use_runechat && !HAS_TRAIT(this, TRAIT_DEAF))
        {
            if (Dm.truthy(is_custom_emote))
                create_chat_message(speaker, null, message_mods[MODE_CUSTOM_SAY_EMOTE], spans, EMOTE_MESSAGE);
            else
                create_chat_message(speaker, message_language, raw_message, spans);
        }

        // Recompose message for AI hrefs, language incomprehension.
        message = await compose_message(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods);

        object hearflags = NONE;
        if (Dm.truthy(show_message(message, MSG_AUDIBLE, deaf_message, deaf_type, avoid_highlight ? TRUE : FALSE)))
            hearflags = Dm.bitor(hearflags, HEAR_HEARD);
        if (understood)
            hearflags = Dm.bitor(hearflags, HEAR_UNDERSTOOD);
        return hearflags;
    }

    // tg: code/modules/mob/living/living_say.dm:398
    public override async DmTask send_speech(object message_raw = null, float? message_range = null, atom_movable source = null, object bubble_type = null, DmList spans = null, object message_language = null, DmList message_mods = null, object forced = null, object tts_message = null, DmList tts_filter = null)
    {
        message_range ??= 6;
        source ??= this;
        bubble_type ??= bubble_icon;
        message_mods ??= Dm.list();
        float whisper_range = 0;
        bool is_speaker_whispering = false;
        if (Dm.truthy(message_mods[WHISPER_MODE])) //If we're whispering
        {
            // Needed for good hearing trait. The actual filtering for whispers happens at the /mob/living/Hear proc
            whisper_range = MESSAGE_RANGE - WHISPER_RANGE;
            is_speaker_whispering = true;
        }

        DmList in_view = get_hearers_in_view(message_range + whisper_range, source) as DmList;
        DmList listening = get_hearers_in_range(message_range + whisper_range, source) as DmList;

        if (is_speaker_whispering)
        {
            turf in_front = get_step(this, dir) as turf;
            if (Dm.truthy(in_front) && HAS_TRAIT(in_front, TRAIT_TURF_PROJECTS_WHISPERS))
            {
                Dm.combine(ref in_view, get_hearers_in_view(message_range + whisper_range, in_front));
                Dm.combine(ref listening, get_hearers_in_range(message_range + whisper_range, in_front));
            }
        }

        // Pre-process listeners to account for line-of-sight
        foreach (atom_movable listening_movable in Dm.each_cast<atom_movable>(listening))
        {
            if (!Dm.@in(listening_movable, in_view) && !HAS_TRAIT(listening_movable, TRAIT_XRAY_HEARING))
                Dm.remove(ref listening, listening_movable);
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_SEND_SPEECH]))
            _SendSignal(COMSIG_LIVING_SEND_SPEECH, Dm.list(this, listening));

        if (Dm.truthy(imaginary_group))
            Dm.combine(ref listening, imaginary_group);

        if (client != null) //client is so that ghosts don't have to listen to mice
        {
            foreach (mob player_mob in Dm.each_cast<mob>(GLOB.player_list))
            {
                if (QDELETED(player_mob)) //Some times nulls and deleteds stay in this list. This is a workaround to prevent ic chat breaking for everyone when they do.
                    continue; //Remove if underlying cause (likely byond issue) is fixed. See TG PR #49004.
                if (player_mob.stat != DEAD) //not dead, not important
                    continue;
                if (player_mob.z != z || get_dist(player_mob, this) > 7) //they're out of range of normal hearing
                {
                    if (is_speaker_whispering)
                    {
                        if (!Dm.truthy(Dm.bitand(get_chat_toggles(player_mob.client), CHAT_GHOSTWHISPER))) //they're whispering and we have hearing whispers at any range off
                            continue;
                    }
                    else if (!Dm.truthy(Dm.bitand(get_chat_toggles(player_mob.client), CHAT_GHOSTEARS))) //they're talking normally and we have hearing at any range off
                        continue;
                }
                Dm.combine(ref listening, player_mob);
            }
        }

        object tts_message_to_use = Dm.truthy(tts_message) ? tts_message : message_raw;

        // this signal ignores whispers or language translations (only used by beetlejuice component)
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_LIVING_SAY_SPECIAL]))
            SSdcs._SendSignal(COMSIG_GLOB_LIVING_SAY_SPECIAL, Dm.list(SSdcs, this, message_raw));
        DmList listened = Dm.list();
        foreach (atom_movable listening_movable in Dm.each_cast<atom_movable>(listening))
        {
            if (!Dm.truthy(listening_movable))
            {
                stack_trace("somehow theres a null returned from get_hearers_in_view() in send_speech!", "code/modules/mob/living/living_say.dm", 454);
                continue;
            }

            if (Dm.truthy(Dm.bitand(await listening_movable.Hear(this, message_language, message_raw, null, null, null, spans, message_mods, message_range), HEAR_HEARD)))
                Dm.append(ref listened, listening_movable);
        }

        //speech bubble
        DmList speech_bubble_recipients = Dm.list();
        object talk_icon_state = say_test(message_raw);
        foreach (mob M in Dm.each<mob>(listening))
        {
            if (M.client != null)
            {
                // tg: :465  || (SSlag_switch.measures[DISABLE_RUNECHAT] && ...): the lag switch is the admin tools (M10), never on
                if (!Dm.truthy(safe_read_pref(M.client, typeof(datum_preference_toggle_enable_runechat))))
                    Dm.append(ref speech_bubble_recipients, M.client);
            }
        }
        do_tts_message(tts_message_to_use, message_language, message_mods, tts_filter, listened);
        image say_popup = image("icons/mob/effects/talk.dmi", this, Dm.text("[][]", bubble_type, talk_icon_state), FLY_LAYER);
        SET_PLANE_EXPLICIT(say_popup, ABOVE_GAME_PLANE, this);
        say_popup.appearance_flags = APPEARANCE_UI_IGNORE_ALPHA;
        Waiting.DetachSync(() => flick_overlay_global(say_popup, speech_bubble_recipients, 3 * SECONDS));
        LAZYADD(ref update_on_z, say_popup);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_saypopup), say_popup), 3.5f * SECONDS, file: "code/modules/mob/living/living_say.dm", line: 478);
    }

    // tg: code/modules/mob/living/living_say.dm:491
    public virtual void clear_saypopup(image say_popup = null)
    {
        LAZYREMOVE(ref update_on_z, say_popup);
    }

    /**
     * Treats the passed message with things that may modify speech (stuttering, slurring etc).
     *
     * message - The message to treat.
     * capitalize_message - Whether we run capitalize() on the message after we're done.
     *
     * Returns a list, which is a packet of information corresponding to the message that has been treated, which
     * contains the new message, as well as text-to-speech information.
     */
    // tg: code/modules/mob/living/living_say.dm:507
    public virtual object treat_message(object message = null, object tts_message = null, object tts_filter = null, float? capitalize_message = null)
    {
        capitalize_message ??= TRUE;

        if (HAS_TRAIT(this, TRAIT_UNINTELLIGIBLE_SPEECH))
            message = unintelligize(message);

        tts_filter = Dm.list();
        DmList data = Dm.list(message, tts_message, tts_filter, capitalize_message);
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_TREAT_MESSAGE]))
            _SendSignal(COMSIG_LIVING_TREAT_MESSAGE, Dm.list(this, data));
        message = data[TREAT_MESSAGE_ARG];
        tts_message = data[TREAT_TTS_MESSAGE_ARG];
        tts_filter = data[TREAT_TTS_FILTER_ARG];
        capitalize_message = Dm.num_or_null(data[TREAT_CAPITALIZE_MESSAGE]);

        if (!Dm.truthy(tts_message))
            tts_message = message;

        if (Dm.truthy(capitalize_message))
        {
            message = capitalize(message);
            tts_message = capitalize(tts_message);
        }

        return Dm.list(Dm.kv("message", message), Dm.kv("tts_message", tts_message), Dm.kv("tts_filter", tts_filter));
    }

    // tg: code/modules/mob/living/living_say.dm:532
    public virtual async DmTask<object> radio__proc(object message = null, DmList message_mods = null, DmList spans = null, object language = null)
    {
        message_mods ??= Dm.list();
        obj_item_implant_radio imp = Dm.locate<obj_item_implant_radio>(this);
        if (imp != null && Dm.truthy(imp.radio.is_on()))
        {
            if (Dm.truthy(message_mods[MODE_HEADSET]))
            {
                imp.radio.talk_into(this, message, null, spans, language, message_mods);
                return Dm.bitor(ITALICS, REDUCE_RANGE);
            }
            if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_DEPARTMENT) || Dm.@in(message_mods[RADIO_EXTENSION], imp.radio.channels))
            {
                imp.radio.talk_into(this, message, message_mods[RADIO_EXTENSION], spans, language, message_mods);
                return Dm.bitor(ITALICS, REDUCE_RANGE);
            }
        }
        object sw = message_mods[RADIO_EXTENSION];
        if (Dm.eq(sw, MODE_R_HAND))
        {
            foreach (obj_item r_hand in Dm.each_cast<obj_item>(get_held_items_for_side(RIGHT_HANDS, all: TRUE)))
            {
                if (Dm.truthy(r_hand))
                    return r_hand.talk_into(this, message, null, spans, language, message_mods);
                return Dm.bitor(ITALICS, REDUCE_RANGE);
            }
        }
        else if (Dm.eq(sw, MODE_L_HAND))
        {
            foreach (obj_item l_hand in Dm.each_cast<obj_item>(get_held_items_for_side(LEFT_HANDS, all: TRUE)))
            {
                if (Dm.truthy(l_hand))
                    return l_hand.talk_into(this, message, null, spans, language, message_mods);
                return Dm.bitor(ITALICS, REDUCE_RANGE);
            }
        }
        else if (Dm.eq(sw, MODE_INTERCOM))
        {
            foreach (obj_item_radio_intercom I in Dm.each<obj_item_radio_intercom>(view(MODE_RANGE_INTERCOM, null)))
                I.talk_into(this, message, null, spans, language, message_mods);
            return Dm.bitor(ITALICS, REDUCE_RANGE);
        }

        return NONE;
    }

    // tg: code/modules/mob/living/living_say.dm:561
    public override object say_mod__proc(object input = null, DmList message_mods = null)
    {
        message_mods ??= Dm.list();
        object dot = null;
        if (Dm.eq(message_mods[WHISPER_MODE], MODE_WHISPER))
            dot = verb_whisper;
        else if (Dm.eq(message_mods[WHISPER_MODE], MODE_WHISPER_CRIT) && !HAS_TRAIT(this, TRAIT_SUCCUMB_OVERRIDE))
            dot = I18n.T("[] in [] last breath", verb_whisper, p_their());
        else if (Dm.truthy(message_mods[MODE_SING]))
            dot = verb_sing;
        // Any subtype of slurring in our status effects make us "slur"
        else if (Dm.truthy(Dm.locate_type(typeof(datum_status_effect_speech_slurring), status_effects)))
        {
            if (HAS_TRAIT(this, TRAIT_SIGN_LANG))
                dot = I18n.T("loosely signs");
            else
                dot = I18n.T("slurs");
        }
        else if (Dm.truthy(has_status_effect(typeof(datum_status_effect_speech_stutter))))
        {
            if (HAS_TRAIT(this, TRAIT_SIGN_LANG))
                dot = I18n.T("shakily signs");
            else
                dot = I18n.T("stammers");
        }
        else if (Dm.truthy(has_status_effect(typeof(datum_status_effect_speech_stutter_derpspeech))))
        {
            if (HAS_TRAIT(this, TRAIT_SIGN_LANG))
                dot = I18n.T("incoherently signs");
            else
                dot = I18n.T("gibbers");
        }
        else
            dot = base.say_mod__proc(input, message_mods);
        return dot;
    }

    /**
     * Living level whisper.
     *
     * Living mobs which whisper have their message only appear to people very close.
     *
     * message - the message to display
     * bubble_type - the type of speech bubble that shows up when they speak (currently does nothing)
     * spans - a list of spans to apply around the message
     * sanitize - whether we sanitize the message
     * language - typepath language to force them to speak / whisper in
     * ignore_spam - whether we ignore the spam filter
     * forced - string source of what forced this speech to happen, also bypasses spam filter / mutes if supplied
     * filterproof - whether we ignore the word filter
     */
    // tg: code/modules/mob/living/living_say.dm:602
    public override async DmTask<object> whisper(object message = null, object bubble_type = null, DmList spans = null, float? sanitize = null, object language = null, float? ignore_spam = null, object forced = null, float? filterproof = null)
    {
        if (!Dm.truthy(message))
            return null;
        await say(Dm.text("#[]", message), bubble_type, spans, sanitize, language, ignore_spam, forced, filterproof);
        return null;
    }
}

public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_say.dm:480
    public override object get_tts_voice(DmList filter = null, DmList special_filter = null)
    {
        object dot = voice;
        obj_item_clothing_mask mask = get_item_by_slot(ITEM_SLOT_MASK) as obj_item_clothing_mask;
        if (mask == null || Dm.truthy(mask.up))
            return dot;
        if (Dm.truthy(mask.voice_override))
            dot = mask.voice_override;
        if (Dm.truthy(mask.voice_filter))
            Dm.append(ref filter, mask.voice_filter);
        if (Dm.truthy(mask.use_radio_beeps_tts))
            Dm.combine(ref special_filter, TTS_FILTER_RADIO);
        return dot;
    }
}

public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/living_say.dm:492
    public override object get_tts_voice(DmList filter = null, DmList special_filter = null)
    {
        object dot = base.get_tts_voice(filter, special_filter);
        Dm.combine(ref special_filter, TTS_FILTER_SILICON);
        return dot;
    }
}

public partial class mob
{
    // tg: code/modules/mob/living/living_say.dm:499
    public virtual object binarycheck()
    {
        return FALSE;
    }
}
