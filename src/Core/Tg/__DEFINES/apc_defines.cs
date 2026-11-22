// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/apc_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only APC_CHANNEL_IS_ON (M7 slice 3: the APC's process); the constants are the defines converter's.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/apc_defines.dm:39  #define APC_CHANNEL_IS_ON(channel) (channel >= APC_CHANNEL_ON)
    public static bool APC_CHANNEL_IS_ON(float channel) => channel >= APC_CHANNEL_ON;
}
