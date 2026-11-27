// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/pull.dm
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
 * # Pull Component
 *
 * Tells the shell to start pulling on a designated atom. Only works on movable shells.
 */
// tg: code/modules/wiremod/components/action/pull.dm:6
public partial class obj_item_circuit_component_pull : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/pull.dm:16
    public override async DmTask populate_ports()
    {
        target = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/action/pull.dm:19
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        atom target_atom = target.value as atom;
        if (target_atom == null)
            return null;

        mob shell = parent.shell as mob;
        if (!(shell is mob) || get_dist(shell, target_atom) > 1 || shell.z != target_atom.z)
            return null;

        Waiting.Detach(shell.start_pulling(target_atom as atom_movable));
        return null;
    }
}
