// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/save_shell.dm
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
 * # Save Shell Component
 *
 * A component that saves and loads a shell for the integrated circuit.
 */
// tg: code/modules/wiremod/components/admin/save_shell.dm:6
public partial class obj_item_circuit_component_save_shell : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/save_shell.dm:17
    public override async DmTask populate_ports()
    {
        on_loaded = add_output_port("On Loaded", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/admin/save_shell.dm:20
    public override async DmTask<object> add_to(obj_item_integrated_circuit added_to = null)
    {
        object dot = await base.add_to(added_to);
        RegisterSignal(added_to, COMSIG_CIRCUIT_POST_LOAD, nameof(on_post_load));
        RegisterSignal(added_to, COMSIG_CIRCUIT_PRE_SAVE_TO_JSON, nameof(on_pre_save_to_json));
        return dot;
    }

    // tg: code/modules/wiremod/components/admin/save_shell.dm:25
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        UnregisterSignal(removed_from, Dm.list(COMSIG_CIRCUIT_POST_LOAD, COMSIG_CIRCUIT_PRE_SAVE_TO_JSON));
        base.removed_from(removed_from);
    }

    // tg: code/modules/wiremod/components/admin/save_shell.dm:29
    [SignalHandler]
    public virtual void on_post_load(datum source = null)
    {
        datum_component_shell shell_component = loaded_shell.GetComponent(typeof(datum_component_shell)) as datum_component_shell;
        if (!(shell_component is datum_component_shell))
            Waiting.NoSleep(loaded_shell.AddComponent(typeof(datum_component_shell), Dm.named("starting_circuit", parent)));
        else
        {
            QDEL_NULL(ref shell_component.attached_circuit);
            Waiting.NoSleep(shell_component.attach_circuit(parent));
        }
        on_loaded.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/admin/save_shell.dm:39
    [SignalHandler]
    public virtual void on_pre_save_to_json(datum source = null, DmList general_data = null)
    {
        // We're custom saving the shell, disable any USB cable connections and default shell components.
        general_data["external_objects"] = Dm.list();
    }

    // tg: code/modules/wiremod/components/admin/save_shell.dm:44
    public override void save_data_to_list(DmList component_data = null)
    {
        base.save_data_to_list(component_data);
        atom_movable shell = parent.shell;
        component_data["shell_type"] = shell.type;
    }

    // tg: code/modules/wiremod/components/admin/save_shell.dm:49
    public override async DmTask load_data_from_list(DmList component_data = null)
    {
        if (parent.shell != null)
            return;

        Type shell_type = text2path(component_data["shell_type"] as string);
        if (shell_type == null || !ispath(shell_type, typeof(atom)))
        {
            await base.load_data_from_list(component_data);
            return;
        }

        loaded_shell = Dm.@new(shell_type, drop_location()) as atom_movable;
        log_admin_circuit(Dm.text("[] spawned in [] at [].", parent.get_creator(), shell_type, ADMIN_COORDJMP(loaded_shell)));
        if (loaded_shell == null)
            return;
        loaded_shell.datum_flags = Dm.bitor(loaded_shell.datum_flags, DF_VAR_EDITED);
        await base.load_data_from_list(component_data);
    }
}
