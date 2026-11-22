// Translated from tgstation (snapshot 1d99bd7f): code/game/say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*
Miauw's big Say() rewrite.
This file has the basic atom/movable level speech procs.
And the base of the send_speech() proc, which is the core of saycode.
*/

// M9 slice A (docs/ai/specs/social.md): the whole file. Text to speech (D307, D308): do_tts_message, get_tts_voice and the TTS identifier
// that joins a line to its radio broadcasts. The language
// icon of a not understood language (an asset spritesheet image) and a language's scrambling are the alpha's languages (not-translated.md
// section 70); every speaker in the port speaks Galactic Common, which tg hides when understood.

public partial class datum_controller_global_vars
{
    // tg: code/game/say.dm:6  GLOBAL_LIST_INIT(freqtospan, list(...))
    public DmList freqtospan;

    // tg: code/game/say.dm:6
    public virtual void InitGlobalfreqtospan()
    {
        freqtospan = Dm.list(
            Dm.kv(Dm.text("[]", FREQ_COMMON), "radio"),
            Dm.kv(Dm.text("[]", FREQ_SCIENCE), "sciradio"),
            Dm.kv(Dm.text("[]", FREQ_MEDICAL), "medradio"),
            Dm.kv(Dm.text("[]", FREQ_ENGINEERING), "engradio"),
            Dm.kv(Dm.text("[]", FREQ_SUPPLY), "suppradio"),
            Dm.kv(Dm.text("[]", FREQ_SERVICE), "servradio"),
            Dm.kv(Dm.text("[]", FREQ_SECURITY), "secradio"),
            Dm.kv(Dm.text("[]", FREQ_COMMAND), "comradio"),
            Dm.kv(Dm.text("[]", FREQ_AI_PRIVATE), "aiprivradio"),
            Dm.kv(Dm.text("[]", FREQ_ENTERTAINMENT), "enteradio"),
            Dm.kv(Dm.text("[]", FREQ_SYNDICATE), "syndradio"),
            Dm.kv(Dm.text("[]", FREQ_UPLINK), "syndradio"),  // this probably shouldnt appear ingame
            Dm.kv(Dm.text("[]", FREQ_CENTCOM), "centcomradio"),
            Dm.kv(Dm.text("[]", FREQ_CTF_RED), "redteamradio"),
            Dm.kv(Dm.text("[]", FREQ_CTF_BLUE), "blueteamradio"),
            Dm.kv(Dm.text("[]", FREQ_CTF_GREEN), "greenteamradio"),
            Dm.kv(Dm.text("[]", FREQ_CTF_YELLOW), "yellowteamradio"),
            Dm.kv(Dm.text("[]", FREQ_STATUS_DISPLAYS), "captaincast"));
        Dm.append(ref gvars_datum_init_order, "freqtospan");
    }

    /// Chat emphasis players can add to their chat messages - assoc (emphasis character) to (HTML tag)
    // tg: code/game/say.dm:256  GLOBAL_LIST_INIT(emphasis_types, list(...))
    public DmList emphasis_types;

    // tg: code/game/say.dm:256
    public virtual void InitGlobalemphasis_types()
    {
        emphasis_types = Dm.list(
            Dm.kv("|", "i"),
            Dm.kv("+", "b"),
            Dm.kv("_", "u"),
            Dm.kv("^", "small"));
        Dm.append(ref gvars_datum_init_order, "emphasis_types");
    }
}

public partial class atom_movable : atom
{
    /**
     * What makes things... talk.
     *
     * * message - The message to say.
     * * bubble_type - The type of speech bubble to use when talking
     * * spans - A list of spans to attach to the message. Includes the atom's speech span by default
     * * sanitize - Should we sanitize the message? Only set to FALSE if you have ALREADY sanitized it
     * * language - The language to speak in. Defaults to the atom's selected language
     * * ignore_spam - Should we ignore spam checks?
     * * forced - What was it forced by? null if voluntary. (NOT a boolean!)
     * * filterproof - Do we bypass the filter when checking the message?
     * * message_range - The range of the message. Defaults to 7
     * * saymode - Saymode passed to the speech
     * This is usually set automatically and is only relevant for living mobs.
     * * message_mods - A list of message modifiers, i.e. whispering/singing.
     * Most of these are set automatically but you can pass in your own pre-say.
     */
    // tg: code/game/say.dm:42
    public virtual async DmTask<object> say(object message = null, object bubble_type = null, DmList spans = null, float? sanitize = null, object language = null, float? ignore_spam = null, object forced = null, float? filterproof = null, float? message_range = null, datum_saymode saymode = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        sanitize ??= TRUE;
        ignore_spam ??= FALSE;
        filterproof ??= FALSE;
        message_range ??= 7;
        message_mods ??= Dm.list();
        if (!Dm.truthy(await try_speak(message, ignore_spam, forced, filterproof)))
            return null;
        if (Dm.truthy(sanitize))
            message = trim(copytext_char(GlobalProcs.sanitize(message), 1, MAX_MESSAGE_LEN));
        if (!Dm.truthy(message) || Dm.eq(message, ""))
            return null;
        Dm.combine(ref spans, speech_span);
        if (!Dm.truthy(language))
            language = get_selected_language();
        if (!Dm.truthy(message_mods[SAY_MOD_VERB]))
            message_mods[SAY_MOD_VERB] = say_mod__proc(message, message_mods);
        await send_speech(message, message_range, this, bubble_type, spans, language, message_mods, forced: forced);
        return null;
    }

    /// Called when this movable hears a message from a source.
    /// Returns TRUE if the message was received and understood.
    // tg: code/game/say.dm:63
    public virtual async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        message_range ??= 0;
        if (Dm.truthy(_listen_lookup?[COMSIG_MOVABLE_HEAR]))
            _SendSignal(COMSIG_MOVABLE_HEAR, Dm.list(this, Dm.list(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range)));
        return Dm.bitor(HEAR_HEARD, HEAR_UNDERSTOOD);
    }

    /**
     * Checks if our movable can speak the provided message, passing it through filters
     * and spam detection. Does not call can_speak. CAN include feedback messages about
     * why someone can or can't speak
     *
     * Used in [proc/say] and other methods of speech (radios) after a movable has inputted some message.
     * If you just want to check if the movable is able to speak in character, use [proc/can_speak] instead.
     *
     * Parameters:
     * - message (string): the original message
     * - ignore_spam (bool): should we ignore spam?
     * - forced (null|string): what was it forced by? null if voluntary
     * - filterproof (bool): are we filterproof?
     *
     * Returns:
     * 	TRUE of FASE depending on if our movable can speak
     */
    // tg: code/game/say.dm:85
    public virtual async DmTask<object> try_speak(object message = null, float? ignore_spam = null, object forced = null, float? filterproof = null)
    {
        return can_speak__proc();
    }

    /**
     * Checks if our movable can currently speak, vocally, in general.
     * Should NOT include feedback messages about why someone can or can't speak

     * Used in various places to check if a movable is simply able to speak in general,
     * regardless of OOC status (being muted) and regardless of what they're actually saying.
     *
     * Checked AFTER handling of xeno channels.
     * (I'm not sure what this comment means, but it was here in the past, so I'll maintain it here.)
     *
     * allow_mimes - Determines if this check should skip over mimes. (Only matters for living mobs and up.)
     * If FALSE, this check will always fail if the movable has a mind and is miming.
     * if TRUE, we will check if the movable can speak REGARDLESS of if they have an active mime vow.
     */
    // tg: code/game/say.dm:102
    public virtual object can_speak__proc(float? allow_mimes = null)
    {
        allow_mimes ??= FALSE;
        return !HAS_TRAIT(this, TRAIT_MUTE) ? TRUE : FALSE;
    }

    // tg: code/game/say.dm:106  (set waitfor = FALSE: it only calls INVOKE_ASYNC, so it never sleeps)
    public virtual void do_tts_message(object message = null, object language = null, DmList message_mods = null, DmList tts_filter = null, DmList hearers = null)
    {
        if (!Dm.truthy(SStts?.tts_enabled) || !Dm.truthy(voice) || HAS_TRAIT(this, TRAIT_SIGN_LANG) || HAS_TRAIT(this, TRAIT_UNKNOWN_VOICE) || Dm.truthy(message_mods[MODE_CUSTOM_SAY_ERASE_INPUT]))
            return;

        DmList filter = Dm.list();
        if (Dm.length(voice_filter) > 0)
            Dm.append(ref filter, voice_filter);

        if (Dm.length(tts_filter) > 0)
            Dm.append(ref filter, tts_filter.Join(","));
        DmList special_filter = Dm.list();
        // tg: the voice filter (ffmpeg's) and the special filters go to the TTS server; the clients do not apply them yet (DV262)
        object speaker = get_tts_voice(filter, special_filter);
        // DEVIATION(D312, DV262): a whisper is voiced breathy and quieter, a shout ("!!", tg's yell) strained and louder, out to the shout's
        // range (DV162); tg voices both as a plain line at range 7
        float message_range = 7, volume_offset = 0;
        if (Dm.truthy(message_mods[WHISPER_MODE]))
        {
            Dm.combine(ref special_filter, datum_controller_subsystem_tts.TTS_FILTER_WHISPER);
            volume_offset = -35;
        }
        else if (copytext_char(Dm.str(message), -2) == "!!")
        {
            Dm.combine(ref special_filter, datum_controller_subsystem_tts.TTS_FILTER_YELL);
            message_range = mob_living.SHOUT_RANGE;
            volume_offset = 15;
        }
        Waiting.DetachSync(() => SStts.queue_tts_message(this, html_decode(Dm.str(message)), language, speaker, filter.Join(","), hearers, message_range: message_range, volume_offset: volume_offset, pitch: pitch, special_filters: special_filter.Join("|"), blip_base: blip_base, blip_number: blip_number, identifier: message_mods[MODE_TTS_IDENTIFIER]));
    }

    // tg: code/game/say.dm:121
    public virtual object get_tts_voice(DmList filter = null, DmList special_filter = null)
    {
        return voice;
    }

    // tg: code/game/say.dm:124  (source is obj/source = src in tg, a hint only: living speech passes the mob itself, rules 3.3)
    public virtual async DmTask send_speech(object message = null, float? range = null, atom_movable source = null, object bubble_type = null, DmList spans = null, object message_language = null, DmList message_mods = null, object forced = null, object tts_message = null, DmList tts_filter = null)
    {
        range ??= 7;
        source ??= this;
        message_mods ??= Dm.list();
        DmList listeners = get_hearers_in_view(range, source) as DmList;
        DmList listened = Dm.list();

        object tts_message_to_use = tts_message;
        if (!Dm.truthy(tts_message_to_use))
            tts_message_to_use = message;

        DmList filter = Dm.list();
        if (Dm.length(voice_filter) > 0)
            Dm.append(ref filter, voice_filter);

        if (Dm.length(tts_filter) > 0)
            Dm.append(ref filter, tts_filter.Join(","));

        string shell_scrubbed_input = tts_speech_filter(html_decode(Dm.str(tts_message_to_use)));
        object identifier = Dm.text("[].[]", Dm.sha1(Dm.text("[][][][][][]", voice, filter.Join(","), num2text(pitch), shell_scrubbed_input, blip_base, blip_number)), world.time);
        message_mods[MODE_TTS_IDENTIFIER] = identifier;
        foreach (atom_movable hearing_movable in Dm.each_cast<atom_movable>(listeners))
        {
            if (!Dm.truthy(hearing_movable))//theoretically this should use as anything because it shouldnt be able to get nulls but there are reports that it does.
            {
                stack_trace("somehow theres a null returned from get_hearers_in_view() in send_speech!", "code/game/say.dm", 145);
                continue;
            }
            if (Dm.truthy(Dm.bitand(await hearing_movable.Hear(this, message_language, message, null, null, null, spans, message_mods, range), HEAR_HEARD)))
                Dm.append(ref listened, hearing_movable);
        }

        do_tts_message(tts_message_to_use, message_language, message_mods, tts_filter, listened);
    }

    // tg: code/game/say.dm:152
    public virtual async DmTask<object> compose_message(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? visible_name = null)
    {
        message_mods ??= Dm.list();
        visible_name ??= FALSE;
        //This proc uses [] because it is faster than continually appending strings. Thanks BYOND.
        //Basic span
        object freq_color = get_radio_color(radio_freq, radio_freq_color);
        string spanpart1 = Dm.text("<span class='[]' []>", Dm.truthy(radio_freq) ? get_radio_span(radio_freq) : "game say", Dm.truthy(freq_color) ? Dm.text("style='color:[];'", freq_color) : "");
        //Start name span.
        string spanpart2 = "<span class='name'>";
        //Radio freq/name display
        string freqpart = Dm.truthy(radio_freq) ? Dm.text("\\[[]\\] ", get_radio_name(radio_freq, radio_freq_name)) : "";
        //Speaker name
        object namepart = (message_mods[MODE_SPEAKER_NAME_OVERRIDE] is var or__1 && Dm.truthy(or__1) ? or__1 : speaker.get_message_voice(visible_name));

        //End name span.
        string endspanpart = "</span>";

        // Language icon.
        string languageicon = "";
        if (!Dm.truthy(message_mods[MODE_CUSTOM_SAY_ERASE_INPUT]))
        {
            datum_language dialect = GLOB.language_datum_instances[message_language] as datum_language;
            object dialect_icon_type = (dialect?.display_icon_type(this, message_mods) is var or__2 && Dm.truthy(or__2) ? or__2 : DISPLAY_LANGUAGE_ICON_NONE);
            if (!Dm.eq(dialect_icon_type, DISPLAY_LANGUAGE_ICON_NONE))
            {
                // tg: :178-181  the language's icon in a subtle tooltip: the chat asset spritesheet is not ported (the chat icons gap, not-translated.md section 76)
                Dm.Deferred("code/game/say.dm:178", "compose_message: the language's icon in the chat line (the chat spritesheet, section 76)");
            }
        }

        // The actual message part.
        object messagepart = speaker.generate_messagepart(raw_message, spans, message_mods);
        messagepart = Dm.text(" <span class='message'>[]</span></span>", messagepart);

        object speaker_voice_description = (message_mods[MODE_SPEAKER_GENDER_OVERRIDE] is var or__3 && Dm.truthy(or__3) ? or__3 : speaker.get_voice_description());

        return Dm.text("[][][][][][][][][]", spanpart1, spanpart2, freqpart, languageicon, compose_track_href(speaker, namepart), span_tooltip_subtle(Dm.str(speaker_voice_description), Dm.str(namepart)), compose_job(speaker, message_language, raw_message, radio_freq), endspanpart, messagepart);
    }

    // tg: code/game/say.dm:191
    public virtual object compose_track_href(atom_movable speaker = null, object message_langs = null, object raw_message = null, object radio_freq = null)
    {
        return "";
    }

    // tg: code/game/say.dm:194
    public virtual object compose_job(atom_movable speaker = null, object message_langs = null, object raw_message = null, object radio_freq = null)
    {
        return "";
    }

    /**
     * Works out and returns which prefix verb the passed message should use.
     *
     * input - The message for which we want the verb.
     * message_mods - A list of message modifiers, i.e. whispering/singing.
     */
    // tg: code/game/say.dm:203
    public virtual object say_mod__proc(object input = null, DmList message_mods = null)
    {
        message_mods ??= Dm.list();
        object dot = null;
        string ending = copytext_char(input, -1);
        if (copytext_char(input, -2) == "!!")
            return verb_yell;
        else if (Dm.truthy(message_mods[MODE_SING]))
            dot = verb_sing;
        else if (Dm.truthy(message_mods[WHISPER_MODE]))
            dot = verb_whisper;
        else if (ending == "?")
            return verb_ask;
        else if (ending == "!")
            return verb_exclaim;
        else
            return get_default_say_verb();
        return dot;
    }

    /**
     * Gets the say verb we default to if no special verb is chosen.
     * This is primarily a hook for inheritors,
     * like human_say.dm's tongue-based verb_say changes.
     */
    // tg: code/game/say.dm:222
    public virtual object get_default_say_verb()
    {
        return verb_say;
    }

    /**
     * This proc is used to generate the 'message' part of a chat message.
     * Generates the `says, "<span class='red'>meme</span>"` part of the `Grey Tider says, "meme"`,
     * or the `taps their microphone.` part of `Grey Tider taps their microphone.`.
     *
     * input - The message to be said
     * spans - A list of spans to attach to the message. Includes the atom's speech span by default
     * message_mods - A list of message modifiers, i.e. whispering/singing
     */
    // tg: code/game/say.dm:235
    public virtual object generate_messagepart(object input = null, DmList spans = null, DmList message_mods = null)
    {
        spans ??= Dm.list(speech_span);
        message_mods ??= Dm.list();
        // If we only care about the emote part, early return.
        if (Dm.truthy(message_mods[MODE_CUSTOM_SAY_ERASE_INPUT]))
            return apply_message_emphasis(message_mods[MODE_CUSTOM_SAY_EMOTE]);

        // Otherwise, we format our full quoted message.
        if (!Dm.truthy(input))
            input = "...";

        object say_mod = (message_mods[MODE_CUSTOM_SAY_EMOTE] is var or__1 && Dm.truthy(or__1) ? or__1 : message_mods[SAY_MOD_VERB] is var or__2 && Dm.truthy(or__2) ? or__2 : say_mod__proc(input, message_mods));

        if (Dm.truthy(_listen_lookup?[COMSIG_MOVABLE_SAY_QUOTE]))
            _SendSignal(COMSIG_MOVABLE_SAY_QUOTE, Dm.list(this, Dm.list(input, spans, message_mods)));

        if (copytext_char(input, -2) == "!!")
            Dm.combine(ref spans, SPAN_YELL);

        /* all inputs should be fully figured out past this point */

        object processed_input = apply_message_emphasis(input); //This MUST be done first so that we don't get clipped by spans
        processed_input = attach_spans(processed_input, spans);

        object processed_say_mod = apply_message_emphasis(say_mod);

        return Dm.text("[], \"[]\"", processed_say_mod, processed_input);
    }

    // tg: code/game/say.dm:243
    public virtual object get_voice_description()
    {
        object sw = gender;
        if (Dm.eq(sw, MALE))
            return VOICE_DESCRIPTION_MASCULINE;
        else if (Dm.eq(sw, FEMALE))
            return VOICE_DESCRIPTION_FEMININE;
        else if (Dm.eq(sw, PLURAL))
            return VOICE_DESCRIPTION_PLURAL;
        else
            return VOICE_DESCRIPTION_NEUTER;
    }

    /// Modifies the message by comparing the languages of the speaker with the languages of the hearer. Called on the hearer.
    // tg: code/game/say.dm:296
    public virtual object translate_language(atom_movable speaker = null, object language = null, object raw_message = null, DmList spans = null, DmList message_mods = null)
    {
        if (!Dm.truthy(language))
            return I18n.T("makes a strange sound.");

        if (!Dm.truthy(has_language(language)))
        {
            DmList mutual_languages = null;
            // Get what we can kinda understand, factor in any bonuses passed in from say mods
            DmList partially_understood_languages = get_partially_understood_languages() as DmList;
            if (Dm.truthy(LAZYLEN(partially_understood_languages)))
            {
                mutual_languages = partially_understood_languages.Copy();
                foreach (object bonus_language in Dm.each(message_mods?[LANGUAGE_MUTUAL_BONUS]))
                    mutual_languages[bonus_language] = Dm.max((message_mods[LANGUAGE_MUTUAL_BONUS] as DmList)[bonus_language], mutual_languages[bonus_language]);
            }

            datum_language dialect = GLOB.language_datum_instances[language] as datum_language;
            raw_message = dialect.scramble_paragraph(raw_message, mutual_languages);
        }

        return raw_message;
    }

    // tg: code/game/say.dm:376  /atom/movable/proc/GetJob() (the virtual speakers' job)
    public virtual object GetJob()
    {
        return null;
    }

    // tg: code/game/say.dm:378
    public virtual object GetSource()
    {
        return null;
    }

    // tg: code/game/say.dm:380
    public virtual object GetRadio()
    {
        return null;
    }
}

public partial class atom : datum
{
    /**
     * Get what this atom sounds like when speaking
     *
     * * add_id_name - If TRUE, ID information such as honorifics are added into the voice
     */
    // tg: code/game/say.dm:361
    public virtual object get_voice(float? add_id_name = null)
    {
        add_id_name ??= FALSE;
        return Dm.text("[]", this); //Returns the atom's name, prepended with 'The' if it's not a proper noun
    }

    /**
     * Get what this atom appears like in chat when speaking
     *
     * * visible_name - If TRUE, returns the visible name rather than the voice
     */
    // tg: code/game/say.dm:369
    public virtual object get_message_voice(float? visible_name = null)
    {
        return Dm.truthy(visible_name) ? get_visible_name(add_id_name: TRUE) : get_voice(add_id_name: TRUE);
    }
}

public static partial class GlobalProcs
{
    /**
     * Replaces player chat emphasis characters with the corresponding HTML tags in the input string.
     *
     * You do NOT (and SHOULD not) need to use on anything that isn't direct user input,
     * as this is designed solely to transform user input. Just put in the HTML tags yourself.
     *
     * Returns the transformed input, which may have no change if no emphasis characters are present.
     */
    // tg: code/game/say.dm:271
    public static object apply_message_emphasis(object input = null)
    {
        if (!Dm.truthy(apply_message_emphasis__emphasis_regex))
        {
            DmList escaped_emphasis_characters = Dm.list();
            foreach (object @char in Dm.each(GLOB.emphasis_types))
                Dm.append(ref escaped_emphasis_characters, Dm.text("\\[]", @char));

            // group 1 will be the emphasis character (like |, +, _, ^) including any escape backslashes
            // group 2 will be all characters enclosed by the emphasis characters
            // note the closing emphasis character is not grouped in either, but is still matched
            apply_message_emphasis__emphasis_regex = Dm.regex(Dm.text("(\\\\?(?:[]))(.+?)\\1", jointext(escaped_emphasis_characters, "|")), "g");
        }

        return apply_message_emphasis__emphasis_regex.Replace_char(input, "/proc/" + nameof(GlobalProcs.__replace_message_emphasis));
    }

    // tg: code/game/say.dm:273  apply_message_emphasis's var/static/regex/emphasis_regex
    private static ref regex apply_message_emphasis__emphasis_regex => ref TgState.Current.GlobalProcs__apply_message_emphasis__emphasis_regex;

    // tg: code/game/say.dm:285
    public static object __replace_message_emphasis(object match = null, object group1 = null, object group2 = null, params object[] __extra)
    {
        // look for corresponding html tag for the emphasis character...
        object html = GLOB.emphasis_types[group1];
        if (Dm.truthy(html))
            return Dm.text("<[]>[]</[]>&#8203;", html, group2, html); // zero-width space to force maptext to respect closing tags.

        // if we didn't find an html tag it means we are an escaped character, so we just need to unescape it.
        string unescaped = copytext_char(group1, 2);
        return Dm.text("[][][]", unescaped, group2, unescaped);
    }

    // tg: code/game/say.dm:314
    public static object get_radio_span(object freq = null)
    {
        object returntext = GLOB.freqtospan[Dm.text("[]", freq)];
        if (Dm.truthy(returntext))
            return returntext;
        return "radio";
    }

    // tg: code/game/say.dm:320
    public static object get_radio_name(object freq = null, object freq_name = null)
    {
        if (Dm.truthy(freq_name))
            return freq_name;
        object name = GLOB.reserved_radio_frequencies[Dm.text("[]", freq)];
        if (Dm.truthy(name))
            return name;
        return Dm.text("[].[]", copytext_char(Dm.text("[]", freq), 1, 4), copytext_char(Dm.text("[]", freq), 4, 5));
    }

    // tg: code/game/say.dm:328
    public static object get_radio_color(object freq = null, object freq_color = null)
    {
        if (Dm.truthy(freq))
        {
            // No custom colors for channels with theme settings
            if (Dm.truthy(GLOB.freqtospan[Dm.text("[]", freq)]))
                return "";
            // No color overrides for commonn channel color (for freqs like 145.3)
            if (Dm.eq(freq_color, RADIO_COLOR_COMMON))
                return "";
            if (Dm.truthy(freq_color))
                return freq_color;
            object color = GLOB.reserved_radio_colors[get_radio_name(freq, null)];
            if (Dm.truthy(color))
                return color;
            return RADIO_COLOR_COMMON;
        }
        return "";
    }

    // tg: code/game/say.dm:343
    public static object attach_spans(object input = null, DmList spans = null)
    {
        return Dm.text("[][]</span>", message_spans_start(spans), input);
    }

    // tg: code/game/say.dm:346
    public static object message_spans_start(DmList spans = null)
    {
        string output = "<span class='";
        foreach (object S in Dm.each(spans))
            output = Dm.text("[][] ", output, S);
        output = Dm.text("[]'>", output);
        return output;
    }

    // tg: code/game/say.dm:353
    public static object say_test(object text = null)
    {
        string ending = copytext_char(text, -1);
        if (ending == "?")
            return "1";
        else if (ending == "!")
            return "2";
        return "0";
    }
}

public sealed partial class TgState
{
    // tg: code/game/say.dm:273  apply_message_emphasis's var/static/regex/emphasis_regex (built on the first call, as tg's)
    internal regex GlobalProcs__apply_message_emphasis__emphasis_regex;
}

//HACKY VIRTUALSPEAKER STUFF BEYOND THIS POINT
//these exist mostly to deal with the AIs hrefs and job stuff.

//VIRTUALSPEAKERS
// tg: code/game/say.dm:404  (M9 slice D: the radio's speakers)
public partial class atom_movable_virtualspeaker : atom_movable
{
    // tg: code/game/say.dm:405
    public object job;
    // tg: code/game/say.dm:406
    public atom_movable source;
    // tg: code/game/say.dm:407
    public obj_item_radio radio;

    // The New() that INITIALIZE_IMMEDIATE(/atom/movable/virtualspeaker) defines (__DEFINES/subsystems.dm:96; inline, rules 11.1 (c), (d))
    // tg: code/game/say.dm:409
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            DmList args = Dm.args(Dm.forward(__extra, loc));
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }

    // tg: code/game/say.dm:410
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom_movable M = Dm.arg(__extra, 0) as atom_movable;
        obj_item_radio _radio = Dm.arg(__extra, 1) as obj_item_radio;
        object dot = base.Initialize(mapload, __extra);
        radio = _radio;
        source = M;
        if (M != null)
        {
            name = Dm.truthy(radio?.anonymize) ? "Unknown" : Dm.str(M.get_voice(TRUE));
            gender = M.gender;
            verb_say = Dm.str(M.get_default_say_verb());
            verb_ask = M.verb_ask;
            verb_exclaim = M.verb_exclaim;
            verb_yell = M.verb_yell;
        }

        // The mob's job identity
        if (ishuman(M))
        {
            // Humans use their job as seen on the crew manifest. This is so the AI
            // can know their job even if they don't carry an ID.
            // GAP(lead): GLOB.manifest (the crew records, datacore) is not made until the records (M10); find_record reads its
            // lists, so without it there is no record
            datum_record_crew found_record = GLOB.manifest != null ? find_record(name) as datum_record_crew : null;
            if (found_record != null)
            {
                gender = found_record.get_byond_gender();
                job = found_record.rank;
            }
            else
                job = "Unknown";
        }
        else if (iscarbon(M))  // Carbon nonhuman
            job = "No ID";
        else if (isAI(M))  // AI
            job = "AI";
        else if (iscyborg(M))  // Cyborg
        {
            mob_living_silicon_robot B = (mob_living_silicon_robot)M;
            job = Dm.text("[] Cyborg", B.designation);
        }
        else if (ispAI(M))  // Personal AI (pAI)
            job = JOB_PERSONAL_AI;
        else if (isobj(M))  // Cold, emotionless machines
            job = "Machine";
        else  // Unidentifiable mob
            job = "Unknown";
        return dot;
    }

    // tg: code/game/say.dm:443
    public override object GetJob()
    {
        return job;
    }

    // tg: code/game/say.dm:446
    public override object GetSource()
    {
        return source;
    }

    // tg: code/game/say.dm:449
    public override object GetRadio()
    {
        return radio;
    }
}
