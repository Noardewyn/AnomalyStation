// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/melee.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only HITS_TO_CRIT (M7x slice E: the baton's readout); the object-like #defines are emitted by the defines converter.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/melee.dm:17  #define HITS_TO_CRIT(damage) round(100 / (damage), 0.1)
    public static float HITS_TO_CRIT(float damage) => round(100 / damage, 0.1f);
}
