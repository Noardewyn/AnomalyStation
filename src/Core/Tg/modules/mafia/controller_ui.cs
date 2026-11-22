// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/controller_ui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): the MafiaPanel window's data and actions: the lobby, the role, the players with the actions the
// user can take on them, the chat log and notes, the judgement votes, and the admin controls. tg passes the PDA itself as the
// 'user' of the static data and the data (mafia_ntos.dm); the slot's parameter is a mob (rules 3.3), so the controller has an
// overload that takes an atom, which the slot's override calls with its mob.

// tg: code/modules/mafia/controller_ui.dm:1
public partial class datum_mafia_controller : datum
{
    // tg: code/modules/mafia/controller_ui.dm:2  'user' can be a modPC, hence why it's pathed to the atom
    public override async DmTask<object> ui_static_data(mob user = null) => await ui_static_data((atom)user);

    // tg: code/modules/mafia/controller_ui.dm:2
    public virtual async DmTask<object> ui_static_data(atom user)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        if (usr?.client?.holder != null)
            data["admin_controls"] = TRUE; //show admin buttons to start/setup/stop
        data["is_observer"] = isobserver(user) ? TRUE : FALSE;
        data["all_roles"] = current_setup_text;

        if (phase == MAFIA_PHASE_SETUP)
            return data;

        datum_mafia_role user_role = get_role_player(user) as datum_mafia_role;
        if (user_role != null)
        {
            data["roleinfo"] = Dm.list(
                Dm.kv("role", user_role.name),
                Dm.kv("desc", user_role.desc),
                Dm.kv("hud_icon", user_role.hud_icon),
                Dm.kv("revealed_icon", user_role.revealed_icon),
                Dm.kv("role_dead", user_role.game_status == MAFIA_DEAD ? TRUE : FALSE));
        }

        return data;
    }

    // tg: code/modules/mafia/controller_ui.dm:26  'user' can be a modPC, hence why it's pathed to the atom
    public override async DmTask<object> ui_data(mob user = null) => await ui_data((atom)user);

    // tg: code/modules/mafia/controller_ui.dm:26
    public virtual async DmTask<object> ui_data(atom user)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        data["phase"] = phase;
        if (Dm.truthy(turn))
            data["turn"] = I18n.T(" - Day []", turn);

        if (phase == MAFIA_PHASE_JUDGEMENT)
            data["person_voted_up_ref"] = REF(on_trial);
        if (phase == MAFIA_PHASE_SETUP)
        {
            data["lobbydata"] = Dm.list();
            foreach (object key in Dm.each(Dm.add(Dm.add(GLOB.mafia_signup, GLOB.mafia_bad_signup), GLOB.pda_mafia_signup)))
            {
                DmList lobby_member = Dm.list();
                lobby_member["name"] = key;
                lobby_member["status"] = Dm.@in(key, GLOB.mafia_bad_signup) ? "Disconnected" : "Ready";
                Dm.append_at(data, "lobbydata", Dm.list(lobby_member));
            }
            return data;
        }

        data["timeleft"] = Dm.truthy(next_phase_timer) ? timeleft(next_phase_timer) : 0;

        datum_mafia_role user_role = get_role_player(user) as datum_mafia_role;

        if (user_role != null)
        {
            data["user_notes"] = user_role.written_notes;
            data["player_voted_up"] = user_role == on_trial ? TRUE : FALSE;
            DmList ui_messages = Dm.list();
            for (float i = length(user_role.role_messages); i >= 1; i--)
                Dm.append(ref ui_messages, Dm.list(Dm.list(Dm.kv("msg", user_role.role_messages[i]))));
            data["messages"] = ui_messages;
        }

        data["players"] = Dm.list();
        foreach (datum_mafia_role role in Dm.each_cast<datum_mafia_role>(all_roles))
        {
            DmList player_info = Dm.list();
            player_info["name"] = role.body.real_name;
            player_info["ref"] = REF(role);
            player_info["alive"] = role.game_status == MAFIA_ALIVE ? TRUE : FALSE;
            player_info["role_revealed"] = FALSE;
            if (Dm.truthy(Dm.bitand(role.role_flags, ROLE_REVEALED)))
                player_info["role_revealed"] = role.name;
            player_info["possible_actions"] = Dm.list();

            if (user_role != null) //not observer
            {
                player_info["is_you"] = role.body.real_name == user_role.body.real_name ? TRUE : FALSE;
                foreach (datum_mafia_ability action in Dm.each_cast<datum_mafia_ability>(user_role.role_unique_actions))
                {
                    if (Dm.truthy(action.validate_action_target(potential_target: role, silent: TRUE)))
                        Dm.append_at(player_info, "possible_actions", Dm.list(Dm.list(Dm.kv("name", action), Dm.kv("ref", REF(action)))));
                }
            }

            Dm.append_at(data, "players", Dm.list(player_info));
        }

        return data;
    }

    // tg: code/modules/mafia/controller_ui.dm:85
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/mafia)): the asset transport is the client's (DV44); the MafiaPanel window
        // draws the roles' pictures from tg's icons/obj/mafia.dmi, which the client holds.
        await DmTask.CompletedTask;
        return Dm.list();
    }

    // tg: code/modules/mafia/controller_ui.dm:90
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        datum_mafia_role user_role = get_role_player(usr, ui) as datum_mafia_role;
        obj_item_modular_computer modpc = ui.src_object as obj_item_modular_computer;

        //Admin actions
        if (ui.user.client.holder != null)
        {
            switch (Dm.str(action))
            {
                case "new_game":
                    if (phase == MAFIA_PHASE_SETUP)
                        return dot;
                    await basic_setup();
                    break;
                case "nuke":
                    qdel(this);
                    break;
                case "next_phase":
                {
                    if (phase == MAFIA_PHASE_SETUP)
                        return dot;
                    datum_timedevent timer = SStimer.timer_id_dict[next_phase_timer] as datum_timedevent;
                    if (!Dm.truthy(timer.spent))
                    {
                        datum_callback tc = timer.callBack;
                        deltimer(next_phase_timer);
                        tc.InvokeAsync();
                    }
                    return TRUE;
                }
                case "players_home":
                {
                    DmList failed = Dm.list();
                    foreach (datum_mafia_role player in Dm.each<datum_mafia_role>(all_roles))
                    {
                        if (player.body == null)
                        {
                            Dm.append(ref failed, player);
                            continue;
                        }
                        await player.body.forceMove(get_turf(player.assigned_landmark));
                    }
                    if (Dm.truthy(length(failed)))
                    {
                        to_chat(usr, I18n.T("List of players who no longer had a body (if you see this, the game is runtiming anyway so just hit \"New Game\" to end it)"));
                        foreach (datum_mafia_role fail in Dm.each_cast<datum_mafia_role>(failed))
                            to_chat(usr, Dm.truthy(fail.player_key) ? fail.player_key : fail.player_pda);
                    }
                    break;
                }
                case "debug_setup":
                {
                    DmList debug_setup = Dm.list();
                    DmList rolelist_dict = Dm.add(Dm.list("CANCEL", "FINISH"), GLOB.mafia_roles_by_name) as DmList;
                    bool done = false;

                    while (!done)
                    {
                        to_chat(usr, I18n.T("You have a total player count of [] in this setup.", counterlist_sum(debug_setup)));
                        object chosen_role_name = await tgui_input_list(usr, I18n.T("Select a role!"), I18n.T("Custom Setup Creation"), rolelist_dict);
                        if (!Dm.truthy(chosen_role_name))
                            return dot;
                        switch (Dm.str(chosen_role_name))
                        {
                            case "CANCEL":
                                done = true;
                                return dot;
                            case "FINISH":
                                done = true;
                                break;
                            default:
                            {
                                object found_path = rolelist_dict[chosen_role_name];
                                object role_count = await tgui_input_number(usr, I18n.T("How many? Zero to cancel."), I18n.T("Custom Setup Creation"), 0, 12);
                                if (Dm.num(role_count) > 0)
                                    debug_setup[found_path] = role_count;
                                break;
                            }
                        }
                    }
                    custom_setup = debug_setup;
                    early_start = TRUE;
                    await try_autostart(); //don't worry, this fails if there's a game in progress
                    break;
                }
                case "cancel_setup":
                    custom_setup = Dm.list();
                    break;
                case "start_now":
                    await forced_setup();
                    break;
            }
        }

        if (user_role == null) // non-player & pre-game actions.
        {
            switch (Dm.str(action))
            {
                case "mf_signup":
                    await signup_mafia(ui.user, ui.user.client, modpc);
                    break;
                case "vote_to_start":
                {
                    client ghost_client = ui.user.client;
                    if (phase != MAFIA_PHASE_SETUP)
                    {
                        to_chat(usr, span_notice(I18n.T("You cannot vote to start while a game is underway!")));
                        return dot;
                    }
                    if (modpc == null)
                    {
                        if (!Dm.truthy(GLOB.mafia_signup[ghost_client.ckey]))
                        {
                            to_chat(usr, span_notice(I18n.T("You must be signed up for this game to vote!")));
                            return dot;
                        }
                        if (Dm.truthy(GLOB.mafia_early_votes[ghost_client.ckey]))
                        {
                            Dm.remove(ref GLOB.mafia_early_votes, ghost_client.ckey);
                            to_chat(usr, span_notice(I18n.T("You are no longer voting to start the game early.")));
                        }
                        else
                        {
                            GLOB.mafia_early_votes[ghost_client.ckey] = ghost_client;
                            to_chat(usr, span_notice(I18n.T("You vote to start the game early ([] out of []).", length(GLOB.mafia_early_votes), Dm.max(round(length(Dm.add(GLOB.mafia_signup, GLOB.pda_mafia_signup)) / 2f), round(MAFIA_MIN_PLAYER_COUNT / 2f)))));
                            if (Dm.truthy(check_start_votes())) //See if we have enough votes to start
                                await forced_setup();
                        }
                    }
                    else
                    {
                        if (!Dm.truthy(GLOB.pda_mafia_signup[modpc]))
                        {
                            to_chat(usr, span_notice(I18n.T("You must be signed up for this game to vote!")));
                            return dot;
                        }
                        if (Dm.truthy(GLOB.mafia_early_votes[modpc]))
                        {
                            Dm.remove(ref GLOB.mafia_early_votes, modpc);
                            to_chat(usr, span_notice(I18n.T("You are no longer voting to start the game early.")));
                        }
                        else
                        {
                            GLOB.mafia_early_votes[modpc] = modpc;
                            to_chat(usr, span_notice(I18n.T("You vote to start the game early ([] out of []).", length(GLOB.mafia_early_votes), Dm.max(round(length(Dm.add(GLOB.mafia_signup, GLOB.pda_mafia_signup)) / 2f), round(MAFIA_MIN_PLAYER_COUNT / 2f)))));
                            if (Dm.truthy(check_start_votes())) //See if we have enough votes to start
                                await forced_setup();
                        }
                    }
                    break;
                }
            }
            return TRUE;
        }

        switch (Dm.str(action)) //actions that both living and dead players can perform.
        {
            case "mf_lookup":
            {
                object role_lookup = @params["role_name"];
                datum_mafia_role helper = null;
                foreach (datum_mafia_role role in Dm.each_cast<datum_mafia_role>(all_roles))
                {
                    if (Dm.eq(role_lookup, role.name))
                    {
                        helper = role;
                        break;
                    }
                }
                helper.show_help(usr);
                break;
            }
            case "change_notes":
                user_role.written_notes = sanitize_text(@params["new_notes"]);
                user_role.send_message_to_player(I18n.T("notes saved"), balloon_alert: TRUE);
                return TRUE;
            case "send_notes_to_chat":
            {
                if (!Dm.truthy(user_role.written_notes))
                    return TRUE;
                if (!(user_role.note_chat_sending_cooldown <= world.time)) // COOLDOWN_FINISHED
                    return FALSE;
                user_role.note_chat_sending_cooldown = world.time + MAFIA_NOTE_SENDING_COOLDOWN; // COOLDOWN_START
                DmList message_mods = Dm.list();
                message_mods[MANNEQUIN_CONTROLLED] = ui.user;
                await user_role.body.say(Dm.text("[]", user_role.written_notes), forced: "mafia notes sending", message_mods: message_mods);
                return TRUE;
            }
            case "send_message_to_chat":
            {
                object message_said = sanitize_text(@params["message"]);
                DmList message_mods = Dm.list();
                message_mods[MANNEQUIN_CONTROLLED] = ui.user;
                await user_role.body.say(message_said, forced: Dm.text("mafia chat (sent by [])", ui.user.client), message_mods: message_mods);
                break;
            }
        }

        if (user_role.game_status == MAFIA_DEAD)
            return TRUE;

        switch (Dm.str(action)) //actions that only living players can perform.
        {
            case "perform_action":
            {
                object located_target = Dm.locate_ref(@params["target"]);
                datum_mafia_role target = Dm.@in(located_target, all_roles) ? located_target as datum_mafia_role : null;
                if (target == null)
                    return dot;
                object located_action = Dm.locate_ref(@params["action_ref"]);
                datum_mafia_ability used_action = Dm.@in(located_action, user_role.role_unique_actions) ? located_action as datum_mafia_ability : null;
                if (used_action == null)
                    return dot;
                switch (phase)
                {
                    case MAFIA_PHASE_DAY:
                    case MAFIA_PHASE_VOTING:
                        used_action.using_ability = TRUE;
                        await used_action.perform_action_target(this, target);
                        break;
                    case MAFIA_PHASE_NIGHT:
                        used_action.set_target(target);
                        break;
                }
                return TRUE;
            }
        }

        if (user_role != on_trial)
        {
            switch (Dm.str(action)) // actions that can only be done while someone is on stand (that isn't you)
            {
                case "vote_abstain":
                    if (phase != MAFIA_PHASE_JUDGEMENT || Dm.@in(user_role, judgement_abstain_votes))
                        return dot;
                    user_role.send_message_to_player(I18n.T("You have decided to abstain."));
                    Dm.remove(ref judgement_innocent_votes, user_role);
                    Dm.remove(ref judgement_guilty_votes, user_role);
                    Dm.append(ref judgement_abstain_votes, user_role);
                    break;
                case "vote_innocent":
                    if (phase != MAFIA_PHASE_JUDGEMENT || Dm.@in(user_role, judgement_innocent_votes))
                        return dot;
                    user_role.send_message_to_player(I18n.T("Your vote on [] submitted as INNOCENT!", on_trial.body.real_name));
                    Dm.remove(ref judgement_abstain_votes, user_role); //no fakers, and...
                    Dm.remove(ref judgement_guilty_votes, user_role); //no radical centrism
                    Dm.append(ref judgement_innocent_votes, user_role);
                    break;
                case "vote_guilty":
                    if (phase != MAFIA_PHASE_JUDGEMENT || Dm.@in(user_role, judgement_guilty_votes))
                        return dot;
                    user_role.send_message_to_player(I18n.T("Your vote on [] submitted as GUILTY!", on_trial.body.real_name));
                    Dm.remove(ref judgement_abstain_votes, user_role); //no fakers, and...
                    Dm.remove(ref judgement_innocent_votes, user_role); //no radical centrism
                    Dm.append(ref judgement_guilty_votes, user_role);
                    break;
            }
        }
        return dot;
    }

    // tg: code/modules/mafia/controller_ui.dm:257
    public override object ui_state(mob user = null) => GLOB.always_state;

    // tg: code/modules/mafia/controller_ui.dm:260
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, null) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "MafiaPanel");
            await ui.open();
        }
        return null;
    }
}
