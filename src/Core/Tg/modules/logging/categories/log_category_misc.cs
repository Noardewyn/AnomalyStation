// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_misc.dm:1
[DmPath("/datum/log_category/virus")]
public partial class datum_log_category_virus : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:1
    public datum_log_category_virus()
    {
        category = LOG_CATEGORY_VIRUS;
        config_flag = typeof(datum_config_entry_flag_log_virus);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:5
[DmPath("/datum/log_category/tool")]
public partial class datum_log_category_tool : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:5
    public datum_log_category_tool()
    {
        category = LOG_CATEGORY_TOOL;
        config_flag = typeof(datum_config_entry_flag_log_tools);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:9
[DmPath("/datum/log_category/mecha")]
public partial class datum_log_category_mecha : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:9
    public datum_log_category_mecha()
    {
        category = LOG_CATEGORY_MECHA;
        config_flag = typeof(datum_config_entry_flag_log_mecha);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:13
[DmPath("/datum/log_category/silicon")]
public partial class datum_log_category_silicon : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:13
    public datum_log_category_silicon()
    {
        category = LOG_CATEGORY_SILICON;
        config_flag = typeof(datum_config_entry_flag_log_silicon);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:17
[DmPath("/datum/log_category/paper")]
public partial class datum_log_category_paper : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:17
    public datum_log_category_paper()
    {
        category = LOG_CATEGORY_PAPER;
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:20
[DmPath("/datum/log_category/attack")]
public partial class datum_log_category_attack : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:20
    public datum_log_category_attack()
    {
        category = LOG_CATEGORY_ATTACK;
        config_flag = typeof(datum_config_entry_flag_log_attack);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:24
[DmPath("/datum/log_category/economy")]
public partial class datum_log_category_economy : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:24
    public datum_log_category_economy()
    {
        category = LOG_CATEGORY_ECONOMY;
        config_flag = typeof(datum_config_entry_flag_log_econ);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:28
[DmPath("/datum/log_category/manifest")]
public partial class datum_log_category_manifest : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:28
    public datum_log_category_manifest()
    {
        category = LOG_CATEGORY_MANIFEST;
        config_flag = typeof(datum_config_entry_flag_log_manifest);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:32
[DmPath("/datum/log_category/shuttle")]
public partial class datum_log_category_shuttle : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:32
    public datum_log_category_shuttle()
    {
        category = LOG_CATEGORY_SHUTTLE;
        config_flag = typeof(datum_config_entry_flag_log_shuttle);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:36
[DmPath("/datum/log_category/supicious_login")]
public partial class datum_log_category_supicious_login : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:36
    public datum_log_category_supicious_login()
    {
        category = LOG_CATEGORY_SUSPICIOUS_LOGIN;
        config_flag = typeof(datum_config_entry_flag_log_suspicious_login);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:40
[DmPath("/datum/log_category/config")]
public partial class datum_log_category_config : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:40
    public datum_log_category_config()
    {
        category = LOG_CATEGORY_CONFIG;
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:43
[DmPath("/datum/log_category/filter")]
public partial class datum_log_category_filter : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:43
    public datum_log_category_filter()
    {
        category = LOG_CATEGORY_FILTER;
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:46
[DmPath("/datum/log_category/signal")]
public partial class datum_log_category_signal : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:46
    public datum_log_category_signal()
    {
        category = LOG_CATEGORY_SIGNAL;
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:49
[DmPath("/datum/log_category/dynamic")]
public partial class datum_log_category_dynamic : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:49
    public datum_log_category_dynamic()
    {
        category = LOG_CATEGORY_DYNAMIC;
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:52
[DmPath("/datum/log_category/telecomms")]
public partial class datum_log_category_telecomms : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:52
    public datum_log_category_telecomms()
    {
        category = LOG_CATEGORY_TELECOMMS;
        config_flag = typeof(datum_config_entry_flag_log_telecomms);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:56
[DmPath("/datum/log_category/transport")]
public partial class datum_log_category_transport : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:56
    public datum_log_category_transport()
    {
        category = LOG_CATEGORY_TRANSPORT;
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:59
[DmPath("/datum/log_category/speech_indicator")]
public partial class datum_log_category_speech_indicator : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:59
    public datum_log_category_speech_indicator()
    {
        category = LOG_CATEGORY_SPEECH_INDICATOR;
        config_flag = typeof(datum_config_entry_flag_log_speech_indicators);
    }
}

// Logs seperately, printed into on server shutdown to store hard deletes and such
// tg: code/modules/logging/categories/log_category_misc.dm:64
[DmPath("/datum/log_category/qdel")]
public partial class datum_log_category_qdel : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:64
    public datum_log_category_qdel()
    {
        category = LOG_CATEGORY_QDEL;
        // We want this human readable so it's easy to see at a glance
        entry_flags = ENTRY_USE_DATA_W_READABLE;
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:69
[DmPath("/datum/log_category/cave_generation")]
public partial class datum_log_category_cave_generation : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:69
    public datum_log_category_cave_generation()
    {
        category = LOG_CATEGORY_CAVE_GENERATION;
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:72
[DmPath("/datum/log_category/fishing")]
public partial class datum_log_category_fishing : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:72
    public datum_log_category_fishing()
    {
        category = LOG_CATEGORY_FISHING;
        config_flag = typeof(datum_config_entry_flag_fishing);
    }
}

// tg: code/modules/logging/categories/log_category_misc.dm:76
[DmPath("/datum/log_category/image")]
public partial class datum_log_category_image : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_misc.dm:76
    public datum_log_category_image()
    {
        category = LOG_CATEGORY_IMAGE;
        config_flag = typeof(datum_config_entry_flag_log_image);
        secret = TRUE;
    }
}
