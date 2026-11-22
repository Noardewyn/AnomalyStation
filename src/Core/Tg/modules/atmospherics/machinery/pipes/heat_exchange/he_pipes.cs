// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/heat_exchange/he_pipes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the heat-exchanging pipes): the whole file; the hot pipe's glow (heat2colour and animate of the pipe's colour)
// since M4v slice 4. The var block is generated.

// tg: code/modules/atmospherics/machinery/pipes/heat_exchange/he_pipes.dm:1
public partial class obj_machinery_atmospherics_pipe_heat_exchanging : obj_machinery_atmospherics_pipe
{
    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/he_pipes.dm:13
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(add_atom_colour("#404040", FIXED_COLOUR_PRIORITY));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/he_pipes.dm:18
    [DmParams("target", "given_layer", "HE_type_check")]
    public override object is_connectable(obj_machinery_atmospherics target = null, object given_layer = null, params object[] __extra)
    {
        object HE_type_check = Dm.arg(__extra, 0);
        HE_type_check ??= TRUE;
        if ((istype(target, typeof(obj_machinery_atmospherics_pipe_heat_exchanging)) ? 1 : 0) != Dm.num(HE_type_check))
            return FALSE;
        return base.is_connectable(target, given_layer, Dm.forward(__extra, HE_type_check));
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/he_pipes.dm:23
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        float environment_temperature = 0;
        datum_gas_mixture pipe_air = await return_air() as datum_gas_mixture;

        turf local_turf = loc as turf;
        if (istype(local_turf, typeof(turf)))
        {
            if (islava(local_turf))
                environment_temperature = 5000; //Yuck
            else if (Dm.truthy(local_turf.blocks_air))
                environment_temperature = local_turf.temperature;
            else
            {
                turf_open open_local = local_turf as turf_open;
                environment_temperature = Dm.num(open_local.GetTemperature());
            }
        }
        else
            environment_temperature = local_turf.temperature;
        if (abs(environment_temperature - pipe_air.temperature) > minimum_temperature_difference)
            await parent.temperature_interact(local_turf, volume, thermal_conductivity);

        //heatup/cooldown any mobs buckled to ourselves based on our temperature
        if (Dm.truthy(has_buckled_mobs()))
        {
            float hc = Dm.num(pipe_air.heat_capacity());
            mob_living heat_source = buckled_mobs[1] as mob_living;
            //Best guess-estimate of the total bodytemperature of all the mobs, since they share the same environment it's ~ok~ to guess like this
            float avg_temp = (pipe_air.temperature * hc + (heat_source.bodytemperature * buckled_mobs.len) * 3500) / (hc + (buckled_mobs != null ? buckled_mobs.len * 3500 : 0));
            foreach (mob_living buckled_mob in Dm.each_cast<mob_living>(buckled_mobs))
                buckled_mob.bodytemperature = avg_temp;
            pipe_air.temperature = avg_temp;
        }
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/he_pipes.dm:52
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(seconds_per_tick, dot), dot); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick, DotBox<object> dot)
    {
        if (parent == null)
            return dot.Value; //machines subsystem fires before atmos is initialized so this prevents race condition runtimes

        datum_gas_mixture pipe_air = await return_air() as datum_gas_mixture;

        //Heat causes pipe to glow
        if (Dm.truthy(pipe_air.temperature) && (icon_temperature > 500 || pipe_air.temperature > 500)) //glow starts at 500K
        {
            if (abs(pipe_air.temperature - icon_temperature) > 10)
            {
                icon_temperature = pipe_air.temperature;

                float h_r = Dm.num(heat2colour_r(icon_temperature));
                float h_g = Dm.num(heat2colour_g(icon_temperature));
                float h_b = Dm.num(heat2colour_b(icon_temperature));

                if (icon_temperature < 2000)//scale glow until 2000K
                {
                    float scale = (icon_temperature - 500) / 1500;
                    h_r = 64 + (h_r - 64) * scale;
                    h_g = 64 + (h_g - 64) * scale;
                    h_b = 64 + (h_b - 64) * scale;
                }

                animate(this, Dm.named("color", rgb(h_r, h_g, h_b)), Dm.named("time", 20), Dm.named("easing", SINE_EASING));
            }
        }

        //burn any mobs buckled based on temperature
        if (!Dm.truthy(has_buckled_mobs()))
            return dot.Value;
        float heat_limit = 1000;
        if (pipe_air.temperature > heat_limit + 1)
        {
            foreach (mob_living buckled_mob in Dm.each_cast<mob_living>(buckled_mobs))
                await buckled_mob.apply_damage(Dm.num(seconds_per_tick) * 2 * log(pipe_air.temperature - heat_limit), BURN, BODY_ZONE_CHEST);
        }
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/he_pipes.dm:83
    public override void update_pipe_icon()
    {
        return;
    }
}
