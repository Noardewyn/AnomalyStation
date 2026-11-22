// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/ai_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the AI's speech, whole: the tracking links in what it hears, its radio and holopad speech, and the VOX
// announcements (AI_VOX is defined).

//the following 2 procs are staying here because its for AI and its shells (also AI controlled)
// tg: code/modules/mob/living/silicon/ai/ai_say.dm:2
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:2
    public override object compose_track_href(atom_movable speaker = null, object message_langs = null, object raw_message = null, object radio_freq = null)
    {
        object namepart = message_langs; // tg's second parameter here is namepart
        if (!HAS_TRAIT(this, TRAIT_CAN_GET_AI_TRACKING_MESSAGE))
            return "";
        mob M = speaker.GetSource() as mob;
        if (M != null)
            return Dm.text("<a href='byond://?src=[];track=[]'>", REF(this), html_encode(namepart));
        return "";
    }

    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:10
    public override object compose_job(atom_movable speaker = null, object message_langs = null, object raw_message = null, object radio_freq = null)
    {
        //Also includes the </a> for AI hrefs, for convenience.
        if (!HAS_TRAIT(this, TRAIT_CAN_GET_AI_TRACKING_MESSAGE))
            return "";
        return Dm.text("[][]", Dm.truthy(radio_freq) ? " (" + Dm.str(speaker.GetJob()) + ")" : "", speaker.GetSource() != null ? "</a>" : "");
    }
}

// tg: code/modules/mob/living/silicon/ai/ai_say.dm:16
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:16
    public override async DmTask<object> try_speak(object message = null, float? ignore_spam = null, object forced = null, float? filterproof = null)
    {
        // AIs cannot speak if silent AI is on.
        // Unless forced is set, as that's probably stating laws or something.
        if (!Dm.truthy(forced) && Dm.truthy(config.Get(typeof(datum_config_entry_flag_silent_ai)))) /* CONFIG_GET(flag/silent_ai) */
        {
            to_chat(this, span_danger(I18n.T("The ability for AIs to speak is currently disabled via server config.")));
            return FALSE;
        }
        return await base.try_speak(message, ignore_spam, forced, filterproof);
    }

    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:24
    public override async DmTask<object> radio__proc(object message = null, DmList message_mods = null, DmList spans = null, object language = null)
    {
        message_mods ??= Dm.list();
        if (Dm.truthy(incapacitated))
            return FALSE;
        if (!Dm.truthy(radio_enabled)) //AI cannot speak if radio is disabled (via intellicard) or depowered.
        {
            to_chat(this, span_danger(I18n.T("Your radio transmitter is offline!")));
            return FALSE;
        }

        object dot = await base.radio__proc(message, message_mods, spans, language);
        if (Dm.truthy(dot))
        {
            do_tts_message(message, language, message_mods, Dm.list(), Dm.list());
            return dot;
        }

        if (Dm.truthy(message_mods[MODE_HEADSET]))
        {
            if (radio is obj_item_radio headset_radio)
            {
                headset_radio.talk_into(this, message, null, spans, language, message_mods);
                do_tts_message(message, language, message_mods, Dm.list(), Dm.list());
            }
            return NOPASS;
        }
        else if (Dm.@in(message_mods[RADIO_EXTENSION], GLOB.default_radio_channels))
        {
            if (radio is obj_item_radio channel_radio)
            {
                channel_radio.talk_into(this, message, message_mods[RADIO_EXTENSION], spans, language, message_mods);
                do_tts_message(message, language, message_mods, Dm.list(), Dm.list());
                return NOPASS;
            }
        }

        return FALSE;
    }

    //For holopads only. Usable by AI.
    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:51
    public virtual async DmTask holopad_talk(object message = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        message = trim(message);

        if (!Dm.truthy(message))
            return;

        // Only continue if there is a hologram and its master is the user.
        if (current is not obj_machinery_holopad active_pad || !Dm.truthy(active_pad.masters[this]))
        {
            to_chat(this, span_alert(I18n.T("No holopad connected.")));
            return;
        }

        obj_effect_overlay_holo_pad_hologram ai_holo = active_pad.masters[this] as obj_effect_overlay_holo_pad_hologram;
        turf pad_turf = get_turf(active_pad);
        object pad_loc = pad_turf != null ? AREACOORD(pad_turf) : "(UNKNOWN)";
        log_sayverb_talk(message, message_mods, tag: Dm.text("HOLOPAD in []", pad_loc));
        await ai_holo.say(message, spans: spans, sanitize: FALSE, language: language, message_mods: message_mods);
    }

    private const float VOX_DELAY = 600; // tg: ai_say.dm:73

    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:75  GAME_VERB_DESC(/mob/living/silicon/ai, announcement_help, "Announcement Help", ...): the verb proc
    public virtual void announcement_help(params object[] __extra)
    {
        Waiting.Detach(__gvb_announcement_help(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:75  the body
    public virtual async DmTask __gvb_announcement_help(DmList structured_args = null)
    {
        if (Dm.truthy(incapacitated))
            return;

        string dat = @"
	<font class='bad'>WARNING:</font> Misuse of the announcement system will get you job banned.<BR><BR>
	Here is a list of words you can type into the 'Announcement' button to create sentences to vocally announce to everyone on the same level at you.<BR>
	<UL><LI>You can also click on the word to PREVIEW it.</LI>
	<LI>You can only say 30 words for every announcement.</LI>
	<LI>Do not use punctuation as you would normally, if you want a pause you can use the full stop and comma characters by separating them with spaces, like so: 'Alpha . Test , Bravo'.</LI>
	<LI>Numbers are in word format, e.g. eight, sixty, etc </LI>
	<LI>Sound effects begin with an 's' before the actual word, e.g. scensor</LI>
	<LI>Use Ctrl+F to see if a word exists in the list.</LI></UL><HR>
	";

        float index = 0;
        foreach (object word in Dm.each(GLOB.vox_sounds))
        {
            index++;
            dat += Dm.text("<A href='byond://?src=[];say_word=[]'>[]</A>", REF(this), word, capitalize(word));
            if (index != GLOB.vox_sounds.len)
                dat += " / ";
        }

        datum_browser popup = Dm.@new<datum_browser>(this, "announce_help", "Announcement Help", 500, 400);
        popup.set_content(dat);
        popup.open();
    }

    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:101
    public virtual async DmTask announcement()
    {
        ref float announcing_vox = ref TgState.Current.mob_living_silicon_ai__announcing_vox; // var/static/announcing_vox: Stores the time of the last announcement
        if (announcing_vox > world.time)
        {
            to_chat(this, span_notice(I18n.T("Please wait [].", DisplayTimeText(announcing_vox - world.time))));
            return;
        }

        object message = await tgui_input_text(
            this,
            "WARNING: Misuse of this verb can result in you being job banned. More help is available in 'Announcement Help'",
            "Announcement",
            last_announcement,
            max_length: MAX_MESSAGE_LEN
        );

        if (!Dm.truthy(message) || TgState.Current.mob_living_silicon_ai__announcing_vox > world.time)
            return;

        last_announcement = Dm.str(message);

        if (Dm.truthy(incapacitated))
            return;

        if (Dm.truthy(control_disabled))
        {
            to_chat(this, span_warning(I18n.T("Wireless interface disabled, unable to interact with announcement PA.")));
            return;
        }

        DmList words = splittext(trim(message), " ") as DmList;
        DmList incorrect_words = Dm.list();

        if (words.len > 30)
            words.len = 30;

        foreach (object raw_word in Dm.each(words.Copy()))
        {
            object word = LOWER_TEXT(trim(raw_word));
            if (!Dm.truthy(word))
            {
                Dm.remove(ref words, raw_word);
                continue;
            }
            if (!Dm.truthy(GLOB.vox_sounds[word]))
                Dm.append(ref incorrect_words, word);
        }

        if (Dm.truthy(incorrect_words.len))
        {
            to_chat(this, span_notice(I18n.T("These words are not available on the announcement system: [].", english_list(incorrect_words))));
            return;
        }

        TgState.Current.mob_living_silicon_ai__announcing_vox = world.time + VOX_DELAY;

        log_message(Dm.text("made a vocal announcement with the following message: [].", message), LOG_GAME);
        log_talk(message, LOG_SAY, tag: "VOX Announcement");

        DmList players = Dm.list();
        turf ai_turf = get_turf(this);
        foreach (mob player_mob in Dm.each<mob>(GLOB.player_list))
        {
            turf player_turf = get_turf(player_mob);
            if (Dm.truthy(is_valid_z_level(ai_turf, player_turf)))
                Dm.append(ref players, player_mob);
        }

        minor_announce(capitalize(message), Dm.text("[] announces:", name), players: players, should_play_sound: Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.does_target_have_vox_off)));

        foreach (object word in Dm.each(words))
            await play_vox_word(word, ai_turf, null);
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:160
    public static async DmTask<object> play_vox_word(object word = null, turf ai_turf = null, mob only_listener = null)
    {
        word = LOWER_TEXT(word);

        if (Dm.truthy(GLOB.vox_sounds[word]))
        {
            object sound_file = GLOB.vox_sounds[word];

            // If there is no single listener, broadcast to everyone in the same z level
            if (only_listener == null)
            {
                // Play voice for all mobs in the z level
                foreach (mob player_mob in Dm.each<mob>(GLOB.player_list))
                {
                    object pref_volume = safe_read_pref(player_mob.client, typeof(datum_preference_numeric_volume_sound_ai_vox));
                    if (HAS_TRAIT(player_mob, TRAIT_DEAF) || !Dm.truthy(pref_volume))
                        continue;

                    turf player_turf = get_turf(player_mob);
                    if (!Dm.truthy(is_valid_z_level(ai_turf, player_turf)))
                        continue;

                    sound voice = Dm.sound(sound_file, wait: 1, channel: CHANNEL_VOX, volume: Dm.num(pref_volume));
                    voice.status = SOUND_STREAM;
                    Dm.output_to(player_mob, voice); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
                }
            }
            else
            {
                object pref_volume = safe_read_pref(only_listener.client, typeof(datum_preference_numeric_volume_sound_ai_vox));
                sound voice = Dm.sound(sound_file, wait: 1, channel: CHANNEL_VOX, volume: Dm.num(pref_volume));
                voice.status = SOUND_STREAM;
                Dm.output_to(only_listener, voice); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            }
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:187
    public static object does_target_have_vox_off(mob target = null)
    {
        return !Dm.truthy(safe_read_pref(target.client, typeof(datum_preference_numeric_volume_sound_ai_vox))) ? TRUE : FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/silicon/ai/ai_say.dm:102  var/static/announcing_vox
    internal float mob_living_silicon_ai__announcing_vox;
}
