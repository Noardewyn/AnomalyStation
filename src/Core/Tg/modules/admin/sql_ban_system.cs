// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/sql_ban_system.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON (M10 slice F): is_banned_from's signature for the job checks and the late join. M10 slice I: the server bans are the host's
// (ServerHost.Admin.cs: world/IsBanned, create_ban, unban; SQL is rules 13); the role bans this proc reads are not ported (DV194), so
// nobody is role banned, as tg answers without a database.

public static partial class GlobalProcs
{
    // tg: code/modules/admin/sql_ban_system.dm:17
    public static object is_banned_from(object player_key = null, object roles = null)
    {
        if (!Dm.truthy(player_key))
        {
            stack_trace("Called is_banned_from without specifying a ckey.", "code/modules/admin/sql_ban_system.dm", 19);
            return FALSE;
        }
        // the ban cache of a connected player (retrieve_ban_cache) and the database's ban table: the role bans (not ported, DV194)
        return FALSE;
    }
}
