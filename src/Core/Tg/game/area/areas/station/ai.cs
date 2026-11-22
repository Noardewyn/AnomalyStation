// Translated from tgstation (snapshot 1d99bd7f): code/game/area/areas/station/ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: gaps E1b (D327): the AI chamber's Initialize, which the grid check's power failure spares (block_area_power_fail); the
// file's other areas are var-only.

// tg: code/game/area/areas/station/ai.dm:43
public partial class area_station_ai_satellite_chamber : area_station_ai_satellite
{
    // tg: code/game/area/areas/station/ai.dm:48
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_block_area_power_fail)));
        return dot;
    }
}
