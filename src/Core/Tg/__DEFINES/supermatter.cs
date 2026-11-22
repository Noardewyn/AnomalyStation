// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/supermatter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only SM_HALLUCINATION_RANGE translated (the M7 supermatter slice); the constants of
// supermatter.dm are generated into Defines (SM_DELAM_STRATEGY_PURGE, a null, is written null at its uses).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/supermatter.dm:51  #define SM_HALLUCINATION_RANGE(P) (min(7, round(P ** 0.25)))
    public static float SM_HALLUCINATION_RANGE(float P) => (min(7, round(Dm.pow(P, 0.25f))));
}
