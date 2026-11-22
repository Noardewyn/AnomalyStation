// Translated from tgstation (snapshot 1d99bd7f): code/datums/emotes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice B (docs/ai/specs/social.md). Left out with a comment: text to
// speech's pitch (D254: SStts is off, so the vary branch picks the frequency, as in tg without a TTS server) and the ghosts' follow link
// (FOLLOW_LINK: orbiting is outside D151's scope, as in observer_say.cs). The AI's camera relay came with the AI (silicons SI4).

// tg: code/datums/emotes.dm:13
public partial class datum_emote : datum
{
    // tg: code/datums/emotes.dm:83
    public override async DmTask New(params object[] __extra)
    {
        if (Dm.eq(mob_type_allowed_typecache, typeof(mob)))
            mob_type_allowed_typecache = GLOB.typecache_mob;
        else if (Dm.eq(mob_type_allowed_typecache, typeof(mob_living)))
            mob_type_allowed_typecache = GLOB.typecache_living;
        else
            mob_type_allowed_typecache = typecacheof(mob_type_allowed_typecache);

        mob_type_blacklist_typecache = typecacheof(mob_type_blacklist_typecache) as DmList;
        mob_type_ignore_stat_typecache = typecacheof(mob_type_ignore_stat_typecache) as DmList;

        if (!Dm.truthy(name))
            name = key;
    }

    /**
     * Handles the modifications and execution of emotes.
     *
     * Arguments:
     * * user - Person that is trying to send the emote.
     * * params - Parameters added after the emote.
     * * type_override - Override to the current emote_type.
     * * intentional - Bool that says whether the emote was forced (FALSE) or not (TRUE).
     *
     */
    // tg: code/datums/emotes.dm:109
    public virtual async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        intentional ??= FALSE;
        object msg = select_message_type(user, message, intentional);
        if (Dm.truthy(@params))
        {
            if (Dm.truthy(message_param))
                msg = select_param(user, @params);
            else
                msg = @params;
        }

        msg = replace_pronoun(user, msg);
        if (!Dm.truthy(msg))
            return null;

        /// Use the type override if it exists
        object running_emote_type = Dm.truthy(type_override) ? type_override : emote_type;

        if (user.client != null)
            user.log_message(msg, LOG_EMOTE);

        object tmp_sound = get_sound(user as mob_living);
        if (Dm.truthy(tmp_sound) && Dm.truthy(should_play_sound(user, intentional)))
        {
            if (Dm.truthy(intentional))
            {
                if (Dm.truthy(TIMER_COOLDOWN_RUNNING(user, MANUAL_GENERAL_EMOTE_AUDIO_COOLDOWN)) || Dm.truthy(TIMER_COOLDOWN_RUNNING(user, MANUAL_SPECIFIC_EMOTE_AUDIO_COOLDOWN(type)))
                    || Dm.truthy(TIMER_COOLDOWN_RUNNING(user, FORCED_GENERAL_EMOTE_AUDIO_COOLDOWN)) || Dm.truthy(TIMER_COOLDOWN_RUNNING(user, FORCED_SPECIFIC_EMOTE_AUDIO_COOLDOWN(type))))
                {
                    user.balloon_alert(user, I18n.T("emote on cooldown!")); // DEVIATION(DV164): so is a sound emote inside the sounds' cooldowns
                    return FALSE;
                }
            }
            else
            {
                if (Dm.truthy(TIMER_COOLDOWN_RUNNING(user, FORCED_GENERAL_EMOTE_AUDIO_COOLDOWN)) || Dm.truthy(TIMER_COOLDOWN_RUNNING(user, FORCED_SPECIFIC_EMOTE_AUDIO_COOLDOWN(type))))
                    return FALSE;
            }
            // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
            LAZYSET(ref user.cooldowns, MANUAL_SPECIFIC_EMOTE_AUDIO_COOLDOWN(type), _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), user, MANUAL_SPECIFIC_EMOTE_AUDIO_COOLDOWN(type)), manual_specific_emote_audio_cooldown, file: "code/datums/emotes.dm", line: 137));
            LAZYSET(ref user.cooldowns, MANUAL_GENERAL_EMOTE_AUDIO_COOLDOWN, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), user, MANUAL_GENERAL_EMOTE_AUDIO_COOLDOWN), manual_general_emote_audio_cooldown, file: "code/datums/emotes.dm", line: 138));
            LAZYSET(ref user.cooldowns, FORCED_SPECIFIC_EMOTE_AUDIO_COOLDOWN(type), _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), user, FORCED_SPECIFIC_EMOTE_AUDIO_COOLDOWN(type)), forced_specific_emote_audio_cooldown, file: "code/datums/emotes.dm", line: 139));
            LAZYSET(ref user.cooldowns, FORCED_GENERAL_EMOTE_AUDIO_COOLDOWN, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), user, FORCED_GENERAL_EMOTE_AUDIO_COOLDOWN), forced_general_emote_audio_cooldown, file: "code/datums/emotes.dm", line: 140));

            object frequency = null;
            // D310: the speaker's TTS pitch shifts the emote's sound, as tg's with TTS on
            if (Dm.truthy(affected_by_pitch) && Dm.truthy(SStts?.tts_enabled) && Dm.truthy(SStts?.pitch_enabled))
                frequency = Dm.num(rand(MIN_EMOTE_PITCH, MAX_EMOTE_PITCH)) * (1 + MathF.Sqrt(MathF.Abs(Dm.num(user.pitch))) * MathF.Sign(Dm.num(user.pitch)) * EMOTE_TTS_PITCH_MULTIPLIER);
            else if (Dm.truthy(vary))
                frequency = rand(MIN_EMOTE_PITCH, MAX_EMOTE_PITCH);
            if (Dm.truthy(use_sound_tokens) && Dm.truthy(sound_wall_ignore))
                playsoundtoken(source: user, soundin: tmp_sound, range: SOUND_RANGE, volume: sound_volume);
            else
                playsound(source: user, soundin: tmp_sound, vol: sound_volume, vary: FALSE, ignore_walls: sound_wall_ignore, frequency: frequency);
        }

        object is_important = Dm.bitand(running_emote_type, EMOTE_IMPORTANT);
        object is_visual = Dm.bitand(running_emote_type, EMOTE_VISIBLE);
        object is_audible = Dm.bitand(running_emote_type, EMOTE_AUDIBLE);
        object additional_message_flags = get_message_flags(intentional);

        // Emote doesn't get printed to chat, runechat only
        if (Dm.truthy(Dm.bitand(running_emote_type, EMOTE_RUNECHAT)))
        {
            foreach (mob viewer in Dm.each<mob>(viewers(user)))
            {
                if (viewer.client == null)
                    continue;
                if (!Dm.truthy(is_important) && viewer != user && (!Dm.truthy(is_visual) || !Dm.truthy(is_audible)))
                {
                    if (Dm.truthy(is_audible) && HAS_TRAIT(viewer, TRAIT_DEAF))
                        continue;
                    if (Dm.truthy(is_visual) && Dm.truthy(viewer.is_blind()))
                        continue;
                }
                if (Dm.truthy(user.runechat_prefs_check(viewer, EMOTE_MESSAGE)))
                {
                    viewer.create_chat_message(
                        speaker: user,
                        raw_message: msg,
                        runechat_flags: EMOTE_MESSAGE);
                }
                else if (Dm.truthy(is_important))
                    to_chat(viewer, span_emote(I18n.T("<b>[]</b> []", user, msg)));
                else if (Dm.truthy(is_audible) && Dm.truthy(is_visual))
                {
                    viewer.show_message(
                        span_emote(I18n.T("<b>[]</b> []", user, msg)), MSG_AUDIBLE,
                        span_emote(I18n.T("You see how <b>[]</b> []", user, msg)), MSG_VISUAL);
                }
                else if (Dm.truthy(is_audible))
                    viewer.show_message(span_emote(I18n.T("<b>[]</b> []", user, msg)), MSG_AUDIBLE);
                else if (Dm.truthy(is_visual))
                    viewer.show_message(span_emote(I18n.T("<b>[]</b> []", user, msg)), MSG_VISUAL);
            }

            // AI-eye emotes
            if (Dm.truthy(is_visual))
                relay_visual_emote_to_ai_runechat(user, msg);

            return null; // Early exit so no dchat message
        }

        // The emote has some important information, and should always be shown to the user
        else if (Dm.truthy(is_important))
        {
            foreach (mob viewer in Dm.each<mob>(viewers(user)))
            {
                to_chat(viewer, span_emote(I18n.T("<b>[]</b> []", user, msg)));
                if (Dm.truthy(user.runechat_prefs_check(viewer, EMOTE_MESSAGE)))
                {
                    viewer.create_chat_message(
                        speaker: user,
                        raw_message: msg,
                        runechat_flags: EMOTE_MESSAGE);
                }
            }
        }
        // Emotes has both an audible and visible component
        // Prioritize audible, and provide a visible message if the user is deaf
        else if (Dm.truthy(is_visual) && Dm.truthy(is_audible))
        {
            user.audible_message(
                message: msg,
                deaf_message: span_emote(I18n.T("You see how <b>[]</b> []", user, msg)),
                self_message: msg,
                audible_message_flags: Dm.num(Dm.bitor(EMOTE_MESSAGE | ALWAYS_SHOW_SELF_MESSAGE, additional_message_flags)));
        }
        // Emote is entirely audible, no visible component
        else if (Dm.truthy(is_audible))
        {
            user.audible_message(
                message: msg,
                self_message: msg,
                audible_message_flags: Dm.num(Dm.bitor(EMOTE_MESSAGE, additional_message_flags)));
        }
        // Emote is entirely visible, no audible component
        else if (Dm.truthy(is_visual))
        {
            user.visible_message(
                message: msg,
                self_message: msg,
                visible_message_flags: Dm.num(Dm.bitor(EMOTE_MESSAGE | ALWAYS_SHOW_SELF_MESSAGE, additional_message_flags)));
        }
        else
            throw Dm.CRASH(Dm.text("Emote [] has no valid emote type set!", type), "code/datums/emotes.dm", 239);

        if (Dm.truthy(is_visual))
            relay_visual_emote_to_ai_runechat(user, msg);

        if (user.client != null)
        {
            object dchatmsg = I18n.T("<b>[]</b> []", user, msg);
            foreach (mob ghost in Dm.each<mob>(Dm.sub(GLOB.dead_mob_list, viewers(get_turf(user)))))
            {
                if (ghost.client == null || isnewplayer(ghost))
                    continue;
                if (!Dm.truthy(Dm.bitand(get_chat_toggles(ghost.client), CHAT_GHOSTSIGHT)))
                    continue;
                // tg: :253  FOLLOW_LINK(ghost, user) in front: orbiting is outside D151's scope (see the file comment)
                to_chat(ghost, span_emote(Dm.str(dchatmsg)));
            }
        }

        return null;
    }

    /**
     * For handling emote cooldown, return true to allow the emote to happen.
     *
     * Arguments:
     * * user - Person that is trying to send the emote.
     * * intentional - Bool that says whether the emote was forced (FALSE) or not (TRUE).
     *
     * Returns FALSE if the cooldown is not over, TRUE if the cooldown is over.
     */
    // tg: code/datums/emotes.dm:251
    public virtual async DmTask<object> check_cooldown(mob user = null, object intentional = null)
    {
        if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_EMOTE_COOLDOWN_CHECK])
            && Dm.truthy(Dm.bitand(user._SendSignal(COMSIG_MOB_EMOTE_COOLDOWN_CHECK, Dm.list(user, key, intentional)), COMPONENT_EMOTE_COOLDOWN_BYPASS)))
            intentional = FALSE;

        if (!Dm.truthy(intentional))
            return TRUE;

        if (Dm.truthy(user.emotes_used) && Dm.num(user.emotes_used[this]) + cooldown > Dm.num(world.time))
        {
            if (cooldown > Dm.initial(typeof(datum_emote), (datum_emote default_emote) => default_emote.cooldown)) // only worry about longer-than-normal emotes
                to_chat(user, span_danger(I18n.T("You must wait another [] before using that emote.", DisplayTimeText(Dm.num(user.emotes_used[this]) - Dm.num(world.time) + cooldown))));
            user.balloon_alert(user, I18n.T("emote on cooldown!")); // DEVIATION(DV164): the player is told the emote is still on its cooldown
            return FALSE;
        }
        if (!Dm.truthy(user.emotes_used))
            user.emotes_used = Dm.list();
        user.emotes_used[this] = world.time;
        return TRUE;
    }

    /**
     * To get the sound that the emote plays, for special sound interactions depending on the mob.
     *
     * Arguments:
     * * user - Person that is trying to send the emote.
     *
     * Returns the sound that will be made while sending the emote.
     */
    // tg: code/datums/emotes.dm:277
    public virtual object get_sound(mob_living user = null)
    {
        DmList comsig_sounds = Dm.list();
        if (Dm.truthy(user?._listen_lookup?[COMSIG_MOB_EMOTE_SOUND(key)]))
            user._SendSignal(COMSIG_MOB_EMOTE_SOUND(key), Dm.list(user, key, comsig_sounds));
        if (Dm.truthy(length(comsig_sounds)))
        {
            object chosen_sound = null;
            object highest_priority = 0;
            foreach (object candidate in Dm.each(comsig_sounds))
            {
                object priority = comsig_sounds[candidate];
                if (Dm.gt(priority, highest_priority))
                {
                    highest_priority = priority;
                    chosen_sound = candidate;
                }
            }
            return chosen_sound;
        }
        object sound_or_sounds = is_type_in_list(user, sounds_by_mobtype, zebra: TRUE);
        if (Dm.truthy(sound_or_sounds))
            return get_emote_sound_from_list(sound_or_sounds, user);
        return sound;
    }

    /**
     * To get the flags visible/audible messages for ran by the emote.
     *
     * Arguments:
     * * intentional - Bool that says whether the emote was forced (FALSE) or not (TRUE).
     *
     * Returns the additional message flags we should be using, if any.
     */
    // tg: code/datums/emotes.dm:302
    public virtual object get_message_flags(object intentional = null)
    {
        // If we did it, we most often already know what's in it, so we try to avoid highlight clutter.
        return Dm.truthy(intentional) ? BLOCK_SELF_HIGHLIGHT_MESSAGE : NONE;
    }

    /**
     * To replace pronouns in the inputed string with the user's proper pronouns.
     *
     * Arguments:
     * * user - Person that is trying to send the emote.
     * * msg - The string to modify.
     *
     * Returns the modified msg string.
     */
    // tg: code/datums/emotes.dm:315
    public virtual object replace_pronoun(mob user = null, object msg = null)
    {
        if (Dm.truthy(findtext(msg, "their")))
            msg = replacetext(msg, "their", user.p_their());
        if (Dm.truthy(findtext(msg, "them")))
            msg = replacetext(msg, "them", user.p_them());
        if (Dm.truthy(findtext(msg, "they")))
            msg = replacetext(msg, "they", user.p_they());
        if (Dm.truthy(findtext(msg, "%s")))
            msg = replacetext(msg, "%s", user.p_s());
        return msg;
    }

    /**
     * Selects the message type to override the message with.
     *
     * Arguments:
     * * user - Person that is trying to send the emote.
     * * msg - The string to modify.
     * * intentional - Bool that says whether the emote was forced (FALSE) or not (TRUE).
     *
     * Returns the new message, or msg directly, if no change was needed.
     */
    // tg: code/datums/emotes.dm:336
    public virtual object select_message_type(mob user = null, object msg = null, object intentional = null)
    {
        // Basically, we don't care that the others can use datum variables, because they're never going to change.
        object dot = msg;
        if (!isliving(user))
            return dot;
        var living_user = (mob_living)user;

        if (HAS_MIND_TRAIT(user, TRAIT_MIMING) && Dm.truthy(message_mime))
            dot = message_mime;
        if (isalienadult(user) && Dm.truthy(message_alien))
            dot = message_alien;
        else if (islarva(user) && Dm.truthy(message_larva))
            dot = message_larva;
        else if (isAI(user) && Dm.truthy(message_AI))
            dot = message_AI;
        else if (HAS_TRAIT(user, TRAIT_LESSER_HUMANOID) && Dm.truthy(message_monkey))
            dot = message_monkey;
        else if ((iscyborg(user) || Dm.truthy(Dm.bitand(living_user.mob_biotypes, MOB_ROBOTIC))) && Dm.truthy(message_robot))
            dot = message_robot;
        else if (isanimal_or_basicmob(user) && Dm.truthy(message_animal_or_basic))
            dot = message_animal_or_basic;

        return dot;
    }

    /**
     * Replaces the %t in the message in message_param by params.
     *
     * Arguments:
     * * user - Person that is trying to send the emote.
     * * params - Parameters added after the emote.
     *
     * Returns the modified string.
     */
    // tg: code/datums/emotes.dm:369
    public virtual object select_param(mob user = null, object @params = null)
    {
        return replacetext(message_param, "%t", @params);
    }

    /**
     * Check to see if the user is allowed to run the emote.
     *
     * Arguments:
     * * user - Person that is trying to send the emote.
     * * status_check - Bool that says whether we should check their stat or not.
     * * intentional - Bool that says whether the emote was forced (FALSE) or not (TRUE).
     * * params - Parameters added after the emote.
     *
     * Returns a bool about whether or not the user can run the emote.
     */
    // tg: code/datums/emotes.dm:383
    public virtual async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        status_check ??= TRUE;
        intentional ??= FALSE;
        if (Dm.truthy(trait_required) && !HAS_TRAIT(user, trait_required))
            return FALSE;
        if (!Dm.truthy(is_type_in_typecache(user, mob_type_allowed_typecache)))
            return FALSE;
        if (Dm.truthy(is_type_in_typecache(user, mob_type_blacklist_typecache)))
            return FALSE;
        if (Dm.truthy(status_check) && !Dm.truthy(is_type_in_typecache(user, mob_type_ignore_stat_typecache)))
        {
            if (IS_UNCONSCIOUS(user) && !Dm.truthy(Dm.bitand(can_use_flags, EMOTE_CANUSE_UNCONSCIOUS)))
            {
                if (Dm.truthy(intentional))
                    to_chat(user, span_warning(I18n.T("You cannot [] while unconscious!", key)));
                return FALSE;
            }
            if (HAS_TRAIT(user, TRAIT_HANDS_BLOCKED) && Dm.truthy(Dm.bitand(can_use_flags, EMOTE_CANUSE_REQUIRE_HANDS)))
            {
                if (Dm.truthy(intentional))
                    to_chat(user, span_warning(I18n.T("You cannot use your hands to [] right now!", key)));
                return FALSE;
            }

            switch (user.stat)
            {
                case SOFT_CRIT:
                    if (!Dm.truthy(Dm.bitand(can_use_flags, EMOTE_CANUSE_SOFTCRIT)))
                    {
                        if (Dm.truthy(intentional))
                            to_chat(user, span_warning(I18n.T("You cannot [] while in a critical condition!", key)));
                        return FALSE;
                    }
                    break;
                case HARD_CRIT:
                    if (!Dm.truthy(Dm.bitand(can_use_flags, EMOTE_CANUSE_HARDCRIT)))
                    {
                        if (Dm.truthy(intentional))
                            to_chat(user, span_warning(I18n.T("You cannot [] while in a critical condition!", key)));
                        return FALSE;
                    }
                    break;
                case DEAD:
                    if (!Dm.truthy(Dm.bitand(can_use_flags, EMOTE_CANUSE_DEAD)))
                    {
                        if (Dm.truthy(intentional))
                            to_chat(user, span_warning(I18n.T("You cannot [] while dead!", key)));
                        return FALSE;
                    }
                    break;
            }
        }

        if (HAS_TRAIT(user, TRAIT_EMOTEMUTE))
            return FALSE;

        return TRUE;
    }

    /**
     * Check to see if the user should play a sound when performing the emote.
     *
     * Arguments:
     * * user - Person that is doing the emote.
     * * intentional - Bool that says whether the emote was forced (FALSE) or not (TRUE).
     *
     * Returns a bool about whether or not the user should play a sound when performing the emote.
     */
    // tg: code/datums/emotes.dm:431
    public virtual object should_play_sound(mob user = null, float? intentional = null)
    {
        intentional ??= FALSE;
        if (Dm.truthy(Dm.bitand(emote_type, EMOTE_AUDIBLE)) && !Dm.truthy(hands_use_check))
        {
            if (HAS_TRAIT(user, TRAIT_MUTE) || HAS_MIND_TRAIT(user, TRAIT_MIMING))
                return FALSE;
            if (iscarbon(user) && !Dm.truthy(user.get_organ_slot(ORGAN_SLOT_TONGUE)))
                return FALSE;
        }

        if (Dm.truthy(only_forced_audio) && Dm.truthy(intentional))
            return FALSE;
        return TRUE;
    }
}

public partial class atom : datum
{
    /**
    * Allows the intrepid coder to send a basic emote
    * Takes text as input, sends it out to those who need to know after some light parsing
    * If you need something more complex, make it into a datum emote
    * Arguments:
    * * text - The text to send out
    *
    * Returns TRUE if it was able to run the emote, FALSE otherwise.
    */
    // tg: code/datums/emotes.dm:451
    public virtual object manual_emote(object text = null, float? log_emote = null)
    {
        log_emote ??= TRUE;
        if (!Dm.truthy(text))
            throw Dm.CRASH("Someone passed nothing to manual_emote(), fix it", "code/datums/emotes.dm", 453);

        if (Dm.truthy(log_emote))
            log_message(text, LOG_EMOTE);
        visible_message(text, visible_message_flags: EMOTE_MESSAGE);

        return TRUE;
    }
}

public partial class mob : atom_movable
{
    // tg: code/datums/emotes.dm:461
    public override object manual_emote(object text = null, float? log_emote = null)
    {
        object dot = null;
        if (IS_UNCONSCIOUS_OR_CRIT(this))
            return FALSE;
        if (log_emote == null)
            log_emote = Dm.num(client != null);
        dot = base.manual_emote(text, log_emote);
        if (!Dm.truthy(dot))
            return FALSE;

        relay_visual_emote_to_ai_runechat(this, text);

        if (client == null)
            return TRUE;
        object ghost_text = I18n.T("<b>[]</b> []", this, text);
        object origin_turf = get_turf(this);
        foreach (mob ghost in Dm.each<mob>(GLOB.dead_mob_list))
        {
            if (ghost.client == null || isnewplayer(ghost))
                continue;
            // tg: :479  FOLLOW_LINK(ghost, src) in front: orbiting is outside D151's scope (see the file comment)
            if (Dm.truthy(Dm.bitand(get_chat_toggles(ghost.client), CHAT_GHOSTSIGHT)) && !Dm.@in(ghost, viewers(origin_turf, null)))
                ghost.show_message(ghost_text);
        }
        return TRUE;
    }
}

public static partial class GlobalProcs
{
    /// AI can also see emotes!
    // tg: code/datums/emotes.dm:483
    public static object ai_eye_turf_in_view(mob_eye_camera_ai eye = null, turf target_turf = null)
    {
        if (eye == null || target_turf == null)
            return FALSE;

        turf eye_turf = get_turf(eye);
        if (eye_turf == null || eye_turf.z != target_turf.z)
            return FALSE;

        if (SScameras == null || !SScameras.is_visible_by_cameras(eye_turf) || !SScameras.is_visible_by_cameras(target_turf))
            return FALSE;

        return Dm.@in(target_turf, eye.get_visible_turfs()) ? TRUE : FALSE;
    }

    // tg: code/datums/emotes.dm:496
    public static void relay_visual_emote_to_ai_runechat(mob user = null, object msg = null)
    {
        turf user_turf = get_turf(user);
        if (user_turf == null)
            return;

        foreach (mob_living_silicon_ai AI in Dm.each_cast<mob_living_silicon_ai>(GLOB.ai_list))
        {
            if (AI?.client == null)
                continue;

            if (user.invisibility > AI.see_invisible)
                continue;

            if (Dm.@in(AI, viewers(user))) // Avoid duplicates if the AI is nearby
                continue;

            bool relayed = false;

            object active_eye = AI.client.eye;
            if (active_eye is mob_eye_camera_ai ai_eye)
            {
                if (ai_eye.ai == AI && Dm.truthy(ai_eye_turf_in_view(ai_eye, user_turf)))
                {
                    to_chat(AI, span_emote(Dm.text("You see how <b>[]</b> []", user, msg)));

                    if (Dm.truthy(user.runechat_prefs_check(AI, EMOTE_MESSAGE)))
                        AI.create_chat_message(speaker: user, raw_message: msg, runechat_flags: EMOTE_MESSAGE);
                    relayed = true;
                }
            }

            if (!relayed && Dm.truthy(AI.multicam_on)) // Multicam
            {
                foreach (mob_eye_camera_ai other_eye in Dm.each_cast<mob_eye_camera_ai>(AI.all_eyes))
                {
                    if (Dm.truthy(ai_eye_turf_in_view(other_eye, user_turf)))
                    {
                        to_chat(AI, span_emote(Dm.text("You see how <b>[]</b> []", user, msg)));

                        if (Dm.truthy(user.runechat_prefs_check(AI, EMOTE_MESSAGE)))
                            AI.create_chat_message(speaker: user, raw_message: msg, runechat_flags: EMOTE_MESSAGE);
                        break;
                    }
                }
            }
        }
    }
}
