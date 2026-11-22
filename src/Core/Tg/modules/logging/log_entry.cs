// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/log_entry.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Schema version must always be the very last element in the array.

// Current Schema: 1.0.0
// [timestamp, category, message, data, world_state, semver_store, id, schema_version]

public sealed partial class TgState
{
    // tg: code/modules/logging/log_entry.dm:10  /datum/log_entry/var/static/next_id = 0
    internal float datum_log_entry__next_id = 0;

    // tg: code/modules/logging/log_entry.dm:115  var/static/in_error_recovery of CHECK_AND_TRY_FILE_ERROR_RECOVERY (:99) in write_entry_to_file
    internal float datum_log_entry__write_entry_to_file__in_error_recovery = FALSE;

    // tg: code/modules/logging/log_entry.dm:120  var/static/in_error_recovery of CHECK_AND_TRY_FILE_ERROR_RECOVERY (:99) in write_readable_entry_to_file
    internal float datum_log_entry__write_readable_entry_to_file__in_error_recovery = FALSE;
}

/// A datum which contains log information.
// tg: code/modules/logging/log_entry.dm:8
[DmPath("/datum/log_entry")]
public partial class datum_log_entry : datum
{
    /// Next id to assign to a log entry.
    // tg: code/modules/logging/log_entry.dm:10
    public static ref float next_id => ref TgState.Current.datum_log_entry__next_id;

    /// Unique id of the log entry.
    // tg: code/modules/logging/log_entry.dm:13
    public object id;

    /// Schema version of the log entry.
    // tg: code/modules/logging/log_entry.dm:16
    public string schema_version = "1.0.0";

    /// Unix timestamp of the log entry.
    // tg: code/modules/logging/log_entry.dm:19
    public object timestamp;

    /// Category of the log entry.
    // tg: code/modules/logging/log_entry.dm:22
    public object category;

    /// Message of the log entry.
    // tg: code/modules/logging/log_entry.dm:25
    public object message;

    /// Bitfield that describes how exactly to log stuff exactly
    /// See code/__DEFINES/logging/dm
    // tg: code/modules/logging/log_entry.dm:29
    public float? flags = NONE;

    /// Data of the log entry; optional.
    // tg: code/modules/logging/log_entry.dm:32
    public DmList data;

    /// Semver store of the log entry, used to store the schema of data entries
    // tg: code/modules/logging/log_entry.dm:35
    public DmList semver_store;

    // tg: GENERAL_PROTECT_DATUM(/datum/log_entry) (:37): the VV and savefile guards are not translated (rules 13).

    // The proc-level statics of the CHECK_AND_TRY_FILE_ERROR_RECOVERY expansions (rules 10.6).
    private static ref float write_entry_to_file__in_error_recovery => ref TgState.Current.datum_log_entry__write_entry_to_file__in_error_recovery;
    private static ref float write_readable_entry_to_file__in_error_recovery => ref TgState.Current.datum_log_entry__write_readable_entry_to_file__in_error_recovery;

    // tg: code/modules/logging/log_entry.dm:39
    [DmParams("timestamp", "category", "message", "flags", "data", "semver_store")]
    public override async DmTask New(params object[] __extra)
    {
        object timestamp = Dm.arg(__extra, 0);
        object category = Dm.arg(__extra, 1);
        object message = Dm.arg(__extra, 2);
        object flags = Dm.arg(__extra, 3);
        DmList data = Dm.arg<DmList>(__extra, 4);
        DmList semver_store = Dm.arg<DmList>(__extra, 5);
        await base.New(Dm.forward(__extra, timestamp, category, message, flags, data, semver_store));

        this.id = next_id++;
        this.timestamp = timestamp;
        this.category = category;
        this.flags = Dm.num_or_null(flags);
        this.message = message;
        with_data(data);
        with_semver_store(semver_store);
    }

    // tg: code/modules/logging/log_entry.dm:50
    public virtual object with_data(object data = null)
    {
        if (data != null)
        {
            if (!islist(data))
            {
                this.data = Dm.list(Dm.kv("data", data));
                stack_trace(Dm.text("Log entry data was not a list, it was [].", ((datum)data).type), "code/modules/logging/log_entry.dm", 54);
            }
            else
                this.data = data as DmList;
        }
        return this;
    }

    // tg: code/modules/logging/log_entry.dm:59
    public virtual object with_semver_store(object semver_store = null)
    {
        if (semver_store == null)
            return null;
        if (!islist(semver_store))
            stack_trace(Dm.text("Log entry semver store was not a list, it was []. We cannot reliably convert it to a list.", ((datum)semver_store).type), "code/modules/logging/log_entry.dm", 63);
        else
            this.semver_store = semver_store as DmList;
        return this;
    }

    /// Converts the log entry to a human-readable string.
    // tg: code/modules/logging/log_entry.dm:69
    public virtual object to_readable_text(float? format = null)
    {
        format ??= TRUE;
        string output = "";
        if (Dm.truthy(format))
            output += Dm.text("\\[[]\\] []: []", timestamp, uppertext(category), message);
        else
            output += Dm.text("[]: []", uppertext(category), message);

        if (Dm.truthy(Dm.bitand(flags, ENTRY_USE_DATA_W_READABLE)))
            output += json_encode(data, JSON_PRETTY_PRINT);
        return output;
    }

    // tg: code/modules/logging/log_entry.dm:80  #define MANUAL_JSON_ENTRY(list, key, value) list.Add("\"[key]\":[(!isnull(value)) ? json_encode(value) : "null"]") (file-local, #undef at :96)
    private static void MANUAL_JSON_ENTRY(DmList list, object key, object value)
        => list.Add(Dm.text("\"[]\":[]", key, (value != null) ? json_encode(value) : "null"));

    /// Converts the log entry to a JSON string.
    // tg: code/modules/logging/log_entry.dm:83
    public virtual object to_json_text()
    {
        // I do not trust byond's json encoder, and need to ensure the order doesn't change.
        DmList json_entries = Dm.list();
        MANUAL_JSON_ENTRY(json_entries, LOG_ENTRY_KEY_TIMESTAMP, timestamp);
        MANUAL_JSON_ENTRY(json_entries, LOG_ENTRY_KEY_CATEGORY, category);
        MANUAL_JSON_ENTRY(json_entries, LOG_ENTRY_KEY_MESSAGE, message);
        MANUAL_JSON_ENTRY(json_entries, LOG_ENTRY_KEY_DATA, data);
        MANUAL_JSON_ENTRY(json_entries, LOG_ENTRY_KEY_WORLD_STATE, world.get_world_state_for_logging());
        MANUAL_JSON_ENTRY(json_entries, LOG_ENTRY_KEY_SEMVER_STORE, semver_store);
        MANUAL_JSON_ENTRY(json_entries, LOG_ENTRY_KEY_ID, id);
        MANUAL_JSON_ENTRY(json_entries, LOG_ENTRY_KEY_SCHEMA_VERSION, schema_version);
        return Dm.text("{[]}", json_entries.Join(","));
    }

    /// Writes the log entry to a file.
    // tg: code/modules/logging/log_entry.dm:114
    public virtual void write_entry_to_file(object file = null, params object[] __extra)
    {
        // CHECK_AND_TRY_FILE_ERROR_RECOVERY(file) (:98-111, file-local), expanded inline (rules 11.1 (c), (d), (f)).
        DmList args = Dm.args(file, __extra);
        if (!Dm.truthy(fexists(file)))
        {
            if (Dm.truthy(write_entry_to_file__in_error_recovery))
            {
                write_entry_to_file__in_error_recovery = FALSE;
                throw Dm.CRASH(Dm.text("Failed to error recover log file: []", file), "code/modules/logging/log_entry.dm", 115);
            }
            write_entry_to_file__in_error_recovery = TRUE;
            logger.Log(LOG_CATEGORY_INTERNAL_ERROR, Dm.text("attempting to perform file error recovery: []", file));
            logger.init_category_file(logger.log_categories[category] as datum_log_category);
            Waiting.NoSleep(Dm.Call(this, nameof(write_entry_to_file), Dm.arglist(args)));
            return;
        }
        write_entry_to_file__in_error_recovery = FALSE;
        WRITE_LOG_NO_FORMAT(file, Dm.text("[]\n", to_json_text()));
    }

    /// Writes the log entry to a file as a human-readable string.
    // tg: code/modules/logging/log_entry.dm:119
    public virtual void write_readable_entry_to_file(object file = null, float? format_internally = null, params object[] __extra)
    {
        format_internally ??= TRUE;
        // CHECK_AND_TRY_FILE_ERROR_RECOVERY(file) (:98-111, file-local), expanded inline (rules 11.1 (c), (d), (f)).
        DmList args = Dm.args(file, format_internally, __extra);
        if (!Dm.truthy(fexists(file)))
        {
            if (Dm.truthy(write_readable_entry_to_file__in_error_recovery))
            {
                write_readable_entry_to_file__in_error_recovery = FALSE;
                throw Dm.CRASH(Dm.text("Failed to error recover log file: []", file), "code/modules/logging/log_entry.dm", 120);
            }
            write_readable_entry_to_file__in_error_recovery = TRUE;
            logger.Log(LOG_CATEGORY_INTERNAL_ERROR, Dm.text("attempting to perform file error recovery: []", file));
            logger.init_category_file(logger.log_categories[category] as datum_log_category);
            Waiting.NoSleep(Dm.Call(this, nameof(write_readable_entry_to_file), Dm.arglist(args)));
            return;
        }
        write_readable_entry_to_file__in_error_recovery = FALSE;
        // If it's being formatted internally we need to manually add a newline
        if (Dm.truthy(format_internally))
            WRITE_LOG_NO_FORMAT(file, Dm.text("[]\n", to_readable_text(format: TRUE)));
        else
            WRITE_LOG(file, Dm.text("[]", to_readable_text(format: FALSE)));
    }
}
