// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/clock.dm
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
 * # Clock Component
 *
 * Fires every tick of the circuit timer SS
 */
// tg: code/modules/wiremod/components/utility/clock.dm:6
public partial class obj_item_circuit_component_clock : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/utility/clock.dm:17
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Clock Interval: []", DisplayTimeText(COMP_CLOCK_DELAY)), "orange", "clock"));
        return dot;
    }

    // tg: code/modules/wiremod/components/utility/clock.dm:21
    public override async DmTask populate_ports()
    {
        on = add_input_port("On", PORT_TYPE_BOOLEAN) as datum_port_input;

        signal = add_output_port("Signal", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/utility/clock.dm:26
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (Dm.truthy(on.value))
            start_process();
        else
            stop_process();
        return null;
    }

    // tg: code/modules/wiremod/components/utility/clock.dm:33
    public override object Destroy(float? force = null)
    {
        stop_process();
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/components/utility/clock.dm:37
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        signal.set_output(COMPONENT_SIGNAL);
        return null;
    }

    /**
     * Adds the component to the SSclock_component process list
     *
     * Starts ticking to send signals between periods of time
     */
    // tg: code/modules/wiremod/components/utility/clock.dm:45
    public virtual void start_process()
    {
        if (SSclock_component != null) // rules 10.6: a test harness world may lack the subsystem
            START_PROCESSING(SSclock_component, this);
    }

    /**
     * Removes the component to the SSclock_component process list
     *
     * Signals stop getting sent.
     */
    // tg: code/modules/wiremod/components/utility/clock.dm:53
    public virtual void stop_process()
    {
        if (SSclock_component != null) // rules 10.6: a test harness world may lack the subsystem
            STOP_PROCESSING(SSclock_component, this);
    }
}
