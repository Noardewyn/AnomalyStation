// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/antagonists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file (the antagonists' log categories; the uplink's purchases log through log_uplink).

public static partial class GlobalProcs
{
    /// Logging for traitor objectives
    // tg: code/__HELPERS/logging/antagonists.dm:2
    public static void log_traitor(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_TRAITOR, text, data);
    }

    /// Logging for items purchased from a traitor uplink
    // tg: code/__HELPERS/logging/antagonists.dm:6
    public static void log_uplink(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_UPLINK, text, data);
    }

    /// Logging for upgrades purchased by a malfunctioning (or combat upgraded) AI
    // tg: code/__HELPERS/logging/antagonists.dm:10
    public static void log_malf_upgrades(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_UPLINK_MALF, text, data);
    }

    /// Logging for changeling powers purchased
    // tg: code/__HELPERS/logging/antagonists.dm:14
    public static void log_changeling_power(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_UPLINK_CHANGELING, text, data);
    }

    /// Logging for heretic powers learned
    // tg: code/__HELPERS/logging/antagonists.dm:18
    public static void log_heretic_knowledge(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_UPLINK_HERETIC, text, data);
    }

    /// Logging for wizard powers learned
    // tg: code/__HELPERS/logging/antagonists.dm:22
    public static void log_spellbook(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_UPLINK_SPELL, text, data);
    }

    /// Logs bounties completed by spies and their rewards
    // tg: code/__HELPERS/logging/antagonists.dm:26
    public static void log_spy(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_UPLINK_SPY, text, data);
    }

    /// Logs blood worm events like entering/leaving a host.
    // tg: code/__HELPERS/logging/antagonists.dm:30
    public static void log_blood_worm(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_BLOOD_WORM, text, data);
    }
}
