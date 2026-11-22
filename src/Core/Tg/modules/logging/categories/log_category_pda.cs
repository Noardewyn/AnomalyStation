// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_pda.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_pda.dm:1
[DmPath("/datum/log_category/pda")]
public partial class datum_log_category_pda : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_pda.dm:1
    public datum_log_category_pda()
    {
        category = LOG_CATEGORY_PDA;
        config_flag = typeof(datum_config_entry_flag_log_pda);
    }
}

// tg: code/modules/logging/categories/log_category_pda.dm:5
[DmPath("/datum/log_category/pda_chat")]
public partial class datum_log_category_pda_chat : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_pda.dm:5
    public datum_log_category_pda_chat()
    {
        category = LOG_CATEGORY_PDA_CHAT;
        master_category = typeof(datum_log_category_pda);
    }
}

// tg: code/modules/logging/categories/log_category_pda.dm:9
[DmPath("/datum/log_category/pda_comment")]
public partial class datum_log_category_pda_comment : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_pda.dm:9
    public datum_log_category_pda_comment()
    {
        category = LOG_CATEGORY_PDA_COMMENT;
        master_category = typeof(datum_log_category_pda);
    }
}
