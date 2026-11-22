// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_href.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_href.dm:1
[DmPath("/datum/log_category/href")]
public partial class datum_log_category_href : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_href.dm:1
    public datum_log_category_href()
    {
        category = LOG_CATEGORY_HREF;
    }
}

// tg: code/modules/logging/categories/log_category_href.dm:4
[DmPath("/datum/log_category/href_tgui")]
public partial class datum_log_category_href_tgui : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_href.dm:4
    public datum_log_category_href_tgui()
    {
        category = LOG_CATEGORY_HREF_TGUI;
        master_category = typeof(datum_log_category_href);
    }
}
