// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/binary_devices.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the station's pumps and valves): the whole file. The var block is generated.

// tg: code/modules/atmospherics/machinery/components/binary_devices/binary_devices.dm:1
public partial class obj_machinery_atmospherics_components_binary : obj_machinery_atmospherics_components
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/binary_devices.dm:12
    public override void set_init_directions(object init_dir = null)
    {
        float sw__1 = dir;
        if (sw__1 == NORTH || sw__1 == SOUTH)
            initialize_directions = Dm.bitor(NORTH, SOUTH);
        else if (sw__1 == EAST || sw__1 == WEST)
            initialize_directions = Dm.bitor(EAST, WEST);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/binary_devices.dm:19
    public override object get_node_connects()
    {
        return Dm.list(REVERSE_DIR(dir), dir);
    }

    /**
     * Used by binary devices to set what the offset will be for each layer, called in update_icon_nopipes()
     * Arguments:
     * * -pipe_layer: is the pipe layer the component should be set to
     */
    // tg: code/modules/atmospherics/machinery/components/binary_devices/binary_devices.dm:27
    public virtual object set_overlay_offset(object pipe_layer = null)
    {
        float sw__1 = Dm.num(pipe_layer);
        if (sw__1 == 1 || sw__1 == 3 || sw__1 == 5)
            return 1;
        else if (sw__1 == 2 || sw__1 == 4)
            return 2;
        return null;
    }
}
