// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_target_zone_switch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_target_zone_switch.dm:1
[DmPath("/datum/log_category/target_zone_switch")]
public partial class datum_log_category_target_zone_switch : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_target_zone_switch.dm:1
    public datum_log_category_target_zone_switch()
    {
        category = LOG_CATEGORY_TARGET_ZONE_SWITCH;
        config_flag = typeof(datum_config_entry_flag_log_zone_switch);
    }
}
