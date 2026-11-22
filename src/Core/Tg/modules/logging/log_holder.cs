// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/log_holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: everything translated except the admin log viewer: ADMIN_VERB(log_viewer_new) (:35, admin verbs, rules 13) and the
// tgui procs ui_interact, ui_state, ui_static_data, ui_data and ui_act (:38-111), not declared because their /datum roots
// in tgui are not translated (rules 13); cache_ui_data, which only they call, is translated. GENERAL_PROTECT_DATUM
// (/datum/log_holder) (:33, __DEFINES/_protect.dm:3) adds the VV and savefile guards can_vv_get, vv_edit_var,
// CanProcCall, Read and Write: not translated (VV and savefiles, rules 13; their roots are not declared).

// tg: code/modules/logging/log_holder.dm:1  GLOBAL_REAL(logger, /datum/log_holder)
public sealed partial class TgState
{
    internal datum_log_holder logger;

    // tg: code/modules/logging/log_holder.dm:165  var/static/list/category_tree in /datum/log_holder/proc/assemble_log_category_tree
    internal DmList datum_log_holder__assemble_log_category_tree__category_tree;

    // tg: code/modules/logging/log_holder.dm:208  var/static/cache in /datum/log_holder/proc/get_recovery_file_for
    internal object datum_log_holder__get_recovery_file_for__cache;

    /// <summary>
    /// PORT(test seam): which log category types <c>assemble_log_category_tree</c> sees in this world, like the
    /// subsystem-type filter of lead decision W3 G2 (<see cref="SubsystemTypeFilter"/>): the type registry also holds
    /// the test assembly's fixture categories, so <c>subtypesof(/datum/log_category)</c> passes through this filter,
    /// which by default keeps only the types of the Tg assembly (production has no other). Not world state: not hashed.
    /// </summary>
    [NotHashed]
    internal Func<Type, bool> LogCategoryTypeFilter;

    /// <summary>
    /// The types of <paramref name="category_types"/> (a <c>subtypesof(/datum/log_category)</c> list) that
    /// <see cref="LogCategoryTypeFilter"/> keeps, in their order, as a new list.
    /// </summary>
    internal DmList FilterLogCategoryTypes(DmList category_types)
    {
        var kept = new DmList();
        foreach (object item in Dm.each(category_types))
        {
            if (item is Type type && (LogCategoryTypeFilter?.Invoke(type) ?? type.Assembly == typeof(TgState).Assembly))
                kept.Add(type);
        }
        return kept;
    }
}

public static partial class GlobalProcs
{
    /// <summary>tg <c>logger</c> (rules 10.6): the log holder of this world.</summary>
    public static ref datum_log_holder logger => ref TgState.Current.logger;
}

/**
 * Main datum to manage logging actions
 */
// tg: code/modules/logging/log_holder.dm:5
[DmPath("/datum/log_holder")]
public partial class datum_log_holder : datum
{
    /// Round ID, if set, that logging is initialized for
    // tg: code/modules/logging/log_holder.dm:7
    public object round_id;
    /// When the log_holder first initialized
    // tg: code/modules/logging/log_holder.dm:9
    public object logging_start_timestamp;

    /// Associative: category -> datum
    // tg: code/modules/logging/log_holder.dm:12
    public DmList log_categories;
    /// typecache list for categories that exist but are disabled
    // tg: code/modules/logging/log_holder.dm:14
    public DmList disabled_categories;
    /// category nesting tree for ui purposes
    // tg: code/modules/logging/log_holder.dm:16
    public DmList category_group_tree;

    /// list of Log args waiting for processing pending log initialization
    // tg: code/modules/logging/log_holder.dm:19
    public DmList waiting_log_calls;

    /// Whether or not logging as human readable text is enabled
    // tg: code/modules/logging/log_holder.dm:22
    public float human_readable_enabled = FALSE;

    /// Cached ui_data
    // tg: code/modules/logging/log_holder.dm:25
    public DmList data_cache;

    /// Last time the ui_data was updated
    // tg: code/modules/logging/log_holder.dm:28
    public float last_data_update = 0;

    // tg: code/modules/logging/log_holder.dm:30
    public float initialized = FALSE;
    // tg: code/modules/logging/log_holder.dm:31
    public float shutdown = FALSE;

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        data_cache = Dm.list();
    }

    // The proc-level statics (rules 10.6).
    private static ref DmList assemble_log_category_tree__category_tree => ref TgState.Current.datum_log_holder__assemble_log_category_tree__category_tree;
    private static ref object get_recovery_file_for__cache => ref TgState.Current.datum_log_holder__get_recovery_file_for__cache;

    // tg: code/modules/logging/log_holder.dm:73
    public virtual void cache_ui_data()
    {
        DmList category_map = Dm.list();
        foreach (object category__it in Dm.each(log_categories))
        {
            object category = category__it;
            category = log_categories[category];
            DmList category_data = Dm.list();

            DmList entries = Dm.list();
            foreach (datum_log_entry entry in Dm.each_cast<datum_log_entry>(((datum_log_category)category).entries))
            {
                Dm.append(ref entries, Dm.list(Dm.list(
                    Dm.kv("id", entry.id),
                    Dm.kv("message", entry.message),
                    Dm.kv("timestamp", entry.timestamp),
                    Dm.kv("data", entry.data),
                    Dm.kv("semver", entry.semver_store))));
            }
            category_data["entries"] = entries;
            category_data["entry_count"] = ((datum_log_category)category).entry_count;

            category_map[((datum_log_category)category).category] = category_data;
        }

        data_cache.Cut();
        last_data_update = world.time;

        data_cache["categories"] = category_map;
        data_cache["last_data_update"] = last_data_update;
    }

    /// Assembles basic information for logging, creating the log category datums and checking for config flags as required
    // tg: code/modules/logging/log_holder.dm:113
    public virtual void init_logging()
    {
        if (Dm.truthy(initialized))
            throw Dm.CRASH("Attempted to call init_logging twice!", "code/modules/logging/log_holder.dm", 115);

        round_id = GLOB.round_id;
        logging_start_timestamp = rustg_unix_timestamp();
        log_categories = Dm.list();
        disabled_categories = Dm.list();

        human_readable_enabled = Dm.num(config.Get(typeof(datum_config_entry_flag_log_as_human_readable)));

        category_group_tree = assemble_log_category_tree() as DmList;
        object config_flag = null;
        foreach (Type master_category in Dm.each_cast<Type>(category_group_tree))
        {
            DmList sub_categories = category_group_tree[master_category] as DmList;
            sub_categories = sub_categories.Copy();
            foreach (Type sub_category in Dm.each_cast<Type>(sub_categories))
            {
                config_flag = Dm.initial(sub_category, (datum_log_category p) => p.config_flag);
                if (Dm.truthy(config_flag) && !Dm.truthy(config.Get(config_flag)))
                {
                    disabled_categories[Dm.initial(sub_category, (datum_log_category p) => p.category)] = TRUE;
                    Dm.remove(ref sub_categories, sub_category);
                    continue;
                }
            }

            config_flag = Dm.initial(master_category, (datum_log_category p) => p.config_flag);
            if (Dm.truthy(config_flag) && !Dm.truthy(config.Get(config_flag)))
            {
                disabled_categories[Dm.initial(master_category, (datum_log_category p) => p.category)] = TRUE;
                if (!Dm.truthy(length(sub_categories)))
                    continue;
            }
            // enabled, or any of the sub categories are enabled
            init_log_category(master_category, sub_categories);
        }

        initialized = TRUE;

        // process any waiting log calls and then cut the list
        foreach (DmList arg_list in Dm.each_cast<DmList>(waiting_log_calls))
            Waiting.NoSleep(Dm.Call(this, nameof(Log), Dm.arglist(arg_list)));
        waiting_log_calls?.Cut();

        if (Dm.truthy(fexists(GLOB.config_error_log)))
        {
            fcopy(GLOB.config_error_log, Dm.text("[]/config_error.log", GLOB.log_directory));
            fdel(GLOB.config_error_log);
        }

        world._initialize_log_files();
    }

    /// Tells the log_holder to not allow any more logging to be done, and dumps all categories to their json file
    // tg: code/modules/logging/log_holder.dm:158
    public virtual void shutdown_logging()
    {
        if (Dm.truthy(shutdown))
            throw Dm.CRASH("Attempted to call shutdown_logging twice!", "code/modules/logging/log_holder.dm", 160);
        shutdown = TRUE;
    }

    /// Iterates over all log category types to assemble them into a tree of main category -> (sub category)[] while also checking for loops and sanity errors
    // tg: code/modules/logging/log_holder.dm:164
    public virtual object assemble_log_category_tree()
    {
        if (Dm.truthy(assemble_log_category_tree__category_tree))
            return assemble_log_category_tree__category_tree;

        assemble_log_category_tree__category_tree = Dm.list();
        DmList all_types = TgState.Current.FilterLogCategoryTypes(subtypesof(typeof(datum_log_category))); // PORT(test seam)
        DmList known_categories = Dm.list();
        DmList sub_categories = Dm.list();

        // Assemble the master categories
        foreach (Type category_type in Dm.each_cast<Type>(all_types))
        {
            object category = Dm.initial(category_type, (datum_log_category p) => p.category);
            // tg never adds to known_categories, so this check never trips (a duplicate category is not skipped).
            if (Dm.@in(category, known_categories))
            {
                stack_trace(Dm.text("log category type '[]' has duplicate category '[]', skipping", category_type, category), "code/modules/logging/log_holder.dm", 178);
                continue;
            }

            if (!Dm.truthy(Dm.initial(category_type, (datum_log_category p) => p.schema_version)))
            {
                stack_trace(Dm.text("log category type '[]' does not have a valid schema version, skipping", category_type), "code/modules/logging/log_holder.dm", 182);
                continue;
            }

            object master_category = Dm.initial(category_type, (datum_log_category p) => p.master_category);
            if (Dm.truthy(master_category))
            {
                Dm.append_at(sub_categories, master_category, Dm.list(category_type));
                continue;
            }
            assemble_log_category_tree__category_tree[category_type] = Dm.list();
        }

        // Sort the sub categories
        foreach (Type master in Dm.each_cast<Type>(sub_categories))
        {
            if (!Dm.@in(master, assemble_log_category_tree__category_tree))
            {
                stack_trace(Dm.text("log category [] is an invalid master category as it's a sub category", master), "code/modules/logging/log_holder.dm", 194);
                continue;
            }
            foreach (Type sub_category in Dm.each_cast<Type>(sub_categories[master]))
            {
                if (!Dm.eq(Dm.initial(sub_category, (datum_log_category p) => p.secret), Dm.initial(master, (datum_log_category p) => p.secret)))
                    stack_trace(Dm.text("log category [] has a secret status that differs from its master category []", sub_category, master), "code/modules/logging/log_holder.dm", 198);
                Dm.append_at(assemble_log_category_tree__category_tree, master, Dm.list(sub_category));
            }
        }

        return assemble_log_category_tree__category_tree;
    }

    /// Log entry header used to mark a file is being reset
    // tg: code/modules/logging/log_holder.dm:204  #define LOG_CATEGORY_RESET_FILE_MARKER (file-local, #undef at :236)
    private const string LOG_CATEGORY_RESET_FILE_MARKER = "{\"LOG FILE RESET -- THIS IS AN ERROR\"}";
    // tg: code/modules/logging/log_holder.dm:205  #define LOG_CATEGORY_RESET_FILE_MARKER_READABLE (file-local, #undef at :237)
    private const string LOG_CATEGORY_RESET_FILE_MARKER_READABLE = "LOG FILE RESET -- THIS IS AN ERROR";

    /// Gets a recovery file for the given path. Caches the last known recovery path for each path.
    // tg: code/modules/logging/log_holder.dm:207
    public virtual object get_recovery_file_for(object path = null)
    {
        if (get_recovery_file_for__cache == null)
            get_recovery_file_for__cache = Dm.list();

        object count = (((DmList)get_recovery_file_for__cache)[path] is var or__1 && Dm.truthy(or__1) ? or__1 : 0);
        while (Dm.truthy(fexists(Dm.text("[].rec[]", path, count))))
            Dm.inc(ref count);
        ((DmList)get_recovery_file_for__cache)[path] = count;

        return Dm.text("[].rec[]", path, count);
    }

    /// Sets up the given category's file and header.
    // tg: code/modules/logging/log_holder.dm:220
    public virtual void init_category_file(datum_log_category category = null)
    {
        object file_path = category.get_output_file(null);
        if (Dm.truthy(fexists(file_path))) // already exists? implant a reset marker
        {
            rustg_file_append(LOG_CATEGORY_RESET_FILE_MARKER, file_path);
            fcopy(file_path, get_recovery_file_for(file_path));
        }
        rustg_file_write(Dm.text("[]\n", json_encode(category.category_header)), file_path);

        if (!Dm.truthy(human_readable_enabled))
            return;

        file_path = category.get_output_file(null, "log");
        if (Dm.truthy(fexists(file_path)))
        {
            rustg_file_append(LOG_CATEGORY_RESET_FILE_MARKER_READABLE, file_path);
            fcopy(file_path, get_recovery_file_for(file_path));
        }
        rustg_file_write(Dm.text("\\[[]\\] Starting up round ID [].\n - -------------------------\n", human_readable_timestamp(), round_id), file_path);
    }

    /// Initializes the given log category and populates the list of contained categories based on the sub category list
    // tg: code/modules/logging/log_holder.dm:240
    public virtual void init_log_category(Type category_type = null, DmList sub_categories = null)
    {
        datum_log_category category_instance = Dm.@new(category_type) as datum_log_category;

        DmList contained_categories = Dm.list();
        // The typed loop var holds the category's type and then its instance (wider values, rules 3.4); it is reassigned
        // in the body, so it iterates as sub_category__it (rules 6.5).
        foreach (object sub_category__it in Dm.each(sub_categories))
        {
            object sub_category = sub_category__it;
            sub_category = Dm.@new(sub_category as Type);
            object sub_category_actual = ((datum_log_category)sub_category).category;
            ((datum_log_category)sub_category).master_category = category_instance;
            log_categories[sub_category_actual] = sub_category;

            if (!Dm.truthy(semver_to_list(((datum_log_category)sub_category).schema_version)))
            {
                stack_trace(Dm.text("log category [] has an invalid schema version '[]'", sub_category_actual, ((datum_log_category)sub_category).schema_version), "code/modules/logging/log_holder.dm", 251);
                ((datum_log_category)sub_category).schema_version = LOG_CATEGORY_SCHEMA_VERSION_NOT_SET;
            }

            Dm.append(ref contained_categories, sub_category_actual);
        }

        log_categories[category_instance.category] = category_instance;

        if (!Dm.truthy(semver_to_list(category_instance.schema_version)))
        {
            stack_trace(Dm.text("log category [] has an invalid schema version '[]'", category_instance.category, category_instance.schema_version), "code/modules/logging/log_holder.dm", 259);
            category_instance.schema_version = LOG_CATEGORY_SCHEMA_VERSION_NOT_SET;
        }

        Dm.append(ref contained_categories, category_instance.category);

        DmList category_header = Dm.list(
            Dm.kv(LOG_HEADER_INIT_TIMESTAMP, logging_start_timestamp),
            Dm.kv(LOG_HEADER_ROUND_ID, GLOB.round_id),
            Dm.kv(LOG_HEADER_SECRET, category_instance.secret),
            Dm.kv(LOG_HEADER_CATEGORY_LIST, contained_categories),
            Dm.kv(LOG_HEADER_CATEGORY, category_instance.category));

        category_instance.category_header = category_header;
        // tg passes category_header as a second argument, which init_category_file does not declare: dropped (rules 7.3).
        init_category_file(category_instance);
    }

    // tg: code/modules/logging/log_holder.dm:275
    public virtual object human_readable_timestamp()
    {
        return rustg_formatted_timestamp("%Y-%m-%d %H:%M:%S%.3f");
    }

    /// Adds an entry to the given category, if the category is disabled it will not be logged.
    /// If the category does not exist, we will CRASH and log to the error category.
    /// the data list is optional and will be recursively json serialized.
    // tg: code/modules/logging/log_holder.dm:281
    public virtual void Log(object category = null, object message = null, object data = null)
    {
        // This is Log because log is a byond internal proc

        // do not include the message because these go into the runtime log and we might be secret!
        if (!istext(message))
        {
            message = Dm.text("[]", message);
            stack_trace("Logging with a non-text message", "code/modules/logging/log_holder.dm", 287);
        }

        if (!Dm.truthy(category))
        {
            category = LOG_CATEGORY_INTERNAL_CATEGORY_NOT_FOUND;
            stack_trace("Logging with a null or empty category", "code/modules/logging/log_holder.dm", 291);
        }

        if (Dm.truthy(data) && !islist(data))
        {
            data = Dm.list(Dm.kv("data", data));
            stack_trace("Logging with data this is not a list, it will be converted to a list with a single key 'data'", "code/modules/logging/log_holder.dm", 295);
        }

        if (!Dm.truthy(initialized)) // we are initialized during /world/proc/SetupLogging which is called in /world/New
        {
            Dm.append(ref waiting_log_calls, Dm.list(Dm.list(category, message, data)));
            return;
        }

        if (Dm.truthy(disabled_categories[category]))
            return;

        datum_log_category log_category = log_categories[category] as datum_log_category;
        if (!Dm.truthy(log_category))
        {
            Log(LOG_CATEGORY_INTERNAL_CATEGORY_NOT_FOUND, message, data);
            throw Dm.CRASH(Dm.text("Attempted to log to a category that doesn't exist! []", category), "code/modules/logging/log_holder.dm", 307);
        }

        DmList semver_store = null;
        if (Dm.truthy(length(data)))
        {
            semver_store = Dm.list();
            data = recursive_jsonify(data as DmList, semver_store);
        }
        log_category.create_entry(message, data as DmList, semver_store);
    }

    /// Recursively converts an associative list of datums into their jsonified(list) form
    // tg: code/modules/logging/log_holder.dm:316
    public virtual object recursive_jsonify(DmList data_list = null, DmList semvers = null)
    {
        if (data_list == null)
            return null;

        DmList jsonified_list = Dm.list();
        foreach (object key in Dm.each(data_list))
        {
            // var/datum/data holds lists, text and numbers too (wider values, rules 3.4).
            object data = data_list[key];

            if (data == null)
                pass(); // nulls are allowed

            else if (islist(data))
                data = recursive_jsonify(data as DmList, semvers);

            else if (isdatum(data))
            {
                DmList options_list = Dm.list(
                    Dm.kv(SCHEMA_VERSION, LOG_CATEGORY_SCHEMA_VERSION_NOT_SET));

                DmList serialization_data = ((datum)data).serialize_list(options_list, semvers) as DmList;
                object current_semver = semvers[((datum)data).type];
                if (!Dm.truthy(semver_to_list(current_semver)))
                {
                    stack_trace("serialization of data had an invalid semver", "code/modules/logging/log_holder.dm", 338);
                    semvers[((datum)data).type] = LOG_CATEGORY_SCHEMA_VERSION_NOT_SET;
                }

                if (!Dm.truthy(length(serialization_data))) // serialize_list wasn't implemented, and errored
                {
                    stack_trace("serialization data was empty", "code/modules/logging/log_holder.dm", 342);
                    continue;
                }

                data = recursive_jsonify(serialization_data, semvers);
            }

            if (islist(data) && !Dm.truthy(length(data)))
            {
                stack_trace("recursive_jsonify got an empty list after serialization", "code/modules/logging/log_holder.dm", 348);
                continue;
            }

            jsonified_list[key] = data;
        }

        return jsonified_list;
    }
}
