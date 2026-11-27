// Translated from tgstation (snapshot 1d99bd7f): code/game/area/areas/station/service.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the bar's Initialize; the file's other areas are var-only.

// tg: code/game/area/areas/station/service.dm:35
public partial class area_station_service_bar : area_station_service
{
    // tg: code/game/area/areas/station/service.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.bar_areas, this);
        return dot;
    }
}
