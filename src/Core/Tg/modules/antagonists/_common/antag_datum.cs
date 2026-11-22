// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/_common/antag_datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A1 (D316): the whole file. Syndicate A3: the preferences menu's preview pictures (get_preview_icon, render_preview_outfit,
// finish_preview_icon: the port's universal icon is the /icon recipe, so the outfit and the icon are retyped, Appendix A); the custom
// objective's admin message (the admin link macros) is a deferred leaf (not-translated.md section 130); replace_banned_player's ghost
// poll since syndicate X2 (SSpolling). The var block is generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/antagonists/_common/antag_datum.dm:4  GLOBAL_LIST_EMPTY(antagonists)
    public DmList antagonists;

    // tg: code/modules/antagonists/_common/antag_datum.dm:4  (GLOBAL_MANAGED(antagonists, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalantagonists()
    {
        antagonists = Dm.list();
        Dm.append(ref gvars_datum_init_order, "antagonists");
    }
}

// tg: code/modules/antagonists/_common/antag_datum.dm:6
public partial class datum_antagonist : datum
{
    // tg: code/modules/antagonists/_common/antag_datum.dm:2  #define CUSTOM_OBJECTIVE_MAX_LENGTH 300 (file-local, #undef at :625)
    private const int CUSTOM_OBJECTIVE_MAX_LENGTH = 300;

    // tg: code/modules/antagonists/_common/antag_datum.dm:73
    public override async DmTask New(params object[] __extra)
    {
        Dm.append(GLOB.antagonists, this);
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:76
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.antagonists, this);
        if (owner != null)
            LAZYREMOVE(ref owner.antag_datums, this);
        QDEL_NULL(ref team_hud_ref);
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:84
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        if (!Dm.truthy(check_rights(R_ADMIN)))
            return null;

        //Some commands might delete/modify this datum clearing or changing owner
        datum_mind persistent_owner = owner;

        object commands = get_admin_commands();
        object admin_command = href_list["command"];
        if (!Dm.@in(admin_command, commands))
            return null;
        datum_callback call_async = ((DmList)commands)[admin_command] as datum_callback;
        await call_async.Invoke(usr);
        await persistent_owner.traitor_panel();
        return null;
    }

    ///ANTAGONIST UI STUFF

    // tg: code/modules/antagonists/_common/antag_datum.dm:120
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, ui_name, name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:126
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot) || ui.user != owner.current)
            return TRUE;
        switch (action)
        {
            case "change_objectives":
                await submit_player_objective();
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:135
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:138
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object dot = await base.ui_status(user, state);
        if (isobserver(user) && Dm.truthy(Dm.bitand(antag_flags, ANTAG_OBSERVER_VISIBLE_PANEL)))
            return UI_UPDATE;
        return dot;
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:143
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["antag_name"] = name;
        data["objectives"] = get_objectives();
        data["can_change_objective"] = can_assign_self_objectives;
        return data;
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:178
    public virtual object can_be_owned(datum_mind new_owner = null)
    {
        datum_mind tested = new_owner ?? owner;
        return Dm.num(!Dm.truthy(tested.has_antag_datum(type)));
    }

    //This will be called in add_antag_datum before owner assignment.
    //Should return antag datum without owner.
    // tg: code/modules/antagonists/_common/antag_datum.dm:184
    public virtual object specialization(datum_mind new_owner = null)
    {
        return this;
    }

    ///Called by the transfer_to() mind proc after the mind (mind.current and new_character.mind) has moved but before the player (key and client) is transferred.
    // tg: code/modules/antagonists/_common/antag_datum.dm:188
    [ShouldCallParent]
    public virtual async DmTask on_body_transfer(mob_living old_body = null, mob_living new_body = null)
    {
        if (old_body != null)
            await remove_innate_effects(old_body);
        if (old_body != null && old_body.stat != DEAD && !Dm.truthy(LAZYLEN(old_body.mind?.antag_datums)))
            old_body.remove_from_current_living_antags();
        datum_action_antag_info info_button = info_button_ref?.resolve() as datum_action_antag_info;
        if (info_button != null)
        {
            if (old_body != null)
                await info_button.Remove(old_body);
            await info_button.Grant(new_body);
        }
        if (antag_moodlet != null)
        {
            if (old_body != null)
                clear_antag_moodies(old_body);
            await give_antag_moodies(new_body);
        }
        await apply_innate_effects(new_body);
        if (new_body.stat != DEAD)
            new_body.add_to_current_living_antags();
    }

    //This handles the application of antag huds/special abilities
    // tg: code/modules/antagonists/_common/antag_datum.dm:208
    public virtual async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        return null;
    }

    //This handles the removal of antag huds/special abilities
    // tg: code/modules/antagonists/_common/antag_datum.dm:212
    public virtual async DmTask remove_innate_effects(mob_living mob_override = null)
    {
    }

    /// This is called when the antagonist is being mindshielded.
    // tg: code/modules/antagonists/_common/antag_datum.dm:216
    [SignalHandler]
    public virtual object pre_mindshield(mob implanter = null, mob_living mob_override = null)
    {
        return COMPONENT_MINDSHIELD_PASSED;
    }

    /// This is called when the antagonist is successfully mindshielded.
    // tg: code/modules/antagonists/_common/antag_datum.dm:221
    [SignalHandler]
    public virtual object on_mindshield(mob implanter = null, mob_living mob_override = null)
    {
        return null;
    }

    /// Handles adding and removing the clumsy mutation from clown antags. Gets called in apply/remove_innate_effects
    // tg: code/modules/antagonists/_common/antag_datum.dm:226
    public virtual async DmTask handle_clown_mutation(mob_living mob_override = null, object message = null, float? removing = null)
    {
        removing ??= TRUE;
        if (!ishuman(mob_override) || !is_clown_job(owner.assigned_role))
            return;
        mob_living_carbon_human human_override = (mob_living_carbon_human)mob_override;
        if (Dm.truthy(removing)) // They're a clown becoming an antag, remove clumsy
        {
            await human_override.dna.remove_mutation(typeof(datum_mutation_clumsy), MUTATION_SOURCE_CLOWN_CLUMSINESS);
            if (!Dm.truthy(silent) && Dm.truthy(message))
                to_chat(human_override, span_boldnotice(Dm.text("[]", message)));
        }
        else
            await human_override.dna.add_mutation(typeof(datum_mutation_clumsy), MUTATION_SOURCE_CLOWN_CLUMSINESS); // We're removing their antag status, add back clumsy
    }

    //Assign default team and creates one for one of a kind team antagonists
    // tg: code/modules/antagonists/_common/antag_datum.dm:239
    public virtual async DmTask create_team(datum_team team = null)
    {
    }

    ///Called by the add_antag_datum() mind proc after the instanced datum is added to the mind's antag_datums list.
    // tg: code/modules/antagonists/_common/antag_datum.dm:243
    [ShouldCallParent]
    public virtual async DmTask on_gain()
    {
        datum_action_antag_info info_button = null;
        if (owner == null)
            throw Dm.CRASH(Dm.text("[] ran on_gain() without a mind", this), "code/modules/antagonists/_common/antag_datum.dm", 247);
        if (owner.current == null)
            throw Dm.CRASH(Dm.text("[] ran on_gain() on a mind without a mob", this), "code/modules/antagonists/_common/antag_datum.dm", 249);
        if (Dm.truthy(ui_name)) //in the future, this should entirely replace greet.
        {
            info_button = Dm.@new<datum_action_antag_info>(this);
            if (Dm.truthy(Dm.bitand(antag_flags, ANTAG_OBSERVER_VISIBLE_PANEL)))
            {
                info_button.show_to_observers = TRUE;
                info_button.allow_observer_click = TRUE;
            }
            await info_button.Grant(owner.current);
            info_button_ref = WEAKREF(info_button) as datum_weakref;
        }
        if (!Dm.truthy(silent))
        {
            await greet();
            if (Dm.truthy(ui_name))
            {
                to_chat(owner.current, span_boldnotice("For more info, read the panel. You can always come back to it using the button in the top left."));
                await info_button.Trigger();
            }
            object type_policy = get_policy(Dm.text("[]", type)); // path to text
            if (Dm.truthy(type_policy))
                to_chat(owner.current, type_policy);
        }

        owner.desensitized_level *= Dm.num(max(DESENSITIZED_MINIMUM, desensitized_modifier));
        await apply_innate_effects();
        await give_antag_moodies();
        RegisterSignal(owner, COMSIG_PRE_MINDSHIELD_IMPLANT, nameof(pre_mindshield));
        RegisterSignal(owner, COMSIG_MINDSHIELD_IMPLANTED, nameof(on_mindshield));
        if (Dm.truthy(await is_banned(owner.current)) && Dm.truthy(replace_banned))
            replace_banned_player();
        else if (Dm.truthy(owner.current.client?.holder) && (Dm.truthy(config.Get(typeof(datum_config_entry_flag_auto_deadmin_antagonists))) || Dm.truthy(Dm.bitand(owner.current.client.prefs?.toggles, DEADMIN_ANTAGONIST))))
            owner.current.client.holder.auto_deadmin();
        if (owner.current.stat != DEAD && Dm.truthy(owner.current.client))
            owner.current.add_to_current_living_antags();

        foreach (object antag_hud in Dm.each(GLOB.active_alternate_appearances))
            ((datum_atom_hud_alternate_appearance)antag_hud).apply_to_new_mob(owner.current);

        LAZYADD(ref owner.special_roles, Dm.truthy(jobban_flag) ? jobban_flag : pref_flag);

        if (Dm.truthy(owner._listen_lookup?[COMSIG_ANTAGONIST_GAINED]))
            owner._SendSignal(COMSIG_ANTAGONIST_GAINED, Dm.list(owner, this));
    }

    /**
     * Proc that checks the sent mob aganst the banlistfor this antagonist.
     * Returns FALSE if no mob is sent, or the mob is not found to be banned.
     *
     *  * mob/player: The mob that you are looking for on the banlist.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:292
    public virtual async DmTask<object> is_banned(mob player = null)
    {
        if (player == null)
        {
            stack_trace("Called is_banned without a mob. This shouldn't happen.", "code/modules/antagonists/_common/antag_datum.dm", 294);
            return FALSE;
        }

        if (!Dm.truthy(player.ckey))
            return FALSE;

        return Dm.num(Dm.truthy(is_banned_from(player.ckey, Dm.list(ROLE_SYNDICATE, Dm.truthy(jobban_flag) ? jobban_flag : pref_flag))) || QDELETED(player));
    }

    /**
     * Proc that replaces a player who cannot play a specific antagonist due to being banned via a poll, and alerts the player of their being on the banlist.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:305
    // (set waitfor = FALSE: the wrapper of waiting-model.md 5.4; the ghosts' poll sleeps; syndicate X2 with SSpolling)
    public virtual void replace_banned_player()
    {
        Waiting.WaitforFalse(replace_banned_player__body());
    }

    private async DmTask replace_banned_player__body()
    {
        mob chosen_one = await SSpolling.poll_ghosts_for_target(check_jobban: Dm.truthy(jobban_flag) ? jobban_flag : pref_flag, role: pref_flag, poll_time: 5 * SECONDS, checked_target: owner.current, alert_pic: owner.current, role_name_text: name) as mob;
        if (chosen_one != null)
        {
            to_chat(owner, "Your mob has been taken over by a ghost! Appeal your job ban if you want to avoid this in the future!");
            message_admins(Dm.text("[] has taken control of ([]) to replace antagonist banned player.", key_name_admin(chosen_one), key_name_admin(owner)));
            log_game(Dm.text("[] has taken control of ([]) to replace antagonist banned player.", key_name(chosen_one), key_name(owner)));
            owner.current.ghostize(FALSE);
            owner.current.PossessByPlayer(chosen_one.key);
        }
        else
            log_game(Dm.text("Couldn't find antagonist ban replacement for ([]).", key_name(owner)));
    }

    /**
     * Called by the remove_antag_datum() and remove_all_antag_datums() mind procs for the antag datum to handle its own removal and deletion.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:321
    [ShouldCallParent]
    public virtual async DmTask on_removal()
    {
        if (owner == null)
            throw Dm.CRASH("Antag datum with no owner.", "code/modules/antagonists/_common/antag_datum.dm", 324);

        owner.desensitized_level /= Dm.num(max(DESENSITIZED_MINIMUM, desensitized_modifier));
        if (owner.current != null)
            await remove_innate_effects();
        clear_antag_moodies();
        LAZYREMOVE(ref owner.antag_datums, this);
        if (!Dm.truthy(LAZYLEN(owner.antag_datums)) && owner.current != null)
            owner.current.remove_from_current_living_antags();
        if (info_button_ref != null)
            QDEL_NULL(ref info_button_ref);
        if (!Dm.truthy(silent) && owner.current != null)
            await farewell();
        UnregisterSignal(owner, COMSIG_PRE_MINDSHIELD_IMPLANT);
        UnregisterSignal(owner, COMSIG_MINDSHIELD_IMPLANTED);
        datum_team team = get_team() as datum_team;
        if (team != null)
            await team.remove_member(owner);
        LAZYREMOVE(ref owner.special_roles, Dm.truthy(jobban_flag) ? jobban_flag : pref_flag);
        if (Dm.truthy(owner._listen_lookup?[COMSIG_ANTAGONIST_REMOVED]))
            owner._SendSignal(COMSIG_ANTAGONIST_REMOVED, Dm.list(owner, this));
        if (owner.current != null && Dm.truthy(owner.current._listen_lookup?[COMSIG_MOB_ANTAGONIST_REMOVED]))
            owner.current._SendSignal(COMSIG_MOB_ANTAGONIST_REMOVED, Dm.list(owner.current, this));
    }

    /**
     * Proc that sends fluff or instructional messages to the player when they are given this antag datum.
     * Use this proc for playing sounds, sending alerts, or helping to setup non-gameplay influencing aspects of the antagonist type.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:351
    public virtual async DmTask greet()
    {
        if (!Dm.truthy(silent))
        {
            to_chat(owner.current, span_big(Dm.text("You are \\the [].", this)));
            play_stinger();
        }
    }

    /// Plays the antag stinger sound, if we have one
    // tg: code/modules/antagonists/_common/antag_datum.dm:357
    public virtual void play_stinger()
    {
        if (stinger_sound == null)
            return;

        owner.current.playsound_local(get_turf(owner.current), stinger_sound, 100, FALSE, pressure_affected: FALSE, use_reverb: FALSE);
    }

    /**
     * Proc that sends fluff or instructional messages to the player when they lose this antag datum.
     * Use this proc for playing sounds, sending alerts, or otherwise informing the player that they're no longer a specific antagonist type.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:367
    public virtual async DmTask farewell()
    {
        if (!Dm.truthy(silent) && owner.current != null)
            to_chat(owner.current, span_userdanger(Dm.text("You are no longer \\the []!", this)));
    }

    /**
     * Proc that assigns this antagonist's ascribed moodlet to the player.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:374
    public virtual async DmTask give_antag_moodies(mob_living mob_override = null)
    {
        if (antag_moodlet == null)
            return;
        mob_living target = mob_override ?? owner.current as mob_living;
        await target.add_mood_event(Dm.text("antag_moodlet_[]", type), antag_moodlet);
    }

    /**
     * Proc that removes this antagonist's ascribed moodlet from the player.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:383
    public virtual void clear_antag_moodies(mob_living mob_override = null)
    {
        if (antag_moodlet == null)
            return;
        mob_living target = mob_override ?? owner.current as mob_living;
        target.clear_mood_event(Dm.text("antag_moodlet_[]", type));
    }

    /**
     * Proc that will return the team this antagonist belongs to, when called. Helpful with antagonists that may belong to multiple potential teams in a single round.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:392
    public virtual object get_team()
    {
        return null;
    }

    /**
     * Proc that sends string information for the end-round report window to the server.
     * This runs on every instance of every antagonist that exists at the end of the round.
     * This is the body of the message, sandwiched between roundend_report_header and roundend_report_footer.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:400
    public virtual object roundend_report()
    {
        DmList report = Dm.list();

        if (owner == null)
            throw Dm.CRASH("Antagonist datum without owner", "code/modules/antagonists/_common/antag_datum.dm", 404);

        Dm.append(ref report, printplayer(owner));

        bool objectives_complete = true;
        if (Dm.truthy(objectives.len))
        {
            Dm.append(ref report, printobjectives(objectives));
            foreach (datum_objective objective in Dm.each<datum_objective>(objectives))
            {
                if (!Dm.truthy(objective.check_completion()))
                {
                    objectives_complete = false;
                    break;
                }
            }
        }

        if (objectives.len == 0 || objectives_complete)
            Dm.append(ref report, Dm.text("<span class='greentext big'>The [] was successful!</span>", name));
        else
            Dm.append(ref report, Dm.text("<span class='redtext big'>The [] has failed!</span>", name));

        return report.Join("<br>");
    }

    /**
     * Proc that sends string data for the round-end report.
     * Displayed before roundend_report and roundend_report_footer.
     * Appears at start of roundend_catagory section.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:428
    public virtual object roundend_report_header()
    {
        return span_header(Dm.text("The [] were:<br>", roundend_category));
    }

    /**
     * Proc that sends string data for the round-end report.
     * Displayed after roundend_report and roundend_report_footer.
     * Appears at the end of the roundend_catagory section.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:436
    public virtual object roundend_report_footer()
    {
        return null;
    }

    //ADMIN TOOLS

    ///Called when using admin tools to give antag status
    // tg: code/modules/antagonists/_common/antag_datum.dm:443
    public virtual async DmTask admin_add(datum_mind new_owner = null, mob admin = null)
    {
        message_admins(Dm.text("[] made [] into [].", key_name_admin(admin), key_name_admin(new_owner), name));
        log_admin(Dm.text("[] made [] into [].", key_name(admin), key_name(new_owner), name));
        await new_owner.add_antag_datum(this);
    }

    ///Called when removing antagonist using admin tools
    // tg: code/modules/antagonists/_common/antag_datum.dm:449
    public virtual async DmTask admin_remove(mob user = null)
    {
        if (user == null)
            return;
        message_admins(Dm.text("[] has removed [] antagonist status from [].", key_name_admin(user), name, key_name_admin(owner)));
        log_admin(Dm.text("[] has removed [] antagonist status from [].", key_name(user), name, key_name(owner)));
        await on_removal();
    }

    /**
     * Additional data to display in the antagonist panel section.
     * For example, nuke disk code, genome count, etc
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:462
    public virtual object antag_panel_data()
    {
        return "";
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:465
    public virtual object enabled_in_preferences(datum_mind noggin = null)
    {
        if (Dm.truthy(pref_flag))
        {
            if (noggin.current != null && noggin.current.client != null && Dm.@in(pref_flag, noggin.current.client.prefs.be_special))
                return TRUE;
            else
                return FALSE;
        }
        return TRUE;
    }

    /// List of ["Command"] = CALLBACK(), user will be appeneded to callback arguments on execution
    // tg: code/modules/antagonists/_common/antag_datum.dm:474
    public virtual object get_admin_commands()
    {
        return Dm.list();
    }

    /// Custom implementors of `get_preview_icon` should use this, as the
    /// result of `get_preview_icon` is expected to be the completed version.
    // tg: code/modules/antagonists/_common/antag_datum.dm:480
    public virtual async DmTask<object> render_preview_outfit(object outfit = null, mob_living_carbon_human dummy = null)
    {
        dummy ??= Dm.@new<mob_living_carbon_human_dummy_consistent>();
        await dummy.equipOutfit(outfit, visuals_only: TRUE);
        if (dummy.wear_suit is obj_item suit)
            await suit.update_greyscale();
        dummy.set_combat_mode(TRUE);
        icon antag_icon = get_flat_uni_icon(dummy) as icon;

        // We don't want to qdel the dummy right away, since its items haven't initialized yet.
        SSatoms.prepare_deletion(dummy);

        return antag_icon;
    }

    /// Given a /datum/universal_icon, will crop it to be consistent of those in the preferences menu.
    /// Not necessary, and in fact will look bad if it's anything other than a human.
    // tg: code/modules/antagonists/_common/antag_datum.dm:494
    public virtual object finish_preview_icon(object antag_icon = null)
    {
        icon picture = antag_icon as icon;
        // Zoom in on the top of the head and the chest
        // I have no idea how to do this dynamically.
        picture.Scale(115, 115);

        // This is probably better as a Crop, but I cannot figure it out.
        picture.Shift(WEST, 8);
        picture.Shift(SOUTH, 30);

        picture.Crop(1, 1, ANTAGONIST_PREVIEW_ICON_SIZE, ANTAGONIST_PREVIEW_ICON_SIZE);

        return picture;
    }

    /// Returns the /datum/universal_icon to shown on the preferences menu.
    // tg: code/modules/antagonists/_common/antag_datum.dm:508
    public virtual async DmTask<object> get_preview_icon()
    {
        if (preview_outfit == null)
            return null;

        object preview_icon = await render_preview_outfit(preview_outfit);
        return finish_preview_icon(preview_icon);
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:515
    public virtual async DmTask edit_memory(mob user = null)
    {
        object new_memo = await tgui_input_text(user, "Write a new memory", "Antag Memory", antag_memory, multiline: TRUE);
        if (new_memo == null)
            return;
        antag_memory = Dm.str(new_memo);
    }

    /**
     * Gets how fast we can hijack the shuttle, return 0 for can not hijack.
     * Defaults to hijack_speed var, override for custom stuff like buffing hijack speed for hijack objectives or something.
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:525
    public virtual object hijack_speed__proc()
    {
        datum_objective_hijack H = Dm.locate<datum_objective_hijack>(objectives);
        return Dm.truthy(H?.hijack_speed_override) ? H.hijack_speed_override : hijack_speed;
    }

    /// Adds a HUD that will show you other members with the same antagonist.
    /// If an antag typepath is passed to `antag_to_check`, will check that, otherwise will use the source type.
    // tg: code/modules/antagonists/_common/antag_datum.dm:531
    public virtual void add_team_hud(mob target = null, object antag_to_check = null)
    {
        QDEL_NULL(ref team_hud_ref);

        object team = get_team();
        team_hud_ref = WEAKREF(target.add_alt_appearance(
            typeof(datum_atom_hud_alternate_appearance_basic_has_antagonist),
            Dm.text("antag_team_hud_[]", REF(this)),
            hud_image_on(target),
            Dm.truthy(antag_to_check) ? antag_to_check : type,
            Dm.truthy(team) ? WEAKREF(team as datum) : team
        ) as datum) as datum_weakref;

        // Add HUDs that they couldn't see before
        foreach (object antag_hud in Dm.each(GLOB.has_antagonist_huds))
            ((datum_atom_hud_alternate_appearance_basic_has_antagonist)antag_hud).apply_to_new_mob(owner.current);
    }

    /// Takes a location, returns an image drawing "on" it that matches this antag datum's hud icon
    // tg: code/modules/antagonists/_common/antag_datum.dm:547
    public virtual object hud_image_on(mob hud_loc = null)
    {
        image hud = image(hud_icon, hud_loc, antag_hud_name);
        SET_PLANE_EXPLICIT(hud, ABOVE_GAME_PLANE, hud_loc);
        return hud;
    }

    ///generic helper to send objectives as data through tgui.
    // tg: code/modules/antagonists/_common/antag_datum.dm:553
    public virtual object get_objectives()
    {
        float objective_count = 1;
        DmList objective_data = Dm.list();
        //all obj
        foreach (datum_objective objective in Dm.each<datum_objective>(objectives))
        {
            Dm.append(ref objective_data, Dm.list(Dm.list(
                Dm.kv("count", objective_count),
                Dm.kv("name", objective.objective_name),
                Dm.kv("explanation", objective.explanation_text),
                Dm.kv("complete", objective.completed))));
            objective_count++;
        }
        return objective_data;
    }

    /// Used to create objectives for the antagonist.
    // tg: code/modules/antagonists/_common/antag_datum.dm:568
    public virtual async DmTask forge_objectives(params object[] __extra)
    {
    }

    /**
     * Allows player to replace their objectives with a new one they wrote themselves.
     * Returns true if they did it successfully.
     * * retain_existing - If true, will just be added as a new objective instead of replacing existing ones.
     * * retain_escape - If true, will retain specifically 'escape alive' objectives (or similar)
     * * force - Skips the check about whether this antagonist is supposed to set its own objectives, for badminning
     */
    // tg: code/modules/antagonists/_common/antag_datum.dm:578
    public virtual async DmTask<object> submit_player_objective(float? retain_existing = null, float? retain_escape = null, float? force = null)
    {
        retain_existing ??= FALSE;
        retain_escape ??= TRUE;
        force ??= FALSE;
        if (owner == null || owner.current == null)
            return FALSE;
        mob_living owner_mob = owner.current as mob_living;
        if (!Dm.truthy(force) && !Dm.truthy(can_assign_self_objectives))
        {
            owner_mob.balloon_alert(owner_mob, "can't do that!");
            return FALSE;
        }
        object custom_objective_text = await tgui_input_text(
            owner_mob,
            message: "Specify your new objective.",
            title: "Custom Objective",
            @default: default_custom_objective,
            max_length: CUSTOM_OBJECTIVE_MAX_LENGTH);
        if (QDELETED(this) || QDELETED(owner_mob) || custom_objective_text == null)
            return FALSE; // Some people take a long-ass time to type maybe they got dusted

        log_game(Dm.text("[] []chose a custom objective: []", key_name(owner_mob), Dm.truthy(retain_existing) ? "" : "opted out of their original objectives and ", custom_objective_text));
        message_admins(Dm.text("[] has chosen a custom antagonist objective: [] | [] | []", ADMIN_LOOKUPFLW(owner_mob), span_syndradio(Dm.text("[]", custom_objective_text)), ADMIN_SMITE(owner_mob), ADMIN_SYNDICATE_REPLY(owner_mob)));

        datum_objective_custom custom_objective = Dm.@new<datum_objective_custom>();
        custom_objective.owner = owner;
        custom_objective.explanation_text = Dm.str(custom_objective_text);
        custom_objective.completed = TRUE;

        if (Dm.truthy(retain_existing))
            objectives.Insert(1, custom_objective);
        else if (!Dm.truthy(retain_escape))
            objectives = Dm.list(custom_objective);
        else
        {
            submit_player_objective__escape_objectives ??= Dm.list(
                typeof(datum_objective_escape),
                typeof(datum_objective_survive),
                typeof(datum_objective_martyr),
                typeof(datum_objective_exile));
            DmList escape_objectives = submit_player_objective__escape_objectives;
            foreach (datum_objective check_objective in Dm.each<datum_objective>(objectives.Copy()))
            {
                if (Dm.truthy(is_type_in_list(check_objective, escape_objectives)))
                    continue;
                Dm.remove(objectives, check_objective);
            }
            objectives.Insert(1, custom_objective);
        }

        can_assign_self_objectives = FALSE;
        await owner.announce_objectives();

        return TRUE;
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:608  var/static/list/escape_objectives (rules 10.6: a proc-level static)
    private static ref DmList submit_player_objective__escape_objectives => ref TgState.Current.submit_player_objective__escape_objectives;

    /// Return TRUE to prevent the antag's job from handling the respawn
    // tg: code/modules/antagonists/_common/antag_datum.dm:628
    public virtual async DmTask<object> on_respawn(mob new_character = null)
    {
        return FALSE;
    }

    /// Dissassociates the antag datum from its owner, without deleting it - allowing one datum and its objectives to be reused for another mind
    // tg: code/modules/antagonists/_common/antag_datum.dm:632
    public virtual async DmTask<object> store_datum()
    {
        if (owner == null)
        {
            stack_trace("Tried to store an antagonist datum that already has no owner.", "code/modules/antagonists/_common/antag_datum.dm", 634);
            return FALSE;
        }

        await on_removal();
        datum_team antag_team = get_team() as datum_team;
        if (antag_team != null)
            await antag_team.remove_member(owner);
        LAZYREMOVE(ref owner.antag_datums, this);
        owner = null;
        log_game(Dm.text("[] has lost antag datum [] ([]).", key_name(owner), this, type));
        QDEL_NULL(ref team_hud_ref);
        return TRUE;
    }

    /// Reassociates the antag datum with a new mind - allowing one datum and its objectives to be reused for another mind
    // tg: code/modules/antagonists/_common/antag_datum.dm:647
    public virtual async DmTask<object> restore_datum(datum_mind new_owner = null)
    {
        if (owner != null)
        {
            stack_trace("Tried to restore an antagonist datum that already has an owner.", "code/modules/antagonists/_common/antag_datum.dm", 649);
            return FALSE;
        }
        if (!Dm.truthy(can_be_owned(new_owner)))
            return FALSE;

        owner = new_owner;
        LAZYADD(ref owner.antag_datums, this);
        datum_team antag_team = get_team() as datum_team;
        if (antag_team != null)
            await antag_team.add_member(new_owner);
        await on_gain();
        log_game(Dm.text("[] has gained antag datum [] ([]).", key_name(new_owner), this, type));
        return TRUE;
    }
}

//This one is created by admin tools for custom objectives
// tg: code/modules/antagonists/_common/antag_datum.dm:100
public partial class datum_antagonist_custom : datum_antagonist
{
    // tg: code/modules/antagonists/_common/antag_datum.dm:105
    public override async DmTask create_team(datum_team team = null)
    {
        custom_team = team;
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:108
    public override object get_team()
    {
        return custom_team;
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:111
    public override async DmTask admin_add(datum_mind new_owner = null, mob admin = null)
    {
        object custom_name = await stripped_input(admin, "Custom antagonist name:", "Custom antag", "Antagonist");
        if (!Dm.truthy(custom_name))
            return;
        name = Dm.str(custom_name);
        await base.admin_add(new_owner, admin);
    }
}

//button for antags to review their descriptions/info
// tg: code/modules/antagonists/_common/antag_datum.dm:151
public partial class datum_action_antag_info : datum_action
{
    // tg: code/modules/antagonists/_common/antag_datum.dm:156
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        name = Dm.text("Open [] Information", target);
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:160
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;

        await target.ui_interact(Dm.truthy(clicker) ? clicker : owner);
        return dot;
    }

    // tg: code/modules/antagonists/_common/antag_datum.dm:167
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        if (target == null)
        {
            stack_trace(Dm.text("[] was used without a target antag datum!", type), "code/modules/antagonists/_common/antag_datum.dm", 169);
            return FALSE;
        }
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return dot;
        if (owner.mind == null || !Dm.@in(target, owner.mind.antag_datums))
            return FALSE;
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/antagonists/_common/antag_datum.dm:608  submit_player_objective's var/static/list/escape_objectives
    internal DmList submit_player_objective__escape_objectives;
}
