// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/spawn.dm
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
 * # Spawn Atom Component
 *
 * Spawns an atom.
 */
// tg: code/modules/wiremod/components/admin/spawn.dm:6
public partial class obj_item_circuit_component_spawn_atom : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/spawn.dm:25
    public override async DmTask populate_ports()
    {
        input_path = add_input_port("Type", PORT_TYPE_ANY) as datum_port_input;
        spawn_at = add_input_port("Spawn At", PORT_TYPE_ATOM) as datum_port_input;
        parameters = add_input_port("Parameters", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;

        spawned_atom = add_output_port("Spawned Atom", PORT_TYPE_ATOM) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/admin/spawn.dm:32
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object typepath = input_path.value;

        if (!ispath(typepath, typeof(atom)))
            return null;

        DmList @params = parameters.value as DmList;
        if (@params == null)
            @params = Dm.list();

        DmList resolved_params = recursive_list_resolve(@params) as DmList;

        resolved_params.Insert(1, spawn_at.value);

        log_admin_circuit(Dm.text("[] spawned in [] with parameters \\[[]\\].", parent.get_creator(), typepath, resolved_params.Join(", ")));
        atom spawned = Dm.@new((Type)typepath, Dm.arglist(resolved_params)) as atom;
        spawned.datum_flags = Dm.bitor(spawned.datum_flags, DF_VAR_EDITED);
        spawned_atom.set_output(spawned);
        return null;
    }
}
