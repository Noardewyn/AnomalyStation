// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// PARTIAL (M10x slice A): is_admin; alert_to_permissions_elevation_attempt (the admin logs) is not declared.

public static partial class GlobalProcs
{
    /// Returns if the given client is an admin, REGARDLESS of if they're deadminned or not.
    // tg: code/__HELPERS/admin.dm:2
    public static object is_admin(client client = null)
    {
        return GLOB.admin_datums[client.ckey] != null || GLOB.deadmins[client.ckey] != null ? TRUE : FALSE;
    }
}
