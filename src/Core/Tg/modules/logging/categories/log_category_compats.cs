// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_compats.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_compats.dm:1
[DmPath("/datum/log_category/game_compat")]
public partial class datum_log_category_game_compat : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_compats.dm:1
    public datum_log_category_game_compat()
    {
        category = LOG_CATEGORY_COMPAT_GAME;
        master_category = typeof(datum_log_category_game);
        config_flag = typeof(datum_config_entry_flag_logging_compat_adminprivate);
    }
}

// tg: code/modules/logging/categories/log_category_compats.dm:6
[DmPath("/datum/config_entry/flag/logging_compat_adminprivate")]
public partial class datum_config_entry_flag_logging_compat_adminprivate : datum_config_entry_flag
{
    // tg: code/modules/logging/categories/log_category_compats.dm:6
    public datum_config_entry_flag_logging_compat_adminprivate()
    {
        @default = FALSE;
    }
}
