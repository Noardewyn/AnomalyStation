// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/new_player/new_player.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F): the lobby mob. Not translated: the polls (Topic's viewpoll and votepollref, the database), the interview (the
// panic bunker, admin tooling). M10 slice G2: the captaincy of a late joiner. M10x slice B (D281): observing from the lobby
// (make_me_an_observer, DV199).

// tg: code/modules/mob/dead/new_player/new_player.dm:3
public partial class mob_dead_new_player : mob_dead
{
    // tg: code/modules/mob/dead/new_player/new_player.dm:22
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (client != null && Dm.eq(SSticker.current_state, GAME_STATE_STARTUP))
        {
            atom_movable_screen_splash fade_out = Dm.@new<atom_movable_screen_splash>(null, null, client, TRUE);
            fade_out.fade(TRUE);
        }

        if (Dm.truthy(length(GLOB.newplayer_start)))
            Waiting.NoSleep(forceMove(pick(GLOB.newplayer_start) as atom));
        else
            Waiting.NoSleep(forceMove(locate(1, 1, 1)));

        object dot = base.Initialize(mapload, __extra);

        Dm.append(ref GLOB.new_player_list, this);
        return dot;
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:35
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.new_player_list, this);

        return base.Destroy(force);
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:40
    public override object mob_negates_gravity()
    {
        return TRUE; //no need to calculate if they have gravity.
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:43
    public override void prepare_huds()
    {
        return;
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:46
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        if (usr != this)
            return null;

        if (client == null)
            return null;

        if (Dm.truthy(client.interviewee))
            return null;

        if (Dm.truthy(href_list?["viewpoll"]) || Dm.truthy(href_list?["votepollref"]))
            Dm.NotTranslated("code/modules/mob/dead/new_player/new_player.dm:56", "Topic: the polls (the database's poll questions, admin tooling)");
        return null;
    }

    /// Quickly gets a boolean of whether the new_player is ready to play or not in places where we would like the boolean logic.
    /// The assertion is that readiness must be an opted in TRUE, while all other states (e.g. not ready, broken, etc) are FALSE.
    /// We organize it this way to ensure the system is extensible for other possible ready states.
    // tg: code/modules/mob/dead/new_player/new_player.dm:66
    public virtual object is_ready_to_play()
    {
        return Dm.eq(ready, PLAYER_READY_TO_PLAY) ? TRUE : FALSE;
    }

    //When you cop out of the round (NB: this HAS A SLEEP FOR PLAYER INPUT IN IT)
    // tg: code/modules/mob/dead/new_player/new_player.dm:70
    public virtual async DmTask<object> make_me_an_observer()
    {
        if (QDELETED(this) || client == null)
        {
            ready = PLAYER_NOT_READY;
            return FALSE;
        }

        object less_input_message = "";
        // tg: :78-79  SSlag_switch.measures[DISABLE_DEAD_KEYLOOP]'s notice: the lag switch is not declared (its measures are off)
        // Don't convert this to tgui please, it's way too important
        // DEVIATION(DV199): BYOND's alert() is tgui's alert window here (the client has no BYOND alert)
        object this_is_like_playing_right = await tgui_alert(usr, I18n.T("Are you sure you wish to observe? You will not be able to play this round![]", less_input_message), I18n.T("Observe"), Dm.list(I18n.T("Yes"), I18n.T("No")));
        if (QDELETED(this) || client == null || !Dm.eq(this_is_like_playing_right, I18n.T("Yes")))
        {
            ready = PLAYER_NOT_READY;
            return FALSE;
        }

        mob_dead_observer observer = Dm.@new<mob_dead_observer>();
        spawning = TRUE;

        observer.started_as_observer = TRUE;
        obj_effect_landmark_observer_start O = locate<obj_effect_landmark_observer_start>(GLOB.landmarks_list);
        to_chat(this, span_notice(I18n.T("Now teleporting.")));
        if (O != null)
            await observer.forceMove(O.loc);
        else
        {
            to_chat(this, span_notice(I18n.T("Teleporting failed. Ahelp an admin please")));
            stack_trace("There's no freaking observer landmark available on this map or you're making observers before the map is initialised", "code/modules/mob/dead/new_player/new_player.dm", 96);
        }

        observer.PossessByPlayer(key);
        // tg: :99  observer.client = client: the key's move (PossessByPlayer) took the client with it
        observer.set_ghost_appearance();
        if (observer.client != null && observer.client.prefs != null)
        {
            observer.real_name = observer.client.prefs.read_preference(typeof(datum_preference_name_real_name)) as string;
            observer.name = Dm.str(observer.real_name);
            observer.client.init_verbs();
            observer.persistent_client.time_of_death = Dm.num(world.time);
        }

        observer.update_appearance();
        observer.stop_sound_channel(CHANNEL_LOBBYMUSIC);
        deadchat_broadcast(I18n.T(" has observed."), Dm.text("<b>[]</b>", observer.real_name), follow_target: observer, turf_target: get_turf(observer), message_type: DEADCHAT_DEATHRATTLE);
        QDEL_NULL(ref mind);
        qdel(this);
        return TRUE;
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:131
    public virtual object IsJobUnavailable(object rank = null, object latejoin = null)
    {
        latejoin ??= FALSE;
        datum_job job = SSjob.get_job(rank) as datum_job;
        if (!Dm.truthy(Dm.bitand(job.job_flags, JOB_NEW_PLAYER_JOINABLE)))
            return JOB_UNAVAILABLE_GENERIC;
        if ((job.current_positions >= job.total_positions) && job.total_positions != -1)
        {
            if (is_assistant_job(job))
            {
                if (Dm.isnum(client.player_age) && Dm.le(client.player_age, 14)) //Newbies can always be assistants
                    return JOB_AVAILABLE;
                foreach (datum_job other_job in Dm.each<datum_job>(SSjob.joinable_occupations))
                {
                    if (other_job.current_positions < other_job.total_positions && other_job != job)
                        return JOB_UNAVAILABLE_SLOTFULL;
                }
            }
            else
                return JOB_UNAVAILABLE_SLOTFULL;
        }

        object eligibility_check = SSjob.check_job_eligibility(this, job, "Mob IsJobUnavailable");
        if (!Dm.eq(eligibility_check, JOB_AVAILABLE))
            return eligibility_check;

        if (Dm.truthy(latejoin) && !Dm.truthy(job.special_check_latejoin(client)))
            return JOB_UNAVAILABLE_GENERIC;
        return JOB_AVAILABLE;
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:154
    public virtual async DmTask<object> AttemptLateSpawn(object rank = null)
    {
        // Check that they're picking someone new for new character respawning
        if (Dm.eq(config.Get(typeof(datum_config_entry_flag_allow_respawn)), RESPAWN_FLAG_NEW_CHARACTER))
        {
            if (Dm.truthy(client?.is_localhost()))
            {
                to_chat(client.mob, span_notice("This character already played in this round, however this is bypassed as localhost."));
                log_admin(Dm.text("[] is bypassing rejoining the round as the same character.", key_name_and_tag(client)));
            }
            else if (Dm.@in(Dm.text("[]", client.prefs.default_slot), persistent_client.joined_as_slots))
            {
                await tgui_alert(usr, "You already have played this character in this round!");
                return FALSE;
            }
        }

        object error = IsJobUnavailable(rank);
        if (!Dm.eq(error, JOB_AVAILABLE))
        {
            await tgui_alert(usr, get_job_unavailable_error_message(error, rank));
            return FALSE;
        }

        if (SSshuttle.arrivals != null)
        {
            if (Dm.truthy(SSshuttle.arrivals.damaged) && Dm.truthy(config.Get(typeof(datum_config_entry_flag_arrivals_shuttle_require_safe_latejoin))))
            {
                await tgui_alert(usr, "The arrivals shuttle is currently malfunctioning! You cannot join.");
                return FALSE;
            }

            if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_arrivals_shuttle_require_undocked))))
                await SSshuttle.arrivals.RequireUndocked(this);
        }
        //Remove the player from the join queue if he was in one and reset the timer
        Dm.remove(ref SSticker.queued_players, this);
        SSticker.queue_delay = 4;

        datum_job job = SSjob.get_job(rank) as datum_job;

        if (!Dm.truthy(SSjob.assign_role(this, job, TRUE)))
        {
            await tgui_alert(usr, "There was an unexpected error putting you into your requested job. If you cannot join with any job, you should contact an admin.");
            return FALSE;
        }

        float latejoin_period = CEILING(STATION_TIME_PASSED() / (5 * MINUTES), 5);
        SSblackbox.record_feedback("tally", "latejoin_time", 1, latejoin_period);
        mind.late_joiner = TRUE;
        atom destination = await mind.assigned_role.get_latejoin_spawn_point() as atom;
        if (destination == null)
            throw Dm.CRASH("Failed to find a latejoin spawn point.", "code/modules/mob/dead/new_player/new_player.dm", 197);
        mob_living character = await create_character(destination, client.prefs.default_slot) as mob_living;
        if (character == null)
            throw Dm.CRASH("Failed to create a character for latejoin.", "code/modules/mob/dead/new_player/new_player.dm", 200);
        await transfer_character();

        await SSjob.equip_rank(character, job, character.client);
        await job.after_latejoin_spawn(character);

        // tg: :204-206  #define IS_NOT_CAPTAIN 0, IS_ACTING_CAPTAIN 1, IS_FULL_CAPTAIN 2
        const int IS_NOT_CAPTAIN = 0, IS_ACTING_CAPTAIN = 1, IS_FULL_CAPTAIN = 2;
        int is_captain = IS_NOT_CAPTAIN;
        object captain_sound = Dm.resource("sound/announcer/notice/notice2.ogg");
        // If we already have a captain, are they a "Captain" rank and are we allowing multiple of them to be assigned?
        if (is_captain_job(job))
        {
            is_captain = IS_FULL_CAPTAIN;
            captain_sound = Dm.resource("sound/announcer/announcement/announce.ogg");
        }
        // If we don't have an assigned cap yet, check if this person qualifies for some from of captaincy.
        else if (!Dm.truthy(SSjob.assigned_captain) && ishuman(character) && Dm.truthy(SSjob.chain_of_command[rank]) && !Dm.truthy(is_banned_from(character.ckey, Dm.list(JOB_CAPTAIN))))
        {
            is_captain = IS_ACTING_CAPTAIN;
        }
        if (is_captain != IS_NOT_CAPTAIN)
        {
            minor_announce(job.get_captaincy_announcement(character), sound_override: captain_sound);
            await SSjob.promote_to_captain(character as mob_living_carbon_human, is_captain == IS_ACTING_CAPTAIN ? TRUE : FALSE);
        }

        Dm.append(ref SSticker.minds, character.mind);
        character.client.init_verbs(); // init verbs for the late join
        mob_living_carbon_human humanc = null;
        if (ishuman(character))
            humanc = character as mob_living_carbon_human; //Let's retypecast the var to be human,

        if (humanc != null) //These procs all expect humans
        {
            if (SSshuttle.arrivals != null)
                SSshuttle.arrivals.QueueAnnounce(humanc, rank);
            else
                await announce_arrival(humanc, rank);
            AddEmploymentContract(humanc);

            humanc.increment_scar_slot();
            humanc.load_persistent_scars();

            // if(GLOB.curse_of_madness_triggered) give_madness(...): the curse of madness (a wizard's ritual, the antagonists) never triggers
        }

        Dm.append(ref GLOB.joined_player_list, character.ckey);

        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_latejoin_antagonists))) && !datum_controller_subsystem_shuttle.EMERGENCY_PAST_POINT_OF_NO_RETURN() && humanc != null) //Borgs aren't allowed to be antags. Will need to be tweaked if we get true latejoin ais.
            await SSdynamic.on_latejoin(humanc);

        if (humanc != null)
        {
            if (Dm.truthy(Dm.bitand(job.job_flags, JOB_ASSIGN_QUIRKS)))
            {
                if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_roundstart_traits))))
                    Dm.Deferred("code/modules/mob/dead/new_player/new_player.dm:247", "AttemptLateSpawn: SSquirks.AssignQuirks (the quirks)");
            }
            else // clear any personalities the prefs added since our job clearly does not want them
                humanc.clear_personalities();
        }

        if (humanc != null) // Quirks may change manifest datapoints, so inject only after assigning quirks
        {
            GLOB.manifest.inject(humanc);
            // SEND_SIGNAL(humanc, COMSIG_HUMAN_CHARACTER_SETUP_FINISHED) (rules 11.1 (e), a statement)
            if (Dm.truthy(humanc._listen_lookup?[COMSIG_HUMAN_CHARACTER_SETUP_FINISHED]))
                humanc._SendSignal(COMSIG_HUMAN_CHARACTER_SETUP_FINISHED, Dm.list(humanc));
        }
        area arrivals = GLOB.areas_by_type[typeof(area_station_hallway_secondary_entry)] as area;
        if (humanc != null && arrivals != null && !Dm.truthy(arrivals.power_environ)) //arrivals depowered
            await humanc.put_in_hands(Dm.@new<obj_item_crowbar_large_emergency>(get_turf(humanc))); //if hands full then just drops on the floor
        log_manifest(character.mind.key, character.mind, character, latejoin: TRUE);
        return null;
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:260
    public virtual void AddEmploymentContract(mob_living_carbon_human employee = null)
    {
        //TODO:  figure out a way to exclude wizards/nukeops/demons from this.
        // for(var/C in GLOB.employmentCabinets): the employment filing cabinets. GAP(lead): /obj/structure/filingcabinet/employment is
        // not translated (records, after M10), so GLOB.employmentCabinets is empty.
    }

    /**
     * Creates, assigns and returns the new_character to spawn as.
     * Assumes a valid mind.assigned_role exists.
     *
     * * destination - where to spawn the character
     * * forced_slot - if provided, will load whatever character is in that slot instead of their active slot
     */
    // tg: code/modules/mob/dead/new_player/new_player.dm:274
    public virtual async DmTask<object> create_character(atom destination = null, object forced_slot = null)
    {
        spawning = TRUE;

        object spawned_slot = Dm.isnum(forced_slot) ? forced_slot : LAZYACCESS(client.prefs.job_assigned_profiles, mind.assigned_role.title);
        if (Dm.isnum(spawned_slot) && !Dm.eq(client.prefs.default_slot, spawned_slot))
            client.prefs.load_character(spawned_slot); // if this fails, we will simply load their current slot anyways

        mind.active = FALSE; //we wish to transfer the key manually
        mob_living spawning_mob = await mind.assigned_role.get_spawn_mob(client, destination) as mob_living;
        if (QDELETED(this))
            return null;

        // Annoyingly the AI mob yoinks our client on init so we have to check for it here
        client player_client = this.client ?? spawning_mob.client;
        if (player_client == null)
            return null;

        if (!isAI(spawning_mob)) // Unfortunately there's still snowflake AI code out there.
        {
            // transfer_to sets mind to null
            datum_mind preserved_mind = mind;
            preserved_mind.original_character_slot_index = player_client.prefs.default_slot;
            await preserved_mind.transfer_to(spawning_mob); //won't transfer key since the mind is not active
            preserved_mind.set_original_character(spawning_mob);
        }

        DmList joined_as_slots = player_client.persistent_client.joined_as_slots;
        LAZYADD(ref joined_as_slots, Dm.text("[]", player_client.prefs.default_slot));
        player_client.persistent_client.joined_as_slots = joined_as_slots;
        player_client.init_verbs();
        object dot = spawning_mob;
        new_character = spawning_mob;
        return dot;
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:305
    public virtual async DmTask<object> transfer_character()
    {
        object dot = new_character;
        if (dot == null)
            return dot;
        new_character.PossessByPlayer(key); //Manually transfer the key to log them in,
        new_character.stop_sound_channel(CHANNEL_LOBBYMUSIC);
        area joined_area = get_area(new_character.loc) as area;
        if (joined_area != null)
            joined_area.on_joining_game(new_character);
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_CREWMEMBER_JOINED, new_character, new_character.mind.assigned_role.title) (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_CREWMEMBER_JOINED]))
            SSdcs._SendSignal(COMSIG_GLOB_CREWMEMBER_JOINED, Dm.list(SSdcs, new_character, new_character.mind.assigned_role.title));
        new_character = null;
        qdel(this);
        return dot;
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:319
    public virtual async DmTask ViewManifest()
    {
        if (client == null)
            return;
        await GLOB.manifest.ui_interact(this);
    }

    // tg: code/modules/mob/dead/new_player/new_player.dm:324
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        return 0;
    }

    // Used to make sure that a player has a valid job preference setup, used to knock players out of eligibility for anything if their prefs don't make sense.
    // A "valid job preference setup" in this situation means at least having one job set to low, or not having "return to lobby" enabled
    // Prevents "antag rolling" by setting antag prefs on, all jobs to never, and "return to lobby if preferences not available"
    // Doing so would previously allow you to roll for antag, then send you back to lobby if you didn't get an antag role
    // This also does some admin notification and logging as well, as well as some extra logic to make sure things don't go wrong
    // tg: code/modules/mob/dead/new_player/new_player.dm:332
    public virtual object check_job_preferences(object warn = null)
    {
        warn ??= TRUE;
        if (client == null)
            return FALSE; //Not sure how this would get run without the mob having a client, but let's just be safe.
        if (!Dm.eq(client.prefs.read_preference(typeof(datum_preference_choiced_jobless_role)), RETURNTOLOBBY))
            return TRUE;
        // If they have antags enabled, they're potentially doing this on purpose instead of by accident. Notify admins if so.
        bool has_antags = Dm.truthy(length(client.prefs.be_special));
        if (Dm.eq(client.prefs.job_preferences.len, 0))
        {
            if (Dm.truthy(warn))
            {
                to_chat(this, span_danger("You have no jobs enabled, along with return to lobby if job is unavailable. This makes you ineligible for any round start role, please update your job preferences."));
            }
            ready = PLAYER_NOT_READY;
            if (has_antags)
            {
                log_admin(Dm.text("[] has no jobs enabled, return to lobby if job is unavailable enabled and [] antag preferences enabled. The player has been forcefully returned to the lobby.", ckey, client.prefs.be_special.len));
                message_admins(Dm.text("[] has no jobs enabled, return to lobby if job is unavailable enabled and [] antag preferences enabled. This is an old antag rolling technique. The player has been asked to update their job preferences and has been forcefully returned to the lobby.", ckey, client.prefs.be_special.len));
            }
            return FALSE; //This is the only case someone should actually be completely blocked from antag rolling as well
        }
        return TRUE;
    }

    ///Auto deadmins an admin when they click to toggle the ready button or join game button in the menu
    // tg: code/modules/mob/dead/new_player/new_player.dm:377
    public virtual object auto_deadmin_on_ready_or_latejoin()
    {
        if (client?.holder == null) //If they aren't an admin we dont care
            return TRUE;
        // tg reads client.prefs's auto_deadmin_on_ready_or_latejoin toggle: an admin preference (not ported), its default FALSE
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_auto_deadmin_on_ready_or_latejoin))) || ((int)Dm.num(client.prefs?.toggles) & DEADMIN_ALWAYS) != 0)
            return client.holder.auto_deadmin();
        return null;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mob/dead/new_player/new_player.dm:111
    public static object get_job_unavailable_error_message(object retval = null, object jobtitle = null)
    {
        if (Dm.eq(retval, JOB_AVAILABLE))
            return Dm.text("[] is available.", jobtitle);
        if (Dm.eq(retval, JOB_UNAVAILABLE_GENERIC))
            return Dm.text("[] is unavailable.", jobtitle);
        if (Dm.eq(retval, JOB_UNAVAILABLE_BANNED))
            return Dm.text("You are currently banned from [].", jobtitle);
        if (Dm.eq(retval, JOB_UNAVAILABLE_PLAYTIME))
            return Dm.text("You do not have enough relevant playtime for [].", jobtitle);
        if (Dm.eq(retval, JOB_UNAVAILABLE_ACCOUNTAGE))
            return Dm.text("Your account is not old enough for [].", jobtitle);
        if (Dm.eq(retval, JOB_UNAVAILABLE_SLOTFULL))
            return Dm.text("[] is already filled to capacity.", jobtitle);
        if (Dm.eq(retval, JOB_UNAVAILABLE_ANTAG_INCOMPAT))
            return Dm.text("[] is not compatible with some antagonist role assigned to you.", jobtitle);
        if (Dm.eq(retval, JOB_UNAVAILABLE_AGE))
            return Dm.text("Your character is not old enough for [].", jobtitle);

        return GENERIC_JOB_UNAVAILABLE_ERROR;
    }
}
