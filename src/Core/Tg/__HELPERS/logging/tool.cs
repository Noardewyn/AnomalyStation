// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/tool.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps A1 (D327): the whole file, the tool log.

public static partial class GlobalProcs
{
    /// Logging for tool usage
    // tg: code/__HELPERS/logging/tool.dm:2
    public static void log_tool(object text = null)
    {
        logger.Log(LOG_CATEGORY_TOOL, text);
    }
}
