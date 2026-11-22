// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_internal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_internal.dm:1
[DmPath("/datum/log_category/internal")]
public partial class datum_log_category_internal : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_internal.dm:1
    public datum_log_category_internal()
    {
        category = LOG_CATEGORY_INTERNAL_ERROR;
    }
}

// tg: code/modules/logging/categories/log_category_internal.dm:4
[DmPath("/datum/log_category/internal_unknown_category")]
public partial class datum_log_category_internal_unknown_category : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_internal.dm:4
    public datum_log_category_internal_unknown_category()
    {
        category = LOG_CATEGORY_INTERNAL_CATEGORY_NOT_FOUND;
        master_category = typeof(datum_log_category_internal);
    }
}
