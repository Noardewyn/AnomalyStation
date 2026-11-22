// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/projectiles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only RETURN_PRECISE_POINT translated (the M7 projectiles slice: the hitscan tracer's points and homing); the object-like
// #defines are emitted by the defines converter.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/projectiles.dm:82  #define RETURN_PRECISE_POINT(A) new /datum/point(A)
    public static datum_point RETURN_PRECISE_POINT(object A) => Dm.@new<datum_point>(A);
}
