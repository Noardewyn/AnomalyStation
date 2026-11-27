// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/species.dm
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
 * # Get Species Component
 *
 * Return the species of a mob
 */
// tg: code/modules/wiremod/components/atom/species.dm:6
public partial class obj_item_circuit_component_species : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/species.dm:19
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Organism", PORT_TYPE_ATOM) as datum_port_input;

        output = add_output_port("Species", PORT_TYPE_STRING) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/atom/species.dm:24
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        mob_living_carbon_human human = input_port.value as mob_living_carbon_human;
        if (!(human is mob_living_carbon_human) || !Dm.truthy(human.has_dna()))
        {
            output.set_output(null);
            return null;
        }

        output.set_output(((datum_species)human.dna.species).name);
        return null;
    }
}
