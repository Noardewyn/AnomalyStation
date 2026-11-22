// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): the Mafia controller: the map loaded on CentCom's game area, the roles dealt to the signed-up
// ghosts and PDAs, the bodies, the day, voting, judgement, lynch and night phases with their timers, the night curtains, the votes
// shown over the bodies, victory, and the signups; the Mafia panel action and the red popup. The achievements a role wins are
// recorded leaves (not-translated section 164). The var blocks are generated (D111).

public static partial class GlobalProcs
{
    // tg: code/modules/mafia/controller.dm:88
    public static object setup_mafia_roles_by_name()
    {
        DmList rolelist_dict = Dm.list();
        foreach (Type mafia_role in Dm.each_cast<Type>(typesof(typeof(datum_mafia_role))))
            rolelist_dict[Dm.text("[] ([])", Dm.initial(mafia_role, (datum_mafia_role r) => r.name), uppertext(Dm.initial(mafia_role, (datum_mafia_role r) => r.role_type)))] = mafia_role;
        return rolelist_dict;
    }

    // tg: code/modules/mafia/controller.dm:94
    public static object setup_mafia_role_by_alignment()
    {
        DmList rolelist_dict = Dm.list();
        foreach (Type mafia_role in Dm.each_cast<Type>(typesof(typeof(datum_mafia_role))))
        {
            string role_type = Dm.initial(mafia_role, (datum_mafia_role r) => r.role_type);
            if (!Dm.truthy(rolelist_dict[role_type]))
                rolelist_dict[role_type] = Dm.list();
            Dm.append_at(rolelist_dict, role_type, mafia_role);
        }
        return rolelist_dict;
    }

    /**
     * Creates the global datum for playing mafia games, destroys the last if that's required and returns the new.
     */
    // tg: code/modules/mafia/controller.dm:915
    public static datum_mafia_controller create_mafia_game()
    {
        if (Dm.truthy(GLOB.mafia_game))
        {
            qdel(GLOB.mafia_game);
            GLOB.mafia_game = null;
        }
        datum_mafia_controller new_controller = Dm.@new<datum_mafia_controller>();
        return new_controller;
    }
}

/**
 * The mafia controller handles the mafia minigame in progress.
 * It is first created when the first ghost signs up to play.
 */
// tg: code/modules/mafia/controller.dm:14
public partial class datum_mafia_controller : datum
{
    ///The 12 roles used in a default game, selected randomly from each list, going in order of position.
    ///This is balanced for player amount, regardless of players you'll still be about equal town and evils.
    // tg: code/modules/mafia/controller.dm:64  var/static/list/default_role_list (rules 10.6)
    private static DmList default_role_list => TgState.Current.datum_mafia_controller__default_role_list ??= Dm.list(
        //town
        Dm.list(TOWN_INVEST, TOWN_OVERFLOW),
        Dm.list(TOWN_SUPPORT),
        //mafia
        Dm.list(MAFIA_REGULAR),
        //town
        Dm.list(TOWN_INVEST),
        //neutral
        Dm.list(NEUTRAL_DISRUPT),
        //town
        Dm.list(TOWN_PROTECT),
        Dm.list(TOWN_OVERFLOW),
        //mafia hard-hitting
        Dm.list(MAFIA_REGULAR, MAFIA_SPECIAL),
        //town
        Dm.list(TOWN_PROTECT, TOWN_KILLING),
        Dm.list(TOWN_KILLING),
        //neutral hard-hitting
        Dm.list(NEUTRAL_KILL, NEUTRAL_DISRUPT),
        //town
        Dm.list(TOWN_SUPPORT, TOWN_OVERFLOW));

    // tg: code/modules/mafia/controller.dm:102
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        GLOB.mafia_game = this;
        map_deleter = Dm.@new<datum_map_generator_massdelete>();
    }

    // tg: code/modules/mafia/controller.dm:107
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        end_game();
        player_role_lookup.Cut();
        QDEL_NULL(ref map_deleter);
        if (GLOB.mafia_game == this)
            GLOB.mafia_game = null;
        return dot;
    }

    /**
     * Triggers at beginning of the game when there is a confirmed list of valid, ready players.
     * Creates a 100% ready game that has NOT started (no players in bodies)
     * Followed by start game
     */
    // tg: code/modules/mafia/controller.dm:129
    public virtual async DmTask prepare_game(object setup_list = null, object ready_ghosts_and_pdas = null)
    {
        DmList possible_maps = TgState.Current.datum_mafia_controller__possible_maps ??= subtypesof(typeof(datum_map_template_mafia)) as DmList;
        turf spawn_area = get_turf(Dm.locate_type(typeof(obj_effect_landmark_mafia_game_area), GLOB.landmarks_list) as atom);

        Type current_map_type = pick(possible_maps) as Type;
        current_map = await Dm.new_async(current_map_type) as datum_map_template_mafia;

        if (spawn_area == null)
            throw CRASH("No spawn area detected for Mafia!", "code/modules/mafia/controller.dm", 137);
        DmList bounds = await current_map.load(spawn_area) as DmList;
        if (bounds == null)
            throw CRASH("Loading mafia map failed!", "code/modules/mafia/controller.dm", 140);
        map_deleter.defineRegion(spawn_area, locate(spawn_area.x + 23, spawn_area.y + 23, spawn_area.z) as turf, replace: TRUE); //so we're ready to mass delete when round ends
        if (!Dm.truthy(length(landmarks))) //we grab town center when we grab landmarks, if there is none (the first game signed up for let's grab them post load)
        {
            foreach (obj_effect_landmark_mafia possible_spawn in Dm.each<obj_effect_landmark_mafia>(GLOB.landmarks_list))
            {
                if (istype(possible_spawn, typeof(obj_effect_landmark_mafia_town_center)))
                    town_center_landmark = possible_spawn;
                else
                    Dm.append(ref landmarks, possible_spawn);
            }
        }

        current_setup_text = Dm.list();
        foreach (object rtype in Dm.each(setup_list))
        {
            for (int i = 1; i <= Dm.num((setup_list as DmList)[rtype]); i++)
            {
                datum_mafia_role new_role = Dm.@new(rtype as Type, this) as datum_mafia_role;
                Dm.append(ref all_roles, new_role);
                Dm.append(ref living_roles, new_role);
            }
            Type role_type = rtype as Type;
            Dm.append(ref current_setup_text, I18n.T("[] x[]", Dm.initial(role_type, (datum_mafia_role r) => r.name), (setup_list as DmList)[role_type]));
        }
        DmList spawnpoints = landmarks.Copy();
        foreach (datum_mafia_role role in Dm.each_cast<datum_mafia_role>(all_roles))
        {
            role.assigned_landmark = pick_n_take(spawnpoints) as obj_effect_landmark_mafia;
            object selected_player; //this can be a ckey or a PDA
            selected_player = pick(ready_ghosts_and_pdas as DmList);
            client player_client = GLOB.directory[selected_player] as client;
            if (player_client != null)
                role.player_key = selected_player;
            else
                role.player_pda = selected_player as obj_item_modular_computer;
            DmList ready = ready_ghosts_and_pdas as DmList;
            Dm.remove(ref ready, selected_player);
        }
    }

    /**
     * send_message
     * By default, this will send a message to every single role in the game, and putting it in their role message history
     * to view on their PDA/Mafia panel.
     */
    // tg: code/modules/mafia/controller.dm:183
    public virtual void send_message(object msg = null, object team = null, float? log_only = null)
    {
        log_only ??= FALSE;
        foreach (datum_mafia_role role in Dm.each_cast<datum_mafia_role>(all_roles))
        {
            if (Dm.truthy(team) && !Dm.truthy(Dm.bitand(role.team, team)))
                continue;
            //people who can "hear" the dead, but are alive, can only hear at night.
            if (Dm.truthy(Dm.bitand(team, MAFIA_TEAM_DEAD)) && role.game_status == MAFIA_ALIVE && phase != MAFIA_PHASE_NIGHT)
                return;
            Dm.append(ref role.role_messages, msg);
            if (!Dm.truthy(log_only))
                to_chat(role.body, msg);
        }
    }

    /**
     * The game by this point is now all set up, and so we can put people in their bodies and start the first phase.
     */
    // tg: code/modules/mafia/controller.dm:201
    public virtual async DmTask start_game()
    {
        await create_bodies();
        // SEND_GLOBAL_SIGNAL(COMSIG_MAFIA_GAME_START, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_MAFIA_GAME_START]))
            SSdcs._SendSignal(COMSIG_MAFIA_GAME_START, Dm.list(SSdcs, this));
        await start_day(can_vote: FALSE);
        send_message(span_notice(I18n.T("<b>The selected map is []!</b></br> []", current_map.name, current_map.description)));
        send_message(I18n.T("<b>Day [] started! There is no voting on the first day. Say hello to everybody!</b>", turn));
        next_phase_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(check_trial), FALSE), FIRST_DAY_PERIOD_LENGTH / time_speedup, TIMER_STOPPABLE, file: "code/modules/mafia/controller.dm", line: 207); //no voting period = no votes = instant night
        foreach (datum_mafia_role roles in Dm.each_cast<datum_mafia_role>(all_roles))
        {
            obj_item_modular_computer modpc = roles.player_pda;
            if (modpc == null)
            {
                await update_static_data(roles.body);
                continue;
            }
            await modpc.update_static_data_for_all_viewers();
        }
    }

    /**
     * How every day starts.
     */
    // tg: code/modules/mafia/controller.dm:222
    public virtual async DmTask start_day(float? can_vote = null)
    {
        can_vote ??= TRUE;
        turn += 1;
        phase = MAFIA_PHASE_DAY;
        if (Dm.truthy(await check_victory()))
            return;

        if (!Dm.truthy(time_speedup)) //lets check if the game should be sped up, if not already.
        {
            if (length(living_roles) < length(all_roles) / 2f)
            {
                time_speedup = MAFIA_SPEEDUP_INCREASE;
                send_message(I18n.T("<span class='bold notice'>With only [] living players left, the game timers have been sped up.</span>", length(living_roles)));
            }
        }

        if (Dm.truthy(can_vote))
        {
            send_message(I18n.T("<b>Day [] started! Voting will start in 1 minute.</b>", turn));
            next_phase_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(start_voting_phase)), DAY_PERIOD_LENGTH / time_speedup, TIMER_STOPPABLE, file: "code/modules/mafia/controller.dm", line: 236);
        }
    }

    /**
     * Players have finished the discussion period, and now must put up someone to the chopping block.
     */
    // tg: code/modules/mafia/controller.dm:244
    public virtual void start_voting_phase()
    {
        phase = MAFIA_PHASE_VOTING;
        next_phase_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(check_trial), TRUE), VOTING_PERIOD_LENGTH / time_speedup, TIMER_STOPPABLE, file: "code/modules/mafia/controller.dm", line: 246); //be verbose!
        send_message(I18n.T("<b>Voting started! Vote for who you want to see on trial today.</b>"));
    }

    /**
     * Players have voted someone up, and now the person must defend themselves while the town votes innocent or guilty.
     */
    // tg: code/modules/mafia/controller.dm:259
    public virtual async DmTask check_trial(float? verbose = null)
    {
        verbose ??= TRUE;
        datum_mafia_role loser = get_vote_winner("Day") as datum_mafia_role; //, majority_of_town = TRUE)
        float loser_votes = get_vote_count(loser, "Day");
        if (loser != null)
        {
            if (loser_votes > UNIVERSALLY_HATED_REQUIREMENT)
                award_role(typeof(datum_award_achievement_mafia_universally_hated), loser);
            //refresh the lists
            judgement_abstain_votes = Dm.list();
            judgement_innocent_votes = Dm.list();
            judgement_guilty_votes = Dm.list();

            foreach (datum_mafia_role voters in Dm.each_cast<datum_mafia_role>(living_roles))
            {
                if (voters == loser)
                    continue;
                voters.mafia_alert.update_text(I18n.T("[] wins the day vote, Listen to their defense and vote INNOCENT or GUILTY!", loser.body.real_name));
                Dm.append(ref judgement_abstain_votes, voters);
            }

            on_trial = loser;
            await on_trial.body.forceMove(get_turf(town_center_landmark as atom));
            phase = MAFIA_PHASE_JUDGEMENT;
            next_phase_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(lynch)), JUDGEMENT_PERIOD_LENGTH / time_speedup, TIMER_STOPPABLE, file: "code/modules/mafia/controller.dm", line: 279);
            reset_votes("Day");
        }
        else
        {
            lockdown();
            if (Dm.truthy(verbose))
                send_message(I18n.T("<b>Not enough people have voted to put someone on trial, nobody will be lynched today.</b>"));
        }
    }

    /**
     * Players have voted innocent or guilty on the person on trial, and that person is now killed or returned home.
     */
    // tg: code/modules/mafia/controller.dm:293
    public virtual async DmTask lynch()
    {
        foreach (datum_mafia_role role in Dm.each_cast<datum_mafia_role>(judgement_abstain_votes))
            send_message(span_comradio(I18n.T("[] abstained.", role.body.real_name)));

        object total_innocent_votes = null;
        foreach (datum_mafia_role role in Dm.each_cast<datum_mafia_role>(judgement_innocent_votes))
        {
            send_message(span_green(I18n.T("[] voted innocent.", role.body.real_name)));
            total_innocent_votes = Dm.add(total_innocent_votes, role.vote_power);
        }

        object total_guilty_votes = null;
        foreach (datum_mafia_role role in Dm.each_cast<datum_mafia_role>(judgement_guilty_votes))
        {
            send_message(span_red(I18n.T("[] voted guilty.", role.body.real_name)));
            total_guilty_votes = Dm.add(total_guilty_votes, role.vote_power);
        }

        if (Dm.num(total_guilty_votes) > Dm.num(total_innocent_votes)) //strictly need majority guilty to lynch
        {
            send_message(span_red(I18n.T("<b>Guilty wins majority, [] has been lynched.</b>", on_trial.body.real_name)));
            await on_trial.kill(this, lynch: TRUE);
            next_phase_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(send_home), on_trial), LYNCH_PERIOD_LENGTH / time_speedup, TIMER_STOPPABLE, file: "code/modules/mafia/controller.dm", line: 313);
        }
        else
        {
            send_message(span_green(I18n.T("<b>Innocent wins majority, [] has been spared.</b>", on_trial.body.real_name)));
            await on_trial.body.forceMove(get_turf(on_trial.assigned_landmark));
        }
        on_trial = null;
        if (!Dm.truthy(await check_victory()))
        {
            //day votes are already cleared, so this will skip the trial and check victory/lockdown/whatever else
            next_phase_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(check_trial), FALSE), LYNCH_PERIOD_LENGTH / time_speedup, TIMER_STOPPABLE, file: "code/modules/mafia/controller.dm", line: 320); // small pause to see the guy dead, no verbosity since we already did this
        }
    }

    /**
     * Teenie helper proc to move players back to their home.
     */
    // tg: code/modules/mafia/controller.dm:325
    public virtual async DmTask send_home(datum_mafia_role role = null)
    {
        await role.body.forceMove(get_turf(role.assigned_landmark));
    }

    /**
     * Checks to see if a faction (or solo antagonist) has won.
     */
    // tg: code/modules/mafia/controller.dm:338
    public virtual async DmTask<object> check_victory()
    {
        DmList living_town = Dm.list();
        DmList living_mafia = Dm.list();
        DmList living_neutrals = Dm.list();

        DmList neutral_killers = Dm.list();

        //voting power of town + solos (since they don't want mafia to overpower)
        float anti_mafia_power = 0;
        //whether town has a role that can theoretically get someone killed singlehandedly.
        bool town_can_kill = false;

        foreach (datum_mafia_role R in Dm.each_cast<datum_mafia_role>(living_roles))
        {
            if (Dm.truthy(Dm.bitand(R.team, MAFIA_TEAM_MAFIA)))
                Dm.append(ref living_mafia, R);
            else if (Dm.truthy(Dm.bitand(R.team, MAFIA_TEAM_TOWN)))
            {
                Dm.append(ref living_town, R);
                anti_mafia_power += R.vote_power;
                //the game cannot autoresolve with killing roles (unless a solo wins anyways, like traitors who are immune)
                if (Dm.truthy(Dm.bitand(R.role_flags, ROLE_CAN_KILL)))
                    town_can_kill = true;
            }
            else if (Dm.truthy(Dm.bitand(R.team, MAFIA_TEAM_SOLO)))
            {
                Dm.append(ref living_neutrals, R);
                anti_mafia_power += R.vote_power;
                if (Dm.truthy(Dm.bitand(R.role_flags, ROLE_CAN_KILL)))
                    Dm.append(ref neutral_killers, R);
            }
            else
                stack_trace(Dm.text("[] somehow lacks a faction while alive in a Mafia match!", R), "code/modules/mafia/controller.dm", 370);
        }

        if (Dm.truthy(length(living_mafia)) && Dm.truthy(length(living_town)) && Dm.truthy(length(living_neutrals)))
            return FALSE;

        string victory_message = null;

        if (length(living_mafia) + length(living_town) <= 0)
        {
            victory_message = I18n.T("Draw!</span>"); //this is in-case no neutrals won, but there's no town/mafia left.
            foreach (datum_mafia_role solo in Dm.each_cast<datum_mafia_role>(neutral_killers))
            {
                victory_message = I18n.T("[] VICTORY!</span>", uppertext(solo.name));
                if (!Dm.truthy(early_start))
                    award_role(solo.winner_award, solo);
            }
        }
        else if (!Dm.truthy(length(living_mafia)) && !Dm.truthy(length(neutral_killers)))
        {
            victory_message = I18n.T("TOWN VICTORY!</span>");
            if (!Dm.truthy(early_start))
            {
                foreach (datum_mafia_role townie in Dm.each_cast<datum_mafia_role>(living_town))
                    award_role(townie.winner_award, townie);
            }
        }
        else if (length(living_mafia) >= anti_mafia_power && !town_can_kill && !Dm.truthy(length(neutral_killers)))
        {
            victory_message = I18n.T("MAFIA VICTORY!</span>");
            if (!Dm.truthy(early_start))
            {
                foreach (datum_mafia_role changeling in Dm.each_cast<datum_mafia_role>(living_mafia))
                    award_role(changeling.winner_award, changeling);
            }
        }

        if (victory_message != null)
        {
            await start_the_end(victory_message);
            return TRUE;
        }
        return FALSE;
    }

    /**
     * Lets the game award roles with all their checks and sanity, prevents achievements given out for debug games
     */
    // tg: code/modules/mafia/controller.dm:403
    public virtual void award_role(object award = null, datum_mafia_role rewarded = null)
    {
        // rewarded.body?.client?.give_award(award, rewarded.body), and for a PDA player the award to the user of every window open on the
        // PDA (window.user?.client?.give_award(award, window.user.client.mob)): the achievements (not-translated section 164)
        Dm.Deferred("code/modules/mafia/controller.dm:404", "award_role: client.give_award (the achievements, M10)");
    }

    /**
     * The end of the game is in two procs, because we want a bit of time for players to see eachothers roles.
     */
    // tg: code/modules/mafia/controller.dm:421
    public virtual async DmTask start_the_end(object message = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_MAFIA_GAME_END]))
            _SendSignal(COMSIG_MAFIA_GAME_END, Dm.list(this));
        foreach (datum_mafia_role roles in Dm.each_cast<datum_mafia_role>(all_roles))
        {
            if (Dm.truthy(message))
                roles.mafia_alert.update_text(Dm.text("[]", message));
            await roles.reveal_role(this);
        }
        phase = MAFIA_PHASE_VICTORY_LAP;
        // QDEL_IN_STOPPABLE(src, VICTORY_LAP_PERIOD_LENGTH) (__DEFINES/qdel.dm:61), expanded inline (rules 11.1 (f))
        next_phase_timer = _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), VICTORY_LAP_PERIOD_LENGTH > GC_FILTER_QUEUE ? WEAKREF(this) : this), VICTORY_LAP_PERIOD_LENGTH, TIMER_STOPPABLE, file: "code/modules/mafia/controller.dm", line: 429);
    }

    /**
     * Cleans up the game, resetting variables back to the beginning and removing the map with the generator.
     */
    // tg: code/modules/mafia/controller.dm:435
    public virtual void end_game()
    {
        QDEL_LIST(all_roles);
        living_roles.Cut();
        QDEL_LIST(landmarks);
        QDEL_NULL(ref town_center_landmark);
        map_deleter.generate(); //remove the map, it will be loaded at the start of the next one
    }

    /**
     * After the voting and judgement phases, the game goes to night shutting the windows and beginning night with a proc.
     */
    // tg: code/modules/mafia/controller.dm:445
    public virtual void lockdown()
    {
        toggle_night_curtains(close: TRUE);
        start_night();
    }

    /**
     * Shuts poddoors attached to mafia.
     */
    // tg: code/modules/mafia/controller.dm:454
    public virtual void toggle_night_curtains(object close = null)
    {
        foreach (obj_machinery_door_poddoor D in Dm.each_cast<obj_machinery_door_poddoor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor))))
        {
            if (!Dm.eq(D.id, "mafia")) //so as to not trigger shutters on station, lol
                continue;
            if (Dm.truthy(close))
                Waiting.Detach(D.close());
            else
                Waiting.Detach(D.open());
        }
    }

    /**
     * The actual start of night for players. Mostly info is given at the start of the night as the end of the night is when votes and actions are submitted and tried.
     */
    // tg: code/modules/mafia/controller.dm:470
    public virtual void start_night()
    {
        phase = MAFIA_PHASE_NIGHT;
        send_message(I18n.T("<b>Night [] started! Lockdown will end in 40 seconds.</b>", turn));
        if (Dm.truthy(_listen_lookup?[COMSIG_MAFIA_SUNDOWN]))
            _SendSignal(COMSIG_MAFIA_SUNDOWN, Dm.list(this));
        next_phase_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(resolve_night)), NIGHT_PERIOD_LENGTH / time_speedup, TIMER_STOPPABLE, file: "code/modules/mafia/controller.dm", line: 474);
    }

    /**
     * The end of the night, and a series of signals for the order of events on a night.
     */
    // tg: code/modules/mafia/controller.dm:487
    public virtual async DmTask resolve_night()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_MAFIA_NIGHT_PRE_ACTION_PHASE]))
            _SendSignal(COMSIG_MAFIA_NIGHT_PRE_ACTION_PHASE, Dm.list(this));
        if (Dm.truthy(_listen_lookup?[COMSIG_MAFIA_NIGHT_ACTION_PHASE]))
            _SendSignal(COMSIG_MAFIA_NIGHT_ACTION_PHASE, Dm.list(this));
        if (Dm.truthy(_listen_lookup?[COMSIG_MAFIA_NIGHT_KILL_PHASE]))
            _SendSignal(COMSIG_MAFIA_NIGHT_KILL_PHASE, Dm.list(this));
        if (Dm.truthy(_listen_lookup?[COMSIG_MAFIA_NIGHT_END]))
            _SendSignal(COMSIG_MAFIA_NIGHT_END, Dm.list(this));
        toggle_night_curtains(close: FALSE);
        await start_day();
    }

    /**
     * Proc that goes off when players vote for something with their mafia panel.
     */
    // tg: code/modules/mafia/controller.dm:505
    public virtual void vote_for(datum_mafia_role voter = null, datum_mafia_role target = null, object vote_type = null, object teams = null)
    {
        if (!Dm.truthy(votes[vote_type]))
            votes[vote_type] = Dm.list();
        DmList type_votes = votes[vote_type] as DmList;
        object old_vote = type_votes[voter];
        if (Dm.truthy(old_vote) && old_vote == target)
            Dm.remove(ref type_votes, voter);
        else
            type_votes[voter] = target;
        if (Dm.truthy(old_vote) && old_vote == target)
        {
            voter.body.maptext_y = Dm.initial(voter.body, (mob_living_carbon_human b) => b.maptext_y);
            voter.body.maptext_x = Dm.initial(voter.body, (mob_living_carbon_human b) => b.maptext_x);
            voter.body.maptext_width = Dm.initial(voter.body, (mob_living_carbon_human b) => b.maptext_width);
            voter.body.maptext = null;
            send_message(span_notice(I18n.T("[] retracts their vote for []!", voter.body.real_name, target.body.real_name)), team: teams);
        }
        else
        {
            voter.body.maptext_y = 12;
            voter.body.maptext_x = -16;
            voter.body.maptext_width = 64;
            voter.body.maptext = Dm.text("<span class='maptext' style='text-align: center; vertical-align: top'>[]</span>", target.body.real_name);
            send_message(span_notice(I18n.T("[] voted for []!", voter.body.real_name, target.body.real_name)), team: teams);
        }
        if (!Dm.truthy(teams))
        {
            target.body.update_appearance(); //Update the vote display if it's a public vote
            datum_mafia_role old = old_vote as datum_mafia_role;
            if (old != null)
                old.body.update_appearance();
        }
    }

    /**
     * Clears out the votes of a certain type (day votes, mafia kill votes) while leaving others untouched
     */
    // tg: code/modules/mafia/controller.dm:534
    public virtual void reset_votes(object vote_type = null)
    {
        DmList bodies_to_update = Dm.list();
        foreach (datum_mafia_role voter in Dm.each_cast<datum_mafia_role>(votes[vote_type]))
        {
            voter.body.maptext_y = Dm.initial(voter.body, (mob_living_carbon_human b) => b.maptext_y);
            voter.body.maptext_x = Dm.initial(voter.body, (mob_living_carbon_human b) => b.maptext_x);
            voter.body.maptext_width = Dm.initial(voter.body, (mob_living_carbon_human b) => b.maptext_width);
            voter.body.maptext = null;
            datum_mafia_role R = (votes[vote_type] as DmList)[voter] as datum_mafia_role;
            Dm.append(ref bodies_to_update, R.body);
        }
        votes[vote_type] = Dm.list();
        foreach (mob M in Dm.each<mob>(bodies_to_update))
            M.update_appearance();
    }

    /**
     * Returns how many people voted for the role, in whatever vote (day vote, night kill vote)
     */
    // tg: code/modules/mafia/controller.dm:553
    public virtual float get_vote_count(object role = null, object vote_type = null)
    {
        float total_votes = 0;
        foreach (datum_mafia_role votee in Dm.each_cast<datum_mafia_role>(votes[vote_type]))
        {
            if ((votes[vote_type] as DmList)[votee] == role)
                total_votes += votee.vote_power;
        }
        return total_votes;
    }

    /**
     * Returns whichever role got the most votes, in whatever vote (day vote, night kill vote)
     * returns null if no votes
     */
    // tg: code/modules/mafia/controller.dm:566
    public virtual object get_vote_winner(object vote_type = null)
    {
        DmList tally = Dm.list();
        foreach (object votee in Dm.each(votes[vote_type]))
        {
            datum_mafia_role vote_role = votee as datum_mafia_role;
            object voted = (votes[vote_type] as DmList)[votee];
            if (!Dm.truthy(tally[voted]))
                tally[voted] = vote_role.vote_power;
            else
                tally[voted] = Dm.add(tally[voted], vote_role.vote_power);
        }
        sortTim(tally, "/proc/" + nameof(cmp_numeric_dsc), associative: TRUE);
        return Dm.truthy(length(tally)) ? tally[1] : null;
    }

    /**
     * Returns a random person who voted for whatever vote (day vote, night kill vote)
     */
    // tg: code/modules/mafia/controller.dm:582
    public virtual object get_random_voter(object vote_type = null)
    {
        if (Dm.truthy(length(votes[vote_type])))
            return pick(votes[vote_type] as DmList);
        return null;
    }

    /**
     * Adds mutable appearances to people who get publicly voted on (so not night votes) showing how many people are picking them
     */
    // tg: code/modules/mafia/controller.dm:592
    [SignalHandler]
    public virtual void display_votes(atom source = null, DmList overlay_list = null)
    {
        if (phase != MAFIA_PHASE_VOTING)
            return;
        float v = get_vote_count(get_role_player(source), "Day");
        mutable_appearance MA = mutable_appearance(Dm.resource("icons/obj/mafia.dmi"), Dm.text("vote_[]", v > 12 ? "over_12" : v)) as mutable_appearance;
        Dm.append(ref overlay_list, MA);
    }

    /**
     * Called when the game is setting up, AFTER map is loaded but BEFORE the phase timers start. Creates and places each role's body and gives the correct player key
     */
    // tg: code/modules/mafia/controller.dm:610
    public virtual async DmTask create_bodies()
    {
        object outfit_to_distribute;
        if (Dm.truthy(current_map.custom_outfit)) //If our map has a cool custom outfit override, we use that.
            outfit_to_distribute = current_map.custom_outfit;
        else
            outfit_to_distribute = player_outfit;

        foreach (datum_mafia_role role in Dm.each_cast<datum_mafia_role>(all_roles))
        {
            mob_living_carbon_human human = Dm.@new<mob_living_carbon_human>(get_turf(role.assigned_landmark));
            human.add_traits(Dm.list(
                TRAIT_NOFIRE,
                TRAIT_NOBREATH,
                TRAIT_CANNOT_CRYSTALIZE,
                TRAIT_PERMANENTLY_MORTAL,
                TRAIT_GODMODE), MAFIA_TRAIT);
            await human.equipOutfit(outfit_to_distribute);
            RegisterSignal(human, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(display_votes));
            obj_item_modular_computer modpc = role.player_pda;
            await role.register_body(human);
            if (modpc != null)
                player_role_lookup[modpc] = role;
            else
                player_role_lookup[role.player_key] = role;
            client player_client = GLOB.directory[role.player_key] as client;
            if (player_client != null)
                await role.put_player_in_body(player_client);
            role.greet();
        }
    }

    ///From a 'user' (Either a mob or ModPC) or TGUI UI, will try to find the Mafia role from 'player_role_lookup'.
    // tg: code/modules/mafia/controller.dm:640
    public virtual object get_role_player(atom user = null, datum_tgui ui = null)
    {
        object modpc = Dm.truthy(ui?.src_object) ? ui.src_object : user;
        if (modpc is obj_item_modular_computer)
            return player_role_lookup[modpc];
        if (user is not mob mob_user)
            throw CRASH(Dm.text("[] is not a modPC nor a mob, but we are trying to find their role.", user), "code/modules/mafia/controller.dm", 646);
        return player_role_lookup[mob_user.ckey];
    }

    /**
     * Signs the player up for Mafia, or removes them from the list if they are already
     * signed up.
     */
    // tg: code/modules/mafia/controller.dm:656
    public virtual async DmTask<object> signup_mafia(mob user = null, client ghost_client = null, obj_item_modular_computer modpc = null)
    {
        if (!Dm.truthy(SSticker.HasRoundStarted()))
        {
            to_chat(user, span_warning(I18n.T("Wait for the round to start.")));
            return FALSE;
        }
        if (modpc == null)
        {
            if (Dm.truthy(GLOB.mafia_signup[ghost_client.ckey]))
            {
                Dm.remove(ref GLOB.mafia_signup, ghost_client.ckey);
                Dm.remove(ref GLOB.mafia_early_votes, ghost_client.ckey); //Remove their early start vote as well
                to_chat(user, span_notice(I18n.T("You unregister from Mafia.")));
            }
            else
            {
                GLOB.mafia_signup[ghost_client.ckey] = TRUE;
                to_chat(user, span_notice(I18n.T("You sign up for Mafia.")));
            }
        }
        else
        {
            if (Dm.truthy(GLOB.pda_mafia_signup[modpc]))
            {
                Dm.remove(ref GLOB.pda_mafia_signup, modpc);
                Dm.remove(ref GLOB.mafia_early_votes, modpc); //Remove their early start vote as well
                to_chat(user, span_notice(I18n.T("You unregister from Mafia.")));
                return TRUE;
            }
            else
            {
                GLOB.pda_mafia_signup[modpc] = TRUE;
                to_chat(user, span_notice(I18n.T("You sign up for Mafia.")));
            }
        }
        if (phase == MAFIA_PHASE_SETUP)
        {
            check_signups();
            await try_autostart();
        }
        return TRUE;
    }

    /**
     * Returns a standard setup, with certain important/unique roles guaranteed.
     */
    // tg: code/modules/mafia/controller.dm:689
    public virtual DmList generate_standard_setup(object req_players = null)
    {
        DmList selected_roles = Dm.list();
        DmList unique_roles_added = Dm.list();
        for (int i = 1; i <= Dm.num(req_players); i++)
            add_setup_role(selected_roles, unique_roles_added, pick(default_role_list[i] as DmList));
        return selected_roles;
    }

    /**
     * Helper proc that adds a random role of a type to a setup. if it doesn't exist in the setup, it adds the path to the list and otherwise bumps the path in the list up one. unique roles can only get added once.
     */
    // tg: code/modules/mafia/controller.dm:699
    public virtual void add_setup_role(DmList setup_list = null, DmList banned_roles = null, object wanted_role_type = null)
    {
        DmList role_type_paths = Dm.list();
        foreach (Type instance in Dm.each_cast<Type>(GLOB.mafia_role_by_alignment[wanted_role_type]))
        {
            if (Dm.@in(instance, banned_roles))
                continue;
            Dm.append(ref role_type_paths, instance);
        }

        Type mafia_path = pick(role_type_paths) as Type;
        if (Dm.truthy(setup_list[mafia_path]))
            setup_list[mafia_path] = Dm.add(setup_list[mafia_path], 1);
        else
            setup_list[mafia_path] = 1;

        if (Dm.truthy(Dm.bitand(Dm.initial(mafia_path, (datum_mafia_role r) => r.role_flags), ROLE_UNIQUE))) //check to see if we should no longer consider this okay to add to the game
            Dm.append(ref banned_roles, mafia_path);
    }

    /**
     * Called when enough players have signed up to fill a setup. DOESN'T NECESSARILY MEAN THE GAME WILL START.
     */
    // tg: code/modules/mafia/controller.dm:722
    public virtual async DmTask basic_setup()
    {
        object req_players = MAFIA_MAX_PLAYER_COUNT;
        DmList setup = custom_setup;
        if (Dm.truthy(length(setup)))
            req_players = counterlist_sum(setup);

        DmList filtered_keys_and_pdas = filter_players(req_players);

        //don't actually have one yet, so generate a max player random setup.
        //it's good to do this here instead of above so it doesn't generate one every time a game could possibly start.
        if (!Dm.truthy(length(setup)))
            setup = generate_standard_setup(length(filtered_keys_and_pdas));
        await prepare_game(setup, filtered_keys_and_pdas);
        await start_game();
    }

    /**
     * Generates a forced role list and runs the game with the current number of signed-up players.
     */
    // tg: code/modules/mafia/controller.dm:743
    public virtual async DmTask forced_setup()
    {
        check_signups(); //Refresh the signup list, so our numbers are accurate and we only take active players into consideration.
        float players_needed = length(GLOB.mafia_signup) + length(GLOB.pda_mafia_signup);
        DmList filtered_keys_and_pdas = filter_players(players_needed);
        if (!Dm.truthy(length(filtered_keys_and_pdas))) //If we have nobody signed up, we give up on starting
        {
            log_admin("Attempted to force a mafia game to start with nobody signed up!");
            return;
        }

        DmList setup = generate_standard_setup(length(filtered_keys_and_pdas));

        await prepare_game(setup, filtered_keys_and_pdas);
        early_start = TRUE;
        await start_game();
    }

    /**
     * Checks if we have enough early start votes to begin the game early.
     */
    // tg: code/modules/mafia/controller.dm:764
    public virtual object check_start_votes()
    {
        check_signups(); //Same as before. What a useful proc.

        if (length(Dm.add(GLOB.mafia_signup, GLOB.pda_mafia_signup)) < MAFIA_MIN_PLAYER_COUNT)
            return FALSE; //Make sure we have the minimum playercount to host a game first.

        if (length(GLOB.mafia_early_votes) < round(length(Dm.add(GLOB.mafia_signup, GLOB.pda_mafia_signup)) / 2f))
            return FALSE;

        return TRUE;
    }

    /**
     * Handles the filtering of disconected signups when picking who gets to be in the round.
     */
    // tg: code/modules/mafia/controller.dm:786
    public virtual DmList filter_players(object max_players = null)
    {
        //final list for all the players who will be in this game
        DmList filtered_keys_and_pdas = Dm.list();
        //cuts invalid players from signups (disconnected/not a ghost)
        DmList possible_players = Dm.list();
        foreach (object key in Dm.each(GLOB.mafia_signup))
        {
            if (Dm.truthy(GLOB.directory[key]))
            {
                client C = GLOB.directory[key] as client;
                if (isobserver(C.mob))
                {
                    Dm.append(ref possible_players, key);
                    continue;
                }
            }
            Dm.remove(ref GLOB.mafia_signup, key); //not valid to play when we checked so remove them from signups
        }

        foreach (obj_item_modular_computer_pda pdas in Dm.each_cast<obj_item_modular_computer_pda>(GLOB.pda_mafia_signup))
            Dm.append(ref possible_players, pdas);

        //if there were too many players, still start but only make filtered keys as big as it needs to be (cut excess)
        //also removes people who do get into final player list from the signup so they have to sign up again when game ends
        for (int i = 1; i <= Dm.num(max_players); i++)
        {
            if (!Dm.truthy(length(possible_players)))
                break;
            object chosen_key = pick_n_take(possible_players);
            Dm.append(ref filtered_keys_and_pdas, chosen_key);
            if (Dm.@in(chosen_key, GLOB.pda_mafia_signup))
                Dm.remove(ref GLOB.pda_mafia_signup, chosen_key);
            else if (Dm.@in(chosen_key, GLOB.mafia_signup))
                Dm.remove(ref GLOB.mafia_signup, chosen_key);
        }
        //small message about not getting into this game for clarity on why they didn't get in
        foreach (object unpicked in Dm.each(possible_players))
        {
            if (!Dm.@in(unpicked, GLOB.mafia_signup))
                continue;
            client unpicked_client = GLOB.directory[unpicked] as client;
            to_chat(unpicked_client, span_danger(I18n.T("Sorry, the starting mafia game has too many players and you were not picked.")));
            to_chat(unpicked_client, span_warning(I18n.T("You're still signed up, getting messages from the current round, and have another chance to join when the one starting now finishes.")));
        }

        return filtered_keys_and_pdas;
    }

    /**
     * Called when someone signs up, and sees if there are enough people in the signup list to begin.
     */
    // tg: code/modules/mafia/controller.dm:828
    public virtual async DmTask try_autostart()
    {
        if (phase != MAFIA_PHASE_SETUP || !Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_MINIGAME)))
            return;
        if ((length(GLOB.mafia_signup) + length(GLOB.pda_mafia_signup)) >= MAFIA_MAX_PLAYER_COUNT || Dm.truthy(custom_setup)) //enough people to try and make something (or debug mode)
            await basic_setup();
    }

    /**
     * Filters inactive player into a different list until they reconnect, and removes players who are no longer ghosts.
     */
    // tg: code/modules/mafia/controller.dm:839  (the #ifndef UNIT_TESTS body: the port is not built with UNIT_TESTS)
    public virtual void check_signups()
    {
        foreach (object bad_key in Dm.each(GLOB.mafia_bad_signup))
        {
            if (Dm.truthy(GLOB.directory[bad_key])) //they have reconnected if we can search their key and get a client
            {
                Dm.remove(ref GLOB.mafia_bad_signup, bad_key);
                GLOB.mafia_signup[bad_key] = TRUE;
            }
        }
        foreach (object key in Dm.each(GLOB.mafia_signup))
        {
            client C = GLOB.directory[key] as client;
            if (C == null) //vice versa but in a variable we use later
            {
                Dm.remove(ref GLOB.mafia_signup, key);
                GLOB.mafia_bad_signup[key] = TRUE;
                continue;
            }
            if (!isobserver(C.mob))
            {
                //they are back to playing the game, remove them from the signups
                Dm.remove(ref GLOB.mafia_signup, key);
            }
        }
    }
}

// tg: code/modules/mafia/controller.dm:856
public partial class datum_action_innate_mafia_panel : datum_action_innate
{
    // tg: code/modules/mafia/controller.dm:864
    [DmParams("Target", "controller")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        controller_panel = Dm.arg(__extra, 1) as datum_mafia_controller;
    }

    // tg: code/modules/mafia/controller.dm:868
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        controller_panel = null;
        return dot;
    }

    // tg: code/modules/mafia/controller.dm:872
    public override async DmTask<object> Activate()
    {
        await controller_panel.ui_interact(owner);
        return null;
    }
}

/**
 * The popup used for sending important messages to players.
 */
// tg: code/modules/mafia/controller.dm:878
public partial class atom_movable_screen_mafia_popup : atom_movable_screen
{
    // tg: code/modules/mafia/controller.dm:890
    [DmParams("mapload", "mafia")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        owner = Dm.arg(__extra, 0) as datum_mafia_role;
        return dot;
    }

    // tg: code/modules/mafia/controller.dm:894
    public override object Destroy(float? force = null)
    {
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mafia/controller.dm:898
    public virtual void update_text(object text = null)
    {
        Dm.append(ref owner.role_messages, text);
        if (owner.body.client == null)
            return;
        maptext = MAPTEXT(Dm.text("<span style='color: []; text-align: center; font-size: 24pt'> []</span>", COLOR_RED, text));
        // maptext_width = view_to_pixels(owner.body.client?.view_size.getView())[1]: the client's /datum/view_data is not declared; the
        // port's HUD lays the screen on tg's 15 x 15 view (D107), the world's view, which getView gives a client without a widescreen view
        maptext_width = Dm.num(view_to_pixels(world.view)[1]);
        Dm.append(owner.body.client.screen, this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(null_text), owner.body.client), 10 * SECONDS, TIMER_UNIQUE | TIMER_OVERRIDE | TIMER_STOPPABLE, file: "code/modules/mafia/controller.dm", line: 905);
    }

    ///Clears all text to re-use in the future. We use to_clear here in case someone takes over their old body.
    // tg: code/modules/mafia/controller.dm:908
    public virtual void null_text(client to_clear = null)
    {
        maptext = null;
        if (to_clear != null)
            Dm.remove(to_clear.screen, this);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mafia/controller.dm:64, :130  the controller's static lists (rules 10.6)
    internal DmList datum_mafia_controller__default_role_list;
    internal DmList datum_mafia_controller__possible_maps;
}
