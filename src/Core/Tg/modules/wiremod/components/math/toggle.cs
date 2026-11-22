// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/toggle.dm
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
 * # Toggle Component
 *
 * Does a toggle between true and false on trigger
 */
// tg: code/modules/wiremod/components/math/toggle.dm:6
public partial class obj_item_circuit_component_compare_toggle : obj_item_circuit_component_compare
{
    // tg: code/modules/wiremod/components/math/toggle.dm:18
    public override async DmTask populate_custom_ports()
    {
        toggle_set = add_input_port("Set Toggle State", PORT_TYPE_BOOLEAN) as datum_port_input;
        toggle_and_compare = add_input_port("Toggle And Compare", PORT_TYPE_SIGNAL) as datum_port_input;
        toggle_state = FALSE;
    }

    // tg: code/modules/wiremod/components/math/toggle.dm:23
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (port == toggle_set)
        {
            toggle_state = Dm.num(Dm.truthy(port.value));
            return null;
        }
        if (COMPONENT_TRIGGERED_BY(toggle_and_compare, port))
        {
            toggle_state = Dm.not(toggle_state);
            if (Dm.truthy(toggle_state))
                @true.set_output(COMPONENT_SIGNAL);
            else
                @false.set_output(COMPONENT_SIGNAL);
        }
        return base.input_received(port, return_values);
    }

    // tg: code/modules/wiremod/components/math/toggle.dm:35
    public override object do_comparisons()
    {
        return toggle_state;
    }
}
