// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/sensors/tempsensor.dm
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
 * # Temperature Sensor
 *
 * Returns the temperature of the tile
 */
// tg: code/modules/wiremod/components/sensors/tempsensor.dm:6
public partial class obj_item_circuit_component_tempsensor : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/sensors/tempsensor.dm:16
    public override async DmTask populate_ports()
    {
        result = add_output_port("Result", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/sensors/tempsensor.dm:19
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        //Get current turf
        turf location = get_location() as turf;
        if (location == null)
        {
            result.set_output(null);
            return null;
        }
        //Get environment info
        datum_gas_mixture environment = Waiting.NoSleep(location.return_air()) as datum_gas_mixture;
        object total_moles = environment.total_moles();
        if (Dm.truthy(total_moles))
        {
            //If there's atmos, return temperature
            result.set_output(Dm.round(environment.temperature, 1));
        }
        else
            result.set_output(0);
        return null;
    }
}
