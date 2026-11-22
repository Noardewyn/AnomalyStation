// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/atmospherics/atmos_mob_interaction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the function-like macro QUANTIZE translated (M2 P2: the gas mixture's remove and adjust procs use it); the
// constants of atmos_mob_interaction.dm are generated into Defines.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/atmospherics/atmos_mob_interaction.dm:14  #define QUANTIZE(variable) (round((variable), (MOLAR_ACCURACY)))
    public static float QUANTIZE(object variable) => round(Dm.num(variable), MOLAR_ACCURACY);
}
