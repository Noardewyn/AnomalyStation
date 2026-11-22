// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/paper.dm
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
    /// Logging for writing made on paper
    // tg: code/__HELPERS/logging/paper.dm:2
    public static void log_paper(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_PAPER, text, data);
    }
}
