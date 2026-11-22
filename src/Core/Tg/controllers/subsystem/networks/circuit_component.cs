// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/networks/circuit_component.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the queue that carries a circuit's port values from an output to its connected inputs
// one subsystem fire later, and the instant execution of the filter component. The var block is generated (D111).

// tg: code/controllers/subsystem/networks/circuit_component.dm:1 SUBSYSTEM_DEF(circuit_component) (the var block is generated)
public partial class datum_controller_subsystem_circuit_component : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(circuit_component) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SScircuit_component)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/networks/circuit_component.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SScircuit_component != this)
        {
            if (SScircuit_component is datum_controller_subsystem_circuit_component)
            {
                Recover();
                qdel(SScircuit_component);
            }
            SScircuit_component = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/networks/circuit_component.dm:17
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (!Dm.truthy(resumed))
        {
            currentrun = callbacks_to_invoke.Copy();
            callbacks_to_invoke.Cut();
        }

        while (Dm.truthy(length(currentrun)))
        {
            datum_callback to_call = currentrun[1] as datum_callback;
            currentrun.Cut(1, 2);

            if (QDELETED(to_call))
                continue;

            to_call.user = null;
            to_call.InvokeAsync();

            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    /**
     * Adds a callback to be invoked when the next fire() is done. Used by the integrated circuit system.
     *
     * Prevents race conditions as it acts like a queue system.
     * Those that registered first will be executed first and those registered last will be executed last.
     */
    // tg: code/controllers/subsystem/networks/circuit_component.dm:41
    public virtual void add_callback(datum_port input = null, datum_callback to_call = null)
    {
        if (Dm.eq(instant_run_tick, world.time) && (TICK_USAGE() - instant_run_start_cpu_usage) <= instant_run_max_cpu_usage)
        {
            Dm.append(instant_run_callbacks_to_run, to_call);
            return;
        }

        Dm.append(callbacks_to_invoke, to_call);
    }

    /// Queues any callbacks to be executed instantly instead of using the subsystem.
    // tg: code/controllers/subsystem/networks/circuit_component.dm:49
    public virtual void queue_instant_run(object start_cpu_time = null)
    {
        if (Dm.truthy(instant_run_tick))
        {
            Dm.append(instant_run_stack, Dm.list(instant_run_callbacks_to_run));
            // If we're already instantly executing, don't change the start_cpu_time.
            start_cpu_time = instant_run_start_cpu_usage;
        }

        if (!Dm.truthy(start_cpu_time))
            start_cpu_time = TICK_USAGE();

        instant_run_tick = Dm.num(world.time);
        instant_run_start_cpu_usage = Dm.num(start_cpu_time);
        instant_run_callbacks_to_run = Dm.list();
    }

    /**
     * Instantly executes the stored callbacks and does this in a loop until there are no stored callbacks or it hits tick limit.
     *
     * Returns a list containing any values added by any input port.
     */
    // tg: code/controllers/subsystem/networks/circuit_component.dm:67
    public virtual object execute_instant_run()
    {
        DmList received_inputs = Dm.list();
        while (Dm.truthy(length(instant_run_callbacks_to_run)))
        {
            DmList instant_run_currentrun = instant_run_callbacks_to_run;
            instant_run_callbacks_to_run = Dm.list();
            while (Dm.truthy(length(instant_run_currentrun)))
            {
                datum_callback to_call = instant_run_currentrun[1] as datum_callback;
                instant_run_currentrun.Cut(1, 2);
                to_call.user = null;
                to_call.InvokeAsync(received_inputs);
            }
        }

        if (Dm.truthy(length(instant_run_stack)))
            instant_run_callbacks_to_run = pop(instant_run_stack) as DmList;
        else
            instant_run_tick = 0;

        if ((TICK_USAGE() - instant_run_start_cpu_usage) <= instant_run_max_cpu_usage)
            return received_inputs;
        else
            return null;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/networks/circuit_component.dm:1  SUBSYSTEM_DEF(circuit_component): GLOBAL_REAL(SScircuit_component, /datum/controller/subsystem/circuit_component)
    internal datum_controller_subsystem_circuit_component SScircuit_component;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_circuit_component SScircuit_component => ref TgState.Current.SScircuit_component;
}
