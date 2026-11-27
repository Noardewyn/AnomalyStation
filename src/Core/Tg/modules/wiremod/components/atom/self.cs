// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/self.dm
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
 * # Self Component
 *
 * Return the current shell.
 */
// tg: code/modules/wiremod/components/atom/self.dm:6
public partial class obj_item_circuit_component_self : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/self.dm:17
    public override async DmTask populate_ports()
    {
        output = add_output_port("Shell", PORT_TYPE_ATOM) as datum_port_output;
        shell_received = add_output_port("Shell Updated", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/atom/self.dm:21
    public override async DmTask register_shell(atom_movable shell = null)
    {
        output.set_output(shell);
        shell_received.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/atom/self.dm:25
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        output.set_output(null);
        shell_received.set_output(COMPONENT_SIGNAL);
    }
}
