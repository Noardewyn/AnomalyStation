// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only log_admin and log_admin_private translated (M1 P1: the client layer's keysend flood kick logs with
// log_admin, bindings_client.dm:25, :33), log_suspicious_login (M10 slice I: the ban check at connect), and log_adminsay and log_dsay
// (D325: /atom/proc/log_message dispatches to them); the circuit logs wait for their tools.

/* Items with ADMINPRIVATE prefixed are stripped from public logs. */

// For backwards compatibility these are currently also added to LOG_CATEGORY_GAME with their respective prefix
// This is to avoid breaking any existing tools that rely on the old format, but should be removed in the future

public static partial class GlobalProcs
{
    /// General logging for admin actions
    // tg: code/__HELPERS/logging/admin.dm:6
    public static void log_admin(object text = null, DmList data = null)
    {
        GLOB.admin_activities.Add(text);
        logger.Log(LOG_CATEGORY_ADMIN, text, data);
        logger.Log(LOG_CATEGORY_COMPAT_GAME, Dm.text("ADMIN: []", text));
    }

    /// General logging for admin actions
    // tg: code/__HELPERS/logging/admin.dm:18
    public static void log_admin_private(object text = null, DmList data = null)
    {
        GLOB.admin_activities.Add(text);
        logger.Log(LOG_CATEGORY_ADMIN_PRIVATE, text, data);
        logger.Log(LOG_CATEGORY_COMPAT_GAME, Dm.text("ADMINPRIVATE: []", text));
    }

    /// Logging for AdminSay (ASAY) messages
    // tg: code/__HELPERS/logging/admin.dm:25
    public static void log_adminsay(object text = null, DmList data = null)
    {
        GLOB.admin_activities.Add(text);
        logger.Log(LOG_CATEGORY_ADMIN_PRIVATE_ASAY, text, data);
    }

    /// Logging for DeachSay (DSAY) messages
    // tg: code/__HELPERS/logging/admin.dm:31
    public static void log_dsay(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_ADMIN_DSAY, text, data);
        logger.Log(LOG_CATEGORY_COMPAT_GAME, Dm.text("ADMIN: DSAY: []", text));
    }

    /**
     * Writes to a special log file if the log_suspicious_login config flag is set,
     * which is intended to contain all logins that failed under suspicious circumstances.
     *
     * Mirrors this log entry to log_access when access_log_mirror is TRUE, so this proc
     * doesn't need to be used alongside log_access and can replace it where appropriate.
     */
    // tg: code/__HELPERS/logging/admin.dm:42  (M10 slice I)
    public static void log_suspicious_login(object text = null, DmList data = null, object access_log_mirror = null)
    {
        access_log_mirror ??= TRUE;
        logger.Log(LOG_CATEGORY_SUSPICIOUS_LOGIN, text);
        if (Dm.truthy(access_log_mirror))
            log_access(text);
    }
}
