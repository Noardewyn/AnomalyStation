// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/trinary_devices/trinary_devices.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the filters and mixers): the whole file. The var block is generated.

// tg: code/modules/atmospherics/machinery/components/trinary_devices/trinary_devices.dm:1
public partial class obj_machinery_atmospherics_components_trinary : obj_machinery_atmospherics_components
{
    // tg: code/modules/atmospherics/machinery/components/trinary_devices/trinary_devices.dm:15
    public override void set_init_directions(object init_dir = null)
    {
        float sw__1 = dir;
        if (sw__1 == NORTH)
            initialize_directions = Dm.bitor(Dm.bitor(EAST, NORTH), SOUTH);
        else if (sw__1 == SOUTH)
            initialize_directions = Dm.bitor(Dm.bitor(SOUTH, WEST), NORTH);
        else if (sw__1 == EAST)
            initialize_directions = Dm.bitor(Dm.bitor(EAST, WEST), SOUTH);
        else if (sw__1 == WEST)
            initialize_directions = Dm.bitor(Dm.bitor(WEST, NORTH), EAST);
    }

    /*
    Housekeeping and pipe network stuff
    */

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/trinary_devices.dm:30
    public override object get_node_connects()
    {
        //Mixer:
        //1 and 2 is input
        //Node 3 is output
        //If we flip the mixer, 1 and 3 shall exchange positions

        //Filter:
        //Node 1 is input
        //Node 2 is filtered output
        //Node 3 is rest output
        //If we flip the filter, 1 and 3 shall exchange positions

        float node1_connect = REVERSE_DIR(dir);
        float node2_connect = turn(dir, -90);
        float node3_connect = dir;

        if (Dm.truthy(flipped))
        {
            node1_connect = REVERSE_DIR(node1_connect);
            node3_connect = REVERSE_DIR(node3_connect);
        }

        return Dm.list(node1_connect, node2_connect, node3_connect);
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/trinary_devices.dm:53
    public virtual object set_overlay_offset(object pipe_layer = null)
    {
        float sw__1 = Dm.num(pipe_layer);
        if (sw__1 == 1)
            return 1;
        else if (sw__1 == 5)
            return 5;
        else
            return 0;
    }
}
