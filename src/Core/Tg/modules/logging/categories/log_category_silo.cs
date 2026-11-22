// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_silo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_silo.dm:1
[DmPath("/datum/log_category/silo")]
public partial class datum_log_category_silo : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_silo.dm:1
    public datum_log_category_silo()
    {
        category = LOG_CATEGORY_SILO;
    }
}
