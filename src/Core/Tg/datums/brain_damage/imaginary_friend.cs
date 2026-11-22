// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/imaginary_friend.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps B1 (D327): the whole file, the imaginary friend: the trauma polls a ghost for the friend, an invisible eye mob that only its
// owner (and the owner's other friends) see as an image of a random crewman, that talks and emotes to them alone, hides, joins its
// owner and points; the trapped owner's figment (the mind swap's victim). The var blocks are generated (D111). The friend's
// picture is get_flat_human_icon's flat icon of a dressed dummy (__HELPERS/icons.cs: the port flattens one direction, DV440).

// tg: code/datums/brain_damage/imaginary_friend.dm:6
public partial class datum_brain_trauma_special_imaginary_friend : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/imaginary_friend.dm:19
    public override async DmTask<object> on_gain()
    {
        mob_living M = owner;
        if (M.stat == DEAD || M.client == null)
            return FALSE;
        object dot = await base.on_gain();
        make_friend();
        await get_ghost();
        return dot;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:27
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (get_dist(owner, friend) > 9)
            await friend.recall();
        if (friend == null)
        {
            qdel(this);
            return;
        }
        if (friend.client == null && Dm.truthy(friend_initialized))
            _addtimer(Dm.@new<datum_callback>(this, nameof(reroll_friend)), 1 * MINUTES, file: "code/datums/brain_damage/imaginary_friend.dm", line: 34);
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:36
    public override void on_death()
    {
        base.on_death();
        qdel(this); //friend goes down with the ship
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:40
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        QDEL_NULL(ref friend);
    }

    //If the friend goes afk, make a brand new friend. Plenty of fish in the sea of imagination.
    // tg: code/datums/brain_damage/imaginary_friend.dm:45
    public virtual async DmTask reroll_friend()
    {
        if (friend.client != null) //reconnected
            return;
        friend_initialized = FALSE;
        QDEL_NULL(ref friend);
        make_friend();
        await get_ghost();
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:53
    public virtual void make_friend()
    {
        friend = Dm.@new<mob_eye_imaginary_friend>(get_turf(owner));
    }

    /// Tries a poll for the imaginary friend
    // tg: code/datums/brain_damage/imaginary_friend.dm:57
    public virtual async DmTask get_ghost()
    {
        mob chosen_one = await SSpolling.poll_ghosts_for_target(
            question: I18n.T("Do you want to play as [] []?", span_danger(Dm.text("[]'s", owner.real_name)), span_notice(I18n.T("imaginary friend"))),
            check_jobban: ROLE_PAI,
            poll_time: 20 * SECONDS,
            checked_target: owner,
            ignore_category: POLL_IGNORE_IMAGINARYFRIEND,
            alert_pic: owner,
            role_name_text: "imaginary friend") as mob;
        await add_friend(chosen_one as mob_dead_observer);
    }

    /// Yay more friends!
    // tg: code/datums/brain_damage/imaginary_friend.dm:70
    public virtual async DmTask add_friend(mob_dead_observer ghost = null)
    {
        if (ghost == null)
        {
            qdel(this);
            return;
        }

        friend.PossessByPlayer(ghost.ckey);
        await friend.attach_to_owner(owner);
        friend.setup_appearance();
        friend_initialized = TRUE;
        friend.log_message(Dm.text("became []'s split personality.", key_name(owner)), LOG_GAME);
        message_admins(Dm.text("[] became []'s split personality.", ADMIN_LOOKUPFLW(friend), ADMIN_LOOKUPFLW(owner)));
    }
}

// tg: code/datums/brain_damage/imaginary_friend.dm:82
public partial class mob_eye_imaginary_friend : mob_eye
{
    // tg: code/datums/brain_damage/imaginary_friend.dm:3  #define IMAGINARY_FRIEND_SPEECH_RANGE IMAGINARY_FRIEND_RANGE (9; #undef at :549)
    private const float IMAGINARY_FRIEND_SPEECH_RANGE = 9;
    // tg: code/datums/brain_damage/imaginary_friend.dm:4  #define IMAGINARY_FRIEND_EXTENDED_SPEECH_RANGE 999 (#undef at :550)
    private const float IMAGINARY_FRIEND_EXTENDED_SPEECH_RANGE = 999;

    // tg: code/datums/brain_damage/imaginary_friend.dm:126  Initialize's var/static/list/grantable_actions (rules 10.6)
    private static ref DmList Initialize__grantable_actions => ref TgState.Current.mob_eye_imaginary_friend__Initialize__grantable_actions;

    // tg: code/datums/brain_damage/imaginary_friend.dm:106
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        if (owner != null)
            greet();
        Show();
        return dot;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:114
    public virtual void greet()
    {
        to_chat(this, span_notice(I18n.T("<b>You are the imaginary friend of []!</b>", owner)));
        to_chat(this, span_notice(I18n.T("You are absolutely loyal to your friend, no matter what.")));
        to_chat(this, span_notice(I18n.T("You cannot directly influence the world around you, but you can see what [] cannot.", owner)));
    }

    /**
     * Arguments:
     * * imaginary_friend_owner - The living mob that owns the imaginary friend.
     * * appearance_from_prefs - If this is a valid set of prefs, the appearance of the imaginary friend is based on these prefs.
     */
    // tg: code/datums/brain_damage/imaginary_friend.dm:124
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        grant_actions_by_list(Initialize__grantable_actions);
        return dot;
    }

    /// Links this imaginary friend to the provided mob
    // tg: code/datums/brain_damage/imaginary_friend.dm:133
    public virtual async DmTask attach_to_owner(mob_living imaginary_friend_owner = null)
    {
        owner = imaginary_friend_owner;
        if (owner.imaginary_group == null)
            owner.imaginary_group = Dm.list(owner);
        Dm.append(ref owner.imaginary_group, this);
        greet();
        await DmTask.CompletedTask;
    }

    /// Copies appearance from passed player prefs, or randomises them if none are provided
    // tg: code/datums/brain_damage/imaginary_friend.dm:141
    public virtual void setup_appearance(datum_preferences appearance_from_prefs = null)
    {
        if (appearance_from_prefs != null)
            Waiting.Detach(setup_friend_from_prefs(appearance_from_prefs));
        else
            Waiting.Detach(setup_friend());
    }

    /// Randomise friend name and appearance
    // tg: code/datums/brain_damage/imaginary_friend.dm:148
    public virtual async DmTask setup_friend()
    {
        gender = Dm.str(pick(MALE, FEMALE));
        real_name = Dm.str(generate_random_name_species_based(gender, FALSE, typeof(datum_species_human)));
        name = Dm.str(real_name);
        human_icon = await get_flat_human_icon(null, pick(SSjob.joinable_occupations) as datum_job) as icon;
        Show();
    }

    /**
     * Sets up the imaginary friend's name and look using a set of datum preferences.
     *
     * Arguments:
     * * appearance_from_prefs - If this is a valid set of prefs, the appearance of the imaginary friend is based on the currently selected character in them. Otherwise, it's random.
     */
    // tg: code/datums/brain_damage/imaginary_friend.dm:161
    public virtual async DmTask setup_friend_from_prefs(datum_preferences appearance_from_prefs = null)
    {
        if (!istype(appearance_from_prefs, typeof(datum_preferences)))
        {
            stack_trace("Attempted to create imaginary friend appearance from null prefs. Using random appearance.", "code/datums/brain_damage/imaginary_friend.dm", 163);
            await setup_friend();
            return;
        }

        real_name = Dm.str(appearance_from_prefs.read_preference(typeof(datum_preference_name_real_name)));
        name = Dm.str(real_name);

        // Determine what job is marked as 'High' priority.
        datum_job appearance_job = null;
        float highest_pref = 0;
        foreach (object job in Dm.each(appearance_from_prefs.job_preferences))
        {
            float this_pref = Dm.num(appearance_from_prefs.job_preferences[job]);
            if (this_pref > highest_pref)
            {
                appearance_job = SSjob.get_job(job) as datum_job;
                highest_pref = this_pref;
            }
        }

        if (appearance_job == null)
            appearance_job = SSjob.get_job(JOB_ASSISTANT) as datum_job;

        if (istype(appearance_job, typeof(datum_job_ai)))
            human_icon = Dm.icon(Dm.resource("icons/mob/silicon/ai.dmi"), state: await resolve_ai_icon(appearance_from_prefs.read_preference(typeof(datum_preference_choiced_ai_core_display))), dir: SOUTH);
        else if (istype(appearance_job, typeof(datum_job_cyborg)))
            human_icon = Dm.icon(Dm.resource("icons/mob/silicon/robots.dmi"), state: "robot");
        else
            human_icon = await get_flat_human_icon(null, appearance_job, appearance_from_prefs) as icon;
        Show();
    }

    /// Returns all member clients of the imaginary_group
    // tg: code/datums/brain_damage/imaginary_friend.dm:191
    public virtual object group_clients()
    {
        DmList group_clients = Dm.list();
        foreach (mob person in Dm.each_cast<mob>(owner.imaginary_group))
        {
            if (person.client != null)
                Dm.append(group_clients, person.client);
        }
        return group_clients;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:198
    public virtual void Show()
    {
        if (client == null || owner == null) //nobody home
            return;

        DmList friend_clients = Dm.sub(group_clients(), this.client) as DmList;
        //Remove old image from group
        remove_image_from_clients(current_image, friend_clients);

        //Generate image from the static icon and the current dir
        current_image = image(human_icon, this, null, MOB_LAYER, dir: this.dir);
        current_image.@override = TRUE;
        current_image.name = name;
        if (Dm.truthy(hidden))
            current_image.alpha = 150;

        //Add new image to owner and friend
        if (!Dm.truthy(hidden))
            add_image_to_clients(current_image, friend_clients);

        Dm.combine(this.client.images, current_image);
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:219
    public override object Destroy(float? force = null)
    {
        if (owner?.client != null)
            Dm.remove(owner.client.images, current_image);
        if (client != null)
            Dm.remove(client.images, current_image);
        if (owner != null)
            Dm.remove(ref owner.imaginary_group, this);
        return base.Destroy(force);
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:225
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        if (Dm.truthy(safe_read_pref(client, typeof(datum_preference_toggle_enable_runechat))) && (Dm.truthy(safe_read_pref(client, typeof(datum_preference_toggle_enable_runechat_non_mobs))) || ismob(speaker)))
            create_chat_message(speaker, message_language as datum_language, raw_message, spans);
        to_chat(this, await compose_message(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods));
        return null;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:230
    public override async DmTask send_speech(object message = null, float? range = null, atom_movable source = null, object bubble_type = null, DmList spans = null, object message_language = null, DmList message_mods = null, object forced = null, object tts_message = null, DmList tts_filter = null)
    {
        range ??= IMAGINARY_FRIEND_SPEECH_RANGE;
        source ??= this;
        bubble_type ??= bubble_icon;
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        message = get_message_mods(message, message_mods);

        if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_ADMIN))
        {
            // tg: :234  SSadmin_verbs.dynamic_invoke_verb(client, /datum/admin_verb/cmd_admin_say, message): the admin tools (M10)
            Dm.Deferred("code/datums/brain_damage/imaginary_friend.dm:234", "send_speech: the admin channel (asay; the admin tools, M10+)");
            return;
        }

        if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_DEADMIN))
        {
            // tg: :238  SSadmin_verbs.dynamic_invoke_verb(client, /datum/admin_verb/dsay, message): the admin tools (M10)
            Dm.Deferred("code/datums/brain_damage/imaginary_friend.dm:238", "send_speech: the admins' dead chat (dsay; the admin tools, M10+)");
            return;
        }

        if (Dm.truthy(check_emote(message, forced)))
            return;

        message = check_for_custom_say_emote(message, message_mods);
        message = capitalize(message);

        if (Dm.truthy(message_mods[MODE_SING]))
        {
            object randomnote = pick("♩", "♪", "♫");
            message = Dm.text("[] [] []", randomnote, capitalize(message), randomnote);
            Dm.combine(spans, SPAN_SINGING);
        }

        if (Dm.truthy(extended_message_range))
            range = IMAGINARY_FRIEND_EXTENDED_SPEECH_RANGE;

        float eavesdrop_range = 0;

        if (!Dm.truthy(message_mods[MODE_CUSTOM_SAY_ERASE_INPUT]))
        {
            if (Dm.eq(message_mods[WHISPER_MODE], MODE_WHISPER))
            {
                Dm.combine(spans, SPAN_ITALICS);
                eavesdrop_range = EAVESDROP_RANGE;
                range = WHISPER_RANGE;
            }
        }

        log_sayverb_talk(message, message_mods, tag: "imaginary friend", forced_by: forced);

        object messagepart = generate_messagepart(message, spans, message_mods);
        string dead_rendered = Dm.text("[] []", span_name(I18n.T("[] (Imaginary friend of [])", name, owner)), messagepart);

        object language = message_language ?? owner.get_selected_language();
        await Hear(this, language, message, null, null, null, spans, message_mods); // We always hear what we say
        DmList group = Dm.sub(owner.imaginary_group, this) as DmList; // The people in our group don't, so we have to exclude ourselves not to hear twice
        foreach (mob person in Dm.each<mob>(group))
            await person.Hear(this, language, message, null, null, null, spans, message_mods, range);

        // Speech bubble, but only for those who have runechat off
        DmList speech_bubble_recipients = Dm.list();
        foreach (mob user in Dm.each_cast<mob>(Dm.add(group, this))) // Add ourselves back in
        {
            // SSlag_switch.measures[DISABLE_RUNECHAT]: the lag switch is the admin tools (M10), never on (as chatmessage.cs)
            if (Dm.truthy(safe_read_pref(user.client, typeof(datum_preference_toggle_enable_runechat))))
                Dm.append(speech_bubble_recipients, user.client);
        }

        image bubble = image(Dm.resource("icons/mob/effects/talk.dmi"), this, Dm.text("[][]", bubble_type, say_test(message)), FLY_LAYER);
        SET_PLANE_EXPLICIT(bubble, ABOVE_GAME_PLANE, this);
        bubble.appearance_flags = APPEARANCE_UI_IGNORE_ALPHA;
        Waiting.DetachSync(() => flick_overlay_global(bubble, speech_bubble_recipients, 3 * SECONDS));
        LAZYADD(ref update_on_z, bubble);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_saypopup), bubble), 3.5f * SECONDS, file: "code/datums/brain_damage/imaginary_friend.dm", line: 285);

        turf center_turf = get_turf(this);
        if (center_turf == null)
            return;

        foreach (mob dead_player in Dm.each<mob>(GLOB.dead_mob_list))
        {
            if (dead_player.z != z || get_dist(this, dead_player) > 7)
            {
                if (eavesdrop_range != 0)
                {
                    if (!Dm.truthy(Dm.bitand(get_chat_toggles(dead_player.client), CHAT_GHOSTWHISPER)))
                        continue;
                }
                else if (!Dm.truthy(Dm.bitand(get_chat_toggles(dead_player.client), CHAT_GHOSTEARS)))
                    continue;
            }
            string link = FOLLOW_LINK(dead_player, owner);
            to_chat(dead_player, Dm.text("[] []", link, dead_rendered));
        }
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:301
    public virtual void clear_saypopup(image say_popup = null)
    {
        LAZYREMOVE(ref update_on_z, say_popup);
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:304
    public override async DmTask<object> whisper(object message = null, object bubble_type = null, DmList spans = null, float? sanitize = null, object language = null, float? ignore_spam = null, object forced = null, float? filterproof = null)
    {
        spans ??= Dm.list();
        sanitize ??= TRUE;
        ignore_spam ??= FALSE;
        if (!Dm.truthy(message))
            return null;
        await say(Dm.text("#[]", message), bubble_type, spans, sanitize, language as datum_language, ignore_spam, forced, filterproof);
        return null;
    }

    // Another snowflake proc, when will they end... should have refactored it differently
    // tg: code/datums/brain_damage/imaginary_friend.dm:385
    public override async DmTask<object> point_at(atom pointed_atom = null, float? intentional = null, Type pointer_type = null)
    {
        if (!isturf(loc))
            return null;

        if (Dm.@in(pointed_atom, this))
        {
            create_point_bubble(pointed_atom);
            return null;
        }

        turf tile = get_turf(pointed_atom);
        if (tile == null)
            return null;

        turf our_tile = get_turf(this);
        image visual = image(Dm.resource("icons/hud/screen_gen.dmi"), our_tile, "arrow", FLY_LAYER);

        Waiting.DetachSync(() => flick_overlay_global(visual, group_clients() as DmList, 2.5f * SECONDS));
        animate(visual, Dm.named("pixel_x", (tile.x - our_tile.x) * ICON_SIZE_X + pointed_atom.pixel_x), Dm.named("pixel_y", (tile.y - our_tile.y) * ICON_SIZE_Y + pointed_atom.pixel_y), Dm.named("time", 1.7f), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:403
    public override object create_thinking_indicator()
    {
        if (active_thinking_indicator != null || active_typing_indicator != null || !HAS_TRAIT(this, TRAIT_THINKING_IN_CHARACTER))
            return FALSE;
        active_thinking_indicator = image(Dm.resource("icons/mob/effects/talk.dmi"), this, Dm.text("[]3", bubble_icon), TYPING_LAYER);
        add_image_to_clients(active_thinking_indicator as image, group_clients() as DmList);
        return null;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:409
    public override object remove_thinking_indicator()
    {
        if (active_thinking_indicator == null)
            return FALSE;
        remove_image_from_clients(active_thinking_indicator, group_clients() as DmList);
        active_thinking_indicator = null;
        return null;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:415
    public override object create_typing_indicator()
    {
        if (active_typing_indicator != null || active_thinking_indicator != null || !HAS_TRAIT(this, TRAIT_THINKING_IN_CHARACTER))
            return FALSE;
        active_typing_indicator = image(Dm.resource("icons/mob/effects/talk.dmi"), this, Dm.text("[]0", bubble_icon), TYPING_LAYER);
        add_image_to_clients(active_typing_indicator as image, group_clients() as DmList);
        return null;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:421
    public override object remove_typing_indicator()
    {
        if (active_typing_indicator == null)
            return FALSE;
        remove_image_from_clients(active_typing_indicator, group_clients() as DmList);
        active_typing_indicator = null;
        return null;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:427
    public override object remove_all_indicators()
    {
        REMOVE_TRAIT(this, TRAIT_THINKING_IN_CHARACTER, CURRENTLY_TYPING_TRAIT);
        remove_thinking_indicator();
        remove_typing_indicator();
        return null;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:432
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        direction ??= 0;
        if (world.time < move_delay)
            return FALSE;
        await setDir(direction);
        if (get_dist(this, owner) > distance_allowance || (Dm.truthy(require_los) && !Dm.truthy(can_see(owner, this, distance_allowance))))
        {
            await recall();
            move_delay = world.time + 10;
            return FALSE;
        }
        await abstract_move(newloc);
        move_delay = world.time + 1;
        return null;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:443
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        Show(); // The image does not actually update until Show() gets called
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:447
    public virtual async DmTask<object> recall()
    {
        if (owner == null || loc == owner)
            return FALSE;
        await abstract_move(owner);
        return null;
    }
}

// tg: code/datums/brain_damage/imaginary_friend.dm:309
public partial class datum_emote_imaginary_friend : datum_emote
{
    // We have to create our own since we can only show emotes to ourselves and our owner
    // tg: code/datums/brain_damage/imaginary_friend.dm:313
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        intentional ??= FALSE;
        user.log_talk(message, LOG_EMOTE);
        if (!Dm.truthy(await can_run_emote(user, FALSE, intentional)))
            return FALSE;

        object msg = select_message_type(user, message, intentional);
        if (Dm.truthy(@params) && Dm.truthy(message_param))
            msg = select_param(user, @params);

        msg = replace_pronoun(user, msg);

        if (!Dm.truthy(msg))
            return TRUE;

        mob_eye_imaginary_friend friend = (mob_eye_imaginary_friend)user;
        string dchatmsg = Dm.text("[] []", span_bold(I18n.T("[] (Imaginary friend of [])", friend, friend.owner)), msg);
        message = Dm.text("[] []", span_name(Dm.text("[]", user)), msg);

        turf user_turf = get_turf(user);
        if (user.client != null)
        {
            foreach (mob ghost in Dm.each_cast<mob>(GLOB.dead_mob_list))
            {
                if (ghost.client == null || isnewplayer(ghost))
                    continue;
                if (Dm.truthy(Dm.bitand(get_chat_toggles(ghost.client), CHAT_GHOSTSIGHT)) && !Dm.@in(ghost, viewers(user_turf, null)))
                    ghost.show_message(Dm.text("[] []", FOLLOW_LINK(ghost, user), dchatmsg));
            }
        }

        foreach (mob person in Dm.each<mob>(friend.owner.imaginary_group))
        {
            to_chat(person, message);
            if (Dm.truthy(safe_read_pref(person.client, typeof(datum_preference_toggle_enable_runechat))))
                person.create_chat_message(friend, raw_message: msg, runechat_flags: EMOTE_MESSAGE);
        }
        return TRUE;
    }
}

// tg: code/datums/brain_damage/imaginary_friend.dm:345
public partial class datum_emote_imaginary_friend_point : datum_emote_imaginary_friend
{
    // tg: code/datums/brain_damage/imaginary_friend.dm:351
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        message_param = Dm.initial(this, (datum_emote p) => p.message_param); // reset
        return await base.run_emote(user, @params, type_override, intentional);
    }
}

// tg: code/datums/brain_damage/imaginary_friend.dm:355
public partial class datum_emote_imaginary_friend_custom : datum_emote_imaginary_friend
{
    // tg: code/datums/brain_damage/imaginary_friend.dm:360
    public override async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        return Dm.num(Dm.truthy(await base.can_run_emote(user, status_check, intentional, @params)) && Dm.truthy(intentional));
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:363
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        intentional ??= FALSE;
        if (!Dm.truthy(await can_run_emote(user, TRUE, intentional)))
            return FALSE;
        // tg: :366-368  is_banned_from(user.ckey, "Emote"): the port has no ban database (as mob_say.cs), nobody is banned
        else if (QDELETED(user))
            return FALSE;
        // tg: :371-373  user.client.prefs.muted & MUTE_IC: the admin mutes are M10's admin tools; nobody is muted
        else if (!Dm.truthy(@params))
            message = copytext(sanitize(await Dm.input("Choose an emote to display.")), 1, MAX_MESSAGE_LEN);
        else
            message = Dm.str(@params);
        object dot = await base.run_emote(user, @params, type_override, intentional);
        message = null;
        return dot;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:381
    public override object replace_pronoun(mob user = null, object msg = null)
    {
        return msg;
    }
}

// tg: code/datums/brain_damage/imaginary_friend.dm:452
public partial class datum_action_innate_imaginary_join : datum_action_innate
{
    // tg: code/datums/brain_damage/imaginary_friend.dm:460
    public override async DmTask<object> Activate()
    {
        mob_eye_imaginary_friend I = (mob_eye_imaginary_friend)owner;
        await I.recall();
        return null;
    }
}

// tg: code/datums/brain_damage/imaginary_friend.dm:464
public partial class datum_action_innate_imaginary_hide : datum_action_innate
{
    // tg: code/datums/brain_damage/imaginary_friend.dm:472
    public virtual async DmTask update_status()
    {
        mob_eye_imaginary_friend I = (mob_eye_imaginary_friend)owner;
        if (Dm.truthy(I.hidden))
        {
            name = I18n.T("Show");
            desc = I18n.T("Become visible to your owner.");
            button_icon_state = "unhide";
        }
        else
        {
            name = I18n.T("Hide");
            desc = I18n.T("Hide yourself from your owner's sight.");
            button_icon_state = "hide";
        }
        await build_all_button_icons();
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:484
    public override async DmTask<object> Activate()
    {
        mob_eye_imaginary_friend fake_friend = (mob_eye_imaginary_friend)owner;
        fake_friend.hidden = Dm.num(!Dm.truthy(fake_friend.hidden));
        fake_friend.Show();
        await build_all_button_icons(Dm.bitor(UPDATE_BUTTON_NAME, UPDATE_BUTTON_ICON));
        return null;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:490
    public override void update_button_name(atom_movable_screen_movable_action_button button = null, float? force = null, params object[] __extra)
    {
        mob_eye_imaginary_friend fake_friend = (mob_eye_imaginary_friend)owner;
        if (Dm.truthy(fake_friend.hidden))
        {
            name = I18n.T("Show");
            desc = I18n.T("Become visible to your owner.");
        }
        else
        {
            name = I18n.T("Hide");
            desc = I18n.T("Hide yourself from your owner's sight.");
        }
        base.update_button_name(button, force, __extra);
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:500
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        force ??= FALSE;
        mob_eye_imaginary_friend fake_friend = (mob_eye_imaginary_friend)owner;
        if (Dm.truthy(fake_friend.hidden))
            button_icon_state = "unhide";
        else
            button_icon_state = "hide";

        base.apply_button_icon(current_button, force);
    }
}

//down here is the trapped mind
//like imaginary friend but a lot less imagination and more like mind prison//

// tg: code/datums/brain_damage/imaginary_friend.dm:512
public partial class datum_brain_trauma_special_imaginary_friend_trapped_owner : datum_brain_trauma_special_imaginary_friend
{
    // tg: code/datums/brain_damage/imaginary_friend.dm:520
    public override void make_friend()
    {
        friend = Dm.@new<mob_eye_imaginary_friend_trapped>(get_turf(owner), this);
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:523
    public override async DmTask reroll_friend() //no rerolling- it's just the last owner's hell
    {
        if (friend.client != null) //reconnected
            return;
        friend_initialized = FALSE;
        QDEL_NULL(ref friend);
        qdel(this);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:530
    public override async DmTask get_ghost() //no randoms
    {
        await DmTask.CompletedTask;
        return;
    }
}

// tg: code/datums/brain_damage/imaginary_friend.dm:533
public partial class mob_eye_imaginary_friend_trapped : mob_eye_imaginary_friend
{
    // tg: code/datums/brain_damage/imaginary_friend.dm:538
    public override void greet()
    {
        to_chat(this, span_notice(span_bold(I18n.T("You have managed to hold on as a figment of the new host's imagination!"))));
        to_chat(this, span_notice(I18n.T("All hope is lost for you, but at least you may interact with your host. You do not have to be loyal to them.")));
        to_chat(this, span_notice(I18n.T("You cannot directly influence the world around you, but you can see what the host cannot.")));
    }

    // tg: code/datums/brain_damage/imaginary_friend.dm:543
    public override async DmTask setup_friend()
    {
        real_name = I18n.T("[]?", owner.real_name);
        name = Dm.str(real_name);
        human_icon = Dm.icon(Dm.resource("icons/mob/simple/lavaland/lavaland_monsters.dmi"), state: "curseblob");
        await DmTask.CompletedTask;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/brain_damage/imaginary_friend.dm:126  /mob/eye/imaginary_friend/Initialize's var/static/list/grantable_actions
    internal DmList mob_eye_imaginary_friend__Initialize__grantable_actions = Dm.list(
        typeof(datum_action_innate_imaginary_join),
        typeof(datum_action_innate_imaginary_hide));
}
