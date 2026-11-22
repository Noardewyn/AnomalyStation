// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/atmospherics/atmos_mapping_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only TURF_TEMPERATURE translated (the M7 supermatter slice, part 2: the pyroclastic anomaly's gas, anomalies_pyroclastic.dm:35);
// the gas strings of the file are generated into Defines.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/atmospherics/atmos_mapping_helpers.dm:3  #define TURF_TEMPERATURE(temperature) "TEMP=[temperature]"
    public static string TURF_TEMPERATURE(object temperature) => Dm.text("TEMP=[]", temperature);
}
