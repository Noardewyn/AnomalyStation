// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/atmos_computers/meters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 4 (the engine's cooling loop): the whole file, the meter the atmospherics control consoles read by its chamber_id (the
// consoles and the air sensors: package E5, _atmos_control.cs and _air_sensor.cs). The var blocks are generated.

// tg: code/game/machinery/computer/atmos_computers/meters.dm:1
public partial class obj_machinery_meter_monitored : obj_machinery_meter
{
    // tg: code/game/machinery/computer/atmos_computers/meters.dm:6
    [DmParams("mapload", "new_piping_layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_piping_layer = Dm.arg(__extra, 0);
        id_tag = assign_random_name();
        return base.Initialize(mapload, Dm.forward(__extra, new_piping_layer));
    }
}
