// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/mecha.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: log_mecha (D325: /atom/proc/log_message dispatches to it); the equipment's log_message override is in
// mecha_equipment.cs.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/logging/mecha.dm:2
    public static void log_mecha(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_MECHA, text, data);
    }
}
