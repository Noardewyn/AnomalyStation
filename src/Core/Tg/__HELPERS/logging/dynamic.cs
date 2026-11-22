// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/dynamic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A3 (D316): the whole file.

public static partial class GlobalProcs
{
    /// Logging for dynamic procs
    // tg: code/__HELPERS/logging/dynamic.dm:2
    public static void log_dynamic(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_DYNAMIC, text, data);
    }
}
