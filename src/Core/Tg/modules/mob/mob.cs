// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/mob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, per the per-proc plan of package C (wave 4, scope.md section 1 C, risk R11): the life cycle (Destroy,
// New, Initialize, GenerateTag), the HUD image bookkeeping (over the shim's minimal /image, lead decision D3), the
// incapacitation, nutrition, stat and HUD-trait procs, spin and the small defaults are translated; every leaf that
// /mob/Initialize reaches is translated or deferred, so a mob without a client initializes without a runtime. Procs of
// later milestones are Dm.NotTranslated with the milestone named (the messages and runechat are M9's: audible_message and the
// runechat preference checks, DV161), examine and eye contact (M2), hands and inventory (M2), magic resistance effects (M9), respawn
// (M10), names (fully_replace_character_name: M2 F1) and records (M9), writing and reading tools (M9), VV and Topic (rules
// 13). The client parts (reset_perspective, become_uncliented, update_mouse_pointer, clear_from_recent_examines, the
// persistent client in Destroy) are translated with the client layer (M1 P1). Verbs (GAME_VERB*) are not declared: verbs are translated with the player command layer (rules
// 7.10). Interleaved types are mirrored as alternating blocks in tg's order (rules 1.9). The ghosts (2026-10-16, D151): the
// mind's current in Destroy, get_ghost, grab_ghost and the mind's name in fully_replace_character_name.

// tg: code/modules/mob/mob.dm:26
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob.dm:880  GAME_VERB(/mob, cancel_camera, "Cancel Camera View", "OOC")
    public virtual async DmTask cancel_camera(params object[] __extra)
    {
        await reset_perspective(null);
    }

    // tg: code/modules/mob/mob.dm:880  the verb's body, which the stat panel's "Cancel Camera View" runs through SSverbs.invoke_verb
    public virtual async DmTask __gvb_cancel_camera(DmList structured_args = null)
    {
        await reset_perspective(null);
    }

    /**
     * Delete a mob
     *
     * Removes mob from the following global lists
     * * GLOB.mob_list
     * * GLOB.dead_mob_list
     * * GLOB.alive_mob_list
     * * GLOB.all_clockwork_mobs
     *
     * Unsets the focus var
     *
     * Clears alerts for this mob
     *
     * Resets all the observers perspectives to the tile this mob is on
     *
     * qdels any client colours in place on this mob
     *
     * Clears any refs to the mob inside its current location
     *
     * Ghostizes the client attached to this mob
     *
     * If our mind still exists, clear its current var to prevent harddels
     *
     * Parent call
     */
    // tg: code/modules/mob/mob.dm:26
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(client))
            stack_trace("Mob with client has been deleted.", "code/modules/mob/mob.dm", 28);
        else if (Dm.truthy(ckey) && !IS_FAKE_KEY(ckey)) // FUCK YOU AGHOST CODE FUCK YOU
            stack_trace(Dm.text("Mob without client but with associated ckey, [], has been deleted.", ckey), "code/modules/mob/mob.dm", 30);

        persistent_client?.set_mob(null);

        remove_from_mob_list();
        remove_from_dead_mob_list();
        remove_from_alive_mob_list();
        remove_from_mob_suicide_list();
        focus = null;
        if (Dm.truthy(length(progressbars)))
        {
            stack_trace(Dm.text("[] destroyed with elements in its progressbars list", this), "code/modules/mob/mob.dm", 40);
            progressbars = null;
        }
        // M2 P2 (integration with P3's alerts): a dying human leaves its alerts, which clear_alert (alert.dm:79) now removes.
        foreach (object alert in Dm.each(alerts))
            clear_alert(alert, TRUE);
        if (Dm.truthy(observers?.len))
        {
            foreach (mob_dead observe in Dm.each_cast<mob_dead>(observers))
                Waiting.NoSleep(observe.reset_perspective(null));
        }

        qdel(hud_used);
        QDEL_LIST(client_colours);
        ghostize(can_reenter_corpse: FALSE); //False, since we're deleting it currently
        if (mind?.current == this) //Let's just be safe yeah? This will occasionally be cleared, but not always. Can't do it with ghostize without changing behavior
            mind.set_current(null);

        if (Dm.truthy(mock_client))
            mock_client.mob = null; // M8 slice E: the mock client of tg's unequip_defib test (its var is generated)

        return base.Destroy(force);
    }

    // tg: code/modules/mob/mob.dm:59
    public override async DmTask New(params object[] __extra)
    {
        // This needs to happen IMMEDIATELY. I'm sorry :(
        GenerateTag();
        await base.New(__extra);
    }

    /**
     * Intialize a mob
     *
     * Sends global signal COMSIG_GLOB_MOB_CREATED
     *
     * Adds to global lists
     * * GLOB.mob_list
     * * GLOB.dead_mob_list - if mob is dead
     * * GLOB.alive_mob_list - if the mob is alive
     *
     * Other stuff:
     * * Sets the mob focus to itself
     * * Generates huds
     * * If there are any global alternate apperances apply them to this mob
     * * set a random nutrition level
     * * Intialize the movespeed of the mob
     */
    // tg: code/modules/mob/mob.dm:81
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_MOB_CREATED, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_MOB_CREATED]))
            SSdcs._SendSignal(COMSIG_GLOB_MOB_CREATED, Dm.list(SSdcs, this));
        add_to_mob_list();
        if (stat == DEAD)
            add_to_dead_mob_list();
        else
            add_to_alive_mob_list();
        update_incapacitated();
        Waiting.NoSleep(set_focus(this));
        prepare_huds();
        foreach (datum_atom_hud_alternate_appearance alt_hud in Dm.each_cast<datum_atom_hud_alternate_appearance>(GLOB.active_alternate_appearances))
        {
            alt_hud.apply_to_new_mob(this);
        }

        Waiting.NoSleep(set_nutrition(rand(NUTRITION_LEVEL_START_MIN, NUTRITION_LEVEL_START_MAX)));
        dot = base.Initialize(mapload, __extra);
        setup_hud_traits();
        Waiting.NoSleep(update_config_movespeed());
        Waiting.NoSleep(initialize_actionspeed());
        // DM passes TRUE; /mob/proc/update_movespeed declares no parameter (modules/movespeed/_movespeed_modifier.dm:175),
        // so the extra argument is dropped (rules 7.3).
        update_movespeed();
        become_hearing_sensitive();
        log_mob_tag(Dm.text("TAG: [] CREATED: [] \\[[]\\]", tag, key_name(this), type));
        return dot;
    }

    /**
     * Generate the tag for this mob
     *
     * This is simply "mob_"+ a global incrementing counter that goes up for every mob
     */
    // tg: code/modules/mob/mob.dm:108
    public override void GenerateTag()
    {
        base.GenerateTag();
        tag = Dm.text("mob_[]", next_mob_id++);
    }

    /// Assigns a (c)key to this mob.
    // tg: code/modules/mob/mob.dm:113
    [ShouldNotOverride]
    public virtual void PossessByPlayer(object ckey = null)
    {
        if (ckey == null)
            return;

        if (!istext(ckey))
            throw Dm.CRASH("Tried to assign a mob a non-text ckey, wtf?!", "code/modules/mob/mob.dm", 119);

        // BYOND moves the player's client into this mob when its ckey is set (the engine part, Builtin/mob.builtin.cs).
        this.ckey = Dm.ckey(ckey);
    }

    // tg: code/modules/mob/mob.dm:123
    public override object serialize_list(DmList options = null, DmList semvers = null)
    {
        Dm.NotTranslated("code/modules/mob/mob.dm:123", "serialize_list: datum serialization has no M0 consumer");
        return null;
    }
}

// tg: code/modules/mob/mob.dm:142  (/atom procs defined in mob.dm)
public partial class atom : datum
{
    /**
     * set every hud image in the given category active so other people with the given hud can see it.
     * Arguments:
     * * hud_category - the index in our active_hud_list corresponding to an image now being shown.
     * * update_huds - if FALSE we will just put the hud_category into active_hud_list without actually updating the atom_hud datums subscribed to it
     * * exclusive_hud - if given a reference to an atom_hud, will just update that hud instead of all global ones attached to that category.
     * This is because some atom_hud subtypes arent supposed to work via global categories, updating normally would affect all of these which we dont want.
     */
    // tg: code/modules/mob/mob.dm:142
    public virtual object set_hud_image_active(object hud_category = null, float? update_huds = null, datum_atom_hud exclusive_hud = null)
    {
        update_huds ??= TRUE;
        if (!istext(hud_category) || !Dm.truthy(hud_list?[hud_category]) || Dm.truthy(active_hud_list?[hud_category]))
            return FALSE;

        LAZYSET(ref active_hud_list, hud_category, hud_list[hud_category]);

        if (!Dm.truthy(update_huds))
            return TRUE;

        if (Dm.truthy(exclusive_hud))
            exclusive_hud.add_single_hud_category_on_atom(this, hud_category);
        else
        {
            foreach (datum_atom_hud hud_to_update in Dm.each_cast<datum_atom_hud>(GLOB.huds_by_category[hud_category]))
                hud_to_update.add_single_hud_category_on_atom(this, hud_category);
        }

        return TRUE;
    }

    ///sets every hud image in the given category inactive so no one can see it
    // tg: code/modules/mob/mob.dm:160
    public virtual object set_hud_image_inactive(object hud_category = null, float? update_huds = null, datum_atom_hud exclusive_hud = null)
    {
        update_huds ??= TRUE;
        if (!istext(hud_category))
            return FALSE;

        if (!Dm.truthy(update_huds))
        {
            LAZYREMOVE(ref active_hud_list, hud_category);
            return TRUE;
        }

        if (Dm.truthy(exclusive_hud))
            exclusive_hud.remove_single_hud_category_on_atom(this, hud_category);
        else
        {
            foreach (datum_atom_hud hud_to_update in Dm.each_cast<datum_atom_hud>(GLOB.huds_by_category[hud_category]))
                hud_to_update.remove_single_hud_category_on_atom(this, hud_category);
        }

        LAZYREMOVE(ref active_hud_list, hud_category);

        return TRUE;
    }

    /**
     * Prepare the huds for this atom
     *
     * Goes through hud_possible list and adds the images to the hud_list variable (if not already cached)
     */
    // tg: code/modules/mob/mob.dm:183
    public virtual void prepare_huds()
    {
        if (Dm.truthy(hud_list)) // I choose to be lenient about people calling this proc more then once
            return;
        hud_list = Dm.list();
        foreach (object hud in Dm.each(hud_possible))
        {
            object hint = hud_possible[hud];

            if (Dm.eq(hint, HUD_LIST_LIST))
                hud_list[hud] = Dm.list();

            else
            {
                // DEFAULT_HUDS_DMI (__DEFINES/atom_hud.dm:75) is a resource, expanded inline (rules 3.7)
                image I = image(Dm.resource("icons/mob/huds/hud.dmi"), this, "");
                I.appearance_flags = Dm.bitor(Dm.bitor(RESET_COLOR, PIXEL_SCALE), KEEP_APART);
                hud_list[hud] = I;
            }
            set_hud_image_active(hud, update_huds: FALSE); //by default everything is active. but dont add it to huds to keep control.
        }
    }
}

// tg: code/modules/mob/mob.dm:202
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob.dm:202  GAME_VERB_PROC(/mob, Cell, "Cell", "Admin"): not declared, verbs are translated with
    // the player command layer (rules 7.10).

    // tg: code/modules/mob/mob.dm:221  /mob/proc/get_photo_description(obj/item/camera/camera): not declared, the type of
    // its parameter (/obj/item/camera, modules/photography) is not ported yet (photography, M9).

    /**
     * Show a message to this mob (visual or audible)
     */
    // tg: code/modules/mob/mob.dm:227
    public virtual object show_message(object msg = null, object type = null, object alt_msg = null, object alt_type = null, float? avoid_highlighting = null)//Message, type of message (1 or 2), alternative message, alt message type (1 or 2)
    {
        avoid_highlighting ??= FALSE;
        if (client == null)
            return FALSE;

        msg = copytext_char(msg, 1, MAX_MESSAGE_LEN);

        // Return TRUE if we sent the original msg, otherwise return FALSE
        object dot = null;
        dot = TRUE;
        if (Dm.truthy(type))
        {
            if (Dm.truthy(Dm.bitand(type, MSG_VISUAL)) && Dm.truthy(is_blind()))//Vision related
            {
                if (!Dm.truthy(alt_msg))
                    return FALSE;
                else
                {
                    msg = alt_msg;
                    type = alt_type;
                    dot = FALSE;
                }
            }

            if (Dm.truthy(Dm.bitand(type, MSG_AUDIBLE)) && HAS_TRAIT(this, TRAIT_DEAF))//Hearing related
            {
                if (!Dm.truthy(alt_msg))
                    return FALSE;
                else
                {
                    msg = alt_msg;
                    type = alt_type;
                    dot = FALSE;
                    if (Dm.truthy(Dm.bitand(type, MSG_VISUAL)) && Dm.truthy(is_blind()))
                        return FALSE;
                }
            }
        }
        // voice muffling
        if (IS_UNCONSCIOUS(this))
        {
            if (Dm.truthy(Dm.bitand(type, MSG_AUDIBLE))) //audio
                to_chat(this, "<I>... You can almost hear something ...</I>");
            return FALSE;
        }
        to_chat(this, msg, avoid_highlighting: avoid_highlighting);
        return dot;
    }
}

// tg: code/modules/mob/mob.dm:280
public partial class atom : datum
{
    /**
     * Generate a visible message from this atom
     *
     * Show a message to all player mobs who sees this atom
     *
     * Show a message to the src mob (if the src is a mob)
     *
     * Use for atoms performing visible actions
     *
     * message is output to anyone who can see, e.g. `"The [src] does something!"`
     *
     * Vars:
     * * message is the message output to anyone who can see.
     * * self_message (optional) is what the src mob sees e.g. "You do something!"
     * * blind_message (optional) is what blind people will hear e.g. "You hear something!"
     * * vision_distance (optional) define how many tiles away the message can be seen.
     * * ignored_mobs (optional) doesn't show any message to any mob in this list.
     * * visible_message_flags (optional) is the type of message being sent.
     */
    // tg: code/modules/mob/mob.dm:280
    public virtual void visible_message(object message = null, object self_message = null, object blind_message = null, float? vision_distance = null, object ignored_mobs = null, float? visible_message_flags = null)
    {
        vision_distance ??= DEFAULT_MESSAGE_RANGE;
        visible_message_flags ??= NONE;
        turf T = get_turf(this) as turf;
        if (T == null)
            return;

        DmList ignored = ignored_mobs as DmList;
        if (ignored == null)
            ignored = ignored_mobs == null ? Dm.list() : Dm.list(ignored_mobs); // list(null) holds a null, which no hearer is
        DmList hearers = mob_only_listeners(get_hearers_in_view(vision_distance, this) as DmList) as DmList; //caches the hearers and then removes ignored mobs.
        Dm.remove(ref hearers, ignored);

        object raw_msg = message;
        if (Dm.truthy(Dm.bitand(visible_message_flags, WITH_EMPHASIS_MESSAGE)))
            message = apply_message_emphasis(message);
        if (Dm.truthy(Dm.bitand(visible_message_flags, EMOTE_MESSAGE)))
            message = span_emote(Dm.text("<b>[]</b> []", this, message));

        foreach (mob hearing_mob in Dm.each_cast<mob>(hearers))
        {
            if (hearing_mob?.client == null)
                continue;
            if (Dm.truthy(self_message) && hearing_mob == this)
                continue;

            //This entire if/else chain could be in two lines but isn't for readibilties sake.
            object msg = message;
            float msg_type = MSG_VISUAL;

            if (hearing_mob.see_invisible < invisibility)//if src is invisible to M
            {
                msg = blind_message;
                msg_type = MSG_AUDIBLE;
            }
            else if (T != loc && T != this) //if src is inside something and not a turf.
            {
                if (hearing_mob != loc) // Only give the blind message to hearers that aren't the location
                {
                    msg = blind_message;
                    msg_type = MSG_AUDIBLE;
                }
            }
            else if (!HAS_TRAIT(hearing_mob, TRAIT_HEAR_THROUGH_DARKNESS) && hearing_mob.lighting_cutoff < LIGHTING_CUTOFF_HIGH && Dm.truthy(T.is_softly_lit()) && !in_range(T, hearing_mob)) //if it is too dark, unless we're right next to them.
            {
                msg = blind_message;
                msg_type = MSG_AUDIBLE;
            }
            if (!Dm.truthy(msg))
                continue;

            if (Dm.truthy(Dm.bitand(visible_message_flags, EMOTE_MESSAGE)) && Dm.truthy(runechat_prefs_check(hearing_mob, visible_message_flags)) && !Dm.truthy(hearing_mob.is_blind()))
                hearing_mob.create_chat_message(this as atom_movable, raw_message: raw_msg, runechat_flags: visible_message_flags);

            hearing_mob.show_message(msg, msg_type, blind_message, MSG_AUDIBLE);
        }
    }
}

// tg: code/modules/mob/mob.dm:325
public partial class mob : atom_movable
{
    ///Adds the functionality to self_message.
    // tg: code/modules/mob/mob.dm:325
    public override void visible_message(object message = null, object self_message = null, object blind_message = null, float? vision_distance = null, object ignored_mobs = null, float? visible_message_flags = null)
    {
        vision_distance ??= DEFAULT_MESSAGE_RANGE;
        visible_message_flags ??= NONE;
        base.visible_message(message, self_message, blind_message, vision_distance, ignored_mobs, visible_message_flags);
        if (!Dm.truthy(self_message))
            return;
        object raw_self_message = self_message;
        object self_runechat = FALSE;
        object block_self_highlight = Dm.bitand(visible_message_flags, BLOCK_SELF_HIGHLIGHT_MESSAGE);
        if (Dm.truthy(Dm.bitand(visible_message_flags, WITH_EMPHASIS_MESSAGE)))
            self_message = apply_message_emphasis(self_message);
        if (Dm.truthy(Dm.bitand(visible_message_flags, EMOTE_MESSAGE)))
            self_message = span_emote(Dm.text("<b>[]</b> []", this, self_message)); // May make more sense as "You do x"

        if (Dm.truthy(Dm.bitand(visible_message_flags, ALWAYS_SHOW_SELF_MESSAGE)))
        {
            to_chat(this, self_message, avoid_highlighting: Dm.num_or_null(block_self_highlight));
            self_runechat = TRUE;
        }
        else
            self_runechat = show_message(self_message, MSG_VISUAL, blind_message, MSG_AUDIBLE, avoid_highlighting: Dm.num_or_null(block_self_highlight));

        if (Dm.truthy(self_runechat) && Dm.truthy(Dm.bitand(visible_message_flags, EMOTE_MESSAGE)) && Dm.truthy(runechat_prefs_check(this, visible_message_flags)))
            create_chat_message(this, raw_message: raw_self_message, runechat_flags: visible_message_flags);
    }
}

// tg: code/modules/mob/mob.dm:358
public partial class atom : datum
{
    /**
     * Show a message to all mobs in earshot of this atom
     *
     * Use for objects performing audible actions
     *
     * vars:
     * * message is the message output to anyone who can hear.
     * * deaf_message (optional) is what deaf people will see.
     * * hearing_distance (optional) is the range, how many tiles away the message can be heard.
     * * self_message (optional) is what the src mob hears.
     * * audible_message_flags (optional) is the type of message being sent.
     */
    // tg: code/modules/mob/mob.dm:358
    public virtual void audible_message(object message = null, object deaf_message = null, float? hearing_distance = null, object self_message = null, float? audible_message_flags = null)
    {
        hearing_distance ??= DEFAULT_MESSAGE_RANGE;
        audible_message_flags ??= NONE;
        DmList hearers = mob_only_listeners(get_hearers_in_view(hearing_distance, this) as DmList) as DmList;
        object raw_msg = message;
        if (Dm.truthy(Dm.bitand(audible_message_flags, WITH_EMPHASIS_MESSAGE)))
            message = apply_message_emphasis(message);
        if (Dm.truthy(Dm.bitand(audible_message_flags, EMOTE_MESSAGE)))
            message = span_emote(Dm.text("<b>[]</b> []", this, message));
        foreach (mob hearing_mob in Dm.each_cast<mob>(hearers))
        {
            if (hearing_mob?.client == null)
                continue;
            if (Dm.truthy(self_message) && hearing_mob == this)
                continue;
            if (Dm.truthy(Dm.bitand(audible_message_flags, EMOTE_MESSAGE)) && Dm.truthy(runechat_prefs_check(hearing_mob, audible_message_flags)) && !HAS_TRAIT(hearing_mob, TRAIT_DEAF))
                hearing_mob.create_chat_message(this as atom_movable, raw_message: raw_msg, runechat_flags: audible_message_flags);
            hearing_mob.show_message(message, MSG_AUDIBLE, deaf_message, MSG_VISUAL);
        }
    }
}

// tg: code/modules/mob/mob.dm:385
public partial class mob : atom_movable
{
    /**
     * Show a message to all mobs in earshot of this one
     *
     * This would be for audible actions by the src mob
     *
     * vars:
     * * message is the message output to anyone who can hear.
     * * self_message (optional) is what the src mob hears.
     * * deaf_message (optional) is what deaf people will see.
     * * hearing_distance (optional) is the range, how many tiles away the message can be heard.
     */
    // tg: code/modules/mob/mob.dm:385
    public override void audible_message(object message = null, object deaf_message = null, float? hearing_distance = null, object self_message = null, float? audible_message_flags = null)
    {
        hearing_distance ??= DEFAULT_MESSAGE_RANGE;
        audible_message_flags ??= NONE;
        base.audible_message(message, deaf_message, hearing_distance, self_message, audible_message_flags);
        if (!Dm.truthy(self_message))
            return;
        object raw_self_message = self_message;
        object self_runechat = FALSE;
        object block_self_highlight = Dm.bitand(audible_message_flags, BLOCK_SELF_HIGHLIGHT_MESSAGE);
        if (Dm.truthy(Dm.bitand(audible_message_flags, WITH_EMPHASIS_MESSAGE)))
            self_message = apply_message_emphasis(self_message);
        if (Dm.truthy(Dm.bitand(audible_message_flags, EMOTE_MESSAGE)))
            self_message = span_emote(Dm.text("<b>[]</b> []", this, self_message));

        if (Dm.truthy(Dm.bitand(audible_message_flags, ALWAYS_SHOW_SELF_MESSAGE)))
        {
            to_chat(this, self_message, avoid_highlighting: Dm.num_or_null(block_self_highlight));
            self_runechat = TRUE;
        }
        else
            self_runechat = show_message(self_message, MSG_AUDIBLE, deaf_message, MSG_VISUAL, avoid_highlighting: Dm.num_or_null(block_self_highlight));

        if (Dm.truthy(self_runechat) && Dm.truthy(Dm.bitand(audible_message_flags, EMOTE_MESSAGE)) && Dm.truthy(runechat_prefs_check(this, audible_message_flags)))
            create_chat_message(this, raw_message: raw_self_message, runechat_flags: audible_message_flags);
    }
}

// tg: code/modules/mob/mob.dm:409
public partial class atom_movable : atom
{
    /// Gets a linked mob, letting atoms act as proxies for actions that rely on hearing sensitivity.
    /// For example, AIs hearing around their holopads, and dullahans hearing around their heads.
    /// Normal say messages are handled by Hear(), this is for other visible/audible messages
    // tg: code/modules/mob/mob.dm:409
    public virtual object get_listening_mob()
    {
        return null;
    }
}

// tg: code/modules/mob/mob.dm:412  (silicons SI4: the holopads' holograms)
public partial class obj_effect_overlay_holo_pad_hologram : obj_effect_overlay
{
    // tg: code/modules/mob/mob.dm:412
    public override object get_listening_mob()
    {
        return Impersonation;
    }
}

// tg: code/modules/mob/mob.dm:415  /obj/item/dullahan_relay/get_listening_mob(): not declared, the dullahans (M2) are not ported yet.

// tg: code/modules/mob/mob.dm:418
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob.dm:418
    public override object get_listening_mob()
    {
        return this;
    }
}

// tg: code/modules/mob/mob.dm:422
public partial class atom : datum
{
    ///Returns the client runechat visible messages preference according to the message type.
    // tg: code/modules/mob/mob.dm:422
    // DEVIATION(DV161): the preferences read as their defaults (safe_read_pref); tg's target.client?.prefs is FALSE without a client.
    public virtual object runechat_prefs_check(mob target = null, float? visible_message_flags = null)
    {
        visible_message_flags ??= NONE;
        if (!Dm.truthy(safe_read_pref(target.client, typeof(datum_preference_toggle_enable_runechat))))
            return FALSE;
        if (!Dm.truthy(safe_read_pref(target.client, typeof(datum_preference_toggle_enable_runechat_non_mobs))))
            return FALSE;
        if (Dm.truthy(Dm.bitand(visible_message_flags, EMOTE_MESSAGE)) && !Dm.truthy(safe_read_pref(target.client, typeof(datum_preference_toggle_see_rc_emotes))))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/mob/mob.dm:431
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob.dm:431
    public override object runechat_prefs_check(mob target = null, float? visible_message_flags = null)
    {
        visible_message_flags ??= NONE;
        if (!Dm.truthy(safe_read_pref(target.client, typeof(datum_preference_toggle_enable_runechat))))
            return FALSE;
        if (Dm.truthy(Dm.bitand(visible_message_flags, EMOTE_MESSAGE)) && !Dm.truthy(safe_read_pref(target.client, typeof(datum_preference_toggle_see_rc_emotes))))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/mob/mob.dm:438
public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/mob/mob.dm:438
    public override object runechat_prefs_check(mob target = null, float? visible_message_flags = null)
    {
        visible_message_flags ??= NONE;
        if (!Dm.truthy(base.runechat_prefs_check(target, visible_message_flags)))
            return FALSE;
        if (!Dm.truthy(safe_read_pref(target.client, typeof(datum_preference_toggle_enable_runechat_ghosts))))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/mob/mob.dm:446
public partial class mob : atom_movable
{
    ///Get the item on the mob in the storage slot identified by the id passed in
    // tg: code/modules/mob/mob.dm:446
    public virtual object get_item_by_slot(object slot_id = null)
    {
        return null;
    }

    // tg: code/modules/mob/mob.dm:449
    public virtual object get_items_by_slots(object slot_ids = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (object slot_id in Dm.each(bitfield_to_list(Dm.num_or_null(slot_ids))))
            Dm.append(ref dot, get_item_by_slot(slot_id));
        return dot;
    }

    /// Gets what slot the item on the mob is held in.
    /// Returns null if the item isn't in any slots on our mob.
    /// Does not check if the passed item is null, which may result in unexpected outcoms.
    // tg: code/modules/mob/mob.dm:457
    public virtual object get_slot_by_item(obj_item looking_for = null)
    {
        if (Dm.@in(looking_for, held_items))
            return ITEM_SLOT_HANDS;

        return null;
    }

    /// Called whenever anything that modifes incapacitated is ran, updates it and sends a signal if it changes
    /// Returns TRUE if anything changed, FALSE otherwise
    // tg: code/modules/mob/mob.dm:465
    [SignalHandler]
    public virtual object update_incapacitated()
    {
        float old_incap = incapacitated;
        incapacitated = Dm.num(build_incapacitated());
        if (old_incap == incapacitated)
            return FALSE;

        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_INCAPACITATE_CHANGED]))
            _SendSignal(COMSIG_MOB_INCAPACITATE_CHANGED, Dm.list(this, old_incap, incapacitated));
        return TRUE;
    }

    /// Returns an updated incapacitated bitflag. If a flag is set it means we're incapacitated in that case
    // tg: code/modules/mob/mob.dm:476
    public virtual object build_incapacitated(params object[] __extra)
    {
        return NONE;
    }

    /**
     * This proc is called whenever someone clicks an inventory ui slot.
     *
     * Mostly tries to put the item into the slot if possible, or call attack hand
     * on the item in the slot if the users active hand is empty
     */
    // tg: code/modules/mob/mob.dm:485
    public virtual async DmTask<object> attack_ui(object slot = null, object @params = null)
    {
        obj_item W = get_active_held_item() as obj_item;

        if (W is obj_item)
        {
            if (Dm.truthy(await equip_to_slot_if_possible(W, slot, 0, 0, 0)))
                return TRUE;
        }

        if (!Dm.truthy(W))
        {
            // Activate the item
            obj_item I = get_item_by_slot(slot) as obj_item;
            if (I is obj_item)
            {
                DmList modifiers = params2list(@params);
                await I.attack_hand(this, modifiers);
            }
        }

        return FALSE;
    }

    /**
     * Reset the attached clients perspective (viewpoint)
     *
     * reset_perspective(null) set eye to common default : mob on turf, loc otherwise
     * reset_perspective(thing) set the eye to the thing (if it's equal to current default reset to mob perspective)
     */
    // tg: code/modules/mob/mob.dm:507
    [ShouldCallParent]
    public virtual async DmTask<object> reset_perspective(atom new_eye = null, params object[] __extra)
    {
        if (!Dm.truthy(client))
            return null;

        if (Dm.truthy(new_eye))
        {
            if (ismovable(new_eye))
            {
                //Set the new eye unless it's us
                if (new_eye != this)
                {
                    client.perspective = EYE_PERSPECTIVE;
                    await client.set_eye(new_eye);
                }
                else
                {
                    await client.set_eye(client.mob);
                    client.perspective = MOB_PERSPECTIVE;
                }
            }
            else if (isturf(new_eye))
            {
                //Set to the turf unless it's our current turf
                if (new_eye != loc)
                {
                    client.perspective = EYE_PERSPECTIVE;
                    await client.set_eye(new_eye);
                }
                else
                {
                    await client.set_eye(client.mob);
                    client.perspective = MOB_PERSPECTIVE;
                }
            }
            else
                return TRUE; //no setting eye to stupid things like areas or whatever
        }
        else
        {
            //Reset to common defaults: mob if on turf, otherwise current loc
            if (isturf(loc))
            {
                await client.set_eye(client.mob);
                client.perspective = MOB_PERSPECTIVE;
            }
            else
            {
                client.perspective = EYE_PERSPECTIVE;
                await client.set_eye(loc);
            }
        }
        /// Signal sent after the eye has been successfully updated, with the client existing.
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_RESET_PERSPECTIVE]))
            _SendSignal(COMSIG_MOB_RESET_PERSPECTIVE, Dm.list(this));
        return TRUE;
    }

    // tg: code/modules/mob/mob.dm:551  GAME_VERB_CONTEXT(/mob, examinate, "Examine", ...): the verb's body (rules 7.10, the
    // M4a exception for D128) is __gvb_examinate below; the verb itself (examinate) is at the end of this file.
    // tg: code/modules/mob/mob.dm:551 (the body proc /mob/proc/__gvb_examinate(list/structured_args), __DEFINES/verb.dm:58)
    public virtual void __gvb_examinate(DmList structured_args = null)
    {
        // VERB_ARG_TYPED(examinify, VERB_ARG_TYPE_MOB | VERB_ARG_TYPE_OBJ | VERB_ARG_TYPE_TURF, VERB_ARG_SOURCE_VIEW, /atom)
        // (__DEFINES/verb.dm:183): the static registration of the argument for the verb panel is left out (rules 10.6, 7.10)
        atom examinify = structured_args?["examinify"] as atom;

        // DEFAULT_QUEUE_OR_CALL_VERB(VERB_CALLBACK(src, PROC_REF(run_examinate), examinify)) (__DEFINES/verb_manager.dm:32,
        // :25-28): the macro builds its callback argument twice, once to queue and once to call.
        if (!Dm.truthy(_queue_verb(Dm.@new<datum_callback_verb_callback>(this, nameof(run_examinate), examinify), VERB_DEFAULT_QUEUE_THRESHOLD, null)))
            Dm.@new<datum_callback_verb_callback>(this, nameof(run_examinate), examinify).InvokeAsync();
    }

    // tg: code/modules/mob/mob.dm:556
    public virtual async DmTask run_examinate(atom examinify = null, float? force_examinate_more = null)
    {
        force_examinate_more ??= FALSE;
        if (QDELETED(examinify)) // since this can run async we might have had the atom get qdeleted already
            return;

        if (isturf(examinify) && !Dm.truthy(Dm.bitand(sight, SEE_TURFS)) && !Dm.@in(examinify, view(Dm.truthy(client) ? client.view : world.view, this)))
        {
            // shift-click catcher may issue examinate() calls for out-of-sight turfs
            return;
        }

        turf examine_turf = get_turf(examinify);
        if (Dm.truthy(is_blind())) //blind people see things differently (through touch)
        {
            if (!Dm.truthy(await blind_examine_check(examinify)))
                return;
        }
        else if (Dm.truthy(examine_turf) && !(Dm.truthy(examine_turf.luminosity) || Dm.truthy(examine_turf.get_dynamic_lumcount())) && get_dist(this, examine_turf) > 1 && !Dm.truthy(has_nightvision()))
        {
            // it is in darkness that you cannot see and farther than next to you (:569)
            return;
        }

        face_atom(examinify);
        object result_combined = null;
        object removes_double_click = client?.prefs.read_preference(typeof(datum_preference_toggle_remove_double_click));
        if (Dm.truthy(client))
        {
            LAZYINITLIST(ref client.recent_examines);
            object ref_to_atom = REF(examinify);
            object examine_time = client.recent_examines[ref_to_atom];
            if (Dm.truthy(force_examinate_more) || (Dm.truthy(examine_time) && (world.time - Dm.num(examine_time) < EXAMINE_MORE_WINDOW) && !Dm.truthy(removes_double_click)))
            {
                DmList result = await examinify.examine_more(this) as DmList;
                if (!Dm.truthy(length(result)))
                    Dm.append(ref result, span_notice(I18n.T("<i>You examine [] closer, but find nothing of interest...</i>", examinify)));
                result_combined = boxed_message(jointext(result, "<br>"));
            }
            else
            {
                client.recent_examines[ref_to_atom] = world.time; // set to when we last normal examine'd them
                _addtimer(Dm.@new<datum_callback>(this, nameof(clear_from_recent_examines), ref_to_atom), RECENT_EXAMINE_MAX_WINDOW, file: "code/modules/mob/mob.dm", line: 586);
                handle_eye_contact(examinify as mob_living);
            }
        }

        if (!Dm.truthy(result_combined))
        {
            DmList result = await examinify.examine(this) as DmList;
            object atom_title = examinify.examine_title(this, thats: TRUE);
            examining(examinify, result);
            DmList overrides = Dm.alist();
            if (Dm.truthy(_listen_lookup?[COMSIG_MOB_EXAMINING]))
                _SendSignal(COMSIG_MOB_EXAMINING, Dm.list(this, examinify, result, overrides));
            if (Dm.truthy(length(overrides)))
                result = overrides[max(overrides)] as DmList;
            if (Dm.truthy(removes_double_click))
                Dm.append(ref result, span_notice(I18n.T("<i>You can <a href=byond://?src=[];run_examinate=[]>examine</a> [] closer...</i>", REF(this), REF(examinify), examinify)));
            result_combined = Dm.truthy(atom_title) ? fieldset_block(Dm.text("[].", atom_title), jointext(result, "<br>"), "boxed_message") : boxed_message(jointext(result, "<br>"));
        }

        to_chat(this, span_infoplain(Dm.str(result_combined)));
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_EXAMINATE]))
            _SendSignal(COMSIG_MOB_EXAMINATE, Dm.list(this, examinify));
    }

    /// Handles adding examine messages for the target that are specific to this mob, e.g. a blood worm examining how much blood a living target has.
    // tg: code/modules/mob/mob.dm:607
    [ShouldNotSleep]
    public virtual void examining(atom target = null, DmList result = null)
    {
        return;
    }

    // tg: code/modules/mob/mob.dm:611
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        object dot = await base.Topic(href, href_list);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(href_list["run_examinate"]))
        {
            atom examined_atom = Dm.locate_ref(href_list["run_examinate"]) as atom;
            //run_examinate only early returns this check for turfs for some reason.
            if (Dm.@in(examined_atom, view(client != null ? client.view : world.view, this)))
                await run_examinate(examined_atom, force_examinate_more: TRUE);
        }
        return dot;
    }

    // tg: code/modules/mob/mob.dm:621
    public virtual async DmTask<object> blind_examine_check(atom examined_thing = null)
    {
        return TRUE; //The non-living will always succeed at this check.
    }
}

// tg: code/modules/mob/mob.dm:625
public partial class mob_living : mob
{
    // tg: code/modules/mob/mob.dm:625
    public override async DmTask<object> blind_examine_check(atom examined_thing = null)
    {
        //need to be next to something and awake
        if (!Dm.truthy(await Adjacent(examined_thing)) || Dm.truthy(incapacitated))
        {
            to_chat(this, span_warning(I18n.T("Something is there, but you can't see it!")));
            return FALSE;
        }

        //you can examine things you're holding directly, but you can't examine other things if your hands are full
        /// the item in our active hand
        obj_item active_item = get_active_held_item() as obj_item;
        bool boosted = false;
        if (active_item != null)
        {
            if (HAS_TRAIT(active_item, TRAIT_BLIND_TOOL))
                boosted = true;
            else if (active_item != examined_thing)
            {
                to_chat(this, span_warning(I18n.T("Your hands are too full to examine this!")));
                return FALSE;
            }
        }

        //you can only initiate exaimines if you have a hand, it's not disabled, and only as many examines as you have hands
        /// our active hand, to check if it's disabled/detached
        obj_item_bodypart active_hand = Dm.truthy(has_active_hand()) ? get_active_hand() as obj_item_bodypart : null;
        if (active_hand == null || Dm.truthy(active_hand.bodypart_disabled) || Dm.num(do_after_count()) >= usable_hands)
        {
            to_chat(this, span_warning(I18n.T("You don't have a free hand to examine this!")));
            return FALSE;
        }

        //you can only queue up one examine on something at a time
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(this, examined_thing)))
            return FALSE;

        to_chat(this, span_notice(I18n.T("You start feeling around for something...")));
        visible_message(span_notice(I18n.T(" [] begins feeling around for \\the []...", name, examined_thing.name)));

        /// how long it takes for the blind person to find the thing they're examining
        float examine_delay_length = rand(1 * SECONDS, 2 * SECONDS);
        if (boosted)
            examine_delay_length = 0.5f * SECONDS;
        if (Dm.truthy(client?.recent_examines) && Dm.truthy(client.recent_examines[REF(examined_thing)])) //easier to find things we just touched
            examine_delay_length = 0.33f * SECONDS;
        else if (isobj(examined_thing))
            examine_delay_length *= 1.5f;
        else if (ismob(examined_thing) && examined_thing != this)
            examine_delay_length *= 2;

        if (examine_delay_length > 0 && !Dm.truthy(await do_after(this, examine_delay_length, target: examined_thing)))
        {
            to_chat(this, span_notice(I18n.T("You can't get a good feel for what is there.")));
            return FALSE;
        }

        //now we touch the thing we're examining
        /// our current intent, so we can go back to it after touching
        object previous_combat_mode = combat_mode;
        set_combat_mode(FALSE);
        Waiting.Detach(examined_thing.attack_hand(this));
        set_combat_mode(previous_combat_mode);
        return TRUE;
    }
}

// tg: code/modules/mob/mob.dm:680
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob.dm:680
    [SignalHandler]
    public virtual void clear_from_recent_examines(object ref_to_clear = null)
    {
        if (!Dm.truthy(client))
            return;
        LAZYREMOVE(ref client.recent_examines, ref_to_clear);
    }

    /**
     * handle_eye_contact() is called when we examine() something. If we examine an alive mob with a mind who has examined us in the last 2 seconds within 5 tiles, we make eye contact!
     *
     * Note that if either party has their face obscured, the other won't get the notice about the eye contact
     * Also note that examine_more() doesn't proc this or extend the timer, just because it's simpler this way and doesn't lose much.
     * The nice part about relying on examining is that we don't bother checking visibility, because we already know they were both visible to each other within the last second, and the one who triggers it is currently seeing them
     */
    // tg: code/modules/mob/mob.dm:693
    public virtual void handle_eye_contact(mob_living examined_mob = null)
    {
        return;
    }
}

// tg: code/modules/mob/mob.dm:696
public partial class mob_living : mob
{
    // tg: code/modules/mob/mob.dm:696
    public override void handle_eye_contact(mob_living examined_mob = null)
    {
        if (!(examined_mob is mob_living) || this == examined_mob || IS_UNCONSCIOUS(examined_mob) || !Dm.truthy(client) || Dm.truthy(is_blind()))
            return;

        float imagined_eye_contact = FALSE;
        if (!Dm.truthy(LAZYACCESS(examined_mob.client?.recent_examines, this)))
        {
            // even if you haven't looked at them recently, if you have the shift eyes trait, they may still imagine the eye contact
            if (HAS_TRAIT(examined_mob, TRAIT_SHIFTY_EYES) && prob(10 - get_dist(this, examined_mob)))
                imagined_eye_contact = TRUE;
            else
                return;
        }

        if (get_dist(this, examined_mob) > EYE_CONTACT_RANGE)
            return;

        // check to see if their face is blocked or, if not, a signal blocks it
        if (Dm.truthy(examined_mob.can_eye_contact()) && !Dm.eq(!Dm.truthy(_listen_lookup?[COMSIG_MOB_EYECONTACT]) ? NONE : _SendSignal(COMSIG_MOB_EYECONTACT, Dm.list(this, examined_mob, TRUE)), COMSIG_BLOCK_EYECONTACT))
        {
            obj_item_clothing eye_cover = examined_mob.is_eyes_covered() as obj_item_clothing;
            if (!Dm.truthy(eye_cover) || (!Dm.truthy(eye_cover.tint) && !Dm.truthy(eye_cover.flash_protect)))
            {
                string msg = span_smallnotice(I18n.T("You make eye contact with [].", examined_mob));
                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.to_chat), this, msg), 0.3f * SECONDS, file: "code/modules/mob/mob.dm", line: 716); // so the examine signal has time to fire and this will print after
            }
        }

        if (!Dm.truthy(imagined_eye_contact) && Dm.truthy(can_eye_contact()) && !Dm.truthy(examined_mob.is_blind()) && !Dm.eq(!Dm.truthy(examined_mob._listen_lookup?[COMSIG_MOB_EYECONTACT]) ? NONE : examined_mob._SendSignal(COMSIG_MOB_EYECONTACT, Dm.list(examined_mob, this, FALSE)), COMSIG_BLOCK_EYECONTACT))
        {
            obj_item_clothing eye_cover = is_eyes_covered() as obj_item_clothing;
            if (!Dm.truthy(eye_cover) || (!Dm.truthy(eye_cover.tint) && !Dm.truthy(eye_cover.flash_protect)))
            {
                string msg = span_smallnotice(I18n.T("[] makes eye contact with you.", this));
                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.to_chat), examined_mob, msg), 0.3f * SECONDS, file: "code/modules/mob/mob.dm", line: 722);
            }
        }
    }

    /// Checks if we can make eye contact or someone can make eye contact with us
    // tg: code/modules/mob/mob.dm:725
    public virtual object can_eye_contact()
    {
        return TRUE;
    }
}

// tg: code/modules/mob/mob.dm:728
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/mob.dm:728
    public override object can_eye_contact()
    {
        Dm.NotTranslated("code/modules/mob/mob.dm:728", "can_eye_contact: /mob/living/carbon's obscured_slots (clothing) is translated in M2");
        return null;
    }
}

// tg: code/modules/mob/mob.dm:738
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob.dm:745  #define SPIN_PROC_TRAIT "trait_from_spin()" (file-local, #undef at :770)
    private const string SPIN_PROC_TRAIT = "trait_from_spin()";

    /**
     * Called by using Activate Held Object with an empty hand/limb
     *
     * Does nothing by default. The intended use is to allow limbs to call their
     * own attack_self procs. It is up to the individual mob to override this
     * parent and actually use it.
     */
    // tg: code/modules/mob/mob.dm:738
    public virtual async DmTask limb_attack_self()
    {
        return;
    }

    ///Can this mob resist (default FALSE)
    // tg: code/modules/mob/mob.dm:742
    public virtual object can_resist()
    {
        return FALSE; //overridden in living.dm
    }

    ///Spin this mob around it's central axis
    // tg: code/modules/mob/mob.dm:748  (set waitfor = 0: the wrapper and its body, waiting-model.md 5.4; '.' is never
    // assigned, so the body has no box)
    public virtual void spin(object spintime = null, object speed = null)
    {
        Waiting.WaitforFalse(spin__body(spintime, speed));
    }

    // The body of the set waitfor = 0 wrapper above (waiting-model.md 5.4).
    private async DmTask spin__body(object spintime, object speed)
    {
        float D = dir;
        if ((Dm.lt(spintime, 1)) || (Dm.lt(speed, 1)) || !Dm.truthy(spintime) || !Dm.truthy(speed))
            return;

        ADD_TRAIT(this, TRAIT_SPINNING, SPIN_PROC_TRAIT);
        while (Dm.ge(spintime, speed))
        {
            await sleep(Dm.num(speed));
            switch (D)
            {
                case NORTH:
                    D = EAST;
                    break;
                case SOUTH:
                    D = WEST;
                    break;
                case EAST:
                    D = SOUTH;
                    break;
                case WEST:
                    D = NORTH;
                    break;
            }
            await setDir(D);
            spintime = Dm.sub(spintime, speed);
        }
        REMOVE_TRAIT(this, TRAIT_SPINNING, SPIN_PROC_TRAIT);
    }

    ///Update the pulling hud icon
    // tg: code/modules/mob/mob.dm:773
    public virtual void update_pull_hud_icon()
    {
        (hud_used?.screen_objects[HUD_MOB_PULL] as atom)?.update_appearance();
    }

    ///Update the resting hud icon
    // tg: code/modules/mob/mob.dm:777
    public virtual object update_rest_hud_icon()
    {
        if (!Dm.truthy(hud_used))
            return FALSE;
        (hud_used.screen_objects[HUD_MOB_REST] as atom)?.update_appearance();
        return TRUE;
    }

    /**
     * Verb to activate the object in your held hand
     *
     * Calls attack self on the item and updates the inventory hud for hands
     */
    // tg: code/modules/mob/mob.dm:788  (mode__proc: /mob also has a var named mode, rules 2.4)
    public virtual void mode__proc()
    {
        // DEFAULT_QUEUE_OR_CALL_VERB(VERB_CALLBACK(src, PROC_REF(execute_mode))) (__DEFINES/verb_manager.dm:32, :25-28): the
        // macro builds its callback argument twice, once to queue and once to call.
        if (!Dm.truthy(_queue_verb(Dm.@new<datum_callback_verb_callback>(this, nameof(execute_mode)), VERB_DEFAULT_QUEUE_THRESHOLD, null)))
            Dm.@new<datum_callback_verb_callback>(this, nameof(execute_mode)).InvokeAsync();
    }

    ///proc version to finish /mob/verb/mode() execution. used in case the proc needs to be queued for the tick after its first called
    // tg: code/modules/mob/mob.dm:792
    public virtual async DmTask execute_mode()
    {
        // GAP(lead): ismecha (__DEFINES/is_helpers.dm:283) has no GlobalProcs member yet: expanded inline.
        if (istype(loc, typeof(obj_vehicle_sealed_mecha)))
            return;

        if (Dm.truthy(incapacitated))
            return;

        obj_item I = get_active_held_item() as obj_item;
        if (Dm.truthy(I))
        {
            await I.attack_self(this);
            await update_held_items();
            return;
        }

        await limb_attack_self();
    }

    // tg: code/modules/mob/mob.dm:814  GAME_VERB(/mob, abandon_mob, "Respawn", "OOC"): not declared, verbs are translated
    // with the player command layer (rules 7.10).

    /// Checks if the mob can respawn yet according to the respawn delay
    // tg: code/modules/mob/mob.dm:860
    public virtual async DmTask<object> check_respawn_delay(float? override_delay = null)
    {
        Dm.NotTranslated("code/modules/mob/mob.dm:860", "check_respawn_delay: respawning is translated with the round loop (M10+)");
        return null;
    }

    // tg: code/modules/mob/mob.dm:886-895  GAME_VERB(/mob, reset_ui_positions_for_mob, ...), GAME_VERB_NATIVE(/mob, DisClick, ...) and
    // GAME_VERB_NATIVE(/mob, DisDblClick, ...): not declared, verbs are translated with the player command layer (rules 7.10);
    // cancel_camera is at the top of this file.

    /// Adds this list to the output to the stat browser
    // tg: code/modules/mob/mob.dm:898
    public virtual object get_status_tab_items()
    {
        Dm.NotTranslated("code/modules/mob/mob.dm:898", "get_status_tab_items: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/modules/mob/mob.dm:904
    [ShouldNotOverride] // Override perform_hand_swap instead
    public virtual object swap_hand(object held_index = null, float? silent = null)
    {
        silent ??= FALSE;
        obj_item held_item = get_active_held_item() as obj_item;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOB_SWAPPING_HANDS]) ? NONE : this._SendSignal(COMSIG_MOB_SWAPPING_HANDS, Dm.list(this, held_item))), COMPONENT_BLOCK_SWAP)))
        {
            if (!Dm.truthy(silent))
                to_chat(this, span_warning(I18n.T("Your other hand is too busy holding [].", held_item)));
            return FALSE;
        }

        object result = perform_hand_swap(held_index);
        if (Dm.truthy(result))
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_SWAP_HANDS]))
                this._SendSignal(COMSIG_MOB_SWAP_HANDS, Dm.list(this, get_active_held_item(), held_item));
        }

        return result;
    }

    /// Performs the actual ritual of swapping hands, such as setting the held index variables
    // tg: code/modules/mob/mob.dm:920
    [DmProtected]
    public virtual object perform_hand_swap(object held_index = null)
    {
        if (!HAS_TRAIT(this, TRAIT_CAN_HOLD_ITEMS))
            return FALSE;

        if (!Dm.truthy(held_index))
            held_index = (Dm.mod(active_hand_index, held_items.len)) + 1;

        if (!isnum(held_index))
            throw Dm.CRASH(Dm.text("You passed [] into swap_hand instead of a number. WTF man", held_index), "code/modules/mob/mob.dm", 929);

        float previous_index = active_hand_index;
        active_hand_index = Dm.num(held_index);
        hud_used?.update_inventory_slot(ITEM_SLOT_HANDS, previous_index);
        hud_used?.update_inventory_slot(ITEM_SLOT_HANDS, held_index);
        return TRUE;
    }

    // tg: code/modules/mob/mob.dm:937
    public virtual async DmTask activate_hand(object selected_hand = null)
    {
        if (!HAS_TRAIT(this, TRAIT_CAN_HOLD_ITEMS))
            return;

        if (!Dm.truthy(selected_hand))
            selected_hand = active_hand_index;
        else if (istext(selected_hand))
        {
            selected_hand = LOWER_TEXT(selected_hand);
            if (Dm.eq(selected_hand, "right") || Dm.eq(selected_hand, "r"))
                selected_hand = 2;
            if (Dm.eq(selected_hand, "left") || Dm.eq(selected_hand, "l"))
                selected_hand = 1;
        }

        if (!Dm.eq(selected_hand, active_hand_index))
            swap_hand(selected_hand);

        // _queue_verb requires a client, so when we don't have it (AI controlled mob) we don't use it
        if (Dm.truthy(client))
            mode__proc();
        else
            await execute_mode();
    }

    // tg: code/modules/mob/mob.dm:956
    public virtual object assess_threat(object judgement_criteria = null, string lasercolor = null, datum_callback weaponcheck = null) //For sec bot threat assessment
    {
        lasercolor ??= "";
        return 0;
    }

    ///Get the ghost of this mob (from the mind)
    // tg: code/modules/mob/mob.dm:960
    public virtual object get_ghost(object even_if_they_cant_reenter = null, object ghosts_with_clients = null)
    {
        if (Dm.truthy(mind))
            return mind.get_ghost(even_if_they_cant_reenter, ghosts_with_clients);
        return null;
    }

    ///Force get the ghost from the mind
    // tg: code/modules/mob/mob.dm:965
    public virtual object grab_ghost(object force = null)
    {
        if (Dm.truthy(mind))
            return mind.grab_ghost(force: force);
        return null;
    }

    ///Notify a ghost that its body is being revived
    // tg: code/modules/mob/mob.dm:970
    public virtual object notify_revival(string message = null, object sound = null, atom source = null, float? flashwindow = null)
    {
        message ??= I18n.T("Someone is trying to revive you. Re-enter your corpse if you want to be revived!");
        sound ??= Dm.resource("sound/effects/genetics.ogg");
        flashwindow ??= TRUE;
        mob_dead_observer ghost = get_ghost() as mob_dead_observer;
        if (ghost != null)
        {
            ghost.send_revival_notification(message, sound, source, flashwindow);
            return ghost;
        }
        return null;
    }

    /**
     * Checks to see if the mob can cast normal magic spells.
     *
     * args:
     * * magic_flags (optional) A bitfield with the type of magic being cast (see flags at: /datum/component/anti_magic)
    **/
    // tg: code/modules/mob/mob.dm:982
    public virtual object can_cast_magic(float? magic_flags = null)
    {
        magic_flags ??= MAGIC_RESISTANCE;
        if (Dm.eq(magic_flags, NONE)) // magic with the NONE flag can always be cast
            return TRUE;

        object restrict_magic_flags = (!Dm.truthy(_listen_lookup?[COMSIG_MOB_RESTRICT_MAGIC]) ? (object)NONE : _SendSignal(COMSIG_MOB_RESTRICT_MAGIC, Dm.list(this, magic_flags)));
        return Dm.num(Dm.eq(restrict_magic_flags, NONE));
    }

    /**
     * Checks to see if the mob can block magic
     *
     * args:
     * * casted_magic_flags (optional) A bitfield with the types of magic resistance being checked (see flags at: /datum/component/anti_magic)
     * * charge_cost (optional) The cost of charge to block a spell that will be subtracted from the protection used
    **/
    // tg: code/modules/mob/mob.dm:996
    public virtual object can_block_magic(float? casted_magic_flags = null, float? charge_cost = null)
    {
        casted_magic_flags ??= MAGIC_RESISTANCE;
        charge_cost ??= 1;
        if (Dm.eq(casted_magic_flags, NONE)) // magic with the NONE flag is immune to blocking
            return FALSE;

        // A list of all things which are providing anti-magic to us
        DmList antimagic_sources = Dm.list();
        float is_magic_blocked = FALSE;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_MOB_RECEIVE_MAGIC]) ? (object)NONE : _SendSignal(COMSIG_MOB_RECEIVE_MAGIC, Dm.list(this, casted_magic_flags, charge_cost, antimagic_sources))), COMPONENT_MAGIC_BLOCKED)))
            is_magic_blocked = TRUE;
        if (HAS_TRAIT(this, TRAIT_ANTIMAGIC))
            is_magic_blocked = TRUE;
        if ((Dm.truthy(Dm.bitand(casted_magic_flags, MAGIC_RESISTANCE_HOLY))) && HAS_TRAIT(this, TRAIT_HOLY))
            is_magic_blocked = TRUE;

        if (Dm.truthy(is_magic_blocked) && Dm.num(charge_cost) > 0 && !HAS_TRAIT(this, TRAIT_RECENTLY_BLOCKED_MAGIC))
            on_block_magic_effects(casted_magic_flags, antimagic_sources);

        return is_magic_blocked;
    }

    /// Called whenever a magic effect with a charge cost is blocked and we haven't recently blocked magic.
    // tg: code/modules/mob/mob.dm:1017
    public virtual void on_block_magic_effects(object magic_flags = null, DmList antimagic_sources = null)
    {
        return;
    }
}

// tg: code/modules/mob/mob.dm:1020
public partial class mob_living : mob
{
    // tg: code/modules/mob/mob.dm:1020
    public override void on_block_magic_effects(object magic_flags = null, DmList antimagic_sources = null)
    {
        Dm.NotTranslated("code/modules/mob/mob.dm:1020", "on_block_magic_effects: the anti-magic effects (mutable_appearance, mob_light, playsound) are translated with magic (M10+)");
    }
}

// tg: code/modules/mob/mob.dm:1064
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob.dm:1064  /mob/buckle_mob(mob/living/M, force = FALSE, check_loc = TRUE, buckle_mob_flags= NONE):
    // not declared, its root /atom/movable/proc/buckle_mob (game/objects/buckling.dm, package A) is not declared in this
    // branch; buckling mobs is translated in M2.

    ///Can the mob interact() with an atom?
    // tg: code/modules/mob/mob.dm:1070
    public virtual async DmTask<object> can_interact_with(atom A = null, object treat_mob_as_adjacent = null)
    {
        // GAP(lead): isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared: no admin ghosts yet (as airlock.cs).
        bool isAdminGhostAI__gap = false;
        if (isAdminGhostAI__gap)
            return TRUE;
        //Return early. we do not need to check that we are on adjacent turfs (i.e we are inside a closet)
        if (Dm.truthy(treat_mob_as_adjacent) && this == A.loc)
            return TRUE;
        if (Dm.truthy(await Adjacent(A)))
            return TRUE;
        datum_dna mob_dna = has_dna() as datum_dna;
        // mob_dna?.check_mutation(/datum/mutation/telekinesis): GAP(lead), check_mutation is not translated (genetics, M8;
        // dna.cs) and no mob has a mutation; not called, because tgui's ui_status reaches this for every open window at a
        // distance (M5 slice 3) and the NotTranslated stub would report there.
        bool telekinesis__gap = false;
        if (mob_dna != null && telekinesis__gap && tkMaxRangeCheck__local(this, A))
            return TRUE;
        object item_in_hand = get_active_held_item();
        if (item_in_hand is obj_item_machine_remote)
            return TRUE;

        //range check
        if (!Dm.truthy(interaction_range)) // If you don't have extra length, GO AWAY
            return FALSE;
        turf our_turf = get_turf(this) as turf;
        turf their_turf = get_turf(A) as turf;
        if (our_turf == null || their_turf == null)
            return FALSE;
        // ISINRANGE(val, min, max) (__DEFINES/maths.dm): min <= val && val <= max
        float tx = Dm.num(their_turf.x), ty = Dm.num(their_turf.y), ox = Dm.num(our_turf.x), oy = Dm.num(our_turf.y);
        return (ox - interaction_range <= tx && tx <= ox + interaction_range && oy - interaction_range <= ty && ty <= oy + interaction_range) ? TRUE : FALSE;
    }

    // tg: code/_onclick/telekinesis.dm:214 /proc/tkMaxRangeCheck (TK_MAXRANGE, telekinesis.dm:7, is 15). GAP(lead): not
    // declared (telekinesis is M8): a local copy, as living.cs's.
    private static bool tkMaxRangeCheck__local(mob user, atom target)
    {
        float d = get_dist(user, target);
        if (d > 15)
        {
            user.balloon_alert(user, I18n.T("can't TK, too far!"));
            return false;
        }
        return true;
    }

    /**
     * Checks whether a mob can perform an action to interact with an object
     *
     * The default behavior checks if the mob is:
     * * Directly adjacent (1-tile radius)
     * * Standing up (not resting)
     * * Allows telekinesis to be used to skip adjacent checks (if they have DNA mutation)
     * *
     * action_bitflags: (see code/__DEFINES/mobs.dm)
     * * NEED_GRAVITY - If gravity must be present to perform action (can't use pens without gravity)
     * * NEED_LITERACY - If reading is required to perform action (can't read a book if you are illiterate)
     * * NEED_LIGHT - If lighting must be present to perform action (can't heal someone in the dark)
     * * NEED_DEXTERITY - If other mobs (monkeys, aliens, etc) can perform action (can't use computers if you are a monkey)
     * * NEED_HANDS - If hands are required to perform action (can't pickup items if you are a cyborg)
     * * FORBID_TELEKINESIS_REACH - If telekinesis is forbidden to perform action from a distance (ex. canisters are blacklisted from telekinesis manipulation)
     * * ALLOW_SILICON_REACH - If silicons are allowed to perform action from a distance (silicons can operate airlocks from far away)
     * * ALLOW_RESTING - If resting on the floor is allowed to perform action ()
     * * ALLOW_VENTCRAWL - Mobs with ventcrawl traits can alt-click this to vent
     * * BYPASS_ADJACENCY - The target does not have to be adjacent
     * * SILENT_ADJACENCY - Adjacency is required but errors are not printed
     * * NOT_INSIDE_TARGET - The target maybe adjacent but the mob should not be inside the target
     * * ALLOW_PAI - Allows pAIs to perform an action
     *
     * silence_adjacency: Sometimes we want to use this proc to check interaction without allowing it to throw errors for base case adjacency
     * Alt click uses this, as otherwise you can detect what is interactable from a distance via the error message
    **/
    // tg: code/modules/mob/mob.dm:1120
    public virtual async DmTask<object> can_perform_action(atom target = null, object action_bitflags = null)
    {
        return null;
    }

    ///Can this mob use storage
    // tg: code/modules/mob/mob.dm:1124
    public virtual object canUseStorage()
    {
        return FALSE;
    }

    /**
     * Fully update the name of a mob
     *
     * This will update a mob's name, real_name, mind.name, GLOB.manifest records, pda, id and traitor text
     *
     * Calling this proc without an oldname will only update the mob and skip updating the pda, id and records ~Carn
     */
    // tg: code/modules/mob/mob.dm:1134
    public virtual async DmTask<object> fully_replace_character_name(object oldname = null, object newname = null, float? log_new_name = null)
    {
        log_new_name ??= FALSE;
        if (!Dm.truthy(newname))
            return FALSE;
        if (Dm.eq(oldname, newname))
            return FALSE;
        if (!istext(newname) && newname != null)
            return FALSE;

        log_played_names(
            ckey,
            Dm.list(
                Dm.kv(Dm.text("[]", newname), tag)
            )
        );

        real_name = newname;
        name = (string)newname;
        if (Dm.truthy(mind))
        {
            mind.name = newname;
            if (Dm.truthy(mind.key))
            {
                log_played_names(
                    Dm.ckey(mind.key),
                    Dm.list(
                        Dm.kv(Dm.text("[]", newname), tag)
                    )
                ); //Just in case the mind is unsynced at the moment.
            }
        }

        if (Dm.truthy(oldname))
        {
            log_message(Dm.text("[] name changed from [] to []", this, oldname, newname), LOG_OWNERSHIP);

            //update the datacore records! This is goig to be a bit costly.
            replace_records_name(oldname, newname);

            //update our pda and id if we have them on our person
            await replace_identification_name(oldname, newname);

            foreach (datum_mind T in Dm.each<datum_mind>(SSticker.minds))
            {
                foreach (datum_objective obj in Dm.each<datum_objective>(T.get_all_objectives()))
                {
                    // Only update if this player is a target
                    if (obj.target != null && obj.target.current != null && Dm.eq(obj.target.current.real_name, name))
                        obj.update_explanation_text();
                }
            }
        }
        else if (Dm.truthy(log_new_name))
            log_message(Dm.text("[] name set to []", this, newname), LOG_OWNERSHIP);

        log_mob_tag(Dm.text("TAG: [] RENAMED: []", tag, key_name(this)));
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_FULLY_RENAMED]))
            this._SendSignal(COMSIG_MOB_FULLY_RENAMED, Dm.list(this, oldname, newname));

        return TRUE;
    }

    ///Updates GLOB.manifest records with new name , see mob/living/carbon/human
    // tg: code/modules/mob/mob.dm:1185
    public virtual void replace_records_name(object oldname = null, object newname = null)
    {
        return;
    }

    ///update the ID name of this mob
    // tg: code/modules/mob/mob.dm:1189
    public virtual async DmTask replace_identification_name(object oldname = null, object newname = null)
    {
        object searching = get_all_contents();
        bool search_id = true;
        bool search_pda = true;

        foreach (object A in Dm.each(searching))
        {
            if (search_id && A is obj_item_card_id)
            {
                obj_item_card_id ID = A as obj_item_card_id;
                if (Dm.eq(ID.registered_name, oldname))
                {
                    ID.registered_name = newname;
                    ID.update_label();
                    await ID.update_icon();
                    if (ID.registered_account != null && Dm.eq(ID.registered_account.account_holder, oldname))
                        ID.registered_account.account_holder = Dm.str(newname);
                    if (!search_pda)
                        break;
                    search_id = false;
                }
            }
            else if (search_pda && A is obj_item_modular_computer_pda PDA)
            {
                if (Dm.eq(PDA.saved_identification, oldname))
                {
                    PDA.imprint_id(name: newname);
                    PDA.UpdateDisplay();
                    if (!search_id)
                        break;
                    search_pda = false;
                }
            }
        }
    }

    // tg: code/modules/mob/mob.dm:1216
    [SignalHandler]
    public virtual void update_stat()
    {
        return;
    }

    // tg: code/modules/mob/mob.dm:1220
    public virtual object update_health_hud(object healthpercent = null)
    {
        return null;
    }

    /// Changes the stamina HUD based on new information
    // tg: code/modules/mob/mob.dm:1224
    public virtual void update_stamina_hud(params object[] __extra)
    {
        return;
    }

    ///Update the lighting plane and sight of this mob (sends COMSIG_MOB_UPDATE_SIGHT)
    // tg: code/modules/mob/mob.dm:1228
    [SignalHandler, ShouldCallParent]
    public virtual void update_sight()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_UPDATE_SIGHT]))
            _SendSignal(COMSIG_MOB_UPDATE_SIGHT, Dm.list(this));
        sync_lighting_plane_cutoff();
    }

    ///Set the lighting plane hud filters to the mobs lighting_cutoff var
    // tg: code/modules/mob/mob.dm:1235
    public virtual void sync_lighting_plane_cutoff()
    {
        if (!Dm.truthy(hud_used))
            return;
        // tg: :1239  for(var/atom/movable/screen/plane_master/rendering_plate/lighting/light as anything in hud_used.get_true_plane_masters(RENDER_PLANE_LIGHTING))
        //                light.set_light_cutoff(lighting_cutoff, lighting_color_cutoffs)
        // The port has no plane masters (the 2D render planes are the 3D client's, D24, rules 13): get_true_plane_masters answers
        // an empty list, so the loop has nothing to set; one that found a plane master would have its light cutoff to set.
        foreach (object light in Dm.each(hud_used.get_true_plane_masters(RENDER_PLANE_LIGHTING)))
            Dm.NotTranslated("code/modules/mob/mob.dm:1239", "sync_lighting_plane_cutoff: light.set_light_cutoff(lighting_cutoff, lighting_color_cutoffs) of a lighting plane master (the port has none, D24)");
    }

    ///Update the mouse pointer of the attached client in this mob
    // tg: code/modules/mob/mob.dm:1242
    public virtual void update_mouse_pointer()
    {
        if (!Dm.truthy(client))
            return;
        if (!Dm.eq(client.mouse_pointer_icon, Dm.initial(client, (client p) => p.mouse_pointer_icon)))//only send changes to the client if theyre needed
            client.mouse_pointer_icon = Dm.initial(client, (client p) => p.mouse_pointer_icon);
        if (Dm.truthy(examine_cursor_icon) && Dm.truthy(client.keys_held["Shift"])) //mouse shit is hardcoded, make this non hard-coded once we make mouse modifiers bindable
            client.mouse_pointer_icon = examine_cursor_icon;
        if (loc is obj_vehicle_sealed E && Dm.truthy(E.mouse_pointer))
            client.mouse_pointer_icon = E.mouse_pointer;
        if (Dm.truthy(client.mouse_override_icon))
            client.mouse_pointer_icon = client.mouse_override_icon;
    }

    /**
     * Can this mob see in the dark
     *
     * This checks all traits, glasses, and robotic eyeball implants to see if the mob can see in the dark
     * this does NOT check if the mob is missing it's eyeballs.
    **/
    // tg: code/modules/mob/mob.dm:1262
    public virtual object has_nightvision()
    {
        // Somewhat conservative, basically is your lighting plane bright enough that you the user can see stuff
        object light_offset = lighting_cutoff;
        if (length(lighting_color_cutoffs) == 3)
            Dm.append(ref light_offset, Dm.div(Dm.add(Dm.add(lighting_color_cutoffs[1], lighting_color_cutoffs[2]), lighting_color_cutoffs[3]), 3));
        return Dm.num(Dm.ge(light_offset, LIGHTING_NIGHTVISION_THRESHOLD));
    }

    // tg: code/modules/mob/mob.dm:221  (gaps J3: the camera's description)
    public virtual object get_photo_description(obj_item_camera camera = null)
    {
        return I18n.T("a ... thing?");
    }

    /// This mob is abile to read books
    // tg: code/modules/mob/mob.dm:1270
    public virtual object is_literate()
    {
        return Dm.num(HAS_TRAIT(this, TRAIT_LITERATE) && !HAS_TRAIT(this, TRAIT_ILLITERATE));
    }

    /**
     * Proc that returns TRUE if the mob can write using the writing_instrument, FALSE otherwise.
     *
     * This proc a side effect, outputting a message to the mob's chat with a reason if it returns FALSE.
     * Unless silent_if_not_writing_tool is TRUE. In that case it'll be silent if it isn't a writing implement/tool/instrument w/e.
     */
    // tg: code/modules/mob/mob.dm:1279
    public virtual async DmTask<object> can_write(obj_item writing_instrument = null, float? silent_if_not_writing_tool = null)
    {
        // gaps S3: the paperwork's writing tools
        silent_if_not_writing_tool ??= FALSE;
        if (writing_instrument == null)
            return FALSE;

        if (!IS_WRITING_UTENSIL(writing_instrument))
        {
            if (!Dm.truthy(silent_if_not_writing_tool))
                to_chat(this, span_warning(I18n.T("You can't write with \\the []!", writing_instrument)));
            return FALSE;
        }

        if (!Dm.truthy(is_literate()))
        {
            to_chat(this, span_warning(I18n.T("You try to write, but don't know how to spell anything!")));
            return FALSE;
        }

        if (!Dm.truthy(has_light_nearby()) && !Dm.truthy(has_nightvision()))
        {
            to_chat(this, span_warning(I18n.T("It's too dark in here to write anything!")));
            return FALSE;
        }

        if (Dm.truthy(has_gravity()))
            return TRUE;

        obj_item_pen pen = writing_instrument as obj_item_pen;

        if (pen != null && Dm.truthy(pen.requires_gravity))
        {
            to_chat(this, span_warning(I18n.T("You try to write, but \\the [] doesn't work in zero gravity!", writing_instrument)));
            return FALSE;
        }

        return TRUE;
    }

    /**
     * Checks if there is enough light where the mob is located
     *
     * Args:
     *  light_amount (optional) - A decimal amount between 1.0 through 0.0 (default is 0.2)
    **/
    // tg: code/modules/mob/mob.dm:1313
    public virtual object has_light_nearby(float? light_amount = null)
    {
        light_amount ??= LIGHTING_TILE_IS_DARK;
        turf mob_location = get_turf(this);
        area mob_area = get_area(this) as area;

        if (Dm.truthy(mob_location.check_lumcount_above(light_amount)))
            return TRUE;
        else if (!Dm.truthy(mob_area.static_lighting))
            return TRUE;

        return FALSE;
    }

    /// Can this mob read
    // tg: code/modules/mob/mob.dm:1327
    public virtual object can_read(atom viewed_atom = null, float? reading_check_flags = null, float? silent = null)
    {
        reading_check_flags ??= Dm.bitor(READING_CHECK_LITERACY, READING_CHECK_LIGHT);
        silent ??= FALSE;
        if ((Dm.truthy(Dm.bitand(reading_check_flags, READING_CHECK_LITERACY))) && !Dm.truthy(is_literate()))
        {
            if (!Dm.truthy(silent))
                to_chat(this, span_warning(I18n.T("You try to read [], but can't comprehend any of it.", viewed_atom)));
            return FALSE;
        }

        if ((Dm.truthy(Dm.bitand(reading_check_flags, READING_CHECK_LIGHT))) && !Dm.truthy(has_light_nearby()) && !Dm.truthy(has_nightvision()))
        {
            if (!Dm.truthy(silent))
                to_chat(this, span_warning(I18n.T("It's too dark in here to read!")));
            return FALSE;
        }

        return TRUE;
    }

    /**
     * Get the mob VV dropdown extras
     */
    // tg: code/modules/mob/mob.dm:1343
    public override object vv_get_dropdown()
    {
        Dm.NotTranslated("code/modules/mob/mob.dm:1343", "vv_get_dropdown: VV is admin tooling (translation-rules.md 13)");
        return null;
    }

    // tg: code/modules/mob/mob.dm:1366
    public override async DmTask<object> vv_do_topic(DmList href_list = null)
    {
        Dm.NotTranslated("code/modules/mob/mob.dm:1366", "vv_do_topic: VV is admin tooling (translation-rules.md 13)");
        return null;
    }

    // tg: code/modules/mob/mob.dm:1436  /mob/vv_get_var(var_name) and :1442 /mob/vv_auto_rename(new_name): not declared,
    // their /datum roots (datums/datumvars.dm, VV) are not translated (rules 13).

    ///Adjust the nutrition of a mob
    // tg: code/modules/mob/mob.dm:1447
    public virtual async DmTask adjust_nutrition(object change = null, float? forced = null) //Honestly FUCK the oldcoders for putting nutrition on /mob someone else can move it up because holy hell I'd have to fix SO many typechecks
    {
        forced ??= FALSE;
        if (HAS_TRAIT(this, TRAIT_NOHUNGER) && !Dm.truthy(forced))
            return;

        nutrition = Dm.num(max(0, Dm.add(nutrition, change)));
    }
}

// tg: code/modules/mob/mob.dm:1453
public partial class mob_living : mob
{
    // tg: code/modules/mob/mob.dm:1453
    public override async DmTask adjust_nutrition(object change = null, float? forced = null)
    {
        await base.adjust_nutrition(change, forced);
        // Queue update if change is small enough (6 is 1% of nutrition softcap)
        if (abs(Dm.num(change)) >= 6)
            await update_nutrition();
        else
        {
            Dm.combine(ref living_flags, QUEUE_NUTRITION_UPDATE);
        }
    }
}

// tg: code/modules/mob/mob.dm:1462
public partial class mob : atom_movable
{
    ///Force set the mob nutrition
    // tg: code/modules/mob/mob.dm:1462
    public virtual async DmTask set_nutrition(object set_to = null, float? forced = null) //Seriously fuck you oldcoders.
    {
        forced ??= FALSE;
        if (HAS_TRAIT(this, TRAIT_NOHUNGER) && !Dm.truthy(forced))
            return;

        nutrition = Dm.num(max(0, set_to));
    }
}

// tg: code/modules/mob/mob.dm:1468
public partial class mob_living : mob
{
    // tg: code/modules/mob/mob.dm:1468
    public override async DmTask set_nutrition(object set_to = null, float? forced = null)
    {
        float old_nutrition = nutrition;
        await base.set_nutrition(set_to, forced);
        // Queue update if change is small enough (6 is 1% of nutrition softcap)
        if (abs(old_nutrition - nutrition) >= 6)
            await update_nutrition();
        else
        {
            Dm.combine(ref living_flags, QUEUE_NUTRITION_UPDATE);
        }
    }

    /// Updates nutrition related effects
    // tg: code/modules/mob/mob.dm:1478
    public virtual async DmTask update_nutrition()
    {
        if (mob_mood != null)
            await mob_mood.update_nutrition_moodlets();
        if (hud_used?.screen_objects?[HUD_MOB_HUNGER] is atom_movable_screen_hunger hunger_bar)
            hunger_bar.update_hunger_bar();
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_UPDATE_NUTRITION]))
            _SendSignal(COMSIG_LIVING_UPDATE_NUTRITION, Dm.list(this));
    }
}

// tg: code/modules/mob/mob.dm:1486
public partial class mob : atom_movable
{
    /// Apply a proper movespeed modifier based on items we have equipped
    // tg: code/modules/mob/mob.dm:1486
    public virtual async DmTask update_equipment_speed_mods()
    {
        float speedies = 0;
        float immutable_speedies = 0;
        foreach (obj_item thing in Dm.each<obj_item>(get_equipped_speed_mod_items()))
        {
            if (Dm.truthy(Dm.bitand(thing.item_flags, IMMUTABLE_SLOW)))
                immutable_speedies += thing.slowdown;
            else
                speedies += thing.slowdown;
        }

        //if  we have TRAIT_STURDY_FRAME, we reduce our overall speed penalty UNLESS that penalty would be a negative value, and therefore a speed boost.
        if (speedies > 0 && HAS_TRAIT(this, TRAIT_STURDY_FRAME))
            speedies *= 0.2f;

        if (Dm.truthy(immutable_speedies))
        {
            await add_or_update_variable_movespeed_modifier(
                typeof(datum_movespeed_modifier_equipment_speedmod_immutable),
                multiplicative_slowdown: immutable_speedies
            );
        }
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_equipment_speedmod_immutable));

        if (Dm.truthy(speedies))
        {
            await add_or_update_variable_movespeed_modifier(
                typeof(datum_movespeed_modifier_equipment_speedmod),
                multiplicative_slowdown: speedies
            );
        }
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_equipment_speedmod));
    }

    // tg: code/modules/mob/mob.dm:1515  (rules 7.5: defined twice on /mob, so this earlier definition is set_stat__d0 and
    // tgui_input/say_modal/typing.dm's later one is the slot's root, typing.cs)
    internal async DmTask<object> set_stat__d0(object new_stat = null)
    {
        object dot = null;
        if (Dm.eq(new_stat, stat))
            return null;
        dot = stat;
        stat = Dm.num(new_stat);
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_STATCHANGE]))
            _SendSignal(COMSIG_MOB_STATCHANGE, Dm.list(this, new_stat, dot));
        return dot;
    }

    // tg: code/modules/mob/mob.dm:1522  /mob/vv_edit_var(var_name, var_value): not declared, its root /datum/proc/vv_edit_var
    // (datums/datumvars.dm, VV) is not translated (rules 13).

    /// Cleanup proc that's called when a mob loses a client, either through client destroy or logout
    /// Logout happens post client del, so we can't just copypaste this there. This keeps things clean and consistent
    // tg: code/modules/mob/mob.dm:1551
    public virtual async DmTask become_uncliented()
    {
        if (!Dm.truthy(canon_client))
            return;

        foreach (datum_callback CB in Dm.each_cast<datum_callback>(persistent_client.post_logout_callbacks))
            await CB.Invoke();

        clear_important_client_contents();
        canon_client = null;
    }

    ///Shows a tgui window with memories
    // tg: code/modules/mob/mob.dm:1562
    public virtual async DmTask open_memory_panel()
    {
        if (mind == null)
        {
            string fail_message = I18n.T("You have no mind!");
            if (isobserver(this))
                fail_message += I18n.T(" You have to be in the current round at some point to have one.");
            to_chat(this, span_warning(fail_message));
            return;
        }
        if (mind.memory_panel == null)
            mind.memory_panel = Dm.@new<datum_memory_panel>(usr, mind);
        await mind.memory_panel.ui_interact(usr);
    }
}

// tg: code/modules/mob/mob.dm:1573  (gaps package G4: the window's procs, tgui's MemoryPanel: godot/scripts/Shared/Tgui/Interfaces/MemoryPanel.cs)
[DmPath("/datum/memory_panel")]
public partial class datum_memory_panel : datum
{
    // tg: code/modules/mob/mob.dm:1574
    public datum_mind mind_reference;
    // tg: code/modules/mob/mob.dm:1575
    public client holder; //client of whoever is using this datum

    // tg: code/modules/mob/mob.dm:1577
    [DmParams("user", "mind_reference")]
    public override async DmTask New(params object[] __extra)
    {
        object user = Dm.arg(__extra, 0);
        datum_mind mind_reference = Dm.arg<datum_mind>(__extra, 1);
        if (user is client)
        {
            client user_client = user as client;
            holder = user_client; //if its a client, assign it to holder
        }
        else
        {
            mob user_mob = user as mob;
            holder = user_mob.client; //if its a mob, assign the mob's client to holder
        }
        this.mind_reference = mind_reference;
    }

    // tg: code/modules/mob/mob.dm:1586
    public override object Destroy(float? force = null)
    {
        object dot = null;
        mind_reference.memory_panel = null;
        dot = base.Destroy(force);
        return dot;
    }

    // tg: code/modules/mob/mob.dm:1590
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/mob/mob.dm:1593
    public override void ui_close(mob user = null)
    {
        qdel(this);
    }

    // tg: code/modules/mob/mob.dm:1596
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "MemoryPanel");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/mob.dm:1602
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList memories = Dm.list();

        foreach (object memory_key in Dm.each(user?.mind.memories))
        {
            datum_memory memory = user.mind.memories[memory_key] as datum_memory;
            Dm.append(ref memories, Dm.list(Dm.list(Dm.kv("name", memory.name), Dm.kv("quality", memory.story_value))));
        }

        data["memories"] = memories;
        return data;
    }
}

// tg: code/modules/mob/mob.dm:1613
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob.dm:1613  (tg names the second parameter client; rules 7.2)
    [DmParams("key", "client", "full_key")]
    public override void key_down(object key = null, client client = null, object full_key = null)
    {
        base.key_down(key, client, full_key);
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_KEYDOWN]))
            _SendSignal(COMSIG_MOB_KEYDOWN, Dm.list(this, key, client, full_key));
    }

    // tg: code/modules/mob/mob.dm:1617
    public virtual void setup_hud_traits()
    {
        foreach (object hud_trait in Dm.each(GLOB.trait_to_hud))
        {
            RegisterSignal(this, SIGNAL_ADDTRAIT(hud_trait), nameof(hud_trait_enabled));
            RegisterSignal(this, SIGNAL_REMOVETRAIT(hud_trait), nameof(hud_trait_disabled));
        }
        foreach (object hud_trait in Dm.each(GLOB.trait_blockers_to_hud))
        {
            RegisterSignal(this, SIGNAL_ADDTRAIT(hud_trait), nameof(hud_trait_blocker_gained));
            RegisterSignal(this, SIGNAL_REMOVETRAIT(hud_trait), nameof(hud_trait_blocker_lost));
        }
    }

    // tg: code/modules/mob/mob.dm:1625
    [SignalHandler]
    public virtual void hud_trait_enabled(datum source = null, object new_trait = null)
    {
        foreach ((object blocker, object blocked_traits) in Dm.pairs(GLOB.trait_blockers_to_hud))
        {
            if (HAS_TRAIT(this, blocker) && (Dm.@in(new_trait, blocked_traits)))
                return;
        }

        datum_atom_hud datahud = GLOB.huds[GLOB.trait_to_hud[new_trait]] as datum_atom_hud;
        datahud.show_to(this);
    }

    // tg: code/modules/mob/mob.dm:1635
    [SignalHandler]
    public virtual void hud_trait_disabled(datum source = null, object lost_trait = null)
    {
        foreach ((object blocker, object blocked_traits) in Dm.pairs(GLOB.trait_blockers_to_hud))
        {
            if (HAS_TRAIT(this, blocker) && (Dm.@in(lost_trait, blocked_traits)))
                return; // it may seem counterintuitive to check for blockers on trait removal, the blocker now has total reign over whether the hud should come back
        }

        datum_atom_hud datahud = GLOB.huds[GLOB.trait_to_hud[lost_trait]] as datum_atom_hud;
        datahud.hide_from(this);
    }

    // tg: code/modules/mob/mob.dm:1645
    [SignalHandler]
    public virtual void hud_trait_blocker_gained(datum source = null, object new_trait = null)
    {
        foreach (object trait in Dm.each(GLOB.trait_blockers_to_hud[new_trait]))
        {
            if (!HAS_TRAIT(this, trait))
                continue;
            datum_atom_hud datahud = GLOB.huds[GLOB.trait_to_hud[trait]] as datum_atom_hud;
            datahud.hide_from(this);
        }
    }

    // tg: code/modules/mob/mob.dm:1654
    [SignalHandler]
    public virtual void hud_trait_blocker_lost(datum source = null, object new_trait = null)
    {
        foreach (object trait in Dm.each(GLOB.trait_blockers_to_hud[new_trait]))
        {
            if (!HAS_TRAIT(this, trait))
                continue;
            datum_atom_hud datahud = GLOB.huds[GLOB.trait_to_hud[trait]] as datum_atom_hud;
            datahud.show_to(this);
        }
    }

    /**
     * Returns the access list for this mob, most mobs don't have any access.
     */
    // tg: code/modules/mob/mob.dm:1666
    public virtual object get_access()
    {
        return Dm.list();
    }
}

// M4a contracts: the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M4a P2 translates
// them or leaves them for their milestone (docs/ai/specs/m4a-ownership.md). Overrides are not declared, so the inherited
// code keeps running until then.

// tg: code/modules/mob/mob.dm:551
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob.dm:551  GAME_VERB_CONTEXT(/mob, examinate, "Examine", "", null, /atom) (__DEFINES/verb.dm:39-58):
    // /mob/verb/examinate(atom/__context_target in world), which tg's own code calls as a proc (ShiftClick, the screen
    // objects). Rules 7.10's M4a exception (D128): the set statements (the verb panel and the right-click verb menu) stay
    // M9, and so does SSverbs; the verb's INVOKE_ASYNC(SSverbs, invoke_verb, src, /mob/verb/examinate, args +
    // list(VERB_ARG_CONTEXT_TARGET_KEY = __context_target), usr) resolves the context target into the typed argument
    // examinify (VERB_ARG_TYPED) and calls the body proc, which is done here directly (the body does not sleep: it queues or
    // calls run_examinate through its callback).
    public virtual void examinate(atom __context_target = null, params object[] __extra)
    {
        if (!Dm.truthy(__context_target))
        {
            // else INVOKE_ASYNC(SSverbs, invoke_verb, src, /mob/verb/examinate, args, usr): the verb typed in the command bar
            // with its argument chosen by the verb manager (M9)
            Dm.NotTranslated("code/modules/mob/mob.dm:551", "examinate: the verb without a context target (the verb manager, M10+)");
            return;
        }
        __gvb_examinate(Dm.list(Dm.kv("examinify", __context_target)));
    }
}
