// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/transport.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z, elevators): the whole file.

public static partial class GlobalProcs
{
    /// Logging for transport (tram/elevator) actions
    // tg: code/__HELPERS/logging/transport.dm:2
    public static void log_transport(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_TRANSPORT, text, data);
    }
}
