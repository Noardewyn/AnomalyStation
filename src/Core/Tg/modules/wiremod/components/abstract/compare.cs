// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/abstract/compare.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

/**
 * # Compare Component
 *
 * Abstract component to build conditional components
 */
// tg: code/modules/wiremod/components/abstract/compare.dm:6
public partial class obj_item_circuit_component_compare : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/abstract/compare.dm:21
    public override async DmTask populate_ports()
    {
        await populate_custom_ports();
        compare = add_input_port("Compare", PORT_TYPE_SIGNAL) as datum_port_input;

        @true = add_output_port("True", PORT_TYPE_SIGNAL) as datum_port_output;
        @false = add_output_port("False", PORT_TYPE_SIGNAL) as datum_port_output;
        result = add_output_port("Result", result_port_type) as datum_port_output;
    }

    /**
     * Used by derivatives to load their own ports in for custom use.
     */
    // tg: code/modules/wiremod/components/abstract/compare.dm:32
    public virtual async DmTask populate_custom_ports()
    {
        return;
    }

    // tg: code/modules/wiremod/components/abstract/compare.dm:35
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object logic_result = do_comparisons();
        if (COMPONENT_TRIGGERED_BY(compare, port))
        {
            if (Dm.truthy(logic_result))
                @true.set_output(COMPONENT_SIGNAL);
            else
                @false.set_output(COMPONENT_SIGNAL);
        }
        result.set_output(logic_result);
        return null;
    }

    /// Do the comparisons and return a result
    // tg: code/modules/wiremod/components/abstract/compare.dm:46
    public virtual object do_comparisons()
    {
        return FALSE;
    }
}
