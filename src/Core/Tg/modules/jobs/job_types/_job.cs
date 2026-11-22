// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/_job.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: of /datum/job the members that the list, sort and comparator helpers (code/__HELPERS/_lists.dm, cmp.dm,
// sorts/) name and M4a P5's SSjob setup reads (New, config_check, map_check, display_order_with_department); and
// /datum/outfit/job's vars, pre_equip and post_equip (M2 F1: a joining player's outfit; M4a P5: the ID card; M9 slice E: the PDA's imprint and ringtone).
// get_chameleon_disguise_info and get_types_to_preload are translated with them. The rest of /datum/job is M10.

// tg: code/modules/jobs/job_types/_job.dm:1
// [DmPath("/datum/job")] is on the part in code/modules/jobs/job_exp.dm, the earliest .dme file that declares /datum/job
// (rules 2.3).
public partial class datum_job : datum
{
    // tg: code/modules/jobs/job_types/_job.dm:142
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // CHECK_MAP_JOB_CHANGE(job, change) (__DEFINES/maps.dm:243) is SSmapping?.current_map?.job_changes?[job]?[change],
        // expanded inline (rules 11.1)
        object new_spawn_positions = (SSmapping?.current_map?.job_changes?[title] as DmList)?["spawn_positions"];
        if (isnum(new_spawn_positions))
            spawn_positions = Dm.num(new_spawn_positions);
        object new_total_positions = (SSmapping?.current_map?.job_changes?[title] as DmList)?["total_positions"];
        if (isnum(new_total_positions))
            total_positions = Dm.num(new_total_positions);
    }

    // tg: code/modules/jobs/job_types/_job.dm:673
    public virtual object display_order_with_department()
    {
        Type main_department = (departments_list?[1]) as Type;
        if (!Dm.truthy(main_department))
            main_department = typeof(datum_job_department_undefined);

        return display_order + (Dm.num(Dm.initial(main_department, (datum_job_department p) => p.display_order)) * 1000);
    }
}

// tg: code/modules/jobs/job_types/_job.dm:334
[DmPath("/datum/outfit/job")]
public partial class datum_outfit_job : datum_outfit
{
    // tg: code/modules/jobs/job_types/_job.dm:335-347
    public datum_outfit_job()
    {
        name = "Standard Gear";
        uniform = typeof(obj_item_clothing_under_color_grey);
        id = typeof(obj_item_card_id_advanced);
        ears = typeof(obj_item_radio_headset);
        belt = typeof(obj_item_modular_computer_pda_crew);
        back = typeof(obj_item_storage_backpack);
        shoes = typeof(obj_item_clothing_shoes_sneakers_black);
        box = typeof(obj_item_storage_box_survival);
        preload = TRUE; // These are used by the prefs ui, and also just kinda could use the extra help at roundstart
    }

    // tg: code/modules/jobs/job_types/_job.dm:337
    public Type jobtype = null;

    // tg: code/modules/jobs/job_types/_job.dm:349
    public Type backpack = typeof(obj_item_storage_backpack);
    // tg: code/modules/jobs/job_types/_job.dm:350
    public Type satchel = typeof(obj_item_storage_backpack_satchel);
    // tg: code/modules/jobs/job_types/_job.dm:351
    public Type duffelbag = typeof(obj_item_storage_backpack_duffelbag);
    // tg: code/modules/jobs/job_types/_job.dm:352
    public Type messenger = typeof(obj_item_storage_backpack_messenger);
    // tg: code/modules/jobs/job_types/_job.dm:353
    public Type wintercoat = typeof(obj_item_clothing_suit_hooded_wintercoat);

    // tg: code/modules/jobs/job_types/_job.dm:355
    public object pda_slot = ITEM_SLOT_BELT;

    // tg: code/modules/jobs/job_types/_job.dm:357
    [DmParams("H", "visuals_only")]
    public override async DmTask pre_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (ispath(back, typeof(obj_item_storage_backpack)))
        {
            switch (H.backpack)
            {
                case GBACKPACK:
                    back = typeof(obj_item_storage_backpack); //Grey backpack
                    break;
                case GSATCHEL:
                    back = typeof(obj_item_storage_backpack_satchel); //Grey satchel
                    break;
                case GDUFFELBAG:
                    back = typeof(obj_item_storage_backpack_duffelbag); //Grey Duffel bag
                    break;
                case LSATCHEL:
                    back = typeof(obj_item_storage_backpack_satchel_leather); //Leather Satchel
                    break;
                case GMESSENGER:
                    back = typeof(obj_item_storage_backpack_messenger); //Grey messenger bag
                    break;
                case DSATCHEL:
                    back = satchel; //Department satchel
                    break;
                case DDUFFELBAG:
                    back = duffelbag; //Department duffel bag
                    break;
                case DMESSENGER:
                    back = messenger; //Department messenger bag
                    break;
                default:
                    back = backpack; //Department backpack
                    break;
            }
        }

        //converts the uniform string into the path we'll wear, whether it's the skirt or regular variant
        string holder = null;
        if (H.jumpsuit_style == PREF_SKIRT)
        {
            holder = Dm.text("[]/skirt", uniform);
            if (!Dm.truthy(text2path(holder)))
                holder = Dm.text("[]", uniform);
        }
        else
            holder = Dm.text("[]", uniform);
        uniform = text2path(holder) as Type;

        // var/client/client = GLOB.directory[ckey(H.mind?.key)]; if(client?.is_veteran() &&
        // client?.prefs.read_preference(/datum/preference/toggle/playtime_reward_cloak)) neck = ... (:388-391): the
        // veteran's playtime reward cloak reads the playtime tracking and the preferences (M10).
        Dm.Deferred("code/modules/jobs/job_types/_job.dm:388", "pre_equip: the veteran's playtime reward cloak (playtime and preferences, M10+)");
    }

    // tg: code/modules/jobs/job_types/_job.dm:394
    [DmParams("equipped", "visuals_only")]
    public override async DmTask post_equip(mob_living_carbon_human equipped = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (Dm.truthy(visuals_only))
            return;

        datum_job equipped_job = SSjob.get_job_type(jobtype) as datum_job;

        if (!Dm.truthy(equipped_job))
            equipped_job = SSjob.get_job(equipped.job) as datum_job;

        obj_item_card_id card = equipped.wear_id as obj_item_card_id;

        if (card != null)
        {
            ADD_TRAIT(card, TRAIT_JOB_FIRST_ID_CARD, ROUNDSTART_TRAIT);
            shuffle_inplace(card.access); // Shuffle access list to make NTNet passkeys less predictable
            card.registered_name = equipped.real_name;

            if (Dm.truthy(equipped.age))
                card.registered_age = equipped.age;

            card.update_label();
            await card.update_icon();
            var account = SSeconomy.bank_accounts_by_id[Dm.text("[]", equipped.account_id)] as datum_bank_account;

            if (account != null && Dm.eq(account.account_id, equipped.account_id))
                card.set_account(account);

            equipped.update_ID_card();
        }

        if (!Dm.truthy(pda_slot)) //This job outfit doesn't have a PDA.
            return;

        obj_item pda_found = equipped.get_item_by_slot(Dm.num(pda_slot)) as obj_item;
        obj_item_modular_computer_pda pda = pda_found as obj_item_modular_computer_pda;
        if (pda_found != null && pda == null) //we found something but it isn't a PDA, check if it's inside it instead.
        {
            foreach (obj_item_modular_computer_pda found in Dm.each<obj_item_modular_computer_pda>(pda_found.contents))
            {
                pda = found; // locate() in pda: the first one
                break;
            }
        }

        if (pda == null) //We couldn't find a PDA at all.
        {
            stack_trace("pda_slot was set but we couldn't find a PDA!", "code/modules/jobs/job_types/_job.dm", 430);
            return;
        }

        pda.imprint_id(equipped.real_name, Dm.truthy(equipped_job?.title) ? equipped_job.title : equipped.job);
        pda.update_ringtone(equipped_job?.job_tone);
        pda.UpdateDisplay();

        client equipped_client = GLOB.directory[ckey(equipped.mind?.key)] as client;

        if (equipped_client != null)
            pda.update_pda_prefs(equipped_client);
    }
}

// M10 slice F: the job's spawn, its equipment and its messages; M10 slice G2: the new head's AAS announcement. Not translated: the
// account age (the database, M10+), the mail goodies, the service awards, the traitor objectives and the
// lobby icon (the job icons' asset, the preferences menu's job page).

// tg: code/modules/jobs/job_types/_job.dm:152
public partial class datum_job : datum
{
    /// Executes after the mob has been spawned in the map. Client might not be yet in the mob, and is thus a separate variable.
    // tg: code/modules/jobs/job_types/_job.dm:152
    public virtual async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        if (Dm.truthy(length(mind_traits)))
            spawned.mind.add_traits(mind_traits, JOB_TRAIT);

        spawned.mind.desensitized_level = Dm.clamp(desensitized_base, DESENSITIZED_MINIMUM, spawned.mind.desensitized_level);

        obj_item_organ_liver liver = spawned.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && Dm.truthy(length(liver_traits)))
            liver.add_traits(liver_traits, JOB_TRAIT);

        if (!ishuman(spawned))
        {
            // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_JOB_AFTER_SPAWN, src, spawned, player_client) (rules 11.1 (e), a statement)
            if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_JOB_AFTER_SPAWN]))
                SSdcs._SendSignal(COMSIG_GLOB_JOB_AFTER_SPAWN, Dm.list(SSdcs, this, spawned, player_client));
            return;
        }

        mob_living_carbon_human spawned_human = spawned as mob_living_carbon_human;
        DmList roundstart_experience;

        if (player_client != null)
        {
            foreach (obj_item_organ our_organ in Dm.each<obj_item_organ>(spawned_human.organs))
                ADD_TRAIT(our_organ, TRAIT_CLIENT_STARTING_ORGAN, ROUNDSTART_TRAIT);
        }

        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_jobs_have_minimal_access))))
            roundstart_experience = minimal_skills;
        else
            roundstart_experience = skills;

        if (Dm.truthy(roundstart_experience))
        {
            foreach (object i in Dm.each(roundstart_experience))
                spawned_human.mind.adjust_experience(i, roundstart_experience[i], TRUE);
        }

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_JOB_AFTER_SPAWN, src, spawned, player_client) (rules 11.1 (e), a statement)
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_JOB_AFTER_SPAWN]))
            SSdcs._SendSignal(COMSIG_GLOB_JOB_AFTER_SPAWN, Dm.list(SSdcs, this, spawned, player_client));
    }

    /// Return the outfit to use
    // tg: code/modules/jobs/job_types/_job.dm:189
    public virtual object get_outfit(object consistent = null)
    {
        return outfit;
    }

    /// Announce that this job as joined the round to all crew members.
    /// Note the joining mob has no client at this point.
    // tg: code/modules/jobs/job_types/_job.dm:194
    public virtual async DmTask announce_job(mob_living joining_mob = null)
    {
        if (Dm.truthy(head_announce))
            announce_head(joining_mob as mob_living_carbon_human, Dm.list(head_announce));
    }

    //Used for a special check of whether to allow a client to latejoin as this job.
    // tg: code/modules/jobs/job_types/_job.dm:200
    public virtual object special_check_latejoin(client latejoin = null)
    {
        return TRUE;
    }
}

// tg: code/modules/jobs/job_types/_job.dm:204
public partial class mob_living : mob
{
    // tg: code/modules/jobs/job_types/_job.dm:204
    public virtual async DmTask on_job_equipping(datum_job equipping = null, client player_client = null)
    {
        return;
    }
}

// tg: code/modules/jobs/job_types/_job.dm:209
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/jobs/job_types/_job.dm:207  #define VERY_LATE_ARRIVAL_TOAST_PROB 20 (file-local, #undef at :226)
    private const int VERY_LATE_ARRIVAL_TOAST_PROB = 20;

    // tg: code/modules/jobs/job_types/_job.dm:209
    public override async DmTask on_job_equipping(datum_job equipping = null, client player_client = null)
    {
        if (Dm.truthy(equipping.paycheck_department))
        {
            var bank_account = Dm.@new<datum_bank_account>(real_name, equipping, ((datum_species)dna.species).payday_modifier);
            await bank_account.payday(STARTING_PAYCHECKS, free: TRUE);
            account_id = bank_account.account_id;
            bank_account.replaceable = FALSE;
            add_mob_memory(typeof(datum_memory_key_account), Dm.named("remembered_id", account_id));
        }

        await dress_up_as_job(
            equipping: equipping,
            visual_only: FALSE,
            player_client: player_client,
            consistent: FALSE);

        // if(EMERGENCY_PAST_POINT_OF_NO_RETURN && prob(VERY_LATE_ARRIVAL_TOAST_PROB)): the emergency shuttle (GAP(lead), SSshuttle is not
        // translated, slice G), never past the point of no return
    }

    // tg: code/modules/jobs/job_types/_job.dm:231
    public override async DmTask dress_up_as_job(datum_job equipping = null, float? visual_only = null, client player_client = null, float? consistent = null)
    {
        visual_only ??= FALSE;
        consistent ??= FALSE;
        await ((datum_species)dna.species).pre_equip_species_outfit(equipping, this, visual_only);
        await equip_outfit_and_loadout(equipping.get_outfit(consistent), player_client?.prefs, visual_only);
    }
}

// tg: code/modules/jobs/job_types/_job.dm:228
public partial class mob_living : mob
{
    // tg: code/modules/jobs/job_types/_job.dm:228
    public virtual async DmTask dress_up_as_job(datum_job equipping = null, float? visual_only = null, client player_client = null, float? consistent = null)
    {
        return;
    }
}

// tg: code/modules/jobs/job_types/_job.dm:236
public partial class datum_job : datum
{
    // tg: code/modules/jobs/job_types/_job.dm:236
    public virtual void announce_head(mob_living_carbon_human human = null, object channels = null)
    {
        //tells the given channel that the given mob is the new department head. See communications.dm for valid channels.
        if (human != null)
        {
            //timer because these should come after the captain announcement
            SSticker.OnRoundstart(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs._addtimer),
                Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.aas_config_announce), typeof(datum_aas_config_entry_newhead),
                    Dm.list(Dm.kv("PERSON", human.real_name), Dm.kv("RANK", human.job)), null, channels, null, TRUE), 1));
        }
    }

    //If the configuration option is set to require players to be logged as old enough to play certain jobs, then this proc checks that they are, otherwise it just returns 1
    // tg: code/modules/jobs/job_types/_job.dm:242
    public virtual object player_old_enough(client player = null)
    {
        if (player == null || !Dm.truthy(available_in_days(player)))
            return TRUE; //Available in 0 days = available right now = player is old enough to play.
        return FALSE;
    }

    // tg: code/modules/jobs/job_types/_job.dm:248
    public virtual object available_in_days(client player = null)
    {
        if (player == null)
            return 0;

        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_age_restriction_for_jobs))))
            return 0;

        //Without a database connection we can't get a player's age so we'll assume they're old enough for all jobs
        // if(!SSdbcore.Connect()) return 0: the database (M10+) is never connected
        return 0;
    }

    /**
     * # config_check
     *
     * Checks the config to see if this job should be enabled
     */
    // tg: code/modules/jobs/job_types/_job.dm:276
    public virtual object config_check()
    {
        return TRUE;
    }

    /**
     * # map_check
     *
     * Checks the map config for job changes
     * If they have 0 spawn and total positions in the config, the job is entirely removed from occupations prefs for the round.
     */
    // tg: code/modules/jobs/job_types/_job.dm:285
    public virtual object map_check()
    {
        bool available_roundstart = true;
        bool available_latejoin = true;

        object edited_spawn_positions = (SSmapping?.current_map?.job_changes?[title] as DmList)?["spawn_positions"]; // CHECK_MAP_JOB_CHANGE
        if (edited_spawn_positions != null && Dm.eq(edited_spawn_positions, 0))
            available_roundstart = false;
        object edited_total_positions = (SSmapping?.current_map?.job_changes?[title] as DmList)?["total_positions"]; // CHECK_MAP_JOB_CHANGE
        if (edited_total_positions != null && Dm.eq(edited_total_positions, 0))
            available_latejoin = false;

        if (!available_roundstart && !available_latejoin) //map config disabled the job
            return FALSE;

        return TRUE;
    }

    /// Gets the message that shows up when spawning as this job
    // tg: code/modules/jobs/job_types/_job.dm:302
    public object get_spawn_message()
    {
        return boxed_message(span_infoplain(jointext(get_spawn_message_information(), "\n&bull; ")));
    }

    /// Returns a list of strings that correspond to chat messages sent to this mob when they join the round.
    // tg: code/modules/jobs/job_types/_job.dm:307
    public virtual DmList get_spawn_message_information()
    {
        DmList info = Dm.list();
        Dm.append(ref info, Dm.text("<b>You are the [].</b>\n", title));
        object related_policy = get_policy(Dm.truthy(policy_override) ? policy_override : title);
        object radio_info = get_radio_information();
        if (Dm.truthy(related_policy))
            Dm.append(ref info, related_policy);
        if (Dm.truthy(supervisors))
            Dm.append(ref info, Dm.text("As the [] you answer directly to []. Special circumstances may change this.", title, supervisors));
        if (Dm.truthy(radio_info))
            Dm.append(ref info, radio_info);
        if (Dm.truthy(req_admin_notify))
            Dm.append(ref info, "<b>You are playing a job that is important for Game Progression. If you have to disconnect, please notify the admins via adminhelp.</b>");
        if (Dm.truthy(config.Get(typeof(datum_config_entry_number_minimal_access_threshold))))
        {
            Dm.append(ref info, span_boldnotice(Dm.text("As this station was initially staffed with a [] have been added to your ID card.",
                Dm.truthy(config.Get(typeof(datum_config_entry_flag_jobs_have_minimal_access))) ? "full crew, only your job's necessities" : "skeleton crew, additional access may")));
        }

        return info;
    }

    /// Returns information pertaining to this job's radio.
    // tg: code/modules/jobs/job_types/_job.dm:330
    public virtual object get_radio_information()
    {
        if (Dm.truthy(Dm.bitand(job_flags, JOB_CREW_MEMBER)))
            return "<b>Prefix your message with :h to speak on your department's radio. To see other prefixes, look closely at your headset.</b>";
        return null;
    }

    /// An overridable getter for more dynamic goodies.
    // tg: code/modules/jobs/job_types/_job.dm:461  (syndicate G4a, D316: the mail)
    public virtual object get_mail_goodies(mob recipient = null)
    {
        return mail_goodies;
    }

    // tg: code/modules/jobs/job_types/_job.dm:465
    public virtual void award_service(client winner = null, object award = null)
    {
        Dm.NotTranslated("code/modules/jobs/job_types/_job.dm:465", "award_service: the service awards (the medals, after M10)");
    }

    // tg: code/modules/jobs/job_types/_job.dm:469
    public virtual object get_captaincy_announcement(mob_living captain = null)
    {
        return Dm.text("Due to extreme staffing shortages, newly promoted Acting Captain [] on deck!", captain.real_name);
    }

    /// Returns an atom where the mob should spawn in.
    // tg: code/modules/jobs/job_types/_job.dm:474
    public virtual async DmTask<object> get_roundstart_spawn_point()
    {
        if (Dm.truthy(random_spawns_possible))
        {
            if (HAS_TRAIT(SSstation, STATION_TRAIT_LATE_ARRIVALS))
                return await get_latejoin_spawn_point();
            if (HAS_TRAIT(SSstation, STATION_TRAIT_RANDOM_ARRIVALS))
            {
                // tg: :479  (gaps R2: the Drive-by landing station trait)
                turf random_turf = get_safe_random_station_turf(typesof(typeof(area_station_hallway)));
                return random_turf ?? await get_latejoin_spawn_point();
            }
            if (HAS_TRAIT(SSstation, STATION_TRAIT_HANGOVER))
            {
                obj_effect_landmark_start hangover_spawn_point = null;
                foreach (obj_effect_landmark_start_hangover hangover_landmark in Dm.each<obj_effect_landmark_start_hangover>(GLOB.start_landmarks_list))
                {
                    hangover_spawn_point = hangover_landmark;
                    if (Dm.truthy(hangover_landmark.used)) //so we can revert to spawning them on top of eachother if something goes wrong
                        continue;
                    hangover_landmark.used = TRUE;
                    break;
                }
                return hangover_spawn_point ?? await get_latejoin_spawn_point();
            }
        }
        if (Dm.truthy(length(GLOB.jobspawn_overrides[title])))
            return pick(GLOB.jobspawn_overrides[title]);
        obj_effect_landmark_start spawn_point = get_default_roundstart_spawn_point() as obj_effect_landmark_start;
        if (spawn_point == null) //if there isn't a spawnpoint send them to latejoin, if there's no latejoin go yell at your mapper
            return await get_latejoin_spawn_point();
        return spawn_point;
    }

    /// Handles finding and picking a valid roundstart effect landmark spawn point, in case no uncommon different spawning events occur.
    // tg: code/modules/jobs/job_types/_job.dm:498
    public virtual object get_default_roundstart_spawn_point()
    {
        object dot = null;
        foreach (obj_effect_landmark_start spawn_point in Dm.each<obj_effect_landmark_start>(GLOB.start_landmarks_list))
        {
            if (!Dm.eq(spawn_point.name, title))
                continue;
            dot = spawn_point;
            if (Dm.truthy(spawn_point.used)) //so we can revert to spawning them on top of eachother if something goes wrong
                continue;
            spawn_point.used = TRUE;
            break;
        }
        if (dot == null)
            log_mapping(Dm.text("Job [] ([]) couldn't find a round start spawn point.", title, type));
        return dot;
    }

    /// Finds a valid latejoin spawn point, checking for events and special conditions.
    // tg: code/modules/jobs/job_types/_job.dm:511
    public virtual async DmTask<object> get_latejoin_spawn_point()
    {
        if (Dm.truthy(length(GLOB.jobspawn_overrides[title]))) //We're doing something special today.
            return pick(GLOB.jobspawn_overrides[title]);
        if (Dm.truthy(length(SSjob.latejoin_trackers)))
            return pick(SSjob.latejoin_trackers);
        // DEVIATION(DV180): without the arrivals shuttle (slice G), the job's own start (where its access opens the doors) before the
        // last resort, which picks any job's
        return SSjob.job_start_turf(title) ?? await SSjob.get_last_resort_spawn_points();
    }

    /// Spawns the mob to be played as, taking into account preferences and the desired spawn point.
    // tg: code/modules/jobs/job_types/_job.dm:520
    public virtual async DmTask<object> get_spawn_mob(client player_client = null, atom spawn_point = null)
    {
        mob_living spawn_instance;
        if (ispath(spawn_type, typeof(mob_living_silicon_ai)))
        {
            // This is unfortunately necessary because of snowflake AI init code. To be refactored.
            spawn_instance = Dm.@new(spawn_type, get_turf(spawn_point), player_client.mob, null, null, TRUE) as mob_living;
        }
        else
            spawn_instance = await spawn_point.JoinPlayerHere(spawn_type, TRUE) as mob_living;
        await spawn_instance.apply_prefs_job(player_client, this);
        if (player_client == null)
        {
            qdel(spawn_instance);
            return null; // Disconnected while checking for the appearance ban.
        }
        return spawn_instance;
    }
}

// tg: code/modules/jobs/job_types/_job.dm:535
public partial class mob_living : mob
{
    /// Applies the preference options to the spawning mob, taking the job into account. Assumes the client has the proper mind.
    // tg: code/modules/jobs/job_types/_job.dm:535
    public virtual async DmTask apply_prefs_job(client player_client = null, datum_job job = null)
    {
    }
}

// tg: code/modules/jobs/job_types/_job.dm:538
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/jobs/job_types/_job.dm:538
    public override async DmTask apply_prefs_job(client player_client = null, datum_job job = null)
    {
        // GLOB.current_anonymous_theme: the anonymous names (an admin's secret, admin tooling), never set
        bool fully_randomize = Dm.truthy(player_client.prefs.should_be_random_hardcore(job, player_client.mob.mind)) || Dm.truthy(is_banned_from(player_client.ckey, "Appearance"));
        if (player_client == null)
            return; // Disconnected while checking for the appearance ban.

        object human_authority_setting = config.Get(typeof(datum_config_entry_string_human_authority));
        bool require_human = false;

        // If the job in question is a head of staff,
        // check the config to see if we should force the player onto a human character or not
        if (Dm.truthy(Dm.bitand(job.job_flags, JOB_HEAD_OF_STAFF)))
        {
            // If non-humans are the norm and jobs must be forced to be only for humans
            // then we only force the player to be a human if the job exclusively allows humans
            if (Dm.eq(human_authority_setting, HUMAN_AUTHORITY_HUMAN_WHITELIST))
                require_human = Dm.eq(job.human_authority, JOB_AUTHORITY_HUMANS_ONLY);
            // If humans are the norm and jobs must be allowed to be played by non-humans
            // then we only force the player to be a human if the job doesn't allow for non-humans to play it
            else if (Dm.eq(human_authority_setting, HUMAN_AUTHORITY_NON_HUMAN_WHITELIST))
                require_human = !Dm.eq(job.human_authority, JOB_AUTHORITY_NON_HUMANS_ALLOWED);
            // If humans are the norm and there is no chance that a non-human can be a head of staff
            // always return true, since there is no chance that a non-human can be a head of staff.
            else if (Dm.eq(human_authority_setting, HUMAN_AUTHORITY_ENFORCED))
                require_human = true;
        }

        this.job = job.title;

        if (fully_randomize)
        {
            player_client.prefs.apply_prefs_to(this);

            if (require_human)
                await randomize_human_appearance(Dm.bitnot(RANDOMIZE_SPECIES));
            else
                await randomize_human_appearance();

            if (require_human)
            {
                set_species(typeof(datum_species_human));
                ((datum_species)dna.species).roundstart_changed = TRUE;
            }
        }
        else
        {
            bool is_antag = Dm.@in(player_client.mob.mind, GLOB.pre_setup_antags);
            if (require_human)
                player_client.prefs.randomise["species"] = FALSE;
            player_client.prefs.safe_transfer_prefs_to(this, TRUE, is_antag ? TRUE : FALSE);
            if (require_human && !ishumanbasic(this))
            {
                set_species(typeof(datum_species_human));
                ((datum_species)dna.species).roundstart_changed = TRUE;
                Waiting.NoSleep(apply_pref_name(typeof(datum_preference_name_backup_human), player_client));
            }
            if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_force_random_names))))
            {
                real_name = Dm.str(generate_random_name_species_based(
                    player_client.prefs.read_preference(typeof(datum_preference_choiced_gender)),
                    TRUE,
                    player_client.prefs.read_preference(typeof(datum_preference_choiced_species))));
            }
        }
        dna.update_dna_identity();
        Waiting.NoSleep(updateappearance());
    }
}

// tg: code/modules/jobs/job_types/_job.dm:638
public partial class datum_job : datum
{
    /**
     * Called after a successful roundstart spawn.
     * Client is not yet in the mob.
     * This happens after after_spawn()
     */
    // tg: code/modules/jobs/job_types/_job.dm:638
    public virtual async DmTask after_roundstart_spawn(mob_living spawning = null, client player_client = null)
    {
    }

    /**
     * Called after a successful latejoin spawn.
     * Client is in the mob.
     * This happens after after_spawn()
     */
    // tg: code/modules/jobs/job_types/_job.dm:647
    public virtual async DmTask after_latejoin_spawn(mob_living spawning = null)
    {
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_JOB_AFTER_LATEJOIN_SPAWN, src, spawning) (rules 11.1 (e), a statement)
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_JOB_AFTER_LATEJOIN_SPAWN]))
            SSdcs._SendSignal(COMSIG_GLOB_JOB_AFTER_LATEJOIN_SPAWN, Dm.list(SSdcs, this, spawning));
        spawning.client.show_spawn_text_overlay();
    }

    /// Called when a mob that has this job is admin respawned
    // tg: code/modules/jobs/job_types/_job.dm:653
    public virtual async DmTask on_respawn(mob new_character = null)
    {
        await SSjob.equip_rank(new_character as mob_living, new_character.mind.assigned_role, new_character.client);
    }

    /// This proc may be called when someone of this job is made into a traitor to create custom objectives related to the job.
    // tg: code/modules/jobs/job_types/_job.dm:657
    public virtual object generate_traitor_objective()
    {
        return null;
    }

    /// Returns a large (due to cropping) icon of this job's sechud icon state.
    // tg: code/modules/jobs/job_types/_job.dm:661
    public virtual object get_lobby_icon()
    {
        Dm.NotTranslated("code/modules/jobs/job_types/_job.dm:661", "get_lobby_icon: the job's lobby icon (the preferences menu's job page)");
        return default;
    }
}

// tg: code/modules/jobs/job_types/_job.dm:609
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/jobs/job_types/_job.dm:609
    public override async DmTask apply_prefs_job(client player_client = null, datum_job job = null)
    {
        if (mmi != null)
        {
            object organic_name;
            // GLOB.current_anonymous_theme: the anonymous names (an admin's secret, admin tooling), never set, as in the human's
            if (Dm.eq(player_client.prefs.read_preference(typeof(datum_preference_choiced_random_name)), RANDOM_ENABLED) || Dm.truthy(config.Get(typeof(datum_config_entry_flag_force_random_names))) || Dm.truthy(is_banned_from(player_client.ckey, "Appearance")))
            {
                if (player_client == null)
                    return; // Disconnected while checking the appearance ban.

                organic_name = generate_random_name_species_based(
                    player_client.prefs.read_preference(typeof(datum_preference_choiced_gender)),
                    TRUE,
                    player_client.prefs.read_preference(typeof(datum_preference_choiced_species))
                );
            }
            else
            {
                if (player_client == null)
                    return; // Disconnected while checking the appearance ban.
                organic_name = player_client.prefs.read_preference(typeof(datum_preference_name_real_name));
            }

            mmi.set_name(organic_name);
        }
        // If this checks fails, then the name will have been handled during initialization.
        if (!Dm.eq(player_client.prefs.read_preference(typeof(datum_preference_name_cyborg)), DEFAULT_CYBORG_NAME)) // !GLOB.current_anonymous_theme: never set
            await apply_pref_name(typeof(datum_preference_name_cyborg), player_client);
    }
}

// tg: code/modules/jobs/job_types/_job.dm:600  (silicons SI4)
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/jobs/job_types/_job.dm:600
    public override async DmTask apply_prefs_job(client player_client = null, datum_job job = null)
    {
        // GLOB.current_anonymous_theme: the anonymous names (an admin's secret, admin tooling), never set, as in the human's
        await apply_pref_name(typeof(datum_preference_name_ai), player_client); // This proc already checks if the player is appearance banned.
        await set_core_display_icon(null, player_client);
        apply_pref_emote_display(player_client);
        apply_pref_hologram_display(player_client);
    }
}
