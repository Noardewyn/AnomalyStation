// Translated from tgstation (snapshot 1d99bd7f): code/datums/station_traits/negative_traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file: the negative station traits (the postal strike opens the shuttle loan's mail strike situation,
// gaps E3). Deferred leaves (not-translated.md section 183): the nebula's parallax layer (no parallax in the 3D view) and the
// radioactive nebula's own system (SSradioactive_nebula, the nebula shielders, the radiation immunity of the rulesets' bodies: not
// ported, so SSstation keeps the trait out of the pool, DV432). The var blocks are generated (D111).

// tg: code/datums/station_traits/negative_traits.dm:9
public partial class datum_station_trait_distant_supply_lines : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:17
    public override void on_round_start()
    {
        SSeconomy.pack_price_modifier *= 1.2f;
    }
}

///A negative trait that stops mail from arriving (or the inverse if on holiday). It also enables a specific shuttle loan situation.
// tg: code/datums/station_traits/negative_traits.dm:21
public partial class datum_station_trait_mail_blocked : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:28
    public override void on_round_start()
    {
        //This is either a holiday or Sunday... well then, let's flip the situation.
        if (Dm.truthy(SSeconomy.mail_blocked))
        {
            name = "Postal system overtime";
            report_message = I18n.T("Despite being a day off, the postal system is working overtime today. Mail will be delivered this shift.");
        }
        else
        {
            var our_event = Dm.locate<datum_round_event_control_shuttle_loan>(SSevents.control);
            Dm.remove(ref our_event.unavailable_situations, typeof(datum_shuttle_loan_situation_mail_strike));
        }
        SSeconomy.mail_blocked = Dm.truthy(SSeconomy.mail_blocked) ? FALSE : TRUE;
    }
}

// tg: code/datums/station_traits/negative_traits.dm:38  (tg's own path: a subtype of mail_blocked, as the snapshot spells it)
public partial class datum_station_trait_mail_blocked_hangover : datum_station_trait_mail_blocked
{
    // tg: code/datums/station_traits/negative_traits.dm:38
    public override void revert()
    {
        var our_event = Dm.locate<datum_round_event_control_shuttle_loan>(SSevents.control);
        Dm.combine(ref our_event.unavailable_situations, typeof(datum_shuttle_loan_situation_mail_strike));
        SSeconomy.mail_blocked = Dm.truthy(SSeconomy.mail_blocked) ? FALSE : TRUE;
        base.revert();
    }
}

// tg: code/datums/station_traits/negative_traits.dm:62
public partial class datum_station_trait_hangover : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:71
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_LATEJOIN_SPAWN, nameof(on_job_after_spawn));
    }

    // tg: code/datums/station_traits/negative_traits.dm:75
    public override void revert()
    {
        foreach (obj_effect_landmark_start_hangover hangover_spot in Dm.each<obj_effect_landmark_start_hangover>(GLOB.start_landmarks_list))
            QDEL_LIST(hangover_spot.hangover_debris);

        base.revert();
    }

    // tg: code/datums/station_traits/negative_traits.dm:81  SIGNAL_HANDLER
    public virtual void on_job_after_spawn(datum source = null, datum_job job = null, mob_living spawned_mob = null)
    {
        if (!prob(35))
            return;
        var hat_type = (Type)pick(
            typeof(obj_item_clothing_head_costume_sombrero_green),
            typeof(obj_item_clothing_head_fedora),
            typeof(obj_item_clothing_mask_balaclava),
            typeof(obj_item_clothing_head_costume_ushanka),
            typeof(obj_item_clothing_head_costume_cardborg),
            typeof(obj_item_clothing_head_costume_pirate),
            typeof(obj_item_clothing_head_cone));
        var hat = Dm.@new(hat_type, spawned_mob) as obj_item;
        Waiting.NoSleep(spawned_mob.equip_to_slot_or_del(hat, ITEM_SLOT_HEAD));
    }
}

// tg: code/datums/station_traits/negative_traits.dm:99
public partial class datum_station_trait_blackout : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:106
    public override void on_round_start()
    {
        base.on_round_start();
        foreach (obj_machinery_power_apc apc in Dm.each_cast<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
        {
            if (Dm.truthy(is_station_level(apc.z)) && prob(60))
                Waiting.NoSleep(apc.overload_lighting());
        }
    }
}

// tg: code/datums/station_traits/negative_traits.dm:125
public partial class datum_station_trait_overflow_job_bureaucracy : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:132
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSjob, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(set_overflow_job_override));
    }

    // tg: code/datums/station_traits/negative_traits.dm:136
    public override object get_report()
    {
        return I18n.T("[] - It seems for some reason we put out the wrong job-listing for the overflow role this shift...I hope you like []s.", name, chosen_job_name);
    }

    // tg: code/datums/station_traits/negative_traits.dm:139  SIGNAL_HANDLER
    public virtual void set_overflow_job_override(datum source = null)
    {
        var picked_job = pick(SSjob.get_valid_overflow_jobs() as DmList) as datum_job;
        chosen_job_name = LOWER_TEXT(picked_job.title); // like Chief Engineers vs like chief engineers
        SSjob.set_overflow_role(picked_job.type);
        UnregisterSignal(SSjob, COMSIG_SUBSYSTEM_POST_INITIALIZE);
    }
}

// tg: code/datums/station_traits/negative_traits.dm:155
public partial class datum_station_trait_bot_languages : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:164
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // What "caused" our robots to go haywire (fluff)
        object event_source = pick(I18n.T("an ion storm"), I18n.T("a syndicate hacking attempt"), I18n.T("a malfunction"), I18n.T("issues with your onboard AI"), I18n.T("an intern's mistakes"), I18n.T("budget cuts"));
        report_message = I18n.T("Your station's friendly bots have had their language matrix fried due to [], resulting in some strange and unfamiliar speech patterns.", event_source);
    }

    // tg: code/datums/station_traits/negative_traits.dm:170
    public override void on_round_start()
    {
        base.on_round_start();
        // All bots that exist round start on station Z OR on the escape shuttle have their set language randomized.
        foreach (mob_living found_bot in Dm.each_cast<mob_living>(GLOB.bots_list))
            found_bot.randomize_language_if_on_station();
    }
}

// tg: code/datums/station_traits/negative_traits.dm:176
public partial class datum_station_trait_machine_languages : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:186
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // What "caused" our machines to go haywire (fluff)
        object event_source = pick(I18n.T("an ion storm"), I18n.T("a malfunction"), I18n.T("a software update"), I18n.T("a power surge"), I18n.T("a computer virus"), I18n.T("a subdued machine uprising"), I18n.T("a clown's prank"));
        report_message = I18n.T("Your station's machinery have had their language matrix fried due to [], resulting in some strange and unfamiliar speech patterns.", event_source);
    }
}

// tg: code/datums/station_traits/negative_traits.dm:192
public partial class datum_station_trait_revenge_of_pun_pun : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:203  var/static/list/weapon_types
    public static ref DmList weapon_types => ref TgState.Current.datum_station_trait_revenge_of_pun_pun__weapon_types;

    // tg: code/datums/station_traits/negative_traits.dm:277  arm_monke's var/static/blood_types
    private static ref DmList arm_monke__blood_types => ref TgState.Current.datum_station_trait_revenge_of_pun_pun__arm_monke__blood_types;

    // tg: code/datums/station_traits/negative_traits.dm:205
    public override async DmTask New(params object[] __extra)
    {
        // tg's New does not call ..(): no round start hook, no code trait (the trait has none)
        if (weapon_types == null)
        {
            weapon_types = Dm.list(
                Dm.kv(typeof(obj_item_chair), 20),
                Dm.kv(typeof(obj_item_tailclub), 10),
                Dm.kv(typeof(obj_item_melee_baseball_bat), 10),
                Dm.kv(typeof(obj_item_melee_chainofcommand_tailwhip), 10),
                Dm.kv(typeof(obj_item_melee_chainofcommand_tailwhip_kitty), 10),
                Dm.kv(typeof(obj_item_reagent_containers_cup_glass_bottle), 20),
                Dm.kv(typeof(obj_item_reagent_containers_cup_glass_bottle_kong), 5),
                Dm.kv(typeof(obj_item_switchblade_extended), 10),
                Dm.kv(typeof(obj_item_sign_random), 10),
                Dm.kv(typeof(obj_item_gun_ballistic_automatic_pistol), 1));
        }

        RegisterSignal(SSatoms, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(arm_monke));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/station_traits/negative_traits.dm:222  SIGNAL_HANDLER
    public virtual void arm_monke()
    {
        mob_living punpun = GLOB.the_one_and_only_punpun;
        if (punpun == null)
            return;
        var weapon_type = (Type)pick_weight(weapon_types);
        var weapon = Dm.@new(weapon_type) as obj_item;
        if (!Dm.truthy(Waiting.NoSleep(punpun.put_in_l_hand(weapon))) && !Dm.truthy(Waiting.NoSleep(punpun.put_in_r_hand(weapon))))
        {
            // Guess they did all this with whatever they have in their hands already
            qdel(weapon);
            weapon = (punpun.get_active_held_item() ?? punpun.get_inactive_held_item()) as obj_item;
        }

        if (weapon != null)
            Waiting.NoSleep(weapon.add_mob_blood(punpun));
        Waiting.NoSleep(punpun.add_mob_blood(punpun));

        if (punpun.ai_controller != null) // In case punpun somehow lacks AI
            QDEL_NULL(ref punpun.ai_controller);

        Dm.@new<datum_ai_controller_monkey_angry>(punpun);

        var place = get_area(punpun) as area;

        DmList area_open_turfs = Dm.list();
        foreach (turf location in Dm.each<turf>(place))
        {
            if (Dm.truthy(location.density))
                continue;
            Dm.append(ref area_open_turfs, location);
        }

        Waiting.NoSleep(punpun.forceMove(pick(area_open_turfs) as atom));

        float blood_count = rand(10, 40);
        for (float i = 1; i <= blood_count; i++)
            Dm.@new<obj_effect_decal_cleanable_blood>(pick(area_open_turfs));

        DmList blood_path = Dm.list();
        for (float i = 1; i <= 10; i++) // Only 10 attempts
        {
            var destination = pick(area_open_turfs) as turf;
            turf next_step = get_step_to(punpun, destination);
            for (float k = 1; k <= 30; k++) // Max 30 steps
            {
                if (next_step == null)
                    break;
                Dm.append(ref blood_path, next_step);
                next_step = get_step_to(next_step, destination);
            }
            if (Dm.truthy(length(blood_path)))
                break;
        }
        if (!Dm.truthy(length(blood_path)))
            throw Dm.CRASH("Unable to make a path from punpun", "code/datums/station_traits/negative_traits.dm", 267);

        turf last_location = null;
        foreach (turf location in Dm.each_cast<turf>(blood_path))
        {
            last_location = location;

            if (prob(80))
                Dm.@new<obj_effect_decal_cleanable_blood>(location);

            if (prob(50))
            {
                arm_monke__blood_types ??= Dm.list(
                    typeof(obj_effect_decal_cleanable_blood_splatter),
                    typeof(obj_effect_decal_cleanable_blood_gibs));
                var blood_type = (Type)pick(arm_monke__blood_types);
                Dm.@new(blood_type, get_turf(pick(orange(location, 2)) as atom));
            }
        }

        Dm.@new<obj_effect_decal_cleanable_blood_gibs_torso>(last_location);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/station_traits/negative_traits.dm:203
    internal DmList datum_station_trait_revenge_of_pun_pun__weapon_types;

    // tg: code/datums/station_traits/negative_traits.dm:277
    internal DmList datum_station_trait_revenge_of_pun_pun__arm_monke__blood_types;

    // tg: code/datums/station_traits/negative_traits.dm:359
    internal DmList datum_station_trait_revolutionary_trashing__trash_talk = Dm.list(
        "amyjon",
        "antilizard",
        "body",
        "cyka",
        "danger",
        "electricdanger",
        "face",
        "guy",
        "matt",
        "peace",
        "prolizard",
        "radiation",
        "revolution",
        "shotgun",
        "skull",
        "splatter",
        "star",
        "stickman",
        "toilet",
        "toolbox",
        "uboa");
}

// Abstract station trait used for traits that modify a random event in some way (their weight or max occurrences).
// tg: code/datums/station_traits/negative_traits.dm:287
public partial class datum_station_trait_random_event_weight_modifier : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:301
    public override void on_round_start()
    {
        base.on_round_start();
        var modified_event = Dm.locate_type(event_control_path, SSevents.control) as datum_round_event_control;
        if (modified_event == null)
            throw Dm.CRASH(Dm.text("[] could not find a round event controller to modify on round start (likely has an invalid event_control_path set)!", type), "code/datums/station_traits/negative_traits.dm", 305);

        modified_event.weight *= weight_multiplier;
        modified_event.max_occurrences += max_occurrences_modifier;
    }
}

// tg: code/datums/station_traits/negative_traits.dm:310
public partial class datum_station_trait_random_event_weight_modifier_ion_storms : datum_station_trait_random_event_weight_modifier
{
    // tg: code/datums/station_traits/negative_traits.dm:318
    public override object get_pulsar_message()
    {
        object advisory_string = I18n.T("Advisory Level: <b>ERROR</b></center><BR>");
        advisory_string = Dm.add(advisory_string, scramble_message_replace_chars(I18n.T("Your sector's advisory level is ERROR. An electromagnetic field has stormed through nearby surveillance equipment, causing major data loss. Partial data was recovered and showed no credible threats to Nanotrasen assets within the Spinward Sector; however, the Department of Intelligence advises maintaining high alert against potential threats due to the lack of complete data."), 35));
        return advisory_string;
    }
}

// tg: code/datums/station_traits/negative_traits.dm:351
public partial class datum_station_trait_revolutionary_trashing : datum_station_trait
{
    ///The IDs of the graffiti designs that we will generate.
    // tg: code/datums/station_traits/negative_traits.dm:359  var/static/list/trash_talk
    public static ref DmList trash_talk => ref TgState.Current.datum_station_trait_revolutionary_trashing__trash_talk;

    // tg: code/datums/station_traits/negative_traits.dm:383
    public override void on_round_start()
    {
        base.on_round_start();

        Waiting.Detach(trash_this_place()); //Must be called asynchronously
    }

    /**
     * "Trashes" the command areas of the station.
     *
     * Creates random graffiti and damages certain machinery/structures in the
     * command areas of the station.
     */
    // tg: code/datums/station_traits/negative_traits.dm:395
    public virtual async DmTask trash_this_place()
    {
        foreach (area_station_command area_to_trash in Dm.each<area_station_command>(GLOB.areas))
        {
            foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(area_to_trash.get_zlevel_turf_lists()))
            {
                foreach (turf current_turf in Dm.each_cast<turf>(zlevel_turfs))
                {
                    if (isclosedturf(current_turf))
                        continue;
                    if (prob(25))
                    {
                        var created_art = Dm.@new<obj_effect_decal_cleanable_crayon>(current_turf, rgb(rand(0, 255), rand(0, 255), rand(0, 255)), pick(trash_talk)); // RANDOM_COLOUR
                        created_art.pixel_x = rand(-10, 10);
                        created_art.pixel_y = rand(-10, 10);
                    }

                    if (prob(0.01f))
                    {
                        Dm.@new<obj_effect_mob_spawn_corpse_human_assistant>(current_turf);
                        continue;
                    }

                    foreach (atom current_thing in Dm.each_cast<atom>(current_turf.contents))
                    {
                        if (istype(current_thing, typeof(obj_machinery_light)) && prob(40))
                        {
                            var light_to_smash = (obj_machinery_light)current_thing;
                            await light_to_smash.break_light_tube(skip_sound_and_sparks: TRUE);
                            continue;
                        }

                        if (istype(current_thing, typeof(obj_structure_window)))
                        {
                            if (prob(15))
                                await current_thing.take_damage(rand(30, 90));
                            continue;
                        }

                        if (istype(current_thing, typeof(obj_structure_table)) && prob(40))
                        {
                            await current_thing.take_damage(100);
                            continue;
                        }

                        if (istype(current_thing, typeof(obj_structure_chair)) && prob(60))
                        {
                            await current_thing.take_damage(150);
                            continue;
                        }

                        if (istype(current_thing, typeof(obj_machinery_computer)) && prob(30))
                        {
                            if (istype(current_thing, typeof(obj_machinery_computer_communications)))
                                continue; //To prevent the shuttle from getting autocalled at the start of the round
                            await current_thing.take_damage(160);
                            continue;
                        }

                        if (istype(current_thing, typeof(obj_machinery_vending)) && prob(45))
                        {
                            var vendor_to_trash = (obj_machinery_vending)current_thing;
                            if (prob(50))
                                await vendor_to_trash.tilt(get_turf(vendor_to_trash), 0); // crit effects can do some real weird shit, lets disable it

                            if (prob(50))
                                await vendor_to_trash.take_damage(150);
                            continue;
                        }

                        if (istype(current_thing, typeof(obj_structure_fireaxecabinet))) //A staple of revolutionary behavior
                        {
                            await current_thing.take_damage(90);
                            continue;
                        }

                        if (istype(current_thing, typeof(obj_item_bedsheet_captain)))
                        {
                            Dm.@new<obj_item_bedsheet_rev>(current_thing.loc);
                            qdel(current_thing);
                            continue;
                        }

                        if (istype(current_thing, typeof(obj_item_bedsheet_captain_double)))
                        {
                            Dm.@new<obj_item_bedsheet_rev_double>(current_thing.loc);
                            qdel(current_thing);
                            continue;
                        }
                    }

                    CHECK_TICK();
                }
            }
        }
    }
}

///Station traits that influence the space background and apply some unique effects!
// tg: code/datums/station_traits/negative_traits.dm:462
public partial class datum_station_trait_nebula : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:474
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        // SSparallax.swap_out_random_parallax_layer(nebula_layer): the space's parallax layer (SSparallax is not ported, the 3D view
        // has no parallax layers)
        Dm.Deferred("code/datums/station_traits/negative_traits.dm:477", "nebula New: SSparallax.swap_out_random_parallax_layer, the nebula's parallax layer (no parallax in the 3D view)");

        //Color the carp in unique colors to better blend with the nebula
        if (Dm.truthy(carp_color_override))
            GLOB.carp_colors = carp_color_override as DmList;
    }
}

///Station nebula that incur some sort of effect if no shielding is created
// tg: code/datums/station_traits/negative_traits.dm:484
public partial class datum_station_trait_nebula_hostile : datum_station_trait_nebula
{
    // tg: code/datums/station_traits/negative_traits.dm:497
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        calculate_nebula_strength();

        await apply_nebula_effect(nebula_intensity - Dm.num(await get_shielding_level()));
        return null;
    }

    // tg: code/datums/station_traits/negative_traits.dm:502
    public override void on_round_start()
    {
        base.on_round_start();

        _addtimer(Dm.@new<datum_callback>(this, nameof(send_instructions)), 30 * SECONDS, file: "code/datums/station_traits/negative_traits.dm", line: 505);
    }

    ///Announce to the station what's going on and what they need to do
    // tg: code/datums/station_traits/negative_traits.dm:508
    public virtual async DmTask send_instructions()
    {
        await DmTask.CompletedTask;
    }

    ///Calculate how strong we currently are
    // tg: code/datums/station_traits/negative_traits.dm:512
    public virtual void calculate_nebula_strength()
    {
        nebula_intensity = min(STATION_TIME_PASSED(), maximum_nebula_intensity) / intensity_increment_time;
    }

    ///Check how strong the stations shielding is
    // tg: code/datums/station_traits/negative_traits.dm:516
    public virtual async DmTask<object> get_shielding_level()
    {
        float shield_strength = 0;
        foreach (atom_movable shielder in Dm.each_cast<atom_movable>(shielding))
        {
            if (!Dm.truthy(is_station_level(shielder.z)))
                continue;
            var callback = shielding[shielder] as datum_callback;
            shield_strength += Dm.num(await callback.Invoke());
        }

        return shield_strength;
    }

    ///Add a shielding unit to ask for shielding
    // tg: code/datums/station_traits/negative_traits.dm:527
    public virtual void add_shielder(atom_movable shielder = null, object shielding_proc = null)
    {
        shielding[shielder] = Dm.@new<datum_callback>(shielder, shielding_proc);

        RegisterSignal(shielder, COMSIG_QDELETING, nameof(remove_shielder));
    }

    ///Remove a shielding unit from our tracking
    // tg: code/datums/station_traits/negative_traits.dm:533  SIGNAL_HANDLER
    public virtual void remove_shielder(atom_movable shielder = null)
    {
        shielding.Remove(shielder);
    }

    ///The station did not set up shielding, start creating effects
    // tg: code/datums/station_traits/negative_traits.dm:539
    public virtual async DmTask apply_nebula_effect(float? effect_strength = null)
    {
        await DmTask.CompletedTask;
    }
}

public static partial class GlobalProcs
{
    // tg: code/datums/station_traits/negative_traits.dm:542
    public static object add_to_nebula_shielding(atom_movable shielder = null, Type nebula_type = null, object shielding_proc = null)
    {
        var nebula = Dm.locate_type(nebula_type, SSstation?.station_traits) as datum_station_trait_nebula_hostile; // rules 10.6: a harness world may lack SSstation
        if (nebula == null)
            return FALSE;

        nebula.add_shielder(shielder, shielding_proc);
        return null;
    }
}

///The station will be inside a radioactive nebula! Space is radioactive and the station needs to start setting up nebula shielding
// tg: code/datums/station_traits/negative_traits.dm:550
public partial class datum_station_trait_nebula_hostile_radiation : datum_station_trait_nebula_hostile
{
    // tg: code/datums/station_traits/negative_traits.dm:581
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        RegisterSignal(SSdcs, COMSIG_RULESET_BODY_GENERATED_FROM_GHOSTS, nameof(on_spawned_mob));

        foreach (area target in Dm.each_cast<area>(get_areas(radioactive_areas)))
        {
            RegisterSignal(target, COMSIG_AREA_ENTERED, nameof(on_entered));
            RegisterSignal(target, COMSIG_AREA_EXITED, nameof(on_exited));
        }
    }

    // tg: code/datums/station_traits/negative_traits.dm:590
    public override void on_round_start()
    {
        base.on_round_start();

        //Let people order more nebula shielding
        var pack = SSshuttle.supply_packs[typeof(datum_supply_pack_engineering_rad_nebula_shielding_kit)] as datum_supply_pack;
        Dm.combine(ref pack.order_flags, ORDER_SPECIAL_ENABLED);

        //Give robotics some radiation protection modules for modsuits
        var supply_pack_modsuits = Dm.@new<datum_supply_pack_engineering_rad_protection_modules>();
        send_supply_pod_to_area(Waiting.NoSleep(supply_pack_modsuits.generate(null)), typeof(area_station_science_robotics), typeof(obj_structure_closet_supplypod_centcompod));

        //Send a nebula shielding unit to engineering
        var supply_pack_shielding = Dm.@new<datum_supply_pack_engineering_rad_nebula_shielding_kit>();
        if (!send_supply_pod_to_area(Waiting.NoSleep(supply_pack_shielding.generate(null)), typeof(area_station_engineering_main), typeof(obj_structure_closet_supplypod_centcompod)))
            //if engineering isn't valid, just send it to the bridge
            send_supply_pod_to_area(Waiting.NoSleep(supply_pack_shielding.generate(null)), typeof(area_station_command_bridge), typeof(obj_structure_closet_supplypod_centcompod));

        // Let medical know resistance is futile
        if (Dm.@in(typeof(area_station_medical_virology), GLOB.areas_by_type))
        {
            Waiting.NoSleep(send_fax_to_area(
                Dm.@new<obj_item_paper_fluff_radiation_nebula_virologist>(),
                typeof(area_station_medical_virology),
                I18n.T("NT Virology Department"),
                force: TRUE,
                force_pod_type: typeof(obj_structure_closet_supplypod_centcompod)));
        }

        //Disables radstorms, they don't really make sense since we already have the nebula causing storms
        var modified_event = Dm.locate<datum_round_event_control_radiation_storm>(SSevents.control);
        modified_event.weight = 0;
    }

    ///They entered space? START BOMBING WITH RADS HAHAHAHA. old_area can be null for new objects
    // tg: code/datums/station_traits/negative_traits.dm:622  SIGNAL_HANDLER
    public virtual void on_entered(area space = null, atom_movable enterer = null, area old_area = null)
    {
        // Old area was radioactive, so what's the point. nothing changes. nothing ever does. also make sure the subsystem is alive before we give it food
        if (istype(old_area, radioactive_areas))
            return;
        // SSradioactive_nebula.initialized and SSradioactive_nebula.fake_irradiate(enterer): the subsystem is not ported
        Dm.Deferred("code/datums/station_traits/negative_traits.dm:626", "radioactive nebula on_entered: SSradioactive_nebula.fake_irradiate (the subsystem is not ported)");
    }

    ///Called when an atom leaves space, so we can remove the radiation effect
    // tg: code/datums/station_traits/negative_traits.dm:632  SIGNAL_HANDLER
    public virtual void on_exited(area space = null, atom_movable exiter = null, object direction = null)
    {
        // SSradioactive_nebula.fake_unirradiate(exiter): the subsystem is not ported
        Dm.Deferred("code/datums/station_traits/negative_traits.dm:635", "radioactive nebula on_exited: SSradioactive_nebula.fake_unirradiate (the subsystem is not ported)");

        // The component handles its own removal
    }

    /// When a mob is spawned by dynamic, intercept and give it a little radiation shield. Only works for dynamic mobs!
    // tg: code/datums/station_traits/negative_traits.dm:640  SIGNAL_HANDLER
    public virtual void on_spawned_mob(datum source = null, mob spawned_mob = null)
    {
        if (!istype(get_area(spawned_mob), radioactive_areas)) //only if you're spawned in the radioactive areas
            return;

        if (!isliving(spawned_mob)) // Dynamic shouldn't spawn non-living but uhhhhhhh why not
            return;

        // spawnee.apply_status_effect(/datum/status_effect/radiation_immunity/radnebula): the status effect is not ported
        Dm.Deferred("code/datums/station_traits/negative_traits.dm:650", "radioactive nebula on_spawned_mob: the radnebula radiation immunity (the status effect is not ported)");
    }

    // tg: code/datums/station_traits/negative_traits.dm:652
    public override async DmTask apply_nebula_effect(float? effect_strength = null)
    {
        effect_strength ??= 0;
        //big bombad now
        if (effect_strength > 0 && !Dm.truthy(SSmapping.is_planetary())) //admins can force this
        {
            if (!Dm.truthy(SSweather.get_weather_by_type(typeof(datum_weather_rad_storm_nebula))))
            {
                send_care_package_at = world.time + send_care_package_time; // COOLDOWN_START
                SSweather.run_weather(typeof(datum_weather_rad_storm_nebula));
            }

            //Send a care package to temporarily lift the storm!
            if (send_care_package_at <= world.time) // COOLDOWN_FINISHED
            {
                send_care_package_at = world.time + send_care_package_time; // COOLDOWN_START
                Type rad_shield = typeof(obj_machinery_nebula_shielding_emergency_radiation);

                priority_announce(
                    I18n.T("Is everything okay there? We're getting high radiation readings from inside the station. We're sending an emergency shielding unit for now, it will last [] minutes. \n\nSet up the nebula shielding. You can order construction kits at cargo if yours have been lost.\n",
                        Dm.initial(rad_shield, (obj_machinery_nebula_shielding_emergency p) => p.detonate_in) / (1 * MINUTES)));

                _addtimer(Dm.@new<datum_callback>(this, nameof(send_care_package)), 10 * SECONDS, file: "code/datums/station_traits/negative_traits.dm", line: 671);
            }
            return;
        }

        //No storms, shielding is good!
        var weather = SSweather.get_weather_by_type(typeof(datum_weather_rad_storm_nebula)) as datum_weather;
        weather?.wind_down();
        send_care_package_at = 0; // COOLDOWN_RESET
    }

    ///Send a care package because it is not going well
    // tg: code/datums/station_traits/negative_traits.dm:680
    public virtual void send_care_package()
    {
        // new /obj/effect/pod_landingzone(get_safe_random_station_turf_equal_weight(), new /obj/structure/closet/supplypod/centcompod(),
        // new /obj/machinery/nebula_shielding/emergency/radiation()): the emergency shielder is not ported
        Dm.Deferred("code/datums/station_traits/negative_traits.dm:681", "radioactive nebula send_care_package: the emergency nebula shielder (not ported)");
    }

    // tg: code/datums/station_traits/negative_traits.dm:683
    public override async DmTask send_instructions()
    {
        Type shielder = typeof(obj_machinery_nebula_shielding_radiation);
        Type innate_shielding = typeof(obj_machinery_gravity_generator_main);
        //How long do we have until the first shielding unit needs to be up?
        object deadline = I18n.T("[] minute\\s", (Dm.initial(innate_shielding, (obj_machinery_gravity_generator_main p) => p.radioactive_nebula_shielding) * intensity_increment_time) / (1 * MINUTES));
        //For how long each shielding unit will protect for
        object shielder_time = I18n.T("[] minute\\s", (Dm.num(Dm.initial(shielder, (obj_machinery_nebula_shielding p) => p.shielding_strength)) * intensity_increment_time) / (1 * MINUTES));
        //Max shielders, excluding the grav-gen to avoid confusion when that goes down
        float max_shielders = ((maximum_nebula_intensity / intensity_increment_time)) / Dm.num(Dm.initial(shielder, (obj_machinery_nebula_shielding p) => p.shielding_strength));

        object announcement = I18n.T("Your station has been constructed inside a radioactive nebula. Standard spacesuits will not protect against the nebula and using them is strongly discouraged. \n\nEXTREME IMPORTANCE: The station is falling deeper into the nebula, and the gravity generator's innate radiation shielding will not hold very long. Your engineering department has been supplied with all the necessary supplies to set up shields to protect against the nebula. Additional supply crates can be ordered at cargo. \n\nYou have [] before the nebula enters the station. Every shielding unit will provide an additional [] of protection, fully protecting the station with [] shielding units.\n",
            deadline, shielder_time, max_shielders);

        priority_announce(announcement, sound: Dm.resource("sound/announcer/notice/notice1.ogg"));

        //Set the display screens to the radiation alert
        var frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS) as datum_radio_frequency;
        if (frequency == null)
            return;

        var signal = Dm.@new<datum_signal>();
        signal.data["command"] = "alert";
        signal.data["picture_state"] = "radiation";

        // new /atom/movable/virtualspeaker(null) as the source: a speaker nowhere (the port's post_signal takes an /obj source; the
        // range is not checked, so no source is the same)
        frequency.post_signal(null, signal);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/station_traits/negative_traits.dm:717
    public override object get_decal_color(object thing_to_color = null, object pattern = null)
    {
        if (istype(get_area(thing_to_color as atom), typeof(area_station_hallway))) //color hallways green
            return COLOR_GREEN;
        return null;
    }
}

///Starts a storm on roundstart
// tg: code/datums/station_traits/negative_traits.dm:722
public partial class datum_station_trait_storm : datum_station_trait
{
    // tg: code/datums/station_traits/negative_traits.dm:726
    public override void on_round_start()
    {
        base.on_round_start();

        SSweather.run_weather(storm_type);
    }
}

/// Calls down an eternal storm on planetary stations
// tg: code/datums/station_traits/negative_traits.dm:732
public partial class datum_station_trait_storm_foreverstorm : datum_station_trait_storm
{
    // tg: code/datums/station_traits/negative_traits.dm:741
    public override object get_pulsar_message()
    {
        object advisory_string = I18n.T("Advisory Level: <b>Ice Giant</b></center><BR>");
        advisory_string = Dm.add(advisory_string, I18n.T("The ongoing blizzard has interfered with our surveillance equipment, and we cannot provide an accurate threat summary at this time. We advise you to stay safe and avoid traversing the area around the station."));
        return advisory_string;
    }
}
