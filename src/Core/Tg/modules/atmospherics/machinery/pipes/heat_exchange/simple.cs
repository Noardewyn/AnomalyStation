// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/heat_exchange/simple.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the heat-exchanging pipes): the whole file. The var block is generated.

// tg: code/modules/atmospherics/machinery/pipes/heat_exchange/simple.dm:1
public partial class obj_machinery_atmospherics_pipe_heat_exchanging_simple : obj_machinery_atmospherics_pipe_heat_exchanging
{
    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/simple.dm:17
    public override void set_init_directions(object init_dir = null)
    {
        if (Dm.truthy(ISDIAGONALDIR(dir)))
        {
            initialize_directions = dir;
            return;
        }
        float sw__1 = dir;
        if (sw__1 == NORTH || sw__1 == SOUTH)
            initialize_directions = Dm.bitor(SOUTH, NORTH);
        else if (sw__1 == EAST || sw__1 == WEST)
            initialize_directions = Dm.bitor(EAST, WEST);
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/simple.dm:27
    public override void update_pipe_icon()
    {
        icon_state = Dm.text("pipe[][]-[]", Dm.truthy(nodes[1]) ? "1" : "0", Dm.truthy(nodes[2]) ? "1" : "0", piping_layer);
        return;
    }
}
