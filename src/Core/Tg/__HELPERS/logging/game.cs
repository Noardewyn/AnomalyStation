// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/game.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    /// Logging for generic/unsorted game messages
    // tg: code/__HELPERS/logging/game.dm:2
    public static void log_game(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME, text, data);
    }

    /// Logging for emotes
    // tg: code/__HELPERS/logging/game.dm:6
    public static void log_emote(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_EMOTE, text, data);
    }

    /// Logging for emotes sent over the radio
    // tg: code/__HELPERS/logging/game.dm:10
    public static void log_radio_emote(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_RADIO_EMOTE, text, data);
    }

    /// Logging for messages sent in OOC
    // tg: code/__HELPERS/logging/game.dm:14
    public static void log_ooc(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_OOC, text, data);
    }

    /// Logging for prayed messages
    // tg: code/__HELPERS/logging/game.dm:18
    public static void log_prayer(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_PRAYER, text, data);
    }

    /// Logging for music requests
    // tg: code/__HELPERS/logging/game.dm:22
    public static void log_internet_request(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_INTERNET_REQUEST, text, data);
    }

    /// Logging for logging in & out of the game, with error messages.
    // tg: code/__HELPERS/logging/game.dm:26
    public static void log_access(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_ACCESS, text, data);
    }

    /// Logging for OOC votes
    // tg: code/__HELPERS/logging/game.dm:30
    public static void log_vote(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_VOTE, text, data);
    }

    // tg: code/__HELPERS/logging/game.dm:33
    public static void log_ghost_poll(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_GHOST_POLLS, text, data);
    }

    /// Logging for drawing on minimap
    // tg: code/__HELPERS/logging/game.dm:37
    public static void log_minimap_drawing(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_MINIMAP_DRAWING, text, data);
    }
}
