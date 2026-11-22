// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/passive_vent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the passive vents): the whole file. The var block is generated.

// tg: code/modules/atmospherics/machinery/components/unary_devices/passive_vent.dm:1
public partial class obj_machinery_atmospherics_components_unary_passive_vent : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/passive_vent.dm:18
    public override void update_icon_nopipes(params object[] __extra)
    {
        cut_overlays();
        if (Dm.truthy(underfloor_state))
        {
            image cap = get_pipe_image(icon, "vent_cap", initialize_directions, pipe_color) as image;
            cap.appearance_flags = Dm.bitor(cap.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
            add_overlay(cap);
        }
        else
            PIPING_LAYER_SHIFT(this, PIPING_LAYER_DEFAULT);
        icon_state = "passive_vent";
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/passive_vent.dm:28
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        turf location = get_turf(loc);
        if (isclosedturf(location))
            return dot.Value;

        datum_gas_mixture external = await location.return_air() as datum_gas_mixture;
        datum_gas_mixture @internal = airs[1] as datum_gas_mixture;

        if (!Dm.truthy(@internal.volume) || !Dm.truthy(external.volume))
            return dot.Value;

        float before = Dm.num(external.total_moles()); // DEVIATION(DV34): what the equalize moved, for the clients' vent streams
        if (Dm.truthy(@internal.equalize(external)))
        {
            AirflowSeam.VentMoles(this, Dm.num(external.total_moles()) - before, external.temperature, external); // DEVIATION(DV34)
            air_update_turf(FALSE, FALSE);
            update_parents();
        }
        return dot.Value;
    }
}
