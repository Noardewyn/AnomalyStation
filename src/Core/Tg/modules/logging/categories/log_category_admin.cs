// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_admin.dm:1
[DmPath("/datum/log_category/admin")]
public partial class datum_log_category_admin : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_admin.dm:1
    public datum_log_category_admin()
    {
        category = LOG_CATEGORY_ADMIN;
        config_flag = typeof(datum_config_entry_flag_log_admin);
    }
}

// tg: code/modules/logging/categories/log_category_admin.dm:5
[DmPath("/datum/log_category/admin_dsay")]
public partial class datum_log_category_admin_dsay : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_admin.dm:5
    public datum_log_category_admin_dsay()
    {
        category = LOG_CATEGORY_ADMIN_DSAY;
        master_category = typeof(datum_log_category_admin);
        config_flag = typeof(datum_config_entry_flag_log_admin);
    }
}

// tg: code/modules/logging/categories/log_category_admin.dm:10
[DmPath("/datum/log_category/admin_circuit")]
public partial class datum_log_category_admin_circuit : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_admin.dm:10
    public datum_log_category_admin_circuit()
    {
        category = LOG_CATEGORY_ADMIN_CIRCUIT;
        master_category = typeof(datum_log_category_admin);
        config_flag = typeof(datum_config_entry_flag_log_admin);
    }
}

// private categories //
// tg: code/modules/logging/categories/log_category_admin.dm:17
[DmPath("/datum/log_category/admin_private")]
public partial class datum_log_category_admin_private : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_admin.dm:17
    public datum_log_category_admin_private()
    {
        category = LOG_CATEGORY_ADMIN_PRIVATE;
        config_flag = typeof(datum_config_entry_flag_log_admin);
        secret = TRUE;
    }
}

// tg: code/modules/logging/categories/log_category_admin.dm:22
[DmPath("/datum/log_category/admin_asay")]
public partial class datum_log_category_admin_asay : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_admin.dm:22
    public datum_log_category_admin_asay()
    {
        category = LOG_CATEGORY_ADMIN_PRIVATE_ASAY;
        master_category = typeof(datum_log_category_admin_private);
        config_flag = typeof(datum_config_entry_flag_log_adminchat);
        secret = TRUE;
    }
}
