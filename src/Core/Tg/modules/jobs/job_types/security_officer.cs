// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/security_officer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the job with its department assignment, the department headsets and the distribution procs.

// tg: code/modules/jobs/job_types/security_officer.dm:51
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/jobs/job_types/security_officer.dm:51  GLOBAL_LIST_INIT(available_depts, list(...))
    public DmList available_depts;

    // tg: code/modules/jobs/job_types/security_officer.dm:51  (GLOBAL_MANAGED(available_depts, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalavailable_depts()
    {
        available_depts = Dm.list(SEC_DEPT_ENGINEERING, SEC_DEPT_MEDICAL, SEC_DEPT_SCIENCE, SEC_DEPT_SUPPLY);
        Dm.append(ref gvars_datum_init_order, "available_depts");
    }

    /**
     * The department distribution of the security officers.
     *
     * Keys are refs of the security officer mobs. This is to preserve the list's structure even if the
     * mob gets deleted. This is also safe, as mobs are guaranteed to have a unique ref, as per /mob/GenerateTag().
     */
    // tg: code/modules/jobs/job_types/security_officer.dm:59  GLOBAL_LIST_EMPTY(security_officer_distribution)
    public DmList security_officer_distribution;

    // tg: code/modules/jobs/job_types/security_officer.dm:59  (GLOBAL_MANAGED(security_officer_distribution, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsecurity_officer_distribution()
    {
        security_officer_distribution = Dm.list();
        Dm.append(ref gvars_datum_init_order, "security_officer_distribution");
    }
}

// tg: code/modules/jobs/job_types/security_officer.dm:1
public partial class datum_job_security_officer : datum_job
{
    // tg: code/modules/jobs/job_types/security_officer.dm:61
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        if (!prob(PIG_COP_PROBABILITY))
            return;
        foreach (obj_item_bodypart ham in Dm.each<obj_item_bodypart>(((mob_living_carbon)spawned).get_bodyparts()))
            ham.butcher_drops_override = Dm.list(Dm.kv(typeof(obj_item_food_meat_slab_pig), ham.base_meat_amount));
    }

    // tg: code/modules/jobs/job_types/security_officer.dm:68
    public override async DmTask after_roundstart_spawn(mob_living spawning = null, client player_client = null)
    {
        await base.after_roundstart_spawn(spawning, player_client);
        if (ishuman(spawning))
            await setup_department((mob_living_carbon_human)spawning, player_client, move_to: TRUE);
    }

    // tg: code/modules/jobs/job_types/security_officer.dm:74
    public override async DmTask after_latejoin_spawn(mob_living spawning = null)
    {
        await base.after_latejoin_spawn(spawning);
        if (ishuman(spawning))
        {
            object department = await setup_department((mob_living_carbon_human)spawning, spawning.client);
            if (Dm.truthy(department))
                await announce_latejoin(spawning, department, GLOB.security_officer_distribution);
        }
    }

    /// Returns the department this mob was assigned to, if any.
    // tg: code/modules/jobs/job_types/security_officer.dm:83
    public virtual async DmTask<object> setup_department(mob_living_carbon_human spawning = null, client player_client = null, float? move_to = null)
    {
        object department = player_client?.prefs?.read_preference(typeof(datum_preference_choiced_security_department));
        if (department != null)
        {
            department = get_my_department(spawning, department);

            // This should theoretically still run if a player isn't in the distributions, but isn't a late join.
            GLOB.security_officer_distribution[REF(spawning)] = department;
        }

        Type ears = null;
        Type accessory = null;
        Type dep_trim = null;
        Type destination = null;

        switch (department)
        {
            case SEC_DEPT_SUPPLY:
                ears = typeof(obj_item_radio_headset_headset_sec_alt_department_supply);
                dep_trim = typeof(datum_id_trim_job_security_officer_supply);
                destination = typeof(area_station_security_checkpoint_supply);
                accessory = typeof(obj_item_clothing_accessory_armband_cargo);
                break;
            case SEC_DEPT_ENGINEERING:
                ears = typeof(obj_item_radio_headset_headset_sec_alt_department_engi);
                dep_trim = typeof(datum_id_trim_job_security_officer_engineering);
                destination = typeof(area_station_security_checkpoint_engineering);
                accessory = typeof(obj_item_clothing_accessory_armband_engine);
                break;
            case SEC_DEPT_MEDICAL:
                ears = typeof(obj_item_radio_headset_headset_sec_alt_department_med);
                dep_trim = typeof(datum_id_trim_job_security_officer_medical);
                destination = typeof(area_station_security_checkpoint_medical);
                accessory = typeof(obj_item_clothing_accessory_armband_medblue);
                break;
            case SEC_DEPT_SCIENCE:
                ears = typeof(obj_item_radio_headset_headset_sec_alt_department_sci);
                dep_trim = typeof(datum_id_trim_job_security_officer_science);
                destination = typeof(area_station_security_checkpoint_science);
                accessory = typeof(obj_item_clothing_accessory_armband_science);
                break;
        }

        if (accessory != null)
        {
            obj_item_clothing_under worn_under = spawning.w_uniform as obj_item_clothing_under;
            await worn_under.attach_accessory(Dm.@new(accessory) as obj_item_clothing_accessory);
        }

        if (ears != null)
        {
            if (spawning.ears != null)
                qdel(spawning.ears);
            await spawning.equip_to_slot_or_del(Dm.@new(ears, spawning) as obj_item, ITEM_SLOT_EARS);
        }

        // If there's a departmental sec trim to apply to the card, overwrite.
        if (dep_trim != null)
        {
            obj_item_card_id worn_id = spawning.get_idcard(hand_first: FALSE) as obj_item_card_id;
            await SSid_access.apply_trim_to_card(worn_id, dep_trim);
            spawning.update_ID_card();

            // Update PDA to match new trim.
            obj_item_modular_computer_pda pda = spawning.get_item_by_slot(ITEM_SLOT_BELT) as obj_item_modular_computer_pda;
            object assignment = worn_id.get_trim_assignment();
            if (pda != null && assignment != null)
                pda.imprint_id(spawning.real_name, assignment);
        }

        object spawn_point = pick(LAZYACCESS(GLOB.department_security_spawns, department) as DmList);

        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_sec_start_brig))) && Dm.truthy(move_to) && (destination != null || spawn_point != null))
        {
            if (spawn_point != null)
            {
                await spawning.forceMove(get_turf(spawn_point));
            }
            else
            {
                DmList possible_turfs = get_area_turfs(destination) as DmList;
                while (Dm.truthy(length(possible_turfs)))
                {
                    float random_index = rand(1, length(possible_turfs));
                    turf target = possible_turfs[random_index] as turf;
                    if (isopenturf(target) && Dm.truthy(await spawning.forceMove(target)))
                        break;
                    possible_turfs.Cut(random_index, random_index + 1);
                }
            }
        }

        if (player_client != null)
        {
            if (Dm.truthy(department))
                to_chat(player_client, Dm.text("<b>") + I18n.T("You have been assigned to []!", department) + Dm.text("</b>"));
            else
                to_chat(player_client, Dm.text("<b>") + I18n.T("You have not been assigned to any department. Patrol the halls and help where needed.") + Dm.text("</b>"));
        }

        return department;
    }

    // tg: code/modules/jobs/job_types/security_officer.dm:162
    public virtual async DmTask announce_latejoin(mob officer = null, object department = null, object distribution = null)
    {
        obj_machinery_announcement_system announcement_system = get_announcement_system(typeof(datum_aas_config_entry_announce_officer), null, Dm.list(RADIO_CHANNEL_SECURITY));
        if (announcement_system == null)
            return;

        await announcement_system.announce(typeof(datum_aas_config_entry_announce_officer), Dm.list(
            Dm.kv("OFFICER", officer.real_name),
            Dm.kv("DEPARTMENT", department)
        ), Dm.list(RADIO_CHANNEL_SECURITY));

        DmList targets = Dm.list();

        DmList partners = Dm.list();
        foreach (object officer_ref in Dm.each((DmList)distribution))
        {
            mob partner = Dm.locate_ref(officer_ref) as mob;
            if (partner == null || !Dm.eq(((DmList)distribution)[officer_ref], department))
                continue;
            Dm.append(ref partners, partner.real_name);
        }

        if (Dm.truthy(length(partners)))
        {
            foreach (object messenger_ref in Dm.each(GLOB.pda_messengers))
            {
                datum_computer_file_program_messenger messenger = GLOB.pda_messengers[messenger_ref] as datum_computer_file_program_messenger;
                if (!Dm.@in(messenger.computer?.saved_identification, partners))
                    continue;
                Dm.append(ref targets, messenger);
            }
        }

        if (!Dm.truthy(length(targets)))
            return;

        // I thought it would be great, if AAS also modifies PDA messages. Especially because it's AASs message.
        var signal = Dm.@new<datum_signal_subspace_messaging_tablet_message>(announcement_system, Dm.list(
            Dm.kv("fakename", "Security Department Update"),
            Dm.kv("fakejob", "Automated Announcement System"),
            Dm.kv("message", announcement_system.compile_config_message(typeof(datum_aas_config_entry_announce_officer), Dm.list(
                Dm.kv("OFFICER", officer.real_name),
                Dm.kv("DEPARTMENT", department)
            ))),
            Dm.kv("targets", targets),
            Dm.kv("automated", TRUE)
        ));

        signal.send_to_receivers();
    }

    // tg: code/modules/jobs/job_types/security_officer.dm:209
    public virtual object get_my_department(mob character = null, object preferred_department = null)
    {
        object department = GLOB.security_officer_distribution[REF(character)];

        // This passes when they are a round start security officer.
        if (Dm.truthy(department))
            return department;

        return get_new_officer_distribution_from_late_join(
            preferred_department,
            shuffle(GLOB.available_depts) as DmList,
            GLOB.security_officer_distribution
        );
    }
}

// tg: code/modules/jobs/job_types/security_officer.dm:267
public partial class obj_item_radio_headset_headset_sec_alt_department : obj_item_radio_headset_headset_sec_alt
{
    // tg: code/modules/jobs/job_types/security_officer.dm:267
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_wires(Dm.@new<datum_wires_radio>(this));
        return dot;
    }
}

public static partial class GlobalProcs
{
    /// Returns the distribution of splitting the given security officers into departments.
    /// Return value is an assoc list of candidate => SEC_DEPT_*.
    // tg: code/modules/jobs/job_types/security_officer.dm:288
    public static DmList get_officer_departments(DmList preferences = null, DmList departments = null)
    {
        if (!Dm.truthy(preferences.len))
            return Dm.list();

        // tg's long comment (:292-368): the algorithm, deterministic; to get random results the preferences and departments are
        // shuffled before the proc is ever called.

        preferences = preferences.Copy();
        departments = departments.Copy();

        DmList distribution = get_distribution(preferences.len, departments.len);
        DmList selection = Dm.list_n(preferences.len);

        DmList grouped = Dm.list();
        DmList biggest_group = null;
        object biggest_preference = null;
        DmList indices = Dm.list();

        for (float index = 1; index <= preferences.len; index++)
        {
            Dm.append(ref indices, index);

            object preference = preferences[index];
            if (!Dm.@in(preference, grouped))
                grouped[preference] = Dm.list();
            DmList preference_list = (DmList)grouped[preference];
            Dm.append(ref preference_list, index);

            DmList preferred_group = (DmList)grouped[preference];

            if (!Dm.eq(preference, SEC_DEPT_NONE) && (biggest_group == null || biggest_group.len < preferred_group.len))
            {
                biggest_group = (DmList)grouped[preference];
                biggest_preference = preference;
            }
        }

        if (biggest_group == null)
        {
            preferences[1] = pop(departments);
            return get_officer_departments(preferences, departments);
        }

        if (biggest_group.len >= Dm.num(distribution[1]))
        {
            for (float index = 1; index <= Dm.num(distribution[1]); index++)
                selection[biggest_group[index]] = biggest_preference;

            if (biggest_group.len > Dm.num(distribution[1]))
            {
                for (float leftover = Dm.num(distribution[1]) + 1; leftover <= biggest_group.len; leftover++)
                    preferences[leftover] = SEC_DEPT_NONE;
            }
        }
        else
        {
            float needed = Dm.num(distribution[1]) - biggest_group.len;
            if (Dm.num(LAZYLEN(LAZYACCESS(grouped, SEC_DEPT_NONE) as DmList)) >= needed)
            {
                foreach (object candidate_index in Dm.each(biggest_group))
                    selection[candidate_index] = biggest_preference;

                for (float index = 1; index <= needed; index++)
                    selection[((DmList)grouped[SEC_DEPT_NONE])[index]] = biggest_preference;
            }
            else
            {
                object least_popular_index = grouped[grouped.len];
                if (Dm.eq(least_popular_index, SEC_DEPT_NONE))
                    least_popular_index = grouped[grouped.len - 1];
                DmList least_popular = (DmList)grouped[least_popular_index];
                foreach (object candidate_index in Dm.each(least_popular))
                    preferences[candidate_index] = SEC_DEPT_NONE;
            }
        }

        // Remove all members of the most popular candidate from the list
        for (float chosen = 1; chosen <= selection.len; chosen++)
        {
            if (Dm.eq(selection[chosen], biggest_preference))
            {
                Dm.remove(ref indices, chosen);
                preferences[chosen] = null;
            }
        }

        list_clear_nulls(preferences);

        Dm.remove(ref departments, biggest_preference);

        if (grouped.len != 1)
        {
            DmList next_step = get_officer_departments(preferences, departments);
            for (float index = 1; index <= indices.len; index++)
            {
                object place = indices[index];
                selection[place] = next_step[index];
            }
        }

        return selection;
    }

    // tg: code/modules/jobs/job_types/security_officer.dm:440
    public static DmList get_distribution(float candidates = 0, float departments = 0)
    {
        float number_of_twos = Dm.min(departments, Dm.round(candidates / 2));
        float redistribute = candidates - (2 * number_of_twos);

        DmList distribution = Dm.list_n(Dm.max(1, number_of_twos));

        for (float index = 1; index <= number_of_twos; index++)
            distribution[index] = 2f;

        for (float index = 0; index <= redistribute - 1; index++)
        {
            float place = Dm.mod(index, departments) + 1;
            distribution[place] = Dm.num(distribution[place]) + 1;
        }

        return distribution;
    }

    // tg: code/modules/jobs/job_types/security_officer.dm:454
    public static object get_new_officer_distribution_from_late_join(object preference = null, DmList departments = null, DmList distribution = null)
    {
        // tg's comment (:459-477): a late joiner goes to a department with one officer, else to the least manned one, the preference
        // first; deterministic, the shuffle of departments gives the randomness.
        DmList amount_in_departments = Dm.list();

        foreach (object department in Dm.each(departments))
            amount_in_departments[department] = 0f;

        foreach (object officer in Dm.each(distribution))
        {
            object department = distribution[officer];
            if (department != null)
                amount_in_departments[department] = Dm.num(amount_in_departments[department]) + 1;
        }

        DmList lowest_departments = Dm.list(departments[1]);
        float lowest_amount = INFINITY;

        foreach (object department in Dm.each(amount_in_departments))
        {
            float amount = Dm.num(amount_in_departments[department]);

            if (amount == 1)
            {
                return department;
            }
            else if (lowest_amount > amount)
            {
                lowest_departments = Dm.list(department);
                lowest_amount = amount;
            }
            else if (lowest_amount == amount)
            {
                Dm.append(ref lowest_departments, department);
            }
        }

        return Dm.@in(preference, lowest_departments) ? preference : lowest_departments[1];
    }
}
