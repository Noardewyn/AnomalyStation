// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/dp_vent_pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the incinerators' dual-port vents): the whole file. The var blocks are generated.

//Acts like a normal vent, but has an input AND output.

///pressure_checks defines for external_pressure_bound and input_pressure_min
// tg: code/modules/atmospherics/machinery/components/binary_devices/dp_vent_pump.dm:3  #define OUTPUT_MAX 4 (file-local, #undef at :163; tg reads it nowhere)

// tg: code/modules/atmospherics/machinery/components/binary_devices/dp_vent_pump.dm:5
public partial class obj_machinery_atmospherics_components_binary_dp_vent_pump : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/dp_vent_pump.dm:28
    public override void update_icon_nopipes(params object[] __extra)
    {
        cut_overlays();
        if (Dm.truthy(underfloor_state))
        {
            image cap = get_pipe_image(icon, "dpvent_cap", dir, pipe_color, piping_layer: Dm.num(piping_layer)) as image;
            cap.appearance_flags = Dm.bitor(cap.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
            add_overlay(cap);
        }

        if (!Dm.truthy(on) || !Dm.truthy(is_operational))
            icon_state = "vent_off";
        else
            icon_state = Dm.truthy(pump_direction) ? "vent_out" : "vent_in";
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/dp_vent_pump.dm:40
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        if (!Dm.truthy(on))
            return dot.Value;
        turf location = get_turf(loc);
        if (isclosedturf(location))
            return dot.Value;

        datum_gas_mixture air1 = airs[1] as datum_gas_mixture;
        datum_gas_mixture air2 = airs[2] as datum_gas_mixture;

        datum_gas_mixture environment = await loc.return_air() as datum_gas_mixture;
        float environment_pressure = Dm.num(environment.return_pressure());

        if (Dm.truthy(pump_direction)) //input -> external
        {
            float pressure_delta = 10000;

            if (Dm.truthy(Dm.bitand(pressure_checks, ATMOS_EXTERNAL_BOUND)))
                pressure_delta = min(pressure_delta, (external_pressure_bound - environment_pressure));
            if (Dm.truthy(Dm.bitand(pressure_checks, ATMOS_INTERNAL_BOUND)))
                pressure_delta = min(pressure_delta, (Dm.num(air1.return_pressure()) - input_pressure_min));

            if (pressure_delta <= 0)
                return dot.Value;

            if (air1.temperature <= 0)
                return dot.Value;

            float transfer_moles = (pressure_delta * Dm.num(environment.volume)) / (air1.temperature * R_IDEAL_GAS_EQUATION);

            datum_gas_mixture removed = air1.remove(transfer_moles) as datum_gas_mixture;
            //Removed can be null if there is no atmosphere in air1
            if (removed == null)
                return dot.Value;

            await loc.assume_air(removed);

            datum_pipeline parent1 = parents[1] as datum_pipeline;
            parent1.update = TRUE;
        }
        else //external -> output
        {
            float pressure_delta = 10000;

            if (Dm.truthy(Dm.bitand(pressure_checks, ATMOS_EXTERNAL_BOUND)))
                pressure_delta = min(pressure_delta, (environment_pressure - external_pressure_bound));
            if (Dm.truthy(Dm.bitand(pressure_checks, ATMOS_INTERNAL_BOUND)))
                pressure_delta = min(pressure_delta, (output_pressure_max - Dm.num(air2.return_pressure())));

            if (pressure_delta <= 0)
                return dot.Value;

            if (environment.temperature <= 0)
                return dot.Value;

            float transfer_moles = (pressure_delta * Dm.num(air2.volume)) / (environment.temperature * R_IDEAL_GAS_EQUATION);

            datum_gas_mixture removed = loc.remove_air(transfer_moles) as datum_gas_mixture;
            //removed can be null if there is no air in the location
            if (removed == null)
                return dot.Value;

            air2.merge(removed);

            datum_pipeline parent2 = parents[2] as datum_pipeline;
            parent2.update = TRUE;
        }
        return dot.Value;
    }
}

// tg: code/modules/atmospherics/machinery/components/binary_devices/dp_vent_pump.dm:102
public partial class obj_machinery_atmospherics_components_binary_dp_vent_pump_high_volume : obj_machinery_atmospherics_components_binary_dp_vent_pump
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/dp_vent_pump.dm:105
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_gas_mixture air1 = airs[1] as datum_gas_mixture;
        datum_gas_mixture air2 = airs[2] as datum_gas_mixture;
        air1.volume = 1000;
        air2.volume = 1000;
        return dot;
    }
}
