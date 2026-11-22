// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/portables_connector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the connector ports): the whole file. The var block is generated.

// tg: code/modules/atmospherics/machinery/components/unary_devices/portables_connector.dm:1
public partial class obj_machinery_atmospherics_components_unary_portables_connector : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/portables_connector.dm:23
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_gas_mixture air_contents = airs[1] as datum_gas_mixture;
        air_contents.volume = 0;
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/portables_connector.dm:28
    public override object Destroy(float? force = null)
    {
        if (connected_device != null)
            Waiting.NoSleep(connected_device.disconnect());
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/portables_connector.dm:33
    public override void update_icon_nopipes(params object[] __extra)
    {
        cut_overlays();
        if (Dm.truthy(underfloor_state))
        {
            image cap = get_pipe_image(icon, "connector_cap", initialize_directions, pipe_color) as image;
            cap.appearance_flags = Dm.bitor(cap.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
            add_overlay(cap);
        }
        else
            PIPING_LAYER_SHIFT(this, PIPING_LAYER_DEFAULT);
        icon_state = "connector";
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/portables_connector.dm:43
    public override object process_atmos(object seconds_per_tick = null)
    {
        if (connected_device == null)
            return null;
        update_parents();
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/portables_connector.dm:48
    public override object return_airs_for_reconcilation(datum_pipeline requester = null)
    {
        object dot = base.return_airs_for_reconcilation(requester);
        if (connected_device == null)
            return dot;
        DmList list__1 = dot as DmList;
        Dm.append(ref list__1, Waiting.NoSleep(connected_device.return_air()));
        return list__1;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/portables_connector.dm:54
    public override object can_unwrench__proc(mob user = null)
    {
        object dot = base.can_unwrench__proc(user);
        if (Dm.truthy(dot) && connected_device != null)
        {
            to_chat(user, span_warning(Dm.text("You cannot unwrench [], detach [] first!", this, connected_device)));
            return FALSE;
        }
        return dot;
    }
}
