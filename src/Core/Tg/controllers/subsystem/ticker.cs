// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/ticker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F): the round's states up to PLAYING: Initialize (the lobby music), fire (the pregame countdown, the setup and
// the playing state's checks), setup and PostSetup, create_characters, collect_minds, equip_characters, transfer_characters and
// release_characters, the join queue, the round-start and round-end callbacks, the state queries, the time left and the lobby music.
// M10 slice H: the round's end (declare_completion, __HELPERS/roundend.cs) and the reboot with its timer; the round-end sound, the news
// report and Shutdown wait.
// Syndicate A3 (D316): the antagonists' selection (SSdynamic's roundstart rulesets in setup, run in PostSetup) and the round-start
// command report's queue. Not translated: the security
// officers' departments (no security job is joinable in M10), the quirks,
// the TGS chat relays (send2chat, send2adminchat), and the roundstart logout report and
// the suicide roles' reopening (admin tooling).

// tg: code/controllers/subsystem/ticker.dm:4 SUBSYSTEM_DEF(ticker)
// The [DmPath] of /datum/controller/subsystem/ticker is on its part in __HELPERS/hearted.cs, the earliest .dme file that declares it (rules 2.3).
public partial class datum_controller_subsystem_ticker : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/ticker.dm:1  #define ROUND_START_MUSIC_LIST "strings/round_start_sounds.txt" (file-local, #undef at :881)
    private const string ROUND_START_MUSIC_LIST = "strings/round_start_sounds.txt";
    // tg: code/controllers/subsystem/ticker.dm:2  #define SS_TICKER_TRAIT "SS_Ticker" (file-local, #undef at :882)
    private const string SS_TICKER_TRAIT = "SS_Ticker";

    // tg: code/controllers/subsystem/ticker.dm:5
    public datum_controller_subsystem_ticker()
    {
        name = "Ticker";
        priority = FIRE_PRIORITY_TICKER;
        ss_flags = SS_KEEP_TIMING;
        runlevels = Dm.bitor(Dm.bitor(RUNLEVEL_LOBBY, RUNLEVEL_SETUP), Dm.bitor(RUNLEVEL_GAME, RUNLEVEL_POSTGAME));
    }

    // The New() that SUBSYSTEM_DEF(ticker) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSticker) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/ticker.dm:4
    public override async DmTask New(params object[] __extra)
    {
        if (SSticker != this)
        {
            if (SSticker is datum_controller_subsystem_ticker)
            {
                Recover();
                qdel(SSticker);
            }
            SSticker = this;
        }
        PreInit();
    }

    /// Time left until the round starts after all subsystems initialize
    // tg: code/controllers/subsystem/ticker.dm:39
    public float? timeLeft = 120 * SECONDS;
    /// value used to initialize `timeLeft` when the master subsystem finishes initializing.
    // tg: code/controllers/subsystem/ticker.dm:44
    public float start_at = 120 * SECONDS;
    // tg: code/controllers/subsystem/ticker.dm:62
    public float round_start_time = 0;
    /// DV244: the title tracks the lobby plays one after another (login_music is the first).
    public DmList lobby_playlist;

    // tg: code/controllers/subsystem/ticker.dm:79
    [DmParams()]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList provisional_title_music = flist(Dm.text("[]/title_music/sounds/", config.directory));
        DmList music = Dm.list();
        bool use_rare_music = prob(1);

        foreach (object S in Dm.each(provisional_title_music))
        {
            object lower = LOWER_TEXT(S);
            DmList L = splittext(lower, "+");
            if (L.len == 3) //rare+MAP+sound.ogg or MAP+rare.sound.ogg -- Rare Map-specific sounds
            {
                if (use_rare_music)
                {
                    if (Dm.eq(L[1], "rare") && Dm.eq(L[2], SSmapping.current_map.map_name))
                        Dm.append(ref music, S);
                    else if (Dm.eq(L[2], "rare") && Dm.eq(L[1], SSmapping.current_map.map_name))
                        Dm.append(ref music, S);
                }
            }
            else if (L.len == 2) //rare+sound.ogg or MAP+sound.ogg -- Rare sounds or Map-specific sounds
            {
                if ((use_rare_music && Dm.eq(L[1], "rare")) || Dm.eq(L[1], SSmapping.current_map.map_name))
                    Dm.append(ref music, S);
            }
            else if (L.len == 1) //sound.ogg -- common sound
            {
                if (Dm.eq(L[1], "exclude"))
                    continue;
                Dm.append(ref music, S);
            }
        }

        object old_login_music = trim(file2text("data/last_round_lobby_music.txt"));
        if (length(music) > 1)
            Dm.remove(ref music, old_login_music);

        foreach (object S in Dm.each(music.Copy()))
        {
            if (Dm.truthy(is_file_type_in_list(S, datum_controller_subsystem_sounds.byond_sound_formats))) // IS_SOUND_FILE (__DEFINES/files.dm:7)
                continue;
            Dm.remove(ref music, S);
        }

        if (!Dm.truthy(length(music)))
        {
            music = world.file2list(ROUND_START_MUSIC_LIST, "\n") as DmList;
            foreach (string removed in SoundLicences.Unlicensed.Keys) // DV574: the tracks with no licence are not shipped (D344)
                Dm.remove(ref music, removed);
            lobby_playlist = music.Copy(); // DV244
            if (length(music) > 1)
                Dm.remove(ref music, old_login_music);
            set_lobby_music(pick(music));
        }
        else
        {
            lobby_playlist = Dm.list(); // DV244: every title track, as login_music names it
            foreach (object S in Dm.each(music))
                Dm.append(ref lobby_playlist, Dm.text("[]/title_music/sounds/[]", config.directory, S));
            set_lobby_music(Dm.text("[]/title_music/sounds/[]", config.directory, pick(music)));
        }

        start_at = Dm.num(config.Get(typeof(datum_config_entry_number_lobby_countdown))) * (1 * SECONDS);
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/ticker.dm:121
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (Dm.eq(current_state, GAME_STATE_STARTUP))
        {
            // for(var/client/C in GLOB.clients) window_flash(C, ignorepref = TRUE): the BYOND window's taskbar flash (the port's client
            // has none) //let them know lobby has opened up.
            to_chat(world, span_notice(Dm.text("<b>Welcome to []!</b>", station_name())));
            // for(var/channel_tag in CONFIG_GET(str_list/channel_announce_new_game)) send2chat(...): the TGS chat relay

            current_state = GAME_STATE_PREGAME;
            // (!Dm.truthy(src._listen_lookup?[COMSIG_TICKER_ENTER_PREGAME]) ? (object)NONE : src._SendSignal(COMSIG_TICKER_ENTER_PREGAME, Dm.list(src))) (rules 11.1 (e), a statement)
            if (Dm.truthy(this._listen_lookup?[COMSIG_TICKER_ENTER_PREGAME]))
                this._SendSignal(COMSIG_TICKER_ENTER_PREGAME, Dm.list(this));
            await fire();
        }
        else if (Dm.eq(current_state, GAME_STATE_PREGAME))
        {
            //lobby stats for statpanels
            totalPlayers = LAZYLEN(GLOB.new_player_list);
            totalPlayersReady = 0;
            total_admins_ready = 0;
            foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(GLOB.new_player_list))
            {
                if (Dm.eq(player.ready, PLAYER_READY_TO_PLAY))
                {
                    ++totalPlayersReady;
                    if (player.client?.holder != null)
                        ++total_admins_ready;
                }
            }

            if (Dm.truthy(start_immediately))
                timeLeft = 0;

            //countdown
            if (Dm.lt(timeLeft, 0))
                return;
            timeLeft -= wait;

            if (Dm.le(timeLeft, 300) && !Dm.truthy(tipped))
            {
                send_tip_of_the_round(world, selected_tip);
                tipped = TRUE;
            }

            if (Dm.le(timeLeft, 0))
            {
                // (!Dm.truthy(src._listen_lookup?[COMSIG_TICKER_ENTER_SETTING_UP]) ? (object)NONE : src._SendSignal(COMSIG_TICKER_ENTER_SETTING_UP, Dm.list(src))) (rules 11.1 (e), a statement)
                if (Dm.truthy(this._listen_lookup?[COMSIG_TICKER_ENTER_SETTING_UP]))
                    this._SendSignal(COMSIG_TICKER_ENTER_SETTING_UP, Dm.list(this));
                current_state = GAME_STATE_SETTING_UP;
                Master.SetRunLevel(RUNLEVEL_SETUP);
                if (Dm.truthy(start_immediately))
                    await fire();
            }
        }
        else if (Dm.eq(current_state, GAME_STATE_SETTING_UP))
        {
            if (!Dm.truthy(await setup()))
            {
                //setup failed
                current_state = GAME_STATE_STARTUP;
                timeLeft = null;
                Master.SetRunLevel(RUNLEVEL_LOBBY);
                // (!Dm.truthy(src._listen_lookup?[COMSIG_TICKER_ERROR_SETTING_UP]) ? (object)NONE : src._SendSignal(COMSIG_TICKER_ERROR_SETTING_UP, Dm.list(src))) (rules 11.1 (e), a statement)
                if (Dm.truthy(this._listen_lookup?[COMSIG_TICKER_ERROR_SETTING_UP]))
                    this._SendSignal(COMSIG_TICKER_ERROR_SETTING_UP, Dm.list(this));
            }
        }
        else if (Dm.eq(current_state, GAME_STATE_PLAYING))
        {
            check_queue();

            if (!Dm.truthy(roundend_check_paused) && (Dm.truthy(check_finished()) || Dm.truthy(force_ending)))
            {
                current_state = GAME_STATE_FINISHED;
                toggle_ooc(TRUE); // Turn it on
                toggle_dooc(TRUE);
                Waiting.Detach(declare_completion(force_ending)); // set waitfor = FALSE

                Master.SetRunLevel(RUNLEVEL_POSTGAME);
            }
        }
        else if (Dm.eq(current_state, GAME_STATE_FINISHED))
        {
            if (Dm.truthy(ready_for_reboot))
            {
                if (reboot_timer == null)
                    reboot_hud.maptext = MAPTEXT_PIXELLARI("<center>Server reboot \n DELAYED</center>");
                else
                    reboot_hud.maptext = MAPTEXT_PIXELLARI(Dm.text("<center>Server rebooting in:\n []</center>", DisplayTimeText(Dm.num(timeleft(SSticker.reboot_timer)), 1)));
            }
        }
    }

    /// Checks if the round should be ending, called every ticker tick
    // tg: code/controllers/subsystem/ticker.dm:198
    public virtual object check_finished()
    {
        if (!Dm.truthy(setup_done))
            return FALSE;
        if (SSshuttle.emergency != null && Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME))
            return TRUE;
        if (Dm.truthy(GLOB.station_was_nuked))
            return TRUE;
        if (Dm.eq(GLOB.revolution_handler?.result, REVOLUTION_VICTORY)) // gaps V1
            return TRUE;
        return FALSE;
    }

    /// Gets a list of players with their readied state so we can post it as a log
    // tg: code/controllers/subsystem/ticker.dm:210
    public virtual DmList get_player_ready_states()
    {
        DmList player_states = Dm.list();
        foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(GLOB.new_player_list))
            player_states[player.ckey] = player.ready;
        return player_states;
    }

    // tg: code/controllers/subsystem/ticker.dm:216
    public virtual async DmTask<object> setup()
    {
        to_chat(world, span_boldannounce("Starting game..."));
        float init_start = world.timeofday;

        DmList players_and_readiness = get_player_ready_states();
        log_game(Dm.text("Players and Readiness: []", Dm.json_encode(players_and_readiness)), players_and_readiness);

        CHECK_TICK();
        //Configure mode and assign player to antagonists
        bool can_continue = Dm.truthy(await SSdynamic.select_roundstart_antagonists()); //Choose antagonists (syndicate package A3)
        CHECK_TICK();
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_PRE_JOBS_ASSIGNED, src) (rules 11.1 (e), a statement)
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_PRE_JOBS_ASSIGNED]))
            SSdcs._SendSignal(COMSIG_GLOB_PRE_JOBS_ASSIGNED, Dm.list(SSdcs, this));
        can_continue = can_continue && Dm.truthy(await SSjob.divide_occupations()); //Distribute jobs
        CHECK_TICK();

        if (!Dm.truthy(GLOB.debugging_enabled))
        {
            if (!can_continue)
            {
                log_game("Game failed pre_setup");
                to_chat(world, "<B>Error setting up game.</B> Reverting to pre-game lobby.");
                SSjob.reset_occupations();
                return FALSE;
            }
        }
        else
            message_admins(span_notice("DEBUG: Bypassing prestart checks..."));

        CHECK_TICK();

        // There may be various config settings that have been set or modified by this point.
        // This is the point of no return before spawning in new players, let's run over the
        // job trim singletons and update them based on any config settings.
        SSid_access.refresh_job_trim_singletons();

        CHECK_TICK();

        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_ooc_during_round))))
            toggle_ooc(FALSE); // Turn it off

        CHECK_TICK();
        GLOB.start_landmarks_list = shuffle(GLOB.start_landmarks_list) as DmList; //Shuffle the order of spawn points so they dont always predictably spawn bottom-up and right-to-left
        await create_characters(); //Create player characters
        await collect_minds();
        await equip_characters();

        GLOB.manifest.build();

        await transfer_characters(); //transfer keys to the new mobs

        foreach (datum_callback cb in Dm.each<datum_callback>(round_start_events))
            cb.InvokeAsync();
        LAZYCLEARLIST(round_start_events);

        round_start_time = world.time; //otherwise round_start_time would be 0 for the signals
        // (!Dm.truthy(src._listen_lookup?[COMSIG_TICKER_ROUND_STARTING]) ? (object)NONE : src._SendSignal(COMSIG_TICKER_ROUND_STARTING, Dm.list(src, world.time))) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_TICKER_ROUND_STARTING]))
            this._SendSignal(COMSIG_TICKER_ROUND_STARTING, Dm.list(this, world.time));

        log_world(Dm.text("Game start took []s", (world.timeofday - init_start) / 10));
        Waiting.Detach(SSdbcore.SetRoundStart()); // INVOKE_ASYNC(SSdbcore, TYPE_PROC_REF(/datum/controller/subsystem/dbcore,SetRoundStart))

        to_chat(world, span_notice(span_bold(Dm.text("Welcome to [], enjoy your stay!", station_name()))));
        Dm.output_to(world, sound(((datum_centcom_announcer)SSstation.announcer).get_rand_welcome_sound())); // SEND_SOUND (DIRECT_OUTPUT, rules 12)

        current_state = GAME_STATE_PLAYING;
        Master.SetRunLevel(RUNLEVEL_GAME);

        if (Dm.length(GLOB.holidays) > 0) // gaps R3
        {
            to_chat(world, span_notice(I18n.T("and...")));
            foreach (object holidayname in Dm.each(GLOB.holidays))
            {
                var holiday = (datum_holiday)GLOB.holidays[holidayname];
                to_chat(world, span_info(Dm.str(holiday.greet())));
            }
        }

        PostSetup();

        return TRUE;
    }

    // tg: code/controllers/subsystem/ticker.dm:290
    public virtual void PostSetup()
    {
        Waiting.Detach(PostSetup__async()); // set waitfor = FALSE
    }

    private async DmTask PostSetup__async()
    {
        // Spawn traitors and stuff
        foreach (datum_dynamic_ruleset_roundstart ruleset in Dm.each<datum_dynamic_ruleset_roundstart>(SSdynamic.queued_rulesets))
        {
            await ruleset.execute();
            SSdynamic.unqueue_ruleset(ruleset);
            Dm.append(ref SSdynamic.executed_rulesets, ruleset);
        }
        // Queue roundstart intercept report
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_intercept_report)))) // CONFIG_GET(flag/no_intercept_report)
            GLOB.communications_controller.queue_roundstart_report();
        // Queue admin logout report, the suicide roles' reopening and send2adminchat: the admin relays (not ported)

        GLOB.start_state = Dm.@new<datum_station_state>();
        GLOB.start_state.count();

        setup_done = TRUE;

        foreach (object i in Dm.each(GLOB.start_landmarks_list))
        {
            obj_effect_landmark_start S = i as obj_effect_landmark_start;
            if (S != null) //we can not runtime here. not in this important of a proc.
                S.after_round_start();
            else
                stack_trace(Dm.text("[] [] found in start landmarks list, which isn't a start landmark!", i, (i as datum)?.type), "code/controllers/subsystem/ticker.dm", 337);
        }

        // handle persistence stuff that requires ckeys, in this case hardcore mode and temporal scarring
        foreach (object i in Dm.each(GLOB.player_list))
        {
            if (!ishuman(i))
                continue;
            mob_living_carbon_human iter_human = i as mob_living_carbon_human;

            iter_human.increment_scar_slot();
            iter_human.load_persistent_scars();

            if (!Dm.truthy(iter_human.hardcore_survival_score))
                continue;
            Dm.NotTranslated("code/controllers/subsystem/ticker.dm:349", "PostSetup: the hardcore random score's notice (the quirks' hardcore mode)");
        }
    }

    //These callbacks will fire after roundstart key transfer
    // tg: code/controllers/subsystem/ticker.dm:431
    public virtual void OnRoundstart(datum_callback cb = null)
    {
        if (!Dm.truthy(HasRoundStarted()))
            LAZYADD(ref round_start_events, cb);
        else
            cb.InvokeAsync();
    }

    //These callbacks will fire before roundend report
    // tg: code/controllers/subsystem/ticker.dm:438
    public virtual void OnRoundend(datum_callback cb = null)
    {
        if (Dm.ge(current_state, GAME_STATE_FINISHED))
            cb.InvokeAsync();
        else
            LAZYADD(ref round_end_events, cb);
    }

    // tg: code/controllers/subsystem/ticker.dm:444
    public virtual async DmTask create_characters()
    {
        foreach (object i in Dm.each(GLOB.new_player_list))
        {
            mob_dead_new_player player = i as mob_dead_new_player;
            if (Dm.eq(player.ready, PLAYER_READY_TO_PLAY) && player.mind != null)
            {
                Dm.append(ref GLOB.joined_player_list, player.ckey);
                atom destination = await player.mind.assigned_role.get_roundstart_spawn_point() as atom;
                if (destination == null) // Failed to fetch a proper roundstart location, won't be going anywhere.
                    continue;
                await player.create_character(destination);
            }
            CHECK_TICK();
        }
    }

    // tg: code/controllers/subsystem/ticker.dm:455
    public virtual async DmTask collect_minds()
    {
        foreach (object i in Dm.each(GLOB.new_player_list))
        {
            mob_dead_new_player P = i as mob_dead_new_player;
            if (P.new_character != null && P.new_character.mind != null)
                Dm.append(ref SSticker.minds, P.new_character.mind);
            CHECK_TICK();
        }
    }

    // tg: code/controllers/subsystem/ticker.dm:553
    public virtual DmList decide_security_officer_departments(DmList new_players = null, DmList departments = null)
    {
        DmList officer_mobs = Dm.list();
        DmList officer_preferences = Dm.list();

        foreach (mob_dead_new_player new_player_mob in Dm.each<mob_dead_new_player>(new_players))
        {
            mob_living_carbon_human character = new_player_mob.new_character as mob_living_carbon_human;
            if (character != null && character.mind?.assigned_role is datum_job_security_officer)
            {
                Dm.append(ref officer_mobs, character);

                client client = GET_CLIENT(new_player_mob) as client;
                object preference = client?.prefs?.read_preference(typeof(datum_preference_choiced_security_department));
                Dm.append(ref officer_preferences, preference);
            }
        }

        DmList distribution = get_officer_departments(officer_preferences, departments);

        DmList output = Dm.list();

        for (float index = 1; index <= officer_mobs.len; index++)
            output[REF(officer_mobs[index])] = distribution[index];

        return output;
    }

    // tg: code/controllers/subsystem/ticker.dm:463
    public virtual async DmTask equip_characters()
    {
        GLOB.security_officer_distribution = decide_security_officer_departments(
            shuffle(GLOB.new_player_list) as DmList,
            shuffle(GLOB.available_depts) as DmList);

        bool captainless = true;

        float highest_rank = Dm.num(length(SSjob.chain_of_command)) + 1;
        DmList spare_id_candidates = Dm.list();
        mob_dead_new_player picked_spare_id_candidate = null;

        // Find a suitable player to hold captaincy.
        foreach (mob_dead_new_player new_player_mob in Dm.each<mob_dead_new_player>(GLOB.new_player_list))
        {
            if (Dm.truthy(is_banned_from(new_player_mob.ckey, Dm.list(JOB_CAPTAIN))))
            {
                CHECK_TICK();
                continue;
            }
            if (!ishuman(new_player_mob.new_character))
                continue;
            var new_player_human = (mob_living_carbon_human)new_player_mob.new_character;
            if (new_player_human.mind == null || is_unassigned_job(new_player_human.mind.assigned_role))
                continue;
            // Keep a rolling tally of who'll get the cap's spare ID vault code.
            // Check assigned_role's priority and curate the candidate list appropriately.
            object player_assigned_role = new_player_human.mind.assigned_role.title;
            object spare_id_priority = SSjob.chain_of_command[player_assigned_role];
            if (Dm.truthy(spare_id_priority))
            {
                if (Dm.num(spare_id_priority) < highest_rank)
                {
                    spare_id_candidates.Cut();
                    Dm.append(ref spare_id_candidates, new_player_mob);
                    highest_rank = Dm.num(spare_id_priority);
                }
                else if (Dm.num(spare_id_priority) == highest_rank)
                {
                    Dm.append(ref spare_id_candidates, new_player_mob);
                }
            }
            CHECK_TICK();
        }

        if (Dm.truthy(length(spare_id_candidates)))
            picked_spare_id_candidate = pick(spare_id_candidates) as mob_dead_new_player;

        foreach (mob_dead_new_player new_player_mob in Dm.each<mob_dead_new_player>(GLOB.new_player_list))
        {
            if (QDELETED(new_player_mob) || !isliving(new_player_mob.new_character))
            {
                CHECK_TICK();
                continue;
            }
            mob_living new_player_living = new_player_mob.new_character;
            if (new_player_living.mind == null)
            {
                CHECK_TICK();
                continue;
            }
            datum_job player_assigned_role = new_player_living.mind.assigned_role;
            if (Dm.truthy(Dm.bitand(player_assigned_role.job_flags, JOB_EQUIP_RANK)))
                await SSjob.equip_rank(new_player_living, player_assigned_role, new_player_mob.client);
            await player_assigned_role.after_roundstart_spawn(new_player_living, new_player_mob.client);
            if (picked_spare_id_candidate == new_player_mob)
            {
                captainless = false;
                bool acting_captain = !is_captain_job(player_assigned_role);
                await SSjob.promote_to_captain(new_player_living as mob_living_carbon_human, acting_captain ? TRUE : FALSE);
                OnRoundstart(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.minor_announce), player_assigned_role.get_captaincy_announcement(new_player_living))); // Dm.@new<datum_callback>(GLOBAL_PROC, GLOBAL_PROC_REF(minor_announce), ...)
            }
            if (ishuman(new_player_living))
            {
                if (Dm.truthy(Dm.bitand(player_assigned_role.job_flags, JOB_ASSIGN_QUIRKS)))
                {
                    if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_roundstart_traits))))
                        Dm.Deferred("code/controllers/subsystem/ticker.dm:516", "equip_characters: the hardcore random setup and SSquirks.AssignQuirks (the quirks)");
                }
                else // clear any personalities the prefs added since our job clearly does not want them
                    ((mob_living_carbon_human)new_player_living).clear_personalities();
            }

            if (ishuman(new_player_living))
            {
                // (!Dm.truthy(new_player_living._listen_lookup?[COMSIG_HUMAN_CHARACTER_SETUP_FINISHED]) ? (object)NONE : new_player_living._SendSignal(COMSIG_HUMAN_CHARACTER_SETUP_FINISHED, Dm.list(new_player_living))) (rules 11.1 (e), a statement)
                if (Dm.truthy(new_player_living._listen_lookup?[COMSIG_HUMAN_CHARACTER_SETUP_FINISHED]))
                    new_player_living._SendSignal(COMSIG_HUMAN_CHARACTER_SETUP_FINISHED, Dm.list(new_player_living));
            }
            CHECK_TICK();
        }

        if (captainless)
        {
            foreach (mob_dead_new_player new_player_mob in Dm.each<mob_dead_new_player>(GLOB.new_player_list))
            {
                mob_living_carbon_human new_player_human = new_player_mob.new_character as mob_living_carbon_human;
                if (new_player_human != null)
                    to_chat(new_player_mob, span_notice("Captainship not forced on anyone."));
                CHECK_TICK();
            }
        }
    }

    // tg: code/controllers/subsystem/ticker.dm:579
    public virtual async DmTask transfer_characters()
    {
        DmList livings = Dm.list();
        foreach (mob_dead_new_player player in Dm.each<mob_dead_new_player>(GLOB.new_player_list.Copy()))
        {
            mob living = await player.transfer_character() as mob;
            if (living != null)
            {
                qdel(player);
                ADD_TRAIT(living, TRAIT_NO_TRANSFORM, SS_TICKER_TRAIT);
                if (living.client != null)
                {
                    atom_movable_screen_splash fade_out = Dm.@new<atom_movable_screen_splash>(null, null, living.client, TRUE);
                    fade_out.fade(TRUE);
                    living.client.init_verbs();
                    living.client.show_spawn_text_overlay();
                }

                Dm.append(ref livings, living);
            }
        }
        if (Dm.truthy(livings.len))
            _addtimer(Dm.@new<datum_callback>(this, nameof(release_characters), livings), 3 * SECONDS, TIMER_CLIENT_TIME, file: "code/controllers/subsystem/ticker.dm", line: 596);
    }

    // tg: code/controllers/subsystem/ticker.dm:598
    public virtual void release_characters(DmList livings = null)
    {
        foreach (mob_living living_mob in Dm.each<mob_living>(livings))
            REMOVE_TRAIT(living_mob, TRAIT_NO_TRANSFORM, SS_TICKER_TRAIT);
    }

    // tg: code/controllers/subsystem/ticker.dm:602
    public virtual void check_queue()
    {
        if (!Dm.truthy(queued_players.len))
            return;
        object hard_popcap = config.Get(typeof(datum_config_entry_number_hard_popcap));
        if (!Dm.truthy(hard_popcap))
        {
            list_clear_nulls(queued_players);
            foreach (mob_dead_new_player new_player in Dm.each<mob_dead_new_player>(queued_players))
            {
                to_chat(new_player, span_userdanger(Dm.text("The alive players limit has been released!<br><a href='byond://?src=[];late_join=override'>[]</a>", REF(new_player), html_encode(">>Join Game<<"))));
                Dm.output_to(new_player, sound("sound/announcer/notice/notice1.ogg")); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
                Waiting.Detach(GLOB.latejoin_menu.ui_interact(new_player));
            }
            queued_players.len = 0;
            queue_delay = 0;
            return;
        }

        queue_delay++;
        mob_dead_new_player next_in_line = queued_players[1] as mob_dead_new_player;

        if (queue_delay == 5) //every 5 ticks check if there is a slot available
        {
            list_clear_nulls(queued_players);
            if (Dm.lt(living_player_count(), hard_popcap))
            {
                if (next_in_line?.client != null)
                {
                    to_chat(next_in_line, span_userdanger(Dm.text("A slot has opened! You have approximately 20 seconds to join. <a href='byond://?src=[];late_join=override'>\\>\\>Join Game\\<\\<</a>", REF(next_in_line))));
                    Dm.output_to(next_in_line, sound("sound/announcer/notice/notice1.ogg")); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
                    Waiting.Detach(next_in_line.ui_interact(next_in_line));
                    return;
                }
                Dm.remove(ref queued_players, next_in_line); //Client disconnected, remove he
            }
            queue_delay = 0; //No vacancy: restart timer
        }
        else if (queue_delay >= 25) //No response from the next in line when a vacancy exists, remove he
        {
            to_chat(next_in_line, span_danger("No response received. You have been removed from the line."));
            Dm.remove(ref queued_players, next_in_line);
            queue_delay = 0;
        }
    }

    ///Whether the game has started, including roundend.
    // tg: code/controllers/subsystem/ticker.dm:633  (M9 slice A: the OOC verb)
    public virtual object HasRoundStarted()
    {
        return current_state >= GAME_STATE_PLAYING ? TRUE : FALSE;
    }

    ///Whether the game is currently in progress, excluding roundend
    // tg: code/controllers/subsystem/ticker.dm:637
    public virtual object IsRoundInProgress()
    {
        return Dm.eq(current_state, GAME_STATE_PLAYING) ? TRUE : FALSE;
    }

    ///Whether the game is currently in progress, excluding roundend
    // tg: code/controllers/subsystem/ticker.dm:641
    public virtual object IsPostgame()
    {
        return Dm.eq(current_state, GAME_STATE_FINISHED) ? TRUE : FALSE;
    }

    // tg: code/controllers/subsystem/ticker.dm:644
    public override void Recover()
    {
        current_state = SSticker.current_state;
        force_ending = SSticker.force_ending;

        login_music = SSticker.login_music;
        round_end_sound = SSticker.round_end_sound;

        minds = SSticker.minds;

        delay_end = SSticker.delay_end;

        tipped = SSticker.tipped;
        selected_tip = SSticker.selected_tip;

        timeLeft = SSticker.timeLeft;

        totalPlayers = SSticker.totalPlayers;
        totalPlayersReady = SSticker.totalPlayersReady;
        total_admins_ready = SSticker.total_admins_ready;

        queue_delay = SSticker.queue_delay;
        queued_players = SSticker.queued_players;
        round_start_time = SSticker.round_start_time;

        queue_delay = SSticker.queue_delay;
        queued_players = SSticker.queued_players;

        if (Master != null) //Set Masters run level if it exists
        {
            if (Dm.eq(current_state, GAME_STATE_SETTING_UP))
                Master.SetRunLevel(RUNLEVEL_SETUP);
            else if (Dm.eq(current_state, GAME_STATE_PLAYING))
                Master.SetRunLevel(RUNLEVEL_GAME);
            else if (Dm.eq(current_state, GAME_STATE_FINISHED))
                Master.SetRunLevel(RUNLEVEL_POSTGAME);
        }
    }

    // tg: code/controllers/subsystem/ticker.dm:765
    public virtual object GetTimeLeft()
    {
        return timeLeft;
    }

    // tg: code/controllers/subsystem/ticker.dm:768
    public virtual void SetTimeLeft(float newtime)
    {
        start_at = newtime;
        timeLeft = newtime;
    }

    /// Updates the lobby music
    /// Does not update if override is FALSE and login_music is already set
    // tg: code/controllers/subsystem/ticker.dm:871
    public virtual void set_lobby_music(object new_music = null, object @override = null)
    {
        @override ??= FALSE;
        if (!Dm.truthy(@override) && Dm.truthy(login_music))
            return;

        login_music = new_music;
        //we just overrode the song, let's update everyone.
        if (Dm.truthy(@override))
        {
            foreach (mob_dead_new_player new_player in Dm.each<mob_dead_new_player>(GLOB.new_player_list))
                new_player?.client.playtitlemusic();
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/ticker.dm:4  SUBSYSTEM_DEF(ticker): GLOBAL_REAL(SSticker, /datum/controller/subsystem/ticker)
    internal datum_controller_subsystem_ticker SSticker;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_ticker SSticker => ref TgState.Current.SSticker;
}

// M10 slice H: the ticker's reboot (ticker.dm:676-724)
public partial class datum_controller_subsystem_ticker : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/ticker.dm:676
    public virtual async DmTask Reboot(object reason = null, object end_string = null, object delay = null)
    {
        // set waitfor = FALSE: the callers run it detached
        if (usr != null && !Dm.truthy(check_rights(R_SERVER, TRUE)))
            return;

        if (!Dm.truthy(delay))
            delay = Dm.num(config.Get(typeof(datum_config_entry_number_round_end_countdown))) * 10;

        object skip_delay = check_rights();
        if (Dm.truthy(delay_end) && !Dm.truthy(skip_delay))
        {
            to_chat(Dm.world, span_boldannounce(I18n.T("An admin has delayed the round end.")));
            return;
        }

        to_chat(Dm.world, span_boldannounce(I18n.T("Rebooting World in []. []", DisplayTimeText(Dm.num(delay)), reason)));
        // tg: :690-695  the round's statistics and logs pages (CONFIG roundstatsurl and gamelogurl links: the logs are not published)

        float start_wait = Dm.num(Dm.world.time);
        // UNTIL(round_end_sound_sent || (world.time - start_wait) > (delay * 2)) (__DEFINES/_helpers.dm), expanded inline
        while (!(Dm.truthy(round_end_sound_sent) || (Dm.num(Dm.world.time) - start_wait) > Dm.num(delay) * 2))
            await stoplag();
        if (reboot_timer != null) //Override existing reboot timers.
            deltimer(reboot_timer);
        reboot_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(reboot_callback), reason, end_string), Dm.num(delay) - (Dm.num(Dm.world.time) - start_wait), TIMER_STOPPABLE, file: "code/controllers/subsystem/ticker.dm", line: 700);
    }

    // tg: code/controllers/subsystem/ticker.dm:703
    public virtual async DmTask reboot_callback(object reason = null, object end_string = null)
    {
        if (Dm.truthy(end_string))
            end_state = Dm.str(end_string);

        log_game(span_boldannounce(Dm.text("Rebooting World. []", reason)));

        await Dm.world.Reboot();
    }

    /**
     * Deletes the current reboot timer and nulls the var
     *
     * Arguments:
     * * user - the user that cancelled the reboot, may be null
     */
    // tg: code/controllers/subsystem/ticker.dm:717
    public virtual object cancel_reboot(mob user = null)
    {
        if (reboot_timer == null)
        {
            to_chat(user, span_warning(I18n.T("There is no pending reboot!")));
            return FALSE;
        }
        to_chat(Dm.world, span_boldannounce(I18n.T("An admin has delayed the round end.")));
        deltimer(reboot_timer);
        reboot_timer = null;
        return TRUE;
    }
}