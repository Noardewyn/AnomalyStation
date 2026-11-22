// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/pda.dm
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
    // tg: code/__HELPERS/logging/pda.dm:2
    public static void log_pda(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_PDA, text, data);
    }

    // tg: code/__HELPERS/logging/pda.dm:6
    public static void log_comment(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_PDA_COMMENT, text, data);
    }

    // tg: code/__HELPERS/logging/pda.dm:10
    public static void log_chat(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_PDA_CHAT, text, data);
    }
}
