// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/heat_exchange/junction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the heat-exchanging pipes): the whole file. The var block is generated.

// tg: code/modules/atmospherics/machinery/pipes/heat_exchange/junction.dm:1
public partial class obj_machinery_atmospherics_pipe_heat_exchanging_junction : obj_machinery_atmospherics_pipe_heat_exchanging
{
    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/junction.dm:20
    public override void set_init_directions(object init_dir = null)
    {
        float sw__1 = dir;
        if (sw__1 == NORTH || sw__1 == SOUTH)
            initialize_directions = Dm.bitor(SOUTH, NORTH);
        else if (sw__1 == EAST || sw__1 == WEST)
            initialize_directions = Dm.bitor(WEST, EAST);
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/junction.dm:27
    public override object get_node_connects()
    {
        return Dm.list(REVERSE_DIR(dir), dir);
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/junction.dm:30
    [DmParams("target", "given_layer", "he_type_check")]
    public override object is_connectable(obj_machinery_atmospherics target = null, object given_layer = null, params object[] __extra)
    {
        if (dir == get_dir(target, this))
            return base.is_connectable(target, given_layer, FALSE); //we want a normal pipe instead
        return base.is_connectable(target, given_layer, TRUE);
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/junction.dm:35
    public override void update_pipe_icon()
    {
        icon_state = Dm.text("pipe[][]-[]", Dm.truthy(nodes[1]) ? "1" : "0", Dm.truthy(nodes[2]) ? "1" : "0", piping_layer);
    }
}
