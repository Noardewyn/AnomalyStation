// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/bci/install_detector.dm
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
 * # Install Detector Component
 *
 * Detects when a BCI, implant, or similar such circuit is installed/removed.
 * Requires a shell that gets inserted into a mob, such as a BCI or implant.
 */
// tg: code/modules/wiremod/components/bci/install_detector.dm:8
public partial class obj_item_circuit_component_install_detector : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/bci/install_detector.dm:19
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        current_state = add_output_port("Current State", PORT_TYPE_BOOLEAN) as datum_port_output;
        implanted = add_output_port("Implanted", PORT_TYPE_SIGNAL) as datum_port_output;
        removed = add_output_port("Removed", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/bci/install_detector.dm:25
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (istype(shell, typeof(obj_item_organ_cyberimp_bci)))
        {
            RegisterSignal(shell, COMSIG_ORGAN_IMPLANTED, nameof(implanted__proc));
            RegisterSignal(shell, COMSIG_ORGAN_REMOVED, nameof(removed__proc));
        }
        if (istype(shell, typeof(obj_item_implant)))
        {
            RegisterSignal(shell, COMSIG_IMPLANT_IMPLANTED, nameof(implanted__proc));
            RegisterSignal(shell, COMSIG_IMPLANT_REMOVED, nameof(removed__proc));
        }
    }

    // tg: code/modules/wiremod/components/bci/install_detector.dm:34
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        await base.unregister_shell(shell);
        if (istype(shell, typeof(obj_item_organ_cyberimp_bci)))
        {
            UnregisterSignal(shell, Dm.list(
                COMSIG_ORGAN_IMPLANTED,
                COMSIG_ORGAN_REMOVED));
        }
        if (istype(shell, typeof(obj_item_implant)))
        {
            UnregisterSignal(shell, Dm.list(
                COMSIG_IMPLANT_IMPLANTED,
                COMSIG_IMPLANT_REMOVED));
        }
    }

    // tg: code/modules/wiremod/components/bci/install_detector.dm:47
    [SignalHandler]
    public virtual void implanted__proc(datum source = null, mob_living owner = null)
    {
        current_state.set_output(TRUE);
        implanted.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/bci/install_detector.dm:52
    [SignalHandler]
    public virtual void removed__proc(datum source = null, mob_living owner = null)
    {
        current_state.set_output(FALSE);
        removed.set_output(COMPONENT_SIGNAL);
    }
}
