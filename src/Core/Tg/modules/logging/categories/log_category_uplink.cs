// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_uplink.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_uplink.dm:1
[DmPath("/datum/log_category/uplink")]
public partial class datum_log_category_uplink : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_uplink.dm:1
    public datum_log_category_uplink()
    {
        category = LOG_CATEGORY_UPLINK;
        config_flag = typeof(datum_config_entry_flag_log_uplink);
    }
}

// tg: code/modules/logging/categories/log_category_uplink.dm:5
[DmPath("/datum/log_category/uplink_malf")]
public partial class datum_log_category_uplink_malf : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_uplink.dm:5
    public datum_log_category_uplink_malf()
    {
        category = LOG_CATEGORY_UPLINK_MALF;
        config_flag = typeof(datum_config_entry_flag_log_uplink);
        master_category = typeof(datum_log_category_uplink);
    }
}

// tg: code/modules/logging/categories/log_category_uplink.dm:10
[DmPath("/datum/log_category/uplink_heretic")]
public partial class datum_log_category_uplink_heretic : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_uplink.dm:10
    public datum_log_category_uplink_heretic()
    {
        category = LOG_CATEGORY_UPLINK_HERETIC;
        config_flag = typeof(datum_config_entry_flag_log_uplink);
        master_category = typeof(datum_log_category_uplink);
    }
}

// tg: code/modules/logging/categories/log_category_uplink.dm:15
[DmPath("/datum/log_category/uplink_changeling")]
public partial class datum_log_category_uplink_changeling : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_uplink.dm:15
    public datum_log_category_uplink_changeling()
    {
        category = LOG_CATEGORY_UPLINK_CHANGELING;
        config_flag = typeof(datum_config_entry_flag_log_uplink);
        master_category = typeof(datum_log_category_uplink);
    }
}

// tg: code/modules/logging/categories/log_category_uplink.dm:20
[DmPath("/datum/log_category/uplink_spell")]
public partial class datum_log_category_uplink_spell : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_uplink.dm:20
    public datum_log_category_uplink_spell()
    {
        category = LOG_CATEGORY_UPLINK_SPELL;
        config_flag = typeof(datum_config_entry_flag_log_uplink);
        master_category = typeof(datum_log_category_uplink);
    }
}

// tg: code/modules/logging/categories/log_category_uplink.dm:25
[DmPath("/datum/log_category/uplink_spy")]
public partial class datum_log_category_uplink_spy : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_uplink.dm:25
    public datum_log_category_uplink_spy()
    {
        category = LOG_CATEGORY_UPLINK_SPY;
        config_flag = typeof(datum_config_entry_flag_log_uplink);
        master_category = typeof(datum_log_category_uplink);
    }
}
