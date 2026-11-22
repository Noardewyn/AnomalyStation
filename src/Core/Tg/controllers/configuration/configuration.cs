// Translated from tgstation (snapshot 1d99bd7f): code/controllers/configuration/configuration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated: the vars, Load (over the host file seam, DmWorldState.Files), full_wipe, Destroy, the error procs, InitEntries,
// RemoveEntry, LoadEntries, Get, Set and DelayedMessageAdmins. Deferred leaves (rules 1.4; nothing in M0 reads what they
// set): loadmaplist (M3, map rotation), LoadMOTD, LoadPolicy, LoadChatFilter (M9, client login and chat) and
// validate_job_config (M10, jobs). Not translated: the admin reload (admin_reload, PreConfigReload: admin tooling, M10), the
// chat filter internals (they compile /regex, which the shim does not have yet, M9), stat_entry (stat panels, rules 13) and the
// VV procs can_vv_get and vv_edit_var (VV, rules 13; their /datum roots do not exist in the port).

// tg: code/controllers/configuration/configuration.dm:1
[DmPath("/datum/controller/configuration")]
public partial class datum_controller_configuration : datum_controller
{
    public datum_controller_configuration()
    {
        name = "Configuration";
    }

    // tg: code/controllers/configuration/configuration.dm:4
    public string directory = "config";

    // tg: code/controllers/configuration/configuration.dm:6
    public float warned_deprecated_configs = FALSE;
    // tg: code/controllers/configuration/configuration.dm:7
    public float hiding_entries_by_type = TRUE; //Set for readability, admins can set this to FALSE if they want to debug it
    // tg: code/controllers/configuration/configuration.dm:8
    public DmList entries;
    // tg: code/controllers/configuration/configuration.dm:9
    public DmList entries_by_type;

    // tg: code/controllers/configuration/configuration.dm:11
    public DmList maplist;
    // tg: code/controllers/configuration/configuration.dm:12
    // GAP(M3): var/datum/map_config/defaultmap. /datum/map_config has no class yet; its [DmPath] belongs on
    // controllers/subsystem/mapping.cs (mapping.dm:974 declares it first in .dme order, rules 2.3), which the translation of
    // SSmapping (M3) completes. Typed datum, its nearest translated ancestor, until the class exists.
    public datum defaultmap;

    // tg: code/controllers/configuration/configuration.dm:14
    public DmList modes; // allowed modes
    // tg: code/controllers/configuration/configuration.dm:15
    public DmList gamemode_cache;
    // tg: code/controllers/configuration/configuration.dm:16
    public DmList votable_modes; // votable modes
    // tg: code/controllers/configuration/configuration.dm:17
    public DmList mode_names;
    // tg: code/controllers/configuration/configuration.dm:18
    public DmList mode_reports;
    // tg: code/controllers/configuration/configuration.dm:19
    public DmList mode_false_report_weight;

    // tg: code/controllers/configuration/configuration.dm:21
    public object motd;
    // tg: code/controllers/configuration/configuration.dm:22
    public object policy;

    /// If the configuration is loaded
    // tg: code/controllers/configuration/configuration.dm:25
    public float? loaded = FALSE;

    /// If a reload is in progress
    // tg: code/controllers/configuration/configuration.dm:28
    public float reload_in_progress = FALSE;

    // The var/statics of :31-67 are TgState fields (rules 10.6, the TgState part below).
    // SHIM-GAP: the six /regex statics are typed object: /regex is not in the shim yet (translation-rules.md Appendix C,
    // planned); only the chat filter (M9) sets and reads them.

    /// A regex that matches words blocked IC
    // tg: code/controllers/configuration/configuration.dm:31
    public static ref object ic_filter_regex => ref TgState.Current.datum_controller_configuration__ic_filter_regex;

    /// A regex that matches words blocked OOC
    // tg: code/controllers/configuration/configuration.dm:34
    public static ref object ooc_filter_regex => ref TgState.Current.datum_controller_configuration__ooc_filter_regex;

    /// A regex that matches words blocked IC, but not in PDAs
    // tg: code/controllers/configuration/configuration.dm:37
    public static ref object ic_outside_pda_filter_regex => ref TgState.Current.datum_controller_configuration__ic_outside_pda_filter_regex;

    /// A regex that matches words soft blocked IC
    // tg: code/controllers/configuration/configuration.dm:40
    public static ref object soft_ic_filter_regex => ref TgState.Current.datum_controller_configuration__soft_ic_filter_regex;

    /// A regex that matches words soft blocked OOC
    // tg: code/controllers/configuration/configuration.dm:43
    public static ref object soft_ooc_filter_regex => ref TgState.Current.datum_controller_configuration__soft_ooc_filter_regex;

    /// A regex that matches words soft blocked IC, but not in PDAs
    // tg: code/controllers/configuration/configuration.dm:46
    public static ref object soft_ic_outside_pda_filter_regex => ref TgState.Current.datum_controller_configuration__soft_ic_outside_pda_filter_regex;

    /// An assoc list of blocked IC words to their reasons
    // tg: code/controllers/configuration/configuration.dm:49
    public static ref DmList ic_filter_reasons => ref TgState.Current.datum_controller_configuration__ic_filter_reasons;

    /// An assoc list of words that are blocked IC, but not in PDAs, to their reasons
    // tg: code/controllers/configuration/configuration.dm:52
    public static ref DmList ic_outside_pda_filter_reasons => ref TgState.Current.datum_controller_configuration__ic_outside_pda_filter_reasons;

    /// An assoc list of words that are blocked both IC and OOC to their reasons
    // tg: code/controllers/configuration/configuration.dm:55
    public static ref DmList shared_filter_reasons => ref TgState.Current.datum_controller_configuration__shared_filter_reasons;

    /// An assoc list of soft blocked IC words to their reasons
    // tg: code/controllers/configuration/configuration.dm:58
    public static ref DmList soft_ic_filter_reasons => ref TgState.Current.datum_controller_configuration__soft_ic_filter_reasons;

    /// An assoc list of words that are soft blocked IC, but not in PDAs, to their reasons
    // tg: code/controllers/configuration/configuration.dm:61
    public static ref DmList soft_ic_outside_pda_filter_reasons => ref TgState.Current.datum_controller_configuration__soft_ic_outside_pda_filter_reasons;

    /// An assoc list of words that are soft blocked both IC and OOC to their reasons
    // tg: code/controllers/configuration/configuration.dm:64
    public static ref DmList soft_shared_filter_reasons => ref TgState.Current.datum_controller_configuration__soft_shared_filter_reasons;

    /// A list of configuration errors that occurred during load
    // tg: code/controllers/configuration/configuration.dm:67
    public static ref DmList configuration_errors => ref TgState.Current.datum_controller_configuration__configuration_errors;

    // tg: code/controllers/configuration/configuration.dm:69
    public virtual async DmTask admin_reload()
    {
        Dm.NotTranslated("code/controllers/configuration/configuration.dm:69", "admin_reload: the admin config reload (admin tooling, M10+)");
    }

    // tg: code/controllers/configuration/configuration.dm:78
    public virtual async DmTask<object> PreConfigReload()
    {
        Dm.NotTranslated("code/controllers/configuration/configuration.dm:78", "PreConfigReload: the admin config reload and its TGS event (admin tooling, M10+)");
        return null;
    }

    // tg: code/controllers/configuration/configuration.dm:88
    public virtual async DmTask Load(object _directory = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall())) //If admin proccall is detected down the line it will horribly break everything.
            return;
        if (Dm.truthy(_directory))
            directory = (string)_directory;
        if (Dm.truthy(entries))
            throw Dm.CRASH("/datum/controller/configuration/Load() called more than once!", "code/controllers/configuration/configuration.dm", 94);
        if (!Dm.truthy(configuration_errors))
            configuration_errors = Dm.list();
        InitEntries();
        if (Dm.truthy(fexists(Dm.text("[]/config.txt", directory))) && Dm.le(await LoadEntries("config.txt"), 1))
        {
            DmList legacy_configs = Dm.list("game_options.txt", "dbconfig.txt", "comms.txt");
            foreach (object I in Dm.each(legacy_configs))
            {
                if (Dm.truthy(fexists(Dm.text("[]/[]", directory, I))))
                {
                    log_config(Dm.text("No $include directives found in config.txt! Loading legacy [] files...", legacy_configs.Join("/")));
                    foreach (object J in Dm.each(legacy_configs))
                        await LoadEntries(J);
                    break;
                }
            }
        }
        if (Dm.truthy(fexists(Dm.text("[]/dev_overrides.txt", directory))))
            await LoadEntries("dev_overrides.txt");
        if (Dm.truthy(fexists(Dm.text("[]/ezdb.txt", directory))))
            await LoadEntries("ezdb.txt");
        loadmaplist(CONFIG_MAPS_FILE);
        LoadMOTD();
        LoadPolicy();
        LoadChatFilter();
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_load_jobs_from_txt))))
        {
            validate_job_config();
            if (Dm.truthy(SSjob.initialized)) // in case we're reloading from disk after initialization, wanna make sure the changes update in the ongoing shift
                SSjob.load_jobs_from_config();
        }

        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_usewhitelist))))
            Dm.NotTranslated("code/controllers/configuration/configuration.dm:119", "Load: load_whitelist (the player whitelist, admin tooling, M10+)");

        loaded = TRUE;

        if (Dm.truthy(Master))
            await Master.OnConfigLoad();
        process_config_errors();
    }

    // tg: code/controllers/configuration/configuration.dm:127
    public virtual void full_wipe()
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return;
        entries_by_type.Cut();
        QDEL_LIST_ASSOC_VAL(entries);
        entries = null;
        QDEL_LIST_ASSOC_VAL(maplist);
        maplist = null;
        QDEL_NULL(ref defaultmap);
        configuration_errors?.Cut();
    }

    // tg: code/controllers/configuration/configuration.dm:138
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        full_wipe();
        config = null;

        return base.Destroy(force);
    }

    // tg: code/controllers/configuration/configuration.dm:144
    public virtual void log_config_error(object error_message = null)
    {
        Dm.append(ref configuration_errors, error_message);
        log_config(error_message);
    }

    // tg: code/controllers/configuration/configuration.dm:148
    public virtual void process_config_errors()
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_config_errors_runtime))))
            return;
        foreach (object error_message in Dm.each(configuration_errors))
            stack_trace(error_message, "code/controllers/configuration/configuration.dm", 152);
    }

    // tg: code/controllers/configuration/configuration.dm:154
    public virtual void InitEntries()
    {
        DmList _entries = Dm.list();
        entries = _entries;
        DmList _entries_by_type = Dm.list();
        entries_by_type = _entries_by_type;

        foreach (object I in Dm.each(valid_subtypesof(typeof(datum_config_entry)))) //typesof is faster in this case
        {
            object E = I;
            E = Dm.@new((Type)I);
            object esname = ((datum_config_entry)E).name;
            datum_config_entry test = _entries[esname] as datum_config_entry;
            if (Dm.truthy(test))
            {
                log_config_error(Dm.text("Error: [] has the same name as []: []! Not initializing []!", test.type, ((datum)E).type, esname, ((datum)E).type));
                qdel(E);
                continue;
            }
            _entries[esname] = E;
            _entries_by_type[I] = E;
        }
    }

    // tg: code/controllers/configuration/configuration.dm:172
    public virtual void RemoveEntry(datum_config_entry CE = null)
    {
        Dm.remove(ref entries, CE.name);
        Dm.remove(ref entries_by_type, CE.type);
    }

    // DECISION(slot): slots.tsv gives LoadEntries a void (DmTask) slot, but the proc increments `.` (`++.`, :239, :288) and
    // Load reads the result (`LoadEntries("config.txt") <= 1`, :97), so its value is used (rules 7.4): DmTask<object>, not
    // narrowed (its early returns, :178 and :202, return a null `.`). Reported for Appendix A.
    // tg: code/controllers/configuration/configuration.dm:176
    public virtual async DmTask<object> LoadEntries(object filename = null, DmList stack = null)
    {
        stack ??= Dm.list();
        object dot = null;
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return dot;

        DmList separate_levels = splittext(filename, "/");
        // allows for inheriting our folder from the thing that included us
        string subfolder = "";
        // do we have an actual directory or is this just one file
        if (length(separate_levels) > 1)
        {
            object actual_filename = separate_levels[length(separate_levels)];
            // We need to sanitize out .. to ensure filename_to_test doesn't accidentially an infinte loop here
            // Need filename in absolute form
            DmList parsed_folder_bits = Dm.list();
            // look at just the relative directory referenced
            foreach (object entry in Dm.each(Dm.sub(separate_levels, actual_filename)))
            {
                if (Dm.eq(entry, "..") && Dm.truthy(length(parsed_folder_bits)))
                    parsed_folder_bits.Cut(length(parsed_folder_bits), 0);
                else
                    Dm.append(ref parsed_folder_bits, entry);
            }
            if (Dm.truthy(length(parsed_folder_bits)))
                subfolder = Dm.text("[]/", parsed_folder_bits.Join("/"));
            filename = Dm.text("[][]", subfolder, actual_filename);
        }

        object filename_to_test = (world.system_type == MS_WINDOWS ? LOWER_TEXT(filename) : filename);
        if (Dm.@in(filename_to_test, stack))
        {
            log_config_error(Dm.text("Warning: Config recursion detected ([]), breaking!", english_list(stack)));
            return dot;
        }
        stack = Dm.add(stack, filename_to_test) as DmList;

        log_config(Dm.text("Loading config file []...", filename));
        DmList lines = world.file2list(Dm.text("[]/[]", directory, filename)) as DmList;
        DmList _entries = entries;
        // RULE-GAP: the loop var L is assigned in the body (L = trim(L)), which C# forbids for a foreach variable (CS1656):
        // it is a local copied from L__it at the top of each iteration (the __it form of rules 6.5).
        foreach (object L__it in Dm.each(lines))
        {
            object L = L__it;
            L = trim(L);
            if (!Dm.truthy(L))
                continue;

            // SHIM-GAP: BYOND 515 text indexing (L[1], the character at a position) is not in the shim; copytext(L, 1, 2)
            // gives the same one-character text (positions count UTF-16 code units, DV4).
            object firstchar = copytext(L, 1, 2);
            if (Dm.eq(firstchar, "#"))
                continue;

            float lockthis = Dm.num(Dm.eq(firstchar, "@"));
            if (Dm.truthy(lockthis))
                L = copytext(L, length(firstchar) + 1);

            float pos = findtext(L, " ");
            object entry = null;
            object value = null;

            if (Dm.truthy(pos))
            {
                entry = LOWER_TEXT(copytext(L, 1, pos));
                value = copytext(L, pos + length(copytext(L, pos, pos + 1))); // SHIM-GAP: L[pos], as above
            }
            else
                entry = LOWER_TEXT(L);

            if (!Dm.truthy(entry))
                continue;

            if (Dm.eq(entry, "$include"))
            {
                if (!Dm.truthy(value))
                    log_config_error(Dm.text("Warning: Invalid $include directive: []", value));
                else
                {
                    await LoadEntries(Dm.text("[][]", subfolder, value), stack);
                    Dm.inc(ref dot);
                }
                continue;
            }

            // Reset directive, used for setting a config value back to defaults. Useful for string list config types
            if (Dm.eq(entry, "$reset"))
            {
                datum_config_entry resetee = _entries[LOWER_TEXT(value)] as datum_config_entry;
                if (!Dm.truthy(value) || !Dm.truthy(resetee))
                {
                    log_config_error(Dm.text("Warning: invalid $reset directive: []", value));
                    continue;
                }
                resetee.set_default();
                log_config(Dm.text("Reset configured value for [] to original defaults", value));
                continue;
            }

            datum_config_entry E = _entries[entry] as datum_config_entry;
            if (!Dm.truthy(E))
            {
                log_config(Dm.text("Unknown setting in configuration: '[]'", entry));
                continue;
            }

            if (Dm.truthy(lockthis))
                Dm.combine(ref E.protection, CONFIG_ENTRY_LOCKED);

            if (Dm.truthy(E.deprecated_by))
            {
                datum_config_entry new_ver = entries_by_type[E.deprecated_by] as datum_config_entry;
                object new_value = E.DeprecationUpdate(value);
                float good_update = Dm.num(istext(new_value));
                log_config(Dm.text("Entry [] is deprecated and will be removed soon. Migrate to []![]", entry, new_ver.name, Dm.truthy(good_update) ? Dm.text(" Suggested new value is: []", new_value) : ""));
                if (!Dm.truthy(warned_deprecated_configs))
                {
                    DelayedMessageAdmins("This server is using deprecated configuration settings. Please check the logs and update accordingly.");
                    warned_deprecated_configs = TRUE;
                }
                if (Dm.truthy(good_update))
                {
                    value = new_value;
                    E = new_ver;
                }
                else
                    warning(Dm.text("[] is deprecated but gave no proper return for DeprecationUpdate()", new_ver.type));
            }

            object validated = await E.ValidateAndSet(value);
            if (!Dm.truthy(validated))
            {
                string log_message = Dm.text("Failed to validate setting \"[]\" for []", value, entry);
                log_config(log_message);
                stack_trace(log_message, "code/controllers/configuration/configuration.dm", 278);
            }
            else
            {
                if (Dm.truthy(E.modified) && !Dm.truthy(E.dupes_allowed) && Dm.eq(E.resident_file, filename))
                    log_config_error(Dm.text("Duplicate setting for [] ([], []) detected! Using latest.", entry, value, E.resident_file));
            }

            E.resident_file = filename;

            if (Dm.truthy(validated))
                E.modified = TRUE;
        }

        Dm.inc(ref dot);
        return dot;
    }

    // tg: code/controllers/configuration/configuration.dm:297
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/configuration/configuration.dm:297", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/configuration/configuration.dm:301
    public virtual object Get(object entry_type = null)
    {
        object E = entry_type;
        float entry_is_abstract = Dm.num(Dm.eq(Dm.initial(E, (datum_config_entry p) => p.abstract_type), entry_type));
        if (Dm.truthy(entry_is_abstract))
            throw Dm.CRASH(Dm.text("Tried to retrieve an abstract config_entry: []", entry_type), "code/controllers/configuration/configuration.dm", 305);
        E = entries_by_type[entry_type];
        if (!Dm.truthy(E))
            throw Dm.CRASH(Dm.text("Missing config entry for []!", entry_type), "code/controllers/configuration/configuration.dm", 308);
        if (Dm.truthy(Dm.bitand(((datum_config_entry)E).protection, CONFIG_ENTRY_HIDDEN)) && Dm.truthy(IsAdminAdvancedProcCall()) && Dm.eq(GLOB.LastAdminCalledProc, "Get") && Dm.eq(GLOB.LastAdminCalledTargetRef, Dm.text("[]", REF(this))))
        {
            Dm.NotTranslated("code/controllers/configuration/configuration.dm:310", "Get: log_admin_private of an admin proc call reading a hidden entry (admin logging, M10+)");
            return null;
        }
        return ((datum_config_entry)E).config_entry_value;
    }

    // tg: code/controllers/configuration/configuration.dm:314
    public virtual async DmTask<object> Set(object entry_type = null, object new_val = null)
    {
        object E = entry_type;
        float entry_is_abstract = Dm.num(Dm.eq(Dm.initial(E, (datum_config_entry p) => p.abstract_type), entry_type));
        if (Dm.truthy(entry_is_abstract))
            throw Dm.CRASH(Dm.text("Tried to set an abstract config_entry: []", entry_type), "code/controllers/configuration/configuration.dm", 318);
        E = entries_by_type[entry_type];
        if (!Dm.truthy(E))
            throw Dm.CRASH(Dm.text("Missing config entry for []!", entry_type), "code/controllers/configuration/configuration.dm", 321);
        if (Dm.truthy(Dm.bitand(((datum_config_entry)E).protection, CONFIG_ENTRY_LOCKED)) && Dm.truthy(IsAdminAdvancedProcCall()) && Dm.eq(GLOB.LastAdminCalledProc, "Set") && Dm.eq(GLOB.LastAdminCalledTargetRef, Dm.text("[]", REF(this))))
        {
            Dm.NotTranslated("code/controllers/configuration/configuration.dm:323", "Set: log_admin_private of an admin proc call writing a locked entry (admin logging, M10+)");
            return null;
        }
        return await ((datum_config_entry)E).ValidateAndSet(Dm.text("[]", new_val));
    }

    // tg: code/controllers/configuration/configuration.dm:327
    public virtual void LoadMOTD()
    {
        Dm.Deferred("code/controllers/configuration/configuration.dm:327", "LoadMOTD: the message of the day (client login, M10+)");
    }

    /*
    Policy file should be a json file with a single object.
    Value is raw html.

    Possible keywords :
    Job titles / Assigned roles (ghost spawners for example) : Assistant , Captain , Ash Walker
    Mob types : /mob/living/basic/carp
    Antagonist types : /datum/antagonist/highlander
    Species types : /datum/species/lizard
    special keywords defined in _DEFINES/admin.dm

    Example config:
    {
        JOB_ASSISTANT : "Don't kill everyone",
        "/datum/antagonist/highlander" : "<b>Kill everyone</b>",
        "Ash Walker" : "Kill all spacemans"
    }

    */
    // tg: code/controllers/configuration/configuration.dm:366
    public virtual void LoadPolicy()
    {
        Dm.Deferred("code/controllers/configuration/configuration.dm:366", "LoadPolicy: the role policy texts (shown to players, M10+)");
    }

    // tg: code/controllers/configuration/configuration.dm:378
    public virtual void loadmaplist(object filename = null)
    {
        Dm.Deferred("code/controllers/configuration/configuration.dm:378", "loadmaplist: the map rotation list (map configs, M3)");
    }

    // tg: code/controllers/configuration/configuration.dm:443
    public virtual void LoadChatFilter()
    {
        Dm.Deferred("code/controllers/configuration/configuration.dm:443", "LoadChatFilter: the IC/OOC word filter (chat, M10+)");
    }

    // tg: code/controllers/configuration/configuration.dm:466
    public virtual void load_legacy_chat_filter()
    {
        Dm.NotTranslated("code/controllers/configuration/configuration.dm:466", "load_legacy_chat_filter: the word filter (chat, M10+; /regex is not in the shim yet)");
    }

    /// Will update the internal regexes of the chat filter based on the filter reasons
    // tg: code/controllers/configuration/configuration.dm:490
    public virtual void update_chat_filter_regexes()
    {
        Dm.NotTranslated("code/controllers/configuration/configuration.dm:490", "update_chat_filter_regexes: the word filter (chat, M10+; /regex is not in the shim yet)");
    }

    // tg: code/controllers/configuration/configuration.dm:498
    public virtual object try_extract_from_word_filter(DmList word_filter = null, object key = null)
    {
        Dm.NotTranslated("code/controllers/configuration/configuration.dm:498", "try_extract_from_word_filter: the word filter (chat, M10+)");
        return null;
    }

    // tg: code/controllers/configuration/configuration.dm:515
    public virtual object compile_filter_regex(DmList banned_words = null)
    {
        Dm.NotTranslated("code/controllers/configuration/configuration.dm:515", "compile_filter_regex: the word filter (chat, M10+; /regex is not in the shim yet)");
        return null;
    }

    /// Check to ensure that the jobconfig is valid/in-date.
    // tg: code/controllers/configuration/configuration.dm:539
    public virtual void validate_job_config()
    {
        string config_toml = Dm.text("[]/jobconfig.toml", directory);
        string config_txt = Dm.text("[]/jobs.txt", directory);
        string message = "Notify Server Operators: ";
        log_config("Validating config file jobconfig.toml...");

        if (!Dm.truthy(fexists(Dm.file(config_toml))))
        {
            SSjob.legacy_mode = TRUE;
            message += "jobconfig.toml not found, falling back to legacy mode (using jobs.txt). To suppress this warning, generate a jobconfig.toml by running the verb 'Generate Job Configuration' in the Server tab.\nFrom there, you can then add it to the /config folder of your server to have it take effect for future rounds.";

            if (!Dm.truthy(fexists(Dm.file(config_txt))))
                message += "\n\nFailed to set up legacy mode, jobs.txt not found! Codebase defaults will be used. If you do not wish to use this system, please disable it by commenting out the LOAD_JOBS_FROM_TXT config flag.";

            log_config(message);
            DelayedMessageAdmins(span_notice(message));
            return;
        }

        // rustg_raw_read_toml_file(config_toml) and the TOML's checks (:557-562): DEVIATION(DV181), the port reads the legacy jobs.txt
        // only (rust-g's TOML reader is not reimplemented); a jobconfig.toml is reported and ignored
        log_config(message + "jobconfig.toml is not read by this server (the legacy jobs.txt is): its job settings are ignored.");
    }

    //Message admins when you can.
    // tg: code/controllers/configuration/configuration.dm:564
    public virtual void DelayedMessageAdmins(object text = null)
    {
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.message_admins), text), 0, file: "code/controllers/configuration/configuration.dm", line: 565);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/configuration/configuration.dm:31-67  the var/statics of /datum/controller/configuration (rules 10.6)
    internal object datum_controller_configuration__ic_filter_regex;
    internal object datum_controller_configuration__ooc_filter_regex;
    internal object datum_controller_configuration__ic_outside_pda_filter_regex;
    internal object datum_controller_configuration__soft_ic_filter_regex;
    internal object datum_controller_configuration__soft_ooc_filter_regex;
    internal object datum_controller_configuration__soft_ic_outside_pda_filter_regex;
    internal DmList datum_controller_configuration__ic_filter_reasons;
    internal DmList datum_controller_configuration__ic_outside_pda_filter_reasons;
    internal DmList datum_controller_configuration__shared_filter_reasons;
    internal DmList datum_controller_configuration__soft_ic_filter_reasons;
    internal DmList datum_controller_configuration__soft_ic_outside_pda_filter_reasons;
    internal DmList datum_controller_configuration__soft_shared_filter_reasons;
    internal DmList datum_controller_configuration__configuration_errors;
}
