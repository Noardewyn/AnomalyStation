// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/health.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Get Health Component
 *
 * Return the health of a mob
 */
// tg: code/modules/wiremod/components/atom/health.dm:6
public partial class obj_item_circuit_component_health : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/health.dm:28
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles", max_range), "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/atom/health.dm:32
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Organism", PORT_TYPE_ATOM) as datum_port_input;

        brute = add_output_port("Brute Damage", PORT_TYPE_NUMBER) as datum_port_output;
        burn = add_output_port("Burn Damage", PORT_TYPE_NUMBER) as datum_port_output;
        toxin = add_output_port("Toxin Damage", PORT_TYPE_NUMBER) as datum_port_output;
        oxy = add_output_port("Suffocation Damage", PORT_TYPE_NUMBER) as datum_port_output;
        health = add_output_port("Overall Health", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/atom/health.dm:41
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        mob_living organism = input_port.value as mob_living;
        turf current_turf = get_location() as turf;
        turf target_location = get_turf(organism);
        if (!(organism is mob_living) || get_dist(current_turf, target_location) > max_range || current_turf.z != target_location.z)
        {
            brute.set_output(null);
            burn.set_output(null);
            toxin.set_output(null);
            oxy.set_output(null);
            health.set_output(null);
            return null;
        }

        brute.set_output(organism.get_brute_loss());
        burn.set_output(organism.get_fire_loss());
        toxin.set_output(organism.get_tox_loss());
        oxy.set_output(organism.get_oxy_loss());
        health.set_output(organism.health);
        return null;
    }
}
