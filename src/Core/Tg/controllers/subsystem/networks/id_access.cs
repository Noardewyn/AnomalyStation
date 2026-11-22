// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/networks/id_access.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P5 (the lead). The subsystem with its M4a procs (docs/ai/specs/m4a-ownership.md, P5's list): the access flags, the
// regions, the trim singletons and the wildcard dictionary, the trim application to cards. M5 slice 6: the TGUI lists and the access
// descriptions (the airlock electronics' access window, D148). The trim overrides (chameleon cards) come after the core version, the
// in-character record (M10).

/**
 * Non-processing subsystem that holds various procs and data structures to manage ID cards, trims and access.
 */
// tg: code/controllers/subsystem/networks/id_access.dm:4 SUBSYSTEM_DEF(id_access)
// The var block (name, ss_flags = SS_NO_FIRE, the lists, spare_id_safe_code) is generated (types-converter.md 3.2), with
// the [DmPath] (rules 2.3: this part declares no var).
public partial class datum_controller_subsystem_id_access : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(id_access) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSid_access)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/networks/id_access.dm:4
    public override async DmTask New(params object[] __extra)
    {
        if (SSid_access != this)
        {
            if (SSid_access is datum_controller_subsystem_id_access)
            {
                Recover();
                qdel(SSid_access);
            }
            SSid_access = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/networks/id_access.dm:38
    [DmParams()]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        // We use this because creating the trim singletons requires the config to be loaded.
        setup_access_flags();
        setup_region_lists();
        setup_trim_singletons();
        setup_wildcard_dict();
        setup_access_descriptions();
        setup_tgui_lists();

        spare_id_safe_code = Dm.text("[][][][][]", rand(0, 9), rand(0, 9), rand(0, 9), rand(0, 9), rand(0, 9));

        return SS_INIT_SUCCESS;
    }

    /**
     * Called by [/datum/controller/subsystem/ticker/proc/setup]
     *
     * This runs through every /datum/id_trim/job singleton and ensures that its access is setup according to
     * appropriate config entries.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:57
    public virtual void refresh_job_trim_singletons()
    {
        foreach (object trim in Dm.each(typesof(typeof(datum_id_trim_job))))
        {
            datum_id_trim_job job_trim = trim_singletons_by_path[trim] as datum_id_trim_job;
            if (QDELETED(job_trim))
            {
                stack_trace(Dm.text("Trim \\[[]\\] missing from trim singleton list. Reinitialising this trim.", trim), "code/controllers/subsystem/networks/id_access.dm", 61);
                trim_singletons_by_path[trim] = Dm.@new(trim as Type);
                continue;
            }

            job_trim.refresh_trim_access();
        }
    }

    /// Build access flag lists.
    // tg: code/controllers/subsystem/networks/id_access.dm:69
    public virtual void setup_access_flags()
    {
        accesses_by_flag[Dm.text("[]", ACCESS_FLAG_COMMON)] = COMMON_ACCESS();
        foreach (object access in Dm.each(accesses_by_flag[Dm.text("[]", ACCESS_FLAG_COMMON)]))
            Dm.combine(ref flags_by_access, Dm.list(Dm.kv(Dm.text("[]", access), ACCESS_FLAG_COMMON)));

        accesses_by_flag[Dm.text("[]", ACCESS_FLAG_COMMAND)] = COMMAND_ACCESS();
        foreach (object access in Dm.each(accesses_by_flag[Dm.text("[]", ACCESS_FLAG_COMMAND)]))
            Dm.combine(ref flags_by_access, Dm.list(Dm.kv(Dm.text("[]", access), ACCESS_FLAG_COMMAND)));

        accesses_by_flag[Dm.text("[]", ACCESS_FLAG_PRV_COMMAND)] = PRIVATE_COMMAND_ACCESS();
        foreach (object access in Dm.each(accesses_by_flag[Dm.text("[]", ACCESS_FLAG_PRV_COMMAND)]))
            Dm.combine(ref flags_by_access, Dm.list(Dm.kv(Dm.text("[]", access), ACCESS_FLAG_PRV_COMMAND)));

        accesses_by_flag[Dm.text("[]", ACCESS_FLAG_CAPTAIN)] = CAPTAIN_ACCESS();
        foreach (object access in Dm.each(accesses_by_flag[Dm.text("[]", ACCESS_FLAG_CAPTAIN)]))
            Dm.combine(ref flags_by_access, Dm.list(Dm.kv(Dm.text("[]", access), ACCESS_FLAG_CAPTAIN)));

        accesses_by_flag[Dm.text("[]", ACCESS_FLAG_CENTCOM)] = CENTCOM_ACCESS();
        foreach (object access in Dm.each(accesses_by_flag[Dm.text("[]", ACCESS_FLAG_CENTCOM)]))
            Dm.combine(ref flags_by_access, Dm.list(Dm.kv(Dm.text("[]", access), ACCESS_FLAG_CENTCOM)));

        accesses_by_flag[Dm.text("[]", ACCESS_FLAG_SYNDICATE)] = SYNDICATE_ACCESS();
        foreach (object access in Dm.each(accesses_by_flag[Dm.text("[]", ACCESS_FLAG_SYNDICATE)]))
            Dm.combine(ref flags_by_access, Dm.list(Dm.kv(Dm.text("[]", access), ACCESS_FLAG_SYNDICATE)));

        accesses_by_flag[Dm.text("[]", ACCESS_FLAG_AWAY)] = AWAY_ACCESS();
        foreach (object access in Dm.each(accesses_by_flag[Dm.text("[]", ACCESS_FLAG_AWAY)]))
            Dm.combine(ref flags_by_access, Dm.list(Dm.kv(Dm.text("[]", access), ACCESS_FLAG_AWAY)));

        accesses_by_flag[Dm.text("[]", ACCESS_FLAG_SPECIAL)] = CULT_ACCESS();
        foreach (object access in Dm.each(accesses_by_flag[Dm.text("[]", ACCESS_FLAG_SPECIAL)]))
            Dm.combine(ref flags_by_access, Dm.list(Dm.kv(Dm.text("[]", access), ACCESS_FLAG_SPECIAL)));

        access_flag_string_by_flag[Dm.text("[]", ACCESS_FLAG_COMMON)] = ACCESS_FLAG_COMMON_NAME;
        access_flag_string_by_flag[Dm.text("[]", ACCESS_FLAG_COMMAND)] = ACCESS_FLAG_COMMAND_NAME;
        access_flag_string_by_flag[Dm.text("[]", ACCESS_FLAG_PRV_COMMAND)] = ACCESS_FLAG_PRV_COMMAND_NAME;
        access_flag_string_by_flag[Dm.text("[]", ACCESS_FLAG_CAPTAIN)] = ACCESS_FLAG_CAPTAIN_NAME;
        access_flag_string_by_flag[Dm.text("[]", ACCESS_FLAG_CENTCOM)] = ACCESS_FLAG_CENTCOM_NAME;
        access_flag_string_by_flag[Dm.text("[]", ACCESS_FLAG_SYNDICATE)] = ACCESS_FLAG_SYNDICATE_NAME;
        access_flag_string_by_flag[Dm.text("[]", ACCESS_FLAG_AWAY)] = ACCESS_FLAG_AWAY_NAME;
        access_flag_string_by_flag[Dm.text("[]", ACCESS_FLAG_SPECIAL)] = ACCESS_FLAG_SPECIAL_NAME;
    }

    /// Populates the region lists with data about which accesses correspond to which regions.
    // tg: code/controllers/subsystem/networks/id_access.dm:112
    public virtual void setup_region_lists()
    {
        accesses_by_region[REGION_ALL_STATION] = REGION_ACCESS_ALL_STATION();
        accesses_by_region[REGION_ALL_GLOBAL] = REGION_ACCESS_ALL_GLOBAL();
        accesses_by_region[REGION_GENERAL] = REGION_ACCESS_GENERAL();
        accesses_by_region[REGION_SECURITY] = REGION_ACCESS_SECURITY();
        accesses_by_region[REGION_MEDBAY] = REGION_ACCESS_MEDBAY();
        accesses_by_region[REGION_RESEARCH] = REGION_ACCESS_RESEARCH();
        accesses_by_region[REGION_ENGINEERING] = REGION_ACCESS_ENGINEERING();
        accesses_by_region[REGION_SUPPLY] = REGION_ACCESS_SUPPLY();
        accesses_by_region[REGION_COMMAND] = REGION_ACCESS_COMMAND();
        accesses_by_region[REGION_CENTCOM] = REGION_ACCESS_CENTCOM();

        station_regions = REGION_AREA_STATION();
    }

    /// Instantiate trim singletons and add them to a list.
    // tg: code/controllers/subsystem/networks/id_access.dm:127
    public virtual void setup_trim_singletons()
    {
        foreach (object trim in Dm.each(typesof(typeof(datum_id_trim))))
            trim_singletons_by_path[trim] = Dm.@new(trim as Type);
    }

    /// Creates various data structures that primarily get fed to tgui interfaces, although these lists are used in other places.
    // tg: code/controllers/subsystem/networks/id_access.dm:132
    public virtual void setup_tgui_lists()
    {
        foreach (object region in Dm.each(accesses_by_region))
        {
            DmList region_access = accesses_by_region[region] as DmList;

            DmList parsed_accesses = Dm.list();

            foreach (object access in Dm.each(region_access))
            {
                object access_desc = get_access_desc(access);
                if (!Dm.truthy(access_desc))
                    continue;

                Dm.append(ref parsed_accesses, Dm.list(Dm.list(
                    Dm.kv("desc", replacetext(access_desc, "&nbsp", " ")),
                    Dm.kv("ref", access)
                )));
            }

            all_region_access_tgui[region] = Dm.list(Dm.list(
                Dm.kv("name", region),
                Dm.kv("accesses", parsed_accesses)
            ));
        }

        sub_department_managers_tgui = Dm.list(
            Dm.kv(Dm.text("[]", ACCESS_CAPTAIN), Dm.list(
                Dm.kv("regions", Dm.list(REGION_COMMAND)),
                Dm.kv("head", JOB_CAPTAIN),
                Dm.kv("templates", Dm.list()),
                Dm.kv("pdas", Dm.list())
            )),
            Dm.kv(Dm.text("[]", ACCESS_HOP), Dm.list(
                Dm.kv("regions", Dm.list(REGION_GENERAL)),
                Dm.kv("head", JOB_HEAD_OF_PERSONNEL),
                Dm.kv("templates", Dm.list()),
                Dm.kv("pdas", Dm.list())
            )),
            Dm.kv(Dm.text("[]", ACCESS_HOS), Dm.list(
                Dm.kv("regions", Dm.list(REGION_SECURITY)),
                Dm.kv("head", JOB_HEAD_OF_SECURITY),
                Dm.kv("templates", Dm.list()),
                Dm.kv("pdas", Dm.list())
            )),
            Dm.kv(Dm.text("[]", ACCESS_CMO), Dm.list(
                Dm.kv("regions", Dm.list(REGION_MEDBAY)),
                Dm.kv("head", JOB_CHIEF_MEDICAL_OFFICER),
                Dm.kv("templates", Dm.list()),
                Dm.kv("pdas", Dm.list())
            )),
            Dm.kv(Dm.text("[]", ACCESS_RD), Dm.list(
                Dm.kv("regions", Dm.list(REGION_RESEARCH)),
                Dm.kv("head", JOB_RESEARCH_DIRECTOR),
                Dm.kv("templates", Dm.list()),
                Dm.kv("pdas", Dm.list())
            )),
            Dm.kv(Dm.text("[]", ACCESS_CE), Dm.list(
                Dm.kv("regions", Dm.list(REGION_ENGINEERING)),
                Dm.kv("head", JOB_CHIEF_ENGINEER),
                Dm.kv("templates", Dm.list()),
                Dm.kv("pdas", Dm.list())
            )),
            Dm.kv(Dm.text("[]", ACCESS_QM), Dm.list(
                Dm.kv("regions", Dm.list(REGION_SUPPLY)),
                Dm.kv("head", JOB_QUARTERMASTER),
                Dm.kv("templates", Dm.list()),
                Dm.kv("pdas", Dm.list())
            ))
        );

        DmList station_job_trims = subtypesof(typeof(datum_id_trim_job));
        foreach (object trim_path in Dm.each(station_job_trims))
        {
            datum_id_trim_job trim = trim_singletons_by_path[trim_path] as datum_id_trim_job;
            if (!Dm.truthy(length(trim.template_access)))
                continue;

            station_job_templates[trim_path] = trim.assignment;
            foreach (object access in Dm.each(trim.template_access))
            {
                DmList manager = sub_department_managers_tgui[Dm.text("[]", access)] as DmList;
                if (!Dm.truthy(manager))
                {
                    if (!Dm.eq(access, ACCESS_CHANGE_IDS))
                    {
                        // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
                        warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Invalid template access access \\[[]\\] registered with []. Template added to global list anyway.", access, trim_path), "code/controllers/subsystem/networks/id_access.dm", 203, this, usr));
                    }
                    continue;
                }
                DmList templates = manager["templates"] as DmList;
                templates[trim_path] = trim.assignment;
            }
        }

        DmList centcom_job_trims = Dm.sub(typesof(typeof(datum_id_trim_centcom)), typesof(typeof(datum_id_trim_centcom_corpse))) as DmList;
        foreach (object trim_path in Dm.each(centcom_job_trims))
        {
            datum_id_trim trim = trim_singletons_by_path[trim_path] as datum_id_trim;
            centcom_job_templates[trim_path] = trim.assignment;
        }

        foreach (Type pda_path in Dm.each(subtypesof(typeof(obj_item_modular_computer_pda_crew))))
        {
            if (Dm.initial(pda_path, (obj_item_modular_computer_pda_crew p) => p.painting_region) == null)
                continue;

            foreach (object access_txt in Dm.each(sub_department_managers_tgui))
            {
                DmList manager_info = sub_department_managers_tgui[access_txt] as DmList;
                DmList manager_regions = manager_info["regions"] as DmList;
                foreach (object whitelisted_region in Dm.each(manager_regions))
                {
                    if (!Dm.eq(Dm.initial(pda_path, (obj_item_modular_computer_pda_crew p) => p.painting_region), whitelisted_region))
                        continue;
                    DmList manager_pdas = manager_info["pdas"] as DmList;
                    manager_pdas[pda_path] = Dm.initial(pda_path, (obj_item_modular_computer_pda_crew p) => p.name);
                    station_pda_templates[pda_path] = Dm.initial(pda_path, (obj_item_modular_computer_pda_crew p) => p.name);
                }
            }
        }
    }

    // tg: code/controllers/subsystem/networks/id_access.dm:235
    public virtual void setup_wildcard_dict()
    {
        wildcard_flags_by_wildcard[WILDCARD_NAME_ALL] = WILDCARD_FLAG_ALL;
        wildcard_flags_by_wildcard[WILDCARD_NAME_COMMON] = WILDCARD_FLAG_COMMON;
        wildcard_flags_by_wildcard[WILDCARD_NAME_COMMAND] = WILDCARD_FLAG_COMMAND;
        wildcard_flags_by_wildcard[WILDCARD_NAME_PRV_COMMAND] = WILDCARD_FLAG_PRV_COMMAND;
        wildcard_flags_by_wildcard[WILDCARD_NAME_CAPTAIN] = WILDCARD_FLAG_CAPTAIN;
        wildcard_flags_by_wildcard[WILDCARD_NAME_CENTCOM] = WILDCARD_FLAG_CENTCOM;
        wildcard_flags_by_wildcard[WILDCARD_NAME_SYNDICATE] = WILDCARD_FLAG_SYNDICATE;
        wildcard_flags_by_wildcard[WILDCARD_NAME_AWAY] = WILDCARD_FLAG_AWAY;
        wildcard_flags_by_wildcard[WILDCARD_NAME_SPECIAL] = WILDCARD_FLAG_SPECIAL;
        wildcard_flags_by_wildcard[WILDCARD_NAME_FORCED] = WILDCARD_FLAG_FORCED;
    }

    /// Setup dictionary that converts access levels to text descriptions.
    // tg: code/controllers/subsystem/networks/id_access.dm:248
    public virtual void setup_access_descriptions()
    {
        desc_by_access[ACCESS_CARGO] = "Cargo Bay";
        desc_by_access[ACCESS_SECURITY] = "Security";
        desc_by_access[ACCESS_BRIG] = "Holding Cells";
        desc_by_access[ACCESS_COURT] = "Courtroom";
        desc_by_access[ACCESS_DETECTIVE] = "Detective Office";
        desc_by_access[ACCESS_MEDICAL] = "Medical";
        desc_by_access[ACCESS_GENETICS] = "Genetics Lab";
        desc_by_access[ACCESS_MORGUE] = "Morgue";
        desc_by_access[ACCESS_MORGUE_SECURE] = "Coroner";
        desc_by_access[ACCESS_SCIENCE] = "R&D Lab";
        desc_by_access[ACCESS_ORDNANCE] = "Ordnance Lab";
        desc_by_access[ACCESS_ORDNANCE_STORAGE] = "Ordnance Storage";
        desc_by_access[ACCESS_PLUMBING] = "Chemistry Lab";
        desc_by_access[ACCESS_RD] = "RD Office";
        desc_by_access[ACCESS_BAR] = "Bar";
        desc_by_access[ACCESS_JANITOR] = "Custodial Closet";
        desc_by_access[ACCESS_ENGINEERING] = "Engineering";
        desc_by_access[ACCESS_ENGINE_EQUIP] = "Power and Engineering Equipment";
        desc_by_access[ACCESS_MAINT_TUNNELS] = "Maintenance";
        desc_by_access[ACCESS_EXTERNAL_AIRLOCKS] = "External Airlocks";
        desc_by_access[ACCESS_CHANGE_IDS] = "ID Console";
        desc_by_access[ACCESS_AI_UPLOAD] = "AI Chambers";
        desc_by_access[ACCESS_TELEPORTER] = "Teleporter";
        desc_by_access[ACCESS_EVA] = "EVA";
        desc_by_access[ACCESS_BUDGET] = "Department Budget";
        desc_by_access[ACCESS_COMMAND] = "Command";
        desc_by_access[ACCESS_CAPTAIN] = "Captain";
        desc_by_access[ACCESS_ALL_PERSONAL_LOCKERS] = "Personal Lockers";
        desc_by_access[ACCESS_CHAPEL_OFFICE] = "Chapel Office";
        desc_by_access[ACCESS_TECH_STORAGE] = "Technical Storage";
        desc_by_access[ACCESS_ATMOSPHERICS] = "Atmospherics";
        desc_by_access[ACCESS_CREMATORIUM] = "Crematorium";
        desc_by_access[ACCESS_ARMORY] = "Armory";
        desc_by_access[ACCESS_CONSTRUCTION] = "Construction";
        desc_by_access[ACCESS_KITCHEN] = "Kitchen";
        desc_by_access[ACCESS_HYDROPONICS] = "Hydroponics";
        desc_by_access[ACCESS_LIBRARY] = "Library";
        desc_by_access[ACCESS_LAWYER] = "Law Office";
        desc_by_access[ACCESS_ROBOTICS] = "Robotics";
        desc_by_access[ACCESS_VIROLOGY] = "Virology";
        desc_by_access[ACCESS_PSYCHOLOGY] = "Psychology";
        desc_by_access[ACCESS_CMO] = "CMO Office";
        desc_by_access[ACCESS_PARAMEDIC] = "Paramedic Office";
        desc_by_access[ACCESS_QM] = "QM Office";
        desc_by_access[ACCESS_SURGERY] = "Surgery";
        desc_by_access[ACCESS_THEATRE] = "Theatre";
        desc_by_access[ACCESS_RESEARCH] = "Science";
        desc_by_access[ACCESS_MINING] = "Mining Dock";
        desc_by_access[ACCESS_SHIPPING] = "Cargo Shipping";
        desc_by_access[ACCESS_VAULT] = "Main Vault";
        desc_by_access[ACCESS_MINING_STATION] = "Mining Outpost";
        desc_by_access[ACCESS_XENOBIOLOGY] = "Xenobiology Lab";
        desc_by_access[ACCESS_HOP] = "HoP Office";
        desc_by_access[ACCESS_HOS] = "HoS Office";
        desc_by_access[ACCESS_CE] = "CE Office";
        desc_by_access[ACCESS_PHARMACY] = "Pharmacy";
        desc_by_access[ACCESS_RC_ANNOUNCE] = "RC Announcements";
        desc_by_access[ACCESS_KEYCARD_AUTH] = "Keycode Auth.";
        desc_by_access[ACCESS_TCOMMS] = "Telecommunications";
        desc_by_access[ACCESS_GATEWAY] = "Gateway";
        desc_by_access[ACCESS_BRIG_ENTRANCE] = "Brig";
        desc_by_access[ACCESS_MINERAL_STOREROOM] = "Mineral Storage";
        desc_by_access[ACCESS_MINISAT] = "AI Satellite";
        desc_by_access[ACCESS_WEAPONS] = "Weapon Permit";
        desc_by_access[ACCESS_NETWORK] = "Network Access";
        desc_by_access[ACCESS_MECH_MINING] = "Mining Mech Access";
        desc_by_access[ACCESS_MECH_MEDICAL] = "Medical Mech Access";
        desc_by_access[ACCESS_MECH_SECURITY] = "Security Mech Access";
        desc_by_access[ACCESS_MECH_SCIENCE] = "Science Mech Access";
        desc_by_access[ACCESS_MECH_ENGINE] = "Engineering Mech Access";
        desc_by_access[ACCESS_AUX_BASE] = "Auxiliary Base";
        desc_by_access[ACCESS_SERVICE] = "Service Hallway";
        desc_by_access[ACCESS_CENT_GENERAL] = "CentCom General Access";
        desc_by_access[ACCESS_CENT_THUNDER] = "CentCom Thunderdome";
        desc_by_access[ACCESS_CENT_STORAGE] = "CentCom Storage";
        desc_by_access[ACCESS_CENT_LIVING] = "CentCom Living Quarters";
        desc_by_access[ACCESS_CENT_MEDICAL] = "CentCom Medical";
        desc_by_access[ACCESS_CENT_TELEPORTER] = "CentCom Teleporter";
        desc_by_access[ACCESS_CENT_SPECOPS] = "CentCom SpecOps";
        desc_by_access[ACCESS_CENT_CAPTAIN] = "CentCom Captain";
        desc_by_access[ACCESS_CENT_BAR] = "CentCom Bar";
        desc_by_access[ACCESS_CENT_OFFICER] = "CentCom Officer";
    }

    /**
     * Returns the access bitflags associated with any given access level.
     *
     * In proc form due to accesses being stored in the list as text instead of numbers.
     * Arguments:
     * * access - Access as either pure number or as a string representation of the number.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:340
    public virtual object get_access_flag(object access = null)
    {
        object flag = flags_by_access[Dm.text("[]", access)];
        return flag;
    }

    /**
     * Returns the access description associated with any given access level.
     *
     * In proc form due to accesses being stored in the list as text instead of numbers.
     * Arguments:
     * * access - Access as either pure number or as a string representation of the number.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:351
    public virtual object get_access_desc(object access = null)
    {
        return desc_by_access[Dm.text("[]", access)];
    }

    /**
     * Builds and returns a list of accesses from a list of regions.
     *
     * Arguments:
     * * regions - A list of region defines.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:360
    public virtual object get_region_access_list(DmList regions = null)
    {
        if (!Dm.truthy(length(regions)))
            return null;

        DmList built_region_list = Dm.list();

        foreach (object region in Dm.each(regions))
            Dm.combine(ref built_region_list, accesses_by_region[region]);

        return built_region_list;
    }

    /**
     * Returns the list of all accesses associated with any given access flag.
     *
     * In proc form due to accesses being stored in the list as text instead of numbers.
     * Arguments:
     * * flag - The flag to get access for as either a pure number of string representation of the flag.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:378
    public virtual object get_flag_access_list(object flag = null)
    {
        return accesses_by_flag[Dm.text("[]", flag)];
    }

    /**
     * Applies a trim singleton to a card.
     *
     * Returns FALSE if the trim could not be applied due to being incompatible with the card.
     * Incompatibility is defined as a card not being able to hold all the trim's required wildcards.
     * Returns TRUE otherwise.
     * Arguments:
     * * id_card - ID card to apply the trim_path to.
     * * trim_path - A trim path to apply to the card. Grabs the trim's associated singleton and applies it.
     * * copy_access - Boolean value. If true, the trim's access is also copied to the card.
     */
    /**
     * Allows the ID card to temporarily override the appearance of the trim.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:445
    public virtual void apply_trim_override(obj_item_card_id_advanced id_card = null, object trim_path = null, float? check_forged = null)
    {
        check_forged ??= TRUE;
        datum_id_trim trim = trim_singletons_by_path[trim_path] as datum_id_trim;
        id_card.trim_icon_override = trim.trim_icon;
        id_card.trim_state_override = trim.trim_state;
        id_card.trim_assignment_override = trim.assignment;
        id_card.sechud_icon_state_override = trim.sechud_icon_state;
        id_card.department_color_override = trim.department_color;
        id_card.department_state_override = trim.department_state;
        id_card.subdepartment_color_override = trim.subdepartment_color;
        id_card.big_pointer = trim.big_pointer;
        id_card.pointer_color = trim.pointer_color;

        if (id_card is obj_item_card_id_advanced_chameleon cham_id && (!Dm.truthy(check_forged) || !Dm.truthy(cham_id.forged)))
            cham_id.assignment = trim.assignment;

        if (ishuman(id_card.loc))
            (id_card.loc as mob_living_carbon_human).update_ID_card();

        // Apply any currently active RETA grants to this card with updated trim override
        apply_active_reta_grants_to_card(id_card);
    }

    /**
     * Removes a trim override from an ID card.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:474
    public virtual void remove_trim_override(obj_item_card_id_advanced id_card = null)
    {
        id_card.trim_icon_override = null;
        id_card.trim_state_override = null;
        id_card.trim_assignment_override = null;
        id_card.sechud_icon_state_override = null;
        id_card.department_color_override = null;
        id_card.department_state_override = null;
        id_card.subdepartment_color_override = null;
        // a card with no trim is a runtime in tg
        if (id_card.trim is datum_id_trim own_trim)
        {
            id_card.big_pointer = own_trim.big_pointer;
            id_card.pointer_color = own_trim.pointer_color;
        }

        if (ishuman(id_card.loc))
            (id_card.loc as mob_living_carbon_human).update_ID_card();
    }

    // tg: code/controllers/subsystem/networks/id_access.dm:392
    public virtual async DmTask<object> apply_trim_to_card(obj_item_card_id id_card = null, object trim_path = null, float? copy_access = null)
    {
        copy_access ??= TRUE;
        datum_id_trim trim = trim_singletons_by_path[trim_path] as datum_id_trim;

        if (!Dm.truthy(id_card.can_add_wildcards(trim.wildcard_access)))
            return FALSE;

        id_card.clear_access();
        id_card.trim = trim;
        id_card.big_pointer = trim.big_pointer;
        id_card.pointer_color = trim.pointer_color;

        if (Dm.truthy(copy_access))
        {
            id_card.access = trim.access.Copy();
            id_card.add_wildcards(trim.wildcard_access);
        }

        if (Dm.truthy(trim.assignment))
            id_card.assignment = trim.assignment;

        datum_job trim_job = trim.find_job() as datum_job;
        if (id_card.registered_account != null)
        {
            datum_job old_job = id_card.registered_account.account_job;
            id_card.registered_account.account_job = trim_job;
            id_card.registered_account.update_account_job_lists(trim_job, old_job);
        }

        id_card.update_label();
        await id_card.update_icon();

        // Apply any currently active RETA grants to this newly trimmed card
        apply_active_reta_grants_to_card(id_card);

        return TRUE;
    }

    /**
     * Removes a trim from an ID card. Also removes all accesses from it too.
     *
     * Arguments:
     * * id_card - The ID card to remove the trim from.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:431
    public virtual async DmTask remove_trim_from_card(obj_item_card_id id_card = null)
    {
        id_card.trim = null;
        id_card.clear_access();
        id_card.update_label();
        await id_card.update_icon();
    }

    /**
     * Adds the accesses associated with a trim to an ID card.
     *
     * Clears the card's existing access levels first.
     * Primarily intended for applying trim templates to cards. Will attempt to add as many ordinary access
     * levels as it can, without consuming any wildcards. Will then attempt to apply the trim-specific wildcards after.
     *
     * Arguments:
     * * id_card - The ID card to remove the trim from.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:499
    public virtual void add_trim_access_to_card(obj_item_card_id id_card = null, object trim_path = null)
    {
        datum_id_trim trim = trim_singletons_by_path[trim_path] as datum_id_trim;

        id_card.clear_access();

        id_card.add_access(trim.access, mode: TRY_ADD_ALL_NO_WILDCARD);
        id_card.add_wildcards(trim.wildcard_access, mode: TRY_ADD_ALL);
        if (trim is datum_id_trim_job)
        {
            var job_trim = (datum_id_trim_job)trim; // Here is where we update a player's paycheck department for the purposes of discounts/paychecks.
            id_card.registered_account.account_job = job_trim.job as datum_job;
        }

        // Apply any currently active RETA grants to this card with updated trim access
        apply_active_reta_grants_to_card(id_card);
    }

    /**
     * Tallies up all accesses the card has that have flags greater than or equal to the access_flag supplied.
     *
     * Returns the number of accesses that have flags matching access_flag or a higher tier access.
     * Arguments:
     * * id_card - The ID card to tally up access for.
     * * access_flag - The minimum access flag required for an access to be tallied up.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:521
    public virtual object tally_access(obj_item_card_id id_card = null, float? access_flag = null)
    {
        access_flag ??= NONE;
        float tally = 0;

        DmList id_card_access = id_card.access;
        foreach (object access in Dm.each(id_card_access))
        {
            if (Dm.ge(flags_by_access[Dm.text("[]", access)], access_flag))
                tally++;
        }

        return tally;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/networks/id_access.dm:4  SUBSYSTEM_DEF(id_access): GLOBAL_REAL(SSid_access, /datum/controller/subsystem/id_access)
    internal datum_controller_subsystem_id_access SSid_access;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_id_access SSid_access => ref TgState.Current.SSid_access;
}

// Lavaland L2a (D304): the in-character record of an ID (ID_DATA), which the material container, the remote materials and the
// ore silo pass along for their log and their access checks.
public partial class datum_controller_subsystem_id_access
{
    /**
     * Helper proc for creating a copy of the in-character information you could render from scanning for an ID card.
     * Accounts for chameleon cards, silicons, and ID read failures.
     * Pertinently, it also returns relevant information for their bank account (if they have one).
     * To return bank account info for a chameleon card, bypass_chameleon must be set to TRUE. Otherwise it returns
     * a bogey record.
     */
    // tg: code/controllers/subsystem/networks/id_access.dm:539
    public virtual object __in_character_record_id_information(atom_movable target_of_record = null, float? bypass_chameleon = null)
    {
        bypass_chameleon ??= FALSE;
        DmList returned_record = Dm.alist(
            Dm.kv("name", null),
            Dm.kv("age", null),
            Dm.kv("assignment", null),
            Dm.kv("account_id", null),
            Dm.kv("account_holder", null),
            Dm.kv("account_assignment", null),
            Dm.kv("accesses", null));
        DmList dot = returned_record;
        if (target_of_record == null)
        {
            dot["name"] = ID_READ_FAILURE;
            dot["age"] = ID_READ_FAILURE;
            dot["assignment"] = ID_READ_FAILURE;
            dot["account_id"] = ID_READ_FAILURE;
            dot["account_holder"] = ID_READ_FAILURE;
            dot["account_assignment"] = ID_READ_FAILURE;
            dot["accesses"] = ID_READ_FAILURE;
            dot[ID_READ_FAILURE] = ID_READ_FAILURE;
            return dot;
        }
        mob_living target = target_of_record as mob_living;
        if (target != null)
        {
            if (!issilicon(target) && !isdrone(target))
                return __in_character_record_id_information(target.get_idcard() as obj_item_card_id_advanced);
            dot["name"] = target.name;
            dot["age"] = 0;
            dot["assignment"] = "Silicon";
            dot["account_id"] = null;
            dot["account_holder"] = null;
            dot["account_assignment"] = null;
            dot["accesses"] = null;
            dot[SILICON_OVERRIDE] = SILICON_OVERRIDE;
            return dot;
        }
        obj_item_card_id_advanced id_card = target_of_record as obj_item_card_id_advanced;
        if (id_card != null)
        {
            dot["name"] = Dm.truthy(id_card.registered_name) ? id_card.registered_name : "Unknown";
            dot["age"] = Dm.truthy(id_card.registered_age) ? id_card.registered_age : "Unknown";
            dot["assignment"] = Dm.truthy(id_card.assignment) ? id_card.assignment : "Unassigned";
            dot["accesses"] = id_card.access;
            datum_bank_account id_account = id_card.registered_account;
            if (istype(id_card, typeof(obj_item_card_id_advanced_chameleon)) && !Dm.truthy(bypass_chameleon))
            {
                // Generate a bogey record based only on the ID card
                // Generates a random bank account number every time as a 'spot the thread' for anyone who
                // went through records for this entry for whatever reason.
                dot["account_id"] = rand(111111, 999999);
                dot["account_holder"] = dot["name"];
                dot["account_assignment"] = dot["assignment"];
                dot[CHAMELEON_OVERRIDE] = CHAMELEON_OVERRIDE;
                return dot;
            }
            if (id_account == null)
            {
                dot["account_id"] = 0;
                dot["account_holder"] = "NO ACCOUNT.";
                dot["account_assignment"] = "NO ACCOUNT.";
                return dot;
            }
            dot["account_id"] = id_account.account_id;
            dot["account_holder"] = id_account.account_holder;
            dot["account_assignment"] = Dm.truthy(id_account.account_job?.title) ? id_account.account_job.title : "Unassigned";
            return dot;
        }
        else
            return ID_DATA(null);
    }
}
