// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/heat_exchanger.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 4 (the engine's cooling loop): the whole file. The var block is generated.

// tg: code/modules/atmospherics/machinery/components/unary_devices/heat_exchanger.dm:1
public partial class obj_machinery_atmospherics_components_unary_heat_exchanger : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/heat_exchanger.dm:26
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("he[]", Dm.truthy(nodes[1]) ? 1 : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/heat_exchanger.dm:30
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon(updates, __extra);
        if (Dm.truthy(nodes[1]))
        {
            obj_machinery_atmospherics node = nodes[1] as obj_machinery_atmospherics;
            await add_atom_colour(node.color, FIXED_COLOUR_PRIORITY);
        }
        PIPING_LAYER_SHIFT(this, Dm.num(piping_layer));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/heat_exchanger.dm:37
    public override void atmos_init(DmList node_connects = null)
    {
        obj_machinery_atmospherics_components_unary_heat_exchanger partner = (partner_ref?.resolve()) as obj_machinery_atmospherics_components_unary_heat_exchanger;
        if (partner == null)
        {
            partner_ref = null;
            float partner_connect = REVERSE_DIR(dir);

            foreach (obj_machinery_atmospherics_components_unary_heat_exchanger target in Dm.each<obj_machinery_atmospherics_components_unary_heat_exchanger>(get_step(this, partner_connect)))
            {
                if (Dm.truthy(Dm.bitand(target.dir, get_dir(this, target))))
                {
                    partner_ref = WEAKREF(target) as datum_weakref;
                    target.partner_ref = WEAKREF(this) as datum_weakref;
                    break;
                }
            }
        }

        base.atmos_init(node_connects);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/heat_exchanger.dm:51
    public override object process_atmos(object seconds_per_tick = null)
    {
        obj_machinery_atmospherics_components_unary_heat_exchanger partner = (partner_ref?.resolve()) as obj_machinery_atmospherics_components_unary_heat_exchanger;
        if (partner == null)
        {
            partner_ref = null;
            return null;
        }
        if (Dm.le(SSair.times_fired, update_cycle))
            return null;

        update_cycle = SSair.times_fired;
        partner.update_cycle = SSair.times_fired;

        datum_gas_mixture air_contents = airs[1] as datum_gas_mixture;
        datum_gas_mixture partnerair_contents = partner.airs[1] as datum_gas_mixture;

        float air_heat_capacity = Dm.num(air_contents.heat_capacity());
        float other_air_heat_capacity = Dm.num(partnerair_contents.heat_capacity());
        float combined_heat_capacity = other_air_heat_capacity + air_heat_capacity;

        float old_temperature = air_contents.temperature;
        float other_old_temperature = partnerair_contents.temperature;

        if (combined_heat_capacity > 0)
        {
            float combined_energy = partnerair_contents.temperature * other_air_heat_capacity + air_heat_capacity * air_contents.temperature;

            float new_temperature = combined_energy / combined_heat_capacity;
            air_contents.temperature = new_temperature;
            partnerair_contents.temperature = new_temperature;
        }

        if (abs(old_temperature - air_contents.temperature) > 1)
            update_parents();

        if (abs(other_old_temperature - partnerair_contents.temperature) > 1)
            partner.update_parents();
        return null;
    }
}
