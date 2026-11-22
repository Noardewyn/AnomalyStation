// Translated from tgstation (snapshot 1d99bd7f): code/modules/reta/reta_system.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G3 (D291): reta_get_user_department_by_name, which the request consoles' init and teardown call (their RETA registration).
// Gaps SC1: the rest of the file. RETA, the Request Emergency Temporary Access: a requests console's emergency call (Security,
// Engineering, Medical) grants the called department's crew, by their ID trims, the caller department's door access for five
// minutes (config/reta.txt), lights the doors that grant it, gives a newly made card of that department the active grants, and
// keeps a cooldown per pair of departments and a record of the calls for the admins' multiple emergency warning.

// tg: code/modules/reta/reta_system.dm:195
public static partial class GlobalProcs
{
    /// Gets the standardized department string from a console department name
    // tg: code/modules/reta/reta_system.dm:195
    public static object reta_get_user_department_by_name(object dept_name = null)
    {
        if (!Dm.truthy(dept_name))
            return null;

        string dept_lower = LOWER_TEXT(dept_name);

        // Check for partial matches to handle variations in console naming
        if (Dm.truthy(findtext(dept_lower, "engineering")) || Dm.truthy(findtext(dept_lower, "engine")))
            return "Engineering";
        if (Dm.truthy(findtext(dept_lower, "science")) || Dm.truthy(findtext(dept_lower, "research")))
            return "Science";
        if (Dm.truthy(findtext(dept_lower, "cargo")) || Dm.truthy(findtext(dept_lower, "supply")))
            return "Cargo";
        if (Dm.truthy(findtext(dept_lower, "mining")) || Dm.truthy(findtext(dept_lower, "mine")))
            return "Mining";
        if (Dm.truthy(findtext(dept_lower, "service")) || Dm.truthy(findtext(dept_lower, "civilian")))
            return "Service";
        if (Dm.truthy(findtext(dept_lower, "medical")) || Dm.truthy(findtext(dept_lower, "medbay")))
            return "Medical";
        if (Dm.truthy(findtext(dept_lower, "security")) || Dm.truthy(findtext(dept_lower, "sec")))
            return "Security";
        if (Dm.truthy(findtext(dept_lower, "command")) || Dm.truthy(findtext(dept_lower, "bridge")))
            return "Command";

        // Autonamed areas that belong to service department
        if (Dm.truthy(findtext(dept_lower, "kitchen")) || Dm.truthy(findtext(dept_lower, "bar")) || Dm.truthy(findtext(dept_lower, "cafeteria")) || Dm.truthy(findtext(dept_lower, "diner")))
            return "Service";
        if (Dm.truthy(findtext(dept_lower, "hydroponics")) || Dm.truthy(findtext(dept_lower, "botany")))
            return "Service";
        if (Dm.truthy(findtext(dept_lower, "janitor")) || Dm.truthy(findtext(dept_lower, "custodial")))
            return "Service";

        // Autonamed areas for medical
        if (Dm.truthy(findtext(dept_lower, "pharmacy")) || Dm.truthy(findtext(dept_lower, "chemistry")) || Dm.truthy(findtext(dept_lower, "chem")))
            return "Medical";
        if (Dm.truthy(findtext(dept_lower, "morgue")) || Dm.truthy(findtext(dept_lower, "virology")))
            return "Medical";
        if (Dm.truthy(findtext(dept_lower, "surgery")) || Dm.truthy(findtext(dept_lower, "operating")) || Dm.truthy(findtext(dept_lower, "cryo")))
            return "Medical";
        if (Dm.truthy(findtext(dept_lower, "patients")) || Dm.truthy(findtext(dept_lower, "exam")))
            return "Medical";

        // Autonamed areas for engineering
        if (Dm.truthy(findtext(dept_lower, "atmospherics")) || Dm.truthy(findtext(dept_lower, "atmos")))
            return "Engineering";
        if (Dm.truthy(findtext(dept_lower, "supermatter")) || Dm.truthy(findtext(dept_lower, "engine")))
            return "Engineering";
        if (Dm.truthy(findtext(dept_lower, "gravity")) || Dm.truthy(findtext(dept_lower, "telecomm")) || Dm.truthy(findtext(dept_lower, "tcomm")))
            return "Engineering";

        // Autonamed areas for science department
        if (Dm.truthy(findtext(dept_lower, "xenobiology")) || Dm.truthy(findtext(dept_lower, "xenobio")))
            return "Science";
        if (Dm.truthy(findtext(dept_lower, "robotics")) || Dm.truthy(findtext(dept_lower, "genetics")))
            return "Science";
        if (Dm.truthy(findtext(dept_lower, "ordnance")) || Dm.truthy(findtext(dept_lower, "cytology")))
            return "Science";

        // Handle specific autonamed areas that belong to security department
        if (Dm.truthy(findtext(dept_lower, "brig")) || Dm.truthy(findtext(dept_lower, "holding")))
            return "Security";
        if (Dm.truthy(findtext(dept_lower, "armory")) || Dm.truthy(findtext(dept_lower, "checkpoint")))
            return "Security";

        return null;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/reta/reta_system.dm:286-287 (file-local, #undef at :318-319)
    internal const float RETA_DEFAULT_DURATION_DS = 3000; // 5 minutes
    internal const float RETA_DEFAULT_COOLDOWN_DS = 150; // 15 seconds

    /// Helper function for RETA-specific logging
    // tg: code/modules/reta/reta_system.dm:7
    public static void log_reta(object text)
    {
        WRITE_LOG(GLOB.reta_log, Dm.text("[] RETA: []", server_timestamp(), text));
        log_game(Dm.text("RETA: []", text));
    }

    // tg: code/modules/reta/reta_system.dm:11
    public static void initialize_reta_system()
    {
        // Define which access flags are granted for each department
        GLOB.reta_dept_grants = Dm.list(
            Dm.kv("Medical", Dm.list(ACCESS_MEDICAL, ACCESS_SURGERY)),
            Dm.kv("Security", Dm.list(ACCESS_SECURITY, ACCESS_BRIG, ACCESS_BRIG_ENTRANCE)),
            Dm.kv("Engineering", Dm.list(ACCESS_ENGINEERING, ACCESS_ATMOSPHERICS)),
            Dm.kv("Science", Dm.list(ACCESS_SCIENCE, ACCESS_RESEARCH)),
            Dm.kv("Service", Dm.list(ACCESS_SERVICE, ACCESS_BAR, ACCESS_KITCHEN, ACCESS_HYDROPONICS)),
            Dm.kv("Cargo", Dm.list(ACCESS_CARGO)),
            Dm.kv("Mining", Dm.list(ACCESS_MINING, ACCESS_MINING_STATION, ACCESS_CARGO)),
            Dm.kv("Command", Dm.list(ACCESS_COMMAND))); // Admin-only, not available through request consoles
    }

    /// Checks if an origin department is on cooldown for calling a target department
    // tg: code/modules/reta/reta_system.dm:25
    public static bool reta_on_cooldown(object origin, object target)
    {
        DmList by_target = GLOB.reta_cooldown[origin] as DmList;
        if (by_target == null)
            return false;
        float next_ok = Dm.truthy(by_target[target]) ? Dm.num(by_target[target]) : 0;
        return world.time < next_ok;
    }

    /// Sets a cooldown for an origin department calling a target department
    // tg: code/modules/reta/reta_system.dm:33
    public static void reta_set_cooldown(object origin, object target, object cd_ds)
    {
        if (!Dm.truthy(GLOB.reta_cooldown[origin]))
            GLOB.reta_cooldown[origin] = Dm.list();
        ((DmList)GLOB.reta_cooldown[origin])[target] = world.time + Dm.num(cd_ds);
    }

    /// Tracks recent emergency calls for multiple department analysis
    // tg: code/modules/reta/reta_system.dm:39
    public static void reta_track_call(object origin, object target)
    {
        DmList call_info = Dm.list(
            Dm.kv("time", world.time),
            Dm.kv("origin", origin));

        if (!Dm.truthy(GLOB.reta_recent_calls[target]))
            GLOB.reta_recent_calls[target] = Dm.list();
        Dm.append_at(GLOB.reta_recent_calls, target, Dm.list(call_info));

        // Clean old calls (older than 10 minutes)
        float cutoff_time = world.time - 6000; // 10 minutes
        if (Dm.truthy(GLOB.reta_recent_calls[target]))
        {
            DmList recent_calls = (DmList)GLOB.reta_recent_calls[target];
            GLOB.reta_recent_calls[target] = recent_calls.Copy();
            foreach (DmList old_call in Dm.each<DmList>((GLOB.reta_recent_calls[target] as DmList)?.Copy()))
            {
                if (Dm.num(old_call["time"]) < cutoff_time)
                {
                    DmList calls = (DmList)GLOB.reta_recent_calls[target];
                    Dm.remove(ref calls, Dm.list(old_call));
                    GLOB.reta_recent_calls[target] = calls;
                }
            }
        }

        // Check for multiple calls scenario
        if (LAZYLEN(GLOB.reta_recent_calls[target] as DmList) >= 3)
        {
            DmList origins = Dm.list();
            foreach (DmList call_data in Dm.each<DmList>(GLOB.reta_recent_calls[target]))
                Dm.combine(ref origins, call_data["origin"]);

            if (LAZYLEN(origins) >= 3)
            {
                message_admins(Dm.text("RETA: Multiple emergency scenario detected! [] has been called by [] in the last 10 minutes. Consider station-wide emergency protocols.", target, english_list(origins)));
                log_game(Dm.text("RETA: Multiple department emergency - [] called by []", target, english_list(origins)));
            }
        }
    }

    /// Finds eligible responders and grants them temporary access
    // tg: code/modules/reta/reta_system.dm:69
    public static object reta_find_and_grant_access(object target_dept, object origin_dept, object duration_ds)
    {
        object dot = 0;

        DmList job_trims = GLOB.reta_job_trims[target_dept] as DmList;
        if (LAZYLEN(job_trims) == 0)
        {
            log_reta(Dm.text("No job trims defined for department '[]'", target_dept));
            return FALSE;
        }

        float granted_count = 0;
        float total_players_checked = 0;
        float matching_trim_players = 0;

        // Check ID cards being carried by living players (fast and efficient)
        foreach (mob_living_carbon_human human_player in Dm.each<mob_living_carbon_human>(GLOB.human_list))
        {
            // Only check players who are alive and have clients (actively playing)
            if (human_player.client == null || human_player.stat == DEAD)
                continue;

            total_players_checked++;

            // Get their ID card (worn_id, hands, or belt)
            var id_card = human_player.get_idcard(hand_first: FALSE) as obj_item_card_id;
            if (id_card == null || !Dm.truthy(id_card.trim))
                continue;

            // Check if this card's trim matches the target department
            if (!Dm.truthy(is_type_in_list(id_card.trim as datum, job_trims)))
                continue;

            matching_trim_players++;

            if (Dm.truthy(id_card.grant_reta_access(origin_dept, duration_ds)))
                granted_count++;
        }

        if (granted_count > 0)
        {
            // Register this as an active RETA grant for new cards
            if (!Dm.truthy(GLOB.reta_active_grants[target_dept]))
                GLOB.reta_active_grants[target_dept] = Dm.list();
            ((DmList)GLOB.reta_active_grants[target_dept])[origin_dept] = world.time + Dm.num(duration_ds);

            // Set up automatic cleanup when the grant expires
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, nameof(cleanup_expired_reta_grant), target_dept, origin_dept), Dm.num(duration_ds), file: "code/modules/reta/reta_system.dm", line: 116);

            // Update all doors to show RETA lights for newly activated grants
            update_all_doors_reta_lights();

            log_reta(Dm.text("Granted temporary [] access to [] [] department ID cards from a call by [].", origin_dept, granted_count, target_dept, origin_dept));
            dot = TRUE;
        }
        else
            log_reta(Dm.text("No [] personnel found who needed [] access from a call by []. (Checked [] living players, [] had matching trims)", target_dept, origin_dept, origin_dept, total_players_checked, matching_trim_players));

        // tg returns . (FALSE for none): the caller counts the grants as granted_count, a boolean here as in tg
        return dot;
    }

    /// Populates the job trims list for RETA system
    // tg: code/modules/reta/reta_system.dm:131
    public static void populate_reta_job_trims()
    {
        GLOB.reta_job_trims = Dm.list(
            Dm.kv("Medical", Dm.list()),
            Dm.kv("Security", Dm.list()),
            Dm.kv("Engineering", Dm.list()),
            Dm.kv("Science", Dm.list()),
            Dm.kv("Service", Dm.list()),
            Dm.kv("Command", Dm.list()),
            Dm.kv("Cargo", Dm.list()),
            Dm.kv("Mining", Dm.list()));

        log_game("RETA: Starting job trim population...");
        float total_trims = 0;
        foreach (Type job_trim_path in Dm.each<Type>(subtypesof(typeof(datum_id_trim_job))))
        {
            var trim = Dm.@new(job_trim_path) as datum_id_trim_job;
            total_trims++;

            // job is the typepath until New swaps it for SSjob's singleton (jobs.cs); not a job datum means none
            var trim_job = trim.job as datum_job;
            if (trim_job == null)
            {
                log_game(Dm.text("RETA: Trim [] has no job", job_trim_path));
                continue;
            }
            if (trim_job.departments_bitflags == 0)
            {
                log_game(Dm.text("RETA: Trim [] job [] has no departments_bitflags", job_trim_path, trim_job));
                continue;
            }

            float flags = trim_job.departments_bitflags;
            if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_MEDICAL)))
                Dm.append_at(GLOB.reta_job_trims, "Medical", job_trim_path);
            if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_SECURITY)))
                Dm.append_at(GLOB.reta_job_trims, "Security", job_trim_path);
            if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_ENGINEERING)))
                Dm.append_at(GLOB.reta_job_trims, "Engineering", job_trim_path);
            if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_SCIENCE)))
                Dm.append_at(GLOB.reta_job_trims, "Science", job_trim_path);
            if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_SERVICE)))
                Dm.append_at(GLOB.reta_job_trims, "Service", job_trim_path);
            if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_COMMAND)))
                Dm.append_at(GLOB.reta_job_trims, "Command", job_trim_path);
            if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_CARGO)))
            {
                Dm.append_at(GLOB.reta_job_trims, "Cargo", job_trim_path);
                Dm.append_at(GLOB.reta_job_trims, "Mining", job_trim_path); // Mining uses CARGO bitflag
            }
        }

        log_game(Dm.text("RETA: Processed [] trims. Final counts: Medical=[], Security=[], Engineering=[], Science=[], Service=[], Command=[], Cargo=[], Mining=[]",
            total_trims,
            LAZYLEN(GLOB.reta_job_trims["Medical"] as DmList), LAZYLEN(GLOB.reta_job_trims["Security"] as DmList),
            LAZYLEN(GLOB.reta_job_trims["Engineering"] as DmList), LAZYLEN(GLOB.reta_job_trims["Science"] as DmList),
            LAZYLEN(GLOB.reta_job_trims["Service"] as DmList), LAZYLEN(GLOB.reta_job_trims["Command"] as DmList),
            LAZYLEN(GLOB.reta_job_trims["Cargo"] as DmList), LAZYLEN(GLOB.reta_job_trims["Mining"] as DmList)));
    }

    /// Pushes UI updates to all consoles in the same origin department
    // tg: code/modules/reta/reta_system.dm:167
    public static void reta_push_ui_updates(object origin, object target)
    {
        foreach (obj_machinery_requests_console console in Dm.each<obj_machinery_requests_console>(GLOB.reta_consoles_by_origin[origin]))
            console.ui_update();
    }

    /// Gets the department string for a user based on their job
    // tg: code/modules/reta/reta_system.dm:172
    public static object reta_get_user_department(mob user)
    {
        if (user?.mind?.assigned_role == null)
            return null;

        datum_job job = user.mind.assigned_role;
        float flags = job.departments_bitflags;
        if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_ENGINEERING)))
            return "Engineering";
        if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_SCIENCE)))
            return "Science";
        if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_CARGO)))
            return "Cargo";
        if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_SERVICE)))
            return "Service";
        if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_MEDICAL)))
            return "Medical";
        if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_SECURITY)))
            return "Security";
        if (0 != (Dm.bitand(flags, DEPARTMENT_BITFLAG_COMMAND)))
            return "Command";

        return null;
    }

    /// Cleans up an expired RETA grant from the active grants registry
    // tg: code/modules/reta/reta_system.dm:257
    public static void cleanup_expired_reta_grant(object target_dept, object origin_dept)
    {
        if (!Dm.truthy(GLOB.reta_active_grants[target_dept]))
            return;
        DmList grants = (DmList)GLOB.reta_active_grants[target_dept];
        Dm.remove(ref grants, origin_dept);
        GLOB.reta_active_grants[target_dept] = grants;
        if (Dm.length(grants) == 0)
            Dm.remove(ref GLOB.reta_active_grants, target_dept);
        log_reta(Dm.text("Cleaned up expired [] grant for [] department", origin_dept, target_dept));

        // Update all doors to remove RETA lights for expired grants
        update_all_doors_reta_lights();
    }

    /// Updates RETA lighting for all doors in the game
    // tg: code/modules/reta/reta_system.dm:270
    public static void update_all_doors_reta_lights()
    {
        foreach (obj_machinery_door_airlock door in Dm.each_cast<obj_machinery_door_airlock>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_airlock))))
            door.update_appearance(UPDATE_OVERLAYS);
    }

    /// Applies any currently active RETA grants to a newly created/spawned ID card
    /// This should be called when ID cards are created, spawned, or have their trim changed
    // tg: code/modules/reta/reta_system.dm:276
    public static void apply_active_reta_grants_to_card(obj_item_card_id id_card)
    {
        if (id_card == null || !Dm.truthy(id_card.trim))
            return;

        // If no active grants, nothing to do (prevents spam during round start)
        if (LAZYLEN(GLOB.reta_active_grants) == 0)
            return;

        // Check if this card's department has any active incoming RETA grants
        foreach (object target_dept in Dm.each((GLOB.reta_active_grants as DmList)?.Copy()))
        {
            DmList job_trims = GLOB.reta_job_trims[target_dept] as DmList;
            if (LAZYLEN(job_trims) == 0)
                continue;

            // Check if this card's trim matches the target department
            if (!Dm.truthy(is_type_in_list(id_card.trim as datum, job_trims)))
                continue;

            log_reta(Dm.text("Card trim [] matches [] department - applying active grants", id_card.trim, target_dept));

            // Apply all active grants for this department
            foreach (object origin_dept in Dm.each((GLOB.reta_active_grants[target_dept] as DmList)?.Copy()))
            {
                float expiry_time = Dm.num(((DmList)GLOB.reta_active_grants[target_dept])[origin_dept]);
                if (world.time >= expiry_time)
                    continue; // Grant expired, skip it

                float remaining_time = expiry_time - world.time;
                if (Dm.truthy(id_card.grant_reta_access(origin_dept, remaining_time)))
                    log_reta(Dm.text("Auto-granted [] access to newly created [] ([] department)", origin_dept, id_card, target_dept));
            }
        }
    }

    /// Initialize RETA config values
    // tg: code/modules/reta/reta_system.dm:291
    public static void reta_init_config()
    {
        log_world(Dm.text("RETA: System initialized with duration=[]ds, cooldown=[]ds, enabled=[]",
            config.Get(typeof(datum_config_entry_number_reta_duration_ds)),
            config.Get(typeof(datum_config_entry_number_reta_dept_cooldown_ds)),
            config.Get(typeof(datum_config_entry_flag_reta_enabled))));
    }
}
