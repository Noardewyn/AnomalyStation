// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_debug.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_debug.dm:1
[DmPath("/datum/log_category/debug")]
public partial class datum_log_category_debug : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_debug.dm:1
    public datum_log_category_debug()
    {
        category = LOG_CATEGORY_DEBUG;
    }
}

// tg: code/modules/logging/categories/log_category_debug.dm:4
[DmPath("/datum/log_category/debug_sql")]
public partial class datum_log_category_debug_sql : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_debug.dm:4
    public datum_log_category_debug_sql()
    {
        category = LOG_CATEGORY_DEBUG_SQL;
        master_category = typeof(datum_log_category_debug);
    }
}

// tg: code/modules/logging/categories/log_category_debug.dm:8
[DmPath("/datum/log_category/debug_lua")]
public partial class datum_log_category_debug_lua : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_debug.dm:8
    public datum_log_category_debug_lua()
    {
        category = LOG_CATEGORY_DEBUG_LUA;
        master_category = typeof(datum_log_category_debug);
    }
}

// tg: code/modules/logging/categories/log_category_debug.dm:12
[DmPath("/datum/log_category/debug_tts")]
public partial class datum_log_category_debug_tts : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_debug.dm:12
    public datum_log_category_debug_tts()
    {
        category = LOG_CATEGORY_DEBUG_TTS;
        master_category = typeof(datum_log_category_debug);
    }
}

// This is not in the debug master category on purpose, do not add it
// tg: code/modules/logging/categories/log_category_debug.dm:17
[DmPath("/datum/log_category/debug_runtime")]
public partial class datum_log_category_debug_runtime : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_debug.dm:17
    public datum_log_category_debug_runtime()
    {
        category = LOG_CATEGORY_RUNTIME;
    }
}

// tg: code/modules/logging/categories/log_category_debug.dm:20
[DmPath("/datum/log_category/debug_mapping")]
public partial class datum_log_category_debug_mapping : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_debug.dm:20
    public datum_log_category_debug_mapping()
    {
        category = LOG_CATEGORY_DEBUG_MAPPING;
        master_category = typeof(datum_log_category_debug);
    }
}

// tg: code/modules/logging/categories/log_category_debug.dm:24
[DmPath("/datum/log_category/debug_job")]
public partial class datum_log_category_debug_job : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_debug.dm:24
    public datum_log_category_debug_job()
    {
        category = LOG_CATEGORY_DEBUG_JOB;
        config_flag = typeof(datum_config_entry_flag_log_job_debug);
        master_category = typeof(datum_log_category_debug);
    }
}

// tg: code/modules/logging/categories/log_category_debug.dm:29
[DmPath("/datum/log_category/debug_mobtag")]
public partial class datum_log_category_debug_mobtag : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_debug.dm:29
    public datum_log_category_debug_mobtag()
    {
        category = LOG_CATEGORY_DEBUG_MOBTAG;
        master_category = typeof(datum_log_category_debug);
    }
}

// tg: code/modules/logging/categories/log_category_debug.dm:33
[DmPath("/datum/log_category/debug_asset")]
public partial class datum_log_category_debug_asset : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_debug.dm:33
    public datum_log_category_debug_asset()
    {
        category = LOG_CATEGORY_DEBUG_ASSET;
        config_flag = typeof(datum_config_entry_flag_log_asset);
        master_category = typeof(datum_log_category_debug);
    }
}
