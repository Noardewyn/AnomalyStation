// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/atmos_computers/outlets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the atmos storage tanks' output inlets): the whole file. The var blocks are generated.

// tg: code/game/machinery/computer/atmos_computers/outlets.dm:1
public partial class obj_machinery_atmospherics_components_unary_vent_pump_siphon_monitored : obj_machinery_atmospherics_components_unary_vent_pump_siphon
{
    // tg: code/game/machinery/computer/atmos_computers/outlets.dm:5
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //we dont want people messing with these special vents using the air alarm interface
        disconnect_from_area();
        return dot;
    }
}

// Same as the rest, but bigger volume.
// tg: code/game/machinery/computer/atmos_computers/outlets.dm:83
public partial class obj_machinery_atmospherics_components_unary_vent_pump_high_volume_siphon_monitored : obj_machinery_atmospherics_components_unary_vent_pump_high_volume_siphon
{
    // tg: code/game/machinery/computer/atmos_computers/outlets.dm:88
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //we dont want people messing with these special vents using the air alarm interface
        disconnect_from_area();
        return dot;
    }
}
