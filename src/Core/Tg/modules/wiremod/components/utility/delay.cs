// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/delay.dm
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
 * # Delay Component
 *
 * Delays a signal by a specified duration.
 */
// tg: code/modules/wiremod/components/utility/delay.dm:9
public partial class obj_item_circuit_component_delay : obj_item_circuit_component
{
    /// The minimum delay value that the delay component can have.
    // tg: code/modules/wiremod/components/utility/delay.dm:2  (#undef at :51)
    private const float COMP_DELAY_MIN_VALUE = 0.1f;

    // tg: code/modules/wiremod/components/utility/delay.dm:26
    public override async DmTask populate_ports()
    {
        delay_amount = add_input_port("Delay", PORT_TYPE_NUMBER, trigger: null) as datum_port_input;
        trigger = add_input_port("Trigger", PORT_TYPE_SIGNAL, trigger: nameof(trigger_delay)) as datum_port_input;
        interrupt = add_input_port("Interrupt", PORT_TYPE_SIGNAL, trigger: nameof(interrupt_timer)) as datum_port_input;

        output = add_output_port("Result", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/utility/delay.dm:33
    [ShouldNotSleep]
    public virtual void trigger_delay(datum_port_input port = null)
    {
        object delay = delay_amount.value;
        if (Dm.gt(delay, COMP_DELAY_MIN_VALUE))
        {
            // Convert delay into deciseconds
            timer_id = _addtimer(Dm.@new<datum_callback>(output, nameof(datum_port_output.set_output), trigger.value), Dm.num(delay) * 10, TIMER_UNIQUE | TIMER_STOPPABLE | TIMER_OVERRIDE, file: "code/modules/wiremod/components/utility/delay.dm", line: 38);
        }
        else
        {
            if (!Dm.eq(timer_id, TIMER_ID_NULL))
            {
                deltimer(timer_id);
                timer_id = TIMER_ID_NULL;
            }
            output.set_output(trigger.value);
        }
    }

    // tg: code/modules/wiremod/components/utility/delay.dm:45
    [ShouldNotSleep]
    public virtual void interrupt_timer(datum_port_input port = null)
    {
        if (!Dm.eq(timer_id, TIMER_ID_NULL))
        {
            deltimer(timer_id);
            timer_id = TIMER_ID_NULL;
        }
    }
}
