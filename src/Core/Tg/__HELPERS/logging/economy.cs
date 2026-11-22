// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/economy.dm
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
    // tg: code/__HELPERS/logging/economy.dm:1
    public static void log_econ(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_ECONOMY, text, data);
    }
}
