// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/log_category.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// The main datum that contains all log entries for a category
// tg: code/modules/logging/log_category.dm:2
[DmPath("/datum/log_category")]
public partial class datum_log_category : datum
{
    /// The category name
    // tg: code/modules/logging/log_category.dm:4
    public object category;

    /// The schema version of this log category.
    /// Expected format of "Major.Minor.Patch"
    // tg: code/modules/logging/log_category.dm:8
    public string schema_version = LOG_CATEGORY_SCHEMA_VERSION_NOT_SET;

    /// The master category that contains this category
    // tg: code/modules/logging/log_category.dm:11
    // var/datum/log_category holds the master's typepath (the subtypes' defaults) and then its instance (log_holder.dm:247):
    // wider values, object (rules 3.2).
    public object master_category;

    /// Flags to apply to our /datum/log_entry's
    /// See code/__DEFINES/logging/dm
    // tg: code/modules/logging/log_category.dm:15
    public float entry_flags = NONE;

    /// If set this config flag is checked to enable this log category
    // tg: code/modules/logging/log_category.dm:18
    public Type config_flag;

    /// Whether or not this log should not be publically visible
    // tg: code/modules/logging/log_category.dm:21
    public float? secret = FALSE;

    /// The list of header information for this category. Used for log file re-initialization
    // tg: code/modules/logging/log_category.dm:24
    public DmList category_header;

    /// Whether the readable version of the log message is formatted internally instead of by rustg
    /// IF YOU CHANGE THIS VERIFY LOGS ARE STILL PARSED CORRECTLY
    // tg: code/modules/logging/log_category.dm:28
    public float internal_formatting = FALSE;

    /// List of log entries for this category
    // tg: code/modules/logging/log_category.dm:31
    public DmList entries;

    /// Total number of entries this round so far
    // tg: code/modules/logging/log_category.dm:34
    public float entry_count = 0;

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        entries = Dm.list();
    }

    // tg: GENERAL_PROTECT_DATUM(/datum/log_category) (:36): the VV and savefile guards are not translated (rules 13).

    /// Add an entry to this category. It is very important that any data you provide doesn't hold references to anything!
    // tg: code/modules/logging/log_category.dm:39
    public virtual void create_entry(object message = null, DmList data = null, DmList semver_store = null)
    {
        datum_log_entry entry = Dm.@new<datum_log_entry>(
            // world state contains raw timestamp
            Dm.named("timestamp", logger.human_readable_timestamp()),
            Dm.named("category", category),
            Dm.named("message", message),
            Dm.named("flags", entry_flags),
            Dm.named("data", data),
            Dm.named("semver_store", semver_store));

        write_entry(entry);
        entry_count += 1;
        if (entry_count <= CONFIG_MAX_CACHED_LOG_ENTRIES)
            Dm.append(ref entries, entry);
    }

    /// Allows for category specific file splitting. Needs to accept a null entry for the default file.
    /// If master_category it will always return the output of master_category.get_output_file(entry)
    // tg: code/modules/logging/log_category.dm:57
    // The slot types entry list/entry (slots.tsv: DmList), but write_entry passes the /datum/log_entry (:68, :70): a "-"
    // cell of rules 3.5, so the parameter is object (a decided slot parameter, reported for Appendix A).
    public virtual object get_output_file(object entry = null, string extension = null)
    {
        extension ??= "log.json";
        if (Dm.truthy(master_category))
            return ((datum_log_category)master_category).get_output_file(entry, extension);
        if (Dm.truthy(secret))
            return Dm.text("[]/secret/[].[]", GLOB.log_directory, category, extension);
        return Dm.text("[]/[].[]", GLOB.log_directory, category, extension);
    }

    /// Writes an entry to the output file(s) for the category
    // tg: code/modules/logging/log_category.dm:65
    public virtual void write_entry(datum_log_entry entry = null)
    {
        // config isn't loaded? assume we want human readable logs
        if (config == null || Dm.truthy(config.Get(typeof(datum_config_entry_flag_log_as_human_readable))))
            entry.write_readable_entry_to_file(get_output_file(entry, "log"), format_internally: internal_formatting);

        entry.write_entry_to_file(get_output_file(entry));
    }
}
