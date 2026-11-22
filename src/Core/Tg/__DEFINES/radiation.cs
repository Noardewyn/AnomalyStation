// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/radiation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the function-like macros of radiation.dm (rules 11.1); its constants are generated into Defines.

public static partial class GlobalProcs
{
    /// Whether or not it's possible for this atom to be irradiated
    // tg: code/__DEFINES/radiation.dm:2
    public static bool CAN_IRRADIATE(object atom) => (ishuman(atom) || isitem(atom));

    /// Calculates the max chance for a radiation_pulse via a radioactive reagent
    // tg: code/__DEFINES/radiation.dm:5
    public static float CALCULATE_RAD_MAX_CHANCE(float rad_power) => (20 + (15 * (rad_power - 1)));
}
